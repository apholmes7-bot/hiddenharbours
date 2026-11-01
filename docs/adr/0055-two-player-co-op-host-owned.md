# ADR 0055 - Two-player co-op, host-owned

- Status: **PROPOSED**. Throwaway spike record; no decision to ship co-op.
- Date: 2026-11-01.
- Steward: lead-architect and gameplay-systems.
- Serves P1, P3 and P4; preserves the shared outfit's progression under P2.
- Basis: [multiplayer scoping study](../design/multiplayer-scoping.md), especially workstreams 2, 4, 6 and 7; ADRs 0008, 0043 and 0051.

## Scope and evidence

The authorized experiment is two players, one hosting, sailing one sea and seeing each other.
The branch is never merged. This draft does not amend the canon or schedule a production build.
The day-1 price is 52 effort hours including reserve over seven active days; waits for leases,
coordinator rulings and prerequisite scenes/panels do not count toward that time box.

Save isolation, package resolution and the first editor import are verified. NGO 2.13.3 and
Relay 1.2.0 resolve; the editor selects its built-in Transport 6.5.0. The shipped public
`Unity.Netcode.TestHelpers.Runtime.NetcodeIntegrationTest` compiles into
`Unity.Netcode.Runtime.Tests.dll`. The editor result clears the earlier tarball-only inference;
there is no custom harness. Five spike EditMode checks pass.

Day 1 now has two passing PlayMode cases after correcting the test assembly's platform list and
isolating the synthetic keyboard through the installed Input System test fixture. They prove two
owned boats, owner movement, the host clock reaching the client, and one client F change reaching
the hull drawn on the host. They do not prove the full roster, the local fishing loop or two games
sharing the current static services. Original replication and drawing assertions were retained.

Day 2 is authorized within the 52-hour price. The authored East Water was taken from main through
a conflict-free merge; this branch does not construct or modify that region. Headless preparation
selects it for dev start, preserves the existing persistent core when loading it, skips the opening,
boards each local player with an authored rod, and publishes the character's drawn compass heading.
The skipper remains the baked look. Creator integration, join recipes and appearance persistence
remain held under the Day 2 order, irrespective of similarly named types already in the codebase.

Six additional PlayMode cases are written: both dev starts in the authored region; both full F
cycles including wrap; both baked skippers' positions and facing; tide/weather and nonempty school
records over twelve transported clock samples; both unchanged local rod loops and separate catch
destinations; and disconnect cleanup of the remote skipper. The host-view capture is prepared in
the skipper case. These cases compile but have not run. No new frame, Windows build, relay session,
friend play or observed internet latency exists. The editor remains closed for the requested restart.

All recommendations below remain proposals to validate. No test result or observed latency is
implied by describing the intended design.

## Proposed transport and authority

Use state synchronization, never lockstep. Unity boat physics is not the deterministic sea.
The locally owned boat simulates on its owner's machine; an owner-authoritative network transform
carries position and heading. The host spawns one network player boat per participant. Stable hull
ids, skipper appearance and the skipper's place aboard supply the other screen's presentation.
Remote presentation must not publish a second local helm, active boat, body or pair of hands.

The host publishes an atomic world seed and absolute game time at join and periodically thereafter.
The client uses `IGameClock.SeekTo`. Both derive tide, wind, weather and fish schools from the
same seed/time and authored region data. Seed validation/application must happen before claiming
agreement: seeking time alone cannot repair different seeds. Clock correction error and pause
behavior need measurement. No environmental random state is introduced or saved.

Local verification uses Unity Transport on the loopback interface and no cloud service. Internet
play uses the owner's relay project after owner-performed setup. No credentials or project secrets
belong in code, evidence, reports or this ADR. No host migration or persistent server is proposed.

## Proposed production player context (study workstream 2)

Give each participant an explicit context carrying the local/remote identity and references for
`ActiveBoat`, `HelmControl`, `HelmInstruments`, `Appearance`, `PlayerTransform`, `Hands`,
`CatchHands` and `PlayerHold`. The existing helm arbiter is still one local player's selection;
its presence does not make it a multi-player context. Audit consumers rather than cloning the
static service locator. Camera and input resolve the local context; actor-sensitive interactions
and presentation resolve an explicit actor. World clock/environment remain shared world services.

The spike may keep one gameplay context per process and draw the other participant without
gameplay registrations. In-process network tests must use injected contexts or isolated fixtures;
two NetworkManagers alone do not prove that two complete games can share the current statics.

## Proposed market and fishing authority (workstreams 6 and 7)

For a real build, the host owns market ticks, transaction ordering, the shared wallet, fleet and
licenses. Client requests carry identity and a request id; validation and deduplication precede
the authoritative mutation. Two simultaneous sales must not credit one catch twice.

School locations remain deterministic. Bite/catch rolls and the real-time rod fight are different
authority questions: the host must validate fishing eligibility and own fishing rolls and landed
catch creation, with client presentation reconciled against its result. Exact fight prediction and
validation remain unpriced production design, not a lockstep promise.

The authorized spike instead leaves each screen's fishing path and catch local and unchanged.
Landing a fish locally does not prove shared catches, market correctness, attribution or anti-cheat.

## Saves and appearance

The spike uses its own product name, leaving the normal game's save destination untouched.
The intended local spike-save path keeps that participant's committed appearance through the
existing appearance/save path; that integration still needs permission and play proof. Send the
committed look on join once that gate opens; remote preview is presentation,
not a write into the host's save. Do not save network ids, connections or co-op session state.
The Day 2 order explicitly holds the creator's panel integration and friend delivery. The spike
boot does not open a creator or transmit a custom recipe while held. The baked look is the only
implemented remote presentation at this point. Existing type names do not clear a delivery gate.

For production, propose host ownership of the world save plus a thin per-player record for body
position, occupied helm and hands. Keep a participant's reusable appearance recipe locally and
cache a validated session copy on the host for display; ownership, reconnect identity and migration
need an accepted ADR before implementation. Do not infer those rules from the spike's save isolation.

## Owner questions that remain leans

The shared purse is ruled. The study's remaining questions are not decided by this draft:

- Q6: lean two players.
- Q2: lean together; changing region takes both.
- Q3: lean skip night only when both have turned in.
- Q4: lean host-owned world, played when both are on.
- Q5: lean a quitter's boat stays anchored and its catch belongs to the outfit.

The owner answers these before a real build, not before the spike.

## Required experiment record

Record instruments reading the wrong boat, doors/body resolution, hands/catch destination,
selling, session persistence and the ghost skipper as observed or not reached. Record what each
player actually did, hull changes seen remotely, both local fishing loops, environmental agreement,
appearance at rejoin, friend-observed delay, build/account/NAT costs and the cost of each added ask.
Day 1 reached the network hull and clock checks listed above. Day 2's full-roster, school, skipper,
hands/catch and disconnect cases remain **written, not run**. Instruments reading a different
player's hull, doors and interior body resolution, selling, and persistence/rejoin remain **not
reached**. East Water is now inherited from main. Friend delivery still waits for the creator gate.

The in-process fishing fixture explicitly selects one local context at a time and uses a separate
memory-only save for each angler. It exercises the existing dev-input rod gate, cast, authored bite,
authored fight and hold path; it does not establish that two complete static-service games can run
concurrently. Remote actors carry no gameplay boat controller, walking controller or hands. A real
per-player context remains production work even if these bounded presentation checks pass.
