# Lobster handling reference and data proposal

**Research checked 2026-11-11; documents only.** This supports the proposed NPC deck job and the [manual goods model](goods-handling/02-model.md), serving P3/P4/P5. Canadian public fisheries sources supply the observations below. Commercial suppliers supply equipment specifications and their own grades. Proposed fields and defaults are design inferences; this document creates no content asset, price, code, save contract or art instruction.

## Gauge, retention and release

A carapace gauge measures the head shell, from the rear of an eye socket to the rear edge of the carapace. It does not measure the tail, overall animal length or carrying envelope. DFO's survey manual illustrates the endpoints in Appendix 5, printed p. 25 (PDF p. 32), and records shell hardness separately on printed p. 10. Its whole-millimetre scientific recording convention must not round an animal across a fractional legal threshold. [DFO technical report 3376, 2020](https://waves-vagues.dfo-mpo.gc.ca/Library/4087638x.pdf)

The following are **regional examples**, not one Canadian minimum or a declaration that the fictional bay belongs to an actual fishing area. The March 30 **2026** Gulf notice, Table 1, gives these minimum carapace lengths; females in its 115-129 mm protection window must be returned immediately with the least harm. Check applicable licence conditions and variation orders before any later real-area profile is authored. [DFO 2026 Gulf management measures](https://www.glf.dfo-mpo.gc.ca/en/node/20568)

| Reference area | Minimum carapace mm | Additional female size restriction in that table |
|---|---:|---|
| 23A, 23B, 23C | 81 | Return 115-129 mm |
| 23D | 79 | Return 115-129 mm; minimum temporarily reduced |
| 24 | 75 | Return 115-129 mm |
| 26A-1, PEI and Gulf NS | 76 | Return 115-129 mm |
| 26A-2 | 78 | Return 115-129 mm |
| 26A-3 | 81 | Return 115-129 mm |
| 26B South | 82.5 | None listed |
| 26B North | 84 | None listed |

LFA 25 has a separate July 9 **2026** notice: its management table lists 79 mm minimum and a 115 mm maximum for females. The prose above that table still says "in 2025". Preserve that discrepancy in the evidence; do not turn the table's abbreviated maximum into an assumed inclusive/exclusive comparison without the licence wording. [DFO 2026 LFA 25 notice, carapace section and management table](https://www.glf.dfo-mpo.gc.ca/en/node/20619)

| Inspection | Documented rule and source | Proposed goods consequence |
|---|---|---|
| Berried female | Eggs attached prohibit buying, selling or possession. Females with eggs washed/removed, or egg cement/glue on swimmerets, are also prohibited. Newfoundland and Labrador's general minimum is 82.5 mm; the general 72 mm provision is not permission to ignore higher local limits. [Atlantic Fishery Regulations, s. 59](https://laws-lois.justice.gc.ca/eng/regulations/SOR-86-21/section-59.html) | Record reproductive/protection facts independently of size. Deck inspection routes protected catch to release before banding or keeper storage. |
| V-notched female | Newfoundland and Labrador's evergreen IFMP, commencing 2017, authorizes voluntary notching of berried females and requires return of notched lobsters, including the relevant tail-fan section missing, disfigured or tampered with. The mark protects females after eggs are gone. [DFO NL IFMP, section 7.6](https://www.dfo-mpo.gc.ca/fisheries-peches/ifmp-gmp/lobster-homard/area-zone-3-14c-eng.html) | Keep notch/protected-tail evidence separate from current egg state; never infer that an unberried animal is retainable. This source does not establish a Canada-wide notch-depth tolerance. |
| Regional scope | The archived **2019**, superseded Maritimes IFMP records exceptions to its v-notch possession restriction in LFAs 27 and 31A. This is historical evidence of variation, not current permission. [DFO archived IFMP, Table 2 footnote 2](https://www.dfo-mpo.gc.ca/fisheries-peches/ifmp-gmp/maritimes/2019/inshore-lobster-eng.html) | A retention policy needs area, effective revision and provenance. An active notching programme and a possession prohibition are distinct policy fields. |

**Recommended research default:** use the 26A-1 PEI example for a future home-bay retention fixture, with its 76 mm minimum and inclusive female window, and release every berried or v-notched female. The notch rule is a conservative fictional design choice informed by the NL example, not a claim about PEI conditions. No current `SpeciesDeckRule.MinKeepSizeMm`, distribution, catch or buyer tuning changes here. Optional regional policies remain separate fixtures, never a blended Atlantic rule.

## Banding and commercial grades

The Canadian industry council's April **2013** husbandry statement, p. 2, describes banding claws after hauling to protect animals and handlers, and keeping bands on in holding systems. This is husbandry guidance rather than a universal statutory banding requirement. [Lobster Council of Canada handling statement](https://lobstercouncilcanada.ca/wp-content/uploads/2013/05/Statement-of-Handling-and-Husbandry-FINAL.pdf)

Proposed deck sequence: inspect one animal, release protected catch, band each present claw of a keeper, then place it in a compatible supported container. Track left/right band state and missing claws; a one-claw animal cannot satisfy a fictional requirement for two present claws. Tool, hands and animal must have a feasible working arrangement. Banding alone neither transfers ownership nor puts catch into a remote hold. Existing deck-work timing is untouched.

| Classification axis | Sourced reference | Proposed interpretation |
|---|---|---|
| Canners | The council describes approximately 0.5-1 lb; its jumbo description is over 2.5 lb. [LCC handling and buying](https://lobstercouncilcanada.ca/lobster-101/handling-lobster/) | A commercial mass band, independent of legal carapace size. A small legal lobster can be a canner; a label never makes a short lobster legal. |
| Markets and jumbos | A Canadian processor publishes Chix 1-1.25 lb, Quarters 1.25-1.5, Halves 1.5-1.75, Selects 1.75-2, Deuces 2-2.5 and Jumbo 2.5+ lb. [Processor live-lobster specification](https://westmorlandfisheries.ca/en/our-products/live-lobster/122-live-lobster) | "Markets" can group the ordinary live-size bands for the proposed buyer. These named bands are one processor's specification, not federal grades. |
| Hard and soft shell | The council relates softer shells to recent moulting and subsequent filling of the new shell. [LCC shell discussion](https://lobstercouncilcanada.ca/all-about-lobster/hard-shell-or-soft-shell/) | Shell condition remains independent of mass, freshness, life and physical damage. Soft does not mean dead or spoiled; a hard shell does not guarantee buyer acceptance. |

For a **synthetic buyer fixture**, recommend nonoverlapping bands: canner below 1 lb, market from 1 lb to below 2.5 lb, jumbo at or above 2.5 lb, after retention has passed. This resolves the descriptive sources' shared endpoints explicitly; it is not an assertion that every buyer uses those boundaries. Use the exact conversion `1 lb = 453.59237 g`, retain source units and apply a declared integer-gram rounding policy once when authoring. Do not use rounded display mass to test legal carapace length. No price or grade multiplier is proposed.

## Crate and on-deck holding

There is no single national crate rating established by these sources. The **IPL-style reusable lobster crate** is a concrete reference family, distinct from an insulated fish tote and a shallow holding tray.

| Equipment evidence | Dimensions, capacity and weight | Limits on using it as data |
|---|---|---|
| IP lobster crate, supplier's North American listing | Outside 32 x 20 x 15 in, listed as 81.30 x 50.80 x 38.10 cm; capacity 3.90 ft3 / 110.40 L. Attached locking lids and flotation are described. [Supplier specification](https://martakindustrial.com/product/ip-containers-lobster-crate/) | Catalogue outside envelope is not a clear opening measurement or a load rating. |
| IPL crate, separate supplier listing | Same nominal outside size; listed inside 27.75 x 18 x 13.75 in; empty weight 17 lb, approximately 7.71 kg; volume 3.9 ft3. **Load capacity is listed as n/a.** [Crate specifications](https://www.tridentsps.com/products/lobster-crate) | Treat 17 lb as that listing's nominal tare pending exact revision/wet-tare confirmation. Do not combine specifications from different revisions into an approved Def. |

No fixed animal count follows from 110.4 L. The goods model needs actual animal envelopes, authored placements, count and gross-mass limits together. Obtain the opening, exact lid envelope/mass, wet tare, permitted payload and support rating before authoring. The ruled two-total insulated-tote stack limit does not automatically apply to this vented crate; no crate stacking permission is inferred here.

**Synthetic gross-weight example:** 100 lb product plus a 17 lb crate would total about 53.07 kg before added material. This exercises the product-versus-gross distinction; 100 lb is an arbitrary fixture load, not a payload or lifting rating from either selected specification.

DFO's **2016** southern Gulf survey reports on-board storage categories including ice, no ice and seawater fill/drain or recirculation, with practices varying by area/season (printed p. 6 and Table 23). These observations do not specify direct ice contact or a safe live-holding duration. [DFO manuscript report 3247, published 2022](https://waves-vagues.dfo-mpo.gc.ca/library-bibliotheque/41063016.pdf)

| Proposed holding mode | Physical requirements before a future profile can be enabled |
|---|---|
| Working deck crate | Supported shell and closed/latching state as appropriate; cool/moist protection must come from a separate compatible holding policy. Ventilation/flotation does not create indefinite live storage on deck. |
| Seawater tank with crate/tray | Tank has a real supported location; its water mass counts against vessel payload. Authored water quality, circulation/oxygen and stocking limits belong to an operating habitat/facility profile. |
| Insulated tote | Separate container identity and measured capacity; a name or insulation does not make it a live tank. Freshwater ice melt is incompatible with the council's live-lobster guidance. Use isolated cooling only after that mode is validated. |

The council's statement, p. 4, describes monitored saltwater holding, including salinity, temperature, oxygen, ammonia and pH. Its food-service guidance warns against freshwater/ice exposure. Those sources support habitat fields, not a new biological timer; use the existing [landed-catch proposal](landed-catch-research.md) boundary when survival data is absent. [LCC husbandry statement](https://lobstercouncilcanada.ca/wp-content/uploads/2013/05/Statement-of-Handling-and-Husbandry-FINAL.pdf), [LCC live handling](https://lobstercouncilcanada.ca/lobster-101/handling-lobster/)

## Wharf receiving and proposed records

One Nova Scotia buyer describes removing tagged crates from boats, receiving/weighing/scanning at its plant or other wharves, then temporary tank holding. Later, individuals are weighed and routed by size/storability; weak/dead, soft, damaged, unbanded, undersized, berried or notched animals go to QA trays tied to the originating crate. This is one documented route, not proof that final grading occurs at every wharf. [Buyer's receiving and sorting process](https://www.lobstersrus.ca/the-process)

For the game, carry a crate to a compatible scale support, identify the lot, apply the declared tare/drain method, inspect and retain separate accepted/refused destinations, then confirm the delivered sale. The reusable crate remains owned and retrievable. A scan records an arrival; it never performs a transfer. The existing [buyer proposal](atlantic-landing-buyers.md) owns weighing/grade/receipt records; physical gross, net product and commercial acceptance remain distinct.

| Def or state, proposed unless marked existing | Fields in the goods model's terms | Validation before implementation |
|---|---|---|
| Retention policy referenced by `GoodsProfileDef` and deck rules | Stable policy id; species/area/revision; carapace measurement basis and precision; min/max and female windows with explicit endpoint flags; egg/cement and notch/protected-tail rules | Valid nonoverlapping ranges; missing sex/protection facts cannot silently pass a required inspection; legal length is independent of `Outside` and mass. |
| Existing `GoodsContainerSpec`, future crate profile | `Outside`, `Inside`, `Opening` in mm; `UsableVolumeMl`; `EmptyMassG`, `LidMassG`, `MaxGrossMassG`; `CountLimit`, `Slots`, `AcceptedProfiles`; grip/hand and stack fields | No asset while required measurements/ratings are missing. Include each lid mass exactly once; preserve the full-put refusal and actual carrier support checks. |
| Holding policy referenced by `GoodsProfileDef`/container/facility | Compatible species/life; ambient-moist, isolated-cooling or seawater mode; water/added-material mass and volume; operating habitat dependencies; data provenance; optional validated exposure budget in game seconds | Generic `Iced` is not `Live`; missing survival data supplies no invented deadline. No state restores prior quality or revives catch. |
| `BuyerGradeDef` and `WeighingPolicyDef` from the receiving proposal | Independent mass bands, shell/condition/life/band criteria; assessment stage; tare/drain method, resolution and rounding; revision | Complete, nonoverlapping bands; no duplicated tare/water deduction; reject unknown commercial conditions without deleting goods. |
| Individual observation and `ReceivingLotState` | Item id, measured carapace/mass, reproductive/protection facts, shell/condition, band states, observation time; source trap/trip/crate ids and delivered membership | Mutable facts belong to instances, not Defs. Retain provenance, life/freshness history and once-only settlement through any later reviewed save contract. |

Future logic acceptance examples: exact fractional minimum and female-window endpoints; a legal-size berried/notched animal releases; a legal canner remains distinct from a short; a live soft-shell animal fails a hard-shell buyer criterion without becoming dead; an overweight or full receiver refuses without mutation; removing a reusable crate never duplicates its catch. These are design acceptance cases, not tests claimed to have run.

The [goods phasing](goods-handling/06-phasing.md) remains authoritative: physical manual delivery first, additional storage next, M3 hired work after the manual route is proven. NPC skippers must perform the same inspect/band/stow/unload steps with real supports and capacity. This research gives no permission to skip carrying, write a save, change current tuneable numbers or alter a shipped scene.
