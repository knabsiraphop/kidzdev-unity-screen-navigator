using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KidzDev.Unity.ScreenNavigator.Tests
{
    internal enum PmKey { A, B }

    internal sealed class PmScreen : INavScreen
    {
        public GameObject Root { get; }
        public CanvasGroup Group { get; }

        public PmScreen(string name, bool withGroup = true)
        {
            Root = new GameObject(name);
            if (withGroup) Group = Root.AddComponent<CanvasGroup>();
            Root.SetActive(false);
        }

        public void OnPushed(object arg) { }
        public void OnRevealed() { }
        public void OnCovered() { }
        public void OnPopped() { }
        public bool OnBackPressed() => false;
    }

    internal sealed class PmProvider : INavScreenProvider<PmKey>
    {
        private readonly Dictionary<PmKey, INavScreen> _screens = new Dictionary<PmKey, INavScreen>();
        public PmProvider Register(PmKey key, INavScreen screen) { _screens[key] = screen; return this; }
        public UniTask<INavScreen> ResolveAsync(PmKey key, object arg, CancellationToken ct) => UniTask.FromResult(_screens[key]);
        public void Release(PmKey key, INavScreen screen) { }
    }

    [TestFixture]
    internal sealed class NavTransitionPlayModeTests
    {
        [UnityTest]
        public IEnumerator Fade_PushThenPop_EndStatesAreCorrect() => UniTask.ToCoroutine(async () =>
        {
            var a = new PmScreen("A");
            var b = new PmScreen("B");
            var provider = new PmProvider().Register(PmKey.A, a).Register(PmKey.B, b);
            var nav = new SubScreenNavigator<PmKey>(provider, new CanvasGroupFadeTransition(0.05f));

            try
            {
                await nav.PushAsync(PmKey.A);
                Assert.IsTrue(a.Root.activeSelf);
                Assert.AreEqual(1f, a.Group.alpha, 0.001f);
                Assert.IsTrue(a.Group.blocksRaycasts);

                await nav.PushAsync(PmKey.B);
                Assert.IsTrue(b.Root.activeSelf);
                Assert.AreEqual(1f, b.Group.alpha, 0.001f);
                Assert.IsFalse(a.Root.activeSelf, "covered screen deactivated after fade");
                Assert.AreEqual(0f, a.Group.alpha, 0.001f);

                await nav.PopAsync();
                Assert.IsTrue(a.Root.activeSelf, "revealed screen active again");
                Assert.AreEqual(1f, a.Group.alpha, 0.001f);
                Assert.IsTrue(a.Group.blocksRaycasts);
                Assert.IsFalse(b.Root.activeSelf);
            }
            finally
            {
                nav.Dispose();
                Object.Destroy(a.Root);
                Object.Destroy(b.Root);
            }
        });

        [UnityTest]
        public IEnumerator Fade_AddsCanvasGroup_WhenMissing() => UniTask.ToCoroutine(async () =>
        {
            var a = new PmScreen("A", withGroup: false);
            var provider = new PmProvider().Register(PmKey.A, a);
            var nav = new SubScreenNavigator<PmKey>(provider, new CanvasGroupFadeTransition(0.05f));

            try
            {
                await nav.PushAsync(PmKey.A);
                var cg = a.Root.GetComponent<CanvasGroup>();
                Assert.IsNotNull(cg, "fade transition should add a CanvasGroup on demand");
                Assert.AreEqual(1f, cg.alpha, 0.001f);
            }
            finally
            {
                nav.Dispose();
                Object.Destroy(a.Root);
            }
        });

        [UnityTest]
        public IEnumerator IsTransitioning_TrueDuringFade_FalseAfter() => UniTask.ToCoroutine(async () =>
        {
            var a = new PmScreen("A");
            var provider = new PmProvider().Register(PmKey.A, a);
            var nav = new SubScreenNavigator<PmKey>(provider, new CanvasGroupFadeTransition(0.1f));

            try
            {
                var pushing = nav.PushAsync(PmKey.A);
                Assert.IsTrue(nav.IsTransitioning, "transition in flight");
                await pushing;
                Assert.IsFalse(nav.IsTransitioning, "transition settled");
            }
            finally
            {
                nav.Dispose();
                Object.Destroy(a.Root);
            }
        });

        [UnityTest]
        public IEnumerator Dispose_CancelsInFlightFade() => UniTask.ToCoroutine(async () =>
        {
            var a = new PmScreen("A");
            var provider = new PmProvider().Register(PmKey.A, a);
            var nav = new SubScreenNavigator<PmKey>(provider, new CanvasGroupFadeTransition(5f)); // long fade

            var pushing = nav.PushAsync(PmKey.A);
            await UniTask.NextFrame();
            Assert.IsTrue(nav.IsTransitioning, "long fade is in flight");

            nav.Dispose(); // must cancel the awaiting fade via the lifetime token

            bool cancelled = false;
            try { await pushing; }
            catch (System.OperationCanceledException) { cancelled = true; }
            Assert.IsTrue(cancelled, "Dispose must cancel the in-flight fade");
            Assert.IsFalse(nav.IsTransitioning);

            Object.Destroy(a.Root);
        });

        [UnityTest]
        public IEnumerator CallerToken_CancelsInFlightFade() => UniTask.ToCoroutine(async () =>
        {
            var a = new PmScreen("A");
            var provider = new PmProvider().Register(PmKey.A, a);
            var nav = new SubScreenNavigator<PmKey>(provider, new CanvasGroupFadeTransition(5f));
            var cts = new CancellationTokenSource();

            var pushing = nav.PushAsync(PmKey.A, ct: cts.Token);
            await UniTask.NextFrame();
            Assert.IsTrue(nav.IsTransitioning);

            cts.Cancel(); // caller token must reach the awaiting fade

            bool cancelled = false;
            try { await pushing; }
            catch (System.OperationCanceledException) { cancelled = true; }
            Assert.IsTrue(cancelled, "caller token must cancel the in-flight fade");
            Assert.IsFalse(nav.IsTransitioning);

            nav.Dispose();
            Object.Destroy(a.Root);
        });
    }
}
