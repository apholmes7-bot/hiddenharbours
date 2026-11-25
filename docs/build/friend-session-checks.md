# Friend session checks — 13 November 2026

Use with the [friend session guide](friend-session-guide.md). Record observations
from **two physical PCs**. These answers are the input to the next co-op pass;
no boxes have been pre-filled from earlier automated checks.

**Zip filename:** ____________________  **Source/build version:** ____________________

**Package:** main / old `11b09e2637` trial  **Network:** same home / different homes

**Host:** first PC / second PC / no host on main

Reviewed main `00b030cf8d` has no co-op. For that build, mark checks **3 and 4
No — unavailable on this build** and continue with the local checks. Do not
treat that as a connection failure or change firewall rules to remedy it.
A later main revision needs its own capability check. The old trial's code
claims below do not imply that code has been merged.

| # | One yes/no result to record | Code claim being checked |
|---|---|---|
| 1 | **Do both PCs reach the expected starting screen?** Main: title with New game/Settings/Quit (Continue only with a save). Old trial: each reaches its dory in East Water using the supplied Host/Join launchers. **Yes / No:** ____ | Main [TitleScreen.BuildMenu](../../Assets/_Project/Code/UI/Shell/TitleScreen.cs#L146); old trial `CoopDevStart.Start` and `CoopSpikeSettings.Scene`. |
| 2 | **Can both people move using a physical keyboard?** Focus the game window and finish any opening prompts first. Main: walk with W/A/S/D once walking is available. Trial: row/turn with W/S/A/D at the helm. **Yes / No:** ____ | [Input bindings](../../Assets/_Project/Data/Input/HiddenHarbours.inputactions) and [PlayerWalkController](../../Assets/_Project/Code/Player/PlayerWalkController.cs#L466). The exe lane did not confirm this through automation. |
| 3 | **In the old trial, does each screen show the other person's boat moving smoothly when they row and turn?** Take turns; stay near the starting area. **Yes / No:** ____ | Trial `CoopBoatReplica` publishes the owner's boat; the network transform and `CoopReplicaView` display the remote hull. A second physical PC has not previously been proved. |
| 4 | **In the old trial, does the visible clock agree on both screens and follow the host's brief pause/resume?** Use Esc then Resume on the host. **Yes / No:** ____ | Trial `CoopBoatReplica.Sea` is server-written; `CoopSeaState.Apply` applies time/rate/pause after checking the seed. Pause behavior remains experimental. If no clock is visible, mark No and write “not observable,” not “desynchronized.” |
| 5 | **Do both games close through the menu without Windows reporting a crash?** Friend first, host second; use Esc → Quit to desktop, or Quit at the title. **Yes / No:** ____ | [PauseMenu.OnQuitToDesktop](../../Assets/_Project/Code/UI/Shell/PauseMenu.cs#L218) calls [ShellQuit.QuitApplication](../../Assets/_Project/Code/UI/Shell/ShellQuit.cs#L18). A shutdown access violation remains an open exe finding. |

Historical trial classes above are from `11b09e2637`, under
`Assets/_Project/Code/App/CoopSpike/`; they are absent from reviewed main.
Main uses local disk saves. The old trial replaces the save service with an
in-memory session and discards progress on quit.

**Notes for each No:** check number, affected PC, last action, rough time and
what appeared. Distinguish unavailable, not reached, not observable and failed.
For check 5, record quit order and any Windows error text. For check 2, record
whether menu keys worked even if movement did not.

____________________________________________________________________________

____________________________________________________________________________

Keep both logs before relaunching. Return these completed checks with the
three items in the guide: **build identity, both logs, and the short failure
record/screenshots**. No shared catch, trade, region travel or save acceptance
is implied by five Yes answers.
