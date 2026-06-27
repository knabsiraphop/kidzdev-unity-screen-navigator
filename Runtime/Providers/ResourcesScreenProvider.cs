using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KidzDev.Unity.ScreenNavigator
{
    /// <summary>
    /// Provider that loads prefabs from <c>Resources</c> by path, instantiates on resolve, and
    /// destroys on release. The same key can therefore appear more than once in the stack.
    /// All loading is synchronous (uses <see cref="Resources.Load{T}"/>); prefer
    /// Addressables for async streaming or memory control.
    /// </summary>
    /// <typeparam name="TKey">Key type used to look up resource paths.</typeparam>
    public sealed class ResourcesScreenProvider<TKey> : INavScreenProvider<TKey>
    {
        private readonly Dictionary<TKey, string> _paths;
        private readonly Transform _parent;

        /// <param name="parent">Optional parent transform for instantiated screens.</param>
        /// <param name="comparer">Optional key equality comparer.</param>
        public ResourcesScreenProvider(Transform parent = null, IEqualityComparer<TKey> comparer = null)
        {
            _parent = parent;
            _paths  = new Dictionary<TKey, string>(comparer ?? EqualityComparer<TKey>.Default);
        }

        /// <summary>
        /// Registers a <c>Resources</c> path for <paramref name="key"/>.
        /// The path follows the same convention as <see cref="Resources.Load{T}"/> (no extension,
        /// relative to any <c>Resources</c> folder). Returns <c>this</c> for fluent chaining.
        /// </summary>
        public ResourcesScreenProvider<TKey> Register(TKey key, string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) throw new ArgumentNullException(nameof(resourcePath));
            _paths[key] = resourcePath;
            return this;
        }

        /// <inheritdoc/>
        public UniTask<INavScreen> ResolveAsync(TKey key, object arg, CancellationToken ct)
        {
            if (!_paths.TryGetValue(key, out var path))
                throw new KeyNotFoundException($"No resource path registered for key '{key}' in {nameof(ResourcesScreenProvider<TKey>)}.");

            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
                throw new InvalidOperationException(
                    $"Resources.Load<GameObject>(\"{path}\") returned null for key '{key}'. " +
                    "Verify the prefab exists inside a Resources folder and the path is correct.");

            var go = UnityEngine.Object.Instantiate(prefab, _parent);
            go.SetActive(false);

            var screen = go.GetComponent<INavScreen>();
            if (screen == null)
                throw new InvalidOperationException(
                    $"Prefab at '{path}' has no component implementing INavScreen.");

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
