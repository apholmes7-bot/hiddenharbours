# ADR 0039 — The quiet HUD: a number comes from an instrument you own

**Status:** **Accepted** · **Date:** 2026-08-22 · **Deciders:** owner (the ruling, made in play
2026-08-22), ui-ux (the build, [#639](https://github.com/apholmes7-bot/hiddenharbours/pull/639)),
lead-architect (this record) · **Supersedes nothing** · **Amends**
`docs/design/ux-and-mobile-controls.md` §4 (the always-visible table) ·
**Related:** ADR 0030 (helm instruments owned per hull), ADR 0025 (the instrument glass the helm
draws), ADR 0008 (the save schema an instrument grant will touch),
`docs/design/diegetic-ui-and-inventory.md` §3 (the keystone rule this is the first enforcement of,
ratified by the owner 2026-07-05), `docs/design/diegetic-devices.md` §5.2 (the notebook, and its
open ruling R10), the 2026-08-07 HUD windowing ruling (the clock's hide-only window)

## Context

The diegetic direction has had a keystone rule since the owner ratified it on 2026-07-05:
**every HUD readout is gated behind owning the instrument that produces it.** The doc that carries
it says so in its own header — the M1 always-on band is *scaffolding*, built to prove the loop is
fun, and the diegetic shape is what it grows into. The rule was ratified **in principle and not
built**, and it stayed that way for seven weeks while the band kept doing what it was written to do.

On **2026-08-22 the owner played the game and ruled**, in play: the band hands the player the tide,
the sea state, the wind and their balance for free, and that quietly deletes three different things
the game is otherwise about.

- **P1 (The Sea Has Moods)** — a tide height and a time-to-turn in the corner is the reason nobody
  looks at the water. The read the pillar wants is *the water against the piling*, and a number
  beside it always wins.
- **P4 (Earn It, Then Automate It)** — an instrument you can buy is worthless the moment its readout
  is already on screen. The band was pre-spending the entire instrument ladder
  (`diegetic-ui-and-inventory.md` §3.2) before a single rung of it was authored.
- **The notebook** — ruled the player's main UI surface on 2026-08-20 — is where a balance is kept.
  A balance also flashing on a band is two books, and the one that costs nothing to read wins.

This is therefore not a new direction. It is the **first place the ratified rule is enforced in
code**, and the moment three of the four band readouts stop being scaffolding.

## Decision

1. **Tide, wind and sea state are shown only from an instrument the player owns.**
   `HudInstruments` — `TideClock`, `Anemometer`, `SeaGauge` — is a flags enum in
   `Assets/_Project/Code/UI/HudVisibilityPolicy.cs`, and `MayShow(readout, owned, devShowAll)` is
   the whole rule. Deliberately no third state between "has one" and "does not".

2. **Money is not instrument-gated, because no gauge could earn it back.** It lives in the
   notebook's head, on every tab, and the payout the band used to flash is annotated beside the
   balance it moved and stands until she spends it — which is how a player actually checks what a
   load was worth. Written as its own arm in the policy (`Money => false`) rather than folded into a
   flag nobody sets, so the reason survives the next reader.

3. **One policy, not four `if`s.** `HudController` asks `HudVisibilityPolicy` and does what it says;
   no readout decides for itself. Each readout deciding for itself is exactly how a band gets its
   showing habit back — one new label, one forgotten check, and the tide is on screen again with
   nobody having decided it should be. Because the rule is a pure function, the truth table is the
   EditMode test's subject and needs no canvas to prove.

4. **The helm's instruments and the wrist-watch are exempt, and that is the ruling read forward, not
   a carve-out.** The chartplotter, the dash compass, the apparent-wind read and the nav cluster are
   instruments the boat *has* — which is precisely the thing the ruling says a number should come
   from. Suppressing them would be reading the ruling backwards. They never pass through this
   policy; where the nav cluster draws is still `HelmHudSuppression`'s answer alone. The clock is
   likewise untouched: it is a hide-only window under the 2026-08-07 windowing ruling, and she is
   wearing the watch.

5. **The current quest leaves its bottom-centre banner for a leaf off the notebook**, pinned
   lower-right, in the book's own stock, ink and hand. Same words and the same source —
   `OnboardingDirector` still decides the step. It stopped shouting in a system font; it did not
   stop existing.

6. **A dev menu restores everything — a menu item, not a key.** `Hidden Harbours ▸ Dev ▸ HUD` offers
   a blanket override or one pretended instrument at a time. Not a key because the A–Z key ledger is
   spent, and because this is a developer's switch rather than an input the game is gaining.

## Consequences

**Good.**

- **The shipped state is every band read off**, in a new game, exactly as the ruling asks — and it
  falls out of the rule rather than being asserted somewhere separately.
- **A hidden read does no WORK either** (rule 7). The tide's forward scan for the next turn is
  skipped entirely while nobody can read it; the quiet HUD is cheaper than the loud one, not the
  same cost with a renderer switched off.
- **Any future readout is gated by construction.** A fifth band element has to name itself in
  `HudReadout` to be laid out at all, and the policy's default arm shows an unknown readout
  *nothing*. Forgetting to think about the ruling fails closed.
- **`MoneyFormat` moved to Core** (`Assets/_Project/Code/Core/Economy/MoneyFormat.cs`) because the
  notebook shows a balance now and `World` cannot reference `UI` (rule 4). Two spellings of the
  currency sign is the drift nobody notices until a screenshot shows both.

**Costs and limits, stated plainly.**

- **No content grants an instrument yet, and that is the shipped state rather than an oversight.**
  No tide clock, anemometer or sea gauge exists as an authorable thing. `HudVisibilityPolicy.Owned`
  is set by nothing but the dev menu. Authoring the instruments — and pricing them against the
  economy and progression docs — is a **later phase**, and until then the only way to see a band
  read is the dev menu.
- **⚠ The ownership seam is NOT reconciled with ADR 0030, and must be before the first instrument is
  authored.** ADR 0030 already ruled that helm instruments are owned **per hull** and that the save
  stores the deviations. `HudInstruments` is a *third* shape beside that and beside `OwnedGear`: a
  **static, process-wide, unpersisted** flag set. Whoever authors the first tide clock owes the
  ruling on which shape it is — a per-hull fit under ADR 0030, or a carried personal instrument
  under `OwnedGear` — and the answer decides whether this enum survives or becomes a projection of
  one that does. It is deliberately small so that swap stays cheap.
- **Nothing here is saved.** Granting an instrument will need a save field and therefore a
  **schema-version bump** under ADR 0008. Do not assume the next version number is free — check the
  open branches, do not count from the last merged one.
- **⚠ §3.3's first proof is NOT delivered by this ruling.** The diegetic doc's cheapest proof is
  *"there is no clock on screen; you buy a watch, and the time appears."* This ADR assumes the watch
  is **already worn** and leaves the clock ungated. That is a coherent position — she owns a watch —
  but it is not the same position, and the on-ramp §3.3 describes remains **open and owed** to
  whoever builds the watch as content. Recording it so the two cannot be quietly conflated later.
- **`ux-and-mobile-controls.md` §4 is amended, not retired.** Its layout, thumb-zone,
  redundant-coding and accessibility requirements all stand and apply unchanged to every instrument
  readout when one is finally granted; only the *always-visible* claim in its §4.1 table is
  overridden. The amendment pointer is in that section.
- **Open ruling R10 is answered halfway.** `docs/design/diegetic-devices.md` R10 asks whether the
  notebook retires the onboarding hint label. The banner is gone and the step is a notebook leaf —
  but `OnboardingDirector` still decides the step, so the *presentation* folded in and the **task
  system did not**. R10 stays open for the authored-`TaskDef` half.
- **The owner's eyeball is owed on the quiet state**, since the whole point is a subtractive one and
  no test can tell him whether the screen now feels right: a new game on St Peters with nothing on
  the band, the quest leaf lower-right, the balance in the book, and the helm still fully
  instrumented when she takes it.
