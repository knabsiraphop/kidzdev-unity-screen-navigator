using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.ScreenNavigator.Tests
{
    internal enum NavKey { Inbox, Message, Confirm, Other }

    /// <summary>An <see cref="INavScreen"/> backed by a real GameObject, counting every lifecycle call.</summary>
    internal sealed class TestScreen : INavScreen
    {
        public GameObject Root { get; }
        public int PushedCount, RevealedCount, CoveredCount, PoppedCount, BackPressedCount;
        public object LastArg;
        public bool ConsumesBack;

        public TestScreen(string name)
        {
            Root = new GameObject(name);
            Root.SetActive(false);
        }

        public void OnPushed(object arg) { PushedCount++; LastArg = arg; }
        public void OnRevealed() { RevealedCount++; }
        public void OnCovered() { CoveredCount++; }
        public void OnPopped() { PoppedCount++; }
        public bool OnBackPressed() { BackPressedCount++; return ConsumesBack; }

        public bool IsActive => Root != null && Root.activeSelf;
    }

    /// <summary>Registry-style provider over <see cref="TestScreen"/>, counting resolves and releases.</summary>
    internal sealed class TestProvider : INavScreenProvider<NavKey>
    {
        private readonly Dictionary<NavKey, INavScreen> _screens = new Dictionary<NavKey, INavScreen>();
        public readonly Dictionary<NavKey, int> ResolveCounts = new Dictionary<NavKey, int>();
        public readonly Dictionary<NavKey, int> ReleaseCounts = new Dictionary<NavKey, int>();
        public bool ReturnNull;

        /// <summary>When set, <see cref="ResolveAsync"/> awaits this (honoring the token) before returning —
        /// lets a test suspend a resolve and then cancel it.</summary>
        public UniTaskCompletionSource ResolveGate;

        public TestProvider Register(NavKey key, INavScreen screen)
        {
            _screens[key] = screen;
            return this;
        }

        public async UniTask<INavScreen> ResolveAsync(NavKey key, object arg, CancellationToken ct)
        {
            ResolveCounts[key] = ResolveCounts.TryGetValue(key, out var c) ? c + 1 : 1;
            if (ResolveGate != null) await ResolveGate.Task.AttachExternalCancellation(ct);
            if (ReturnNull) return null;
            return _screens[key];
        }

        public void Release(NavKey key, INavScreen screen)
        {
            ReleaseCounts[key] = ReleaseCounts.TryGetValue(key, out var c) ? c + 1 : 1;
        }
    }

    /// <summary>
    /// A transition whose completion is controlled by the test. Each call returns a pending task until
    /// <see cref="Release"/> opens the gate; thereafter calls complete synchronously. Uses no frame
    /// pumping, so it works in EditMode <c>[Test]</c> methods.
    /// </summary>
    internal sealed class GatedTransition : INavTransition
    {
        public int Calls;
        public bool Open;
        private readonly List<UniTaskCompletionSource> _pending = new List<UniTaskCompletionSource>();

        public UniTask PlayAsync(INavScreen from, INavScreen to, NavDirection direction, CancellationToken ct)
        {
            Calls++;
            if (Open) return UniTask.CompletedTask;
            var tcs = new UniTaskCompletionSource();
            _pending.Add(tcs);
            return tcs.Task;
        }

        public void Release()
        {
            Open = true;
            var copy = _pending.ToArray();
            _pending.Clear();
            foreach (var t in copy) t.TrySetResult();
        }
    }
}
