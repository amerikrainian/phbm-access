# PixelHeroesAccess — Accessibility Mod for Pixel Heroes: Byte & Magic

Screen-reader accessibility mod for blind players, on the guildrun-access architecture by way of
monster_traina11y2 (the Mono sibling): a key graph per screen, Core/Module hot reload, the dev
server and REPL, buffers, key help. Speech is the sole interface: a silent failure is invisible to
the player, so every catch logs, and nothing caches game state.

## Game facts
- **Engine**: Unity **2022.3.62f3**, **Mono** (not IL2CPP). Managed code in
  `Pixel Heroes - Byte and Magic_Data\Managed`; the game is `Assembly-CSharp.dll` (346 types, all in
  the global namespace but `Extensions.GlobalExtensions` and the Google Play stubs). A 2015 mobile
  game ported forward: build line "Pixel Heroes: Byte & Magic - Build 1.365" (`v2015.txt`), shown
  in game as "Version 2.015".
- **Install**: `C:\Program Files (x86)\Steam\steamapps\common\Pixel Heroes` (Steam app **338320**).
  The build resolves it itself (`Directory.Build.props`: `-p:GameDir=`, `PIXEL_HEROES_PATH`, the
  Steam registry, the default path, or a gitignored `Directory.Build.local.props`).
- **Modding framework**: **BepInEx 5.4.23.4** (Mono), vendored as the release zip in
  `third_party\bepinex`, installed in the game folder by `setup-bepinex.ps1`.
- **Speech**: Prism (`third_party\prism\prism.dll`, deployed next to the exe) via hand-written
  P/Invoke in the host (`src/PixelHeroesAccess/Speech/`).
- **UI is 2D Toolkit, nothing else**: no uGUI, no TextMesh Pro, no EventSystem. Buttons are
  `tk2dButton` (handlers by `targetObject` + `messageName` SendMessage from the prefab, or the
  `ButtonPressedEvent` / `ButtonDownEvent` events from code), texts `tk2dTextMesh` (each with
  duplicate shadow meshes named `Shadow.pSHADOW_NAME_TEXTMESH`: skip them), sprites `tk2dSprite`
  (a checkbox is a sprite named `Checkbox_Checked` / `Checkbox_Unchecked`), and the game's own
  `Container` (`v_Objects`, `GetAText`/`GetAButton`/`GetASprite` by index). The screen is 320x240
  world units.
- **Screens**: one `ScreenBase` subclass per scene (`eScenes`; `ScreenBase.pCurrentInstance`, null
  during a scene transition), popups on a stack (`Popup.mActivePopups`, `Popup.GetTopMostActivePopup`),
  the shop as an overlay (`Shop.pActiveShop`), the speech box (`Talkbox.pActiveTalkbox`).
  `tk2dButton.IsTriggerable` is the game's own gate: a button counts only under the topmost popup,
  else under the open shop.
- **Input**: the game reads NO keyboard for play (the hero rename popup alone types, through the
  Input System's `Keyboard.current`); its mouse through `InputPointer` (legacy and Input System); its
  gamepad through `InputGamepad` (one per screen, `HandleControllerInput` each frame): a cursor over
  the triggerable buttons, moved by the `pContrInp*` neighbour arrays each screen sets
  (`UpdateControllerInputButtonNeighbors`) or by position, A pressing `tk2dButton.TriggerClick`, B
  the screen's cancel (`BaseTriggerEventControllerInputButtonCancelDown`: the shop's back, the top
  popup's `EventRequestCancel`, the talk box, else the screen's own), Start its back
  (`EventSystemButtonBack`, the pause menu). `TriggerClick` is how the mod presses a button.
- **Text**: the game's own tables, already in the player's language: `DataTexts.GetData(ET)` for the
  GUI and messages (341 ids), the `Data*` classes for content, all loaded from the loose
  `<game>\Data\Data_*.txt` files (each string twice: English, then German). The font draws
  characters U+00C0 to U+00E3 as icons (`FontIcon`: elements, hero values, clock, statuses, damage
  types, heal, range); `|` is a thin spacer; `~` is the data files' line break.
  `Module/Interop/GameText` reads all three (icons by the game's own words).
- **Languages**: English and German only (`Options.pLanguage`); the mod follows it.
- **Name clash**: the game declares a class `Action` in the global namespace, which wins name lookup
  over `using System;`. Every module namespace block starts with `using Action = System.Action;`
  (a file-level alias loses to the global type too).

## Decompiled reference
- `game/decompiled/` (Assembly-CSharp) and `game/firstpass/` — the decompiled game, one file per
  type, bodies included (gitignored, never committed). Regenerate with
  `uv run python tools/python/decompile.py` (needs `ilspycmd`). Grep it before guessing a shape.
- For live behaviour use the game itself: `GET /gui` for structure, `POST /eval` for values.

## Tools (`tools/python/`, standard library only; run with `uv run python tools/python/<tool>.py`)
- `dev.py <cmd>` — the dev server from the command line: `launch`, `kill`, `reload`, `nav`,
  `input ui.down ui.activate`, `speech --tail 20`, `log --grep X`, `gui --grep RE --context 3`,
  `eval file.cs`, `wait "<bool expr>"`, `typeinfo Name`, `actions`, `module`, `screenshot out.png`,
  `click [x y]`. `gamelib.py` holds the shared constants (app id, exe, data folder, port).
- `decompile.py [--game DIR]` — the decompiled reference above.
- `make_cues.py [--force]` — placeholder sound cues under `assets/audio`.

## Build & deploy
```
dotnet build PixelHeroesAccess.slnx      # everything; deploys host + Core + Module
dotnet build src/PixelHeroesAccess.Module/PixelHeroesAccess.Module.csproj   # the hot loop
dotnet test src/PixelHeroesAccess.Tests/PixelHeroesAccess.Tests.csproj      # Core + Contracts
```
Debug builds deploy to `<game>\BepInEx\plugins\PixelHeroesAccess\` (host, Contracts, Roslyn and
its facades, Core, Module, `lang\`, `assets\audio`) and `prism.dll` next to the exe.
- **A host or Contracts change needs the game closed** (their DLLs are locked) and a relaunch.
- **Core/Module changes need no restart**: the module-only build copies those two DLLs (and
  `lang\`), then `curl -X POST 127.0.0.1:8775/reload`. `GET /module` shows the generation and the
  DLL write times.
- The build must have **0 warnings**: `dotnet build ... 2>&1 | tail -5`.
- Launch: `"/c/Program Files (x86)/Steam/steam.exe" -applaunch 338320`, then wait with
  `curl -s --retry 60 --retry-connrefused --retry-delay 2 --retry-all-errors http://127.0.0.1:8775/health`.
  Kill: `MSYS_NO_PATHCONV=1 taskkill.exe /F /IM "Pixel Heroes - Byte and Magic.exe"`.
- First-time setup: `.\setup-bepinex.ps1`, then build.
- Release builds (`-c Release`) compile without deploying.

## Logs
Our lines go through the BepInEx logger with a `[Pixel Heroes Access]` source into
`<game>\BepInEx\LogOutput.log` (truncated each launch), and in-band via `GET /log?since=N&grep=S`.
The game's own Unity log is `%USERPROFILE%\AppData\LocalLow\The Bitfather\Pixel Heroes - Byte and Magic\Player.log`.
