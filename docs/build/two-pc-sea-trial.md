# Two PCs, one sea: how to try it with a friend

This is the first co-op test. Two people, each on their own PC, each in their own dory at
St Peters. You see each other's boat, and you share one clock, one tide and one weather.
That is all it does. The list of what does not work yet is at the end.

Everything here uses **Windows and PowerShell**. Nothing is saved, on either PC.

## What you need

- Two Windows PCs on the **same home network** (same router, by cable or Wi-Fi).
- The same copy of the trial folder on both PCs. It holds `TwoPcSeaTrial.exe` and the folders
  beside it. Copy the whole folder, not just the `.exe`. Both PCs must have the same build.

The trial is a separate application with its own name. It does not read or write the saved
game of the ordinary game.

## Making the trial folder (once, on the PC that has the project and the editor)

Close the editor for that project folder first. Then, in PowerShell:

```powershell
$project = "<the project folder>"
& "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -batchmode -quit -projectPath $project -executeMethod HiddenHarbours.CoopSpike.Editor.CoopSpikeAssets.BuildWindowsTrial -logFile "$project\Builds~\coop-trial-build.log" | Out-Null
Get-Content "$project\Builds~\coop-trial\build-summary.txt"
```

The first line of the summary should read `result=Succeeded`. The folder to copy to both PCs is
`<the project folder>\Builds~\coop-trial`.

## Step by step

### 1. On the first PC (the host)

1. Open the trial folder and double-click `TwoPcSeaTrial.exe`.
2. The title page appears. In the top right corner there is a small box named
   **Two-PC sea trial**. Click **Host a session**.
3. The window closes and opens again by itself. This takes a few seconds and is expected.
4. The first time, Windows may ask whether to allow the application through the firewall.
   Tick **Private networks** and click **Allow access**.
5. You are now in your dory at the St Peters pier. A strip along the top reads something like:
   `Hosting. Your friend types: 192.168.1.20   Port 7777.   Players 1 of 2.`
6. Tell your friend the four numbers with dots. That is the host's address.

### 2. On the second PC (the friend)

1. Double-click `TwoPcSeaTrial.exe`.
2. In the **Two-PC sea trial** box, click in the address field, type the host's address
   (for example `192.168.1.20`) and click **Join**.
3. The window closes and opens again by itself.
4. The strip along the top first reads `Looking for the host at ...`, then
   `Joined the host at ... You share his clock, tide and weather.`

### 3. What you should both see

- Your own dory, with you at the helm, at the St Peters pier. The friend's dory lies a few
  metres off the host's.
- The other player's dory and the other player standing in it. When he steers away, his boat
  moves on your screen.
- The host's strip changes to `Players 2 of 2`.
- The same time of day on both screens, and the same tide and weather.
- If the host pauses, the friend's clock stops too.

### 4. Leaving

Close the window. If the friend leaves, his boat disappears from the host's screen and the host
plays on. If the host leaves, the friend's strip reads `The session has ended` and he closes his
window too. To play again, start the application again.

Sometimes Windows records the close as an unexpected stop of the application. That is a known
fault of closing the game from inside the world (see `friend-build-shutdown-crash.md`). The
session has already ended cleanly by then and nothing is lost, because nothing is saved.

## If it does not connect

The friend's strip will read `No answer from ...` after about fifteen seconds.

1. **Start the host first.** The friend can only join a host that is already running.
2. **Check the address.** On the host PC, in PowerShell:

   ```powershell
   Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -ne "127.0.0.1" -and $_.IPAddress -notlike "169.254.*" } | Select-Object IPAddress, InterfaceAlias
   ```

   Use the address of the Wi-Fi or Ethernet line.
3. **Check the firewall on the host PC.** If the Windows question was closed or answered "no",
   open PowerShell **as administrator** on the host PC and run:

   ```powershell
   New-NetFirewallRule -DisplayName "Two PC Sea Trial" -Direction Inbound -Protocol UDP -LocalPort 7777 -Action Allow -Profile Private
   ```

   To remove that rule later:

   ```powershell
   Remove-NetFirewallRule -DisplayName "Two PC Sea Trial"
   ```
4. **Check that both PCs are on the same network**, and that the network is set to Private in
   Windows on the host PC. Guest Wi-Fi networks often stop PCs from seeing each other.

## Starting without the menu

The menu only starts the application again with these options. You can type them yourself, in
PowerShell, from inside the trial folder.

Host:

```powershell
& .\TwoPcSeaTrial.exe --coop-spike-host
```

Friend (put the host's address in):

```powershell
& .\TwoPcSeaTrial.exe --coop-spike-client --coop-spike-address 192.168.1.20
```

A different port, on both PCs: add `--coop-spike-port 7778`.

## Trying it alone on one PC

Start the application twice. Host in the first window. In the second, type `127.0.0.1` and
click Join.

## Over the internet

Not tested. It can work if the host's router forwards UDP port 7777 to the host PC and the
friend types the host's public address. There is no invitation, no friend list and no relay.

## What has and has not been checked

Checked, with two copies running on one PC: host and friend connect, each gets his own dory,
both see the other's boat in the same place, the clock, tide and weather agree, a third copy
is refused, and nothing is written to a saved game.

Not checked yet: two separate PCs on a real home network, the Windows firewall question, and
the menu box under a real mouse and keyboard (it was pressed by a test switch, not by hand).
If the menu does not answer to clicks, use "Starting without the menu" above.

## What does not work yet

- **Nothing is shared except the sea.** Each player has his own dory, his own catch, his own
  money and his own hands. A fish one player catches does not exist for the other.
- **One shared dory with one rowing and one fishing is not in this test.** It comes in a later
  step of the plan.
- **Nothing is saved.** Every session starts fresh on both PCs. No progress is kept.
- **The friend does not see you ashore properly.** The other player is drawn as a figure in or
  near his boat. Walking the village together is a later step.
- **Islanders, shops and the story are each player's own.** Talking to someone or buying
  something happens only on your own PC.
- **Each player looks the same.** Your own character's look is not sent.
- **Two players only.** A third sees `The host said: This session is full.`
- **At low tide the host's dory is drawn at the foot of the pier,** partly under the planks.
  The friend's dory lies clear of it.
- **Closing the window can be recorded by Windows as an unexpected stop.** See "Leaving".
- **Both players are at St Peters.** There is no travelling to another place together.
- **A typed address only.** No Steam, no invitations.
- **If the host leaves, the session ends.**
- **The other boat can look jumpy on a poor Wi-Fi link.** Nothing smooths over lost messages yet.
