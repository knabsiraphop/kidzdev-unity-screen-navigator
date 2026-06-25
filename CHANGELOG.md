# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - 2026-06-25

### Added
- `SubSceneNavigator<TKey>` — stack-based screen navigator with `PushAsync`, `PopAsync`, `ReplaceAsync`, `PopToRootAsync`, `PopToAsync`, and `HandleBackAsync`. Keeps a true push/pop history (the same key may appear more than once), commits the stack before animating, and exposes `Current`/`Depth`/`CanGoBack`/`IsTransitioning` plus `Pushed`/`Popped`/`Changed` events.
- `INavScreen` — visually passive navigable view contract with `Root` and lifecycle hooks (`OnPushed`/`OnRevealed`/`OnCovered`/`OnPopped`/`OnBackPressed`).
- `INavScreenProvider<TKey>` — seam that resolves keys to screens and releases them on pop; `RegistryScreenProvider<TKey>` default for scene-resident panels.
- `INavTransition` — animation seam; `InstantTransition` and `CanvasGroupFadeTransition` (bare per-frame alpha lerp, no third-party animation dependency) ship in-box.
- `NavQueuePolicy` (`DropWhileBusy`, `Queue`) governing overlapping requests during a transition; default `DropWhileBusy` for touch-UI safety.
- `INavigator` — non-generic back-handling surface; `NavBackButton` component routes the Escape/hardware back key and UI button clicks, with an `OnBackAtRoot` event for quit/confirm. The hardware-key listener is guarded behind `ENABLE_LEGACY_INPUT_MANAGER` so it never throws under the new Input System.
- `NavPanel` — drop-in `MonoBehaviour` screen with overridable lifecycle methods and `UnityEvent`s for designer wiring.
- Mail Flow sample: Inbox → Message → Delete-confirm driven by `SubSceneNavigator<MailScreen>` with CanvasGroup fades, a back button, and full push/pop history.
