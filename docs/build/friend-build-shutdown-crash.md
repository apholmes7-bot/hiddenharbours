# Friend build: shutdown crash and physical keyboard check

The retained Windows player evidence establishes a native shutdown crash, but
does not establish a faulty project teardown. No runtime fix is justified yet.
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
directory, or dump in the retained lane evidence. The log itself does not record
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

## Ranked candidates on audited main

Ranks are investigation priority, not probabilities. Project paths and line
numbers refer to the audited main above. Package paths refer to the installed
source for the versions pinned by `Packages/manifest.json` and
`Packages/packages-lock.json`; package cache folder names are deliberately omitted.

| Rank | Candidate and source mapping | Evidence for and against |
| --- | --- | --- |
| 1 | Project deck-shadow buffers: `Assets/_Project/Code/Art/HullDeckShadowMesh.cs:46` and `:47`; dispose at `:80–83`. Owner: `Assets/_Project/Code/Art/HullDeckShadowCaster.cs:34–35`, `:68–83`, `:91–99`. | These are the only explicit project `ComputeBuffer` allocations. Both buffers already receive `Dispose` from `Release`, and both disable and destroy call `Release`. The owner clears its geometry reference after disposal. Constructor failure after an allocation, or an exception before disposal, could lose ownership, but neither is shown in this log. These files are unchanged since the tested build. There is no demonstrated missing guard or reproducible failure to fix. |
| 2 | Engine/graphics shutdown: `UnityPlayer.dll + 0x109401`; pinned engine in `ProjectSettings/ProjectVersion.txt:1–2`. | The access violation is native and the finalizer diagnostic follows engine/input cleanup messages. D3D12 was active. Matching native symbols and a dump are absent, so no engine source file, function, line, or driver fault can be inferred. |
| 3 | Package pipeline buffers: URP `Runtime/ShaderData.cs:9–13`, `:30–35`, `:58–78`; `Runtime/UniversalRenderPipeline.cs:443–469`. | URP also allocates `ComputeBuffer`s and explicitly disposes them during pipeline teardown. Its buffers are another possible source of the anonymous finalizer warning. The retained evidence does not identify a package allocation or skipped disposal. Other package buffer owners are not excluded. |
| 4 | Other project rendering resources: `Assets/_Project/Code/Art/IsoFacetFigureTarget.cs:436–445`, `:497–513`. | The log reports a missing or unsupported figure-target shader earlier in play. This target releases render targets/materials and constructs temporary native arrays for renderer lists. Those are not the explicit project ComputeBuffers above. No teardown stack connects this warning to the crash; changing rendering would require separate proof and ownership. |
| 5 | Save, audio, asynchronous file reads: `Assets/_Project/Code/Core/Save/SaveService.cs:89–98`, `:124`, `:141–163`; `Assets/_Project/Code/Core/Save/SaveStore.cs:100–119`; `Assets/_Project/Code/Audio/AudioDirector.cs:189–195`; `Assets/_Project/Code/Audio/ProceduralAudio.cs:440`; `Assets/_Project/Code/Art/RoadFloorStreamer.cs:131–136`, `:260–279`, `:317–342`, `:345–350`. | Quit saves synchronously, and the old log reports the final write completed. SaveStore uses synchronous file helpers rather than a retained file handle. Audio tears down subscriptions/services and owns Unity audio resources. Road-floor tasks decode managed byte arrays and enqueue results; close advances the generation so late results are ignored. No stack implicates these paths. The input polling thread's recorded exit belongs to engine/package shutdown, not a project-created input thread. |

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
4. At a seller with a sellable pail carried there by hand, use **E** to advance
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
