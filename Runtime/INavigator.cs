using System.Threading;
using Cysharp.Threading.Tasks;

namespace KidzDev.Unity.SceneNavigator
{
    /// <summary>
    /// Non-generic navigation surface used by key-agnostic components such as <see cref="NavBackButton"/>.
    /// Implemented by <see cref="SubSceneNavigator{TKey}"/>.
    /// </summary>
    public interface INavigator
    {
        /// <summary><c>true</c> when there is more than one screen on the stack, so a pop is possible.</summary>
        bool CanGoBack { get; }

        /// <summary><c>true</c> while a transition is in progress.</summary>
        bool IsTransitioning { get; }

        /// <summary>Number of screens currently on the stack.</summary>
        int Depth { get; }

        /// <summary>
        /// Routes a back request: first offers it to the current screen via
        /// <see cref="INavScreen.OnBackPressed"/>; if not consumed and <see cref="CanGoBack"/> is true,
        /// pops the top screen.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the back request was handled (consumed by the screen or resulted in a pop);
        /// <c>false</c> if it could not be handled (e.g. at the root) — the caller may then quit the app.
        /// </returns>
        UniTask<bool> HandleBackAsync(CancellationToken ct = default);
    }
}
