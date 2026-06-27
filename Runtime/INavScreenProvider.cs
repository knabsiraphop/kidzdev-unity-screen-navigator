using System.Threading;
using Cysharp.Threading.Tasks;

namespace KidzDev.Unity.ScreenNavigator
{
    /// <summary>
    /// Resolves a navigation key to an <see cref="INavScreen"/> instance, and releases it when popped.
    /// This is the seam that decouples the navigator from <i>how</i> screens are produced.
    /// </summary>
    /// <typeparam name="TKey">Key type identifying a screen (enum, string, etc.).</typeparam>
    /// <remarks>
    /// Implementations may return a pre-registered in-scene panel (see <see cref="RegistryScreenProvider{TKey}"/>),
    /// instantiate a prefab, or load one via Addressables. <see cref="ResolveAsync"/> is awaited so async
    /// loads complete before the enter transition begins.
    /// <para>
    /// <b>Shared vs. instancing providers:</b> a provider that returns a single shared instance per key (such as
    /// <see cref="RegistryScreenProvider{TKey}"/>) cannot have that key appear at two stack positions at once —
    /// the navigator throws if a resolved instance is already on the stack. To let a key repeat in the history,
    /// return a freshly instantiated screen from each <see cref="ResolveAsync"/> and destroy it in
    /// <see cref="Release"/>.
    /// </para>
    /// </remarks>
    public interface INavScreenProvider<TKey>
    {
        /// <summary>
        /// Produces (or returns a cached) screen for <paramref name="key"/>. Called before the enter
        /// transition. The returned screen's <see cref="INavScreen.Root"/> may be inactive — the
        /// navigator activates it.
        /// </summary>
        /// <param name="key">The screen key.</param>
        /// <param name="arg">Optional payload forwarded to the screen; may be <c>null</c>.</param>
        /// <param name="ct">Cancellation token linked to the navigator lifetime and the caller token.</param>
        UniTask<INavScreen> ResolveAsync(TKey key, object arg, CancellationToken ct);

        /// <summary>
        /// Called after a screen has been popped and its exit transition has completed. Implementations
        /// that instantiate or load screens should destroy/release here; registry-style providers that
        /// keep panels resident can leave this empty.
        /// </summary>
        /// <param name="key">The key the screen was resolved with.</param>
        /// <param name="screen">The screen instance being released.</param>
        void Release(TKey key, INavScreen screen);
    }
}
