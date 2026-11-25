# The NPC trawler fleet: a plan in four PRs (2026-11-12)

**Owner's direction, verbatim:** "the trawerls fishing should refeclet true fish schools. using the
same single fish logic and the nets can haul so many indiviudal fish, each one a seperate sprite.
work on the automated fishing with npcs doing full routines same as the lobster boats"

**Status.** A plan, not a build. Read against main `12bc04b19a`. Nothing here is started. Paths are
under `Assets/_Project/` unless they start with `docs/`.

## The day, in one line

Crew board at the Rice Point trawler berths, steam down the dredged channel, cross into the South
Water, reach one of three grounds, shoot, tow, haul, sort and stow, steam home, unload by hand and
sell to one fish buyer on the Rice Point wharf.

## What main already has

| Piece | Where | State |
|---|---|---|
| The pattern to copy: a pot boat's whole day as a function of the clock | `Code/Fishing/NpcPotDay.cs`, `NpcPotDayDef.cs`, `NpcPotDayCatch.cs`, `NpcPotLanding.cs`, `NpcPotDayLibrary.cs`; host `Code/App/NpcPotBoats.cs` | live at Nine Mile Creek, three boats |
| Trawl arithmetic: tow, catch, damage, deck batch | `Code/Fishing/TrawlOperation.cs`, `TrawlTowSolver.cs`, `TrawlDeckBatch.cs`, `TrawlSurvey.cs`; `docs/design/trawl-fishing-logic.md` | merged, **unwired**: no scene, no input, no caller |
| The seam the net reads the schools through | `ITrawlSurvey.Fill`, `TrawlSchoolFieldBridge.TrySample` | interface and bridge exist; nothing in the game supplies a survey yet |
| The three grounds | `Data/FishingGrounds/SouthWaterShelf.asset` (sand, 20 m), `SouthWaterBank.asset` (rock and gravel, 35 m), `SouthWaterDeepMud.asset` (mud, 70 m) | data only; no runtime reader |
| Two trawler berths | `Data/Berths/RicePointTrawlerFace.asset`, `RicePointTrawlerOutside.asset` | merged with the dredged channel |
| The door between the two regions | Rice Point's arrival named `south_water` and the South Water's arrival at (-900, 576) | merged; the Rice Point point is being corrected by its own lane |
| A fish buyer | `Code/Economy/FishBuyer.cs`, sprite `FishBuyerStall.png` | the player's wharf buyer at Nine Mile Creek (sells a hold at market prices, pays the wallet); the pot boats' landing walks to its counter and moves no money. Rice Point has none |

## Rules the whole job keeps

- **A function of the clock.** Where a boat is, what its crew are doing and what is on its deck are
  computed from `(worldSeed, gameTime)` and the day's Def. Nothing about a boat's day is saved.
- **Data, one entity per file.** One trawl-day Def per boat, id `trawlday.<name>`; every time, speed,
  count and cap is a field. A new Def type is appended to the identity ledger.
- **Through Core.** Fishing owns the day and the catch; App hosts the boats (as it does the pot
  boats); the buyer is Economy's. They meet on Core interfaces and events.
- **NPC landings move no money.** The lobster rule: the buyer weighs and takes the fish; no price,
  offer or purse changes.
- **NPC boats do not use player doors.** Their tracks are laid through the same two door points, and
  the boat is drawn in whichever of the two regions is loaded.
- **Existing art only.** Hulls, net, winches and crew deck poses are another lane's. Each PR lists
  what it could not show as an art ask.

## PR 1. The day and the passage: one boat out and home, no fishing

**The player sees:** before dawn a crew walk down the Rice Point wharf and board one trawler. She
lets go, steams down the dredged channel and out of sight to the south. Standing in the South Water,
the same boat comes in at the north seam, steams to a ground, lies there for the working hours,
steams back. In the afternoon she is fast at her berth again.

**In it:**
- `NpcTrawlDayDef` (Fishing): owner, hull, berth (by `BerthDef` id), depart hour and jitter, the
  harbour passage, the ground (by `FishingGroundDef` id), speeds, draught and keel clearance.
- `NpcTrawlDay`: the plan. Legs in order (board, let go, channel, door, passage, on the ground,
  passage home, door, channel, make fast), each with a start and end time, built once per game day.
  It answers "which region, where, heading, what state" for any instant.
- A library asset under Resources, as the pot days have.
- The host draws the boat only when its region is the loaded one. The crossing instant is the same
  whichever side the player stands on.
- One trawl-day asset, for the face berth.

**Tests:** the plan is the same for the same seed and day, and differs across days by the jitter
only; every leg point has water for the draught at the lowest tide of the leg (the channel is
dredged as data, so this is arithmetic); the boat is in exactly one region at every instant; the two
door points equal the committed arrivals tables, read by name, never a literal; PlayMode in each
region: she appears, moves and never grounds.

**Needs:** the trawler hull's visual asset from the trawlers lane (read only). If it is not on main,
the PR ships on the nearest existing hull and says so.

## PR 2. Shoot, tow, haul: catch from the schools, as individuals

**The player sees:** on the ground the boat slows, the gear goes over, she tows a straight line at
working speed, stops and hauls. A fish finder run over the same water before the tow shows the marks
her net then takes.

**In it:**
- The tow as legs of the day: shoot, tow (track, depth and duration from the Def and the ground),
  haul. Several tows a day, the count a field.
- **The catch is the solver's, not a table.** Each tow's track is handed to `TrawlTowSolver`; the
  fish it meets come through `ITrawlSurvey` from the same school field the finder and the rod read,
  at the same seed and time. The result is a list of individual fish: species, weight, size.
- The `ITrawlSurvey` adapter over the live school field. Mine unless the fish lane ships it first;
  if it ships first, this PR only calls it.
- The day's catch is built once per tow on the slow tick, never per frame.

**Tests:** same seed, same day, same catch, fish for fish; a tow over empty water lands nothing; a
tow never lands a species the ground and the season do not hold; the count never passes the net's
capacity; the adapter and the finder agree on what is under a point; the build cost of one tow is
measured and stated.

**Open, for the seat:** the trawl note lists "live occupant reservation and depletion" as not
connected. This plan reads the schools and does not thin them, so a player fishing behind a trawler
finds the same fish. Thinning them is a change to the fish lane's field and is not in these four PRs
unless ruled in.

## PR 3. Sort and stow, steam home, unload by hand, sell

**The player sees:** the cod end is emptied on deck and every fish is its own sprite. The crew pick
them one at a time into boxes by species; the boxes are stowed. Home at the berth, the crew carry
the boxes up the wharf to the buyer, who weighs each one.

**In it:**
- Deck work as a function of the clock, as the pot boats' pick, band and stow are: at any instant
  the fish still on deck and the boxes filled follow from the catch list and the Def's seconds per
  fish.
- **One sprite per fish, pooled.** A cap on fish drawn at once is a Def field; past it the pile
  draws the cap and the count stays true in the boxes. The species' existing sprites are used.
- The landing: the pattern of `NpcPotLanding` (pick up, carry, set down, weigh, walk back), with the
  walk laid along the Rice Point wharf.
- **One fish buyer on the Rice Point wharf**, working as the Nine Mile Creek buyer does. Landings
  move no money.
- **The buyer's stock does not vanish.** It goes into a store a truck can later collect from. In
  this PR the store's contents are a function of the clock (the day's landings so far), exposed on a
  Core read, and are not saved.

**Tests:** every fish caught is on deck, in a box or at the buyer at every instant, and nowhere
twice; the boxes at the end equal the catch; the buyer's store at the end of the day equals the
landings; no purse and no price moved; no per-frame allocation on the deck with the cap reached.

**STOPs this PR may meet (the seat's call, not mine):** if the store must outlive the day or be
emptied by a truck, that is saved state, which is a save-format change. If the Rice Point buyer needs
a new offer or price, that is a price change. If the buyer must stand in the Rice Point scene as an
authored object, that scene is another lane's.

## PR 4. The fleet, and the proof that it is affordable

**The player sees:** several trawlers leave Rice Point through the morning, work different grounds,
and come home through the afternoon to both berths in turn.

**In it:** a trawl-day asset per boat; staggered departures; grounds shared out across the three;
berth turns so two boats never want one berth at one time (checked as arithmetic across the whole
day, as the pot boats' berths are).

**The proof:** on a GPU editor, the whole fleet at the worst instant (every deck full, the camera on
the wharf) against the same view with no fleet: frame time, draw calls and fish sprites alive, stated
as numbers. The catch build per tow on the slow tick, stated in milliseconds. Zero allocation per
frame.

**Tests:** no two boats share a berth, a ground track or a point of water within a hull's length at
any instant of any day in a sample of seeds; the whole fleet's day is the same for the same seed.

## What each PR needs from others

| Need | From | For |
|---|---|---|
| The corrected Rice Point `south_water` arrival point | the Rice Point lane | PR 1 |
| Trawler hull, net, winches, crew deck poses | the trawlers lane | PR 1 to 3; each falls back to existing art and lists the gap |
| The `ITrawlSurvey` adapter, or leave it to me | the fish lane | PR 2 |
| A ruling on thinning the schools | the seat | PR 2 |
| A ruling on a saved store and on the buyer's place in the scene | the seat | PR 3 |
| An editor slot per PR, and disk above the floor | the seat | every PR's PlayMode and plate |

## Not in these four

The truck that collects from the buyer's store; the player's own trawling (input, HUD, hired crew);
weather that keeps the fleet in; radar echoes for the trawlers (the helm tools review lists the same
gap for the pot boats).
