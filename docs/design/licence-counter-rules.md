# Licence counter rules: current data and the limited-fishery proposal

The island store sells one shellfish licence for 15. It covers soft-shell clams, blue mussels and
American oysters. A cod licence costs 120 and is sold only at the mainland harbourmaster. Those are
the only two licences on this baseline. The player can attempt a catch without either; at landing,
a catch covered by a licence he does not hold goes back, with his spoken line and no catch record.

The rule that **every catch needs a licence is not fully authored yet**. Fifteen of the nineteen
species have no licence mapping and therefore currently bypass this release rule. Ten of those
species occur in the island roster. Cod has a mapping but no island seller. The exceptions below
make these gaps visible; they do not approve them as permanent exemptions.

The [limited-licences design](limited-licences-and-crewing.md), merged in PR #1493, describes a
later system: a fixed set of individually held rights, passed on rather than created, with the
player able to work under another holder. Main cannot represent those rights yet. This audit adds
documentation and tests only. It adds no registry, licence asset, runtime rule or saved field.

Baseline: `541276cbc9308ba60ded8b905308366306438570`, save version 25. The island-only first week
and demo are the governing direction. The older comments describing a first-week mainland cod
purchase are historical. The numbers and disputed choices in #1493 remain proposals, not approvals.
The pinned-main merge changes no fish, licence, seller or permission-query input to this
audit. All exact inventories and named exceptions remain unchanged after the merge.

## 1. What is actually for sale

Both assets are under `Assets/_Project/Data/Resources/Catalog/Licenses/`. Both are listed in
catalog section `Gear`, sort order 50. Prices below are game-money integers, read from the assets.

| Def / stable ID | Display name | Price | Seller / keeper | Species permitted |
|---|---|---:|---|---|
| [ClamLicense.asset](../../Assets/_Project/Data/Resources/Catalog/Licenses/ClamLicense.asset), `license.clam` | Shellfish Licence | 15 | `seller.leblancs` / `npc.marguerite_leblanc`, island general store | `fish.soft_shell_clam`, `fish.blue_mussel`, `fish.american_oyster` |
| [CodLicense.asset](../../Assets/_Project/Data/Resources/Catalog/Licenses/CodLicense.asset), `license.cod` | Cod Fishing License | 120 | `seller.nmc_harbourmaster` / `npc.nmc_harbourmaster`, mainland counter | `fish.atlantic_cod` |

[SellerDefs](../../Assets/_Project/Data/Resources/Sellers/) carry stable seller, keeper and trade
IDs. They have no region field. Island availability here means the existing island general-store
counter, `seller.leblancs`; it is not inferred by parsing seller names. Neither the outboard seller
nor the other mainland counters solve an island-week gap. A future second island counter needs an
explicit addition to this content contract.

There is no stock quantity on a licence. A counter purchase spends `Price` and grants its ID once;
another purchase of an already-held ID is refused without charging. There is no resale operation.
`Catalog.Listed` and `Catalog.Sellers` control book availability, not how many rights exist.

## 2. How main asks whether he may keep the catch

1. [LicenseDef](../../Assets/_Project/Code/Economy/LicenseDef.cs) owns `PermittedSpeciesIds`.
   A fish or gear method does **not** carry a separate required-licence reference. Permission is
   species-wide: the same mapping applies to each of that fish's `AllowedGear` flags.
2. [CatalogSource.Licenses](../../Assets/_Project/Code/Economy/CatalogSource.cs) loads every licence
   from Resources, including unlisted ones. Seller books separately filter with `IsStockedBy`.
   An unlisted class licence can therefore gate landing without appearing in a book.
3. [CatchLicensePolicy.RequiredLicenseFor](../../Assets/_Project/Code/Economy/CatchLicensePolicy.cs)
   returns the **first** licence listing the species. `MayLand` asks Core `ILicenseService.IsLicensed`
   for that ID. No mapping means allowed; a mapping with no wallet means refused. There is no
   authored overlap or alternative-permission field, so duplicate permission entries are errors,
   including a duplicate inside one licence. Ordering is not an intentional-overlap declaration.
4. [LicenseService](../../Assets/_Project/Code/Economy/LicenseService.cs) checks an exact-ID set.
   It bootstraps after scene load, mirrors the existing `OwnedLicenses` list, and saves a new grant.
   It has no class-to-instance resolution, holder, transfer, operator, place or time query. `Grant`
   accepts arbitrary nonempty IDs; idempotence is not a finite-world ownership guarantee.
5. [UnlicensedCatch](../../Assets/_Project/Code/Fishing/UnlicensedCatch.cs) checks the component's
   handed licence list and the shipped list; refusal from either requires release. The shipped
   list is consulted when a live wallet exists, or when tests explicitly override the catalog.
   With neither a live wallet nor a handed mapping, bare fixtures remain ungated. The stricter
   `CatchLicensePolicy.Evaluate` helper is not the live landing path.
6. Release publishes `UnlicensedCatchReleased` and the inner-voice cue, rather than the successful
   catch event. Attempts, bites and digging remain possible. The existing integration tests check
   that the release does not enter the hold or Notebook.

| Landing path on main | Where the shared rule is asked | Current authored gate relevant to it |
|---|---|---|
| Rod | `FishingController.OnLanded` | cod on Handline; no currently licensed Jig species |
| Hand digging | `ClamDig` at dig completion | the three shellfish on ClamFork |
| Immediate trap haul | `PlacedTrap.TryHaul` | no shipped Trap species has a licence yet; synthetic gate cases already test release |
| Pot deck sorting / haul | `DeckPot` marks unlicensed animals returned; `PotDeckWorkController` emits the release | same missing lobster/crab class data; existing tests cover unlicensed sorting |
| Other authored gear | `FishSpeciesDef.AllowedGear`, plus crew/voyage work tags | cod also names Longline; Net and Dredge species currently lack mappings |

The last row is a **data validation**, not proof of a playable longline, net or dredge landing flow.
`LonglineCatch` leaves licensing to the eventual haul; a future landing path must use the shared
release rule. Crew `work.*` tags express boat work and staffing, not licence ownership.

The counter seam also has limits: [LicenseVendor.TryBuy](../../Assets/_Project/Code/Economy/LicenseVendor.cs)
does not enforce `Listed`, the seller, or a licence tier. Its public direct-purchase overload can
be handed any Def. With a null licence service it can spend money without granting. These are
existing boundaries to address before using this seam for scarce rights; hiding a class in the
book alone is not enforcement of “never sell this class”. No purchase code changes here.

## 3. First-week needs and named exceptions

#1493 §§1 and 3 require ordinary counter access on the island for the catches the demo is meant
to keep: the existing shellfish licence, cod, and the other rod fish. It leans toward counter
striped bass and green crab. It reserves lobster and rock crab for limited ownership; L0a calls
for **unlisted class gates**, not an island offer. A missing class is therefore a release-rule gap,
not a request to sell that limited right at the store.

The table includes every species whose current `RegionIds` contains `region.st_peters`, even
when its method or gathering interaction is future work. It is a conservative content inventory,
not a claim that all fourteen are obtainable in the first seven days.

| Island species | First-week requirement from the design | Current result / known exception and reason |
|---|---|---|
| Soft-shell clam, blue mussel, American oyster | ordinary shellfish counter licence | **Available:** `license.clam` at the island store; the existing fronted fee covers it |
| Atlantic cod | ordinary cod counter licence available before leaving the island | **Missing island offer:** `license.cod` exists but only names `seller.nmc_harbourmaster`; mainland access waits until about day 7 |
| Striped bass | counter licence is the §3 lean so the demo can keep its sport fish | **Missing Def/mapping:** proposal has not been authored |
| Green crab | cheap counter licence is the §3 lean | **Missing Def/mapping:** which class covers it remains undecided |
| Mackerel, rainbow smelt, winter flounder | ordinary rod-fish coverage under “every catch” | **Missing mappings:** class grouping, price and island listings are unspecified |
| Atlantic herring | coverage under “every catch”; its authored method is Net | **Missing mapping:** ordinary class grouping is unspecified; method data alone is not a shipped net loop |
| Atlantic surf clam, northern quahog | coverage under “every catch”; authored ClamFork method | **Missing mappings:** the existing shore licence lists only three species; widening it or adding a class has not been decided |
| Lobster, rock crab | limited class, never offered at a counter; release until authorized | **Missing L0a class Defs:** neither `license.lobster` nor `license.rock_crab` exists, so they currently bypass licence release |

Outside the island roster, **Atlantic tomcod, cunner, haddock and pollock** also have no mappings;
ordinary class grouping is undecided. **Sea scallop** has no mapping and deliberately has no
catalog gear under the existing trawl-arc contract. That is a named deferral, not a permanent
licence exemption. Bluefin, halibut and swordfish in #1493 are later proposals, not current species
assets. Shark is not proposed by that design.

The first-week test expects exactly three covered species and eleven named gaps (cod's seller
gap plus ten missing mappings). The full-roster test expects exactly fifteen missing mappings.
Resolving a gap must remove its exception and update this table in the same content change;
neither an unnamed omission nor a stale exception passes silently.

## 4. Each rule in #1493 against the current model

“Partial” means useful existing data or a landing seam exists, not that the proposed feature ships.
Every new field below is a design recommendation only. No save step is approved here.

| Design section / rule | Can main express it? | Missing representation or behavior; proposed home |
|---|---|---|
| §§1–3: unlimited ordinary counter licences, data prices and permissions | **Yes**, for the two authored classes | Finish the missing `LicenseDef` permissions and island `Catalog.Sellers`; class grouping is still a content decision |
| §1: attempt anything, release unlicensed catch, spoken reason, no record | **Partial**: shared rod/dig/trap release exists | Author the missing class mappings; connect future methods at landing; do not remove species from catch rolls |
| §3: counter / limited / leased-water distinction | **No explicit tier** | Add an acquisition policy to class data if needed by purchase validation; an unlisted catalog row alone does not enforce it |
| §4: fixed number, one file per right, starting NPC holder, never created at runtime | **No** | `LimitedLicenceDef`: `Id`, `ClassLicenceId`, `StartingHolderNpcId`; a read-only fixed catalog and deterministic holder resolution through Core |
| §4: water-specific right, existing season definitions, pot cap | **Partial**: season and region data exist; wallet has no context | `WaterId` with a specified ID type, season reference, `PotAllotment` on each limited Def; landing and setting context must include water, time and operator |
| §4: relative values and shares; lost pots release catch rather than keep fishing | **No limited-right fields** | `Value`, default shares on limited data; lost-pot lifecycle must deactivate its catch production and release contents, separate from the wallet |
| §5: hand openings seeded by day; season, weather, regular crew absence; gear week and setting day | **Partial**: NPC days, boat work and crew roles exist | Hand-day policy data: skipper/boat/right IDs, need schedule, work kind, prep pay and eligibility; session assignment with start/end; time/season/weather inputs |
| §5: 3 a.m. start, six working days, missed day affects trust, no new tiredness penalty | **No hand-day contract** | Departure/rest-day schedule and trust deltas in hand-day policy; participation/no-show facts; preserve existing sleep/hunger rules |
| §5: skipper owns catch, the hand is licensed under that holder, 15–25% share and trust unlocks | **No** | Active assignment identifies holder, operator and catch beneficiary; share/trust curve in policy; a permission query resolves that holder's right without granting it to the hand |
| §5: gauge, shorts/berried returns, canner/market grade, optional maximum female size, chilled delivery | **Partial**: current deck sorting already returns shorts/berried animals | Species market-grade threshold, optional per-water maximum size and applicability; work-stage/grade pricing data; manual stage/hold integration remains later work |
| §5.1: old holder lends 10 of 30 pots, one water/season, suitable boat, 40% share | **No** | Arrangement record: right, operator, water, start/end season, reserved pots, share and status; validate boat work and global cap rather than copying ownership |
| §5.1: renew, end after improper sorting, buy after two seasons at 70% or earn after four | **No** | Arrangement policy thresholds/discount, completed-season and conduct facts, transfer outcome; a transfer must preserve the same right ID |
| §6: retirement/bad season/no heir/debt; one or two sales per year; notices and closing date | **No** | Holder story facts on limited/story data; annual sale policy (seed inputs, rate, notice lead, deadline), derived sale events; no clock-time random draw |
| §6: seeded NPC bids, one binding offer, trust-based matching right, winner may be NPC | **No** | Offer spread and matching policy in data; player's pending offer and response facts; atomic money/holder transfer with a replayable result |
| §6: higher price for additional holdings, no hard cap; fish one of each kind, lease others at 30% or hire | **No** | Price curve per class; operator assignment/lease records with right, skipper, dates and share; idle rights yield nothing; staffing integration later |
| §7: seasonal tags; two bluefin keepers; reset on season change; release uses no tag | **No** | `TagsPerSeason` plus season policy on right data; usage indexed by right and season stamp; atomic keep/tag decision at landing, with a distinct release reason |
| §7: guests fight, holder operates boat; trip fee plus fish-raised fee; shared tag pool; still earn after tags exhausted | **No** | Charter trip/party/right/boat/crew state, raised-vs-kept accounting and fee policy; guest keeper debits holder's allowance exactly once |
| §7: seeded bookings, reputation, mate and suitable work/gear; mainland then city guests; farm tours | **Partial**: crew/work requirements exist | `CharterGuestPoolDef`: origins, offer lead, fees, reputation curve and trip kind; accepted bookings, reputation facts and roster/gear checks; tours reuse booking shape |
| §8: seed + game time + player-caused facts; replay non-player transfers and offers | **No registry yet** | Pure Core holder/sale calculations over immutable initial data and explicit player overrides; deterministic identities/order; snapshot state for a session prototype |
| §8: owned IDs and the eight additional fact categories | **Partial**: `OwnedLicenses` already stores strings | Limited IDs fit that list syntactically but need class resolution. Transfers, trust, arrangements, leases, offers, used tags, bookings and plots need the separately approved save design |
| §9.1: finite bay area, pre-authored NPC leases, grants until full, then resale/lapse, annual rent | **No** | `LeaseBayDef`: area, application fee/delay, rent; `AquaLeaseDef`: plot, area, starting holder; allocator must prevent overlap/overbooking and replay transfers |
| §9.2: crop calendar, one growth factor, whole-season rounding, minimum three seasons | **No licence fields for it** | Crop policy data (duration/method/season), global growth compression and pure growth calculation; reconcile the rounded examples before implementation |
| §§9.2–9.3: collector ropes, socks, lines, buoys, anchors, winch, ice saw, sleigh, insulated containers; gear determines hull capability | **Partial**: equipment and boat-work data patterns exist | Separate gear Defs with length, holding mass, line slots, buoy lift and winch capacity; fitted capability checks; manual small-line vs full-line work, sinking/loss and safe ice-day rules |
| §9.4: stage/run/plant supply chain, live oysters to outlets | **Partial**: referenced supply-chain Defs exist | Mussel processing-line content and manual handoff integration; no licence should teleport a harvest or imply a new processing line exists |
| §9.5: bottom and off-bottom oyster culture, flipping chore, bay flavour and price; no disease | **No farm contract** | Culture-method/crop/maintenance policy; flavour note and price multiplier on `LeaseBayDef`; deliberately omit disease |
| §§9.6–9.7: manual first, then hired hands/manager; wild picking differs from private plots; NPCs respect his plot | **No plot-sensitive permission** | Catch origin/plot ID and holder context, independent wild-licence and plot-access checks; saved setting facts for pure growth/fouling/yield; later worker assignments |
| §10: Core queries/events, tag consumption, release reasons and pure tests | **Partial**: licence service and release signal exist | Proposed registry/allowance interfaces and events; query inputs must carry context, not only species; future tests must cover conservation, replay and seasonal boundaries |
| §11: L0a/L0b and optional A0 only in week one; later registry, sale, charter and lease slices | **Expressible as scope**, not runtime data | Class assets and dialogue remain separate small content slices; no later subsystem or scenery is included in this audit |
| §§12–13: owner leans and unverified claims | **Unresolved decisions** | Keep placeholders in future Defs; do not treat proposed holders, values, species tiers, save bump, geography or real-world comparisons as approved facts |

The new fields belong to these responsibilities, rather than all being added to `LicenseDef`:

- **Class content (Economy):** species permissions, acquisition policy and counter listing. Keep
  limited **instances** separate so several rights can share one species gate without duplicate
  `PermittedSpeciesIds` rows. If intentional alternate permissions are ever needed, author an
  explicit group/mode and evaluate it; current first-match behavior cannot express that intention.
- **Limited-right content (Economy, exposed through Core):** stable instance ID, class ID, typed
  water/season references, initial holder, pots, tags, value and story/share policy references.
  The Def describes the starting holder; changing ownership must not mutate the content asset.
- **Pure Core state/query inputs:** world seed, game time, right ID, current holder, operator,
  beneficiary, water/plot, species, method, trip/assignment, season stamp and keeper intent.
  Exact shape should be settled at L1. `IsLicensed(classId)` alone cannot distinguish the wrong
  water, a hired hand, an expired arrangement or someone else's farm. Reading mutable global
  “current water” would hide that dependency rather than solve it.
- **Player-caused records (session snapshots first):** transfer overrides, trust, arrangement
  and lease terms, bids, tag use, accepted bookings, plots and line/cage setting facts. A later
  save step needs an approved schema, migration, round-trip and replay tests. No such rows are
  added here; the existing owned-ID list cannot reconstruct these facts by itself.
- **Farm/charter/hand-day policy data:** the fees, schedules, growth, work, pay and reputation
  fields listed above belong with those systems. Core interfaces/events carry their results
  across modules; fishing should not acquire a concrete registry dependency.

Required future invariants: the set of limited IDs is constant during a world; each has exactly
one holder at any instant; a sale/award/lease cannot mint or duplicate it; employment never grants
ownership; an allotted pot cannot be counted in two arrangements; released fish consume no tag;
personal and guest keepers share the same allowance; replaying a successful landing cannot spend
its tag twice. These require later pure logic tests, not assertions about features absent today.

## 5. Verification and limits of this change

Existing coverage was extended rather than replaced:

- [ShellfishLicenceAndToolsTests](../../Assets/Tests/EditMode/ShellfishLicenceAndToolsTests.cs):
  the existing no-overlap test now also checks stable unique licence IDs, nonempty permissions and
  species references. Two additional tests validate the runtime-visible catalog, all nineteen
  species and twenty-six authored species/method pairs, the five currently gated pairs, and the
  exact known exceptions and first-week offers. Empty and absent wallets release mapped species;
  granting the resolved ID permits them. These checks use shipped assets, not replacement Defs.
- [SellerContentTests](../../Assets/Tests/EditMode/Economy/SellerContentTests.cs): the existing
  catalog/seller validator now rejects a listed licence with no sellers or a negative fee and
  checks that each offer resolves through the runtime seller catalog. Existing seller/keeper
  uniqueness and reference checks remain.
- [UnlicensedCatchReleaseTests](../../Assets/Tests/EditMode/UnlicensedCatchReleaseTests.cs) and
  [LicenseTests](../../Assets/Tests/EditMode/Economy/LicenseTests.cs) already cover landing releases,
  spoken cues, record suppression, counter purchase and policy behavior. They are unchanged.

Static inventory, relative-link and change-boundary checks were performed. No editor, compiler,
EditMode test, player build or scene inspection ran locally; CI must compile and execute the
existing and extended suites. Thus this page proves the audited source/data inventory, not a
completed first-week licensing experience. It makes no claim to verify real-world fishery law,
prices, aquaculture practice or the design's pacing assumptions.
