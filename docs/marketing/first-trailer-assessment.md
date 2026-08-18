# Hidden Harbours — First Trailer: Assessment

> **Status:** Assessment / recommendation. Not yet ratified. Written 2026-08-18, audited against
> `main` @ `183edb2`.
> **Canon:** [`../vision-and-pillars.md`](../vision-and-pillars.md) wins on any conflict, then
> [`../../CLAUDE.md`](../../CLAUDE.md), then [`../roadmap.md`](../roadmap.md), then this file.
> **What this is:** an honest read of what we can film today, what blocks a trailer, what the
> trailer should contain, and when to shoot it.
> **What this is not:** a marketing plan. It covers one artifact — the first trailer — and the
> minimum around it that stops that artifact being wasted.

---

## 1. The verdict, up front

**We should not shoot a trailer yet. We should start building the things a trailer needs
immediately** — because two of them (audio and the capture kit) are on the M1 critical path
anyway, and one of them (audio) has the longest lead time in the project.

The good news is larger than expected. **The hard part of a trailer for this game is already
built.** The deterministic tide, the displaced water, the tide-aware shoreline, the boat force
model, twenty-three hulls lying at anchor, a village that keeps a day, the clam dig, the
buy-and-repair dory — that is a genuinely strong sixty seconds of footage sitting in the repo
right now, and most cozy-fishing competitors would kill for the tide alone.

The bad news is narrow but total:

- **There is not one second of real audio in this project.** Zero `.wav`, `.ogg` or `.mp3` files.
  Every sound in the build today is generated procedurally at boot as a placeholder
  (`ProceduralAudio.cs`), and the music bus is wired but empty — `AudioDirector.cs:89`, *"music bus
  (reserved — no stem yet)"*. A trailer is half sound. This is the blocker.
- **There is no way to film the game.** No free camera, no HUD toggle, no time/tide lock, no
  offline high-res capture. Today you could only screen-record a play session at whatever the sim
  decided to do that minute.
- **There is no wordmark.** No logo, no title card, no capsule art, no Steam page.
- **The village isn't populated yet.** The one pillar a trailer most needs to *show* — P3, a living
  working coast — is the biggest unbuilt item in M1 (`plan-to-m1.md` §7.1).

So: **capture kit and audio start now; the shoot happens when M1 is content-complete, timed to go
up with the Steam page and the external playtest.** Detail in §7.

---

## 2. What the trailer is actually for

Worth being blunt, because it changes every decision below. A first trailer is not for applause.
For a PC indie it has exactly one job: **convert a stranger into a wishlist.** Everything else —
press pickup, Discord signups, playtester recruitment — follows from the same sixty seconds doing
that one job well.

Three consequences that constrain the cut:

1. **Steam autoplays trailers muted.** The first five or six seconds must land with no sound at
   all. That rules out opening on a music sting or a logo, and it rules in opening on a *visual
   idea* — which, luckily, is exactly what we have.
2. **The viewer does not know what genre this is.** They have seen four fishing games this month.
   The opening has to answer "why is this one different" before it answers anything else.
3. **Wishlists compound.** A wishlist earned eighteen months before launch is worth as much as one
   earned the week before, and it accrues interest in the form of Steam's own visibility
   algorithms. This is the argument *for* doing it earlier rather than later — and it is the
   argument that has to be balanced against §7's "don't announce a game nobody can play yet."

---

## 3. The hook — what makes this trailer not-another-fishing-game

The cozy fishing genre is crowded and visually convergent: a small boat, nice water, a fish on a
line, a warm town. A trailer that opens on any of those reads as the fourth such trailer this week
and gets scrolled past.

**Hidden Harbours has one idea nobody else has, and it is visual, immediate, and needs no
explanation: the sea goes away and you walk on it.**

The project's own planning already knows this. `plan-to-m1.md` §2 says it outright — *"the tide is
M1's engine, and the tide-gated crossing to the mainland is the best idea in this design."* The
art bible §2.1 calls the tide-aware moving shoreline *"the single most important art expression of
P1."* The trailer should take that at face value and build the whole cut around it.

**The spine of the trailer is the tide.** It opens on the tide, it structures its middle on what
the tide lets you do, and it closes on the tide. Everything else — the clams, the village, the
dory, the fleet — hangs off that spine.

Two supporting differentiators, both already built, both worth screen time:

- **True metric scale (P2).** PPU is locked at 32 and boats are authored at real metres, so a
  tanker genuinely dwarfs a dory *on screen, in the same shot, with no cheating*. Twenty-three
  hulls already lie at anchor off Nine Mile Creek (`c139a92`). A single slow pull-back from the
  dory to that anchored fleet sells the entire eight-tier ladder in three seconds, and no amount of
  text could do it as well.
- **The earned boat (P4).** You do not inherit a dory. You dig clams by hand, sell them, buy a
  wreck, and pay to have her put right. That is a better story than "here is your boat" and it is
  already implemented (`DamagedDoryOffer`, `RepairLedger`).

---

## 4. What we can film today — the honest inventory

Audited against the working tree. Each row lists the systems that produce the shot, so whoever
builds the capture kit knows what they are pointing at.

| Beat | Filmable now? | What produces it |
|---|---|---|
| **Tide falling, shoreline retreating, seabed baring** | **Yes** | `TideModel` (deterministic semidiurnal), `TidalExposure`, `PaintedTidalTerrain`, `TidalWalkability` (ADR 0009/0014), tide-aware shoreline (ADR 0012), displaced water surface (ADR 0023) |
| **Walking out onto bared seabed** | **Yes** | `TidalWalkability`; St Peters flats authored |
| **The sandbar crossing to the mainland** | **Partly — verify** | The seam is done (`plan-to-m1.md` §6); whether the sandbar is *authored end-to-end* as a walkable route between the two committed scenes needs checking before it goes in a shot list |
| **Clam dig — the two squirting holes, shovel, bucket filling** | **Yes** | `ClamDig`, `ClamDigger`, `ClamSpot`, `Data/Gear/Shovel.asset`, `ClamBucket`, carried-item rig (`8a595c1`, `cdb5ffc`) |
| **Village life — people, speech bubbles, routines, lit windows at dusk** | **Partly** | Routines are a pure function of the clock (`1d55b4c`), anchored speech bubbles (`77d8403`), `DayNightController`, 2D lights (ADR 0016), grass wind + footstep trails. **But the M1 cast and buildings are not authored yet** — see §5 |
| **The derelict dory, the purchase, the repair** | **Yes** | `DamagedDoryOffer`, `RepairLedger`, `Shipwright`, shipyard sprites |
| **She swims — wind pushing, tide setting, wake, spray** | **Yes, and this is the strongest single shot** | Boat force model (owner-playtested repeatedly), boat wake, `SprayEmitter`, hull depth shear (ADR 0033) |
| **The ladder — dory to the anchored fleet** | **Yes** | 23 hulls at anchor (`c139a92`), 78 boat defs, 8 sailable, true metric scale at PPU 32 |
| **Traps and pots, deck work** | **Yes** | Soak-and-haul loop, deck gear, deck occupancy |
| **Selling — the market, prices moving** | **Yes** | Supply/demand market, two channels, sell screen |
| **Instruments — chartplotter, radar, sounder, compass** | **Yes** | `Code/UI/Draw/*`, per-hull instrument ownership (ADR 0030). Proof renders already exist in `docs/art/proofs/` |
| **Weather atmosphere — rain, sea mist, spray, palette shift** | **Yes, as mood only** | `RainEmitter`, `SeaMistEmitter`, `WeatherWaterPalette` (ADR 0017) |
| **Danger as an *event* — grounding, capsize, stranding, rescue** | **No — M2** | Deferred by the roadmap. See §5 |
| **Storms, fog banks, weather fronts** | **No — M2** | Deferred |
| **Rot / frost on the catch** | **No** | Arithmetic landed; visual and gating not wired (`plan-to-m1.md` §7.3) |
| **Title screen / menu** | **No** | No shell exists at all (`plan-to-m1.md` §7.8) |

**The summary:** roughly 80% of a strong announce trailer is already standing. What is missing is
not footage — it is the ability to *capture* footage, anything to hear over it, and the last
content beat that makes the village read as inhabited.

---

## 5. What we cannot show, and what that costs

Three honest gaps. Two are cheap to work around; one changes the shape of the cut.

**P5 — "Cozy, but with Teeth" — has almost nothing filmable.** Grounding, capsize, stranding,
rescue, storms and fog are all M2 by the roadmap's explicit deferral. We have rain, mist, spray,
night, a rising wind and a weather-driven water palette — enough to make the sea look like it
*could* hurt you, not enough to show it doing so. **The first trailer therefore cashes P5 as
atmosphere, not as event.** That is a real weakness and it should be accepted deliberately rather
than papered over: it means the first trailer is a *cozy* trailer with an undertow, and the
teeth get their own trailer at M2. Trying to fake danger we haven't built would be worse than
omitting it — the footage would not match the game a playtester downloads that same week.

**The village is not yet a village.** M1's Definition of Done wants the aunt's house, a
schoolhouse, a general store, two or three more homes, and four to six named people with faces and
opinions. Today the island is a cottage, Aunt Ginny, Ned's letter and a dock. Until that lands
there is no P3 footage worth the name, and P3 — a place that feels inhabited — is precisely what
sells a cozy game. **This is the gating content item for the shoot, and it is owner-serialized
scene-authoring work** (ADR 0019), so it is a schedule item, not a sprint item.

**There is no shell.** No title screen means the trailer's title card has to be composited in the
edit rather than filmed. That is normal practice and costs nothing — but it does mean the wordmark
has to exist as artwork, and it doesn't.

---

## 6. What's needed — the blockers, ranked

Ranked by lead time × severity. The first two should start now regardless of when we shoot.

### B1 · Audio — critical path, longest lead, start this week

Zero audio assets exist. The project's own risk register already flags this (`plan-to-m1.md` §11,
*"Audio lead time — zero assets; a canon-sacred wind tell can't be conjured in a sprint"*) and the
route puts audio sourcing in Wave 0. **A trailer makes it urgent rather than merely important,
because a trailer cannot be rescued in the edit by good visuals.** Silent or placeholder-scored
footage reads as pre-alpha to every viewer, fairly or not.

What the trailer specifically needs, beyond what M1 needs anyway:

- **A theme.** Folk/maritime, sparse and warm — the bible §8.3 specifies guitar, fiddle,
  concertina, low drone, occasional wordless voice, Maritime/Newfoundland DNA without pastiche.
  **Commission the game's actual main theme and cut the trailer to it**, rather than licensing
  library music. The trailer should teach the ear what the game sounds like; a stock track teaches
  the wrong thing and has to be un-taught later.
- **A trailer edit of that theme** — roughly 75 seconds with a deliberate build and one clear lift
  where the fleet reveal lands. Ask the composer for a trailer cut alongside the loop; it is far
  cheaper commissioned together than reconstructed later.
- **Rights that cover marketing use**, in perpetuity, including third-party channels. Worth stating
  explicitly in the commission — a licence that covers in-game use but not YouTube/Steam is a
  common and expensive trap.
- **A real SFX and ambience pass for the shots we film**: wind bed, calm-sea wash, gulls, hull
  slap, shovel into wet sand, a clam dropping into the pail, oar stroke, outboard putter, rain on
  water, footsteps on decking vs. grass. These slot straight into `AudioDirector`'s existing
  serialized fields — the manifest already documents the swap and no code changes are needed.

**Owner:** `audio` + an external composer. **Decision needed from the owner:** commission vs.
library, and budget. This is decision D4 in the M1 route and it is now the schedule's long pole.

### B2 · The capture kit — blocks all filming, ~1–2 weeks

Nothing exists today. `DevFastTide` and the clock are the entire toolkit. Without this we cannot
shoot repeatable, clean, high-resolution footage — and "repeatable" is the operative word, because
a trailer shot is never right the first time.

**This project has an unusual advantage worth exploiting deliberately: the simulation is a pure
function of `(worldSeed, gameTime)`.** Tide, wind and weather are recomputed, never saved. That
means a given seed and timestamp reproduces a given sea *exactly* — so every shot is re-shootable
after a lighting tweak, an art fix, or a colour-grade change, months later, frame for frame. Most
games cannot do this. The capture kit should be built to make it explicit.

Requirements:

- **Detached free camera** — fly, orbit, look-at-target, and smooth dolly along an authored path.
  Framing a trailer through a follow-cam is not possible.
- **Scene state lock** — pin `worldSeed`, `gameTime`, tide phase, wind vector and weather, and
  restore that exact state on demand. This is the re-shootability feature.
- **Time-lapse capture** — decouple render rate from sim rate so the tide-fall shot can compress
  six hours of sim into eight seconds of footage smoothly, without the stutter of a naive
  fast-forward.
- **HUD toggle** — hide everything, or show one element in isolation. Most trailer shots want no
  HUD; the tide-gauge shot wants only the tide gauge.
- **Offline high-res capture** — render at 4K, fixed 60fps, writing frames to disk rather than
  real-time recording, so frame drops and stutters cannot enter the master.
- **A shot manifest** — capture settings saved as an asset per shot, so a shot list is data and a
  re-shoot is a button.

**Owner:** `tools-editor` (+ `art-pipeline` for the render path). **Worth noting this pays for
itself outside marketing**: the same kit makes owner review, art proofs, playtest bug repro and
Steam screenshots all dramatically cheaper. It is not a marketing-only expense.

### B3 · The village — the gating content item

Covered in §5. `plan-to-m1.md` §7.1 owns it. Nothing in the trailer plan changes its scope; it just
becomes the thing the shoot date is pinned to.

### B4 · Branding — wordmark and key art

Does not exist. Needed before a title card can be composited:

- **A wordmark/logotype.** The vision board already carries a usable palette — deep sea `#0e1f2c`,
  slate `#2b4a5e`, teal `#6fa39c`, fog `#c7d2d4`, parchment `#ece3cf`, buoy orange `#d65a3a`, buoy
  yellow `#e0a83b` — and a serif display face. That is a defensible starting point rather than a
  blank page.
- **The title card treatment.** §7.8 of the M1 plan already has the right instinct and it is free:
  *"frame the dory at a mooring at dawn from an in-game camera and put the wordmark over it."*
  Do exactly that. No hand-painted key art before the M1 GO/POLISH/PIVOT verdict.
- **Steam capsule art** is a separate and non-trivial commission — capsules are the single
  highest-ROI piece of marketing art an indie buys, because they are what people actually click.
  It is needed for the page, not the trailer, but it shares the same commission and should be
  scoped with it.

**Owner:** external illustrator + `art-director`. **Decision needed:** commission scope and budget.

### B5 · The shell — title, New Game, settings, pause

Already specified in `plan-to-m1.md` §7.8 and already required for the external playtest. Not a
trailer blocker in itself (the title card is composited), but the trailer, the page and the
playtest all land in the same week, and a build with no main menu undercuts all three.

### B6 · The Steam page — the trailer is one item of six

A trailer with nowhere to convert is a wasted asset. The page needs the trailer *plus* capsule art
(main, small, header, library), at least five screenshots pulled from the same capture session,
a short description, a long description, tags and genre. **Budget the screenshots into the shoot** —
they are nearly free while the capture kit is loaded and expensive to come back for.

---

## 7. When to shoot — the timing recommendation

**Recommendation: shoot when M1 is content-complete, immediately before the external playtest, and
put the trailer and the Steam page up together as the playtest opens.**

The reasoning:

- **Not now.** The village is unbuilt, there is no audio, and no way to film. A trailer cut today
  would show an empty island scored with procedural noise, and would misrepresent the game to the
  first strangers who ever see it. First impressions are not re-runnable.
- **Not after the M1 verdict either.** Waiting for GO/POLISH/PIVOT wastes the one moment when the
  same push can do three jobs at once: earn wishlists, recruit playtesters, and give the owner an
  outside signal to weigh alongside `qa-test`'s verdict. Public reaction to the trailer is itself
  useful evidence for the go/no-go — it is a cheap read on "is this game worth making" from people
  who have no stake in the answer.
- **So: content-complete M1, before the playtest.** The build is honest, the footage matches what a
  tester downloads, and the page starts accruing wishlists during the months of M2.

**One thing to protect: do not burn Steam Next Fest on M1.** A game may only participate once, and
it works hardest when run close to launch with a demo attached. Next Fest belongs to the M2 /
Early-Access window, with the teeth trailer and a real demo. The M1 push is an announce, not a
festival.

Sequenced against the M1 route in `plan-to-m1.md` §10:

| Wave | Trailer work that rides along |
|---|---|
| **Wave 0 — decide** | Commission audio (D4). Commission wordmark + capsules. Both are long-lead and both start here. |
| **Wave 1 — numbers and tooling** | `tools-editor` builds the capture kit alongside the region tooling. |
| **Wave 2 — build the world** | Nothing. The village is the deliverable; the trailer waits on it. |
| **Wave 3 — dress and pace it** | Audio lands and is mixed. Shell lands. **Lock the shot list; do a scout shoot** — rough capture of every planned shot, cut to a temp track, purely to find what doesn't work while there is still time to fix it. |
| **Wave 4 — prove it** | **Final capture, edit, and grade.** Page copy and capsules finalised. Trailer + page go live with the playtest. |

---

## 8. What should be in it — the cut

**Format:** 70 seconds. Gameplay only, no voiceover, minimal text. 16:9 master at 4K/60, plus a
9:16 vertical cut and a 15-second silent loop for social.

Seventy is deliberate. Announce trailers that run past ninety seconds shed viewers steadily and the
back half is watched by almost nobody; under sixty and the ladder reveal has no room to breathe.

### The beats

**0:00 – 0:09 · The sea leaves** *(no logo, no title, no music sting — this must work muted)*

Locked camera on the St Peters flats at high water. Time-lapse: the water withdraws, the shoreline
walks backwards, wet sand and weed and tide-pools come up out of it, and a sandbar rises and joins
two pieces of land. A figure walks out onto ground that was sea nine seconds ago.

*This is the whole pitch, delivered before the viewer has decided whether to keep watching. It
needs no sound and no caption. Nothing else in the cut is allowed to be more interesting than this,
which is why it goes first.*

**0:09 – 0:20 · Two hands and a tide table**

Ground level now, close and tactile. The two squirting holes in the wet sand. The shovel. A clam
into the pail, and the pail slowly filling. Cut wide once: one small figure alone on an enormous
bared seabed, the sea a long way off.

*Establishes that you start with nothing and that the work is done by hand — P4's foundation, and
the tonal promise that this is a slow, warm game.*

**0:20 – 0:32 · The island keeps a day**

Dusk falling on the village. Windows warming one by one. People walking their routines, stopping to
talk, a speech bubble or two. Grass moving in the wind. The store, the school, the aunt's door.

*P3. This is the section that most needs the village to be finished, and the section that decides
whether a cozy audience feels invited in.*

**0:32 – 0:46 · The next rung**

The crossing at low water — the walk to the mainland with a bucket. Nine Mile Creek's wharf. And
there, hauled out on the hard where you cannot miss her: **the derelict dory**. Hold on her a beat
longer than feels comfortable. Coin changing hands. Work on the hull.

Then she goes in, and **she swims** — the force model doing its thing, wind on the bow, wake behind
her, spray, the tide setting her sideways as she turns.

*P2's first rung and the emotional peak of the slice. The held beat on the wreck is doing real
work: it is the "visible before it is reachable" idea that the whole M1 ladder is built on, and it
reads instantly on screen.*

**0:46 – 0:58 · How far this goes**

The music lifts. Escalating cuts, each held about a second and a half: the dory under oars → the
outboard on her transom → a skiff → traps coming up over a gunwale → a lobster boat working → and
then a slow pull-back off the water to reveal **the fleet at anchor off the creek**, hull after
hull, the dory tiny among them. Hold.

*P2, and the single best-value shot in the project. Twenty-three hulls at true metric scale already
exist. Everything before this is a small cozy game; this shot says the small cozy game is the first
rung of something much larger, and it does it without a word of text.*

**0:58 – 1:04 · The undertow**

Hard turn. Night, or near it. Rain on black water, the palette gone cold, mist coming in, wind
audibly up, the boat small and a long way from a light on shore.

*P5 as atmosphere. Six seconds is honest — it is enough to promise that the sea has moods without
claiming teeth we have not built. It also sets up the M2 trailer, which is where the teeth belong.*

**1:04 – 1:10 · Title**

Cut to calm. The dory at her mooring at dawn, in-game camera, held still. Wordmark up over it.
Then, small and last: *Wishlist on Steam.*

*One card, one line of text, no feature bullets, no review quotes we don't have.*

### The rules the cut has to obey

- **Gameplay only.** No pre-rendered cinematic, no footage from an editor view, no capability the
  build doesn't have. A trailer that oversells is repaid with interest at playtest.
- **HUD hidden by default**, with one deliberate exception: a brief, clean look at the tide gauge
  during the opening, because there the instrument *is* the pitch.
- **Cut to the music, not the reverse.** Lock the audio edit first, then hang picture on it.
- **The first frame is the poster frame** — Steam displays it before playback. It must be a
  composed, beautiful still in its own right.
- **No text walls.** If a beat needs a caption to be understood, the beat is wrong.
- **No fake UI, no fake weather, no speed-ramped water.** The sim is deterministic; shoot the real
  thing at the real seed and let it be true.

---

## 9. Decisions the owner needs to make

Everything above is blocked on five calls, in rough order of lead time:

1. **Audio: commission or library?** Recommendation: **commission the theme.** It is the longest
   lead item, it is already required for M1, and the trailer wants the game's real voice rather
   than a rented one. Needs a budget.
2. **Branding: who makes the wordmark and the capsules?** Recommendation: external illustrator, one
   commission covering both, scoped in Wave 0 so it isn't blocking in Wave 4.
3. **Shoot timing: confirm "content-complete M1, before the playtest."** If the answer is instead
   "as early as possible," say so now — it changes the cut (it would drop §8's village beat) and it
   changes the honesty of what we show.
4. **Is a public announce wanted at all before the M1 verdict?** A reasonable alternative is to keep
   M1 entirely private and make the first public trailer an M2 one, with teeth and a demo. That
   trades away six-plus months of wishlist accrual for a stronger first impression. Recommendation:
   announce at M1 — but this is genuinely the owner's call and both answers are defensible.
5. **Editor: in-house or contracted?** Trailer editing is a real craft and a badly cut trailer
   wastes good footage. Recommendation: contract an editor who works on game trailers, hand them
   the captured shots and the shot manifest, and keep the capture in-house where the deterministic
   re-shoot lives.

---

## 10. What not to do

- **Don't announce with a teaser that shows no gameplay.** For an unknown indie, a logo-and-mood
  teaser converts almost nobody and spends the announcement.
- **Don't cut the trailer to library music and swap the score in later.** The edit will have been
  built to the wrong rhythm and the swap never quite fits.
- **Don't show storms, grounding, or rescue.** They are M2. Showing them now is a promise the
  playtest build will immediately break.
- **Don't spend hand-painted key art budget before the M1 verdict** — §7.8 of the M1 plan is right,
  and an in-game title frame is both free and more honest.
- **Don't burn Steam Next Fest on the announce.** Once per game; save it for the demo.
- **Don't let trailer work pull agents off the village.** The trailer is downstream of M1 being
  finished. `plan-to-m1.md` §11 already names this failure mode — *"the verbs pull focus"* — and a
  trailer is an even more seductive distraction than the verbs, because it is visible and fun. The
  only trailer work that belongs in Waves 0–1 is the capture kit and the audio commission, both of
  which M1 needs regardless.

---

## 11. Summary of the ask

| Item | Owner | When | Blocking? |
|---|---|---|---|
| Commission the theme + trailer cut + SFX pass | `audio` + external | **Wave 0 — now** | **Yes, hard** |
| Commission wordmark + Steam capsules | `art-director` + external | **Wave 0 — now** | Yes |
| Build the capture kit (free cam, state lock, time-lapse, HUD toggle, 4K offline capture, shot manifest) | `tools-editor` + `art-pipeline` | Wave 1 | **Yes, hard** |
| Finish St Peters village + the cast | `world-content` + owner | Wave 2 | **Yes — this is the shoot date** |
| The shell (title, New Game, settings, pause, build stamp) | `ui-ux` + `lead-architect` | Wave 3 | No, but same week |
| Lock the shot list; scout shoot to a temp track | `art-director` + owner | Wave 3 | No |
| Final capture, edit, grade; screenshots in the same session | contracted editor + `tools-editor` | Wave 4 | — |
| Steam page: copy, tags, capsules, screenshots, trailer | owner | Wave 4 | — |

**The two things to start this week are the audio commission and the capture kit.** Everything else
can wait for the world to be finished; those two cannot, and both are already owed to M1.
