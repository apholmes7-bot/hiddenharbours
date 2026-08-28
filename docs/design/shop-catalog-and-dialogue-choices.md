# Hidden Harbours — Shop Talk & the Catalog (choices in the bubble, a wares book on the counter)

> **Status: BUILT — designed 2026-08-23, rulings taken 2026-08-27, PR 1 shipped the same day.**
> The design-and-mock half came first (§1–§11 below); the owner ruled §9 on 2026-08-27 and PR 1 built
> it. **Where this document and the build disagree, the build is right and the disagreement is marked
> ⚠ AS BUILT inline.** Four such marks: R2 (§3.2), the module boundary (§2.4), the phasing (§8) and
> the dev keys (§6).
>
> **Still not built, and still proposals:** the CLERKS who should be opening this book (PR 2 —
> Marguerite at the island store, a clerk at the creek chandlery), and everything in §7 and §11 that
> is not ticked.
>
> Design module. Subordinate to [`../vision-and-pillars.md`](../vision-and-pillars.md) (CANON — wins on
> conflict), to [`diegetic-ui-and-inventory.md`](diegetic-ui-and-inventory.md) (the ratified keystone:
> information is an earned instrument) and to
> [`dialogue-and-knowledge.md`](dialogue-and-knowledge.md) (the ratified conversation law: **no
> portraits, ever**; knowledge lives in things and people). Implements the surface
> [`../adr/0039-quiet-hud.md`](../adr/0039-quiet-hud.md) ruled the player's main UI — the notebook —
> for a second reader.
>
> Siblings: [`diegetic-devices.md`](diegetic-devices.md) (§4.4 + R5 already ruled on browse-vs-buy for
> the for-sale apps — this doc is the **buy-in-person** half of that same seam),
> [`economy-and-business.md`](economy-and-business.md) §9.1a (the St Peters opening the vendors were
> built for), [`progression-and-housing.md`](progression-and-housing.md) §4.3 (commercial property —
> the *lots* and *businesses* sections), [`ux-and-mobile-controls.md`](ux-and-mobile-controls.md) §4
> (layout, redundant coding, accessibility — binding on every row this panel draws).

---

## 1. The brief (owner, 2026-08-23)

> "Dialogue UI gains choices (Animal Crossing rule: no portraits); shop talk lives inside it; a catalog
> panel (notebook-styled, ADR 0039) for browse-and-buy: lots, businesses, tools, vehicles, boats, gear
> — data-driven from Defs with a catalog tag."

Three asks, and they are not three equal-sized pieces of work. §2 says why.

---

## 2. What exists today (grounded in the code, not remembered)

**Read this section before estimating anything.** Two of the three asks are further along than the
brief assumes, and one of them is a bigger change than it sounds.

### 2.1 Dialogue choices are BUILT — and they already obey the Animal Crossing rule

Shipped 2026-08-17 (`dialogue-and-knowledge.md` §4). In the tree right now:

- **`DialogueOption`** (`Code/World/DialogueOption.cs`) — a `[Serializable]` row: `Id`, `Label`,
  `ReplyLines[]`. Authored as data on **`DialogueDef.Options`**, so a choice is an inspector row and
  never a C# branch (rule 2).
- **`DialogueOptionPicker`** — pure, EditMode-tested. Move-axis latched selection, wrap at both ends,
  `Confirm()` takes no argument so it structurally cannot confirm a row the cursor is not on.
- **`RowsFor()` appends "See you later." always last**, and reserves `option.close` so no authored
  conversation can ship without a way out.
- **Picking publishes `Core.DialogueOptionPicked(dialogueId, optionId, speakerId)`** and World branches
  on nothing (rule 4).
- **No portrait exists to remove.** `DialogueLine.Portrait`, `Interactable._portrait` and the region
  builders' `Art/Portraits` loads were all deleted with the panel. The bubble anchors at the speaker,
  the tail tracks them, the fill *is* the audio event, and the kit at
  `Art/UI/DialogueBubble/` is **baked** — panel, tail in six orientations, gold pill, four cursors.

> **⚠ So "dialogue UI gains choices" is not new work.** What is genuinely new is **one** thing: a
> picked option today is *terminal* — its reply plays and the conversation ends. Shop talk needs a row
> that **hands off and comes back** (§3.2). That is the only change to the dialogue system in this
> whole document, and it is small.

### 2.2 There is already a buy layer — and already a screen the doctrine forbids

`Code/Economy/` carries a complete purchase stack:

| Piece | What it is | Verdict |
|---|---|---|
| `BuyLogic` | Pure quote rules — `Gear`/`License`/`Pot`/`Bait`/`Supply`/`Instrument`/`Boat`, returning `BuyQuote{Kind, Price, Owned, Affordable}`. No Unity. | **Keep, untouched.** This is the economics and it is already right. |
| `Shipwright`, `GearShop`, `PotShop`, `BaitShop`, `SupplyShop`, `InstrumentShop`, `LicenseVendor` | MonoBehaviours owning `TryBuy()`/`TryRepair()` — the wallet spend, the save write, the Core event. | **Keep the seams.** Any panel is a skin over these, never a second implementation. |
| `ShipwrightOffer`, `GearOffer`, `PotOffer`, `InstrumentOffer` (+ `BaitDef`, `SupplyDef`, `LicenseDef` used directly) | `ScriptableObject` offers with `Id`/`DisplayName`/`Flavor`/`Price`. | **These are the Defs the brief means.** They already exist and are already the "for sale" layer. |
| `BuyCatalog` | Builds `BuyRow`s by scanning **`stall.GetComponents<T>()`** for each vendor type. | **This is the thing that changes.** See §2.3. |
| `BuyScreen` | A 920×540 dark-slate uGUI overlay, gold-on-charcoal, "For Sale" in a system font. | **This is what the panel replaces.** |
| `BuyPointInstaller` + `DevBuyInput` | Self-installing scanner that bolts a **dev `P` key** onto every vendor in every loaded scene. | **Retires** when a conversation opens the book. |

`BuyScreen`'s own header calls it *"a skin over the purchase flow"* and `BuyPointInstaller`'s calls
itself *"removable wholesale when the real Interact intent lands."* Both were written expecting this
document.

### 2.3 The one structural fact that decides the work

**Today, stock is a scene fact, not a content fact.** `BuyCatalog.Build(stall, …)` reads whatever
vendor components a level designer stacked on a stall GameObject. Adding a boat to a yard is therefore
a **scene edit**, and the offer asset alone can never say where it is sold.

The brief's *"data-driven from Defs with a catalog tag"* is precisely the inversion of that: the
listing says where it is sold, and no scene is touched. That is the real change in this document, and
§4 is its shape.

### 2.4 The notebook — the surface this borrows

> ⚠ **AS BUILT.** The three pure files named below now live in **`Code/Core/Notebook/`**, not in World,
> and `DialogueBubbleKit` and `HarbourType` moved to `Code/Core/Art/` with them. The reason is not
> tidiness: `HiddenHarbours.Economy` and `HiddenHarbours.World` both reference only Core and nothing in
> the project references World, so "share `NotebookInk`/`NotebookKit` exactly as `QuestPanelPresenter`
> does" **does not compile** for a book that lives where the buy stack lives. `QuestPanelPresenter` is a
> misleading precedent for exactly this reason — it is inside World. Adding a World reference to Economy
> would be a feature module reaching into another feature module's concrete classes (rule 4), so the
> book's *hand* became a Core surface language instead. `NotebookKit` was not pure as this section
> assumed: it aliases six type metrics from `DialogueBubbleKit`, which being in the same namespace it
> needed no `using` to do.

ADR 0039 ruled the notebook the player's main UI. `Code/World/` holds a complete, pure, tested book:
`NotebookLayout` (wrap → blocks → placements → leaves → spread), `NotebookKit` (the pixel geometry:
5 px cells, 10 px pitch, a `5c+29` page, 8 fore-edge tabs, 5-character chips, integer scale only),
`NotebookInk` (cover, paper, rule, three ink weights, gold, tab stock) and a baked 26-piece art kit at
`Art/UI/Resources/Notebook/` — including `Notebook_Stamp_Done`, which §5.3 spends.

`QuestPanelPresenter` is the precedent that matters: **a second surface already draws in the book's
hand** — the current-quest leaf, pinned lower-right, sharing `NotebookInk` precisely so the two can
never drift a shade apart. The catalog is the third such surface, not a new visual language.

---

## 3. The design

### 3.1 The Animal Crossing rule, stated so it survives contact with a shop

The ratified law is that **the character on screen is the portrait**. A shop is where that law is
usually broken — a merchant UI is the classic place a face gets pasted into a box beside a grid of
items. It does not happen here:

- **No portrait, no name plate, no merchant frame.** The seller is standing there, animating, facing
  you, and the bubble's tail points at them.
- **The book does not replace them.** It opens *low*, over the lower half of the screen, and the
  speaker and their bubble stay visible above it. You are looking at a book someone handed you, in a
  place, with them still in it.
- **The seller keeps talking.** Picking a row is a beat they can answer (§3.3) — "she's sound, that
  one" — in the same bubble, in the same hand, while the book is open.

### 3.2 Shop talk lives inside the conversation

There is no shop *verb*. There is a person, and one of the things you can say to them is that you'd
like to see what they have.

```
  [bubble]  "Morning. Tide's dropping — good day for it."
            ▸ What have you got?          ← authored row, opens the book
              Any word on the cod?        ← authored row, plays a reply (built today)
              See you later.              ← appended by the picker, always last (built today)
```

**The seam, and it needs exactly one new field.** A `DialogueOption` gains a nullable pointer at a
*catalog view* — the seller and the section to open on. Everything else already works:

1. The picker confirms the row and publishes `DialogueOptionPicked` exactly as it does now.
2. **Economy listens** — it is the first subscriber that signal has ever had — and opens the book.
   World still branches on nothing and still names no economy type (rule 4).
3. The conversation **holds** rather than ending: the bubble stays, dimmed, and the picker is gone.
4. On close, **the picker comes back on the same rows** and the conversation carries on normally from
   there — the way out is still last, and taking it ends the conversation as it always did.

> ⚠ **AS BUILT (owner ruling, 2026-08-27).** Step 4 originally read *"the row's `ReplyLines` play and the
> conversation ends normally."* The owner ruled the other way: closing the book **re-arms the picker**,
> so `browse → sell → "See you later."` is one conversation with one person. PR 2's clerk needs it —
> her sell row is unreachable after a browse otherwise, and you would have to walk up and talk to her
> twice to buy a thing and then sell a thing. It is still ONE ROUND (rule 8): the rows that come back
> are the rows that went down, never a different second picker.

> **This is still one round.** A catalog row never leads to a second picker, so the flat-and-one-round
> rule (`DialogueOption.cs`, rule 8) holds: a dialogue TREE is still the M2/M3 knowledge-graph work.
> What changes is only that one kind of row is **deferred-terminal** rather than terminal. If the owner
> would rather not touch the dialogue system at all, the fallback is that the row ends the conversation
> and the book opens after it — cheaper, and it loses the "come back if you change your mind" beat.
> **R2.**

### 3.3 The catalog is the SELLER'S book — not hers

This is the load-bearing choice in the document, and it is what makes a browse-and-buy panel legal
under a doctrine whose test is *"would a menu do this?"*

Her notebook (`N`) is **hers**: tasks, knowledge, her own lines. A shop's stock is not hers and must
never appear in it. So the catalog is **the second book** — the wares book the chandler keeps under
the counter and turns around for you. Same stock, same rule, same ink, same 5-px hand; **different
cover, and it is not on your key**.

Everything follows from that one idea:

- **It is opened by a person, and closed back to a person.** No key summons it. A book with nobody
  holding it is a menu.
- **Its tabs are that seller's stock**, not a fixed six. The chandler's book has GEAR · TOOLS · BAIT.
  The yard's has BOATS. The land agent's has LOTS · TRADE. A seller with one section shows one stub —
  which is also how a single counter is a whole general store, exactly as `BuyCatalog`'s header
  already promised.
- **The right leaf is the entry, not a hover card.** Left leaf lists; right leaf is the written-up
  page for the row the cursor is on: name, price, the seller's own note, and the condition line.
- **Ownership is a stamp, not a grey.** `Notebook_Stamp_Done` across a bought line, and the price
  crossed out in her hand. `ux-and-mobile-controls.md` §4 forbids colour-alone state — a stamp, a rule
  through the price and a status sentence are three codings of the same fact, which is what that
  section asks for.
- **Money sits in the book's head, on every tab** — the same rule ADR 0039 §2 wrote for the notebook,
  in the same place, so the balance never has two spellings.

**What it is NOT.** It is not the phone's for-sale app. `diegetic-devices.md` §4.4 already ruled that
seam and R5 recommends **browse remotely, transact in person**: an app that closes the deal *"makes
every one of those yards a room nobody enters"* and serves no pillar. This panel is the in-person half
— the same listing data, read at the counter — so when the app is built it is a second reader over §4,
not a fork. The catalog is what makes R5 buildable, not what pre-empts it.

**Pillars.** P4 (you can see the rung above you and what it costs, long before you can afford it) ·
P3 (the seller stays a person in a place; the yard stays a room you enter) · P2 (the ladder from a
`₲60` rod to a `₲1,800` punt to a lot is legible in one hand) · P5 (a price you cannot meet is a
plain, kind sentence, not a locked door).

---

## 4. The data model — the catalog tag

**One `[Serializable]` block, added to the offer Defs that already exist.**

```csharp
[System.Serializable]
public struct CatalogListing
{
    public bool Listed;            // THE TAG. False (default) = invisible to every book. Fails closed.
    public CatalogSection Section; // Lots · Businesses · Tools · Vehicles · Boats · Gear
    public string[] Sellers;       // seller ids that stock it. Empty = listed nowhere yet (a draft).
    public int SortOrder;          // ties break on Id, so an unset order is still deterministic.
}
```

```csharp
public interface ICatalogListing        // implemented by the offer Defs, all inside Economy
{
    string ListingId { get; }
    string ListingName { get; }
    string ListingFlavor { get; }
    CatalogListing Catalog { get; }
}
```

**Six sections, from the brief.** `Lots` · `Businesses` · `Tools` · `Vehicles` · `Boats` · `Gear`.
Append-only, like every other shipped enum here. Chips clip at `NotebookKit.ChipChars` = 5, so the
lettering is **LOTS · TRADE · TOOLS · RIGS · BOATS · GEAR** — with `RIGS` for vehicles because that is
what a truck is called on this coast.

**Four rules that keep this honest:**

1. **The tag goes on the OFFER, never on the gameplay Def.** `VehicleDef` has no price and must not
   grow one: it lives in `HiddenHarbours.Vehicles`, and a price on it would put an economy concept in
   another module's content (rule 4). A vehicle for sale is a **`VehicleOffer`** in Economy pointing at
   `vehicle.*` by id — the same shape `ShipwrightOffer` already uses for boats. `LotOffer` and
   `BusinessOffer` are the two other new assets, and `BusinessOffer` should point at M2-41's
   `WharfBuildingDef` rather than restate it.
2. **The listing names the seller; the scene does not.** This is the inversion §2.3 named. Adding a
   boat to the yard becomes *one new asset* — no scene edit, no prefab touch, no merge on a `.unity`
   file (rule 9). It is also what lets one listing be stocked by two ports without duplicating it.
3. **Loaded the way the book already loads.** `Resources.LoadAll` under
   `Assets/_Project/Data/Resources/Catalog/`, **sorted, null-filtered, resolved on every open and never
   cached** — `NotebookContentSource` is the pattern and its reasoning transfers verbatim: a listing
   added while the book was shut is simply there next time, with no cache to invalidate. Cost is a
   `LoadAll` of small text assets on a page the player is about to read, not a frame-budget item
   (rule 7).
4. **`BuyLogic` still quotes, and the vendors still sell.** The panel resolves `BuyQuote` through the
   existing arms and routes Confirm through the existing `TryBuy()`/`TryRepair()` seams. Money, save
   writes and Core events do not move. **No new purchase economics are written in this work.**

> **⚠ The one thing §4 does not solve.** If the seller is an id and the stall components are no longer
> scanned, *something* must map `seller.harbourmaster` → the component that owns `TryBuy()`. That is a
> real seam and it is **R1** — the only question in this document that could change the shape of the
> code rather than the shape of the screen.

---

## 5. The panel

### 5.1 Geometry — the book's own, at the book's own scale

Nothing new is invented. `NotebookKit`'s formulae are the layout: a page is `5·cols + 29` px wide and
`10·lines + 24` tall, a spread is `10·cols + 100`, tabs are 5-character chips down the fore-edge to a
maximum of eight, and **scale is an integer** — a fractional one *"puts the hand on a half cell and the
wobble stops reading as a hand and starts reading as a bug."* The catalog fits itself to the lower
screen the same way `NotebookLayout.Fit` fits hers to the room.

### 5.2 The spread

```
┌──────────────────────────────────────────────────────────────┬──────┐
│  MacAulay & Son — Chandlery                    ₲ 1,240      ││ GEAR │  ← head: seller, then the
├──────────────────────────────────────────────────────────────┤ TOOLS│    balance, on every tab
│                              │                               │ BAIT │    (ADR 0039 §2)
│  ▸ Fishing Rod        ₲ 60   │   FISHING ROD                 │      │
│    Hand-line          ₲ 12   │                               │      │  ← fore-edge stubs =
│    Gaff               ₲ 25   │   A proper rod and reel — the │      │    THIS seller's stock
│    Oilskins   ~₲ 90~  [PAID] │   step up from a hand-line.   │      │
│    Depth Sounder     ₲ 140   │                               │      │
│                              │   Fits any hull.              │      │
│                              │   ─────────────────────────   │      │
│                              │   ₲ 60          ▸ Buy her     │      │
└──────────────────────────────┴───────────────────────────────┴──────┘
   the list                       the entry, written up
```

- **Left leaf, the list.** One ruled line per listing, price right-aligned on tabular figures. The
  cursor is the book's own `Notebook_Current_Arrow` / gold pill — the same selection language the
  option picker uses in the bubble, so choosing a row of stock and choosing a line of dialogue feel
  like one gesture.
- **Right leaf, the entry.** Name on the title lane, the seller's blurb in her hand, then the notes
  `BuyCatalog` already computes and which are some of the best writing in the economy layer: *"Sold
  as-is — needs ₲300 of repairs before she'll sail"*, *"You own 4 — 2 in the water"*, *"Fits to
  boat.dory"*, *"You have 12 in the box."* They move over unchanged.
- **The buy line.** Bottom of the right leaf: price, and the action in the seller's words — **Buy her**
  for a boat, **Take it** for gear, **Put her right** for a repair. `BuyQuote.Kind` already carries
  which.
- **More rows than a leaf holds** turn the page on the book's existing 3-frame turn — `Notebook_Mark_Next`
  is already baked and already means this.

### 5.3 The three states of a row, each coded three ways

| State | Ink | Mark | Sentence |
|---|---|---|---|
| **Affordable** | full ink, gold pill on the cursor | — | the buy line, in the seller's words |
| **Owned / held** | price ruled through | `Notebook_Stamp_Done` | "Already yours." |
| **Too dear** | price in `InkFaint` | — | "You're ₲540 short." — *the shortfall, not a refusal* |

The third row is the P5 one. `BuyScreen` says *"You can't afford this yet."*; a book says how much
short, because that is a number the player can plan against and the panel already knows it.

### 5.4 Input — no new binding, because there is none left

The A–Z dev-key ledger was swept 2026-08-17 and is **spent**. Nothing here asks for a key:

- **Open** — a dialogue row. **Close** — Esc / gamepad East, the shared Cancel.
- **Move** the cursor, **Interact** to confirm: the `DialogueOptionPicker` axis-latch rule, reused
  rather than re-derived, so a held key cannot rip through four rows in four frames.
- **Tabs** turn on the book's existing stub controls.
- Pointer works throughout, as it does on every other screen.

### 5.5 Budget (rule 7)

Rows rebuild **on open, on tab change, and after a purchase** — never per frame; the same discipline
`BuyScreen.Refresh` already keeps. Text is set once per rebuild, digits on `tabular-nums`, and the
kit's sprites are already loaded for the notebook, so a second book costs no new texture memory.

---

## 6. What this retires

- **`BuyScreen`** — deleted. It is the "would a menu do this?" test failing in 466 lines.
- **`DevBuyInput` + `BuyPointInstaller`** — deleted, with the `P` key returned to the ledger. Their own
  headers say they are placeholders for this.

> ⚠ **AS BUILT (PR 1).** They did **not** retire in PR 1, and could not: `DevBuyInput` opened
> `BuyScreen`, and `BuyScreen` called the `Build(stall, …)` signature the inversion removes — so "delete
> the screen, keep the dev keys" was self-contradictory. `DevBuyInput` was instead repointed: it reads
> the seller id off its stall's vendor and publishes the **same** `CatalogViewRequested` a dialogue row
> publishes. The dev key and the conversation now reach the book through one door, so PR 2 removes a
> *caller*, not a path.

> ⭐ **AS BUILT (PR 2) — RETIRED, and it was THREE files and TWO keys, not two and one.**
> `BuyPointInstaller`, `DevBuyInput` **and `DevSellInput`** are deleted. The commissioning handoff said
> "P and O"; **`O` was never this — it is the displaced-water key**, and the sell placeholder was on
> **`B`** (`DevSellInput.Update`, `bKey`). **`P` and `B`** are the letters returned, recorded in
> `DevBoatPicker`'s tooltip (where the ledger lives) and in the six other tooltips that transcribe it,
> so no stale copy can be swept against.
>
> Their six `AddComponent` sites across three builders are gone, and the four doc comments that named
> them now name `StallReach` — which **stays**: the proximity gate is not placeholder work and
> `HomeDoor` and `GinnyFreezer` already lean on it.
>
> ⚠ The six components were also serialized into `Greybox`/`NineMileCreek`/`StPeters`, so deleting the
> classes would have left dangling script references until the next scene bank. Those six blocks are
> stripped from the scene YAML in the same commit — pure deletions (17 lines each: the `MonoBehaviour`
> document and the one `- component:` line pointing at it), which is exactly what the next builder run
> would have produced.
- **`BuyCatalog`'s component scan** — replaced by the tag sweep. **The quote arms and every note string
  survive** and move into the new source; they are the accumulated correctness of six vendor types and
  must not be retyped.
- **`SellScreen` is NOT in scope** and stays as it is. It shares `BuyScreen`'s dark palette and will
  eventually want the same treatment — a *sell* is a different beat (the market moves under you,
  `economy-and-business.md` §1.2) and deserves its own design pass, not a rename of this one. **Flagged
  so it is not silently inherited.**

---

## 7. What this does NOT build (rule 8)

Named so nobody reads permission into the section above:

- **No new purchasable content.** No lot, no business, no vehicle is *authored* here. §4 builds the
  shelf; what goes on it is the owner's, priced against `economy-and-business.md` §8.
- **No property or business gameplay.** Buying a building is **M2-42** over M2-41's `WharfBuildingDef`;
  leases are `progression-and-housing.md` §4.3. This panel can *list* them once they exist.
- **No dialogue tree.** Still flat, still one round (§3.2).
- **No phone app.** R5's browse-remotely half stays where it is.
- **No instrument grants.** ADR 0039's ⚠ stands: whoever authors the first tide clock still owes the
  ADR 0030 ownership ruling and a save-schema bump under ADR 0008. Listing an instrument does not pay
  that debt.

---

## 8. Phasing (a way to land it in two safe pieces) — ⚠ SUPERSEDED

> ⚠ **AS BUILT.** The A/B split below was **not** taken. PR 1 built both, because PR 2's clerks need
> `SellerId` to exist to open their own books, so Phase B could not wait behind a separate review. The
> resulting PR is large — four commits: the Core move, the dialogue row, the inversion, the book — and
> that size is the known cost of the decision, not an accident of it. The advice below is still sound
> for anyone landing a comparable surface who does *not* have a dependent PR queued behind it.

**Phase A — the book replaces the screen.** New panel, same `BuyCatalog` component scan. No data
change, no seller ids, no migration. Everything in §5 is visible and playable; `BuyScreen` dies.
*This is the piece the owner can look at and rule on.*

**Phase B — the tag replaces the scan.** `CatalogListing`, `ICatalogListing`, the `Resources` sweep,
the seller-id seam (R1), and the three new offer types. Stock becomes content; scenes stop carrying it.

Both are in-phase for M2 only once the owner's handoff says so. **A is worth doing first even if B is
ruled differently**, because a notebook-styled panel over the existing scan is strictly better than the
dark overlay and throws nothing away.

### 8.1 PR 2 — the people behind the counters, AS BUILT

Four things the plan did not cover, recorded here rather than left for the next reader.

**1. Marguerite already existed, and so did her day.** The brief asked for "a clerk at the St Peters
general store — an `NpcDef` + routine = f(clock)". She has been standing at that counter since #354
with a five-block routine that opens the shop at 07:00 and takes her upstairs at 21:00. PR 2 gave her
**the conversation** and moved nothing else. The brief's own beat — *"a closed store is a clerk who is
not there, no signage system needed yet"* — was therefore already true and cost nothing; it is pinned
by reading her shipped routine through `RoutineSchedule.BlockIndexAt`, the engine's own block rule.

**2. Nine Mile Creek's clerk is ANCHORED, and that is a ruling, not a shortcut.** The region has **no
routine engine at all** — no station table, no lane graph, no indoor stand-point; the whole
`RoutineDef`/`RoutineStations` machinery is St Peters-only, and `NineMileCreekPeople` says so in its own
header (*"ANCHORED, NOT SCHEDULED"*). The owner ruled (2026-08-27) that the creek's storekeeper matches
that shipped convention: **no store hours at Nine Mile Creek, she is simply there.** Standing up an NMC
station table is its own properly-priced world-content lane. **⚠ DEBT, unpaid:** until it lands, the
creek's shops cannot open and close, and its cast cannot walk anywhere.

**3. The creek's general store gets NO sell row, deliberately (R7).** Its lot carries a `GearShop` and
nothing else — no `Market`, no `FishBuyer`, no `WharfSellPoint`. R7 says the sell verb fronts a
counter's **existing** sell components; there are none, so a sell row there would mean writing new sell
economics, which this slice does not do. Fish is sold at the buyer's truck on the wharf, which is
Wendell's. Pinned by a test so the omission reads as a decision.

**4. A sell row's answer carries a number World cannot know**, so the crossing is two Core signals on
the `CatalogViewRequested`/`CatalogClosed` pattern: World publishes `CounterSellRequested`, Economy's
`CounterSellDesk` resolves the counter through the **same** `BuyCatalog.ArmsFor` lookup the book uses,
sells through `WharfSellPoint`'s existing seam, and answers `CounterSellReported` on the same publish.
**It reports facts, never words** — the payout and how many units left the hold — and the sentences
around them are authored on the option asset with `{payout}`/`{units}` tokens. That keeps
`FeeFronted`'s standing rule: *the economy never writes dialogue.*

> ⚠⚠ **THE SHIPPED SCENES DO NOT CARRY ANY OF THIS YET, AND THAT IS THE NORMAL STATE.** Region scenes
> are authored from the builders and banked separately by the owner's Build click; the last bank was
> **2026-08-23**, and PR 1's builder edits (the `CatalogBookPresenter` on `DialogueUI`, `_sellerId` on
> all seven vendor kinds) came after it. So in the scene bytes as they stand there is **no book to open
> and no seller id to resolve**, and the whole shop-talk surface waits on the next bank.
>
> The failure that would have been is a **soft-lock**: the catalog hold is released by `CatalogClosed`
> and by nothing else, and `Advance()` refuses while it is on — so a browse row published into a scene
> with no presenter traps the player in a dimmed bubble with no way out. `ConfirmOption` now asks
> `EventBus.HasSubscribers<CatalogViewRequested>()` first and falls through to the ordinary
> answer-and-end arm when nobody is listening, with a warning naming the fix. The sell row degrades the
> same way, to its empty-pail line. Both paths are pinned by PlayMode cases.

---

## 9. Owner rulings — **ALL TAKEN 2026-08-27**

> Every ruling below was taken **at the recommendation**, with one addition (R7) and one reversal of the
> doc's own text (R2, see §3.2). R1 · R3 · R4 · R5 · R6 as recommended; **R2 = hold AND re-arm**;
> **R7 = the clerk's sell verb fronts the store's existing sell components** (PR 2 spends it — the Nine
> Mile Creek general store has no sell components at all, so its clerk gets no sell row).

**R1 · How does a seller id find the thing that sells?** *(§4, the ⚠)*
With stock as content, `seller.macaulay` must resolve to the component owning `TryBuy()`. Options: a
`SellerId` field on the existing vendor components (smallest, keeps every seam); a registry keyed by
id (cleanest, one more Core surface); or vendors become stateless services taking an offer (best
long-term, biggest change). → **Recommend: `SellerId` on the vendors.** It is a one-field change, the
purchase flow does not move, and it can become a registry later without touching content.

**R2 · Does the conversation hold while the book is open, or end?** *(§3.2)*
→ **Recommend: hold.** It costs one deferred-terminal row and buys the "come back if you change your
mind" beat, which is the Animal Crossing shape the brief is asking for. Ending is cheaper and loses it.

**R3 · Is a second book right, or should stock be a tab in HERS?** *(§3.3)*
The whole design rests on this. → **Recommend: a second book.** A shop's stock is not her writing, and
a stock tab in her notebook is reachable on `N` from the middle of the ocean — which is a menu with a
paper texture. Ruling the other way is legitimate but it collapses §3.3 and most of §5.

**R4 · Is browsing itself gated?** *(the keystone rule, `diegetic-ui-and-inventory.md` §3)*
Every readout is earned. Is a *price list* a readout? → **Recommend: no.** A seller showing you their
wares is the oldest un-gated information there is, and gating it would make the ladder illegible and
cost P2/P4 the exact thing §3.3 says this panel buys. Recorded because the keystone rule deserves to be
asked, not assumed past.

**R5 · Are `Lots` and `Businesses` sections now, or headings held empty until M2-42?** *(§7)*
→ **Recommend: define the enum now, author nothing.** Append-only ids are cheap to reserve and
expensive to renumber; an empty section simply shows no stub.

**R6 · The seller's verbs.** *(§5.2)*
"Buy her" / "Take it" / "Put her right" — per `BuyRowKind`, or one plain "Buy"? Taste, and cheap either
way. → Suggest the per-kind verbs; it is one `switch` and it is most of the voice.

---

## 10. How it gets tested (qa-test, when built)

- **Pure and headless, like the rest of the book.** `CatalogSource` (tag filter, section grouping,
  seller match, deterministic sort) and the row states are EditMode subjects with no canvas — the same
  property that makes `HudVisibilityPolicy`'s truth table testable.
- **Content validation** (rule: touched `Data/` → run it): every `Listed` entry has an id, a section, a
  price and at least one seller; ids unique and append-only; **a listing whose `Sellers` names nobody
  fails loudly**, because a listing nobody sells is exactly the silent-empty-tab failure
  `QuestContentValidationTests` exists to make loud.
- **Layout** reuses `NotebookLayout`'s tested wrap/place/split, so the new tests are about *content*,
  not about drawing.
- **PlayMode**: talk → pick the row → book opens with the conversation held → buy → money and save move
  exactly once → close → the reply plays → the conversation ends.
- **A regression that matters:** a purchase made through the panel and the same purchase made through
  the vendor seam must leave identical save state. The panel is a skin; the test is what keeps it one.

---

## 11. Proposed backlog items — **PROPOSALS ONLY** (neither claimed nor scheduled)

| # | Item | Role | Shape | Blocked on |
|---|---|---|---|---|
| P-CAT-01 | Catalog panel — the seller's book (Phase A) | ui-ux | The spread, tabs, list/entry leaves, the three row states, over the existing `BuyCatalog` | R3, R6 |
| P-CAT-02 | Retire `BuyScreen` + `DevBuyInput` + `BuyPointInstaller` | ui-ux + economy-sim | Delete; return `P` to the key ledger | P-CAT-01 |
| P-CAT-03 | `CatalogListing` tag + `ICatalogListing` on the offer Defs | economy-sim + lead-architect | §4; append-only section enum | R1, R5 |
| P-CAT-04 | `CatalogSource` — the `Resources` sweep | economy-sim | `NotebookContentSource`'s pattern; sorted, uncached | P-CAT-03 |
| P-CAT-05 | Seller id seam | lead-architect | R1's answer, wired through the existing vendors | **R1** |
| P-CAT-06 | `VehicleOffer` · `LotOffer` · `BusinessOffer` | economy-sim | Three offer assets on the `ShipwrightOffer` pattern; `BusinessOffer` points at M2-41 | P-CAT-03, M2-41 |
| P-CAT-07 | A dialogue row that opens a catalog view | world-content + economy-sim | The one new `DialogueOption` field; Economy subscribes to `DialogueOptionPicked` | R2 |
| P-CAT-08 | Content validation for listings | qa-test | §10 | P-CAT-03 |
| P-CAT-09 | The chandler's book, authored | world-content | The first real seller: rows, blurbs, verbs | P-CAT-01, P-CAT-07 |

---

## 12. Cross-references — what this doc touches

- [`../adr/0039-quiet-hud.md`](../adr/0039-quiet-hud.md) — the notebook as main UI; the money-in-the-head
  rule §5.2 reuses; the instrument debt §7 refuses to pay.
- [`dialogue-and-knowledge.md`](dialogue-and-knowledge.md) — the ratified no-portrait law (§3.1) and the
  built option picker (§2.1). **§4's as-built record gains one line** if R2 is ruled "hold".
- [`diegetic-devices.md`](diegetic-devices.md) §4.4 + **R5** — browse remotely, transact in person. This
  is that ruling's in-person half; the app is a later reader over the same §4 data.
- [`diegetic-ui-and-inventory.md`](diegetic-ui-and-inventory.md) §3 — the keystone rule, asked properly
  at **R4** rather than assumed past.
- [`ux-and-mobile-controls.md`](ux-and-mobile-controls.md) §4 — redundant coding and accessibility, which
  §5.3 is written to satisfy.
- [`economy-and-business.md`](economy-and-business.md) §9.1a — the St Peters vendors this reskins;
  §8 for what anything new should cost.
- [`progression-and-housing.md`](progression-and-housing.md) §4.3 — the *lots* and *businesses* sections.
- `../../backlog/backlog.md` **M2-41 / M2-42** — the building Def and the buy-and-site beat P-CAT-06 waits on.
