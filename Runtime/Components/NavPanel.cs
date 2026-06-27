using UnityEngine;
using UnityEngine.Events;

namespace KidzDev.Unity.ScreenNavigator
{
    /// <summary>
    /// Drop-in <see cref="INavScreen"/> for a UI panel. Attach to the panel's root GameObject and register
    /// it with a <see cref="RegistryScreenProvider{TKey}"/>. Lifecycle hooks are exposed both as overridable
    /// methods (for subclasses that bind data) and as <see cref="UnityEvent"/>s (for designer wiring).
    /// </summary>
    [AddComponentMenu("KidzDev/Screen Navigator/Nav Panel")]
    [DisallowMultipleComponent]
    public class NavPanel : MonoBehaviour, INavScreen
    {
        [Tooltip("When true, a back press while this panel is on top is consumed here instead of popping. " +
                 "Override OnBackPressed for conditional behaviour (e.g. close an inline dialog first).")]
        [SerializeField] private bool _consumesBack;

        [Header("Lifecycle Events")]
        [SerializeField] private UnityEvent _onPushed;
        [SerializeField] private UnityEvent _onRevealed;
        [SerializeField] private UnityEvent _onCovered;
        [SerializeField] private UnityEvent _onPopped;

        /// <inheritdoc/>
        public GameObject Root => gameObject;

        /// <summary>The payload from the most recent push/replace; <c>null</c> if none.</summary>
        protected object PushArg { get; private set; }

        /// <inheritdoc/>
        public void OnPushed(object arg)
        {
            PushArg = arg;
            OnPushedInternal(arg);
            _onPushed?.Invoke();
        }

        /// <inheritdoc/>
        public void OnRevealed()
        {
            OnRevealedInternal();
            _onRevealed?.Invoke();
        }

        /// <inheritdoc/>
        public void OnCovered()
        {
            OnCoveredInternal();
            _onCovered?.Invoke();
        }

        /// <inheritdoc/>
        public void OnPopped()
        {
            OnPoppedInternal();
            _onPopped?.Invoke();
        }

        /// <inheritdoc/>
        public virtual bool OnBackPressed() => _consumesBack;

        /// <summary>Override to bind incoming data when pushed. <paramref name="arg"/> is the push payload.</summary>
        protected virtual void OnPushedInternal(object arg) { }

        /// <summary>Override to refresh when this panel becomes the top again after a pop above it.</summary>
        protected virtual void OnRevealedInternal() { }

        /// <summary>Override to pause work when another panel is pushed over this one.</summary>
        protected virtual void OnCoveredInternal() { }

        /// <summary>Override to release transient state when this panel is popped.</summary>
        protected virtual void OnPoppedInternal() { }
    }
}
