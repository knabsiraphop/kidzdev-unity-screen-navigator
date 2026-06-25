using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace KidzDev.Unity.SceneNavigator
{
    /// <summary>
    /// The simplest provider: a dictionary of pre-built, scene-resident screens registered by key.
    /// <see cref="Release"/> is a no-op — panels stay in the hierarchy (the navigator deactivates them),
    /// so the same instance is reused every time its key is pushed.
    /// </summary>
    /// <typeparam name="TKey">Key type identifying a screen.</typeparam>
    /// <remarks>
    /// Use this for the common case where every panel already exists in the scene. For
    /// instantiate-on-demand or Addressables-loaded screens, implement <see cref="INavScreenProvider{TKey}"/>
    /// directly and release in <see cref="INavScreenProvider{TKey}.Release"/>.
    /// </remarks>
    public sealed class RegistryScreenProvider<TKey> : INavScreenProvider<TKey>
    {
        private readonly Dictionary<TKey, INavScreen> _screens;

        public RegistryScreenProvider(IEqualityComparer<TKey> comparer = null)
        {
            _screens = new Dictionary<TKey, INavScreen>(comparer ?? EqualityComparer<TKey>.Default);
        }

        /// <summary>Registers a screen instance for <paramref name="key"/>. Returns this for chaining.</summary>
        public RegistryScreenProvider<TKey> Register(TKey key, INavScreen screen)
        {
            if (screen == null) throw new ArgumentNullException(nameof(screen));
            _screens[key] = screen;
            // Start hidden so the first push fades/activates it from a known state.
            if (screen.Root != null) screen.Root.SetActive(false);
            return this;
        }

        /// <inheritdoc/>
        public UniTask<INavScreen> ResolveAsync(TKey key, object arg, CancellationToken ct)
        {
            if (!_screens.TryGetValue(key, out var screen))
                throw new KeyNotFoundException($"No screen registered for key '{key}'. Call Register first.");
            return UniTask.FromResult(screen);
        }

        /// <inheritdoc/>
        public void Release(TKey key, INavScreen screen)
        {
            // No-op: scene-resident panels are reused, not destroyed.
        }
    }
}
