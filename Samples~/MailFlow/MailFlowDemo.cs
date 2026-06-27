using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KidzDev.Unity.ScreenNavigator.MailFlow
{
    /// <summary>
    /// A self-contained Mail feature driven by <see cref="SubScreenNavigator{TKey}"/>:
    /// <b>Inbox → Message → Delete-confirm</b>. Everything (Canvas, panels, buttons) is built in code so the
    /// scene needs only this one component.
    /// </summary>
    /// <remarks>
    /// What it demonstrates:
    /// <list type="bullet">
    ///   <item><c>PushAsync(arg)</c> to open a message; <see cref="MessagePanel"/> binds the content from the
    ///         payload in its <c>OnPushedInternal</c> — the idiomatic data-passing pattern.</item>
    ///   <item><c>PopAsync</c> and a hardware-and-UI back button via <see cref="NavBackButton"/>, with an
    ///         <c>OnBackAtRoot</c> hint when back can't go any further.</item>
    ///   <item><c>PopToAsync(Inbox)</c> to collapse straight back after a delete, and <c>PopToRootAsync</c>
    ///         via the Home button.</item>
    ///   <item>A live breadcrumb built from <see cref="SubScreenNavigator{TKey}.GetStackKeys"/>.</item>
    ///   <item>CanvasGroup cross-fades on every transition, with <see cref="NavQueuePolicy.DropWhileBusy"/> safety.</item>
    /// </list>
    /// </remarks>
    public sealed class MailFlowDemo : MonoBehaviour
    {
        private enum Screen { Inbox, Message, Confirm }

        private readonly MailStore _store = new MailStore();

        private SubScreenNavigator<Screen> _nav;
        private int _currentMailId;

        private Text _statusText;
        private RectTransform _inboxList;
        private Font _font;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvas = BuildCanvas();
            EnsureEventSystem();

            var inbox   = BuildInbox(canvas);
            var message = BuildMessage(canvas);
            var confirm = BuildConfirm(canvas);

            var provider = new RegistryScreenProvider<Screen>()
                .Register(Screen.Inbox,   inbox)
                .Register(Screen.Message, message)
                .Register(Screen.Confirm, confirm);

            _nav = new SubScreenNavigator<Screen>(provider, new CanvasGroupFadeTransition(0.18f))
            {
                EnableLogging = true,
            };
            _nav.Changed += RefreshStatus;

            BuildStatusBar(canvas);
        }

        private async void Start()
        {
            await _nav.PushAsync(Screen.Inbox, ct: destroyCancellationToken);
        }

        private void OnDestroy() => _nav?.Dispose();

        // ── Screens ──────────────────────────────────────────────────────────────────────

        private NavPanel BuildInbox(Canvas canvas)
        {
            var (root, panel) = NewPanel<NavPanel>(canvas, "InboxPanel", new Color(0.12f, 0.16f, 0.28f));
            Label(root, "Inbox", 34, new Vector2(0, -50), TextAnchor.MiddleCenter, 600, 50);

            var listGo = new GameObject("List", typeof(RectTransform));
            _inboxList = (RectTransform)listGo.transform;
            _inboxList.SetParent(root.transform, false);
            _inboxList.anchoredPosition = new Vector2(0, 60);

            RebuildInboxList();
            return panel;
        }

        private NavPanel BuildMessage(Canvas canvas)
        {
            var (root, panel) = NewPanel<MessagePanel>(canvas, "MessagePanel", new Color(0.10f, 0.24f, 0.22f));
            var subject = Label(root, "Subject", 30, new Vector2(0, -50), TextAnchor.MiddleCenter, 640, 50);
            var body    = Label(root, "Body", 22, new Vector2(0, 40), TextAnchor.UpperLeft, 640, 280);
            panel.Configure(_store, subject, body);

            Button(root.transform, "Delete", new Vector2(-150, -260), 250, 70,
                () => _nav.PushAsync(Screen.Confirm, ct: destroyCancellationToken).Forget());
            Button(root.transform, "Back", new Vector2(150, -260), 250, 70,
                () => _nav.PopAsync(destroyCancellationToken).Forget());
            return panel;
        }

        private NavPanel BuildConfirm(Canvas canvas)
        {
            var (root, panel) = NewPanel<NavPanel>(canvas, "ConfirmPanel", new Color(0.30f, 0.10f, 0.12f));
            Label(root, "Delete this message?", 30, new Vector2(0, 60), TextAnchor.MiddleCenter, 640, 60);

            Button(root.transform, "Yes, delete", new Vector2(-150, -40), 250, 70, OnConfirmDelete);
            Button(root.transform, "Cancel", new Vector2(150, -40), 250, 70,
                () => _nav.PopAsync(destroyCancellationToken).Forget());
            return panel;
        }

        // ── Flow logic ───────────────────────────────────────────────────────────────────

        private void OpenMessage(int id)
        {
            _currentMailId = id;
            // No imperative widget poking — MessagePanel binds itself from this arg in OnPushedInternal.
            _nav.PushAsync(Screen.Message, arg: id, ct: destroyCancellationToken).Forget();
        }

        private void OnConfirmDelete()
        {
            _store.Delete(_currentMailId);
            RebuildInboxList();
            // Collapse straight back to the inbox, past the message we just deleted.
            _nav.PopToAsync(Screen.Inbox, destroyCancellationToken).Forget();
        }

        private void RebuildInboxList()
        {
            for (int c = _inboxList.childCount - 1; c >= 0; c--)
                Destroy(_inboxList.GetChild(c).gameObject);

            if (_store.All.Count == 0)
            {
                Label(_inboxList.gameObject, "📭  No messages", 24, Vector2.zero, TextAnchor.MiddleCenter, 560, 70);
                return;
            }

            int i = 0;
            foreach (var mail in _store.All)
            {
                int id = mail.Id;
                Button(_inboxList, $"✉  {mail.Subject}", new Vector2(0, -i * 90), 560, 70,
                    () => OpenMessage(id));
                i++;
            }
        }

        private void RefreshStatus()
        {
            if (_statusText == null) return;

            var crumb = new StringBuilder();
            var keys = _nav.GetStackKeys();
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0) crumb.Append("  ›  ");
                crumb.Append(keys[i]);
            }
            _statusText.text = $"{crumb}      (depth {_nav.Depth})";
        }

        private void OnBackAtRoot()
        {
            if (_statusText != null) _statusText.text = "Inbox      (at root — back would exit the app)";
        }

        // ── UI scaffolding (sample-only helpers) ─────────────────────────────────────────

        private Canvas BuildCanvas()
        {
            var go = new GameObject("DemoCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800, 1280);
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_LEGACY_INPUT_MANAGER
            // Pointer input for the legacy/both input backends. Projects configured for the new Input
            // System only should add an InputSystemUIInputModule here instead.
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        private void BuildStatusBar(Canvas canvas)
        {
            var bar = new GameObject("StatusBar", typeof(Image));
            var rt = (RectTransform)bar.transform;
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(800, 110);
            rt.anchoredPosition = Vector2.zero;
            bar.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            _statusText = Label(bar, "…", 22, new Vector2(0, 34), TextAnchor.MiddleCenter, 760, 40);

            var backButton = bar.AddComponent<NavBackButton>();
            backButton.Bind(_nav);
            backButton.OnBackAtRoot.AddListener(OnBackAtRoot);

            Button(bar.transform, "◀ Back", new Vector2(-150, -26), 280, 56, () => backButton.TriggerBack());
            Button(bar.transform, "⌂ Home", new Vector2(150, -26), 280, 56,
                () => _nav.PopToRootAsync(destroyCancellationToken).Forget());
        }

        private (GameObject root, T panel) NewPanel<T>(Canvas canvas, string name, Color color) where T : NavPanel
        {
            var go = new GameObject(name, typeof(Image), typeof(CanvasGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0, -120); // leave room for the status bar
            go.GetComponent<Image>().color = color;
            var panel = go.AddComponent<T>();
            return (go, panel);
        }

        private Text Label(GameObject parent, string text, int size, Vector2 pos, TextAnchor anchor, float w, float h)
        {
            var go = new GameObject("Label", typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent.transform, false);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = pos;
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = text;
            return t;
        }

        private Button Button(Transform parent, string label, Vector2 pos, float w, float h, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = new Color(0.95f, 0.95f, 0.97f);
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var txt = new GameObject("Text", typeof(Text));
            var trt = (RectTransform)txt.transform;
            trt.SetParent(go.transform, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var t = txt.GetComponent<Text>();
            t.font = _font;
            t.fontSize = 24;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.1f, 0.1f, 0.12f);
            t.text = label;
            return go.GetComponent<Button>();
        }
    }
}
