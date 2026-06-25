# KidzDev Unity Scene Navigator

Stack-based UI/scene navigation for Unity — push/pop **history** with a back button, async transitions, and an injectable animation seam. UniTask-only, zero singleton coupling.

## Why a navigator and not a state machine?

A back stack is a **stack**, not a finite state machine. An FSM has one current state and a fixed graph; a navigation history needs `Push(Profile) → Push(Friend) → Push(Friend) → Pop` — the *same screen repeating* in a dynamic stack. This package owns that stack model.

| You need | Use |
|---|---|
| Fixed graph, no history (boot flow, scene-to-scene) | [`AsyncStateMachine`](https://github.com/knabsiraphop/kidzdev-unity-state-machine) |
| Push/pop history, back button, same screen repeats | **this package** (`SubSceneNavigator<TKey>`) |

It reuses the state machine's transition discipline (single-in-flight, lifetime-cancellation) without taking a dependency on it.

## Install

Add to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.kidzdev.unity.scene-navigator": "https://github.com/knabsiraphop/kidzdev-unity-scene-navigator.git#v0.1.0"
  }
}
```

> UniTask (`com.cysharp.unitask`) is a required dependency. Add the OpenUPM scoped registry if it is not already present.

## Quick start

```csharp
enum Screen { Inbox, Message, ConfirmDelete }

// 1. Register scene-resident panels (each has a NavPanel + CanvasGroup).
var provider = new RegistryScreenProvider<Screen>()
    .Register(Screen.Inbox,         inboxPanel)
    .Register(Screen.Message,       messagePanel)
    .Register(Screen.ConfirmDelete, confirmPanel);

// 2. Create the navigator with a fade transition.
var nav = new SubSceneNavigator<Screen>(provider, new CanvasGroupFadeTransition(0.2f))
{
    EnableLogging = true,
};

// 3. Drive it.
await nav.PushAsync(Screen.Inbox);                    // first screen
await nav.PushAsync(Screen.Message, arg: mailId);     // push over Inbox
await nav.PushAsync(Screen.ConfirmDelete);            // push over Message
await nav.PopAsync();                                  // back to Message
await nav.PopToAsync(Screen.Inbox);                   // collapse back to Inbox

// 4. Wire the hardware/UI back button.
backButton.Bind(nav);

// 5. Dispose with the owner (cancels in-flight work, releases screens).
nav.Dispose();
```

## Operations

| Method | Effect |
|---|---|
| `PushAsync(key, arg)` | Resolve + animate a screen in over the current top. |
| `PopAsync()` | Animate the top out, reveal the one beneath. No-op on empty. |
| `ReplaceAsync(key, arg)` | Swap the top screen; stack depth unchanged. |
| `PopToRootAsync()` | Collapse to the bottom screen (top animates, the rest tear down silently). |
| `PopToAsync(key)` | Collapse back to the nearest screen with `key` below the top. |
| `HandleBackAsync()` | Offer back to the screen first (`OnBackPressed`); else pop. Returns `false` at root. |

## Screens

Attach `NavPanel` to a panel root (or implement `INavScreen` yourself). Screens are **visually passive** — the navigator toggles active state, the transition animates. Override the hooks to bind data:

```csharp
public sealed class MessagePanel : NavPanel
{
    [SerializeField] private TMP_Text body;

    protected override void OnPushedInternal(object arg)
    {
        var mailId = (int)arg;
        body.text = MailStore.GetBody(mailId);
    }
}
```

`OnBackPressed` returning `true` consumes the back press (e.g. close an inline overlay before the navigator pops).

## Production guarantees

| Guarantee | How |
|---|---|
| One transition at a time | `NavGate` serializes operations; overlaps are dropped (`DropWhileBusy`, default) or FIFO-queued (`Queue`). |
| Deterministic on cancel | Stack is committed **before** the animation; a cancelled transition leaves the stack settled and propagates the exception. |
| No input leak mid-fade | `CanvasGroupFadeTransition` blocks raycasts during the fade, restores them on the survivor. |
| Touch-mash safety | Default `DropWhileBusy` — a double-tap can't double-push; back-mash can't over-pop during an animation. |
| Clean disposal | Lifetime `CancellationTokenSource` cancels in-flight work; every remaining screen is released through the provider. |

## Transitions

`InstantTransition` (default) and `CanvasGroupFadeTransition` ship with the package and need no third-party animation library. To restyle every navigation in the app, implement `INavTransition` once:

```csharp
public sealed class SlideTransition : INavTransition
{
    public async UniTask PlayAsync(INavScreen from, INavScreen to, NavDirection dir, CancellationToken ct)
    {
        // animate to.Root in / from.Root out; honor ct.
    }
}
```

## Input backends

`NavBackButton`'s hardware/Escape-key listener is compiled only when the legacy Input Manager is enabled (`ENABLE_LEGACY_INPUT_MANAGER`), so the package never throws in projects configured for the new Input System only. In a new-Input-System-only project, bind your own back action to `NavBackButton.TriggerBack()` (or call `navigator.HandleBackAsync()` directly). UI button clicks work on either backend.

## Concurrency

Main-thread only; frame-and-await reentrancy safe. Re-entrant calls from a lifecycle callback are dropped or queued per policy — never run nested.

## Samples

Import via **Package Manager → KidzDev Unity Scene Navigator → Samples**.

### Mail Flow

A Mail feature driven by `SubSceneNavigator<MailScreen>`: **Inbox → Message → Delete-confirm**. Demonstrates a real push/pop history (open a message, open the delete confirm, back out step by step), CanvasGroup fades on every move, and a back button that pops the stack and quits at the root.
