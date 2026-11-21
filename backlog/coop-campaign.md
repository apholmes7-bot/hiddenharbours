# Campaign co-op — CO-01 to CO-12

Owner-directed, 2026-11-13. [ADR 0057](../docs/adr/0057-campaign-coop-host-world.md)
is Proposed; the product direction is two friends playing the full campaign over
the internet in the host's world. One save, purse, fleet and licence set; separate
hands and characters. Crew the one earned dory first. Both join aboard the intro
boat at sea from the opening, fish and sail in together. No wharf-only substitute.

CO-01 is in progress in this draft; no row is claimed shipped by this backlog.
CO-01 to CO-04 may run beside M1. Reassess the larger CO-05 at its two-machine
checkpoint. The seat orders conflicting lane needs; M1's gate does not move.
Business, staff and freight remain M3. Chat is separate.

Sizes: S = one to two lane days; M = three to five; L = six to ten. A row is one
PR unless stated otherwise. Core/save changes need lead-architect review. A star
marks the minimum sequence for playing the campaign together from the intro.

| # | Slice | After it, the two players can... | Size | Role | Editor | Core / save | Proof |
|---|---|---|---|---|---|---|---|
| CO-01 ★ | **Land the branch on main**, off by default. Fix the product name, drop the "0055" draft, approve the packages. ADR 0057 and the roadmap line ride with it. | ...do exactly what the zip does today, but from a build of current main (jet ski, new hulls, lamps, fixes). | M | lead-architect | Yes, one import and test run | Core no, save no | Solo CI green and unchanged. The trial's 18 edit and 9 network tests pass on main. A solo build's product name and save path are unchanged. |
| CO-02 ★ | **Fix the crash on close.** Likely cause, read from code and not yet proven: the only runs that exit cleanly shut the network down before quitting (`CoopProcessProof.cs:103`); an ordinary window close never does (`CoopDevStart.cs` has no shutdown on quit). Also fix the wake-pose error storm (78,550 errors after a hull change, `FoamInjector` / `MeshHullDriver.TryGetWakePose`). | ...close the game without a crash report. | S | gameplay-systems | Yes, two-process close proof | No | Ten ordinary closes each side, host first and guest first, all exit code 0. If the crash also shows in a solo build, it is not a co-op bug and goes to the art lane. |
| CO-03 ★ | **Host and Join from the title menu, and a way in from another house.** Host picks New Game or Continue on their real save. Guest types an address or a code. The guest's computer gets a save that cannot write. | ...start a session without command lines, on the host's real campaign file, from two houses. | M | ui-ux + lead-architect | Yes | Core yes (session contract, read-only save facade). Save no. | Tests: third player refused, wrong build refused, guest quit writes no file. A real two-house join. Relay needs the owner's Unity account link (owner step). |
| CO-04 ★ | **The full clock.** Send the hour hold and flow as well as the time, including joining aboard the intro boat at sea. Guest menus stop pausing anyone. | ...join the intro boat at sea and see the same sea and held hour. | S | gameplay-systems | No (CI) | Core yes, additive | Tests: join during a hold, stale packet, wrong seed, midnight crossed once. |
| CO-05 ★ | **Two bodies in one place.** Each player has their own body, hands and helm claim, addressed by who they are (not "the player"). Support both passengers aboard the intro boat from the opening; walk ashore and crew one earned boat, one steering while the other stands on deck. The friend's figure shows the right look, walk and helm pose. | ...walk the wharf and village together and crew one dory. | L (two PRs: the Core per-player context, then presence) | lead-architect, then gameplay-systems | Yes, plates on both screens | **Core yes, the biggest one.** Save no. | Tests: two helm claims, one winner; drop releases the helm. Two-machine check: does standing on a moving deck feel right for the guest? This is the go or no-go point. |
| CO-06 ★ | **Travel together.** One region at a time; both at the passage or aboard, including the intro crossing and landfall together. | ...leave St Peters and arrive together. | M | world-content | Yes | Core yes (one group crossing). Save no. | Slow loader, failed load and a drop mid-crossing each leave both players in one region with no copied cargo. |
| CO-07 ★ | **One story.** Story flags, quests, onboarding and NPC conversations are decided on the host and shown to both. Then the intro: both aboard the arrival boat at sea from the opening, each makes their character, both fish and sail in together, then land. Wharf-only admission is not the target. | ...start a new game together, play the opening, be taught by Ginny, and see the same quest list. | L (two PRs: shared flags and dialogue, then the two-passenger intro) | world-content | Yes | Core yes (who is acting, on dialogue and quest signals). Save no: the host's ordinary save already keeps the story. | Two players choosing at once pays one reward. New game to landfall with two passengers. Drop during the crossing recovers. |
| CO-08 ★ | **One purse, real catch.** The host confirms each landed fish, each lift and put-down, and each sale or purchase. Hands and pails show on both screens, including fishing together during the intro at sea. | ...fish, carry a pail between them, sell it, and spend from one wallet. | L (two PRs: catch and carry, then buy and sell) | gameplay-systems, then economy-sim | Yes | Core yes (actor on catch and sale events). Save no. | Two sales of one pail: one payment. Two buys with money for one: one succeeds. Counts and freshness exact after a retry. |
| CO-09 | **The guest is remembered.** The host's save gains a small record per player: place, hands, notes. | ...quit in the evening and come back as the same two people in the same spots. | M | lead-architect | No for the migration (CI); yes for one resume proof | **Save yes: planned version 18 (reconfirm against main), migration, old-save test.** | Version 17 save loads. Round trip of both players. A fault mid-write leaves a whole before or after file. Guest disk untouched. |
| CO-10 | **The friend's boat is a real boat.** Cabin, door and cutaway on the other player's boat; later, each can skipper a different owned boat. | ...see inside each other's cabin; then sail two owned boats in one region. | M, then L | gameplay-systems | Yes | Core small | Replica cabin, door and cutaway cases pass on the replica. Two holds never swap. |
| CO-11 | **Vehicles, building interiors, sleep.** Riding and towing shown to the other player; indoor levels; both-ready sleep. | ...drive, go indoors and end the day together. | M | gameplay-systems + world-content | Yes | Core small | One vehicle, one driver. Night advances once. |
| CO-12 | **Acceptance run and performance.** | ...play new game to rest and resume on two real machines at 60 fps. | M | qa-test | Yes | No | Frame times, bytes per second and corrections recorded on the friend's actual computer. |

CO-01 brings only today's optional LAN trial onto current main: separate local
boats and catches, shared presence and seed/clock, no campaign save or story.
The crash on ordinary close is a known CO-02 fault, not fixed by CO-01.
The three package additions require owner and lead-architect approval before merge.

CO-01 to CO-08 are the minimum campaign sequence. The host's ordinary save keeps
host progress once the later campaign slices land. Until CO-09, the guest rejoins
empty-handed at the campaign's supported join point; during the intro this is
aboard the arrival boat, not a wait for landfall. Testers must stow carried goods
before leaving. The guest keeps only local character look/name between worlds;
fish, money and boats never go home with them. A live disconnect leaves supported
goods recoverable by hand; it never auto-sells or teleports them.

The intro is deliberately before persistence. CO-09 owns the migration and must
check the next available schema version then. No save change is part of CO-01.
Rough total: 35–60 focused lane days, excluding review and waits. CO-05's moving
deck experience is the principal go/no-go risk, not an assumed solved problem.
