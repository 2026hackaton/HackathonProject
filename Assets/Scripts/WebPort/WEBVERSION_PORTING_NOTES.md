# WebVersion Porting Notes

## Source Design

WebVersion is a React + React Three Fiber game with a Node `ws` relay server.
The live code is a 3D top-down delivery party game, not the older four-zone
territory draft left in `API.md`.

- World: cross-shaped walkable map on the X/Z plane.
- Camera: fixed follow camera at player position plus `(0, 140, 420)`, FOV 38.
- Start: left arm at `(-560, 0, 0)`.
- Goal: one of three endpoints, rotating every 25 seconds.
- Obstacles: pillar `(250, 0)`, wall `(0, -250)`, rock `(0, 250)`.
- Packages: 3x3 refill slots at the start point, refill delay 4 seconds.
- Package types: normal, high, bomb, gravity.
- Session: 180 seconds.
- Truck: every 20 delivered packages, delivered boxes are cleared and a truck animation plays.
- Player input: WASD, Shift careful walk, E pickup, left mouse charge/throw, Q grab, Space charge/push.
- Rendering: simple 3D floor/boxes/obstacles plus billboard PNG sprite sheets for players.
- Networking: current server handles lobby, room code, start/end, package spawn, pickup arbitration, and relay. Client still owns movement and most package physics.

## Unity Port

The Unity port keeps the WebVersion coordinate scale and constants so behavior
maps closely to the original. The current implementation is intentionally local
only: no WebSocket/backend code is included.

Backend-ready boundary:

- `Runtime/Transport/IGameTransport.cs`
- `Runtime/Transport/LocalGameTransport.cs`
- payload models in `Runtime/Core/WebPortModels.cs`

A future WebSocket transport should implement `IGameTransport` and emit the same
room/game events into `WebPortGameManager`.

Runtime entry:

- `Runtime/Gameplay/WebPortBootstrap.cs` creates `WebPortGameManager` after scene load if none exists.
- Therefore the current build scene can be played without a dedicated scene.
- `Editor/WebPortProjectBuilder.cs` can generate `Assets/Scenes/WebVersionPort.unity` from Unity's menu:
  `Tools > WebVersion Port > Rebuild Scene`.

Visual replacement workflow:

- The runtime reads `Assets/Resources/WebPort/WebPortVisualConfig.asset`.
- Unity creates that asset automatically after script reload if it is missing.
- You can also open it from `Tools > WebVersion Port > Select Visual Config`.
- Replace player sprite sheets, package prefabs/materials, obstacle prefabs/materials,
  vehicle prefabs/materials, UI sprites/colors/fonts, and fallback colors there without changing gameplay code.
- `uiPrefab` is optional. If it is empty, the game builds the default runtime UI.
  If it is assigned, the prefab is instantiated under the generated Canvas and
  drives Menu, Lobby, Results, and HUD through `WebPortUiPrefabView`.
- A custom UI prefab should have `WebPortUiPrefabView` on its root and should
  bind the root objects for `menuPanel`, `lobbyPanel`, `resultsPanel`, and
  `hudRoot`. Missing required roots make the runtime fall back to the default UI.
- Text fields in `WebPortUiPrefabView` are `Component` references. They support
  built-in UGUI `Text` now and can also drive TextMeshPro components later
  without changing this gameplay code once TMP is installed in the project.
- Add `WebPortThemedImage` to prefab Images that should keep reading style data
  from `WebPortVisualConfig`. Leave it off when the prefab art should be fully
  designer-controlled.
- UI replacement fields include screen background, centered panels, HUD panels,
  buttons, inputs, progress bars, font, text colors, button colors, shadow color,
  and opacity values.
- UI image customization uses safe `UiImageStyle` slots. Each slot controls the
  sprite, image mode (`Auto`, `Simple`, `Sliced`, `Tiled`), color mode, color,
  material, raycast target, preserve aspect, and pixels-per-unit multiplier.
- `Auto` chooses `Sliced` only when the sprite has a border. Borderless sprites
  are rendered as `Simple`, which avoids broken nine-slice rendering.
- `Tint` color mode keeps the existing theme-color workflow. `PreserveSprite`
  renders the assigned sprite with white tint so finished UI artwork is not
  recolored by button or panel theme colors.
- Custom UI sprites do not call `SetNativeSize`, so imported art cannot resize
  the layout unexpectedly.
- Text uses bounded best-fit sizing so custom fonts and translated labels are
  less likely to overflow their assigned panel or button rects.
- If UI sprites are empty, runtime-generated rounded sprites are used so the
  default UI is less boxy. Assign your own sliced sprites for a custom style.
- Replacement prefabs are treated as visuals only. Their colliders are removed at runtime
  because gameplay collision is calculated by WebPort code.
- Each replacement prefab has a matching transform override in the config
  (`localPosition`, `localEulerAngles`, `localScale`). Use these fields to resize
  or align imported package, obstacle, truck, and bus prefabs without editing code.
- `diagonalFacingDeadZone` controls diagonal animation stability. Higher values keep the
  current facing animation longer when diagonal movement is near 45 degrees.

Validation performed:

- `dotnet build Assembly-CSharp.csproj --no-restore`: 0 warnings, 0 errors.
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`: 0 warnings, 0 errors.

Unity batchmode scene generation was not run because the project was already open
in another Unity instance, which Unity does not allow.
