using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.ScreenNavigator
{
    /// <summary>
    /// Cross-fades screens using a <see cref="CanvasGroup"/> on each screen's root, with no third-party
    /// animation dependency (a bare per-frame alpha lerp). A <see cref="CanvasGroup"/> is added on demand
    /// if a screen's root doesn't already have one.
    /// </summary>
    /// <remarks>
    /// The incoming screen fades 0→1 while the outgoing screen fades 1→0; both run concurrently. Raycasts
    /// are blocked during the fade and restored on the survivor so input can't leak to a half-faded panel.
    /// </remarks>
    public sealed class CanvasGroupFadeTransition : INavTransition
    {
        private readonly float _duration;

        /// <param name="duration">Fade duration in seconds. Values &lt;= 0 make the transition instant.</param>
        public CanvasGroupFadeTransition(float duration = 0.2f)
        {
            _duration = duration;
        }

        /// <inheritdoc/>
        public async UniTask PlayAsync(INavScreen from, INavScreen to, NavDirection direction, CancellationToken ct)
        {
            var fromCg = GetCanvasGroup(from);
            var toCg = GetCanvasGroup(to);

            if (toCg != null)
            {
                toCg.alpha = 0f;
                toCg.blocksRaycasts = false;
            }
            if (fromCg != null) fromCg.blocksRaycasts = false;

            if (_duration <= 0f)
            {
                if (fromCg != null) fromCg.alpha = 0f;
                if (toCg != null) { toCg.alpha = 1f; toCg.blocksRaycasts = true; }
                return;
            }

            float elapsed = 0f;
            while (elapsed < _duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(elapsed / _duration);
                if (toCg != null) toCg.alpha = k;
                if (fromCg != null) fromCg.alpha = 1f - k;
                await UniTask.NextFrame(ct);
            }

            if (fromCg != null) fromCg.alpha = 0f;
            if (toCg != null)
            {
                toCg.alpha = 1f;
                toCg.blocksRaycasts = true;
            }
        }

        private static CanvasGroup GetCanvasGroup(INavScreen screen)
        {
            if (screen?.Root == null) return null;
            var cg = screen.Root.GetComponent<CanvasGroup>();
            if (cg == null) cg = screen.Root.AddComponent<CanvasGroup>();
            return cg;
        }
    }
}
