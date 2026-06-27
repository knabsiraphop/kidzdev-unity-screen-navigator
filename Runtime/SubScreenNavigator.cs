using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.ScreenNavigator
{
    /// <summary>
    /// Stack-based screen navigator with push/pop history. Unlike a finite state machine (fixed graph,
    /// single current state), this keeps a true navigation stack: the same key can appear more than once
    /// (when the <see cref="INavScreenProvider{TKey}"/> returns a fresh screen instance per resolve), and
    /// <c>Pop</c> returns to whatever was beneath the top — like a mobile app's back stack.
    /// </summary>
    /// <typeparam name="TKey">Key type identifying a screen (enum, string, etc.).</typeparam>
    public sealed class SubScreenNavigator<TKey> : INavigator, IDisposable
    {
        private readonly INavScreenProvider<TKey> _provider;
        private readonly INavTransition _transition;
        private readonly NavGate _gate;
        private readonly List<NavEntry<TKey>> _stack = new List<NavEntry<TKey>>();
        private readonly CancellationTokenSource _lifetimeCts = new CancellationTokenSource();
        private readonly IEqualityComparer<TKey> _keyComparer;
        private bool _disposed;

        /// <summary>When <c>true</c>, drop/queue decisions and no-op pops are logged.</summary>
        public bool EnableLogging { get; set; }

        /// <summary>Optional custom log sink; falls back to <see cref="Debug.Log"/> when null.</summary>
        public Action<string> LogHandler { get; set; }

        /// <summary>Fired after a screen has been pushed (and its enter transition completed).</summary>
        public event Action<TKey> Pushed;

        /// <summary>Fired after a screen has been popped (and its exit transition completed). Fires once per popped screen.</summary>
        public event Action<TKey> Popped;

        /// <summary>Fired once after any successful navigation operation settles.</summary>
        public event Action Changed;

        /// <param name="provider">Resolves keys to screens and releases them on pop. Required.</param>
        /// <param name="transition">Animation seam; defaults to <see cref="InstantTransition"/> when null.</param>
        /// <param name="policy">Concurrency policy for overlapping requests.</param>
        /// <param name="comparer">Key equality comparer; defaults to <see cref="EqualityComparer{T}.Default"/>.</param>
        public SubScreenNavigator(
            INavScreenProvider<TKey> provider,
            INavTransition transition = null,
            NavQueuePolicy policy = NavQueuePolicy.DropWhileBusy,
            IEqualityComparer<TKey> comparer = null)
        {
            _provider    = provider ?? throw new ArgumentNullException(nameof(provider));
            _transition  = transition ?? new InstantTransition();
            _gate        = new NavGate(policy, Log);
            _keyComparer = comparer ?? EqualityComparer<TKey>.Default;
        }

        // ── State ───────────────────────────────────────────────────────────────────────

        /// <summary>The key of the top screen, or <c>default</c> when the stack is empty.</summary>
        public TKey Current => _stack.Count > 0 ? _stack[_stack.Count - 1].Key : default;

        /// <summary><c>true</c> when at least one screen is on the stack.</summary>
        public bool HasCurrent => _stack.Count > 0;

        /// <inheritdoc/>
        public int Depth => _stack.Count;

        /// <inheritdoc/>
        public bool CanGoBack => _stack.Count > 1;

        /// <inheritdoc/>
        public bool IsTransitioning => _gate.IsBusy;

        private NavEntry<TKey> Top => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

        /// <summary>Returns <c>true</c> if any screen on the stack has key <paramref name="key"/>.</summary>
        public bool Contains(TKey key)
        {
            for (int i = 0; i < _stack.Count; i++)
                if (_keyComparer.Equals(_stack[i].Key, key)) return true;
            return false;
        }

        /// <summary>
        /// Returns a fresh snapshot of the stack keys ordered bottom (root) → top. Useful for breadcrumbs.
        /// For hot paths prefer <see cref="CopyStackKeysTo"/> to avoid the allocation.
        /// </summary>
        public IReadOnlyList<TKey> GetStackKeys()
        {
            var keys = new List<TKey>(_stack.Count);
            for (int i = 0; i < _stack.Count; i++) keys.Add(_stack[i].Key);
            return keys;
        }

        /// <summary>
        /// Non-allocating variant of <see cref="GetStackKeys"/>: clears <paramref name="buffer"/> and
        /// fills it bottom (root) → top. Reuse the same list every <c>Changed</c> callback.
        /// </summary>
        public void CopyStackKeysTo(IList<TKey> buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            buffer.Clear();
            for (int i = 0; i < _stack.Count; i++) buffer.Add(_stack[i].Key);
        }

        // ── Operations ──────────────────────────────────────────────────────────────────

        /// <summary>Pushes <paramref name="key"/> onto the stack, animating it in over the current top.</summary>
        public UniTask PushAsync(TKey key, object arg = null, CancellationToken ct = default)
            => Execute(t => PushCoreAsync(key, arg, t), ct);

        private async UniTask PushCoreAsync(TKey key, object arg, CancellationToken token)
        {
            var from   = Top;
            var screen = await ResolveAsync(key, arg, token);
            GuardNotOnStack(key, screen);

            Activate(screen);
            screen.OnPushed(arg);
            from?.Screen.OnCovered();

            _stack.Add(new NavEntry<TKey>(key, screen, arg));

            await _transition.PlayAsync(from?.Screen, screen, NavDirection.Forward, token);

            if (from != null) Deactivate(from.Screen);
            Pushed?.Invoke(key);
            Changed?.Invoke();
        }

        /// <summary>Pops the top screen, animating it out and revealing the one beneath. No-op on an empty stack.</summary>
        public UniTask PopAsync(CancellationToken ct = default)
            => Execute(PopCoreAsync, ct);

        private async UniTask PopCoreAsync(CancellationToken token)
        {
            if (_stack.Count == 0) { Log("PopAsync ignored: the stack is empty."); return; }

            var popped = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            var newTop = Top;

            if (newTop != null) { Activate(newTop.Screen); newTop.Screen.OnRevealed(); }
            popped.Screen.OnPopped();

            await _transition.PlayAsync(popped.Screen, newTop?.Screen, NavDirection.Backward, token);

            Deactivate(popped.Screen);
            _provider.Release(popped.Key, popped.Screen);
            Popped?.Invoke(popped.Key);
            Changed?.Invoke();
        }

        /// <summary>Replaces the top screen with <paramref name="key"/> (stack depth unchanged). Pushes if the stack is empty.</summary>
        public UniTask ReplaceAsync(TKey key, object arg = null, CancellationToken ct = default)
            => Execute(t => ReplaceCoreAsync(key, arg, t), ct);

        private async UniTask ReplaceCoreAsync(TKey key, object arg, CancellationToken token)
        {
            var old    = Top;
            var screen = await ResolveAsync(key, arg, token);
            GuardNotOnStack(key, screen, ignoreIndex: _stack.Count - 1);

            Activate(screen);
            screen.OnPushed(arg);

            if (old != null) { _stack.RemoveAt(_stack.Count - 1); old.Screen.OnPopped(); }
            _stack.Add(new NavEntry<TKey>(key, screen, arg));

            await _transition.PlayAsync(old?.Screen, screen, NavDirection.Forward, token);

            if (old != null)
            {
                Deactivate(old.Screen);
                _provider.Release(old.Key, old.Screen);
                Popped?.Invoke(old.Key);
            }
            Pushed?.Invoke(key);
            Changed?.Invoke();
        }

        /// <summary>Pops every screen above the bottom one, animating the top down to the root.</summary>
        public UniTask PopToRootAsync(CancellationToken ct = default)
            => Execute(PopToRootCoreAsync, ct);

        private async UniTask PopToRootCoreAsync(CancellationToken token)
        {
            if (_stack.Count <= 1) { Log("PopToRootAsync ignored: already at (or below) the root."); return; }
            await CollapseAboveAsync(0, token);
        }

        /// <summary>
        /// Pops back to the nearest screen below the top whose key equals <paramref name="key"/>.
        /// No-op (logged) if no such screen exists below the current top.
        /// </summary>
        public UniTask PopToAsync(TKey key, CancellationToken ct = default)
            => Execute(t => PopToCoreAsync(key, t), ct);

        private async UniTask PopToCoreAsync(TKey key, CancellationToken token)
        {
            int targetIndex = -1;
            for (int i = _stack.Count - 2; i >= 0; i--)
            {
                if (_keyComparer.Equals(_stack[i].Key, key)) { targetIndex = i; break; }
            }
            if (targetIndex < 0)
            {
                Log($"PopToAsync ignored: no screen with key '{key}' exists below the current top.");
                return;
            }
            await CollapseAboveAsync(targetIndex, token);
        }

        private async UniTask CollapseAboveAsync(int targetIndex, CancellationToken token)
        {
            int firstVictim = targetIndex + 1;
            int victimCount = _stack.Count - firstVictim;

            var victims = new List<NavEntry<TKey>>(victimCount);
            for (int i = firstVictim; i < _stack.Count; i++) victims.Add(_stack[i]);
            _stack.RemoveRange(firstVictim, victimCount);

            var target = Top;
            Activate(target.Screen);
            target.Screen.OnRevealed();

            // Intermediates: tear down silently, no animation.
            for (int i = 0; i < victims.Count - 1; i++)
            {
                var v = victims[i];
                v.Screen.OnPopped();
                Deactivate(v.Screen);
                _provider.Release(v.Key, v.Screen);
                Popped?.Invoke(v.Key);
            }

            // Visible top victim: animate down to target.
            var topVictim = victims[victims.Count - 1];
            topVictim.Screen.OnPopped();
            await _transition.PlayAsync(topVictim.Screen, target.Screen, NavDirection.Backward, token);
            Deactivate(topVictim.Screen);
            _provider.Release(topVictim.Key, topVictim.Screen);
            Popped?.Invoke(topVictim.Key);

            Changed?.Invoke();
        }

        /// <inheritdoc/>
        public async UniTask<bool> HandleBackAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
            if (_gate.IsBusy) return true;
            var top = Top;
            if (top != null && top.Screen.OnBackPressed()) return true;
            if (!CanGoBack) return false;
            await PopAsync(ct);
            return true;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Centralises the ThrowIfDisposed + linked-CTS + gate pattern shared by every public operation.
        /// The <c>async</c> keeps the linked CTS alive for the full lifetime of the gate's await.
        /// </summary>
        private async UniTask Execute(Func<CancellationToken, UniTask> core, CancellationToken ct)
        {
            ThrowIfDisposed();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetimeCts.Token);
            await _gate.RunAsync(core, linked.Token);
        }

        private async UniTask<INavScreen> ResolveAsync(TKey key, object arg, CancellationToken token)
        {
            var screen = await _provider.ResolveAsync(key, arg, token);
            if (screen == null)
                throw new InvalidOperationException($"INavScreenProvider returned null for key '{key}'.");
            if (screen.Root == null)
                throw new InvalidOperationException($"Screen for key '{key}' has a null Root GameObject.");
            return screen;
        }

        private void GuardNotOnStack(TKey key, INavScreen screen, int ignoreIndex = -1)
        {
            for (int i = 0; i < _stack.Count; i++)
            {
                if (i == ignoreIndex) continue;
                if (ReferenceEquals(_stack[i].Screen, screen))
                    throw new InvalidOperationException(
                        $"The screen instance for key '{key}' is already on the stack. " +
                        "A shared-instance provider cannot place the same screen at two stack positions. " +
                        "Use PrefabScreenProvider or ResourcesScreenProvider to allow a key to repeat.");
            }
        }

        private static void Activate(INavScreen screen)
        {
            if (screen?.Root != null && !screen.Root.activeSelf) screen.Root.SetActive(true);
        }

        private static void Deactivate(INavScreen screen)
        {
            if (screen?.Root != null && screen.Root.activeSelf) screen.Root.SetActive(false);
        }

        private void Log(string message)
        {
            if (!EnableLogging) return;
            if (LogHandler != null) LogHandler(message);
            else Debug.Log($"[SubScreenNavigator<{typeof(TKey).Name}>] {message}");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SubScreenNavigator<TKey>));
        }

        /// <summary>
        /// Cancels any in-flight transition and releases every screen still on the stack through the provider.
        /// Safe to call from <c>OnDestroy</c>.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();
            for (int i = 0; i < _stack.Count; i++)
                _provider.Release(_stack[i].Key, _stack[i].Screen);
            _stack.Clear();
        }
    }
}
