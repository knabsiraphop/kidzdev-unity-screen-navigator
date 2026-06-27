using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace KidzDev.Unity.ScreenNavigator
{
    /// <summary>
    /// Zero-code setup for the common case: panels live in the scene, keys are strings, and you
    /// want an Inspector-driven wiring instead of C# boilerplate.
    /// <br/><br/>
    /// Add this component to your Canvas (or a child), populate <c>Screens</c> in the Inspector,
    /// and call <c>Navigator.PushAsync("key")</c> from buttons. For keyed enums or custom providers,
    /// create a <c>MonoBehaviour</c> that constructs <see cref="SubScreenNavigator{TKey}"/> directly.
    /// </summary>
    [AddComponentMenu("KidzDev/Screen Navigator/Screen Navigator Host")]
    [DisallowMultipleComponent]
    public sealed class ScreenNavigatorHost : MonoBehaviour
    {
        /// <summary>One Inspector row mapping a string key to a scene-resident panel.</summary>
        [Serializable]
        public sealed class ScreenEntry
        {
            public string   Key;
            public NavPanel Panel;
        }

        /// <summary>Built-in transition options selectable in the Inspector.</summary>
        public enum TransitionMode
        {
            Instant,
            Fade,
        }

        [Header("Screens")]
        [Tooltip("Map each string key to a scene-resident NavPanel. All panels must start inactive.")]
        [SerializeField] private ScreenEntry[] _screens = Array.Empty<ScreenEntry>();

        [Header("Transition")]
        [SerializeField] private TransitionMode _transitionMode = TransitionMode.Fade;
        [SerializeField, Min(0f)] private float _fadeDuration = 0.2f;

        [Header("Behaviour")]
        [SerializeField] private NavQueuePolicy _policy = NavQueuePolicy.DropWhileBusy;
        [Tooltip("Key of the screen to push immediately on Start. Leave empty to skip.")]
        [SerializeField] private string _initialScreen;
        [Tooltip("Optional NavBackButton in the hierarchy to bind automatically.")]
        [SerializeField] private NavBackButton _backButton;
        [Tooltip("Fired when a back request reaches the root (stack depth 1 or 0).")]
        [SerializeField] private UnityEvent _onBackAtRoot;
        [SerializeField] private bool _enableLogging;

        /// <summary>The underlying navigator. Available after <c>Awake</c>.</summary>
        public SubScreenNavigator<string> Navigator { get; private set; }

        private void Awake()
        {
            var provider = new RegistryScreenProvider<string>();
            foreach (var e in _screens)
            {
                if (e.Panel != null && !string.IsNullOrEmpty(e.Key))
                    provider.Register(e.Key, e.Panel);
            }

            INavTransition transition = _transitionMode == TransitionMode.Fade
                ? (INavTransition)new CanvasGroupFadeTransition(_fadeDuration)
                : new InstantTransition();

            Navigator = new SubScreenNavigator<string>(provider, transition, _policy)
            {
                EnableLogging = _enableLogging,
            };

            if (_backButton != null)
            {
                _backButton.Bind(Navigator);
                _backButton.OnBackAtRoot.AddListener(() => _onBackAtRoot?.Invoke());
            }
        }

        private async void Start()
        {
            if (!string.IsNullOrEmpty(_initialScreen))
                await Navigator.PushAsync(_initialScreen, ct: destroyCancellationToken);
        }

        private void OnDestroy() => Navigator?.Dispose();
    }
}
