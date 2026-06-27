using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace KidzDev.Unity.ScreenNavigator.Demo
{
    /// <summary>
    /// One controller, three providers. Switch <see cref="_mode"/> in the Inspector to compare how each
    /// shipped <see cref="INavScreenProvider{TKey}"/> loads screens — everything else stays identical:
    /// <list type="bullet">
    /// <item><b>Registry</b> — shared scene panels, always loaded, never released.</item>
    /// <item><b>Prefab</b> — instantiated from a prefab reference on push, destroyed on pop.</item>
    /// <item><b>Resources</b> — loaded from a <c>Resources/</c> path on push, destroyed on pop.</item>
    /// </list>
    /// The status line shows the live instance count so the difference is visible at runtime.
    /// </summary>
    public sealed class NavDemoController : MonoBehaviour
    {
        /// <summary>Set in Awake so prefab/scene buttons (<see cref="ScreenNavLink"/>) can reach the navigator.</summary>
        public static NavDemoController Current { get; private set; }

        private enum Mode { Registry, Prefab, Resources }

        [SerializeField] private Mode _mode = Mode.Prefab;

        [Header("Screen keys (first is the initial screen)")]
        [SerializeField] private string[] _keys = { "Home", "Settings", "About" };

        [Header("Where on-demand screens are parented (full-screen stretch)")]
        [SerializeField] private RectTransform _container;

        [Header("Registry mode — scene panels, aligned to _keys")]
        [SerializeField] private NavPanel[] _scenePanels;

        [Header("Prefab mode — prefab refs, aligned to _keys")]
        [SerializeField] private GameObject[] _prefabs;

        [Header("Resources mode — Resources paths, aligned to _keys")]
        [SerializeField] private string[] _resourcePaths;

        [Header("Scene chrome")]
        [SerializeField] private NavBackButton _backButton;
        [SerializeField] private Text _status;
        [SerializeField] private Text _modeLabel;

        private SubScreenNavigator<string> _nav;

        private void Awake()
        {
            Current = this;

            INavScreenProvider<string> provider;
            switch (_mode)
            {
                case Mode.Registry:
                    var reg = new RegistryScreenProvider<string>();
                    for (int i = 0; i < _keys.Length; i++) reg.Register(_keys[i], _scenePanels[i]);
                    provider = reg;
                    break;

                case Mode.Prefab:
                    var pf = new PrefabScreenProvider<string>(_container);
                    for (int i = 0; i < _keys.Length; i++) pf.Register(_keys[i], _prefabs[i]);
                    provider = pf;
                    break;

                default:
                    var rs = new ResourcesScreenProvider<string>(_container);
                    for (int i = 0; i < _keys.Length; i++) rs.Register(_keys[i], _resourcePaths[i]);
                    provider = rs;
                    break;
            }

            _nav = new SubScreenNavigator<string>(provider, new CanvasGroupFadeTransition(0.2f));
            _nav.Changed += UpdateStatus;
            if (_backButton != null) _backButton.Bind(_nav);
            if (_modeLabel != null) _modeLabel.text = $"Provider: {_mode}ScreenProvider";
        }

        private async void Start()
        {
            UpdateStatus();
            if (_keys.Length > 0)
                await _nav.PushAsync(_keys[0], ct: destroyCancellationToken);
        }

        /// <summary>Pushes a screen on demand. Called by <see cref="ScreenNavLink"/> buttons.</summary>
        public void Push(string key) => _nav.PushAsync(key, ct: destroyCancellationToken).Forget();

        private void UpdateStatus()
        {
            if (_status == null) return;
            int depth = _nav?.Depth ?? 0;
            string note = _mode == Mode.Registry
                ? "shared scene instances — always loaded"
                : "instantiated on push, destroyed on pop";
            string stack = depth > 0 ? string.Join("  →  ", _nav.GetStackKeys()) : "(empty)";
            _status.text = $"Live in stack: {depth}   ({note})\nStack: {stack}";
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            _nav?.Dispose();
        }
    }
}
