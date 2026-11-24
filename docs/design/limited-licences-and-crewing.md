# Limited Licences, Working as a Hand, Charters, and Leased Water (DESIGN PROPOSAL)

> **Status:** proposal for the owner, 2026-11-23. Nothing here is built, and **nothing here is built
> during the first week on St Peters** (the demo), except the two tiny pieces marked in §11.
> **Agrees with:** the canon amendment of 2026-11-23 (`../vision-and-pillars.md` §5.3, the first week
> on St Peters; ADR 0062), which sets the direction: lobster and crab licences are a higher tier, a
> fixed number in the world, never sold over a counter.
> **Builds on:** `progression-and-housing.md` §2.2 (licences), `economy-and-business.md` §5 (staff),
> `crew-roles-and-trip-limits.md` (roles and crew per hull), `st-peters-living-economy.md` (the island
> boat), `mussel-lease-and-longline.md` (the lease loop, which this document extends, not replaces).
> **Owner:** economy-sim, with world-content (people and stories), gameplay-systems (deck work, tags at
> landing), ui-ux (the Notebook pages) and lead-architect (the save step).
> All numbers below are **placeholders for the owner to tune in data**.

---

## 1. The owner's words (the brief)

On lobster and crab, 2026-11-23:

> "the issues with the lobster/crab licneses are that i wanted them to be a higher tier item, on pei
> these licesnes can worth millions for lobster and perhaps 100k for crab. i wanted that same
> progression in that the player must work as a hand on a lobster or crab boat and earn their way to
> buying a boat/license. perhaps the player can fish shellfish under the licenese of an older fisher on
> st peters who refuses to sell? then eventually the player can either buy or earn that licnese. thers a
> fixes amount of licenses on pei. they are only sold and not created. i wanted this game to reflect
> that in some way"

On tuna and sport fish, the same day:

> "tuna and certain sport fish will also have these exclusive expensive licesnes, the player will be
> able to fish these catching a limited amount per season, and can host charters to have npcs catch
> them and the player earn money on the charters"

On leased water, the same day:

> "i also want to create mussel leases, and use the same equipment/processes used on pei. the same can
> be done for oyster farms."

Rules already ruled the same day, which this design keeps:

- **Every catch needs a licence.** Nothing is ever blocked: the shovel, the rod and the traps all
  work. A catch he holds no licence for is **released automatically**, he says "I don't have a
  license for this <name> yet", and **no Notebook entry** is made.
- **Ordinary licences are cheap counter licences** (the shellfish licence Ginny buys him; the cod
  licence; others for rod fish). Which ones exist is still open.
- **The first week is St Peters only, and it is the demo.** This system comes **after** that week.
- Larger shops will later be supplied by trucks from a city not built yet, tying into cargo ships.
  The arc is **dory to dynasty**.

---

## 2. The idea in one paragraph (plain language)

There are two kinds of licence. **Counter licences** are cheap paper anyone can buy; there is no
limit on how many exist. **Limited licences** are different: the coast has a **fixed number** of
them, written down from the start, each held by a named fisher. A new one is never made. The only way
to get one is for a holder to let it go, and that only happens through a holder's own story: he
retires, has a bad year, has no one to leave it to, or is in debt. So the player cannot simply save
up and buy one. First he **works as a hand** on someone else's boat, learning the fishery and earning
a share and the skipper's trust. Then he may **fish a few pots under the licence of an old St Peters
fisher** who will not sell. In the end he **buys or earns** a licence of his own, and much later he
may hold several and **lease them to other skippers**: that is the dynasty. Tuna and a few sport fish
work the same way, with a set number of **tags per season**, and the holder can run **charters** for
paying guests. Leased water for **mussels and oysters** is a third kind of scarce right, with its own
yearly cycle and real PEI equipment.

Pillars served overall: **P2 Dory to Dynasty** (a licence is the biggest rung below a fleet), **P3
Living Working Coast** (the licences belong to people with lives), **P4 Earn It, Then Automate It**
(hand first, holder next, employer last), **P5 Cozy but with Teeth** (scarcity is the teeth; nothing
is ever taken from him).

---

## 3. The licence tiers

| Tier | What it is | How many | How you get it | Price scale | Pillar |
|---|---|---|---|---|---|
| **Counter** | Paper bought over a counter | unlimited | pay the fee | 15 to a few hundred | P3 (the gentle first gate) |
| **Limited** | A place in a closed fishery, held by a person | **fixed in data**, never created | a holder lets it go (§6), or you inherit/earn it (§5) | thousands to tens of thousands | P2, P5 |
| **Leased water** | A plot of water for mussel or oyster culture | limited by the bay's area (§9) | granted on unleased water, then passed on | a fee plus yearly rent | P2, P4 |

Which species sit where (proposals, each marked):

| Species (on main unless noted) | Tier | Why |
|---|---|---|
| Lobster (`fish.lobster`) | **Limited** (ruled direction) | The owner's PEI picture: the dearest licence on the coast. |
| Rock crab (`fish.rock_crab`) | **Limited** (ruled direction: "crab") | His "100k for crab". The crab licence here is the rock crab one. |
| Green crab (`fish.green_crab`) | **Counter** *(proposal)* | An invasive pest in real PEI water, worth 1 here. A closed licence for a pest would read wrong. Lean: covered by a cheap counter licence (which one is open). |
| Bluefin tuna (not on main; listed in `fish-and-content.md` as waiting on a method) | **Limited, with tags** *(proposal)* | PEI's north shore is famous for giant bluefin and for tuna charters. The strongest fit for his second quote. |
| Atlantic halibut (not on main) | **Limited, with tags** *(proposal, later)* | Offshore, the Banks; fits M3 when the Banks open. |
| Swordfish (not on main) | **Limited** *(proposal, later)* | The harpoon work (`work.swordfish_harpoon`) and the striker/lookout roles already exist as crew data. |
| Shark (porbeagle, not on main) | **Not proposed** | Would only fit as charter catch-and-release; a sensitive look for a cozy game. Left out unless the owner wants it. |
| Striped bass (`fish.striped_bass`) | **Counter** *(proposal)* | See below. |

**What moving striped bass into the limited tier would do to the first week.** Striped bass is the
island's one sport fish and bites in High Summer, which is when the game starts. The first week must
sell the licences it needs on the island. If striped bass became limited, every bass he hooked in the
demo would go back with the spoken line, and the demo would lose its best fight on the rod (Junior
Poirier loves striped bass; it is a feel moment). Lean: **keep striped bass a counter licence**, and
let bluefin carry the "exclusive sport fish" role from M3.

---

## 4. The fixed registry

Each limited licence is **one data file**: who holds it now, which water it covers, how many pots or
tags it carries, and the story facts that decide when it might come up for sale (§6). The list is
written once and only ever grows by the owner adding a file; the game never makes a new one.

**Which water.** On main the lobster season is already split by water (eight `FishingSeasonDef`
assets: St Peters, East Water and Coddle Cove open Early Spring day 15 to High Summer day 21; Nine
Mile Creek opens in Hard Winter; Rice Point, South Water, West Water and Cumberland each have their
own). A lobster licence names **one of those waters**, the way a real PEI licence names its fishing
area. That alone stops one person fishing the whole coast.

**Who holds them** (reusing people who exist; proposals):

| Licence | Holder | Boat and home port | Notes |
|---|---|---|---|
| Lobster, St Peters water #1 | **Monique Landry** | the island boat, St Peters (ruled skipper, with Angus Gillis as sternman) | the working licence the player sees fished daily |
| Lobster, St Peters water #2 | **Basil Samson** | none now; he sells the broken dory | the old fisher who refuses to sell (§5); "Forty-one years", says his own line |
| Lobster, Nine Mile Creek water #1 to #4 | **Leo Arsenault, Yvette Doiron, Ross MacDonald, Hughie Campbell** | lobster boats, NMC quay wall | the NMC wharf register as shipped |
| Lobster, Nine Mile Creek water #5 | **Marie Gallant** | Cape Islander, NMC | also a tuna holder |
| Lobster, Cumberland water #1 | an estate on the mainland *(new, unnamed)* | none | a story seed: a licence nobody is fishing |
| Rock crab #1 to #4 | **Monique Landry, Ross MacDonald, Hughie Campbell, Celeste Bernard** | as above; Celeste's fishing skiff | Hughie's low prosperity (1) makes him the likely first crab seller |
| Bluefin #1, #2 | **Marie Gallant, Leo Arsenault** | Cape Islander; lobster boat | the Cape Islander already lists the bluefin rod work |
| Bluefin #3 | a charter skipper from the mainland town *(new, later)* | a sport fisher | the guest-trade rival |

**Relative values.** His PEI figures put lobster at "millions" and crab near "100k": crab is about a
twentieth of lobster, maybe less. On PEI a lobster licence is commonly worth more than the boat that
fishes it. Game money on main: the broken dory 400 plus 300 to mend, a punt 1,800, an inshore lobster
boat 7,200, the standard lobster boat 20,200, a Cape Islander 17,400, a side dragger 216,000, houses
4,500 to 18,000; counter licences are 15 (shellfish) and 120 (cod).

| Limited licence | Proposed value (placeholder) | Reasoning |
|---|---|---|
| Lobster | **60,000** | about three standard lobster boats; dearer than any house; a real goal at the "workboat" stage (`economy-and-business.md` puts that stage at ~8k to ~40k) |
| Rock crab | **3,000** | a twentieth of lobster, his ratio; reachable early in M2, a first taste of owning a place in a closed fishery |
| Bluefin | **15,000** *(guess)* | between the two; I could not find a reliable PEI price to scale from |
| Halibut, swordfish | not priced | later |

**Trap cap per licence, as data.** A real PEI lobster licence caps gear at about 250 to 300 traps,
set by area. The game scales that by **about one tenth**: placeholder **30 pots** per lobster licence
(the cap can differ by water, as the real one does by area), **15** per rock crab licence. One tenth
keeps the real proportions between waters while making a full licence about one long day of hauling
for a skipper and one hand, worked pot by pot in the deck work, rather than hundreds of repeats. Real
traps carry **escape hatches** for small lobsters and **biodegradable twine**, so a lost trap frees
its catch; proposal: a pot lost in a blow in the game frees its catch too and never fishes on (no
"ghost" catch), which keeps the cozy rule and the real one.

---

## 5. Working as a hand (pillar P4, P3)

**Getting hired.** After the first week, the skippers above need a hand on some days. Monique's
regular sternman is Angus Gillis; the first opening is a day he is laid up (a seeded day, so it is the
same on every replay of the same world), and Monique asks the player at the wharf the evening before.
At Nine Mile Creek, any lobster or crab skipper may ask, and he may also ask them. A skipper asks
only in their own season and only if the weather lets them go out.

**Before the season: gear week.** The days before the season opens are hand work too: building and
mending traps, rigging buoys and lines, painting the skipper's buoy colours, and work on the boat.
Paid by the day (placeholder), it is the gentlest first hire and the first place trust is earned.
Setting day follows: the boat leaves before dawn loaded with baited traps.

**A day of it.** The boat leaves at about **3 a.m.** (real PEI crews leave around 2:30 to 3:00), so
he must be at the wharf before she goes; late means she has gone without him. Steam out. Traps are
baited with **herring or mackerel** (both bait items exist on main), set along the bottom in strings,
their positions kept on the boat's plotter and marked with buoys. At each string the **hydraulic
hauler** brings them up and the existing **pot deck work** runs, as the hand's deck jobs:
- **Pick** (a slow careful grab avoids a nip).
- **Measure and sort:** a carapace **gauge**, then sex and eggs. Shorts and **berried** females go
  back at once. Main already decides each animal's size and berried flag and already returns shorts
  and berried hens; this builds on it, adding the gauge as the visible act. (Some real areas also
  return large females; proposal: a per-water maximum size field, off by default.)
- **Grade and band:** keepers are graded **canners** (smaller) and **markets** (larger) and their
  claws banded. Main has a minimum keep size but no grade split; proposal: one more size threshold
  per species in data (market grade above it), and the buyer pays the grade.
- **Rebait and reset.**
Steam home, land at the wharf stage: the catch is **weighed** and held **live and chilled**, the
buyer grades and ships it (the stage and the run to the NMC plant are in the island-economy plan),
and he is paid there. The catch is the **skipper's**, under the skipper's licence, so nothing he
handles is released with the line; the Notebook may note "hauled with Monique".

**The week and his sleep.** Boats fish **six days a week, with the rest day off**. A 3 a.m. start
meets the game's rulings on sleep: sleep is the save and lasts at most 8 hours, and hunger slows him
but never stops him. So a hand's evening is early: in bed by about 19:00 to wake at 02:30. Lean: no
tiredness penalty is added; if he stays up and misses the boat, the cost is the missed day and a
little trust. I found no tiredness system on main to hook into, and none is proposed.

**Pay.** A **share** of that day's landed value, paid in cash at the stage. Placeholder: **15%** for a
new hand, rising to **25%** with trust. A fair day (30 pots, about one keeper each, lobster's base
value 28) pays him around 125 to 210. That is a lot more than a day of clams, and it costs him the whole day.

**Trust with a skipper.** One number per skipper, never shown as a bar. It rises when he is on time,
sorts honestly (no shorts or berried hens kept), and does not quit a day early; it falls when he is
late or no-shows. Higher trust means: a better share, being asked more often, running the hauler,
taking the wheel home, and **hearing first** when a licence may come up (§6). It is said in lines,
never a meter: "You can take her in, if you like."

**What it teaches.** The seasons and waters, the pot count, the sorting rules, what a licence earns
in a good and a bad week, and what a sternman is worth. That last one matters: later **he hires
sternmen himself** (the existing `role.sternman` data), and he knows what fair pay looks like
because he took it.

### 5.1 Fishing under the old fisher's licence (proposal: Basil Samson)

**Who.** Lean: **Basil Samson**. He exists, lives in the fisherman's cottage with Junior, has fished
forty-one years, and is already the player's first trade (the broken dory). Selling the boat he
cannot use any more while keeping the licence his father fished is a clear, human contrast. A new
islander is the alternative.

**The arrangement.** After the player has hand days behind him and some trust with Monique (who
vouches for him), Basil offers: "I'll not sell it. But it shouldn't sit idle either." The player may
set **10 of the licence's 30 pots** (placeholder) in St Peters water, in St Peters season, from **his
own boat that can carry pots** (on main the punt and console skiff list pot work; the dory does not,
so this also sets him a boat goal). He lands at St Peters, and **Basil takes a share of each
landing** (placeholder **40%**). These catches count as licensed and go in his Notebook.

**Its limits.** Ten pots, one water, one season at a time, renewed by Basil each season. If he keeps
shorts or berried hens, Basil ends it: it is Basil's name on the paper.

**The two ways it ends.**
1. **He buys it.** After two full seasons under the arrangement (placeholder), Basil agrees to sell
   to him and only him, at a family price (placeholder **70%** of value).
2. **He earns or inherits it.** If he keeps fishing it well, Basil retires fully and passes it to him
   (placeholder: after four seasons). Junior does not want it; he would rather dig, and Basil says
   so. That is the "earn" path the owner named.

---

## 6. How a licence comes up for sale (P3, P5)

**Always a story, never a menu.** Each licence file carries a holder's story facts: the holder's age
band, whether there is an heir, how well the holder is doing (the wharf register already has a
prosperity number), and any debt. Once per game year, a pure function of world seed, licence and
year decides whether that holder lets the licence go, and why: **retiring**, **a bad season**, **no
heir**, **debt**. Most years, most licences do not move. Placeholder: on average **one or two
licences on the whole coast come up each year**.

**How he hears.** First as **talk on the wharf** (a skipper who trusts him says it a few days early),
then as a **notice on the board** at the wharf or the post office, with a closing day about two weeks
out and an asking price.

**Can NPCs outbid him?** Yes. Other fishers make offers too; their offers are seeded from the value
(placeholder: 90% to 115% of it). He makes **one binding offer** before the closing day. The best
offer wins, except that a seller who trusts him (worked with, fished under) gives him **first word**:
he may match the best offer. If he loses, the licence goes to the NPC buyer, and the coast moves on.

**What stops cornering.** The price; how rarely they come up; one water per licence; NPC bidders;
and a placeholder rule that **each licence of the same kind he already holds raises sellers' asking
price** (the coast talks: "You've two already."). There is no hard cap.

**When he holds several (the dynasty).** He can fish **one** of each kind himself. Others must have
an operator: he **leases** them to an NPC skipper for a share of their season (placeholder 30%), or,
from M3, **hires a skipper** through the staff layer. An idle licence simply earns nothing. Leasing
is income without labour, and the right ending of "earn it, then automate it".

---

## 7. Tuna and sport fish: tags and charters (P1, P2, P4)

**Tags.** A bluefin licence carries **tags per season** (placeholder **2**, only in the seasons the
species data says it runs, High Summer and The Turn). Each bluefin he keeps uses a tag. At the limit,
the next fish he brings alongside **goes back** with a spoken line: "That's my tags done for the
season. Off you go." Same rule as the unlicensed release: nothing is blocked, the catch is simply not
his to keep. Tags reset when the season turns.

**Charters.** Guests book a trip; the holder takes them out; **the guests fight the fish**; the
holder handles the boat. Paid **per trip** (placeholder 400) plus **per fish raised** (placeholder 150).
- **Booking:** offers arrive a few days ahead, as a letter at the post office or a word at the wharf.
  He accepts or lets it pass. Which guests offer on which day is seeded; how many offer rises with
  his charter reputation.
- **Tags:** a guest's fish **kept** uses one of the same tags. Fish **released** alongside use none
  (on PEI much tuna charter fishing is catch and release; I could not check the exact rules, so treat
  this as game design). So charters still earn after the tags are spent.
- **Boat and gear:** a hull that lists the bluefin rod work (on main: the Cape Islander and both
  sport fishers), the bluefin rod, and a **mate** (the crew table gives bluefin rod two people:
  skipper and mate). He cannot run a charter alone, so charters wait for hiring (M3).
- **Reputation:** a charter reputation rises with fish raised, safe trips and good weather calls
  (turning back in time counts as good), and falls with cancelled or frightening trips.
- **Where guests come from:** at first, **the mainland town**; later **the city**, once the trucks
  and cargo arc exists (guests arrive like goods do, by road).
- **Farm tours (P3):** the same booking shape can bring guests to an **oyster farm** for a tour and a
  tasting (§9.6).

---

## 8. Determinism and the save (rule 5)

The world must stay a pure function of **world seed + game time + saved facts the player caused**.

**Recomputed, never saved:** who holds each licence while the player has not been involved (the
starting holder plus the seeded yearly story events); which licences come up for sale, when and why;
NPC offers and the NPC winner; which days a skipper needs a hand; which guests offer charters on
which day; NPC catch plans (the existing NPC pot day is already a pure function); mussel and oyster
growth (§9.7).

**Saved, because the player caused it** (all small):
1. Licences he holds: **already saved** in the existing owned-licences list. A limited licence id
   goes in the same list. No new field.
2. Licences he sold or passed to an NPC (licence id, new holder, day): stops the recomputed chain
   from handing it back.
3. Trust per skipper (skipper id, number).
4. The under-licence arrangement (licence id, start day, pots, share).
5. Leases he has given to NPC skippers (licence id, skipper id, start day, share).
6. A pending binding offer (licence id, amount, closing day), self-clearing after the day.
7. Tags used (licence id, season stamp, count), self-clearing when the season turns.
8. Accepted charter bookings (day, guest party id), self-clearing after the trip.
9. Leased water: plots and what was set where and when (§9.7).

**This needs a save version beyond v26, which is not approved and must be asked of the owner.** Main
is at v25; v26 is the one the island-economy plan asks for at its slice 8. To keep this bump small:
**one** list of compact rows (kind, id, a few numbers, a stamp), many of them self-clearing, and no
change to any existing field. If v26 has not shipped when this is scheduled, the owner may prefer to
carry these rows in the same bump; that is his call.

---

## 9. Mussel leases and oyster farms (leased water, the third scarce right)

`mussel-lease-and-longline.md` already designs the lease loop (lease, set gear, seed, grow, read the
crop by how low the floats ride, tend, harvest, automate). This section adds what the owner asked for
on 2026-11-23: **real PEI equipment and process**, the **scarcity** of leased water, the **ice
harvest**, and **oyster farms**. Where the two disagree, the owner's PEI facts below win.

PEI practice as the owner described it (reference only): rope culture in bays and estuaries; seed
(spat) collected on ropes; seed graded into **mesh socks about 7 to 12 ft long hung vertically** from
**longlines held up by buoys**; **12 to 24 months** to market size (about 55 to 60 mm) **with no
feed**; open-water harvest by **boats with cranes, pulleys and winches** lifting longlines (a line can
weigh about **2 tonnes**) for delivery to a **processing plant**; a **winter harvest, roughly January
to March, through the ice** (lines sunk below the ice, holes cut with saws, lines winched up, mussels
hauled ashore by **sleigh in insulated containers**); leases are **private water, not open to
public picking**; **wild mussels live under rocks, not on sand flats**; St. Peters Bay is a real lease
area.

### 9.1 How a lease is obtained (P2, P5)

Leased water is limited by **area**, not by a number of papers. Proposal (lean): a **hybrid**.
- Each bay that can hold leases has, in data, a **leaseable area** and a set of **existing NPC
  leases** (each one file: holder, plot, what grows there).
- While unleased water remains, he **applies** at the counter (the harbourmaster; a placeholder fee
  and a wait of some days) and is **granted** a plot of it.
- When a bay's leaseable area is full, leases only **change hands**, through the same story
  mechanism as licences (§6): a grower retires, sells, or leaves a lapsed lease behind. The lapsed
  lease (the starter-ruin candidate the mussel document names) is one such story.
- Yearly **rent** per plot (placeholder) keeps idle leases from being free to hoard.

### 9.2 The yearly cycle on the game's calendar (P1)

A game year is four 28-day seasons. Real growth times are compressed by **one tunable factor**
(placeholder **0.5**: one real year becomes half a game year, rounded to whole seasons, never under
three seasons), so the ratios between crops stay true.

| Season | Mussels |
|---|---|
| **Early Spring** | spat settles; he hangs **collector ropes** on his longline |
| **High Summer** | collectors fill; first **grading and socking** of seed (deck work, by hand first) |
| **The Turn** | socked lines grow; tend floats as the crop gets heavy; first harvest of lines socked a year before |
| **Hard Winter** | **the ice harvest** (below); open-water work stops |

Mussels: 12 to 24 months real, so **three to four seasons** in the game from socking to market size.

**The ice harvest (Hard Winter, P1, P5).** Lines are sunk below the ice before freeze-up (a chore in
late The Turn; a line left floating is lost to the ice). On the ice: cut a hole with the ice saw, winch
the line up through it, strip into **insulated containers** on a **sleigh**, and haul ashore (towed by
hand, or by the island's tractor when that exists). Weather and ice thickness, both recomputed from
seed and time, decide the safe days.

### 9.3 The equipment, as data (rule 2)

Each a Def, one per file, stable id: **collector rope**, **mesh sock** (length, holding mass),
**longline** (backbone length, sock count), **buoy** (lift), **anchor**, **mussel crane/winch** (lift
limit, so a 2-tonne line needs a real one), **ice saw**, **sleigh**, **insulated container**. Which
hulls carry the crane: the mussel document's rule stands, **the role comes from the gear fitted, not
the hull class**. Candidates on main: the 12 m lobster boats and the Cape Islander (both already work
a hauler and longline gear), plus the small low-freeboard mussel boat the owner asked for in August.
The dory and punt can work collector ropes and a short line by hand, not a full 2-tonne line.

### 9.4 Where the harvest goes (P3)

Into the supply chain already in data: St Peters landings go to `fishstore.st_peters_wharf` and over
the bar on `fishrun.st_peters_to_plant` to the NMC plant (`fishplant.nmc_industrial`). The plant has
canned, fresh fillet, frozen and lobster lines today, and **no shellfish line**: a **mussel line**
(declump, grade, bag, ship live) is new data. Oysters sell mainly **live to the restaurants and the
bay hotel** (outlets already in data), with the city later.

### 9.5 Oyster farms, the parallel farm (P1, P3)

The species is the **American oyster** (`fish.american_oyster`, on main), feeding on plankton, no
feed. PEI methods as the owner described them: traditional **bottom culture** (5 to 7 years to
market size; worked by **tonging** from a small boat), and **off-bottom culture** in **floating
cages, sea baskets and bags** (cocktail oysters in 2 to 3 years, full market size in about 4).
Floating cages are **flipped roughly every two weeks in peak season** to air and sun the oysters and
control fouling: a good hands-on chore, later done by hired hands. With the 0.5 factor: off-bottom
cocktail in **five seasons**, full size in **eight**; bottom culture in **twelve**. Oysters from
different bays differ in shell colour and taste ("merroir": briny, sweet, earthy, nutty, mineral), so
each lease **bay** carries a flavour note and a price multiplier in data. Disease is **deliberately
left out**, per the owner: "i will probably leave dieases out as this is a sensitive topic".

### 9.6 Earn it, then automate it; what the world shows (P4, P3)

He works his first line by hand: collecting, socking, flipping cages, stripping, the ice harvest.
Later hired hands (deckhand, the existing `role.shucker`, and a new grower role if needed) do the
tending and harvest, and a manager runs a site. In the world: **rows of buoy lines in the bay** are a
strong look from the shore and the water, with **NPC growers** working them on seeded days, and in
Hard Winter, figures and sleighs out on the ice. Farm tours and tastings use the charter booking
shape (§7).

**Picking on someone's lease is not wild harvest.** The shellfish licence covers wild picking only
(wild mussels under rocks, oysters on wild beds). A mussel or oyster taken from a lease he does not
hold is released with a line ("That's someone's lease. Not mine to pick."), and NPCs never pick his.
This also means the wild-picking work should put wild mussels **under rocks, not on the sand flats**
(a note for world-content and the low-water picking item).

### 9.7 Determinism and save

Growth, yield, fouling, float sag and ice days are a **pure function of world seed + game time** and
the saved setting facts, exactly as the mussel document's §5 says. **Saved, because he caused it:**
leases he holds (the owned-licences list), his plots, and per line or cage what was set, where and
when. The NPC leases are data plus recomputed growth. These rows join the same small bump as §8:
**beyond v26, not approved, must be asked of the owner**.

---

## 10. Technical section (for the builders)

**Content is data (ADR 0003).** New Def types, one entity per file, append-only `type.snake_case` ids:
- `LimitedLicenceDef` (`limitedlicence.lobster_st_peters_01` …): class licence id, water (a
  `FishingSeasonDef` id or region id), pot allotment, tags per season, value, starting holder (NPC
  id), story facts (age band, heir, debt), share defaults.
- `LeaseBayDef` (`leasebay.st_peters` …): leaseable area, rent, flavour note, price multiplier;
  `AquaLeaseDef` per NPC lease; gear Defs per §9.3.
- `CharterGuestPoolDef`: where guests come from, fees, reputation curve.
- Tunables (shares, trust steps, price multipliers, offer spread, sales per year, growth compression)
  live in these Defs or `GameConfig`. No magic numbers.

**The catch gate stays where it is.** Today `CatchLicensePolicy` maps a species to the first
`LicenseDef` that lists it and asks Core `ILicenseService.IsLicensed(id)`. Proposal: author one
**class** licence per closed fishery (`license.lobster`, `license.rock_crab`, `license.bluefin`) with
no catalog listing, so it is never sold. The Economy `LicenseService` then answers `IsLicensed` for a
class id as true when the player holds **any** `LimitedLicenceDef` of that class, or has an active
under-licence arrangement in that water and season. Fishing does not change, and the two class
LicenseDefs alone make lobster and rock crab release in the first week (§11, L0).

**Cross-module talk through Core (rule 4).** New Core interfaces: `ILimitedLicenceRegistry`
(`HolderOf(id, gameTime)`, `ForSaleAt(gameTime)`), `ICatchAllowance` (`TryUseTag(speciesId)`, for
the tag release at landing). New Core events: `LicenceForSaleAnnounced`, `LimitedLicenceTransferred`,
`HandDayPaid`, `CharterTripCompleted`, and the release event the counter-licence work adds, with a
reason (no licence, no tag, someone's lease). Lobster grading adds data only: a market-grade size
threshold per species (canners below, markets above) and an optional per-water maximum keep size,
both read by the existing deck-work sort (`DeckWork.IsKeeper` today checks berried and minimum
size). The trap cap is a field on `LimitedLicenceDef`. The hand day reuses `PotDeckWorkController` and the NPC
pot day (`NpcPotDayDef`); the arrangement pots reuse `PlacedTrap`.

**Tests (EditMode):** registry holder resolution is a pure function (same seed, same holders at any
time; replay converges); sale events and NPC offers are deterministic; a saved player sale overrides
the recomputed chain; tag count self-clears at the season turn; class-licence `IsLicensed` true for
any held instance; growth compression rounds to whole seasons with a three-season floor.

---

## 11. Slices, in build order

Each slice is one PR. Size: S (a day or less), M (a few days), L (a week).

| # | Slice | Size | Owner | Depends on | Save | Milestone |
|---|---|---|---|---|---|---|
| **L0a** | **Class licences for lobster and rock crab, never sold**, so they release with the spoken line | S | economy-sim | the counter-licence release work (trap case) | none | **may ship in the island week** |
| **L0b** | **Basil's lines** hinting he holds a licence he will not sell (dialogue only, no system) | S | world-content | none | none | **may ship in the island week** |
| **A0** | NPC **buoy lines in the bay** as scenery only | S/M | world-content + art-pipeline | art | none | **may ship in the island week (optional)** |
| L1 | Registry Defs + pure holder resolution + the class-licence check | M | economy-sim | L0a | none | M2 |
| L2 | Hand days: hired on Monique's and NMC boats, deck work aboard, share paid (trust in memory only) | L | gameplay-systems + economy-sim | L1, island boat (living-economy slice 4) | none yet | M2 |
| L3 | Story sale events, wharf talk, notice board, NPC offers and winners (watch only) | M | economy-sim + world-content | L1 | none | M2 |
| L4 | **The save step**: ADR and one bump with the rows of §8 | M | lead-architect | **owner approval** | **beyond v26** | M2 |
| L5 | Fishing under Basil's licence, and its two endings | M | economy-sim + world-content | L2, L4 | rows 3, 4 | M2 |
| L6 | Buying a licence: the binding offer, first word | M | economy-sim + ui-ux | L3, L4 | rows 2, 6 | M2 |
| L7 | Leasing licences to NPC skippers | M | economy-sim | L6, staff layer | row 5 | M3 |
| L8 | Bluefin species, method and tags | L | gameplay-systems + economy-sim | L4, the bluefin method | row 7 | M3 |
| L9 | Charters: bookings, guests aboard, fees, reputation | L | gameplay-systems + economy-sim + world-content | L8, mate hire (M3) | row 8 | M3 |
| L10 | Halibut and swordfish as limited fisheries | M | economy-sim | the Banks | none new | M3/M4 |
| A1 | Lease bays, NPC leases, application and grant | M | economy-sim + world-content | L4, mussel doc rulings | row 9 | M3 |
| A2 | Collectors, socking, longline and buoys, pure growth | L | gameplay-systems + economy-sim | A1 | row 9 | M3 |
| A3 | Crane/winch harvest on fitted hulls; the plant's mussel line | M | gameplay-systems + economy-sim | A2 | none new | M3 |
| A4 | The ice harvest in Hard Winter | M | gameplay-systems | A3, ice | none new | M3 |
| A5 | Oyster farms: cages and flipping, bottom culture and tonging, merroir | L | economy-sim + gameplay-systems | A1 | row 9 | M3 |
| A6 | Hired hands work the lines and cages | M | economy-sim | A2/A5, staff layer | staff save | M3 |
| A7 | Farm tours and tastings | S | world-content | A5, L9's booking shape | row 8 | M3/M4 |

Everything except L0a, L0b and (optionally) A0 **waits until after the first week**.

---

## 12. Questions for the owner (lean in brackets)

1. Is **Basil Samson** the old fisher who will not sell his lobster licence? (**yes**)
2. Placeholder values **lobster 60,000, rock crab 3,000**, tuned later in data? (**yes**)
3. Keep **striped bass a counter licence** so it stays in the first week? (**yes**)
4. **Bluefin** the first tagged sport fish; halibut and swordfish later; no shark? (**yes**)
5. May he **hold several licences**, fishing one of each kind himself and leasing the rest? (**yes**)
6. May **NPCs outbid** him, with a trusting seller giving him first word? (**yes**)
7. Is **rock crab** the limited crab, with **green crab** on a cheap counter licence? (**yes**)
8. Approve **one save step beyond v26** when slice L4 comes, not before? (**yes, at L4**)
9. Leases: **granted on a bay's unleased water until it is full, then passed on only**? (**yes**)
10. Is the **ice harvest** in Hard Winter in scope for the lease arc? (**yes, as its own later slice**)

---

## 13. What I could not verify

- Real PEI prices and rules (licence values, tuna tag and charter rules, swordfish licensing, oyster
  growth by method): only the owner's figures and general knowledge; my own claims are marked.
- Whether `PotDeckWorkController` can run aboard an NPC-owned boat without changes (read from its
  summary only).
- The game-time length of a day in real minutes, so the share and growth numbers are not paced.
- Whether the bluefin species data in `fish-and-content.md` (High Summer and The Turn) is final.
- The St Peters bay geography for lease plots and NPC buoy lines (no scene was opened).
- Whether the "beds" in the mussel document sit on flats; if so they conflict with the owner's
  "under rocks, not on sand flats".
