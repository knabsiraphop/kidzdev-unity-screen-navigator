using UnityEngine;
using UnityEngine.Events;

namespace KidzDev.Unity.ScreenNavigator
{
    /// <summary>
    /// Drop-in <see cref="INavScreen"/> for a UI panel. Attach to the panel's root GameObject and
    /// register it with a provider. <see cref="INavScreen"/> is implemented explicitly so lifecycle
    /// methods are only callable by the navigator — subclasses override the clean protected hooks.
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

        // ── Explicit INavScreen — navigator-only surface ──────────────────────────────────

        GameObject INavScreen.Root => gameObject;

        void INavScreen.OnPushed(object arg)
        {
            PushArg = arg;
            OnPushed(arg);
            _onPushed?.Invoke();
        }

        void INavScreen.OnRevealed()
        {
            OnRevealed();
            _onRevealed?.Invoke();
        }

        void INavScreen.OnCovered()
        {
            OnCovered();
            _onCovered?.Invoke();
        }

        void INavScreen.OnPopped()
        {
            OnPopped();
            _onPopped?.Invoke();
        }

        bool INavScreen.OnBackPressed() => OnBackPressed();

        // ── Protected hooks — override in subclasses ─────────────────────────────────────

        /// <summary>Called when this panel is pushed. Bind incoming data from <paramref name="arg"/> here.</summary>
        protected virtual void OnPushed(object arg) { }

        /// <summary>Called when this panel becomes the top again after the screen above it was popped.</summary>
        protected virtual void OnRevealed() { }

        /// <summary>Called when another panel is pushed over this one. Pause timers, audio, etc.</summary>
        protected virtual void OnCovered() { }

        /// <summary>Called when this panel is popped. Release transient state here.</summary>
        protected virtual void OnPopped() { }

        /// <summary>
        /// Called when a back request reaches this panel. Return <c>true</c> to consume it
        /// (e.g. close an inline dialog); return <c>false</c> to let the navigator pop.
        /// </summary>
        protected virtual bool OnBackPressed() => _consumesBack;
    }

    /// <summary>
    /// Typed-arg variant of <see cref="NavPanel"/> that eliminates the cast from <c>object</c>.
    /// Concrete subclasses implement <see cref="OnPushed(TArg)"/> with the exact type they expect.
    /// </summary>
    /// <typeparam name="TArg">Type of the push/replace payload.</typeparam>
    public abstract class NavPanel<TArg> : NavPanel
    {
        /// <summary>Sealed so the cast happens once here; override <see cref="OnPushed(TArg)"/> instead.</summary>
        protected sealed override void OnPushed(object arg)
        {
            if (arg is TArg typed)
                OnPushed(typed);
            else if (arg != null)
                Debug.LogWarning($"[{GetType().Name}] Expected arg of type {typeof(TArg).Name} but received {arg.GetType().Name}.");
        }

        /// <summary>Called with the typed payload when this panel is pushed.</summary>
        protected abstract void OnPushed(TArg arg);
    }
}
