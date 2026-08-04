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
> - **Date:** 2026-08-04
> - **Measured against:** commit `b8dcebe`, Unity 6000.5.0f1, ~128.6k lines of non-test C# across
>   957 files and 142 MonoBehaviours.

---

## 1. The two-sentence answer

**Two people sailing boats on the same sea, seeing each other, sharing one tide — is roughly a
week**, and it is a genuinely cheap spike because the sea itself needs almost no networking (§3).
**Two people actually *playing Hidden Harbours* together — fishing, selling, owning boats, saving —
is a multi-month rewrite of the seams the whole project has spent 400+ PRs deliberately building
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
- **where the fish are** (`Fishing.FishSchoolModel` is a hashed pure function — ADR 0025 S3a)
- the wave field, hull rocking, wakes, buoy motion (ADR 0018 — one shared derivation)
- dormant NPC positions, the ambient fleet
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

`Core/Services/GameServices.cs` is a **static service locator** — read at **399 sites across 109
files**. `Core/Events/EventBus.cs` is a **static, process-wide** pub/sub — **217 sites**. Between
them they are the project's nervous system, and they are built on the assumption that there is
exactly one of everything.

The useful discovery is that the members split cleanly in two:

| **World-scoped — fine as globals, no change needed** | **Player-scoped — must become per-player** |
|---|---|
| `Clock`, `Environment` | `Wallet` — *whose* money |
| `TidalTerrain`, `CurrentRegionBounds` | `Licenses` — *whose* cod licence |
| `Config`, `WaveField`, `WaveFetch`, all tunables | `ActiveBoat` — ⚠ **one slot, one boat** |
| `FishSchools` | `HelmControl` — whose hand is on the tiller |
| `CatchFactory`, `AudioMix`, `IconRegistry`, `RegionDisplayNames` | `HelmInstruments` — whose dash |
| | `Save` — one blob, one player |
| | `CurrentRegionId` — players can be in different regions |

That right-hand column is the project. Seven singletons, but they are reached from the Player, Boats,
Fishing, Economy, UI and World modules, and each has a documented "there is exactly ONE of these"
invariant that a second player breaks:

- **`ActiveBoat` is a single slot** (`Boats/ActiveBoatProbe.cs:48` — `OnEnable() => GameServices.ActiveBoat = this`).
  Put two crewed boats in a scene and **whichever enables last wins**; the depth sounder, fish finder,
  compass and HUD then all read the *other* player's boat. This one bites in the spike, not just the
  full build (§5).
- **Pause is global by design.** `IGameClock.IsPaused` is documented as *"the project's ONE pause
  path, no second clock"* (`tech-architecture.md` §6) and the shell stops the world through it. In
  co-op you cannot stop the sea because your friend opened a menu — so the shell, the title flow,
  `ShellPause`, `WorldInputBlocked` and the settings sheet all need rework.
- **The EventBus has no sender.** `FishCaught`, `MoneyChanged`, `CatchSold`, `BoatPurchased`,
  `TrapPlaced` all mean *"the player did this"*. Every one needs a player identity, and every
  subscriber needs to decide whether it cares about *this* player or *any* player.
- **The save is one blob** (ADR 0008, schema v2). It becomes a host-owned world save plus per-player
  records — schema v3 with a migration, and `SaveService.WritesAllowed` / the quit-to-title teardown
  both change meaning when four people share a world.

### 4.1 The input layer that was specified but never built

`tech-architecture.md` §3 and §9 promise an `InputService` translating raw input into intents
(`MoveIntent`, `ThrottleIntent`, …) — *"platform-swappable"*, so that desktop/gamepad *"is a new
input map, not a rewrite"*.

**It does not exist.** `Keyboard.current` / `Mouse.current` / `Gamepad.current` are read directly at
**55 sites across 31 files** — `PlayerWalkController`, `DevBoatInput`, `DevFishingInput`,
`ClamDigger`, `WorldInteractor`, `HelmOverlayHost`, the shop screens, the title screen.

This matters more than it looks. **An intent seam is precisely the socket a remote player plugs
into**: a networked player is just a boat whose intents arrive from a wire instead of a keyboard. With
no seam, there is nowhere to inject them. Building it is unavoidable for co-op — and it is *already
owed* for the PC-first gamepad support ADR 0005 promises, and for the eventual mobile port. **It is
the one piece of multiplayer prep that is worth doing whether or not multiplayer ever happens.**

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
| 5 | Build for Windows and get it to the friend. `Greybox.unity` (Coddle Cove) is committed and in build settings, so a build is possible today. ⚠ Nine Mile Creek and St Peters are **builder-generated and uncommitted** (`docs/authoring-scenes.md` §1) — they must be generated in-editor and added to build settings first. |

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
| 1 | **Input → intents** | Build the `InputService` §4.1 already owes. 31 files, 55 sites. Remote players become intent sources. | **M** — *owed anyway for gamepad* |
| 2 | **Player identity split** | The 7 player-scoped singletons → a per-player context. Thread "which player" through the Player, Boats, Fishing, Economy, UI and World modules. **The big one.** | **XL** |
| 3 | **EventBus player identity** | Sender on every player-scoped signal; every subscriber decides *this player* vs *any player*. 217 sites to audit. | **L** |
| 4 | **Transport + ownership** | Netcode for GameObjects, `NetworkObject` on boats/players/traps, client-authoritative movement with host reconciliation. **State-sync, never lockstep** (§3). | **L** |
| 5 | **Shell, pause & title rework** | The one-pause-path invariant dies (§4). Menus must not stop the world. Join/leave/host-migration flows are new UI that does not exist. | **L** |
| 6 | **Economy authority** | Host-authoritative market ticks and transaction ordering. *Two players selling into one market that moves with supply is a genuinely great co-op feature* — and it needs real work to not be exploitable. | **L** |
| 7 | **Fishing authority** | *Where* fish are is free (deterministic); *whether you hooked one* is not — the fight is explicitly RNG-injected (§3). Needs an authoritative roll. | **M** |
| 8 | **Save schema v3** | Host-owned world save + per-player records, with a migration off v2 (ADR 0008/0020). Never strand a save. | **M** |
| 9 | **Per-player region streaming** | Two players in different regions = two additively-loaded scenes at once. ADR 0004's scene model assumes one active region. | **M–L** |
| 10 | **Camera** | `App/CameraFollow.cs` follows one player and clamps to `CurrentRegionBounds`. | **S** |
| 11 | **Content rework** | NPC routines, dialogue and onboarding all address "the player". Ginny teaches *one* fisher. Quest and onboarding flags are per-world today. | **M** |
| 12 | **Test & CI** | Multiplayer PlayMode tests, two-client harness. ⚠ CI is currently **not** an automated gate (CLAUDE.md rule 10) and needs `UNITY_EMAIL`/`UNITY_PASSWORD` secrets to pass at all. | **M** |

**Realistically: several months at this project's pace**, and — the part that is easy to miss — **it
competes directly with finishing M1**. M1 is the *"is this game worth making?"* gate
(`roadmap.md` §M1) and it is not done. Multiplayer would also **invalidate some already-shipped M1
work**, in the same way the owner's buy-and-repair ruling already flagged VS-21 for rework.

### 6.1 Questions only the owner can answer

These are design, not engineering, and **the engineering cannot start without them** — each one
changes workstream 2 and 6 substantially:

1. **Shared purse or separate?** One boat, one business, two skippers — or two rival operations in
   one harbour? This is *the* fork; it decides the shape of the economy work.
2. **Can players be in different regions at once?** "Yes" costs workstream 9 and complicates
   everything. "No — you sail together" is dramatically cheaper *and* arguably cozier.
3. **What happens when someone sleeps?** Time advance is a shared resource. Does the world wait for
   the slowest player?
4. **Is the world persistent when the host is offline?** "Host-owned, play when we're both on" is a
   fishing-trip-with-a-friend. "Always there" is T3 and a different product.
5. **What happens to your dory when your friend quits mid-trip?**
6. **How many players?** 2 is meaningfully cheaper than 4.

### 6.2 Where multiplayer would genuinely *serve* the pillars

Not an argument to build it — an argument that it is not merely bolted on, should the owner want it:

- **P1 (The Sea Has Moods)** — a gale you're both reading, and one of you gets it wrong. Being
  stranded by the falling tide while your friend makes it across the St Peters sandbar is P1 and P5
  in one moment, and it is *better* with a witness.
- **P3 (Living Working Coast)** — a second real skipper is the most alive thing that could be on that
  coast.
- **P4 (Earn It, Then Automate It)** — a friend is the first "crew" you ever had.

And where it fights them: **P2 (Dory to Dynasty)** is a *long, legible ladder of personal
ownership*. Two players on one ladder is the shared-purse question (§6.1.1) and it is not obvious the
fantasy survives it.

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
5. **Answer §6.1 before any engineering.** Especially the shared-purse question.

### What it costs to defer

**Very little, and that is the real finding.** The determinism contract, the data-driven content
model and the Core-mediated seams are all *already* the right groundwork — a networked build would
reuse them rather than fight them. The one thing that gets more expensive with every PR is the
player-scoped singleton problem (§4), which grows roughly with the number of `GameServices` call
sites. It is at 399 today. That is a slope, not a cliff.

---

## 9. Sources

Measured from the tree at `b8dcebe`, not from memory: `Core/Services/GameServices.cs`,
`Core/Events/EventBus.cs`, `Core/Events/GameSignals.cs`, `Core/Save/SaveRestore.cs`,
`Boats/ActiveBoatProbe.cs`, `Boats/BoatController.cs`, `Player/PlayerWalkController.cs`,
`Packages/manifest.json`, `ProjectSettings/EditorBuildSettings.asset`, `.github/workflows/ci.yml`,
[`architecture/tech-architecture.md`](../architecture/tech-architecture.md) §1/§3/§4/§6/§9/§10,
[`vision-and-pillars.md`](../vision-and-pillars.md), [`roadmap.md`](../roadmap.md),
ADRs [0004](../adr/0004-perspective-and-scene-strategy.md), [0005](../adr/0005-pc-first-target.md),
[0008](../adr/0008-save-schema-and-versioning.md), [0020](../adr/0020-world-placed-object-persistence.md),
[0025](../adr/0025-ui-rig-runtime-rendering.md).
