# Friend build: shutdown crash and physical keyboard check

The retained Windows player evidence establishes a native shutdown crash, but
does not establish a faulty project teardown. The current source audit below
orders the explicit cleanup calls and identifies three hypotheses with small
diagnostic changes. None is a confirmed cause or an implemented fix.
The physical keyboard check below separates input delivery from an unavailable
interaction or an unselected UI button.

## Evidence and limits

- Tested build: `069f55084a72cb959f87d202c87da524e535f5a1`, release Win64 Mono.
- Audited main: `00b030cf8d286bc9d3b1464acd55c025e5a26a32`.
- Engine: `6000.5.0f1 (88b47c5e7076)`; URP `17.5.0`; Input System `1.19.0`.
- Graphics in the retained log: Direct3D 12, split graphics jobs, GeForce RTX 4060,
  driver `32.0.15.9649`.
- Window close returned `-1073741819` (`0xc0000005`). The retained Windows
  Application event 1000 summary identifies `UnityPlayer.dll`, version
  `6000.5.0.46204`, fault offset `0x109401`.
- Sources: the retained player log `player-20261112T043640Z.log`,
  `shutdown-fault.txt`, `player-findings.txt`, and the previous build report.
  These private artifacts are not included in this change.

There are **no managed stack frames**, symbolic native frames, dump text, crash
directory, or dump in the retained evidence. The log itself does not record
the access violation. The Windows event/exit summary supplies that observation;
the faulting module alone cannot identify who caused it.

The last gameplay diagnostics, log lines 54–55, report that the step ashore was
offered and mooring scope was eased. The remaining lines are:

```text
56 [SaveStore] Saved to [destination omitted]
57 [Physics::Module] Cleanup current backend.
58 [Physics::Module] Id: 0xf2b8ea05
59 Input System module state changed to: ShutdownInProgress.
60 Input Polling Thread exited.
61 Input System module state changed to: Shutdown.
62 CodeReloadManager destroyed
63 GarbageCollector disposing of ComputeBuffer. Please use ComputeBuffer.Release() or .Dispose() to manually release the buffer.
```

These are log messages, not a call stack or proof of callback order. In particular,
the buffer finalizer warning does not name an allocation, owner, or failing native
instruction. No managed error or exception is recorded. The old save diagnostic
was removed on main; its corresponding write remains in `SaveStore.Write`.

## Historical hypotheses on the earlier audited main

Every candidate below is a hypothesis. Ranks are investigation priority, not probabilities. Project paths and line
numbers refer to the audited main above. Package paths refer to the installed
source for the versions pinned by `Packages/manifest.json` and
`Packages/packages-lock.json`; package cache folder names are deliberately omitted.

| Rank | Hypothesis and source mapping | Evidence for and against |
| --- | --- | --- |
| 1 | Project deck-shadow buffers: `Assets/_Project/Code/Art/HullDeckShadowMesh.cs:46` and `:47`; dispose at `:80–83`. Owner: `Assets/_Project/Code/Art/HullDeckShadowCaster.cs:34–35`, `:68–83`, `:91–99`. | These are the only explicit project `ComputeBuffer` allocations. Both buffers already receive `Dispose` from `Release`, and both disable and destroy call `Release`. The owner clears its geometry reference after disposal. Constructor failure after an allocation, or an exception before disposal, could lose ownership, but neither is shown in this log. These files are unchanged since the tested build. There is no demonstrated missing guard or reproducible failure to fix. |
| 2 | Engine/graphics shutdown: `UnityPlayer.dll + 0x109401`; pinned engine in `ProjectSettings/ProjectVersion.txt:1–2`. | The access violation is native and the finalizer diagnostic follows engine/input cleanup messages. D3D12 was active. Matching native symbols and a dump are absent, so no engine source file, function, line, or driver fault can be inferred. |
| 3 | Package pipeline buffers: URP `Runtime/ShaderData.cs:9–13`, `:30–35`, `:58–78`; `Runtime/UniversalRenderPipeline.cs:443–469`. | URP also allocates `ComputeBuffer`s and explicitly disposes them during pipeline teardown. Its buffers are another possible source of the anonymous finalizer warning. The retained evidence does not identify a package allocation or skipped disposal. Other package buffer owners are not excluded. |
| 4 | Other project rendering resources: `Assets/_Project/Code/Art/IsoFacetFigureTarget.cs:436–445`, `:497–513`. | The log reports a missing or unsupported figure-target shader earlier in play. This target releases render targets/materials and constructs temporary native arrays for renderer lists. Those are not the explicit project ComputeBuffers above. No teardown stack connects this warning to the crash; changing rendering would require separate proof and ownership. |
| 5 | Save, audio, asynchronous file reads: `Assets/_Project/Code/Core/Save/SaveService.cs:89–98`, `:124`, `:141–163`; `Assets/_Project/Code/Core/Save/SaveStore.cs:100–119`; `Assets/_Project/Code/Audio/AudioDirector.cs:189–195`; `Assets/_Project/Code/Audio/ProceduralAudio.cs:440`; `Assets/_Project/Code/Art/RoadFloorStreamer.cs:131–136`, `:260–279`, `:317–342`, `:345–350`. | Quit saves synchronously, and the old log reports the final write completed. SaveStore uses synchronous file helpers rather than a retained file handle. Audio tears down subscriptions/services and owns Unity audio resources. Road-floor tasks decode managed byte arrays and enqueue results; close advances the generation so late results are ignored. No stack implicates these paths. The input polling thread's recorded exit belongs to engine/package shutdown, not a project-created input thread. |

## Current audit: world close versus title close

The source and line references in this section and the three hypotheses below
are from `98f9f3c55a1bfb3dda2e2c52a035138b0d9fab7a`. The earlier table and keyboard
appendix retain their earlier baseline; their line numbers are not current maps.
The engine remains `6000.5.0f1`; the manifest still pins URP `17.5.0` and Input
System `1.19.0`. Package source is absent from this checkout, so the historical
package-disposal claims above were not independently rechecked here.

The later reproduction report is **3 access violations in 7 closes from inside
the world, with none reported from the title**. Its per-attempt build identities,
title-attempt count and distinction between a fresh title and a returned-to title
were not supplied. That is reported reproduction evidence, not seven runs made
for this audit. A separate retained summary, `shutdown-fault-c60.json`, records
`D3D12Core.dll + 0xa1f5`, exception `c0000005`, on 2026-11-13. It is a different
fault record from the `UnityPlayer.dll` event above, not an additional counted
member of the seven or a symbolic diagnosis.

**The title is not an empty scene.** `Core/Shell/ShellFlow.cs:72–79` pauses the
clock and publishes the title phase over the already loaded world. Entering play
restores services and publishes `Playing` at `:115–132`; that wakes the dormant
arrival through `App/ArrivalOpening.cs:638–641`. The sea opening can then request
an additive load at `App/ArrivalOpening.Sea.cs:31–44`, and movement can start
neighbour preloads through `App/RegionTravelCoordinator.cs:703–729`. World play
therefore changes scene, rig, camera and outstanding-operation lifetimes. It does
not simply switch all graphics or audio from absent to present. Prefix the short
code paths here with `Assets/_Project/Code/`.

### Teardown order actually expressed in code

There is **no project-wide ordered shutdown coordinator**. The explicit call
order below is knowable; the order in which different objects receive engine
quit/disable/destroy callbacks is not established by this source audit. An
execution-order attribute or the order of old log lines is not a recorded
cross-object destruction trace. Persistent objects surviving region changes
still have their own cleanup callbacks at process shutdown.

| Stage | Calls and effects, in the order this source expresses them |
| --- | --- |
| 1. In-world menu exit | `UI/Shell/PauseMenu.cs:218–223` calls the current save's `Save()`, then `ShellQuit.QuitApplication()`. It does not first unload the world, close the menu or call `QuitToTitle`. `UI/Shell/ShellQuit.cs:18–24` calls `Application.Quit()` in a player; the editor branch only stops Play mode. |
| 1a. Title or window exit | `UI/Shell/TitleScreen.cs:279` calls the same `ShellQuit` without the menu's preliminary write. Window close bypasses that menu method. Both must be distinguished from the in-world menu path in a reproduction record. |
| 2. Quit callbacks | `Core/Save/SaveService.cs:129` calls `Save()` again. `:148–162` first defers an open write group, then refuses a title or mid-sleep write; otherwise it snapshots live state, preserves an unreadable file if necessary, and writes. An ordinary in-world menu exit can therefore request **two sequential saves**, despite the narrower comment in `ShellQuit`. `App/RegionTravelCoordinator.cs:107` sets its own `_quitting`; that prevents its later destroy callback from unloading warm neighbours. These are the two project `OnApplicationQuit` implementations found; their relative callback order is not asserted. |
| 3. Disable callbacks | Owners unregister listeners and release or withdraw resources independently. Important ordered chains are expanded below. No common quit flag gates every render callback, scene-load completion or gameplay event. |
| 4. Destroy callbacks | `App/GameRoot.cs:108–115` conditionally clears only the clock, environment, wallet and config it published, then resets the shell. `Core/Shell/ShellFlow.cs:160–165` resets phase, pause and sleep without publishing a transition. `SaveService.cs:93–102` unsubscribes boat events, uninstalls rest/wake/cleanliness responders, then clears its service and instance references. `RegionTravelCoordinator.cs:94–107` clears player/whereabouts registrations and unregisters the hold, skipping warm-neighbour unload when quitting. Their order relative to one another is not fixed here. |
| 5. Render-feature disposal | `Art/IsoFacetHullFeature.cs:338–354` lowers feature globals, lowers the breaker passes, releases the calm mirror, disposes its pass, then destroys its three materials. Pass disposal at `:1590–1614` disposes figure targets, mesh-camera states and the clear-reflection handle, switches reflection to idle, releases resolve/caustic/water/reflection targets, releases underwater targets and binds underwater idle, then releases foam states. This is an internal sequence, not proof that it runs before or after a particular scene object's destruction or the last native graphics job. |
| 6. Native shutdown | The retained log ends with physics cleanup, input shutdown and polling-thread exit, code-reload cleanup, and an anonymous ComputeBuffer finalizer warning. The access violation is reported separately by Windows. Neither source nor those lines locate the faulting native instruction or prove all preceding resource releases completed safely. |

Returning to the title is a different lifecycle. `ShellFlow.cs:145–153` saves,
abandons sleep, resumes the pause and publishes `ReturnToTitleRequested`.
`App/ShellRestart.cs:87–96` destroys the tagged persistent core, yields one frame,
then loads the boot scene in Single mode. Its loop at `:102–114` requests each
root's destruction; it does not define a dependency order among those roots.
Save and audio services survive this restart by design. Thus "quit at a fresh
title" and "play, return to title, then quit" are separate controls.

### Resource owners and callbacks checked

- **Deck buffers:** `Art/HullDeckShadowCaster.cs:34–35` calls `Release` on both
  disable and destroy. At `:91–99` it disables the draw renderer, schedules its
  GameObject for destruction, decrements shared-material users, disposes geometry
  and nulls the geometry/draw/renderer references. `HullDeckShadowMesh.cs:80–83`
  disposes triangle then node buffers and destroys the receiver mesh. Ordinary
  repeated owner cleanup therefore sees a null geometry; two lifecycle hooks
  alone are not evidence of a double dispose. Buffer bindings are made at
  `HullDeckShadowCaster.cs:83`, copied onto the renderer at `:66`, and not explicitly
  cleared by `Release`. Its `_block` also remains referenced.
- **Other render ownership:** `IsoFacetHullRenderer.cs:935–947` unregisters the
  hull and releases id/occupant slots; its destroy path at `:998`, `:1393–1418`
  destroys owned drawing objects, materials and ramp textures. Figure target
  disposal releases cached RTHandles before materials and `Unpublish`
  (`IsoFacetFigureTarget.cs:436–445`). That last method only zeroes the pixel-map
  vector (`:126–130`); it does not clear the global screen texture. Silhouette
  disposal similarly releases and clears its target dictionary
  (`SilhouetteMaskFeature.cs:208–212`). Foam releases its transport first, then
  both history handles and nulls them (`IsoFacetHullFeature.cs:520–536`).
  `FoamTransportField.cs:493–500` wraps Texture2Ds in RTHandles, releases the
  wrappers and destroys the textures; without the pinned package implementation,
  those two layers must not be labelled a demonstrated double-free.
- **Static subscriptions:** `DayNightController.cs:128–141` and
  `SquallVeil.cs:84–101` remove their camera callbacks before further cleanup.
  `WaterSurface.cs:597–618` removes `TidalTerrainChanged`, clears the renderer's
  property block, withdraws globals, then destroys its baked textures. Its terrain
  callback explicitly ignores a null terrain (`:1695–1705`). Character creation
  removes its render callback before restoring the world and freeing its camera;
  it detaches portrait/camera targets and clears the active render target before
  release (`UI/CharacterCreatorPanel.cs:166–192`, `:495–505`). These are existing
  safeguards, not proof of a stale subscriber in those paths.
- **Events can still do work during teardown:** arrival disable withdraws its
  offers, stops its clip and, unless transferring/waiting, closes character
  creation, releases the player and releases the held hour
  (`App/ArrivalOpening.cs:600–630`). If `_holding` remains true, player release
  enables walking, restores Rigidbody2D simulation and publishes
  `CarriedAboardChanged(false)` followed by `ControlModeChanged(OnFoot)`
  (`:1949–1966`). `Core/Events/EventBus.cs:60–68` invokes its current snapshot
  synchronously and logs managed exceptions; it has no global quitting gate.
  Unsubscribing does not retract an already captured dispatch snapshot.
  `Audio/AudioDirector.cs:352–371` can turn that mode change into an ashore cue,
  subject to the trip-strength and null-source/clip checks at `:405–419`.
- **Audio and file handles:** audio removes its subscriptions on disable and again
  on destroy before clearing its service (`AudioDirector.cs:195–225`); its child
  AudioSources and generated clips are engine objects. `BuildSources` starts
  ambient sources already at boot (`:425–440`), so their mere existence does not
  explain the title contrast. `ProceduralAudio.cs:440–441` creates non-streaming
  clips from completed sample arrays; no project audio-thread callback was found.
  `SaveStore.cs:100–119` uses synchronous write/replace/move calls, with no retained
  file handle. A pre-quit write exception would interrupt the menu path before
  `Application.Quit`, rather than explain an access violation after native cleanup.
- **Background work:** `Art/RoadFloorStreamer.cs:131–136`, `:260–279` increments
  the session, detaches road textures, destroys them and clears its queue on
  disable. Outstanding tasks decode bytes and enqueue managed results
  (`:317–342`); the main-thread drain ignores an old session (`:345–350`). It does
  not join those tasks, but they do not upload into destroyed textures themselves.
  Scene-operation completion callbacks below have a different risk: they call
  native scene APIs and toggle roots. No project P/Invoke, SafeHandle, GCHandle or
  native audio callback was found in the runtime-source search; the two explicit
  project ComputeBuffer allocations remain `HullDeckShadowMesh.cs:46–47`.

### Three hypotheses, in investigation order

**1. Hypothesis: deck-buffer bindings survive their resource release.**
`Art/HullDeckShadowCaster.cs:66`, `:83`, `:91–101` and
`HullDeckShadowMesh.cs:46–47`, `:80–83` are the narrowest project-owned route from
native buffers to the finalizer diagnostic. Draw destruction is deferred in play
while the buffers are disposed immediately; neither the renderer's copied
property block nor the retained block is explicitly detached first. World play
configures hull presentations through `IsoFacetHullPresentationService.cs:54–74`
and can replace a deck geometry. A title has a rendered world too, so this fits
the contrast only if the failing world run has a different live/rebuilt caster
set or pending draw work. Count that difference; do not assume it.

Smallest behavioral probe: in `HullDeckShadowCaster.Release`, after disabling the
renderer and before the existing destroy/dispose calls, clear its property block
and detach the receiver from its MeshFilter, then clear the retained block. Keep
all ordinary rendering and disposal otherwise identical. Record each buffer
allocation and disposal, including construction failure: the second allocation
or `SetData` can throw before `_geometry` receives the completed constructor,
which currently has no partial-construction cleanup. An unpaired allocation
would redirect the fix toward exception-safe construction. Balanced ownership
with a rate change only after detaching bindings would favor the binding-lifetime
hypothesis. The engine may already retire native draws safely; a managed block
reference is not proof of a dangling GPU pointer, and the warning could be from
package-owned buffers.

**2. Hypothesis: published render targets outlive their native lifetime during
pipeline shutdown.** `Art/IsoFacetFigureTarget.cs:404–445` exports a persistent
target as a global texture, then releases it during disposal without clearing
that texture binding. The broader target cleanup is in
`IsoFacetHullFeature.cs:1590–1614`; the figure exporter is conditional on live
figures and camera settings at `IsoFacetFigureTarget.cs:249–264`. World entry,
character creation and travel can leave different camera caches and pending
passes from an untouched title. That condition must be observed: the title also
renders, and a missing/unsupported figure shader prevents this particular export
path. A late native graphics fault and the separate D3D12 fault summary fit a
graphics-lifetime hypothesis, but neither identifies this target as the cause.

Smallest behavioral probe: immediately before releasing the figure targets,
unpublish the map and clear `ScreenTexId` with a null texture binding that allocates
nothing; retain the existing release loop. Log target identities, last export,
dispose entry and active camera so an unused target path cannot be called a
successful isolation. A subsequent, separate probe can apply that ordering to
other published targets if evidence points there. Do not call a lazy fallback
allocator during shutdown: `ReflectionRegistry.cs:72–75`, `:120–131` demonstrates
that an apparently harmless idle binding can allocate a new Texture2D. If the
failure persists with all implicated bindings detached and no late recording,
obtain the native stack before treating an engine/driver teardown fault as a
project ownership bug. The smallest independent backend control is the same
player and route launched with D3D11 instead of D3D12; it is a diagnostic only,
not an accepted rendering or project-setting change.

**3. Hypothesis: a pending scene-load completion restarts object lifecycles while
the application is quitting.** `World/RegionSceneLoader.cs:119–124` attaches a
completion delegate that may set the active scene; `:196–213` attaches the
preload callback, which may also set the active scene or park the result.
`ParkReleased` at `:222–231` disables the loaded scene's roots and cycles active
roots off and on. These callbacks have no destroyed-owner or quit guard. The
coordinator's `_quitting` at `App/RegionTravelCoordinator.cs:94–107` protects only
its own neighbour-unload call; it does not suppress these delegates. Re-enabling
roots can republish services and rebuild render resources during teardown, with
the synchronous arrival/audio event chains above adding work. Entering the world
starts the sea journey and moving toward an edge starts preloading; a fresh title
does neither through that gameplay path. This explains the contrast only for a
run with a pending operation. A settled world with no pending load weakens it.

Smallest behavioral probe: add a session-only quitting flag to the loader, set it
on application quit and destroy, and have **both** completion paths return before
scene APIs or root toggles when quitting or when the loader is destroyed. Record
load request/completion, quit entry and each skipped callback. Do not attempt to
cancel or synchronously wait for the native scene operation, and retain ordinary
travel behavior. A callback observed after quit, followed by suppression and a
lower failure rate, would support this hypothesis. If every failing run has zero
pending callbacks, move it down the list. The code exposes a possible overlap;
it does not prove that the engine delivers that overlap in the failing player.

### How the hypotheses would be separated

These are proposed diagnostics only. Make one behavioral change at a time and
keep the build, save fixture, API, elapsed route and exit method recorded. Compare
fresh-title exit, in-world menu exit, in-world window close and play-to-title-to-exit
separately. Within world exits, separate an in-flight load/arrival from a settled
world after handover; note whether character creation was used. Repeat a fixed
count per condition rather than stopping at the first clean close. A small series
with no failures is not a cure for a fault previously seen only 3 times in 7.

Minimal tracing should number quit, disable, destroy, load-complete and feature
dispose boundaries and pair resource allocation/release identities; retain the
exit status, last relevant log lines and fault module/offset. Do not force a
garbage collection or change several release paths in the same experiment. A
matching-symbol native dump remains the decisive way to distinguish a dangling
resource, engine finalizer or driver fault. Existing `HullDeckShadowTests` includes
`Disable_ReleasesOwnedResources` (`Assets/Tests/EditMode/Art/HullDeckShadowTests.cs:102`)
and explicitly cleans fixture casters at `:111–120`; that is useful ownership
coverage, not a native player-shutdown reproduction. `IsoFacetFigureTargetTests`
and the shell restart tests likewise do not certify process exit.

This extension changed only this document. No editor or player was started, no
code or visual asset changed, no save was written, and no runtime tests or
diagnostic probes above were executed. It narrows the next experiment without
claiming the close crash has been solved.

## Decision and next player evidence

Use the evidence report fallback. There is no small teardown change for which the
retained evidence supplies a failing EditMode case. An EditMode disposal test
alone would not reproduce this native player shutdown. No code, shader, material,
scene, baseline, package, or engine-version change is made.

One later, explicitly authorized player run can settle whether physical keys
arrive and whether ordinary menu Quit on a fresh build reproduces the failure:

1. Keep the tested build identifier and run the five checks below at the relevant
   screens. An unavailable screen is recorded as **NOT REACHED**. Travel to the
   sale screen is not part of the one-minute input probe.
2. Quit through the pause menu once. Retain the exit code, filtered final log
   lines, Windows fault event, and any crash report/dump produced. Record this quit
   route: the previous failure followed window close.
3. If it faults, resolve the native stack with symbols matching the reported
   engine/player binary. A stack in buffer release/finalization, pipeline disposal,
   audio, or another native subsystem would direct the next investigation. Without
   a useful dump or allocation/lifetime evidence, buffer ownership remains open.

A clean exit would establish only that run's outcome. It would not prove that the
old build is repaired, exclude an intermittent fault, or establish a rendering
change as a cure. No player run, editor run, build, or save write was performed for
this audit.

## Five physical keyboard checks

Spend at most one minute probing keys at the relevant available screens. Record
**NOT REACHED** for a screen or offer that is unavailable; reaching the market
from the opening is a separate journey. The cabin door is already open in the
recorded opening, so a failed attempt to open it again is not a delivery test.

1. Click inside the player window, enter the world, then hold **W/A/S/D or the
   arrow keys** briefly: the character should move when no modal owns movement.
2. At the aft cabin doorway, press **E** to work the door if closed; walk through
   the open doorway with **W/A/S/D or arrows** to reach the deck.
3. Once tied up, above deck and showing **Step ashore**, press **E**: the boarding
   move should put the character ashore. A missing offer is **NOT REACHED**.
4. At a seller with a sellable bucket carried there by hand, use **E** to advance
   the conversation, **W/S or up/down** to choose the sale row, then **E** to
   confirm: the reply and carried contents should reflect the sale. If the
   separate **Sell at the Wharf** screen is open, click a species row, navigate
   to **Confirm sale** with arrows, and use main **Enter** instead.
5. With dialogue and other modal pages closed, press **Escape**: the pause menu
   should open; press it again to resume. A page that owns Escape may close first.

The spoken counter sale is **E**, not Enter. Source mappings on audited main:

| Action | Shipped reader and conditions |
| --- | --- |
| Move aboard/in cabin | `Assets/_Project/Data/Input/HiddenHarbours.inputactions`, Deck/Move: W/A/S/D and arrows; `Assets/_Project/Code/Player/Input/DeviceDeckIntentSource.cs:26–49`; `Assets/_Project/Code/App/ArrivalOpening.cs:1328`. |
| Cabin door | Raw E in `Assets/_Project/Code/Player/ControlSwitcher.cs:3149–3150` dispatches the interaction. `Assets/_Project/Code/App/ArrivalOpening.cs:2238–2309` forwards the arrival offer to `BoatCabinDoor.TryUse`; `Assets/_Project/Code/Boats/BoatCabinDoor.cs:278`, `:353–376`, `:462–500` gates/toggles the door. Walk through the open measured threshold; E is not an unconditional exit shortcut. |
| Step ashore | Same E dispatch; `Assets/_Project/Code/App/ArrivalOpening.cs:1623–1640`, `:2396–2436` requires moored, the arrival beat elapsed, a player above deck and no step already in progress. Ordinary own-boat disembark also checks reach/facing in `Assets/_Project/Code/Player/ControlSwitcher.cs:715–764`. |
| Spoken sale | `Assets/_Project/Code/World/WorldInteractor.cs:117–133`, `:435–441` reads E and W/S/up/down. `Assets/_Project/Code/World/DialoguePresenter.cs:471–477`, `:890–898`, `:936–941`, `:987–1012` confirms the row and requests the counter sale. `Assets/_Project/Code/Economy/CounterSellDesk.cs:79–80` calls `SellOverTheCounter`; `Assets/_Project/Code/Economy/WharfSellPoint.cs:142–146` reads carried contents where the counter is configured for that. Empty or refused goods are not a keyboard failure. |
| Separate sale screen | `Assets/_Project/Code/Economy/SellScreen.cs:233–239`, `:271–279`, `:345–346`. There is no dedicated global sale key. Submit acts on the currently selected UI control. The screen selects species data but does not explicitly select an EventSystem button on opening, so a bare Enter can do nothing. |
| Pause menu | `Assets/_Project/Code/UI/Shell/ShellPresenter.cs:83–118` reads Escape directly and respects title, modal/cancel ownership, wardrobe, notebook, tide panel and settings gates. |

## Player versus editor input

No player-only keyboard disable or backend mismatch was found in the inspected
paths. This is a source audit, not physical input proof.

- `ProjectSettings/ProjectSettings.asset:784` sets `activeInputHandler: 1` (new
  Input System). Runtime Player, Boats, World and UI assemblies reference
  `Unity.InputSystem` without editor-only platform or define constraints. The
  inspected E, movement, sale and Escape readers are not compiled out in a player.
- The actions GUID in `ProjectSettings/EditorBuildSettings.asset:36` matches
  `HiddenHarbours.inputactions.meta`. `InputBindings.cs:49–79` uses
  `InputSystem.actions` and enables each requested map. Input System's
  `InputSystem/Editor/ProjectWideActions/ProjectWideActionsBuildProvider.cs:16–29`,
  `:56–69` adds that configured asset to player preloaded assets during a build.
  An empty serialized `preloadedAssets` list before a build is not evidence that
  the actions are missing from the player.
- ADR 0043 describes the intent rollout. On this main, cabin movement is seamed,
  while the verb, spoken choices and pause readers above still poll the keyboard.
  A declared Deck/Interact binding is not proof that every verb consumer has
  migrated. Missing actions would impair movement and log an InputBindings error;
  they would not by themselves explain raw E and raw Escape both failing. The
  retained log contains no such InputBindings diagnostic.
- The release setting is `runInBackground: 0`
  (`ProjectSettings/ProjectSettings.asset:90`). Focus is therefore a required
  condition for this player probe. Editor Play mode keeps its player loop running
  independently of Game View focus and applies editor input-focus rules; this can
  differ from a release window. The installed package documents the distinction
  in `InputSystem/InputSettings.cs:474–481`, `:899–912`. No project override of
  background/focus input settings was found. Prior automated key attempts,
  including after a click, do not establish physical delivery or exclude focus.
- The UI modules created by `PaperUi.cs:383–391` and `SellScreen.cs:271–279` assign
  the package's default UI actions. In Input System 1.19.0,
  `InputSystem/Plugins/UI/InputSystemUIInputModule.cs:1541–1557` explicitly uses
  `DefaultInputActions`, whose Submit binding is `*/{Submit}`. The keyboard layout
  gives main Enter that usage (`InputSystem/Devices/Keyboard.cs:55`); numpad Enter
  at `:132` and Space at `:54` do not carry it. Use main Enter for this probe and
  ensure the intended button owns selection. This differs from the spoken sale's
  E path in both editor and player.
- Quit itself differs: `Assets/_Project/Code/UI/Shell/ShellQuit.cs:18–24` stops
  editor Play mode under `UNITY_EDITOR` and calls `Application.Quit()` in a player.
  An editor exit is not proof of native player shutdown.
