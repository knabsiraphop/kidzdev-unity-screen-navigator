# KidzDev Unity Screen Navigator

Stack-based UI navigation for Unity — push/pop **history** with a back button, async transitions, and an injectable animation seam. UniTask-only, zero singleton coupling.

## Why a navigator and not a state machine?

A back stack is a **stack**, not a finite state machine. An FSM has one current state and a fixed graph; a navigation history needs `Push(Profile) → Push(Friend) → Push(Friend) → Pop` — the *same screen repeating* in a dynamic stack. This package owns that stack model.

| You need | Use |
|---|---|
| Fixed graph, no history (boot flow, scene-to-scene) | [`AsyncStateMachine`](https://github.com/knabsiraphop/kidzdev-unity-state-machine) |
| Push/pop history, back button, same screen repeats | **this package** (`SubScreenNavigator<TKey>`) |

---

## Install

Add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.kidzdev.unity.screen-navigator": "https://github.com/knabsiraphop/kidzdev-unity-screen-navigator.git#v1.1.0"
  }
}
```

> UniTask (`com.cysharp.unitask`) is a required dependency. Add the OpenUPM scoped registry if it is not already present.

> **Sample:** import **Provider Demos** from the package's *Samples* tab (Package Manager) for three runnable scenes — `RegistryScreenProvider`, `PrefabScreenProvider`, and `ResourcesScreenProvider` — sharing one controller so you can compare how each loads screens.

---

## Zero-code setup — ScreenNavigatorHost

The fastest way to integrate. Panels live in the scene; everything is wired in the Inspector.

### 1. Hierarchy

```
Canvas  (Screen Space Overlay, ScaleWithScreenSize 800×1280)
├── HomePanel     ← NavPanel + CanvasGroup  (active: OFF)
├── ProfilePanel  ← NavPanel + CanvasGroup  (active: OFF)
├── SettingsPanel ← NavPanel + CanvasGroup  (active: OFF)
├── NavBackButton ← NavBackButton component
└── ScreenNavigatorHost ← add this component
```

Set every panel **inactive** in the Inspector. `ScreenNavigatorHost` activates the initial screen on Start.

### 2. Inspector setup

| Field | Value |
|---|---|
| **Screens** | Add one row per panel: key `"Home"` → `HomePanel`, etc. |
| **Transition Mode** | `Fade` (cross-fades using `CanvasGroup`) or `Instant` |
| **Fade Duration** | seconds, e.g. `0.2` |
| **Initial Screen** | key of the first screen to push, e.g. `"Home"` |
| **Back Button** | drag your `NavBackButton` here |
| **On Back At Root** | wire to a "quit?" dialog or minimize call |

### 3. Push from buttons

```csharp
// Any MonoBehaviour on the canvas can find the host:
[SerializeField] private ScreenNavigatorHost _host;

public void OpenProfile(UserData user)
    => _host.Navigator.PushAsync("Profile", arg: user, ct: destroyCancellationToken).Forget();

public void GoBack()
    => _host.Navigator.PopAsync(destroyCancellationToken).Forget();
```

---

## Code setup — custom keys or providers

Use this when you need enum keys, a prefab provider, or a custom transition.

### Scene panels with enum keys

```csharp
using Cysharp.Threading.Tasks;
using KidzDev.Unity.ScreenNavigator;
using UnityEngine;

public sealed class NavController : MonoBehaviour
{
    [SerializeField] private NavPanel _homePanel;
    [SerializeField] private NavPanel _profilePanel;
    [SerializeField] private NavPanel _settingsPanel;
    [SerializeField] private NavBackButton _backButton;

    private SubScreenNavigator<Screen> _nav;

    private enum Screen { Home, Profile, Settings }

    private void Awake()
    {
        var provider = new RegistryScreenProvider<Screen>()
            .Register(Screen.Home,     _homePanel)
            .Register(Screen.Profile,  _profilePanel)
            .Register(Screen.Settings, _settingsPanel);

        _nav = new SubScreenNavigator<Screen>(provider, new CanvasGroupFadeTransition(0.2f));
        _backButton.Bind(_nav);
    }

    private async void Start()
    {
        await _nav.PushAsync(Screen.Home, ct: destroyCancellationToken);
    }

    private void OnDestroy() => _nav?.Dispose();

    public void OpenProfile(UserData user)
        => _nav.PushAsync(Screen.Profile, arg: user, ct: destroyCancellationToken).Forget();

    public void GoBack()
        => _nav.PopAsync(destroyCancellationToken).Forget();
}
```

---

## Screens — NavPanel lifecycle hooks

Attach `NavPanel` to the panel root and override the protected hooks. `INavScreen` is implemented explicitly so lifecycle methods are only callable by the navigator — you cannot accidentally call them from outside.

| Hook | When it fires |
|---|---|
| `OnPushed(object arg)` | Screen enters the stack. Bind data from `arg` here. |
| `OnCovered()` | Another screen pushed on top — pause timers, audio, etc. |
| `OnRevealed()` | Screen is the top again after a pop above it — resume work. |
| `OnPopped()` | Screen is leaving the stack — release transient state. |
| `OnBackPressed()` | Return `true` to consume back (e.g. close an inline dialog). |

`NavPanel` also exposes each hook as a `UnityEvent` (`_onPushed`, `_onRevealed`, `_onCovered`, `_onPopped`) for designer wiring. The `_consumesBack` Inspector toggle is equivalent to overriding `OnBackPressed` to always return `true`.

### Untyped subclass

```csharp
public sealed class ProfilePanel : NavPanel
{
    [SerializeField] private TMP_Text _nameText;

    protected override void OnPushed(object arg)
    {
        if (arg is UserData user)
            _nameText.text = user.DisplayName;
    }

    protected override void OnRevealed()
    {
        // Returned to after a pop above us — refresh if needed.
    }

    protected override void OnCovered()
    {
        // Another screen pushed over us — pause animations, timers, etc.
    }
}
```

### Typed subclass — NavPanel\<TArg\>

Eliminates the cast when you always receive the same payload type:

```csharp
public sealed class ProfilePanel : NavPanel<UserData>
{
    [SerializeField] private TMP_Text _nameText;

    protected override void OnPushed(UserData user)
    {
        _nameText.text = user.DisplayName;
    }
}
```

Push with data:

```csharp
await _nav.PushAsync(Screen.Profile, arg: userData, ct: destroyCancellationToken);
```

---

## Providers

A provider is the seam between keys and screen instances. Three are shipped.

### RegistryScreenProvider — shared scene instances (default)

Screens live in the scene, pre-loaded. `Release` is a no-op. Pushing the same key twice throws because the single instance can't be in two stack slots.

```csharp
var provider = new RegistryScreenProvider<Screen>()
    .Register(Screen.Home,    _homePanel)
    .Register(Screen.Profile, _profilePanel);
```

### PrefabScreenProvider — instantiate per push

Each `ResolveAsync` instantiates a fresh copy; each `Release` destroys it. Allows the same key to appear multiple times in the stack.

```csharp
// Scene hierarchy:
// Canvas/PanelContainer   ← assign as parent so screens are sized correctly
[SerializeField] private Transform _container;
[SerializeField] private GameObject _homePrefab;
[SerializeField] private GameObject _profilePrefab;

var provider = new PrefabScreenProvider<Screen>(parent: _container)
    .Register(Screen.Home,    _homePrefab)
    .Register(Screen.Profile, _profilePrefab);

_nav = new SubScreenNavigator<Screen>(provider, new CanvasGroupFadeTransition(0.2f));
```

### ResourcesScreenProvider — load from Resources

Loads by path from any `Resources/` folder. Same instantiate-per-push, destroy-per-release behaviour as `PrefabScreenProvider`.

```
Assets/
└── Resources/
    └── Screens/
        ├── Home.prefab      ← NavPanel component on root
        └── Profile.prefab
```

```csharp
var provider = new ResourcesScreenProvider<string>()
    .Register("Home",    "Screens/Home")
    .Register("Profile", "Screens/Profile");

_nav = new SubScreenNavigator<string>(provider, new CanvasGroupFadeTransition(0.2f));

await _nav.PushAsync("Home",    ct: destroyCancellationToken);
await _nav.PushAsync("Profile", arg: userData, ct: destroyCancellationToken);
await _nav.PushAsync("Profile", arg: otherUser, ct: destroyCancellationToken); // same key — fresh instance
await _nav.PopAsync(destroyCancellationToken);                                  // destroys top Profile
```

### Addressables provider (implement yourself)

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using KidzDev.Unity.ScreenNavigator;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public sealed class AddressableScreenProvider : INavScreenProvider<string>
{
    public async UniTask<INavScreen> ResolveAsync(string key, object arg, CancellationToken ct)
    {
        var handle = Addressables.InstantiateAsync(key);
        await handle.ToUniTask(cancellationToken: ct);

        if (handle.Status != AsyncOperationStatus.Succeeded)
            throw new System.Exception($"Addressables failed to instantiate '{key}': {handle.OperationException}");

        var go = handle.Result;
        go.SetActive(false);
        return go.GetComponent<INavScreen>();
    }

    public void Release(string key, INavScreen screen)
    {
        if (screen?.Root != null)
            Addressables.ReleaseInstance(screen.Root);
    }
}
```

```csharp
var provider = new AddressableScreenProvider();
_nav = new SubScreenNavigator<string>(provider, new CanvasGroupFadeTransition(0.2f));

await _nav.PushAsync("Screens/Home",    ct: destroyCancellationToken);
await _nav.PushAsync("Screens/Profile", arg: userData, ct: destroyCancellationToken);
await _nav.PopAsync(destroyCancellationToken); // ReleaseInstance — asset can unload
```

`AssetReference` key variant: use `key.InstantiateAsync()` / `key.ReleaseInstance(screen.Root)` and `INavScreenProvider<AssetReference>`.

---

## Operations

| Method | Effect |
|---|---|
| `PushAsync(key, arg)` | Resolve + animate a screen in over the current top. |
| `PopAsync()` | Animate the top out, reveal the one beneath. No-op on empty. |
| `ReplaceAsync(key, arg)` | Swap the top screen; stack depth unchanged. |
| `PopToRootAsync()` | Collapse to the bottom screen (top animates, rest tear down silently). |
| `PopToAsync(key)` | Collapse back to the nearest screen with `key` below the top. |
| `HandleBackAsync()` | Offer back to the screen first (`OnBackPressed`); else pop. Returns `false` at root. |

Inspect the stack with `Contains(key)`, `GetStackKeys()` / `CopyStackKeysTo(buffer)` (bottom→top, useful for breadcrumbs), `Current` / `HasCurrent` / `Depth` / `CanGoBack` / `IsTransitioning`.

---

## Production guarantees

| Guarantee | How |
|---|---|
| One transition at a time | `NavGate` serialises operations; overlaps are dropped (`DropWhileBusy`, default) or FIFO-queued (`Queue`). |
| Deterministic on cancel | Stack committed **before** animation; a cancelled transition leaves the stack settled and propagates the exception. |
| One instance, one slot | Pushing an instance already on the stack throws `InvalidOperationException`. Use `PrefabScreenProvider` or `ResourcesScreenProvider` to allow a key to repeat. |
| No input leak mid-fade | `CanvasGroupFadeTransition` blocks raycasts during the fade, restores them on the survivor. |
| Touch-mash safety | Default `DropWhileBusy` — a double-tap cannot double-push; back-mashing cannot over-pop during an animation. |
| Clean disposal | Lifetime `CancellationTokenSource` cancels in-flight work; every remaining screen is released through the provider. |

---

## Transitions

`InstantTransition` (default) and `CanvasGroupFadeTransition` ship with the package. Implement `INavTransition` once to restyle every navigation in the app:

```csharp
public sealed class SlideTransition : INavTransition
{
    public async UniTask PlayAsync(INavScreen from, INavScreen to, NavDirection dir, CancellationToken ct)
    {
        // Animate to.Root in and from.Root out, respecting dir and ct.
    }
}
```

Pass your transition to the constructor:

```csharp
_nav = new SubScreenNavigator<Screen>(provider, new SlideTransition(0.25f));
```

---

## Input backends

`NavBackButton` listens for Escape / Android hardware back only when the legacy Input Manager is enabled (`ENABLE_LEGACY_INPUT_MANAGER`). In new-Input-System-only projects, bind your own back action to `NavBackButton.TriggerBack()` or call `navigator.HandleBackAsync()` directly.

---

## Concurrency

Main-thread only; frame-and-await re-entrant safe. Overlapping calls are dropped or queued per `NavQueuePolicy`:

```csharp
// Drop overlapping requests (default — safe for touch UI):
_nav = new SubScreenNavigator<Screen>(provider, transition, NavQueuePolicy.DropWhileBusy);

// Queue overlapping requests (for scripted sequences where no push should be lost):
_nav = new SubScreenNavigator<Screen>(provider, transition, NavQueuePolicy.Queue);
```
