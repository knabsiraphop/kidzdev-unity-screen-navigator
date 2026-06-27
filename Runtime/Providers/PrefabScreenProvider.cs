using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.ScreenNavigator
{
    /// <summary>
    /// Provider that instantiates a registered prefab on every <c>ResolveAsync</c> and destroys the
    /// instance on <c>Release</c>. Because each call creates a fresh instance, the same key can appear
    /// more than once in the navigation stack (unlike <see cref="RegistryScreenProvider{TKey}"/>).
    /// </summary>
    /// <typeparam name="TKey">Key type used to identify prefabs.</typeparam>
    public sealed class PrefabScreenProvider<TKey> : INavScreenProvider<TKey>
    {
        private readonly Dictionary<TKey, GameObject> _prefabs;
        private readonly Transform _parent;

        /// <param name="parent">
        /// Optional parent transform for instantiated screens. Pass the navigator's Canvas
        /// transform so screens are sized and layered correctly.
        /// </param>
        /// <param name="comparer">Optional key equality comparer.</param>
        public PrefabScreenProvider(Transform parent = null, IEqualityComparer<TKey> comparer = null)
        {
            _parent  = parent;
            _prefabs = new Dictionary<TKey, GameObject>(comparer ?? EqualityComparer<TKey>.Default);
        }

        /// <summary>
        /// Registers a prefab for <paramref name="key"/>. Calling again with the same key replaces
        /// the old registration. Returns <c>this</c> for fluent chaining.
        /// </summary>
        public PrefabScreenProvider<TKey> Register(TKey key, GameObject prefab)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            _prefabs[key] = prefab;
            return this;
        }

        /// <inheritdoc/>
        public UniTask<INavScreen> ResolveAsync(TKey key, object arg, CancellationToken ct)
        {
            if (!_prefabs.TryGetValue(key, out var prefab))
                throw new KeyNotFoundException($"No prefab registered for key '{key}' in {nameof(PrefabScreenProvider<TKey>)}.");

            var go = UnityEngine.Object.Instantiate(prefab, _parent);
            go.SetActive(false);

            var screen = go.GetComponent<INavScreen>();
            if (screen == null)
                throw new InvalidOperationException(
                    $"Prefab for key '{key}' has no component implementing INavScreen.");

            return UniTask.FromResult(screen);
        }

        /// <inheritdoc/>
        public void Release(TKey key, INavScreen screen)
        {
            if (screen?.Root != null)
                UnityEngine.Object.Destroy(screen.Root);
        }
    }
}
