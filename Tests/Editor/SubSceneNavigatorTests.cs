using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace KidzDev.Unity.SceneNavigator.Tests
{
    [TestFixture]
    internal sealed class SubSceneNavigatorTests
    {
        private readonly List<TestScreen> _screens = new List<TestScreen>();

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _screens)
                if (s.Root != null) UnityEngine.Object.DestroyImmediate(s.Root);
            _screens.Clear();
        }

        private TestScreen Screen(string name)
        {
            var s = new TestScreen(name);
            _screens.Add(s);
            return s;
        }

        private (SubSceneNavigator<NavKey> nav, TestProvider provider) Build(
            INavTransition transition = null,
            NavQueuePolicy policy = NavQueuePolicy.DropWhileBusy,
            params (NavKey key, TestScreen screen)[] screens)
        {
            var provider = new TestProvider();
            foreach (var (key, screen) in screens) provider.Register(key, screen);
            var nav = new SubSceneNavigator<NavKey>(provider, transition ?? new InstantTransition(), policy);
            return (nav, provider);
        }

        private static void Run(UniTask task) => task.GetAwaiter().GetResult();

        // ── Push ──────────────────────────────────────────────────────────────────────

        [Test]
        public void Push_AddsToStack_ActivatesRoot_FiresPushed()
        {
            var a = Screen("A");
            var (nav, _) = Build(screens: (NavKey.Inbox, a));
            NavKey? pushed = null;
            nav.Pushed += k => pushed = k;

            Run(nav.PushAsync(NavKey.Inbox));

            Assert.AreEqual(1, nav.Depth);
            Assert.AreEqual(NavKey.Inbox, nav.Current);
            Assert.IsTrue(nav.HasCurrent);
            Assert.IsTrue(a.IsActive);
            Assert.AreEqual(1, a.PushedCount);
            Assert.AreEqual(NavKey.Inbox, pushed);
        }

        [Test]
        public void Push_CoversAndDeactivatesPrevious()
        {
            var a = Screen("A");
            var b = Screen("B");
            var (nav, _) = Build(screens: new[] { (NavKey.Inbox, a), (NavKey.Message, b) });

            Run(nav.PushAsync(NavKey.Inbox));
            Run(nav.PushAsync(NavKey.Message));

            Assert.AreEqual(2, nav.Depth);
            Assert.AreEqual(NavKey.Message, nav.Current);
            Assert.IsFalse(a.IsActive, "covered screen should be deactivated");
            Assert.IsTrue(b.IsActive);
            Assert.AreEqual(1, a.CoveredCount);
        }

        [Test]
        public void Push_PassesArgToScreen()
        {
            var a = Screen("A");
            var (nav, _) = Build(screens: (NavKey.Message, a));

            Run(nav.PushAsync(NavKey.Message, arg: 42));

            Assert.AreEqual(42, a.LastArg);
        }

        // ── Pop ───────────────────────────────────────────────────────────────────────

        [Test]
        public void Pop_RemovesTop_RevealsPrevious_ReleasesPopped_FiresPopped()
        {
            var a = Screen("A");
            var b = Screen("B");
            var (nav, provider) = Build(screens: new[] { (NavKey.Inbox, a), (NavKey.Message, b) });
            NavKey? popped = null;
            nav.Popped += k => popped = k;

            Run(nav.PushAsync(NavKey.Inbox));
            Run(nav.PushAsync(NavKey.Message));
            Run(nav.PopAsync());

            Assert.AreEqual(1, nav.Depth);
            Assert.AreEqual(NavKey.Inbox, nav.Current);
            Assert.IsTrue(a.IsActive);
            Assert.IsFalse(b.IsActive);
            Assert.AreEqual(1, a.RevealedCount);
            Assert.AreEqual(1, b.PoppedCount);
            Assert.AreEqual(1, provider.ReleaseCounts[NavKey.Message]);
            Assert.AreEqual(NavKey.Message, popped);
        }

        [Test]
        public void Pop_OnEmpty_IsNoOp()
        {
            var (nav, _) = Build();
            Assert.DoesNotThrow(() => Run(nav.PopAsync()));
            Assert.AreEqual(0, nav.Depth);
        }

        [Test]
        public void Pop_LastScreen_EmptiesStack()
        {
            var a = Screen("A");
            var (nav, _) = Build(screens: (NavKey.Inbox, a));
            Run(nav.PushAsync(NavKey.Inbox));
            Run(nav.PopAsync());

            Assert.AreEqual(0, nav.Depth);
            Assert.IsFalse(nav.HasCurrent);
            Assert.IsFalse(a.IsActive);
        }

        // ── Replace ───────────────────────────────────────────────────────────────────

        [Test]
        public void Replace_KeepsDepth_SwapsTop_ReleasesOld()
        {
            var a = Screen("A");
            var b = Screen("B");
            var (nav, provider) = Build(screens: new[] { (NavKey.Inbox, a), (NavKey.Message, b) });

            Run(nav.PushAsync(NavKey.Inbox));
            Run(nav.ReplaceAsync(NavKey.Message));

            Assert.AreEqual(1, nav.Depth);
            Assert.AreEqual(NavKey.Message, nav.Current);
            Assert.IsFalse(a.IsActive);
            Assert.IsTrue(b.IsActive);
            Assert.AreEqual(1, a.PoppedCount);
            Assert.AreEqual(1, provider.ReleaseCounts[NavKey.Inbox]);
        }

        [Test]
        public void Replace_OnEmpty_Pushes()
        {
            var a = Screen("A");
            var (nav, _) = Build(screens: (NavKey.Inbox, a));
            Run(nav.ReplaceAsync(NavKey.Inbox));

            Assert.AreEqual(1, nav.Depth);
            Assert.AreEqual(NavKey.Inbox, nav.Current);
        }

        // ── PopToRoot / PopTo ───────────────────────────────────────────────────────────

        [Test]
        public void PopToRoot_CollapsesToBottom_ReleasesIntermediates()
        {
            var a = Screen("A");
            var b = Screen("B");
            var c = Screen("C");
            var (nav, provider) = Build(screens: new[]
            {
                (NavKey.Inbox, a), (NavKey.Message, b), (NavKey.Confirm, c)
            });

            Run(nav.PushAsync(NavKey.Inbox));
            Run(nav.PushAsync(NavKey.Message));
            Run(nav.PushAsync(NavKey.Confirm));
            Run(nav.PopToRootAsync());

            Assert.AreEqual(1, nav.Depth);
            Assert.AreEqual(NavKey.Inbox, nav.Current);
            Assert.IsTrue(a.IsActive);
            Assert.AreEqual(1, provider.ReleaseCounts[NavKey.Message]);
            Assert.AreEqual(1, provider.ReleaseCounts[NavKey.Confirm]);
            Assert.AreEqual(1, b.PoppedCount);
            Assert.AreEqual(1, c.PoppedCount);
        }

        [Test]
        public void PopTo_CollapsesToKey()
        {
            var a = Screen("A");
            var b = Screen("B");
            var c = Screen("C");
            var (nav, provider) = Build(screens: new[]
            {
                (NavKey.Inbox, a), (NavKey.Message, b), (NavKey.Confirm, c)
            });

            Run(nav.PushAsync(NavKey.Inbox));
            Run(nav.PushAsync(NavKey.Message));
            Run(nav.PushAsync(NavKey.Confirm));
            Run(nav.PopToAsync(NavKey.Message));

            Assert.AreEqual(2, nav.Depth);
            Assert.AreEqual(NavKey.Message, nav.Current);
            Assert.IsTrue(b.IsActive);
            Assert.AreEqual(1, provider.ReleaseCounts[NavKey.Confirm]);
        }

        [Test]
        public void PopTo_MissingKey_IsNoOp()
        {
            var a = Screen("A");
            var b = Screen("B");
            var (nav, _) = Build(screens: new[] { (NavKey.Inbox, a), (NavKey.Message, b) });

            Run(nav.PushAsync(NavKey.Inbox));
            Run(nav.PushAsync(NavKey.Message));
            Run(nav.PopToAsync(NavKey.Confirm)); // not on stack

            Assert.AreEqual(2, nav.Depth);
            Assert.AreEqual(NavKey.Message, nav.Current);
        }

        // ── Back handling ───────────────────────────────────────────────────────────────

        [Test]
        public void HandleBack_ConsumedByScreen_DoesNotPop()
        {
            var a = Screen("A");
            var b = Screen("B");
            b.ConsumesBack = true;
            var (nav, _) = Build(screens: new[] { (NavKey.Inbox, a), (NavKey.Message, b) });

            Run(nav.PushAsync(NavKey.Inbox));
            Run(nav.PushAsync(NavKey.Message));
            bool handled = nav.HandleBackAsync().GetAwaiter().GetResult();

            Assert.IsTrue(handled);
            Assert.AreEqual(2, nav.Depth, "screen consumed the back press, no pop");
            Assert.AreEqual(1, b.BackPressedCount);
        }

        [Test]
        public void HandleBack_PopsWhenCanGoBack()
        {
            var a = Screen("A");
            var b = Screen("B");
            var (nav, _) = Build(screens: new[] { (NavKey.Inbox, a), (NavKey.Message, b) });

            Run(nav.PushAsync(NavKey.Inbox));
            Run(nav.PushAsync(NavKey.Message));
            bool handled = nav.HandleBackAsync().GetAwaiter().GetResult();

            Assert.IsTrue(handled);
            Assert.AreEqual(1, nav.Depth);
            Assert.AreEqual(NavKey.Inbox, nav.Current);
        }

        [Test]
        public void HandleBack_AtRoot_ReturnsFalse()
        {
            var a = Screen("A");
            var (nav, _) = Build(screens: (NavKey.Inbox, a));
            Run(nav.PushAsync(NavKey.Inbox));

            bool handled = nav.HandleBackAsync().GetAwaiter().GetResult();

            Assert.IsFalse(handled, "at root, back cannot be handled — caller may quit");
            Assert.AreEqual(1, nav.Depth);
        }

        [Test]
        public void HandleBack_DuringTransition_ReturnsTrue_NoPop()
        {
            var a = Screen("A");
            var gate = new GatedTransition();
            var (nav, _) = Build(transition: gate, screens: (NavKey.Inbox, a));

            var pushing = nav.PushAsync(NavKey.Inbox); // pending: gated transition in flight
            Assert.IsTrue(nav.IsTransitioning);

            bool handled = nav.HandleBackAsync().GetAwaiter().GetResult();
            Assert.IsTrue(handled, "back is swallowed while a transition is in flight");
            Assert.AreEqual(0, a.PoppedCount);

            gate.Release();
            Run(pushing);
        }

        // ── Concurrency policy ──────────────────────────────────────────────────────────

        [Test]
        public void DropWhileBusy_SecondOpDuringTransition_IsDropped()
        {
            var a = Screen("A");
            var b = Screen("B");
            var gate = new GatedTransition();
            var (nav, provider) = Build(transition: gate, policy: NavQueuePolicy.DropWhileBusy,
                screens: new[] { (NavKey.Inbox, a), (NavKey.Message, b) });

            var t1 = nav.PushAsync(NavKey.Inbox); // pending
            Assert.IsTrue(nav.IsTransitioning);
            var t2 = nav.PushAsync(NavKey.Message); // dropped

            gate.Release();
            Run(t1);
            Run(t2);

            Assert.AreEqual(1, nav.Depth);
            Assert.AreEqual(NavKey.Inbox, nav.Current);
            Assert.IsFalse(provider.ResolveCounts.ContainsKey(NavKey.Message), "dropped op must not resolve a screen");
        }

        [Test]
        public void Queue_SecondOpDuringTransition_RunsAfter()
        {
            var a = Screen("A");
            var b = Screen("B");
            var gate = new GatedTransition();
            var (nav, provider) = Build(transition: gate, policy: NavQueuePolicy.Queue,
                screens: new[] { (NavKey.Inbox, a), (NavKey.Message, b) });

            var t1 = nav.PushAsync(NavKey.Inbox); // pending
            Assert.IsTrue(nav.IsTransitioning);
            nav.PushAsync(NavKey.Message).Forget(); // queued

            gate.Release();
            Run(t1); // drains the queued push

            Assert.AreEqual(2, nav.Depth);
            Assert.AreEqual(NavKey.Message, nav.Current);
            Assert.AreEqual(1, provider.ResolveCounts[NavKey.Message]);
        }

        // ── Validation & disposal ───────────────────────────────────────────────────────

        [Test]
        public void Resolve_NullScreen_Throws()
        {
            var (nav, provider) = Build();
            provider.ReturnNull = true;
            Assert.Throws<InvalidOperationException>(() => Run(nav.PushAsync(NavKey.Inbox)));
        }

        [Test]
        public void Dispose_ReleasesRemaining_BlocksFurtherOps()
        {
            var a = Screen("A");
            var b = Screen("B");
            var (nav, provider) = Build(screens: new[] { (NavKey.Inbox, a), (NavKey.Message, b) });

            Run(nav.PushAsync(NavKey.Inbox));
            Run(nav.PushAsync(NavKey.Message));
            nav.Dispose();

            Assert.AreEqual(1, provider.ReleaseCounts[NavKey.Inbox]);
            Assert.AreEqual(1, provider.ReleaseCounts[NavKey.Message]);
            Assert.Throws<ObjectDisposedException>(() => Run(nav.PushAsync(NavKey.Inbox)));
        }

        [Test]
        public void CanGoBack_And_Depth_TrackStack()
        {
            var a = Screen("A");
            var b = Screen("B");
            var (nav, _) = Build(screens: new[] { (NavKey.Inbox, a), (NavKey.Message, b) });

            Assert.IsFalse(nav.CanGoBack);
            Run(nav.PushAsync(NavKey.Inbox));
            Assert.IsFalse(nav.CanGoBack);
            Run(nav.PushAsync(NavKey.Message));
            Assert.IsTrue(nav.CanGoBack);
        }
    }
}
