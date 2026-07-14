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
- Player animation now supports Sprite frame arrays directly. Use
  `frontSprites`, `frontHoldingSprites`, `sideSprites`, and `sideHoldingSprites`
  in `WebPortVisualConfig`; each motion has separate `idleFrames` and
  `moveFrames`.
- Player animation also supports Sprite sheets through `frontSpriteSheets`,
  `frontHoldingSpriteSheets`, `sideSpriteSheets`, and `sideHoldingSpriteSheets`.
  Each motion has an `idleSheet` and `moveSheet` with its own `sheet`,
  `columns`, `rows`, and `frameCount`.
- Texture2D PNG sheets with separate idle/move states are assigned directly in
  the `Legacy Player Texture Sheets` section. Use `frontIdleTexture`,
  `frontMoveTexture`, `frontHoldingIdleTexture`, `frontHoldingMoveTexture`,
  `sideIdleTexture`, `sideMoveTexture`, `sideHoldingIdleTexture`, and
  `sideHoldingMoveTexture` when the imported PNG is kept as a Texture2D instead
  of Sprite.
- Runtime player animation priority is: explicit `Sprite[]` frames first,
  Sprite sheet grid second, direct legacy Texture2D idle/move fields third.
- Sprite sheet fields cut the assigned Sprite's `textureRect`, so either a full
  single-Sprite sheet or an atlas region can be used. If the image is imported
  as Multiple sprites, assign the individual frames to the `Sprite[]` arrays
  instead.
- Side-facing Sprite frames are flipped at runtime for left/right movement, so
  one right-facing or left-facing side animation set is enough.
- If a Sprite motion array is empty, the runtime falls back to the closest
  available Sprite motion. If no Sprite frames are assigned, it uses the direct
  legacy Texture2D fields in the same section. Idle texture fields are optional;
  when an idle field is empty, the matching move texture is reused.
- Character visual size is controlled by `playerSpriteWorldWidth`,
  `playerSpriteWorldHeight`, `playerSpriteLocalPosition`, and
  `playerSpriteLocalScale`. Reduce width/height or scale if newly imported
  Sprite frames look too large. These fields only affect visuals, not gameplay
  collision or movement.
- Skybox is controlled by `skyboxMaterial` in `WebPortVisualConfig`. If it is
  assigned, the WebPort camera uses `CameraClearFlags.Skybox` and applies that
  material to `RenderSettings.skybox`. If it is empty, the camera keeps using
  `pageBackground` as a solid color.
- Indoor boundary walls are generated automatically when `createBoundaryWalls`
  is enabled. The runtime calculates the rendered map bounds and places four
  long visual walls around it. Wall height, thickness, padding, material, and
  fallback color are controlled by `boundaryWallHeight`,
  `boundaryWallThickness`, `boundaryWallPadding`, `boundaryWallMaterial`, and
  `boundaryWallColor`. These walls are visual-only; colliders are removed so
  gameplay remains controlled by WebPort movement code.
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
  Package prefabs explicitly copy their authored root local position, rotation,
  and scale when instantiated. Package transform overrides are applied on top of
  that original prefab transform, so a scale override multiplies the prefab's
  existing scale instead of replacing it.
- Package gameplay collision can follow replacement prefab size. When
  `packageCollisionMatchesPrefabBounds` is enabled, package collision is measured
  from the replacement prefab after transform overrides. If the prefab has
  Collider components, those Collider bounds are used and Renderer/SpriteRenderer
  bounds are ignored. If it has no Collider, Renderer/SpriteRenderer bounds are
  used. If neither exists, `fallbackPackageCollisionSize` is used. Authored
  runtime Colliders are kept as triggers. When none exist, a trigger BoxCollider
  is generated from the combined Renderer/SpriteRenderer bounds so Unity physics
  does not compete with the WebPort gameplay solver.
- Package visual ground offset also uses the measured prefab bounds, so Sprite or
  mesh prefabs keep their authored size instead of being placed with the old
  fixed 12-unit box offset.
- `diagonalFacingDeadZone` controls diagonal animation stability. Higher values keep the
  current facing animation longer when diagonal movement is near 45 degrees.

Validation performed:

- `dotnet build Assembly-CSharp.csproj --no-restore`: 0 warnings, 0 errors.
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`: 0 warnings, 0 errors.

Unity batchmode scene generation was not run because the project was already open
in another Unity instance, which Unity does not allow.
