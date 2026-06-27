# Provider Demos — Screen Navigator

Three scenes, one controller (`NavDemoController`). The **only** thing that changes between them is
the `INavScreenProvider` — so you can see exactly how each shipped provider loads screens. Every scene
shows a live **instance count** in its status line.

| Scene | Provider | Screens are… | Loaded on demand? |
|---|---|---|---|
| `1_Registry` | `RegistryScreenProvider` | shared instances living in the scene | **No** — all alive the whole time |
| `2_Prefab` | `PrefabScreenProvider` | prefab references | **Yes** — instantiated on push, destroyed on pop |
| `3_Resources` | `ResourcesScreenProvider` | prefabs under `Resources/Screens/` | **Yes** — loaded by path on push, destroyed on pop |

## Run it

Open any scene and press **Play**. Navigate **Home → Settings → About** with the buttons; **◀ Back**
(or Escape) pops. Watch the status line:

- In `1_Registry` the count reflects the stack, but the three panels always exist in the hierarchy.
- In `2_Prefab` / `3_Resources` instances appear and disappear under `ScreenContainer` as you push/pop —
  that's real load/release.

## What it shows

- **Swapping providers is a one-line change** — see `NavDemoController.Awake()`.
- **On-demand loading** via `PrefabScreenProvider` / `ResourcesScreenProvider` (instantiate on push,
  destroy on release), versus the always-loaded `RegistryScreenProvider`.
- **Lifecycle hooks** — `AboutScreen.OnPushed()` fills its label the moment the screen is shown.
- **Back routing** — `NavBackButton` drives `HandleBackAsync`; at the root it raises `OnBackAtRoot`.
- **Cross-fade** — `CanvasGroupFadeTransition` (0.2s) between every screen.

## Files

- `Scripts/NavDemoController.cs` — builds the chosen provider + navigator (the `_mode` enum selects it).
- `Scripts/ScreenNavLink.cs` — a button helper that pushes a target key.
- `Scripts/AboutScreen.cs` — a `NavPanel` subclass using the `OnPushed` hook.
- `Resources/Screens/*.prefab` — the three screen prefabs (shared by all three scenes).
