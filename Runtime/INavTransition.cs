using System.Threading;
using Cysharp.Threading.Tasks;

namespace KidzDev.Unity.SceneNavigator
{
    /// <summary>
    /// The animation seam. Animates the visual move from <paramref name="from"/> to <paramref name="to"/>.
    /// Swap one implementation to restyle every navigation in the app.
    /// </summary>
    /// <remarks>
    /// Contract:
    /// <list type="bullet">
    ///   <item>The navigator activates <c>to.Root</c> <b>before</b> calling this, and deactivates the
    ///         outgoing screen's root <b>after</b> this returns. Implementations therefore only animate
    ///         visual properties (alpha, position, scale) and must not toggle active state.</item>
    ///   <item>Either argument may be <c>null</c>: <paramref name="from"/> is <c>null</c> for the first
    ///         push onto an empty stack; <paramref name="to"/> is <c>null</c> when popping the last screen.</item>
    ///   <item>Must honor <paramref name="ct"/>. On cancellation the navigator leaves the stack committed;
    ///         the exception propagates to the caller.</item>
    /// </list>
    /// The built-in <see cref="InstantTransition"/> and <see cref="CanvasGroupFadeTransition"/> require no
    /// third-party animation packages. A future tween/animation package can provide richer implementations.
    /// </remarks>
    public interface INavTransition
    {
        /// <summary>
        /// Animates from <paramref name="from"/> to <paramref name="to"/>.
        /// </summary>
        /// <param name="from">The outgoing screen, or <c>null</c> when entering onto an empty stack.</param>
        /// <param name="to">The incoming screen, or <c>null</c> when popping the last screen.</param>
        /// <param name="direction">Forward (push/replace) or Backward (pop).</param>
        /// <param name="ct">Cancellation token.</param>
        UniTask PlayAsync(INavScreen from, INavScreen to, NavDirection direction, CancellationToken ct);
    }
}
