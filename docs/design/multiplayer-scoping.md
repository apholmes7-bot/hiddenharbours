# Multiplayer — Scoping Study (answers an owner question; commits to nothing)

> **Status: INFORMATIONAL. Docs-only.** This ships no code, no package, no ADR and no backlog item.
> It answers two owner questions — *"what would multiplayer cost?"* and *"how quickly could I test
> with a friend?"* — against the code that actually exists on `main` today, so the decision can be
> made with numbers instead of vibes.
>
> **Multiplayer is not in the canon.** [`vision-and-pillars.md`](../vision-and-pillars.md) never
> mentions it, [`roadmap.md`](../roadmap.md) never schedules it, and
> [`architecture/tech-architecture.md`](../architecture/tech-architecture.md) §10 explicitly files
> networking as *"out of scope for now"*. Nothing below changes that. If the owner wants it in the
> canon, it goes in the canon **first** (CLAUDE.md §1), then here.
>
> - **Date:** 2026-08-04. **Re-measured 2026-08-14** — see **§10** for everything that moved.
> - **Measured against:** commit `9a0fabf` (2026-08-14), Unity 6000.5.0f1, **~200.1k lines of
>   non-test C# across 799 non-test files** (1,359 `.cs` including tests) and **177 MonoBehaviours**.
>   *Originally measured at `b8dcebe` (2026-08-04): ~128.6k lines, 565 non-test files, 142
>   MonoBehaviours. The first draft's "957 files" was the all-`.cs` count including tests, not the
>   non-test count it was labelled as; both figures are stated separately here so they cannot be
>   conflated again.*
> - **Owner rulings recorded here:**
>   - **Shared purse** (2026-08-04, §6.1.1) — one business, one wallet, several skippers. Deciding it
>     shrank the largest workstream and closed the study's biggest design risk; the sizings below
>     already reflect it. Recorded verbatim and unchanged by the refresh.
>   - **Finish and merge** (2026-08-14) — the owner ruled that this study is to be completed and
>     landed on `main` rather than left open as a draft. **Merging it changes nothing about scope:**
>     the doc stays INFORMATIONAL, multiplayer stays out of canon
>     (`tech-architecture.md` §10), and the recommendation in §8 is still *don't build it now*. What
>     the ruling buys is that the study stops rotting on a branch — which is why §10 exists.

---

## 1. The two-sentence answer

**Two people sailing boats on the same sea, seeing each other, sharing one tide — is roughly a
week**, and it is a genuinely cheap spike because the sea itself needs almost no networking (§3).
**Two people actually *playing Hidden Harbours* together — fishing, selling, owning boats, saving —
is a multi-month rewrite of the seams the whole project has spent 530+ PRs deliberately building
around a single player**, and it would land on top of an M1 that is not finished yet.

The gap between those two numbers is the entire content of this document.

---

## 2. Three different things called "multiplayer"

Be specific about which one is wanted, because they price differently by an order of magnitude.

| | What the player experiences | Rough cost | Verdict |
|---|---|---|---|
| **T0 — Shared build, separate worlds** | You and a friend each play your own save, same `worldSeed`, and compare notes. No networking. | **Days** (it is a build, not a feature) | **Already the plan.** M1 is a "Steam / itch.io closed playtest" (`roadmap.md` §M1). |
| **T1 — Local co-op, one machine** | Two gamepads, one screen. | Weeks–months | **Poor fit, don't.** See §7. |
| **T2 — Networked co-op, 2–4 friends, one player hosts** | You sail out together, fish the same grounds, sell into the same market. | **Months** | The real question. Scoped in §5–§6. |
| **T3 — Persistent shared world / dedicated servers** | An always-on Sablewick Banks. | Years + running costs | Not a fishing RPG any more. Out of scope. |

Everything below concerns **T2** unless it says otherwise.

---

## 3. The good news: the sea is already free

This is the genuinely unusual thing about this codebase, and it is worth understanding before
reading the cost sections, because it is a real asset that most projects do not have.

**CLAUDE.md rule 5 — the determinism contract — means the expensive half of a fishing game's netcode
does not exist.** Tide, wind, weather, sea state, visibility, the moon, and the fish schools are all
*pure functions of `(worldSeed, gameTime)`*, recomputed and never saved
(`tech-architecture.md` §1.2, §4, §4.1). So to make two machines agree about the sea, you sync:

```
worldSeed   (int, once at join)
gameTime    (one double, occasionally)
```

…and **that is the whole environment sync.** Both clients then independently compute a bit-identical
tide, an identical gale, and identical fish schools in identical places. In a normal networked game
this layer is continuous bandwidth and a constant source of desync bugs; here it is ~16 bytes and a
correction every few seconds.

Better still, **the seek machinery already exists and is already tested.** `IGameClock.SeekTo(double)`
was built for save-restore (`Core/Save/SaveRestore.cs:72`) and does exactly what a late-joining
client needs. A joining player calling `SeekTo(hostTime)` inherits the host's entire world state for
free.

What this buys, concretely — none of the following needs to travel over the wire:

- the tide height and the water surface, everywhere, for everyone
- wind vector, sea state, fog/visibility, the weather fronts
- **where the fish are** — `Fishing.FishSchoolMath` is a hashed pure function of
  `(worldSeed, gameTime, place, weather, season)`, with `FishSchoolModel` and the `IFishSchools`
  seam over it (`tech-architecture.md` §4's fish-school seam; ADR 0025 S3a). *"No spawner, no
  `Update`, no timer, nothing saved."*
- the wave field, hull rocking, wakes, buoy motion (ADR 0018 — one shared derivation)
- **where every villager is, all day** — this got *stronger* since the first draft. NPC routines are
  no longer merely "dormant"; the village day is now a pure function of the clock
  (`World/Routines/RoutineSchedule.cs:10`, PR #514), and the code says the multiplayer case out loud
  without having been asked to: *"a function of the clock, never a saved state machine (CLAUDE.md
  rule 5), **so joining a session at any moment puts everybody exactly where the clock says they
  should be**."* The ambient fleet is the same shape (`AmbientFleetSchedule`).
- authored geometry, the seabed height map

**The honest asterisk.** Determinism holds for the *sim*, not for *Unity physics*. Boats are
`Rigidbody2D` on the Box2D-v3 backend (`tech-architecture.md` §5), and Unity physics is not
guaranteed bit-identical across machines or builds. So this project can do **state-synced** netcode
very cheaply; it **cannot** do lockstep/deterministic-rollback netcode without replacing the boat
physics with a custom fixed-point integrator. Do not attempt lockstep. Also non-deterministic by
explicit design: the rod fight is *"real-time and RNG-injected — **not** part of the
`(worldSeed, gameTime)` determinism contract"* (`tech-architecture.md` §4.4).

---

## 4. The bad news: "the player" is a global variable

Every single-player assumption in this codebase is concentrated in one place, which is good news for
finding them and bad news for how load-bearing that place is.

`Core/Services/GameServices.cs` is a **static service locator** — read at **535 sites across 140
files** (was 399/109 on 2026-08-04). `Core/Events/EventBus.cs` is a **static, process-wide** pub/sub
— **261 sites across 71 files** (was 217/60). Between them they are the project's nervous system,
and they are built on the assumption that there is exactly one of everything.

The useful discovery is that the members split cleanly in two:

| **World-scoped — fine as globals, no change needed** | **Player-scoped — must become per-player** |
|---|---|
| `Clock`, `Environment` | `ActiveBoat` — ⚠ **one slot, one boat** |
| `TidalTerrain`, `CurrentRegionBounds` | `HelmControl` — whose hand is on the tiller |
| `Config`, `WaveField`, `WaveFetch`, all tunables | `HelmInstruments` — whose dash |
| `FishSchools`, `RadarContacts` | **`PlayerTransform`** — ⚠ **whose body** *(new since 2026-08-04)* |
| `CatchFactory`, `AudioMix` | **`Hands`** — ⚠ **whose hands** *(new)* |
| `IconRegistry`, `RegionDisplayNames` — *separate Core statics (`Core/Services/IconLibrary.cs`, `Core/Services/RegionDisplayNames.cs`), not `GameServices` members; the first draft filed them here in error. World-scoped either way.* | **`CatchHands`** — ⚠ **whose catch lands where** *(new)* |
| `Wallet` — **stays shared** (owner ruling, §6.1.1) | `CurrentRegionId` — *if* players may be apart (§6.1.2, open) |
| `Licenses` — **stays shared** (owner ruling, §6.1.1) | `PendingArrivalKey` — same condition; consume-once *(new)* |
| `Save` — **one world blob**, plus a thin per-player record (§6.1.1) | |

That right-hand column is the project.

**⚠ That table reflects the owner's shared-purse ruling (§6.1.1) — and it grew after it.** Before
the ruling, the player-scoped column held **seven** members; the ruling returned `Wallet`, `Licenses`
and most of `Save` to the world-scoped column, leaving **three** irreducible ones (plus a conditional
fourth). That was true on 2026-08-04 and is the sizing §6 still carries.

**It is no longer three. As of `9a0fabf` it is six, plus two conditional — and the verdict the first
draft drew from "three" does not survive the re-measure.** The draft said the three helm members were
the *complete* set a second player breaks, and reasoned that "everything else about 'the player' is
now, by ruling, about *the business*." **That reasoning was wrong about the future, not about the
ruling.** Three PR arcs since (#512/#515 travel, #525 the carry seam, #528/#529 pick-up-and-carry)
added exactly the kind of member the shared purse *cannot* absorb, because they are about a person's
**body**, not the outfit's books:

- **`PlayerTransform`** (`GameServices.cs:224`) — the travel-aware relay every region resolves the
  player against, published by `App/RegionTravelCoordinator.cs:88`. **The `ActiveBoat` failure mode
  exactly, one level down:** it is a single slot, so with two players every door, interior and
  dialogue in every region watches one arbitrary body. Its own doc-comment explains that region
  content *physically cannot* be wired to a player at build time — which is why it is a global, and
  why per-player resolution has to happen here rather than by wiring it away.
- **`Hands`** (`:260`) and **`CatchHands`** (`:284`) — the carry seam, both published by the one
  `Player/CarryHands.cs:102-103` on enable and cleared on destroy. `Hands` answers "is the shovel in
  your hands?" for gates in other lanes; `CatchHands` is where a landed clam goes. Two players, one
  slot: your friend's dig checks *your* hands.
- **`PendingArrivalKey`** (`:324`) — which way in you took, consume-once. Conditional on §6.1.2 in
  the same way `CurrentRegionId` is, and *more* fragile: it is explicitly documented as unsafe to
  leave standing, and two players crossing at once would consume each other's.

The shared-purse ruling's reach is unchanged and its logic still holds — **what grew is the surface
it was never going to cover.** The honest statement is now: *the irreducibly-personal set is
everything about where your body is, what is in your hands, and which helm you are standing at* —
and that set is drifting upward as the game grows a body to inhabit. That is a real finding of the
re-measure, and it makes workstream 2 bigger than the ruling left it (§6, §10).

Each member of that column — the three from the first draft and the three that joined them — has a
documented "there is exactly ONE of these" invariant that a second player breaks:

- **`ActiveBoat` is a single slot** (`Boats/ActiveBoatProbe.cs:48` — `OnEnable() => GameServices.ActiveBoat = this`).
  Put two crewed boats in a scene and **whichever enables last wins**; the depth sounder, fish finder,
  compass and HUD then all read the *other* player's boat. This one bites in the spike, not just the
  full build (§5). *(Citation re-verified at `9a0fabf` — still line 48, still that exact line.)*
- **`HelmInstruments` is downstream of `ActiveBoat`, not independently personal.** ADR 0030 (accepted
  2026-08-03, landed since) makes instrument *ownership* per **hull** — flat `(hullId, instrumentId)`
  rows — and a hull belongs to the outfit, so under the ruling the instruments themselves are world
  state. Only the *which dash am I reading* pointer is per-player. That is a smaller problem than the
  first draft implied, and it is the one place the re-measure moved a number **down**.
- **Pause is global by design.** `IGameClock.IsPaused` is documented as *"the project's ONE pause
  path, no second clock"* (`tech-architecture.md` §6) and the shell stops the world through it. In
  co-op you cannot stop the sea because your friend opened a menu — so the shell, the title flow,
  `ShellPause`, `WorldInputBlocked` and the settings sheet all need rework. *(Re-verified: still the
  one pause path — `Core/Shell/ShellPause.cs:7`, `Core/Shell/ShellFlow.cs:16`.)*
- **The EventBus has no sender.** `FishCaught`, `MoneyChanged`, `CatchSold`, `BoatPurchased`,
  `TrapPlaced` all mean *"the player did this"*. **Re-verified at `9a0fabf`: still no sender** — not
  one payload in `Core/Events/GameSignals.cs` carries a player, actor or sender id. The attribution
  surface has grown rather than shrunk: `CatchDumped`, `TrapHaulStateChanged`, `ControlModeChanged`
  and `ActiveBoatChanged` all landed since and all mean "*the* player", singular. The shared-purse
  ruling softens this considerably:
  `MoneyChanged` and `BoatPurchased` are now genuinely *business*-level facts and need no identity to
  be **correct**. But the catch/sell/trap signals still want a sender for **attribution** — "Sam
  landed a 12 kg cod" is most of what makes co-op feel like co-op, and a trap needs to know whose
  hands set it. Identity becomes a presentation need rather than a correctness need, which is a much
  cheaper kind of need.
- **The save is one blob** (ADR 0008). Under the ruling it stays *mostly* one blob — a
  host-owned world save (money, fleet, licences, market, flags: all shared) plus a **thin** per-player
  record (position, which boat you're at the helm of, gear in hand). Still a schema bump with a
  migration, but a far smaller one than separate purses would have forced. `SaveService.WritesAllowed`
  and the quit-to-title teardown still change meaning when several people share a world.
  **⚠ Corrected: the schema is v10, not the v2 the first draft cited**
  (`Core/Save/SaveMigration.cs:16` — `CurrentVersion = 10`). That correction *helps* the estimate
  rather than hurting it: eight versioned bumps with working migrations have shipped since v2, so
  "bump the schema and migrate" is demonstrated routine machinery on this project, not a risk to be
  priced in. Workstream 8 holds at S–M, and holds more firmly than when it was written.

### 4.1 The input layer that was specified but never built

`tech-architecture.md` §3 and §9 promise an `InputService` translating raw input into intents
(`MoveIntent`, `ThrottleIntent`, …) — *"platform-swappable"*, so that desktop/gamepad *"is a new
input map, not a rewrite"*.

**It still does not exist** — re-checked at `9a0fabf`: there is no `InputService`, no
`IInputService`, and no `MoveIntent`/`ThrottleIntent` type anywhere in the tree.
`Keyboard.current` / `Mouse.current` / `Gamepad.current` are read directly at **73 sites across 37
files** (was 55/31) — `PlayerWalkController`, `DevBoatInput`, `DevFishingInput`, `WorldInteractor`,
`HelmOverlayHost`, `ControlSwitcher`, `DeckWalkController`, `TrapHaulController`, the four instrument
overlay hosts, the shop screens, the shell and the title screen.

This matters more than it looks. **An intent seam is precisely the socket a remote player plugs
into**: a networked player is just a boat whose intents arrive from a wire instead of a keyboard. With
no seam, there is nowhere to inject them. Building it is unavoidable for co-op — and it is *already
owed* for the PC-first gamepad support ADR 0005 promises, and for the eventual mobile port. **It is
the one piece of multiplayer prep that is worth doing whether or not multiplayer ever happens.**

### 4.2 What the interact verb proved (new, 2026-08-14)

The refresh found one thing that cuts **against** the "everything is a global" framing, and it is
worth stating because it changes how hard §6's workstream 1 looks.

The contextual interact verb landed (#503, the M2-39 gameplay half) as a **Core seam**, and it was
built in exactly the shape this study says is missing. `Core/Interaction/InteractResolver` is a pure
function, and *who is reaching* is passed to it **as a value** — `InteractActor` (`InteractActor.cs`)
carries position, facing and context, and its own doc-comment gives the reason: *"Core may not name a
Player-lane type (rule 4)… passing the actor as three plain facts keeps the whole selection rule
EditMode-testable with no scene, no components and no input device."* `Interactables` is a
register-on-enable list, not a `GameServices` slot, for the same reason.

Three things follow:

1. **A second player is a second `InteractActor`.** Nothing about the verb assumes there is one of
   them. This is the first subsystem in the project where "the player" is a *parameter*, not a
   singleton — the seam §4.1 asks for, built once, for reasons that had nothing to do with
   multiplayer (testability and rule 4).
2. **It removes input sites rather than adding them.** `ClamDigger` reads no input device at all any
   more — each dig is an `IInteractable` and the resolver decides. The raw-input count rose to 73
   *despite* that, which is the trend, not a contradiction of it.
3. **`InteractIntent` is one of the four intents `tech-architecture.md` §3 promised.** It arrived —
   as a Core seam rather than through an `InputService`. So the intent *idea* is proven to fit this
   codebase and to survive review; what is missing is the generalisation, not the concept.

This does not make workstream 1 free. It does make it **lower-risk than the first draft implied**:
there is now a landed, reviewed precedent for actor-as-a-value in Core to copy, instead of a design
that existed only in a doc.

---

## 5. The spike: "two boats, one sea" — about a week

The cheapest honest experiment. Goal: **you and a friend, on separate machines, in Coddle Cove,
seeing each other's hulls move on the same tide.** Nothing else.

**Why it is cheap:** §3. The sea is free, so the spike is almost entirely transport plumbing.

| Step | Work |
|---|---|
| 1 | Add `com.unity.netcode.gameobjects` + Relay (or a Steam transport). ⚠ The manifest's `com.unity.multiplayer.center` is **not** networking — it is the Unity 6 recommender window and ships by default. There is **zero** networking capability in this project today. |
| 2 | `NetworkManager` on the persistent core. |
| 3 | `NetworkObject` + owner-authoritative transform on the boat prefab; spawn one per client. |
| 4 | Host publishes `worldSeed` + `gameTime`; clients call the existing `IGameClock.SeekTo`. **Both players now share a tide, weather and fish schools** — the payoff of §3, and close to a free line of code. |
| 5 | Build for Windows and get it to the friend. **✅ This step got cheaper since the first draft.** All four region scenes — `Greybox.unity` (Coddle Cove), `StPeters.unity`, `NineMileCreek.unity`, `Greywick.unity` — are now **committed and in `EditorBuildSettings.asset`** (ADR 0011 and the scene-banking PRs #507/#514/#518). The draft's warning that Nine Mile Creek and St Peters were *"builder-generated and uncommitted"* and had to be generated in-editor first is **no longer true** — delete it from your planning. A build today ships the whole coast, not one cove. |

**Wall-clock: ~1 week**, if it is the only thing being worked on. The engineering is maybe 1–2 days;
the rest is Unity licensing, a first Windows build, Relay/Steam account setup, and NAT traversal
being NAT traversal.

### ⚠ What the spike does NOT prove

Be clear-eyed, because it is easy to see two hulls moving and conclude the hard part is done.

- **Nobody can fish.** `FishingController`, the rod fight, the catch resolver and the hold are all
  wired to the one-player path.
- **Nobody can sell.** The market, wallet and shops are single-player.
- **Instruments read the wrong boat.** The `ActiveBoat` single-slot problem (§4) — the depth sounder
  and fish finder will show your friend's readings, or yours, arbitrarily.
- **⚠ And now: the wrong body, and the wrong hands.** New since the first draft, and it makes the
  spike's failure modes *more* visible rather than less. `PlayerTransform` and `Hands`/`CatchHands`
  are single slots too (§4), so with two players every door and interior in the region watches one
  arbitrary body (`RegionTravelCoordinator`), and a gate asking "is the shovel in your hands?" checks
  one arbitrary pair. This is good news for the spike, oddly: these break *loudly and immediately*,
  so a week of two-hulls-one-sea will surface the real shape of workstream 2 rather than hiding it.
- **Nothing saves.** Quit and the session is gone.
- **The second player is a ghost skipper** — a hull that moves. That is the *whole* deliverable.

It answers exactly one question, and it is a good question: **is sailing together fun enough to be
worth the rest of §6?** If two people mucking about in dories on a shared tide isn't delightful,
stop — cheaply, having spent a week.

---

## 6. Real co-op: the honest scope

Sized as workstreams, roughly in dependency order. "Cost" is relative effort at this project's
current pace, not a promise.

| # | Workstream | What it means | Cost |
|---|---|---|---|
| 1 | **Input → intents** | Build the `InputService` §4.1 already owes. **37 files, 73 sites** (was 31/55). Remote players become intent sources. **§4.2 lowers the risk**: the interact verb landed a reviewed actor-as-a-value precedent in Core to copy. | **M** — *owed anyway for gamepad* |
| 2 | **Player identity split** | ~~7~~ ~~3~~ **6** player-scoped singletons (+2 conditional) → a per-player context. The helm trio, **plus the body and the hands** the ruling cannot absorb (§4). Still **no Economy threading** — the ruling holds there. But **535** `GameServices` sites now, not 399. | ~~XL~~ **L**, *at the top of the band and drifting* — see §10 |
| 3 | **EventBus player identity** | Sender on the catch/sell/trap signals for **attribution**; money/purchase signals stay identity-free (§4). **261** sites to audit (was 217), most needing no change. | ~~L~~ **M** |
| 4 | **Transport + ownership** | Netcode for GameObjects, `NetworkObject` on boats/players/traps, client-authoritative movement with host reconciliation. **State-sync, never lockstep** (§3). | **L** |
| 5 | **Shell, pause & title rework** | The one-pause-path invariant dies (§4). Menus must not stop the world. Join/leave/host-migration flows are new UI that does not exist. | **L** |
| 6 | **Economy authority** | Host-authoritative market ticks + transaction ordering (two simultaneous sells must not double-credit one purse). The *rival-economy* balance problem — trade, undercutting, the exploitable spread — **is deleted by the ruling**: partners pool, they don't compete. | ~~L~~ **M** |
| 7 | **Fishing authority** | *Where* fish are is free (deterministic); *whether you hooked one* is not — the fight is explicitly RNG-injected (§3). Needs an authoritative roll. | **M** |
| 8 | **Save schema** | One world blob + a thin per-player record, migrated off **v10** (ADR 0008/0020) — *not* v2 as first drafted. Never strand a save. Eight shipped bumps since v2 make this proven machinery. | ~~M~~ **S–M** *(firmer)* |
| 9 | **Per-player region streaming** | Two players in different regions = two additively-loaded scenes at once. ADR 0004's scene model assumes one active region. **Now four committed regions with real travel between them** (#512/#515, `PendingArrivalKey`), so this is no longer hypothetical. | **M–L** |
| 10 | **Camera** | `App/CameraFollow.cs` follows one player and clamps to `CurrentRegionBounds` (`CameraFollow.cs:393`, re-verified). | **S** |
| 11 | **Content rework** | NPC routines, dialogue and onboarding all address "the player". Ginny teaches *one* fisher. Quest and onboarding flags are per-world today. **Grew materially**: four built regions, villager routines and dialogue (#514/#515/#524), and **23 incoming hulls** (`docs/design/fleet-flotation.md`). More coast to make two-player-aware. | ~~M~~ **M–L** |
| 12 | **Test & CI** | Multiplayer PlayMode tests, two-client harness. ⚠ CI is currently **not** an automated gate (CLAUDE.md rule 10) and needs `UNITY_EMAIL`/`UNITY_PASSWORD` secrets to pass at all. | **M** |

**Realistically: several months at this project's pace**, and — the part that is easy to miss — **it
competes directly with finishing M1**. M1 is the *"is this game worth making?"* gate
(`roadmap.md` §M1) and it is not done. Multiplayer would also **invalidate some already-shipped M1
work**, in the same way the owner's buy-and-repair ruling already flagged VS-21 for rework.

### 6.1 Questions only the owner can answer

These are design, not engineering, and **the engineering cannot start without them** — each one
changes workstream 2 and 6 substantially:

1. ~~**Shared purse or separate?**~~ **✅ DECIDED — owner ruling, 2026-08-04: SHARED PURSE ("for
   now").** One business, one wallet, one set of licences, one fleet; two or more skippers crewing
   it. See §6.1.1 for what it changes and the three follow-ons it opens.
2. **Can players be in different regions at once?** "Yes" costs workstream 9 and complicates
   everything. "No — you sail together" is dramatically cheaper *and* arguably cozier.
3. **What happens when someone sleeps?** Time advance is a shared resource. Does the world wait for
   the slowest player?
4. **Is the world persistent when the host is offline?** "Host-owned, play when we're both on" is a
   fishing-trip-with-a-friend. "Always there" is T3 and a different product.
5. **What happens to your dory when your friend quits mid-trip?**
6. **How many players?** 2 is meaningfully cheaper than 4.

#### 6.1.1 The shared-purse ruling (owner, 2026-08-04)

**Ruling: one business, one purse, several skippers — "for now".** Recorded here rather than in the
canon because multiplayer is not in the canon; if it ever goes there, this ruling goes with it.

**What it decides.** The wallet, the licences, the fleet and the market position all belong to **the
outfit**, not to a person. Any partner may spend from the purse and take any boat out. Progression —
licences, unlocks, reputation — accrues to the business. What stays personal is only what must:
where you are, which helm you are standing at, what is in your hands.

**Why it is the cheaper answer, and by how much.** It moves three of the seven player-scoped
singletons back to world-scope (§4) and deletes the rival-economy problem outright: workstream 2
drops XL→L, 3 drops L→M, 6 drops L→M, 8 drops M→S–M. The headline "several months" does not
collapse — workstreams 1, 4, 5, 9, 10, 11 and 12 are untouched — but the **riskiest design ambiguity
in the whole study is now closed**, and the single most expensive workstream got materially smaller.

**Why it is also the better answer for the fantasy.** It resolves the P2 tension §6.2 flags. A shared
purse is not two players on one ladder — it is **one dynasty with two people crewing it**. The
*Dory to Dynasty* arc survives completely intact because it was always the *outfit* that climbs. And
it lands squarely on **P4**: a friend is not a second protagonist, a friend is *the first crew you
ever had* — which is precisely the beat P4 says you should have to earn before you automate it.

**On "for now" — this is genuinely low-risk, with one asterisk.** Choosing shared means *not doing*
the work of splitting the wallet, not doing work you would later tear out; a future reversal defers
cost rather than wasting it. The asterisk is the **migration**: a shared-purse save has no per-player
balances to split, so reversing later means inventing them out of one pooled number — "who gets
what?" is a question with no correct answer. Content and UI built assuming one business (a single
balance readout, shared licence gating, one progression ladder) is the part that would actually need
rework. Neither is a reason to hesitate now; both are reasons to make the call deliberately if it is
ever revisited.

**Three follow-ons it opens** (not blocking — recommended defaults given, all cheap to change):

- **Can either partner spend without asking?** *Recommend yes*, with a visible ledger of who bought
  what. Anything else needs a permission UI that does not exist, and "partners" is the whole framing.
- **Can either partner take any boat?** *Recommend yes* — one purse implies one fleet, and per-boat
  ownership would quietly reintroduce the personal-property split the ruling just removed.
- **What if a partner plays while you are offline, and spends the purse?** This sharpens §6.1.4 (world
  persistence) rather than answering it. *Recommend host-owned, play-when-both-on* — the "fishing trip
  with a friend" shape, which makes the question moot.

### 6.2 Where multiplayer would genuinely *serve* the pillars

Not an argument to build it — an argument that it is not merely bolted on, should the owner want it:

- **P1 (The Sea Has Moods)** — a gale you're both reading, and one of you gets it wrong. Being
  stranded by the falling tide while your friend makes it across the St Peters sandbar is P1 and P5
  in one moment, and it is *better* with a witness.
- **P3 (Living Working Coast)** — a second real skipper is the most alive thing that could be on that
  coast.
- **P4 (Earn It, Then Automate It)** — a friend is the first "crew" you ever had.

And where it *would have* fought them: **P2 (Dory to Dynasty)** is a *long, legible ladder of personal
ownership*, and two players on one ladder was the open risk. **The shared-purse ruling closes it**
(§6.1.1): the ladder belongs to the outfit, and both partners climb it together. P2 survives intact.

---

## 7. Local co-op (T1) — why not

Cheaper on transport (zero) but it still needs workstreams 1, 2, 3, 5 and 10 — *the expensive ones* —
and then it fights the design: this is a boat game across a whole region, so split-screen halves an
already-atmospheric camera, and a shared camera tethers two players who exist to sail apart. It costs
most of what T2 costs and delivers less. If the answer is "multiplayer", the answer is T2.

---

## 8. Recommendation

1. **Do not build multiplayer now.** It is not in the canon, not on the roadmap, and CLAUDE.md rule 8
   exists for exactly this shape of idea. M1 has to answer *"is this game worth making?"* first — and
   a great single-player Hidden Harbours is the precondition for a co-op one, not the alternative to it.
2. **Do build the `InputService` intent layer** (§4.1) when the backlog reaches input polish. It is
   already specified, already owed for PC gamepad support and the mobile port, and it happens to be
   the single highest-leverage piece of multiplayer groundwork. **This is the whole "keep the option
   open" move** — no netcode, no commitment, no scope creep.
3. **Keep honouring the determinism contract** (rule 5). It is why §3 is short. Every system that
   recomputes instead of saving is a system that would never need syncing.
4. **If the itch needs scratching: time-box the §5 spike to one week**, after M1's loop is judged
   fun. It answers "is sailing together delightful?" for about 1% of the cost of finding out the
   expensive way — but run it on a throwaway branch, and **do not merge it**.
5. **Answer §6.1 before any engineering.** ✅ The shared-purse fork is **decided** (§6.1.1) — the one
   that mattered most, and it made the scope smaller. The next most valuable answer is **§6.1.2 (can
   partners be in different regions at once?)**: "no — you sail together" deletes workstream 9
   outright, and is arguably the cozier game.

### What it costs to defer

**Very little, and that is the real finding.** The determinism contract, the data-driven content
model and the Core-mediated seams are all *already* the right groundwork — a networked build would
reuse them rather than fight them. The one thing that gets more expensive with every PR is the
player-scoped singleton problem (§4), which grows roughly with the number of `GameServices` call
sites. ~~It is at 399 today. That is a slope, not a cliff.~~

**Re-measured 2026-08-14 — and this is the one paragraph the refresh was worth doing for.** In the
ten days and ~128 PRs from `b8dcebe` to `9a0fabf`, `GameServices` call sites went **399 → 535**
(+34%), across **109 → 140** files, and the player-scoped member set went **3 → 6**. That is a
measured gradient of roughly **+1 call site per PR**, and — more importantly — **the personal set
grows, it does not just get referenced more.** Every arc that gives the fisher a body to inhabit (a
transform regions can find, hands that hold things, a way in and out of a region) adds another
"there is exactly one of these".

**It is still a slope, not a cliff, and the recommendation above does not change.** But it is a
steeper slope than the first draft could see from one measurement, and the honest revision is:
*deferring costs very little per month; it is the §4.1 groundwork, not the deferral, that is doing
the work of keeping the option open.* If the option matters to the owner, workstream 1 is the thing
to fund — not a spike, and not a decision.

---

## 9. Sources

Measured from the tree at **`9a0fabf` (2026-08-14)**, not from memory — originally at `b8dcebe`
(2026-08-04): `Core/Services/GameServices.cs`, `Core/Events/EventBus.cs`, `Core/Events/GameSignals.cs`,
`Core/Save/SaveRestore.cs`, `Core/Save/SaveMigration.cs`, `Core/Shell/ShellPause.cs`,
`Core/Shell/ShellFlow.cs`, `Core/Interaction/InteractActor.cs`, `Core/Interaction/InteractResolver.cs`,
`Core/Interaction/Interactables.cs`, `Core/Services/IconLibrary.cs`,
`Core/Services/RegionDisplayNames.cs`, `Boats/ActiveBoatProbe.cs`, `Boats/BoatController.cs`,
`Player/PlayerWalkController.cs`, `Player/CarryHands.cs`, `App/RegionTravelCoordinator.cs`,
`App/CameraFollow.cs`, `World/Routines/RoutineSchedule.cs`, `Fishing/ClamDigger.cs`,
`Packages/manifest.json`, `ProjectSettings/EditorBuildSettings.asset`, `.github/workflows/ci.yml`,
[`architecture/tech-architecture.md`](../architecture/tech-architecture.md) §1/§3/§4/§6/§9/§10,
[`vision-and-pillars.md`](../vision-and-pillars.md), [`roadmap.md`](../roadmap.md),
[`design/fleet-flotation.md`](fleet-flotation.md), [`authoring-scenes.md`](../authoring-scenes.md),
ADRs [0004](../adr/0004-perspective-and-scene-strategy.md), [0005](../adr/0005-pc-first-target.md),
[0008](../adr/0008-save-schema-and-versioning.md), [0011](../adr/0011-committed-hand-authored-scenes.md),
[0020](../adr/0020-world-placed-object-persistence.md),
[0025](../adr/0025-ui-rig-runtime-rendering.md), [0030](../adr/0030-per-hull-instrument-ownership.md).

---

## 10. Refresh log — what moved between `b8dcebe` and `9a0fabf`

Re-measured 2026-08-14 on the owner's finish-and-merge ruling. The study's authority was that it
measured the real tree; ten days and ~128 PRs later, this is what a re-measure of every reproducible
claim found. **Where a verdict moved, it is recorded as moved — the old conclusions have not been
kept over the new numbers.**

### 10.1 The reproducible counts

| Measurement | 2026-08-04 (`b8dcebe`) | 2026-08-14 (`9a0fabf`) | Δ |
|---|---|---|---|
| `GameServices.` call sites / files | 399 / 109 | **535 / 140** | +34% / +28% |
| `EventBus.` call sites / files | 217 / 60 | **261 / 71** | +20% / +18% |
| `Keyboard/Mouse/Gamepad.current` sites / files | 55 / 31 | **73 / 37** | +33% / +19% |
| Non-test C#: lines / files | ~128.6k / 565 | **~200.1k / 799** | +56% / +41% |
| All `.cs` under `Assets/` | 957 | **1,359** | +42% |
| MonoBehaviours (non-test) | 142 | **177** | +25% |
| Player-scoped `GameServices` members | 3 (+1 conditional) | **6 (+2 conditional)** | **doubled** |
| Save schema version | v2 *(as drafted — in fact already higher)* | **v10** | — |
| Committed region scenes in build settings | 1 | **4** | +3 |

**Scope, so these reproduce.** The three call-site counts are `grep -rn … Assets/_Project/Code
--include="*.cs"`; the file/line/MonoBehaviour counts are over **all of `Assets/`**, with
`Assets/Tests/` excluded for the "non-test" rows (the exact commands are in the PR body). The
2026-08-04 column was **re-derived** from the `b8dcebe` tree with those same commands — it
reproduced 399/109, 217/60 and 55/31 exactly — so the two columns are comparable rather than merely
quoted from the old draft.

### 10.2 Claims that moved

| Claim (2026-08-04) | Verdict now |
|---|---|
| "The `InputService` was never built" (§4.1) | **STILL TRUE, and worse.** No `InputService`, `IInputService` or `MoveIntent`/`ThrottleIntent` exists. Direct device reads rose 55 → 73. Recommendation 2 (§8) is unchanged and more owed. |
| "Three player-scoped singletons, and that is the complete set" (§4) | **MOVED — it is six, plus two conditional.** `PlayerTransform`, `Hands`, `CatchHands` landed (#512/#515/#525/#528/#529); `PendingArrivalKey` joins `CurrentRegionId` as conditional. The shared purse cannot absorb them: they are about a body, not the books. Workstream 2 is bigger than the ruling left it. |
| "`ActiveBoat` is a single slot, `ActiveBoatProbe.cs:48`" (§4) | **STILL TRUE, still line 48**, still `OnEnable() => GameServices.ActiveBoat = this`. But the *shape* is no longer unique to it — `PlayerTransform`, `Hands` and `CatchHands` are four publish-on-enable single slots in total, not one. |
| "`IGameClock.SeekTo` is what a joining client needs, `SaveRestore.cs:72`" (§3) | **STILL TRUE, still line 72** — `clock.SeekTo(data.GameTimeSeconds)`. |
| "The sea is nearly free to network" (§3) | **STRENGTHENED.** The village day joined the free list: `RoutineSchedule.cs:10` makes where everybody is a pure function of the clock, and says the join case out loud. |
| "Nine Mile Creek and St Peters are uncommitted; generate them before you can build" (§5) | **NOW FALSE.** All four regions are committed and in `EditorBuildSettings.asset`. Spike step 5 got cheaper. |
| "The save is one blob, schema v2" (§4) | **CORRECTED to v10** (`SaveMigration.cs:16`). Helps rather than hurts: eight migrated bumps since make workstream 8 proven machinery. |
| "The EventBus has no sender" (§4) | **STILL TRUE** — no payload in `GameSignals.cs` carries an identity, and four more identity-free signals landed. |
| "Pause is global by design" (§4) | **STILL TRUE** — `ShellPause.cs:7` still calls it the project's ONE pause path. |
| "Zero networking capability; `com.unity.multiplayer.center` is the recommender window" (§5) | **STILL TRUE.** Still `1.0.1`, still the only match in `manifest.json`. |
| "Multiplayer is out of canon" (header) | **STILL TRUE** — `tech-architecture.md:354` still files networking as out of scope for now. |
| "CI is not an automated gate and needs `UNITY_EMAIL`/`UNITY_PASSWORD`" (§6 WS12) | **STILL TRUE** (`.github/workflows/ci.yml`). |
| `IconRegistry` / `RegionDisplayNames` listed as `GameServices` members (§4) | **CITATION ERROR, corrected.** They are separate Core statics. World-scoped either way — no verdict changes. |
| "957 files of non-test C#" (header) | **LABELLING ERROR, corrected.** 957 was the all-`.cs` count including tests; non-test was 565. Both are now stated separately. |
| `Fishing.FishSchoolModel` is the hashed pure function (§3) | **SHARPENED.** `FishSchoolMath` is the pure function; `FishSchoolModel`/`IFishSchools` are the seam over it. Both exist; the claim holds. |

### 10.3 The one genuinely new finding

**§4.2 — the interact verb (#503) shipped the actor-as-a-value shape this study says is missing**,
in Core, for testability and rule-4 reasons that had nothing to do with multiplayer. It is the first
place in the project where "the player" is a parameter rather than a singleton, and it is a landed,
reviewed precedent for workstream 1 to copy. It does not change any cost band, but it moves
workstream 1 from *"design a seam"* to *"generalise one that exists"*.

### 10.4 What did **not** change

The recommendation (§8). The shared-purse ruling and its re-costing (§6.1.1) — re-read against the
new tree, nothing in it turns on a number that moved, so it stands verbatim as the owner made it.
The two-sentence answer (§1): ~1 week for the spike, several months for real co-op, still competing
with an unfinished M1. And the headline conclusion — **don't build it now** — which the steeper
measured slope in §8 supports rather than undermines.
