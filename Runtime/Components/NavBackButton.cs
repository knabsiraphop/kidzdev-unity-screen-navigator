using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace KidzDev.Unity.SceneNavigator
{
    /// <summary>
    /// Routes back requests to an <see cref="INavigator"/>. Listens for the hardware/Escape back key and can
    /// be wired to a UI Button's <c>onClick</c> via <see cref="TriggerBack"/>. When a back request reaches the
    /// root and cannot be handled, <see cref="OnBackAtRoot"/> fires (hook a quit/confirm dialog here).
    /// </summary>
    /// <remarks>
    /// The navigator is key-generic, so bind it from code after constructing it:
    /// <c>backButton.Bind(navigator);</c>.
    /// </remarks>
    public sealed class NavBackButton : MonoBehaviour
    {
        [Tooltip("Listen for the Escape / Android hardware back key.")]
        [SerializeField] private bool _listenForHardwareBack = true;

        [Tooltip("Raised when a back request reaches the root and cannot be handled — e.g. quit or show a confirm dialog.")]
        [SerializeField] private UnityEvent _onBackAtRoot;

        private INavigator _navigator;

        /// <summary>Raised when a back request reaches the root and cannot be handled.</summary>
        public UnityEvent OnBackAtRoot => _onBackAtRoot;

        /// <summary>Binds the navigator this button drives.</summary>
        public void Bind(INavigator navigator) => _navigator = navigator;

        /// <summary>Wire this to a UI Button's <c>onClick</c>.</summary>
        public void TriggerBack() => RequestBack().Forget();

        private void Update()
        {
            if (!_listenForHardwareBack) return;
#if ENABLE_LEGACY_INPUT_MANAGER
            // Legacy Input. Guarded so the package never throws in projects configured for the new
            // Input System only (where UnityEngine.Input is disabled). Such projects should bind their
            // own back action to TriggerBack() instead.
            if (Input.GetKeyDown(KeyCode.Escape))
                RequestBack().Forget();
#endif
        }

        private async UniTaskVoid RequestBack()
        {
            if (_navigator == null) return;
            bool handled = await _navigator.HandleBackAsync(destroyCancellationToken);
            if (!handled) _onBackAtRoot?.Invoke();
        }
    }
}
