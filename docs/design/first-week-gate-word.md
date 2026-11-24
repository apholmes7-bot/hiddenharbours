# The islander's word at the end of the first week

**Ginny should give the word, in an ordinary conversation.** Once the player has mended the dory, holds the cod licence, has kept a fish caught from the dory, has sold shellfish and has reached day seven, she points him towards Nine Mile Creek. He hears a short, warm line in her usual speech bubble. The next journey grows out of a week of island life.

Recommend her first eligible conversation, on about day seven or later, with passage opening the following morning. This follows the timing proposed in the separate first-week canon draft; it is a proposal here, not an implemented transition. The words below say “come morning” so they make that timing clear. A missed conversation stays available. The sea and tide still need reading when the way opens.

This is copy and encounter design only. No dialogue asset, TASKS string, routine, weather, scene or save data changes in this document's change. Evidence is pinned main `541276cbc9`; the five-fact/key contract is the draft in PR 1511, revision `32eb9272b8`. The first week stays entirely on St Peters: Basil sells the broken dory at the island slip, the player mends her by hand, and the island store sells basic fishing gear without boat hardware.

## The speaker and where to find her

Ginny already frames work as something the player earns, fronts his shellfish-licence fee, and sends him off with affection and practical caution. Her [opening dialogue](../../Assets/_Project/Data/NPCs/Dialogue/GinnyOpening.asset) uses “child”, brief instructions and reminders about the tide. Her [gift lines](../../Assets/_Project/Data/NPCs/AuntGinny.asset) bring the conversation back to supper and home. Those are the voice references for the new line. The opening asset's early mainland trip and paid mainland repair are old route instructions; they are not directions to repeat in this island week.

Basil is the stronger specialist weather reader. His [first dialogue](../../Assets/_Project/Data/NPCs/Dialogue/BasilFirst.asset) recalls forty-one years working the channel and tells the player to read the sea for himself. His [gift lines](../../Assets/_Project/Data/NPCs/BasilSamson.asset) are spare and appreciative. His daily weather conversation is already separate work in PR 1498. Keep him as the person the player can consult about conditions, while Ginny marks the change from staying with her to making a longer journey. Her line does not claim that a particular wind direction, sea state or forecast is safe.

The routines give a concrete meeting plan:

| Resident | Authored departure and destination windows | What the location means |
|---|---|---|
| Ginny, `routine.aunt_ginny` | 04:30–17:00: `station.st_peters.ginny_village_mark`; 17:00–20:00: `station.st_peters.cottage_garden`; 20:00–04:30: `station.st_peters.cottage_step` | Her daytime mark is in the village, where the opening conversation happens. The garden and step are at her own cottage on the eastern plot. |
| Basil, `routine.basil_samson` | 05:00–12:00: `station.st_peters.wharf_head`; 12:00–15:00: `station.st_peters.slip_head`; 15:00–18:00: `station.st_peters.wharf_head`; 18:00–05:00: `station.st_peters.home_sage_b` | He spends the working day between the outer wharf and shoreward end of the slip, then goes home. |

Sources: [Ginny's routine](../../Assets/_Project/Data/Routines/AuntGinnyRoutine.asset), [Basil's routine](../../Assets/_Project/Data/Routines/BasilSamsonRoutine.asset), and the station entries in [the committed island scene](../../Assets/_Project/Scenes/StPeters.unity). These are departure windows, not exact arrival appointments. Ginny has ±3 minutes of schedule jitter and walks at 1 m/s; Basil has ±8 minutes and walks at 0.95 m/s. Neither asset supplies a special seventh-day or weekly replacement schedule. The existing [route audit](../world/st-peters-routine-routes-audit.md) explains the journeys; its older geometry measurements are not a new movement verification.

**Who is the player sure to meet on the day? Main guarantees no meeting with either islander.** It supplies their destinations, not a mandatory day-seven encounter. Staying with Ginny does not mean she is at the cottage when he wakes: her ordinary departure is 04:30. It would be inaccurate to promise a breakfast-doorway scene from these assets.

The proposed dependable contact is **Ginny at her village mark during the working day**, with the same pending conversation available when he finds her in the garden or on her step later. A normal daytime return through the village gives him a known place to seek her. “About day seven” means the first eligible ordinary talk, not a single appointment he can miss. If the experience must guarantee that they actually meet on day seven, it needs a separately authored encounter; the existing routine cannot prove that guarantee. The decision is called out below rather than quietly moving her or stopping the player.

## What must be true before she gives her word

All five facts must hold together when the gate conversation is offered. Their order below is the evaluator's missing-note order, not a required order of play.

| Fact | Source | What does not satisfy it |
|---|---|---|
| Dory mended | Repaired boats contains the configured `boat.dory` | Buying her without finishing the repair |
| Cod licence held | Owned licences contains `license.cod` | The separate `license.clam` |
| A fish kept from the dory | `st_peters.fact.fish_from_dory` | A fish released for want of a licence, a protected fish released, shellfish, or a shore catch carried aboard afterwards |
| Shellfish sold | `st_peters.fact.shellfish_sold` | Digging, gifting, dumping, or having shellfish in the pail without selling any |
| Day seven reached | Restore the Core clock normally, then pass `IGameClock.DayIndex` to `Read(save, dayIndex)`; index 6 begins day seven with the authored threshold 7 | Day six; a stale saved day cache |

The day rule means **the start of the seventh day**, not seven completed days. The historical catch and sale facts need one qualifying event each; neither asks for a full hold, a full pail, a particular price or a repeated daily quota. A clam sold before he buys the dory still counts. The intended week must make every fact achievable on the island, but main still lacks the island cod seller and the proposed hand-repair route described below. This is not a claim that the full sequence is playable now. The read-only evaluator does not grant passage by itself. Its current `Read(SaveData, int dayIndex)` contract accepts no seconds/day-length conversion or `ReadSaved` call; use the restored/live Core clock, not a saved cache or a season day.

In the eventual full-game conversation, finishing the chosen line is the handoff to the passage decision. Cancelling before it finishes leaves the word available. A later talk must not repeatedly award or relock passage. The proposed `st_peters.weather_turned` flag is the passage flag discussed by the related draft, absent from pinned main's runtime/data sources; this page does not implement its write or a dawn delay. A promise for the following morning needs a reload-safe handoff design before implementation: a boolean alone does not record which morning was promised. Until a persistence step is accepted, any new word/pending-morning state must remain session-only in a plain snapshot. A later save step needs an authoritative promised-morning representation, completion semantics, cancellation handling and reload/old-save tests; reusing a boolean flag list alone cannot recover the promised date. This page adds no saved field, flag writer or save-format change.

## Three versions of Ginny's line

Choose one version, not a sequence of three. Each is short enough to be treated as one ordinary, player-paced speech turn, with the usual bubble layout still to be checked in the game. There is no HUD banner, checklist recital or new reward screen.

| Version | Proposed line |
|---|---|
| A — recommended | “You've found your feet here, child. Try Nine Mile Creek come morning. Mind the tide.” |
| B | “That little dory's ready for a longer errand. Nine Mile Creek in the morning, child. No racing the tide.” |
| C | “There's more coast waiting for you now. Nine Mile Creek come morning. Bring me back a story, child.” |

A is the clearest route instruction and leaves the achievement in the player's hands. B puts more weight on his repaired boat. C puts more weight on leaving home and coming back. None claims the whole sea is safe. If immediate passage is chosen instead of the following morning, the timing words must be revised before these lines are used.

## The five missing TASKS notes

These are alternatives for the **same five keys** proposed in PR 1511. A is the recommended set; B and C are editorial choices, not random variants. Display a note only while its corresponding fact is missing, using the ordinary TASKS surface. Keep the authored key order when several are missing. The copy describes things the player can do in the world and adds no sixth fact.

| Text key | A — recommended | B | C |
|---|---|---|---|
| `tasks.st_peters.mend_dory` | Mend the dory at the slip | Put the dory right by hand | Finish the dory's repairs |
| `tasks.st_peters.cod_licence` | Get a cod licence on St Peters | Sign for my cod licence | Get a licence to keep cod |
| `tasks.st_peters.fish_from_dory` | Keep a fish from the dory | Land a kept fish from the dory | Fish from the dory and keep one |
| `tasks.st_peters.sell_shellfish` | Carry shellfish in to sell | Sell shellfish at the store | Sell a clam at the counter |
| `tasks.st_peters.first_week` | Give it a few more tides | See in my seventh island day | Live on St Peters to day seven |

All fifteen alternatives fit the existing 31-character note budget. This is a text-length check, not visual fit proof.

The cod-licence wording describes the intended island week. Main currently lists [the cod licence](../../Assets/_Project/Data/Resources/Catalog/Licenses/CodLicense.asset) at the mainland harbourmaster, `seller.nmc_harbourmaster`; the island seller must be supplied by its own content change. Do not point him off-island to satisfy this note. The repair note names the slip and hand work; it does not send him to the grocery store for boat hardware or to a mainland yard for a repair fee.

The shellfish note is already supported by the island's source data: the supplied starting shovel and pail (`gear.shovel`, `gear.bucket`; physical `tool.st_peters.shovel`, `container.st_peters.bucket`), the fronted fee, `license.clam` at `seller.leblancs`, and the `StPetersStore` buyer at `shop.st_peters_general`. The [shellfish licence](../../Assets/_Project/Data/Resources/Catalog/Licenses/ClamLicense.asset) costs 15 and the [configuration](../../Assets/_Project/Data/Config/GameConfig.asset) fronts 20. Digging without it is allowed, but the clam is released, so he needs it to keep a clam for sale. No separate selling licence is checked by [the sale service](../../Assets/_Project/Code/Economy/SellService.cs). Carrying and selling a single kept clam suffices; an unlisted shovel or pail purchase is not required.

## If he asks before the facts hold

Ginny answers the question naturally and gives one useful next step. Choose the first missing fact in the order above, so repeated talks give a stable answer while the state is unchanged. The other missing notes stay in TASKS. These are suggested conversation responses, not another rules evaluator.

| First missing fact | What he hears |
|---|---|
| Dory mended | “Finish putting that dory right, child. We're in no hurry off this island.” |
| Cod licence held | “Get your cod licence sorted, child. Then you can keep a cod for your trouble.” |
| Fish kept from the dory | “Have a fish from that dory you can keep, child. You'll know her better for it.” |
| Shellfish sold | “Sell a clam at the store, child. You'll need your shellfish licence to keep one.” |
| Day seven reached | “Stay a little yet, child. There's time to learn this shore before you take on the next.” |

An early question changes no fact and promises no immediate passage. No spoken checklist, countdown, map wall or demand to sleep through days is needed. Once the word has been heard, a repeat question during the proposed overnight wait can be answered with “Come morning, child. Mind the tide when you go.” That line belongs only to a real pending handoff, never to a guessed calendar state.

## Decisions reserved for the owner

| Question | Lean and consequence |
|---|---|
| Should the word be given on the first eligible ordinary conversation, even if that is after day seven, or must the story ensure a meeting on the seventh day? | **First eligible conversation.** Ginny remains available at the known village mark and later at home. A guaranteed day-seven encounter would require new staging and a separate review; neither current routine provides it. |
| Does finishing her line open passage immediately, or does the weather barrier ease the following morning? | **Following morning**, matching the separate canon draft and all three lines here. This needs a reload-safe pending/completed handoff within the existing save constraints before implementation. Immediate opening would need different timing words. |
| Who issues the cod licence on St Peters, and how does the player obtain the dory's repair materials while the store sells no boat hardware? | **The store counter handles licence paperwork; Basil supplies or points out repair materials at the slip.** Keep the work physical and the grocery shelves free of boat hardware. These are content choices, not new purchases asserted to exist on main. |
| Does the demo close at Ginny's word or let the player reach the now-open route before its ending? | **Close the demo at her word, with continued island fishing**, as the separate first-week draft proposes. A demo that keeps the route closed needs a distinct closing version that does not promise a playable trip the next morning; choose that ending before using this full-game copy there. |

Ginny is the recommended speaker, consistent with the existing canon draft. Basil's role as the seller and weather reader stays coherent with it. The decisions above concern encounter timing, distribution and the demo boundary; they do not reopen the settled island-only week, hand repairs, licence release rule or weather-only closure.

## Evidence and limits

Read the two NPC assets, both dialogue assets, both complete routine assets, their serialized scene stations, the current shellfish licence and sale path, the Core clock contract, and the five keys on PR 1511. Read the relevant proposed direction in PRs 1492 and 1498 without editing their files. Main's old opening text and current island equipment are distinguished from the proposed week throughout.

Checked all five key spellings against PR 1511 at `32eb9272b8`, the fifteen note lengths, the three gate alternatives, the nine conversation-line mappings below and the relative source links. No Unity, bubble-layout check, dialogue playthrough, route simulation, save/reload handoff test or weather-transition verification was run. The work delivers a reviewable design page; none of those future behaviors is claimed to ship with it.

## Conversation line, text key and prerequisite

These are proposed text keys, not registered dialogue assets or saved flags. Only one of A/B/C is chosen for the full game. All three require a fresh `AllHold` reading from the five-fact snapshot, with the word not already completed. They also require the promised next-morning handoff to exist before shipping; none is suitable as the permanently closed demo's ending. Early answers apply only before the word is completed and select the first missing fact in the authored order. The overnight answer reads a real pending handoff, not eligibility alone. No sixth eligibility requirement is introduced.

| Conversation line | Proposed text key | Fact or state required |
|---|---|---|
| You've found your feet here, child. Try Nine Mile Creek come morning. Mind the tide. | `dialogue.st_peters.ginny_gate.word_a` | All five facts hold; full game; word not yet completed. |
| That little dory's ready for a longer errand. Nine Mile Creek in the morning, child. No racing the tide. | `dialogue.st_peters.ginny_gate.word_b` | All five facts hold; full game; word not yet completed. |
| There's more coast waiting for you now. Nine Mile Creek come morning. Bring me back a story, child. | `dialogue.st_peters.ginny_gate.word_c` | All five facts hold; full game; word not yet completed. |
| Finish putting that dory right, child. We're in no hurry off this island. | `dialogue.st_peters.ginny_gate.mend_dory` | DoryMended is missing. |
| Get your cod licence sorted, child. Then you can keep a cod for your trouble. | `dialogue.st_peters.ginny_gate.cod_licence` | DoryMended holds; CodLicence is missing. |
| Have a fish from that dory you can keep, child. You'll know her better for it. | `dialogue.st_peters.ginny_gate.fish_from_dory` | DoryMended and CodLicence hold; FishFromDory is missing. |
| Sell a clam at the store, child. You'll need your shellfish licence to keep one. | `dialogue.st_peters.ginny_gate.sell_shellfish` | The first three facts hold; ShellfishSold is missing. |
| Stay a little yet, child. There's time to learn this shore before you take on the next. | `dialogue.st_peters.ginny_gate.first_week` | All four practical facts hold; DayReached is missing (day index below 6 with the current threshold). |
| Come morning, child. Mind the tide when you go. | `dialogue.st_peters.ginny_gate.pending_morning` | Full game; word completed; promised morning still pending; passage has not opened. |
