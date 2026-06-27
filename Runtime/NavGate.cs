using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace KidzDev.Unity.ScreenNavigator
{
    /// <summary>
    /// Serializes navigation operations so only one transition runs at a time. This is the same
    /// single-in-flight discipline used by the KidzDev async state machine, specialized for the
    /// navigator's stack model.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><see cref="NavQueuePolicy.DropWhileBusy"/>: a request that arrives while busy is dropped.</item>
    ///   <item><see cref="NavQueuePolicy.Queue"/>: requests are FIFO-queued and drained iteratively by the
    ///         in-flight call — never recursively.</item>
    ///   <item>Re-entrant calls from inside an operation (e.g. a lifecycle callback) are queued or dropped,
    ///         never run nested.</item>
    /// </list>
    /// Main-thread only; not guarded against concurrent OS-thread access.
    /// </remarks>
    internal sealed class NavGate
    {
        private readonly NavQueuePolicy _policy;
        private readonly Action<string> _log;
        private readonly Queue<Func<CancellationToken, UniTask>> _pending;

        public NavGate(NavQueuePolicy policy, Action<string> log = null)
        {
            _policy = policy;
            _log = log;
            _pending = policy == NavQueuePolicy.Queue
                ? new Queue<Func<CancellationToken, UniTask>>()
                : null;
        }

        /// <summary><c>true</c> while an operation is running.</summary>
        public bool IsBusy { get; private set; }

        /// <summary>
        /// Runs <paramref name="op"/> if idle; otherwise applies the policy (drop or queue).
        /// When queued, the returned task completes immediately — the operation runs later, drained by
        /// the in-flight call. When run inline, the returned task completes when the operation finishes.
        /// </summary>
        public async UniTask RunAsync(Func<CancellationToken, UniTask> op, CancellationToken ct)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            if (IsBusy)
            {
                if (_policy == NavQueuePolicy.Queue)
                {
                    _pending.Enqueue(op);
                }
                else
                {
                    _log?.Invoke("Navigation request dropped: a transition is already in flight (DropWhileBusy).");
                }
                return;
            }

            IsBusy = true;
            try
            {
                await op(ct);

                if (_policy == NavQueuePolicy.Queue)
                {
                    // Drain iteratively so a chain of queued operations never recurses.
                    while (_pending.Count > 0)
                    {
                        var next = _pending.Dequeue();
                        await next(ct);
                    }
                }
            }
            finally
            {
                IsBusy = false;
                _pending?.Clear(); // on exception, abandon any queued work rather than running it in a faulted context
            }
        }
    }
}
