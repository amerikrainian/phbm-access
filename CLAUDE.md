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
- **Saves**: `<game>\Internal\` (`pixelheroes_save_progress.txt`, `pixelheroes_save_graveyard.txt`,
  the run's savestates), not AppData. Steam Cloud syncs them (`steam_autocloud.vdf`). Back them up
  before a test that loses or abandons a run.
- **Modding framework**: **BepInEx 5.4.23.4** (Mono), vendored as the release zip in
  `third_party\bepinex`, installed in the game folder by `setup-bepinex.ps1`. HarmonyX in
  `BepInEx\core\0Harmony.dll`, Mono.Cecil beside it.
- **Speech**: Prism (`third_party\prism\prism.dll`, deployed next to the exe) via hand-written
  P/Invoke in the host (`src/PixelHeroesAccess/Speech/`).
- **UI is 2D Toolkit, nothing else**: no uGUI, no TextMesh Pro, no EventSystem. Buttons are
  `tk2dButton` (handlers by `targetObject` + `messageName` SendMessage from the prefab, which reaches
  private methods too, or the `ButtonPressedEvent` / `ButtonDownEvent` events from code), texts
  `tk2dTextMesh` (each with duplicate shadow meshes named `Shadow.pSHADOW_NAME_TEXTMESH`: skip them),
  sprites `tk2dSprite` (a checkbox is a sprite named `Checkbox_Checked` / `Checkbox_Unchecked`, often
  on the button itself), and the game's own `Container` (`v_Objects`, `GetAText`/`GetAButton`/
  `GetASprite` by index). The screen is 320x240 world units. A text mesh's `text` may carry the
  word wrap's own `\n` (the tavern's backstory): read content from the data, not the mesh, where a
  line matters.
- **Screens**: one `ScreenBase` subclass per scene (`eScenes`; `ScreenBase.pCurrentInstance`, null
  during a scene transition), popups on a stack (`Popup.mActivePopups`, `Popup.GetTopMostActivePopup`;
  open and interactive is `IsActiveAndNotMoving`: while one slides in, `tk2dButton.pAllButtonsBlocked`
  is set and every button refuses), the shop as an overlay (`Shop.pActiveShop`), the speech box
  (`Talkbox.pActiveTalkbox`). `tk2dButton.IsTriggerable` is the game's own gate: a button counts only
  under the topmost popup, else under the open shop. The splash screen is no `ScreenBase` (on PC it
  shows a tip and moves on by itself).
- **Input**: the game reads NO keyboard for play (the hero rename popup alone types, through the
  Input System's `Keyboard.current`, by physical key); its mouse through `InputPointer`; its gamepad
  through `InputGamepad` (one per screen, `HandleControllerInput` each frame): a cursor over the
  triggerable buttons moved by the `pContrInp*` neighbour arrays each screen sets, A pressing
  `tk2dButton.TriggerClick`, B the screen's cancel (`BaseTriggerEventControllerInputButtonCancelDown`:
  the shop's back, the top popup's `EventRequestCancel`, the talk box, else the screen's own), Start
  its back (`EventSystemButtonBack`, the pause menu; on the title menu with nothing open it QUITS the
  game with no question asked). `TriggerClick` is how the mod presses a button.
- **Text**: the game's own tables, already in the player's language: `DataTexts.GetData(ET)` for the
  GUI and messages (341 ids), the `Data*` classes for content, all loaded from the loose
  `<game>\Data\Data_*.txt` files (each string twice: English, then German). A prefab label whose
  text starts with "GUI" is an `ET` name the scene's start replaces (`ScreenBase.ApplyGUITexts`).
  The font draws characters U+00C0 to U+00E3 as icons (`FontIcon`: elements, hero values, clock,
  statuses, damage types, heal, range; the range skips Ä, Ö and Ü, so German never collides);
  `FontIcon.ExtractFontIcons` replaces an icon in a mesh by a run of `|` and draws it in a mesh of its
  own, so such a mesh's text has lost its icons; `|` is a thin spacer; `~` the data files' line
  break; `§` a soft break hint the meshes drop. `Module/Interop/GameText` reads all of them (icons by
  the game's own words).
- **Languages**: English and German only (`Options.pLanguage`), set in the title menu's options (a
  change reloads the game through the splash screen); the mod follows it.
- **Name clash**: the game declares a class `Action` in the global namespace, which wins name lookup
  over `using System;` and over file-level aliases. Every module namespace block starts with
  `using Action = System.Action;`. Avoid `using Extensions;` (its `First`/`Last`/`IsEmpty` clash
  with LINQ); call the extension's class or the plain Unity API.
- **The REPL** (`/eval`) sees `Assembly-CSharp` as shipped: private members through reflection
  there. The module compiles against it publicized.

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
- `run_driver.py [--until town|battle|treasure|loot|inventory|talk|rooms] [--steps N]
  [--treasure try|leave]` — plays a run forward through the mod's own screens (Enter on the action a
  fight's turn lands on and its first target, Escape on a reward, Enter Room, the first option of an
  event, a popup's first button) and stops at the stage named, at the town, at a screen it does not
  know, or when a step changes nothing three times running. Keep `--steps` small and run it in the
  background: it heals nobody, so a party it drives dies soon enough.
- `decompile.py [--game DIR]` — the decompiled reference above.
- Setting up a state is faster through `/eval` than playing to it: a treasure room is
  `ScreenBase.pCurrentInstance.TransitionToNextScene(eScenes.eSceneRoomTreasure)` from a dungeon's
  inventory (`GameCore.EnterCurrentRoom` picks one 25% of the time); a defeat, every hero's
  `SetHPCurrent(1)` before the enemy's turn; the next room, `(ScreenBase.pCurrentInstance as
  Inventory).v_ButtonRoomOverview.TriggerClick()`.

## Build & deploy
```
dotnet build PixelHeroesAccess.slnx      # everything; deploys host + Core + Module
dotnet build src/PixelHeroesAccess.Module/PixelHeroesAccess.Module.csproj   # the hot loop
dotnet test src/PixelHeroesAccess.Tests/PixelHeroesAccess.Tests.csproj      # Core + Contracts
```
Debug builds deploy to `<game>\BepInEx\plugins\PixelHeroesAccess\` (host, Contracts, Roslyn and
its facades, Core, Module, `lang\`) and `prism.dll` next to the exe.
- **A host or Contracts change needs the game closed** (their DLLs are locked) and a relaunch.
- **Core/Module changes need no restart**: the module-only build copies those two DLLs (and
  `lang\`), then `curl -X POST 127.0.0.1:8775/reload`. `GET /module` shows the generation, the DLL
  write times and the Harmony patch table.
- The build must have **0 warnings**: `dotnet build ... 2>&1 | tail -5`.
- Launch: `"/c/Program Files (x86)/Steam/steam.exe" -applaunch 338320`, then wait with
  `curl -s --retry 60 --retry-connrefused --retry-delay 2 --retry-all-errors http://127.0.0.1:8775/health`.
  Kill: `MSYS_NO_PATHCONV=1 taskkill.exe /F /IM "Pixel Heroes - Byte and Magic.exe"`.
- **Dev builds keep the game running in the background** (`ModuleMain.Tick`, `#if DEBUG`, sets
  `Application.runInBackground` every frame: the game re-applies its own Run in Background option,
  off by default, at each scene start). Without it a minimized or unfocused game stops updating and
  every dev request times out ("main thread did not run the job within 30s"); after a fresh launch
  the game needs focus once for a frame to run.
- First-time setup: `.\setup-bepinex.ps1`, then build.
- Release builds (`-c Release`) compile without deploying.
- After adding or rewording a string: `PHA_WRITE_LANG_TEMPLATE=1 dotnet test ...` regenerates
  `lang/en.txt` (a deployed `en.txt` overrides the defaults, so an unregenerated one hides a change),
  then add the key to `lang/de.txt`.

## Releases (Guildrun's tooling, the dd2a11y / Non-Visual Calculus pattern)
The version lives in `Directory.Build.props` alone (`<Version>`; the host's `BuildVersion` constant is
generated from it). A release is: bump the version, add a `## Vx.y.z` section to `CHANGELOG.md` (the
release notes are read from it), commit, tag `vx.y.z` and push the tag, then:
- `build_release.ps1 [-GameDir X]` — `releases\PixelHeroesAccess-vX.Y.Z.zip`: the vendored BepInEx 5
  zip's game-folder layout (its `changelog.txt` dropped), the four mod DLLs from a clean Release
  build under `BepInEx\plugins\PixelHeroesAccess`, `lang\*.txt` beside them, `prism.dll` at the root.
  The dev server's Roslyn assemblies are Debug-only and are not shipped. The zip root is the game
  folder.
- `build-installer.ps1` — `releases\PixelHeroesAccessInstaller.exe` from `installer\` (Rust +
  wxWidgets; `test-installer.ps1` runs its unit tests). Its game facts are the constants in
  `installer\src\core\paths.rs` (exe, folder names, the Mono marker, the plugin path, the releases
  URL: `amerikrainian/phbm-access`).
- `create-release.ps1 vX.Y.Z` — the GitHub release (gh) for the pushed tag, uploading the zip and the
  installer with the CHANGELOG section as notes.
`releases\` and `installer\target\` are gitignored. README.md is the mod's name and one line linking
the game, nothing more (StarVaders' pattern).

## Logs
Our lines go through the BepInEx logger with a `[Pixel Heroes Access]` source into
`<game>\BepInEx\LogOutput.log` (truncated each launch), and in-band via `GET /log?since=N&grep=S`.
The game's own Unity log is `%USERPROFILE%\AppData\LocalLow\The Bitfather\Pixel Heroes - Byte and Magic\Player.log`.
`IconLabels` logs each caption-less button it had to name by its object name: grep `IconLabels` for
icon buttons still wanting a word.

## Navigation strategy (decided)
**Custom keyboard and gamepad navigation over the live tk2d widgets**, NOT the game's gamepad cursor.
A key graph per screen is built from the live game (`Build(GraphBuilder)`, immediate mode: declared
fresh every render, focus persisting by `ControlId` identity; equality is the STRUCTURAL key alone,
so a list that can hold the same object as another list keys it as `(listName, item)`), labels read
at speak time, and activation through the button's `TriggerClick` or the method the click reaches,
so the game's own handler runs. Where the game's interaction is a drag (the inventory) or a walk (the
town), the mod calls the game's own drop or arrival path with its state set as the drag or walk would
set it; where it is select-then-press (shops, battle), one press does both through the game's calls.

While focus mode is on (`Module/UI/FocusMode`), the game's gamepad handler is skipped (a Harmony
prefix on `InputGamepad.HandleControllerInput`, its cursor hidden); the pads are still polled for the
mod's controller bindings (`Module/Input/Gamepad`, the Input System's `Gamepad.all`). The game reads
no keyboard, so nothing else needs silencing. Ctrl+Shift+A hands everything back.

**Button order is one rule everywhere**: the content, then the way out (Leave, Back, Cancel,
Close, Exit), then the system buttons (Quest Log, Help, Pause), then the status lines.
`GameScreens.LiveButtons` sorts by it (`ButtonRank`, by object name, then reading order); a screen
listing buttons by hand follows it too.

**No scene scans in feature code** except where the game gives no registry: screens find their game
screen by `ScreenBase.pCurrentInstance` and its serialized fields (`Module/Interop/GameScreens`); the
fallback's button and text lists walk the topmost input root's children (a popup, the shop, the
scene), which the game's own gamepad does every frame too.

## Architecture — HOST/MODULE split
Five projects, all under `src/`:
- **`PixelHeroesAccess.Contracts`** (netstandard2.0, PERMANENT): `IModHost`, `IModModule`,
  `IDevDriver`, `ISpeechBackend`, `SpeechPipeline`, `ISettingsStore`/`ModSettings`, `TextFilter`.
- **`PixelHeroesAccess`** (netstandard2.1, PERMANENT host; **changing it needs a game restart, keep it
  minimal**): `Plugin` (BepInEx 5 entry; GUID `com.amerikrainian.pixelheroesaccess`), `Speech/`
  (Prism), `Host/HostPump` (the one MonoBehaviour), `Modularity/ModuleLoader`, `Dev/` (dev server,
  REPL; `GuiInspector` dumps the tk2d scene, `FocusInspector` the game's gamepad target, popup, shop
  and talk box). References `Assembly-CSharp` for those inspectors. Never references Core or Module.
- **`PixelHeroesAccess.Core`** (netstandard2.0, RELOADABLE, engine-free, unit-tested): `Graph/`,
  `UI/` (`GraphNavigator`, `ChoiceSubmenuScreen`'s menus, `KeyHelp`, `TypeAheadSearch`), `Input/`
  (`InputManager`, `KeyBindings`), `Screens/`, `Buffers/`, `Events/` (the log the log buffer reads),
  `Strings/`.
- **`PixelHeroesAccess.Module`** (netstandard2.1, RELOADABLE, engine-coupled; **day-to-day feature
  work goes here**): `ModuleMain` (seams, input registration, screen registration, Tick,
  `IDevDriver`), `Screens/` (one file per game screen or family, below), `Battle/BattleEvents` (the
  fight's narration hooks), `Nodes/` (`ItemLines`, `HeroLines`, `HeroNodes`, `StatsLines`: the
  reusable readers), `Interop/` (`GameScreens`, `GameText`), `UI/` (`GameNodes` node factories over
  tk2d, `FocusMode`, `Buffers`, `SpeechBubbles`, `IconLabels`, `TextEntry`, `RunGlance`, `Later`, `ScreenIntro`),
  `Input/` (`KeyboardBinding`, `ControllerBinding`, `Gamepad`, `UnityNavInput`), `LanguageSync`,
  `UpdateChecker`. References `Assembly-CSharp` **publicized**
  (`BepInEx.AssemblyPublicizer.MSBuild`): private members read as plain fields, nested private enums
  too, so a member the game renames is a compile error.
- **`PixelHeroesAccess.Tests`** (net8.0 xUnit): Core + Contracts only; runs serially.

**Hot reload on Mono** (MT2's): each generation loads as NEW assemblies renamed with Mono.Cecil
(`PixelHeroesAccess.Core.gN` / `.Module.gN`); old generations stay loaded and idle. The new one loads
and patches before the old one is disposed. Module Harmony uses a per-load GUID id and `UnpatchSelf()`
in Dispose; patching is class by class (`ModuleMain.Load`), so a hook a game update broke loses that
hook alone and logs it. Patched parameters are taken positionally (`__0`): the game's parameter names
are not stable (`bActionOutcome`). No MonoBehaviour types in the Module.

## Screens (`Module/Screens`, registered in `ModuleMain.RegisterScreens`)
Base classes (`GameScreenBase.cs`): `SceneScreen<T>` is live while scene `T` is current and nothing
covers it (no popup, shop or talk box), keeps its focus across a popup (`KeepStateOnPop`), Escape the
game's cancel; it OWNS its scene, so in a cutscene state it builds nothing and stays silent rather
than letting the fallback read disabled figures. `PopupScreen` is live while its popup is topmost and
done moving (layer 10). Layers: the fallback -10, scenes 0, the talk box 5, the shop 8, the reward 9,
popups 10, the rename popup 12, the mod menu 50.
- **`GenericScreen`** (the fallback): the buttons under the input root in reading order (disabled
  ones too, as disabled; a checkbox button as a toggle; an icon button by `IconLabels`), and over a
  popup its loose texts too, said as it opens, landing on its first button; Escape the game's cancel,
  then the popup's own close when the cancel leaves it up (the base `Popup` refuses cancel). Figures
  and scenery (hero, enemy and NPC selectors, the tavern's barkeeper and band, the "Blackness_*"
  border blockers, the town's walking area) are left out (`GameScreens.IsFigure`). It covers the
  small popups (title menu's Continue/New Game, the abandon warning with its countdown, achievements,
  leave town, the info popups, the inventory break, throw away for fist) and the library's choice.
- **Title**: `CampaignSelectScreen` (a campaign per control, its description and lock line);
  `PauseScreen` (pause page; options page's tabs, the window mode, language and music as radio
  groups, the toggles). `CreditsScreen`, `ProgressScreen` (campaigns, townspeople, hero classes,
  achievements from `PlayerProgress`, whatever is scrolled into view), `GraveyardScreen` (tombstones
  from their records; landing selects one; its panel the control buffer).
- **Tavern**: `TavernScreen` (the heroes for hire, the party the game keeps sorted, Dismiss, re-roll,
  Start, the barkeeper (a new game only) and the band, each answering with a speech bubble; a later
  visit's drinks with their cost and the band), `TavernHeroCardScreen` (the backstory rebuilt from
  the hero, the per-point gains, the skills, Hire, Rename). `RenameScreen` + `UI/TextEntry`: the
  rename popup types by itself; the mod's keys stand down, each change is echoed, Enter presses OK
  (two characters with a letter, the game's rule), Escape cancels.
- **Talks**: `TalkScreen`, any `Talkbox` (an NPC, the campaign opening, the temple, the library, fixed
  texts, overland events): each page said as it comes (the speaker when it changes, with the quest
  and dungeon), Enter turns the page in one press (the game's first click only finishes the
  typewriter), a halted last page or an event's options take focus (once the render holds them),
  taken choices vanish quietly; Escape is the game's cancel, which over a quest offer refuses it.
- **Town**: `TownScreen`, the street's places in order (buildings, blacksmith, the "!!!" guys,
  townspeople by the middle of their wander, with their quest standing), entered through the game's
  own arrival path (party placed at the door, the zero-length walk completing); the interface, gold,
  the quest under way. `ShopScreen` (any shop; one press buys or sells through the game's own
  select-then-Buy/Sell; potions are not sold). `TempleScreen` (revive per dead hero with its price,
  the potion shop).
- **Road and dungeon**: `OverlandScreen` (destination, event n of 3), `RoomOverviewScreen` (room n of
  8, Enter Room first), `InventoryScreen` (hero rows of five slots, attribute points, the backpack,
  item menus: equip on each living hero's matching slot, move hands, unequip, drink, throw away with
  a confirmation the game lacks; hero swaps; every move the game's `PerformItemDrop` with its drop
  target set), `TreasureScreen` (a try per living hero with the tested attribute and the game's
  chance; Destroy, Leave; the message line said as an attempt plays), `LootScreenScreen` (the reward
  taken as `CreateLoot*` makes it, since the game grants it at once and the screen only animates it).
- **Battle**: `BattleScreen` — on the heroes' turn one stop, the field, in two regions: a row per
  hero (state, two hands, two skills, whatever the game's hand/skill toggle shows; a stunned hero's
  one action), then the enemies as one row front to back, "1, Creepy Crawler 2, Healthy" (the place
  is what a range counts, from the enemies' front whoever strikes; the condition word is all the
  game shows of enemy health, nothing for a boss). Heroes and enemies are not paired by place: the
  game's ranges give that no meaning. Enter selects as the slot's click does
  (`BattleGUI.SelectHand`; the rest and cooldown refusals said); then the side the action addresses
  leads with "target" ("all") or "out of range" (the game's selectors), focus goes to the first
  target, Enter strikes, Escape returns to the action. Backspace offers throw away for fist. A new
  turn lands on the first action of the first hero who may act. Enemies sharing a name are numbered
  by their place at the fight's start. `Battle/BattleEvents` narrates in the game's own log words
  (`LogScreen.AddMessageForAction`, deduplicated by outcome; `AddMessageForStunned`), plus what the
  game never logs: status ticks and ends (`StatusEffect.UpdateTick`), and game over and victory from
  `GameCore.HandleGameOver` (the scene changes the same frame). The heroes' remarks are
  `UI/SpeechBubbles`' (below). The game has no crits and no fleeing.
- **Arena**: `ArenaStartupScreen`, `ArenaPartyScreen`, `ArenaHeroCardScreen`, `ArenaHubScreen`
  (round, gold, next enemies, bonuses with prices, re-roll, the shops, Fight).
- **Help and log**: `GameHelpScreen` (the help popup's four tabs from the game's sources: statuses in
  the game's order with turns; element relations the game draws as a diagram, from
  `GameCore.CalculateElementRelationSourceToTarget`; items; heroes), `QuestLogScreen`.
- **Mod**: `ModMenuScreen` (settings, all keys), `HelpScreen` (F1), `KeyBindingScreens`.
- **Speech bubbles** (`UI/SpeechBubbles`, not a screen): every single-line bubble, said as it
  appears with its speaker ("Calen: Damn you!", the hero, else the NPC it hangs from, else nobody: the
  shop's "Thank you!") and kept in the log buffer: fight remarks (a hit's or heal's held until after
  its outcome line), the tavern's hired and rejected heroes (said at Start), its barkeeper and band
  ("band" the mod's word; both its band lines are the same sentence in the game's data), the shop, the inventory's swap hint, and
  the idle chatter of town, tavern, temple, library and arena (the town's about every 3.5 seconds).
  The line is taken at `ShowWithText` and said at `InternalShowSpeechBubble` without a delay, since
  some wait two seconds first. Two mod menu settings: Speech bubbles, and Idle chatter among them.

## Keys
Focus mode is on at launch; Ctrl+Shift+A toggles it. F1 is the key help: the keys that do something
RIGHT NOW (a handler key silent somewhere says so with `.When(...)`). Ctrl+Shift+M the mod menu.
Arrows navigate, Tab/Shift+Tab cycle stops (always wrapping; silent with one stop), Enter activates,
Backspace is the secondary action, Escape backs out (the game's cancel; on a scene where that has
nothing to do, `SceneScreen.PauseButton`/`CancelActs`, the pause button), Home/End jump,
Alt+Up/Down jump sections, letters search the focused stop, Ctrl+arrows review the buffers. In a
run (`UI/RunGlance`, Global category, live only where the line exists): Ctrl+H the party's health, Ctrl+G the gold, Ctrl+Q the quest under way. Ctrl+Tab (pad Y, Global, `ShopScreen.SwapButton`) goes between a shop and the inventory either way:
the shop's Inventory button, the inventory's Shop button (the arena's: its Go, which reopens the shop). Every action
carries a gamepad binding too (`ControllerBinding`). The mod menu's All keys rebinds any of them.

## Buffers (`Core/Buffers`, `Module/UI/Buffers`)
Ctrl+Left/Right switch buffers, Ctrl+Up/Down step lines; empty buffers are skipped; a focus change
re-homes review to the control's buffer. **control** (the focused node's head line, then its
details: an item's facts, `ItemLines`; then the help's entry for each status, element and
Redemption the lines name, `Nodes/Definitions`, also closing the hero buffer and an enemy's
statuses), **hero** and **items** (`NodeVtable.SideLines`: the hero a control concerns, whole,
`HeroLines.Lines`, and its equipment and skills a line per slot), **log** (the fight's narration
and the rewards, newest first).

## Languages (`Module/LanguageSync`, `Core/Strings`, `lang/`)
The mod's words follow the game's language: `Options.pLanguage` (English or German), compared each
tick (one static field; the game raises nothing when it changes). `lang/en.txt` (the template) and
`lang/de.txt`; `LangFilesTests` expects exactly those two, complete. Game terms are the game's own:
`GameText.Tr(ET)` and the data classes. Key names in the key help are not translated.

## Hard rules
- **All speech through `Speech.Say`** (Core); never call Prism directly. Navigation moves interrupt;
  screen entry and feedback queue. A screen that speaks its own entry (the talk box, the rename
  popup) quiets the landing (`Navigation.QuietNextLanding`).
- **Never cache game state.** Hold live widgets and read them at speak time. Event data is the
  exception (a reward as it was made, the enemy numbering by identity), like a log line.
- **Reuse game text** wherever it exists; every mod-authored word lives in `Core/Strings/Strings.cs`
  (keyed, overridden by `lang/<code>.txt`). Templates hold for any count ("points to spend: 1").
- **No silent failures**: every catch logs through `CoreLog`.
- **Announcements**: distinguishing word first, no nav hints, no fluff, never less information.
- **Never press a destructive button through the driver without reading its handler first**: the
  title menu's Exit and the pad's Start there quit at once; New Game over a save abandons it.
- **0 warnings**; no `#pragma` or `[SuppressMessage]` to get there.
- Beware `int.MinValue` "long ago" sentinels: use `int.MinValue / 2`.

## Open items
- The installer is ported from MT2's and has not been run against a real install.
- No manual (`docs_src`) yet.
- The splash screen's tip is not read (on PC it passes by itself).
- The library has no screen of its own (the fallback reads its choice; talk and shop are covered).
- Enemy skills, defenses and elements are not exposed (the game shows none of them; an element shows
  only as Strong, Weak or Absorbed on a hit, which the narration says).
