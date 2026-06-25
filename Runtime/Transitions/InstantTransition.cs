using System.Threading;
using Cysharp.Threading.Tasks;

namespace KidzDev.Unity.SceneNavigator
{
    /// <summary>
    /// The default transition: no animation. Completes synchronously. The navigator handles activation,
    /// so this implementation has nothing to do beyond return.
    /// </summary>
    public sealed class InstantTransition : INavTransition
    {
        /// <inheritdoc/>
        public UniTask PlayAsync(INavScreen from, INavScreen to, NavDirection direction, CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }
    }
}
