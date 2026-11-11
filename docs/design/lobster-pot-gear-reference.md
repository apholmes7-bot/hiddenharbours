# Lobster pot gear reference and data proposal

**Research checked 2026-11-11; documents only.** This complements the [lobster handling reference](lobster-handling-reference.md) for a future complete NPC deck job, serving P1/P3/P4/P5. Dimensions, historic observations, licence limits and proposed game inputs have different authority. No asset, code, price, current tuning, save format or visual changes are included.

## Trap envelope, mass and support

| Reference and scope | Documented values | What a future Def may infer |
|---|---|---|
| Atlantic Fishery Regulations, s. 61(4), except LFA 41 | Greatest dimensions must not exceed 125 cm long, 90 cm wide, 50 cm high. [Federal trap dimensions](https://laws-lois.justice.gc.ca/eng/regulations/SOR-86-21/section-61.html) | A broad regulatory ceiling is not a measured trap, usable catch volume or a regional permission. |
| Quebec LFAs 15-18, **2026-2028**, section 8.2 | Type 1 maximum 124 x 90 x 50 cm; Type 2 maximum 92 x 71 x 50 cm. [DFO conservation harvesting plan](https://www.qc.dfo-mpo.gc.ca/en/lobster-areas-15-16-17-18-seasons-2026-2028-chp) | Distinct gear profiles and applicable policy limits. Do not give a larger trap the smaller type's licensed count. |
| Magdalen Islands LFA 22, **2025**, sections 7.1-7.2 | Maximum outside 82 x 61 x 50 cm; 273 traps authorized. [DFO LFA 22 plan](https://www.qc.dfo-mpo.gc.ca/en/conservation-harvesting-plan-lobster-area-22-magdalen-islands-fleet-season-2025) | A dated smaller-envelope example; no claim that these are the home bay's limits or today's universal trap size. |
| Bay of Fundy historical gear description, **1999 draft**, printed p. 64 (PDF p. 65) | Average inshore wire trap about 23 kg / 50 lb. Ballast includes rocks, bricks or cement and varies with depth/current. [DFO-hosted draft right-whale recovery plan, gear appendix](https://waves-vagues.dfo-mpo.gc.ca/Library/284873.pdf) | A historical mass reference, **not** the tare of any dimensioned trap above. Obtain shell, ballast, wet gear and contents separately for a particular profile. |
| TSB **2023** advisory, one occurrence | Reports approximately 45 kg traps arranged in strings of seven. [TSB marine safety advisory 02/23](https://www.tsb.gc.ca/eng/securite-safety/marine/2023/m23a0107/m23a0107-02-23.html) | Trap mass varies substantially. Do not make the historical 23 kg average a maximum or assume every pot is hand-carriable. |

An outside envelope includes the trap's projecting parts for carrying and stowage. Interior kitchen/parlour space, entrances and escape apertures need separate measurements. Regulatory maxima provide no catch count, occupied-envelope packing layout, lid clearance or stack-support rating.

For the goods model, a trap is an identified gear item with its own shell/ballast mass and supported deck location; retained animals are identified children only where the future ownership contract permits that relationship. Hauling uses the attached line and rated equipment. Once aboard, trap and crate movements obey the same authored gross-mass, reach, support and carrier limits as other goods. No trap hovers while another operation borrows its support.

## Buoys, rope, strings and trawls

In this reference, **pot** and **trap** mean the same gear. A **string/trawl** is a connected series of traps on a groundline, with short bridles/snoods and one or more buoylines. It is not a mobile trawl net. Model actual ordered connections; a set is not an anonymous count of traps.

The historical Fundy appendix describes mostly 10-24 traps per trawl, buoylines at both ends and groundline intervals around 10 fathoms (18.288 m). It distinguishes sinking and floating rope; some configurations used sinking rope near the trap and floating rope farther up. These **1999 observations** explain gear components, not current rope prescriptions. [Historical Fundy gear description, p. 64](https://waves-vagues.dfo-mpo.gc.ca/Library/284873.pdf)

The **2026** Gulf table requires at least six traps per line in LFA 24 and in part of 26A-1 PEI. Applicability belongs to the selected subarea; a minimum is not a universal string length. [DFO 2026 Gulf table and footnotes](https://www.glf.dfo-mpo.gc.ca/en/node/20568)

The **2026 LFA 25** notice limits cumulative primary-to-secondary buoy rope to 3.7 m and prohibits trap-to-primary-buoy rope remaining afloat on the surface after setting. Its rope-marking requirements remain in force. Historical floating groundlines cannot be read as permission to leave excess surface rope. [DFO 2026 LFA 25 rope requirements](https://www.glf.dfo-mpo.gc.ca/en/node/20619)

| Component | Proposed physical data, all with source/revision | Validation consequence |
|---|---|---|
| Primary and optional secondary buoy | Separate stable Def and instance ids; envelope, dry mass, displacement/buoyancy rating and attachment capability; identification-policy id | The float supports the authored line load at the surface; it does not lift the trap above the seabed. Secondary-buoy connector length has its own limit. |
| Buoyline | Segment material/diameter, actual length, mass per length, sinking/floating behaviour, breaking rating, applicable weak-link policy and attachment ends | Nonnegative physical mass; distinguish submerged apparent weight from carried mass. Ratings need a selected product, not the historic appendix's example. |
| Groundline and bridles/snoods | Ordered segments, trap attachment sequence, lengths, material/mass and join method ids | Every pot appears once; segment lengths match the declared set; no hidden shared rope or duplicate traps in two sets. |
| Anchor/ballast and spare rope coil | Identified mass, envelope and support/attachment; rope remaining on deck has measured length and stowage volume | Attached and loose components count once toward boat payload. Empty/retrieved sets still have physical gear to stow. |

Marking/identification requirements belong to policy data. The existing player-buoy presentation and all colours, rigs and line art remain with their owners. This document neither repaints a buoy nor gives an NPC colour a new ownership meaning.

## Rope length against depth and tide

**Design inference for a future placement validator:** charted depth and tide must use the same vertical datum. Let `Dmax` be the maximum water-column depth expected during the intended soak, computed from the existing tide service and seabed. If the input is charted depth, include the expected tide height; if it is already instantaneous water depth, add only the subsequent rise, not the tide twice. Reserve authored connection/slack allowances and account for lateral buoy excursion under current. A necessary geometric check is `usable line length >= sqrt(Dmax^2 + excursion^2) + allowance`; it is not a complete catenary, tension, weather or real rigging calculation.

For a **synthetic arithmetic example**, charted depth 10 m plus a maximum tide height 4 m gives 14 m maximum water depth. A 12 m line fails even with zero lateral excursion and zero allowance. A line longer than 14 m has not automatically passed: current, attachment allowances, buoy rating and excess rope restrictions still matter. All inputs are authored/recomputed quantities, not proposed shipped numbers or a universal depth multiplier.

Keep a selected rope's sinking/floating behaviour explicit. A too-short line may submerge a buoy or strain the set; unnecessary slack introduces a different problem. The cited LFA 25 surface-rope constraint therefore remains a separate validation from sufficient reach. This proposal claims no solution for detailed line physics and supplies no real-world safety rating.

## Soak, hauler practice and a working day

| Evidence | Documented observation or restriction | Boundary for game data |
|---|---|---|
| LCC husbandry statement, **2013**, p. 2 | Describes soak periods of 1-5 days. [Canadian industry handling statement](https://lobstercouncilcanada.ca/wp-content/uploads/2013/05/Statement-of-Handling-and-Husbandry-FINAL.pdf) | An industry description, not a national minimum, optimum or permission to exceed local tending rules. |
| Quebec LFAs 15-18, **2026-2028**, section 8.3 | No lifting/baiting more than once daily; gear must be hauled at least once every 72 hours. [DFO tending restrictions](https://www.qc.dfo-mpo.gc.ca/en/lobster-areas-15-16-17-18-seasons-2026-2028-chp) | Separate minimum interval/calendar rule and maximum unattended interval. The generic 1-5 day description cannot override this regional maximum. |
| Southern Gulf survey, **2016** observations, published **2022** | Disc haulers were nearly universal; bow/stern location varied. 93.8% of respondents reported hauling all traps daily, with about eight hours to haul all gear on average. Table 25 records, for example, 249-300 traps fished by LFA 24 respondents. [DFO report 3247, pp. 6, 11 and Tables 25, 46-48](https://waves-vagues.dfo-mpo.gc.ca/library-bibliotheque/41063016.pdf) | Several hundred pot hauls can describe a full Gulf working day. Combining the regional allowance and daily-haul observations is an inference, not a measured universal throughput for each boat. |
| Newfoundland and Labrador IFMP, **2017** baseline | Most vessels under 30 ft; Appendix 2 lists licensed maxima from 100 to 300 traps by area, not pots hauled per day. [DFO NL IFMP, section 1.5 and Appendix 2](https://www.dfo-mpo.gc.ca/fisheries-peches/ifmp-gmp/lobster-homard/area-zone-3-14c-eng.html) | Smaller inshore operations vary. Licence inventory is not deck capacity or a promise to work that many in a weather-shortened day. |
| TSB investigation M16A0140, **2016** occurrence | Records gaffing a buoy, passing its rope through a boom block/hauler and bringing a pot to the rail for handling. [TSB investigation, history of voyage](https://www.tsb.gc.ca/eng/rapports-reports/marine/2016/m16a0140/m16a0140.html) | A documented mechanism, not an operating tutorial. A hauler raises gear; it does not sort animals, band claws, supply bait or carry crates ashore. |

**Recommended research fixtures:** a small synthetic operation with 100 pots and a Gulf-scale one with 272 pots, each revisited on a nominal 24-hour cycle; a separate delayed 48-hour case exercises weather interruption. These are workload scenarios, not new catch rates, crew speeds or guaranteed daily completions. The 272-pot value corresponds to the 2026 26A-1 PEI allowance already cited; minimum traps-per-line applicability must still be selected by subarea. Daily hauling can process deployed gear sequentially, so 272 deployed pots does not imply 272 pots stacked aboard at once.

For a future NPC planner, derive completed hauls from travel, buoy approach, line/depth/hauler limits, pot handling, animal-by-animal inspection/banding/stowage, rebaiting, resetting and return/unload time. A busy or full receiver blocks the same step for a skipper as for the player. Long soak does not promise linear extra catch or free storage. Do not replace current `TrapDef.SoakHours`, `HoursToFullPot`, catch capacity, depth bands or deterministic catch resolution with these observations.

## Proposed Def fields and implementation gates

Reuse existing species, `TrapDef`, `DeckWorkDef`, boat and goods identities. New candidate types below are review proposals, not reserved schemas or a parallel trap catalog. Every authored entity has one file and a stable id; cross-module coordination goes through Core.

| Authoritative record | Candidate fields and units | Acceptance before a later implementation |
|---|---|---|
| `TrapDef` physical-profile reference | Goods profile id; `Outside`/access envelope mm; shell, ballast and wet-added mass g; catch-placement/profile ids; gear attachment and support capabilities | Measured revision; gross mass counts gear, water and contents once. Keep current visual/stack fields under their existing ownership. |
| `RopeDef`, `BuoyDef`, anchor/ballast goods profiles | Length mm or m with one declared conversion; diameter mm; mass g; mass/length; displacement and force units explicitly named; sinking/floating and attachment policies | Finite positive measurements, compatible ends, and rated configuration. Do not label breaking force, safe working load and a hauler's pull as interchangeable. |
| `PotSetDef` template | Stable Def id; allowed trap/rope/buoy/anchor Def ids; ordered connection template, permitted counts and authored segment lengths | Immutable equipment choices and topology constraints; no runtime instance ids or changing deployed state inside a Def. |
| `PotSetState` instance topology | Set instance id; actual ordered trap/rope/bridle/buoy/anchor instance ids and connections; current supported/deployed state and soak anchors | No cycles, orphan attachments or multiply owned gear. Runtime topology/soak state belongs to a separately reviewed persistence contract. |
| Retention/gear policy by area and revision | Trap/entry/escape limits; degradable panel requirement; licence maximum versus actual deployed count; minimum traps per line and subarea applicability; surface-rope/marking policy ids | Keep legal envelopes distinct from equipment specs. Applicable conditions must be complete before a real-area profile is enabled. |
| Hauling and deck-work capability profile | Boat/support/access ids; hauling reach/depth, line compatibility and rated pull; working support and transfer route; required crew capability | No invisible extra hands or carrier, unsupported elevation, assumed rail contact, or machine lift inferred solely from boat length. Exact geometry is an owning-lane input. |
| Soak/tending and NPC work policy | Existing catch timing referenced once; intended revisit interval, maximum interval, any daily calendar restriction; travel/work budget and stopping conditions | Research uses real elapsed hours/days; simulation consumes explicit game-time units through the existing clock. Preserve seeded determinism and observation-independent results; no wall clock or hidden RNG. |

Future acceptance examples: a set valid at low tide fails at predicted high water when line is short; sinking/floating segments retain distinct properties; retrieving one pot cannot duplicate the rest of the string; wet ballast and rope remain in vessel payload after catch removal; a full live crate blocks a put with the animal still held; weather-delayed work records an incomplete cycle; repeat hauls reuse the same identified trap under the selected tending policy. These specify later tests and do not claim a current Unity run.

The [manual goods delivery order](goods-handling/06-phasing.md) and [hired-hands proposal](goods-carrying-staff-proposal.md) govern phasing. Full NPC deck work reuses the proven physical route; hiring and offline work remain M3. Before authoring, confirm one actual local trap/rope/buoy configuration, its wet masses and support/stack ratings, current subarea conditions and measured working route. These missing equipment inputs do not justify changing art, saves or existing tuning in this reference task.
