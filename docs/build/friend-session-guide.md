# Friend session guide — 13 November 2026

**A fresh build from the main revision reviewed here cannot host or join a
co-op game.** The two-boat trial is a separate, older build. Do not spend the
evening changing firewall settings to make the main build multiplayer.

This guide reviews main `00b030cf8d286bc9d3b1464acd55c025e5a26a32`. The earlier
trial is `11b09e2637`. No player was built or run for this guide. The fresh
archive is supplied separately after its main build passes CI; this document
does not certify an archive or a two-machine playtest.

## Before you start

1. Both people need a Windows x64 PC, a physical keyboard and mouse, and the
   **same supplied zip**. Unity Editor is not needed. Right-click the zip,
   choose **Extract All**, and open the extracted folder. Keep the executable,
   its Data folder and all DLLs together; do not run inside the zip.
2. Identify the package before launching:

   | Package | How to recognize it | What you can do |
   |---|---|---|
   | Fresh main build | Read `build-info.txt`; the builder's default executable is `Red Skies At Night.exe`. | Play independent single-player games. No Host or Join option. |
   | Older two-boat trial | Archive `HiddenHarbours-Coop-11b09e26.zip`, with `HiddenHarboursCoopSpike.exe`, `Host.cmd`, `Join.cmd` and its README. | Try the limited two-person session below. It does not contain newer main changes. |

3. If the main revision differs from the one above, retain `build-info.txt`:
   this source audit does not establish that later build's co-op support.
   Launchers alone do not add networking to a main executable.
4. For **main**, there is no multiplayer port to open and no requirement to
   share a network. For the **old trial**, use the same trusted home Wi-Fi or
   Ethernet first. Guest Wi-Fi can isolate the computers. If Windows Firewall
   prompts, allow the trial on the trusted Private network. The host needs
   inbound **UDP 7777** for `HiddenHarboursCoopSpike.exe`; the friend needs
   outbound traffic and replies. Keep the firewall enabled. The trial README
   has the exact manual rule if the prompt was missed. The trial uses Netcode
   for GameObjects with Unity Transport over UDP.

Different houses are **not a verified setup**. The old trial has no relay,
matchmaking or automatic router configuration. It needs either a private
network connecting the PCs, or a host router forwarding UDP 7777 to the host
PC's UDP 7777, plus the Windows rule. Direct internet joining uses the host
router's public IPv4 address, not its home-network address. Provider NAT can
prevent that route. The trial README covers these alternatives; neither was
tested. The trial has no participant authentication. Use only the intended
friend connection, and remove any temporary forwarding afterwards.

## Starting the fresh main build

Each person opens the supplied game executable. Choose **New game** on a first
run, or **Continue** for an existing game. New game replaces that product's
existing save after confirmation. Follow the opening prompts. Once walking is
available, click the game window and try **W/A/S/D** on a physical keyboard.

**Host: unavailable. Join: unavailable.** There is no address box, join code or
working co-op launch argument in this main source. You can compare your
separate games, but cannot meet, see the other player or share progress.
Main uses ordinary local saves; the old trial's “nothing is saved” rule does
not apply to it.

## Hosting the older trial only

Use these steps only when both people have the identified `11b09e2637` trial.

1. Double-click **Host.cmd**. Wait until your dory appears in **East Water**.
   Leave the game running.
2. Open Windows Start, type **cmd**, and open Command Prompt. Type `ipconfig`
   and press Enter. Under the active Wi-Fi or Ethernet adapter, read **IPv4
   Address**. Give your friend that address, not Default Gateway or an address
   from a disconnected adapter. For example, `192.168.1.20` is an example only;
   `127.0.0.1` points to the friend's own PC. [Microsoft's ipconfig reference](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/ipconfig).
3. Accept the trial's firewall prompt for the trusted network if it appears.
   Leave the host running while the friend joins. One friend can join.

## Joining the older trial only

1. Wait until the host sees their dory, then double-click **Join.cmd**.
2. At the prompt, type the host's IPv4 address and press Enter: four numbers
   separated by dots, with no web prefix, computer name or `:7777` suffix.
3. When connected, both screens should show two dories. Each person controls
   their own. There is no in-game connection/status menu. If the second boat
   never appears, use the troubleshooting steps below.

For reference, the packaged launchers invoke these arguments. These are
**not supported by the main executable**:

```text
HiddenHarboursCoopSpike.exe --coop-spike-host --coop-spike-port 7777 -force-d3d11
HiddenHarboursCoopSpike.exe --coop-spike-client --coop-spike-address HOST_IPV4 --coop-spike-port 7777 -force-d3d11
```

Use the launchers in normal play: they also put each log beside the game.

## Ten minutes in the older trial

**There is no known working ten-minute co-op route on reviewed main.** The
following is a small trial route based on earlier two-process checks on one
physical PC. A second physical PC, physical keyboard input, connection quality
and the complete route still need the [session checks](friend-session-checks.md).

| Time | Where to go and what to try |
|---|---|
| 0–2 minutes | Start both launchers and stay at the two dories' starting area in East Water. Confirm the second boat appears on both screens. |
| 2–4 minutes | Take turns moving a short distance while the other watches. At the dory helm, **W** rows forward, **S** backs, **A/D** turns, **Space** braces/brakes. Keep the boats near each other. |
| 4–6 minutes | Stop. Press **E** to leave your helm, walk on your own deck with **W/A/S/D**, then press **E** near the helm to take it again. The other screen should show your skipper's position; matching rowing or rod animations are not promised. |
| 6–8 minutes | From your own deck, try a local cast: hold the left mouse button, draw back, sweep forward and release. After a bite, hold to reel and release to ease. A catch is not guaranteed and is not shared. Do not try to pass fish to the other boat. |
| 8–9 minutes | Compare any visible clock readout and the tide/weather. Have the host briefly open **Esc**, then **Resume**: the host publishes time and pause. Pause behavior is experimental; record disagreement. If no clock is visible, record that instead of guessing. |
| 9–10 minutes | Have the friend quit first, then the host: **Esc → Quit to desktop**. Note whether Windows reports a crash. Copy both logs before relaunching. |

Stay in East Water throughout. The trial also has an **F** development hull
cycler, but this route uses the dories from the native proof. A region hop,
town visit or larger boat is not part of the verified shared scope.

## What is shared, and what will not work yet

| Thing | Reviewed main | Older trial only |
|---|---|---|
| Boats | Local only | Position, heading and selected hull shown to the other peer. Each player controls their own boat. |
| Players | Local only | Skipper position/facing and drawn offset; the remote skipper is a simpler baked figure. No custom appearance or matching rod/rowing animation. |
| Catch and goods | Local only | Casts, fights, catches, fish removed by catching, holds, hands and inventories remain local. No shared storage or transfers. |
| Clock | Local only | Host publishes time, speed and pause; the client follows. The seed must match; a different world is not downloaded. |
| Tide, weather, fish schools | Local simulation | Recomputed on each PC from the agreed seed/time. Matching schools do not mean shared catches or depletion. |
| Saving | Local disk save | Memory-only progress, discarded on quit. Logs are still written. |

The trial cannot support boarding your friend's boat, passengers, shared
ownership, money, shops, licences, doors/interiors, region travel or a shared
campaign. The remote boat is a display without storage or gameplay controls.
The **host alone publishes the shared clock and approves the second
participant**; each boat's owner publishes that boat's state. This is not a
host-authoritative fishing or economy system.

If a trial participant disconnects, the runtime logs the disconnect. There is
no recovery screen, host migration or reconnect identity. If the host leaves,
the shared session ends; do not assume the friend's window closes or returns
to a menu. Preserve logs, close both copies, then start host and join again.
The trial does not recover progress. Main has no network session to disconnect.

The trial's held rod can appear detached above the figure. The earlier trial
had a native shutdown access violation whose cause remains unknown. Twelve
ordinary quits in earlier checks did not reproduce it; that does not establish
a fix or long-session stability.

## If it breaks

**Fresh-main exe finding:** automated input did not confirm that the built
player accepts keyboard controls, and an access violation was observed on
shutdown. Click the window and try a **physical keyboard**, including menu
selection and movement once the opening allows it. Quit with **Esc → Quit to
desktop** (or **Quit** at the title), then note whether Windows reports a crash.
These are open checks, not known fixes. If the menu cannot be reached, record
that and the fallback used to close the window.

For the old trial failing to connect, check the package matches on both PCs,
the host is running, the address is its active adapter's IPv4, and UDP 7777 is
allowed on the actual network profile. Avoid guest-network isolation. Keep the
logs before restarting both copies. Opening ports will not repair missing
co-op support in a main build.

**Logs on each machine:**

- Old trial launchers: **host-player.log** on the host and **client-player.log**
  on the friend, in the extracted game folder beside the launchers.
- Main builder defaults: paste
  `%USERPROFILE%\AppData\LocalLow\One More Minute Games\Red Skies At Night`
  into File Explorer's address bar and copy **Player.log**. If `build-info.txt`
  lists a different product, use that product's folder under the same company.
  If a supplied launcher specifies `-logFile`, use its named log instead.
  This combines the builder's product/company with [Unity's Windows player-log location](https://docs.unity3d.com/6000.0/Documentation/Manual/log-files.html).

Copy the logs before another launch can replace them. Send back exactly these
three things through the usual build-feedback route:

1. **The build identity:** zip filename and `build-info.txt` if present; say
   main or old trial and which computer hosted.
2. **Both computers' logs**, labelled host/friend or first/second computer.
3. **A short failure record:** completed checks, the last action and rough
   time, same house or different houses, physical keyboard result, quit order
   and whether Windows showed a crash. Include a screenshot of any error.

## Source record for the next co-op pass

These references describe source, not a fresh build test:

- [TitleScreen.BuildMenu](../../Assets/_Project/Code/UI/Shell/TitleScreen.cs#L146)
  constructs Continue, New game, Settings and Quit; no Host/Join.
  [GameRoot.Start](../../Assets/_Project/Code/App/GameRoot.cs#L78) enters the
  local title/play flow. No `CoopSpike` runtime, resources or tests exist on
  the reviewed main tree, and [manifest.json](../../Packages/manifest.json)
  has neither Netcode for GameObjects nor Unity Transport.
- [GameRoot](../../Assets/_Project/Code/App/GameRoot.cs#L61) assigns
  `GameServices.Clock = _clock`; [EnvironmentService.Sample](../../Assets/_Project/Code/Environment/EnvironmentService.cs#L80)
  uses the local clock/seed. Determinism is not network synchronization.
- [PauseMenu.OnQuitToDesktop](../../Assets/_Project/Code/UI/Shell/PauseMenu.cs#L218)
  calls `save.Save()` and [ShellQuit](../../Assets/_Project/Code/UI/Shell/ShellQuit.cs#L18)
  calls `Application.Quit()`. [SaveService](../../Assets/_Project/Code/Core/Save/SaveService.cs#L124)
  also saves on application quit.
- [PlayerBuild](../../Assets/_Project/Code/Tools/Editor/PlayerBuild.cs#L17)
  sets the product/company used in the default executable and log instructions;
  its `build-info.txt` records the actual product if the build overrides it.
- Historical trial sources at `11b09e2637`, **absent from main**, are under
  `Assets/_Project/Code/App/CoopSpike/`: `CoopDevStart.cs` defines the launch
  flags, product gate, transport, two-person approval, memory-only save and
  disconnect log; `CoopSpikeSettings.cs` sets EastWater and port 7777;
  `CoopBoatReplica.cs` makes Boat owner-written and Sea server-written;
  `CoopSeaState.cs` rejects a mismatched seed and applies time/rate/pause.
  `CoopBoatState.cs` explicitly says “no catch, wallet, hands or save state.”
  Prior trial records report 17 EditMode, 9 network PlayMode and 1 boot test
  passing; the final native pair had 110 common time samples and 2,750 matching
  school records. Those were one-PC checks, not new results for main.
- History from trial's main `51d30dca25fa836696be476b8b134b9ff990a9bd` to
  reviewed main contains **zero changes** on the co-op runtime/resources/tests,
  networking manifests, shell, boot, save, clock and environment paths above.
  The private trial head is not an ancestor of reviewed main. Related build
  changes are `c34ad86a41` (#1110), adding the Windows exporter and product/log
  identity, and `3504d3ae7d` (#1116), adding South Water to its scene list and
  build settings. Neither integrates co-op.

The remaining prerequisite for shared play in a fresh main build is a reviewed
co-op integration and a newly verified player from the build lane. This guide
does not supply either.
