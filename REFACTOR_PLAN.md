# Refactor & Hardening Plan — Taiwu_Godot

Written from a full audit of the live project (Godot 4.7 / .NET 8, ~21k lines C#, 62 tests).
Item numbering follows the audit report.

**Status**

| # | Item | Status |
|---|---|---|
| 1 | Version control of the live project | Owner handling |
| 2 | Delete `scene-references/` + remove its `GuDatabase` fallback paths | **Done** |
| 3 | Single map authority | **Done** (Phase 2) |
| 4 | Honest test coverage | **Done** (57 domain tests + harness; see Phase 1 caveats) |
| 5 | Instance event bus / composition root | Planned — Phase 5 (per-view) |
| 6 | Save pipeline correctness | Planned — Phase 3 |
| 7 | UI decomposition | In progress — Phase 4 (scaffold landed) |
| 8 | Remove fake engine types | Planned — Phase 2 (with 3) |
| 9 | Input actions, pause, determinism | Planned — Phase 3 |
| 10 | Project hygiene | Planned — Phase 0 |

Target dates are effort estimates, not calendar commitments. Every phase must leave the game
buildable and the test suite green; none of this requires a rewrite or a freeze.

---

## Phase 0 — Hygiene (half a day)

Small, mechanical, and it removes noise that makes every later change harder.

1. `README.md` in the project root: what the project is, how to run it in Godot, how to run
   `dotnet test`, where saves live (`%USERPROFILE%/.taiwu_saves/Saves`, overridden by
   `SavePaths.BaseSaveDirectory`), and a 10-line architecture sketch.
2. `.editorconfig` at the project root (the three currently in the repo are Unity's). C# style,
   analyzers on. A clean build currently emits **35 pre-existing nullable warnings** (CS8600/8602/
   8603/8618) in `SaveDiskIO`, `SaveCoordinationService`, `DiagnosticCapture` and the tests — fix
   those first, then enable `TreatWarningsAsErrors` for `Core.Logic` so it cannot drift back.
3. Delete `Taiwu_Godot.csproj.old` (4.3.0 SDK leftover).
4. Move `test_*.png` (10 files, 9.3 MB) out of the project root, or into `docs/captures/` with
   their `.import` files removed. They are currently imported as game resources and would ship.
5. Remove the `[SYSTEM_DIAGNOSTIC]` node from `Scenes/OverworldScene.tscn`. Keep
   `DiagnosticCapture` as a separate `Scenes/VerificationHarness.tscn` used only with
   `--headless --verify`.
6. Project-local `.gitignore`: `.godot/`, `bin/`, `obj/`, `*.user`, `test_*.png`, `Screenshots/`.
   Note for the VCS work: the parent `.gitignore` has Unity's `*.csproj`, which must be
   excepted for this subtree or a fresh clone will not build.

**Acceptance:** `dotnet build` and `dotnet test` still green; `git status` after a full build
shows no build output; the game boots from the main scene with no diagnostic node.

---

## Phase 1 — Make the tests tell the truth — **Done**

**Delivered**

- `Core.Logic.Tests/Stubs/` deleted, along with every test that only exercised those fakes:
  Phase1And2 Test06, Phase3 Test06 (and the stub-reflection tail of Test10), Phase4 Test06,
  Phase5 Test01–02. Test count went 62 → 57; the 5 removed could not fail for a real reason.
- Suite retargeted to `net8.0` (the game's target) with `global.json` pinning 8.0 intent and
  `rollForward: latestMajor`.
- `Scripts/Verification/VerificationRunner.cs` added: asserts state shape, rendered-grid shape and
  coordinate orientation, and a full save → load → compare of every tile. `DiagnosticCapture`
  parses `--verify` / `--strict-poi` and quits with the runner's exit code.
- `SaveRoundTripTests` added so the harness's core assertion is covered by `dotnet test`.

**Caveats (honest state)**

- Only the .NET 10 runtime is installed on this machine. The suite compiles against the `net8.0`
  API surface but executes on 10 via `RollForward=Major`. Install the .NET 8 runtime to make
  "runs on the shipped runtime" literally true.
- No Godot binary is on PATH or in the usual user-scoped install locations, so `--verify` has not
  been executed yet. Run
  `godot --headless --path . -- --verify` (expect exit 0) and
  `godot --headless --path . -- --verify --strict-poi` (expect exit 0 since Phase 2 landed;
  `--strict-poi` additionally asserts the POIs the game renders are the POIs in state).

**Problem.** `Core.Logic.Tests/Stubs/ViewStubs.cs` declares fake `TaiwuGu.UI.TaiwuLedgerView`,
`TilePOIModalView`, `RefinementCauldronView`, `TaiwuSystemMenuView`, plus fake
`TaiwuInputRouter` and `DedicatedBattleStage`. `Phase3ArchitecturalTests.Test06`/`Test10` reflect
over those fakes to prove the "decoupling law". The real views are `Taiwu_Godot.Scripts.UI.*`
(300–1,681 lines) and the test project has no Godot reference, so the largest and most fragile
layer has zero coverage while CI reports 62/62 green. The suite also runs on `net10.0` while the
game ships on `net8.0`.

**Target.** Domain tests that exercise real services; one headless harness that exercises the real
scene graph; no tests over placeholders.

**Work**

1. Delete `Core.Logic.Tests/Stubs/ViewStubs.cs` and the two reflection-based decoupling tests.
   They cannot fail for a reason that matters. Do not replace them with another reflection check.
2. Retarget `Core.Logic.Tests` to `net8.0` and add `global.json` pinning SDK 8.x, so tests run on
   the runtime the game runs on.
3. Convert `DiagnosticCapture` from "print and screenshot" into assertions with a real exit code:
   - `--verify` boots `OverworldScene.tscn`, asserts the tile manager's tile count equals
     `StateContext.WorldMap.Tiles.Length`, asserts every POI in state is the POI rendered at that
     coordinate, exercises save → load → compare, then `GetTree().Quit(0|1)`.
   - This is the cheapest honest integration layer and reuses machinery you already have.
4. Keep the existing domain tests (they are real) and add the missing critical ones named in
   Phases 2, 3 and 6 below.
5. Optional later: adopt GdUnit4 for scene-level UI tests (modals, LIFO escape order, pause).
   Not required for the above and not a prerequisite for anything else.

**Acceptance**

- `dotnet test` covers domain only and passes on .NET 8.
- `godot --headless --path . -- --verify` returns exit code 0, and returns 1 if the map, a POI,
  or a save round-trip is wrong (prove it by temporarily breaking one and seeing the failure).
- `grep -r "TaiwuGu.UI" Core.Logic.Tests` returns nothing.

**Do not**: build a mock framework, a UI abstraction layer, or DI just to make views testable.
The headless harness is enough.

---

## Phase 2 — One map authority — **Done**

**Landed.** `Core.Logic/Map/SectorGenerator.cs` (pure, xorshift32-seeded, BFS-connected, preserves
 the designed anchor POIs) is the only tile author. `WorldMapState.InitializeDefaultGrid()` calls it;
 `IsometricTileManager` is projection-only and reads every tile through `MapContextProvider`.
 `FogOfWarManager` is deleted — `ExplorationService` is the only fog mutator. Sector snapshots store
 seed + explored/visible indices + sparse `TilePatch` deltas (save version 2.1.0; a 2.0 payload's
 per-sector tile arrays are converted to patches on load rather than dropped).
 The three parallel visibility representations are now one (`TileNode.IsExplored/IsVisible`).

**Problem (historical).** Tiles are authored in two incompatible places:

- `GameStateContext.CreateDefault()` (`GameStateContext.cs:742`) calls `InitializeDefaultGrid()`
  (`:434`): a flat 30×30 grid, all 900 tiles `IsActive`, POIs at hardcoded coordinates.
- `IsometricTileManager._Ready()` (`IsometricTileManager.cs:24`) generates a *different* organic
  island with its own BFS culling and POI scatter (`PopulatePOIs`), ignoring state entirely.
- The bootstrapper syncs state → view at boot, so both write the same grid and the later one wins.
- `SyncToStateContext` — the only path from the rendered grid back into state — has **no callers**
  in the Godot project (only the deleted Unity copy called it).

Net effect: the tests assert POIs the game does not show, and saves persist the seeded grid rather
than what the player explored. There are also three parallel representations of visibility:
`TileNode.IsExplored/IsVisible`, `WorldMapState.ExploredTileIndices/VisibleTileIndices`, and
`SectorMapSnapshot`'s copies of both.

**Target.** The domain owns the world. The renderer is a projection. Save/load round-trips exactly.

**Work**

1. Add `Core.Logic/Map/SectorGenerator.cs` — pure and deterministic:
   `TileNode[] Generate(int seed, int width, int height, WorldRegionID region, BiomeType biome)`.
   Move the organic mask, the BFS connectivity cull, danger ranking, and POI placement out of
   `IsometricTileManager`. Take the seed from `WorldMapState.Seed`; the literal `17881588` must
   exist in exactly one place (the default) or disappear.
2. Use an explicit PRNG with a documented algorithm (small xorshift/PCG seeded from `Seed`).
   `System.Random` is not guaranteed stable across .NET versions and will silently break old saves.
3. Make `WorldMapState.InitializeDefaultGrid()` call the generator instead of hand-writing tiles,
   so there is one tile-authoring path.
4. Strip generation from `IsometricTileManager`: delete `GenerateOrganicRegionInstance`,
   `GenerateRegionInstance`, `PopulatePOIs`, `CullDisconnectedIslands` and `instanceGrid`.
   `_Ready` allocates nothing until it receives `SyncFromStateContext`. `IsTileWalkable` asks the
   domain (`WorldMapState.ValidateStep` / `LandmarkRegistry`), so rendering and pathfinding can
   never disagree.
5. Visibility: keep `TileNode.IsExplored/IsVisible` as the only stored truth. Make
   `ExploredTileIndices`/`VisibleTileIndices` computed at save time (or remove them). `FogOfWarManager`
   and the tile manager react to `OnFogOfWarUpdated` and must not mutate `WorldMapState`.
6. Sector snapshots: store seed + deltas (unlocked landmarks, destroyed tiles) instead of cloning a
   900-element `TileNode[]` per sector. Deterministic generation makes a sector reproducible.
7. Delete `SyncToStateContext` once nothing writes the view back — it should not need to exist.

**Acceptance**

- `SectorGenerator` test: same seed twice → identical tiles; different seed → different.
- Test: save → load → every one of the 900 tiles equals the pre-save state (struct equality).
- End-to-end: a POI asserted in state is the POI shown by `IsometricTileManager.GetTile`.
- `grep -c "GenerateOrganicRegionInstance\|PopulatePOIs" Scripts/` == 0.

**Do not**: keep a "verify and resync" step at boot as a safety net. If both sides can author
tiles, one will drift again. Remove one author.

---

## Phase 3 — State provider, saves, input, determinism (2–3 days)

**Status.** 3a, 3b and 3c landed.

**Landed (3a).** `IGameStateProvider` + `GameStateProvider` (swappable) + `StaticGameStateProvider`
 (tests). All eleven domain services resolve `provider.Current` per call; the bootstrapper owns the
 single provider and passes it to every service. `SaveCoordinationService` installs a loaded save
 with `provider.Replace`, so a load is a pointer swap instead of field-by-field mutation.

**Landed (3b).** One `save.json` per slot (metadata embedded in the payload, so the two cannot
 disagree — the old `metadata.json`/`payload.json` layout is still read). Writes go to a temp file
 then `File.Replace`, with the superseded bytes kept as `.bak`; the loader tries primary, `.bak`,
 then the legacy layout, so a crash yields the old save or the new one, never neither. Slot ids are
 validated (`[A-Za-z0-9_-]{1,32}`) before touching the filesystem. `SaveCoordinationService` now
 awaits the asynchronous write behind an `IsSaving` reentrancy guard; `HydrateContext` is deleted.

**Landed (3c).** `project.godot` declares `toggle_ledger`, `open_cauldron`, `open_breakthrough`,
 `toggle_system_menu` and `recenter_camera`; `TaiwuInputRouter` is the only code that reads them
 (`InputEventKey.IsActionPressed`), and the bootstrapper's keycode chain and the provider's duplicate
 keyboard branch are deleted. The bootstrapper is explicitly `Pausable` and clamps every tick to
 0.1 s. Simulation randomness goes through `IRandomSource`/`SeededRandom` (xorshift32, seeded from
 the new persisted `GameStateContext.RunSeed`), wired into refinement, exploration, wild encounters,
 combat, transit and breakthrough.

**Remaining from 3c, deliberately deferred.** `GuDatabase` (static catalog) and the NPC generator
 still use `Random.Shared`/`new Random()`; the on-work shift scheduler was not converted. The RNG
 stream restarts from the saved seed on load rather than resuming mid-stream.

### 3a. One live-state seam

**Problem.** All 14 services capture `GameStateContext? _context` in `Initialize`. That makes an
atomic context swap impossible (needed for safe load), and every service holds a private
reference to the truth.

**Target.** A single `IGameStateProvider` with `GameStateContext Current { get; }`, owned by the
bootstrapper. Services read `provider.Current` per call instead of caching the object. This is a
mechanical, sed-scale change, not a rewrite — and it is a prerequisite for safe loading.

### 3b. Save pipeline

**Problem.**

- `CommitAtomicReplace` (`SaveDiskIO.cs:391-398`) does copy → `File.Delete(target)` → `File.Move`.
  The canonical file does not exist between delete and move; a crash there loses the save.
- Payload and metadata are replaced in two independent operations (`:48-49`) and can disagree.
- The async API (`SaveContextAsync`, `LoadPayloadAsync`, `LoadAllMetadataAsync`) has zero callers;
  `SaveCoordinationService` claims "non-blocking" but calls the synchronous path and serializes the
  whole 900-tile graph on the main thread.
- `HydrateContext` assigns loaded sub-objects by reference into the live context field by field; a
  partial failure leaves a half-swapped game.
- `SavePaths.GetSlotPath(slotID)` interpolates unvalidated input into a path.

**Target.** One durable write path, one atomic load swap, validated identifiers, the async path
actually used.

**Work**

1. Replace copy/delete/move with `File.Replace(tempPath, targetPath, backupPath)`. Keep the existing
   `.bak` recovery on load.
2. Collapse metadata + payload into a single `save.json` (they are one logical unit), or — if you
   want the cheap version — keep two files but treat `metadata.json` as the commit marker and reject
   a payload whose metadata is older. Single file is less code and fewer failure modes; prefer it.
3. Keep `SaveVersion` and the existing "reject unknown version" behavior. Do **not** build a
   migration framework for versions that do not exist; add one function per real migration when a
   v3 appears, plus a test per supported version.
4. Load: deserialize → validate → build a **new** `GameStateContext` → `provider.Replace(newCtx)` →
   broadcast. Never mutate the live context during load.
5. Validate `slotID` with `^[A-Za-z0-9_-]{1,32}$` in `SavePaths.GetSlotPath`; reject anything else.
6. Use the async path from `SaveCoordinationService`: serialize on the main thread (required for
   consistent snapshots), write with `await`, and drive a visible "Saving…" state. Delete whichever
   path you do not choose — do not maintain two.

**Acceptance**

- Test: simulate interruption at each step (temp written, target deleted, replace mid-way) → loader
  always recovers either the old or the new save, never neither.
- Test: two slots saved and loaded independently; a load cannot partially mutate the previous state.
- Test: `slotID = "../evil"` is rejected.
- Main-thread time for a save measured before/after; report actual numbers, not a claim.

### 3c. Input, pause, determinism

**Problem.**

- `project.godot` has **no `[input]` section**. Esc/Tab/L/C/B are literal `Key.*` checks in
  `TaiwuGameBootstrapper._UnhandledInput`, with handling split across `TaiwuInputRouter` and
  `StandaloneInputProvider`. No rebinding, no gamepad, and `C`/`B` collide with future text entry.
- `TaiwuSystemMenuView` sets `GetTree().Paused = true`; the bootstrapper is Pausable so all five
  service ticks stop, while every UI node is `ProcessMode.Always` and keeps animating. Any UI timer
  driven by its own `_Process` will desync from the frozen simulation (verify: cauldron needle).
- `_Process` passes raw delta to five services with no clamp; one hitch can skip a refinement phase.
- `Random.Shared` in `GuRefinementService` plus non-portable `System.Random` makes the minigame
  non-reproducible and untestable, which is why tests write `WillAlignmentProgress` directly.

**Work**

1. Declare actions in `project.godot`: `ui_cancel`, `toggle_ledger`, `open_cauldron`,
   `open_breakthrough`, `toggle_system_menu`. Use `Input.IsActionJustPressed` in exactly one place
   (`TaiwuInputRouter`); delete the duplicate key checks from the bootstrapper and the duplicate
   input provider.
2. Decide the pause contract and enforce it in one node: a `Simulation` node (Pausable) owns all
   ticked services; UI stays `Always` but renders **only** from ticked state. If a view animates
   from a local timer, move that timer into the ticked service state so pause freezes it visibly.
3. Clamp `dt = MathF.Min((float)delta, 0.1f)` at the tick entry, and make phase transitions consume
   or discard leftover time explicitly.
4. Introduce `IRandomSource` seeded per session and persisted in the save; use it for wild will
   frequency, wild Gu satiety, and encounter rolls. Inject it into services.

**Acceptance**

- Rebind a key in `project.godot` and the game honors it with no code change.
- Headless test: pause during Phase 2, resume, assert no phase skip and no soft-lock.
- Refinement test with a fixed seed asserts an exact outcome twice.

**Do not** add a third-party input library. Godot's action map is the right tool.

---

## Phase 4 — UI decomposition (1–2 weeks, incremental)

**Problem.** `TaiwuLedgerView` is 1,681 lines with 115 `AddChild` calls and 76 inline widget
constructions; `RefinementCauldronView` 845; `TaiwuHUDView` 677. Every one of 11 views reimplements
the same modal scaffold (backdrop, panel, header, close, LIFO registration) — ~100 duplicated lines
each. There is exactly **one `.tscn`** in the project; all UI is code-built, so there is no visual
iteration. Views also hardcode domain values (`_minStonesRequired = 11`, `_phase1Duration = 4.0f`,
`_playerStones = 250`, `_recipeName`), so every balance change requires touching UI code.

**Target.** Scene-based views, one reusable scaffold, no domain constants in presentation.

**Work**

1. `Scenes/UI/ModalScaffold.tscn` (layout: backdrop, centered panel, header with title, an
   accessory slot and a close button, and a content root) plus `Scripts/UI/ModalScaffold.cs`
   (behaviour only: open/close, LIFO registration of the owning `IModalView`, Escape for the stack
   top, backdrop dismissal, event cleanup). Layout stays declarative; no view builds chrome in
   code. Repeated dynamic content also becomes a packed scene
   (`Scenes/UI/CultivatorCard.tscn`) instead of a hand-built widget. Delete the same ~100 lines
   from all 11 views. Do this first: it is the biggest line reduction for the least risk.
2. `UI/TabbedModal.cs` on top of it for Ledger / Cauldron / Breakthrough / Transit.
3. Split `TaiwuLedgerView` into four `Control` subclasses (`CultivationTab`, `GuWormsTab`,
   `DaoMarksTab`, `FactionsTab`), each with `Bind(<readonly record> viewModel)`. The view model is
   built from the event payloads the view already receives.
4. Delete hardcoded domain values; read them from `CauldronRefinementState` / `GuWormData` /
   `CultivatorState` that arrive in the existing events.
5. Convert each modal to a `.tscn`: in the Godot editor, run the game, select the built node, "Save
   Branch as Scene", then delete the construction code. Do it one view at a time, starting with
   Ledger (biggest), then Cauldron, then HUD. Do not attempt all 11 in one pass.
   Landed: `TilePOIModalView` composes `ModalScaffold.tscn` and instantiates `CultivatorCard.tscn`
   per resident cultivator; `TaiwuLedgerView`, `RefinementCauldronView` and `TaiwuSystemMenuView`
   compose the same scaffold and keep only their bodies (subtitle, tabs or phase panels, footer).
   The system menu puts Resume in the scaffold's header accessory, because resuming is not the
   scaffold's generic dismiss, and its `Dismiss()` steps back out of a sub-view first.
   `TabbedModal.tscn` layers a header row area, a tab strip and a tab body on the scaffold, so the
   ledger's four-tab bar and the system menu's five sub-views are registered rather than rebuilt
   per view. Views seed no domain values: the cauldron, ledger and HUD start empty and render only
   what `CauldronRefinementState`, the resource events and the calendar events supply, with the
   cultivator's identity broadcast instead of hardcoded. Views keep their remaining widget rows in
   code until each gets its own `.tscn`. The scaffold replaces the per-view Escape priority checks
   with one stack-top rule.
6. While rewriting each view, migrate that view's subscriptions to the instance bus (Phase 6) — do
   not migrate the same file twice.

**Acceptance**

- No view file over ~400 lines.
- Zero `new Label(` / `new Button(` outside the shared UI factory.
- Every modal has a `.tscn`.
- `grep -rn "= 250\|= 11\|= 4\.0f" Scripts/UI/` returns no domain constants.
- Escape order and modal behavior still pass the headless harness.

**Do not** adopt an MVVM framework, reactive bindings, or an ECS for UI. `Bind(state)` on Control
subclasses plus scenes is the correct size for a solo project.

---

## Phase 5 — Event bus and composition root (as part of Phase 4, plus 1–2 days)

**Problem.** ~100 `public static event` members in one 474-line class, a 200-line
`ClearAllSubscriptions()` called from production `_ExitTree`, and `public static bool EscapeHandled`
as a global input handshake. `Bootstrap_Validation()` prints failures but still returns true with
views missing. Statics make two game instances in one process impossible, force the whole test
suite to be serialized, and make shutdown order load-bearing.

**Target.** One instance `IGameEvents` owned by the bootstrapper, injected into services and views.
No static mutable state in the domain. Hard failure when wiring is incomplete.

**Work**

1. Extract `IGameEvents` with today's command/state split. Keep the same event names and payloads so
   migration is mechanical.
2. Start with the 14 services: construct them with `new XService(events, stateProvider)`.
   Keep a thin static adapter (`GameEventManager` forwarding to the current instance) during
   migration so views keep working; delete the adapter only at the end.
3. Migrate views one per Phase 4 rewrite, passing `events` through the existing `Initialize(...)`
   injection points. Do not introduce scene searching or a service locator.
4. Delete `EscapeHandled`. `TaiwuModalStack.TryPop()` already returns a bool — that is the single
   owner of input priority.
5. `Bootstrap_Validation()`: missing required node → `GD.PushError` and refuse to boot (or boot into
   an explicit debug state that is visibly broken). Never "log and continue" for required wiring.

**Acceptance**

- `grep -c "static event" Core.Logic` == 0 and `ClearAllSubscriptions` is gone.
- Test: boot two independent game instances in one process; mutating one does not affect the other.
- Removing a required view export makes boot fail loudly.

**Do not** add a DI container or reflection-based auto-wiring. Explicit construction in the
bootstrapper is correct at this size; the bug is the statics, not the manual wiring.

---

## Phase 6 — Domain types (half a day, do with Phase 2)

**Problem.** `Core.Logic/GlobalUsings.cs` aliases `Vector2Int = GridCoord`, `Vector2 = WorldVec2`
and — worst — `Vector3 = WorldVec2`, a 3D type that is actually 2D and silently drops z.
`GridCoord` exposes `X/Y` **and** `x/y` (`Primitives.cs:5-9`) purely to impersonate `Vector2Int`.
Every Godot view needs `using Mathf = Godot.Mathf; using Color = Godot.Color;` to disambiguate, and
the custom `Debug` writes to `Console` instead of Godot's logger, so domain errors bypass Godot's
output panel and callstacks.

**Work**

1. Delete the `Vector3` and `Vector2` aliases; fix the handful of compile errors (mostly combat
   stubs). This alias is a silent-wrong-answer generator.
2. Rename `GridCoord` → `GridPos`; drop the `x/y` alias properties; update call sites (~200, all
   mechanical).
3. Replace the `Mathf` shim with `Math`/`MathF` at call sites.
4. Replace the `Debug` shim with a small `ILog` seam injected once, whose Godot implementation calls
   `GD.Print/GD.PushError`, and whose test implementation collects messages for assertions.
5. Confirm the save DTO graph contains only POCOs and the grid struct — no engine-shaped types.

**Acceptance**

- `grep -rn "Vector3" Core.Logic` == 0.
- `Core.Logic` has no global aliases except `GridPos`.
- A domain error appears in Godot's output panel with its source location.

---

## Sequencing and why

| Phase | Work | Effort | Depends on |
|---|---|---|---|
| 0 | Hygiene, docs, harness relocation | 0.5 d | — |
| 1 | Honest tests, net8.0, `--verify` | 1–2 d | 0 |
| 2 | Map authority + domain types | 3 d | 1 |
| 3 | State provider, saves, input, determinism | 2–3 d | 2 |
| 4 | UI scaffold, tab split, scenes | 1–2 wk | 1, 3 |
| 5 | Instance bus (per-view, alongside 4) | +1–2 d | 4 |
| 6 | (folded into 2) | — | — |

Rationale:

- **Correctness before architecture.** Phases 2 and 3 fix real data loss (map desync, save
  corruption). Do not start with the event bus: it touches every file and would collide with
  everything else.
- **UI last** because it is the longest phase and benefits from a stable domain; and because the bus
  migration is cheapest *inside* the view rewrites rather than as a separate sweep.
- **Phase 1 before all of it** because until the suite can fail, every later phase is unverifiable.

## Explicit non-goals

Not in this plan, deliberately:

- A rewrite, an engine change, or a move back to Unity.
- A DI container, an ECS, an MVVM framework, or a message-broker library.
- A save-migration framework for versions that do not exist.
- Networking, modding seams, or an editor toolchain.
- Chasing the `scene-references` Unity copy for feature parity. It is deleted; the Godot project is
  now the only implementation of this game.
