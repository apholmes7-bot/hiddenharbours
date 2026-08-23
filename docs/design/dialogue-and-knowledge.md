# Dialogue & Knowledge — how people talk, and where information lives

> **Status: RATIFIED DIRECTION (owner, 2026-07-30)** — the conversation-and-knowledge panel of
> the diegetic doctrine. Subordinate to [`../vision-and-pillars.md`](../vision-and-pillars.md)
> (CANON) and sibling to [`diegetic-ui-and-inventory.md`](diegetic-ui-and-inventory.md) (the
> keystone *why*: information is an earned instrument) and
> [`diegetic-instruments-and-consoles.md`](diegetic-instruments-and-consoles.md) (the boat's
> dash). Build phase: **M2 by default** — §4 marks the one slice the owner may pull into M1.
> Nothing here authorizes out-of-phase construction (CLAUDE.md rule 8).

---

## 1. The owner's words (verbatim, 2026-07-30)

> "the npc agent asked for 5 new character portraits. I think i will stay away from that
> design and follow an interaction behaviour more like animal crossing"

> "There should be movement animations that happen while the character speaks. And the sounds
> can be the speak bubble itself populating. There will be options for the player to select
> for gameplay here or to ask a character questions. Instead of menus there will be
> cellphones, computers, documents and the other npcs who contain gameplay knowledge"

## 2. The conversation model — the character IS the portrait

**No portrait dialogue boxes, ever.** (The five #354 portrait asks are cancelled; the
`Art/Portraits/` slots on `NpcDef` are dead and get deprecated whenever the presenter is next
touched.) Conversation is the Animal Crossing shape, in this game's voice:

- **The speech bubble anchors at the speaker**, in the world; the world stays visible behind
  the exchange.
- **The character moves while speaking** — turns to face the player, and plays movement/
  emote animation through the line. The 8-dir character rig's animation surface is the pool
  this draws from; what a "talking" animation set needs is an art-director ask when this
  builds.
- **The sound IS the bubble populating.** No voice, no animalese synth: the text filling the
  bubble is itself the audio event. Per-character cadence (fill rate, tick timbre) is the
  characterisation channel — a taste surface for the owner, not a guess.
- **Dialogue carries player OPTIONS**: choices that do gameplay ("can you front me the fee?")
  and **questions the player asks the character** — because people are knowledge surfaces
  (§3). The option picker lives in/at the bubble, in the same visual language.

## 3. The knowledge doctrine — no menus; knowledge lives in things and people

**"Instead of menus there will be cellphones, computers, documents and the other npcs who
contain gameplay knowledge."** This extends the keystone rule (information is an earned
instrument) from *readings* (clock, tide, wind) to *knowledge itself*:

- **Cellphones and computers** are in-world devices you look at — the era allows them (the
  same world that has outboards, freezers, and a gas pump). What lives on which device, who
  owns one, and what upgrades unlock (a phone that receives price texts? a computer at the
  buyer's office?) are OPEN — owner's world-building, capture before building.
  > **Captured 2026-08-14** — the owner directed a four-device suite (calendar · notebook ·
  > phone · computers) and named what the phone carries. The capture, the reconciliation with
  > the earned-instrument rule, and the rulings still needed are in
  > [`diegetic-devices.md`](diegetic-devices.md); §5 Q3 below stays open until those are ruled.
- **Documents** — letters, notices, the almanac, ledgers, charts. The tide-table almanac page
  (#355) is the proof piece of this pattern and the template.
- **Other NPCs** — asking people is a first-class information channel (the §2 question
  options). Who knows what is content, authored per character; a fisherman knows grounds, a
  buyer knows prices, Ginny knows the island.
- The test for any future information feature: *"would a menu do this?"* — then it must
  instead be a device, a document, or a person.

## 4. Phasing

- **M1 (shipped/OK):** anchored NPCs with lines (#354); the almanac tide table (#355) as the
  document-pattern proof. The `DialoguePresenter` panel was ACCEPTED AS INTERIM — and has now
  been replaced (below).
- **The bubble presentation slice** (panel → anchored bubble + facing + populate-sound,
  options picker): **PULLED INTO PHASE by the owner, 2026-08-17**, the way he pulled the
  instruments (§0 of the consoles doc).
  > **Built 2026-08-17.** The panel is gone. `DialoguePresenter` is now the anchored bubble:
  > world-tracked screen space, tail aimed at the speaker, screen-edge clamping that slides the
  > bubble while the tail keeps pointing (`DialogueBubbleLayout`, pure + tested at the corners);
  > per-character fill at a per-character cadence (`DialogueVoiceDef` on `NpcDef`, so §5 Q2 is a
  > slider rather than a guess); **the fill IS the audio event**, published through Core as
  > `DialogueTypewriterTick` — the SOUND of it is the audio lane's own PR, and nothing is
  > synthesised in World; and an option picker (`DialogueDef.Options`) driven by the existing
  > move axis + Interact, whose picks cross modules as `DialogueOptionPicked`. The close row
  > ("See you later.") is APPENDED by the picker, always last, so no authored conversation can
  > ship without a way out. Options are flat and one-round on purpose — a reply never leads to
  > more options; the tree is the M2/M3 knowledge-graph work below.
  > **The portrait slot is gone with the panel** (§2's "no portrait dialogue boxes, ever"):
  > `DialogueLine.Portrait`, `Interactable._portrait` and the region builders' `Art/Portraits`
  > loads are all removed. The character on screen is the portrait.
  > **~~Still greybox~~ — the kit LANDED.** Corrected 2026-08-23: the bubble no longer draws as
  > tinted rects. `Assets/_Project/Art/UI/DialogueBubble` carries the baked pieces (panel, six tail
  > orientations, gold pill, four cursors, caret, markers), committed. `DialogueBubbleArtTests` has
  > **flipped** — its own header says so — and now fails the day the art *leaves* rather than the day
  > it arrives.
- **NPCs stopping to talk** (facing the player, the routine held as an overlay): built alongside
  the bubble, 2026-08-17 — see `npcs-and-routines.md` §2.7.
- **Devices (phones/computers) and ask-a-question knowledge graphs: M2/M3**, alongside the
  physical inventory and merchant-conversation work the keystone doc already places there.
- **Shop talk as an option row — PROPOSED, not built.** The owner directed (2026-08-23) that buying
  live inside the conversation, opening a notebook-styled catalog. Designed in
  [`shop-catalog-and-dialogue-choices.md`](shop-catalog-and-dialogue-choices.md); it needs **one** new
  `DialogueOption` field and leaves the flat/one-round rule intact (a catalog row is *deferred*-
  terminal, never a tree). Its **R2** is the ruling on whether the conversation holds while the book
  is open.

## 5. Open questions (owner's taste — capture, never guess)

1. Emote/movement vocabulary while speaking — how big, and per-character or shared?
2. Bubble typography and the populate sound palette (per-character cadence?).
3. Who owns a phone; what's on the buyer's computer; which knowledge is device vs person.
4. Do question topics unlock from context (heard rumours, seen objects) — the earned-
   instrument pattern applied to conversation?
