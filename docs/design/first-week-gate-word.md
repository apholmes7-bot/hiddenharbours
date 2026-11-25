# Ginny's word closes the first week

**The demo ends when Ginny finishes giving her word in the evening.** This page is the single written source for that conversation: its exact lines, eligibility and completion. The opening copy and Notebook proposal refer here instead of authoring another ending. The closing beat is the end of playable demo time, not a new errand or a promise of another playable morning.

The first week takes place entirely on St Peters. The player stays with Ginny, digs and sells shellfish, learns to fish from shore, buys Basil's broken dory on the island slip, mends her by hand, launches and fishes from her. The store sells basic fishing gear and ordinary repair materials, never boat hardware. Three hand jobs are accepted; their prices and final material rows are separate content work. Goods and catch are carried by hand.

This is a design page, not an implemented conversation. It follows the [first-week canon](../vision-and-pillars.md), [ADR 0062](../adr/0062-the-first-week-is-closed-by-weather.md) and the later settled ending: stop the demo at her evening word. The earlier ADR's continuation sentence is superseded by that decision. The demo has its own save file and supports two to four players. This page changes no dialogue asset, scene, configuration value, runtime or version-26 save field.

Source baseline: `f808af6ae5d5a0041f3d2147d74b1ddef7dce609`. The five facts below are checked against [PR 1511](https://github.com/apholmes7-bot/hiddenharbours/pull/1511) at `f6006e9fb91ccbe62500bc4cc6200f0c5cbed6ec`; its latest completed test run succeeded. That establishes source/test evidence, not a played first week.

## Exact conversation

Use these lines as written. Keys are proposed text identities, not new registered assets or saved fields. There is one ending line shared by the demo and full game, with no alternate wording selected by chance or edition. Other documents must reference this table for Ginny's word and its prerequisite replies.

| Proposed text key | Exact line | When it is selected |
|---|---|---|
| `dialogue.st_peters.ginny_gate.word` | You've found your feet here, child. I'm proud of you. Mind the tide, wherever you go. | All five facts hold for the addressed player; evening; he is ashore talking to Ginny; his word is not completed. |
| `dialogue.st_peters.ginny_gate.return_evening` | Come by this evening, child. We'll talk then. | All five facts hold; before the authored evening window; his word is not completed. |
| `dialogue.st_peters.ginny_gate.mend_dory` | Finish putting that dory right, child. We're in no hurry off this island. | DoryMended is the first missing fact. |
| `dialogue.st_peters.ginny_gate.cod_licence` | Get your cod licence sorted, child. Then you can keep a cod for your trouble. | CodLicence is the first missing fact. |
| `dialogue.st_peters.ginny_gate.fish_from_dory` | Have a fish from that dory you can keep, child. You'll know her better for it. | FishFromDory is the first missing fact. |
| `dialogue.st_peters.ginny_gate.sell_shellfish` | Sell a clam at the store, child. You'll need your shellfish licence to keep one. | ShellfishSold is the first missing fact. |
| `dialogue.st_peters.ginny_gate.first_week` | Stay a little yet, child. There's time to learn this shore before you take on the next. | DayReached is the first missing fact. |
| `dialogue.st_peters.ginny_gate.pending_morning` | Get some sleep, child. Mind the tide when you go. | Full game only; his word is completed and passage awaits a qualifying sleep. |

The word is a short, player-paced turn in Ginny's ordinary conversation. Completing it triggers the demo closing beat exactly once for that player. Cancelling before completion leaves it available on a later eligible evening. A demo completion does not select the full game's sleep reminder or another task. Nobody speaks at the helm: every line above is spoken during an ashore conversation; no remote voice or helm reminder is queued.

## What must be true

Read one fresh `IslandWeekSnapshot` for the addressed player from PR 1511's `IslandWeekProgress.Read(SaveData, int dayIndex)`. Use that player's progress and the restored Core clock's `IGameClock.DayIndex`. Recheck `AllHold`, evening and the conversation participant at completion. Do not combine one player's licence with another player's catch to manufacture eligibility. The fact order is the authored missing-note order, not a mandatory order of play.

| Fact | Exact evidence | Does not count |
|---|---|---|
| DoryMended | Repaired boats contains the configured `boat.dory` | Buying the broken hull alone |
| CodLicence | Owned licences contains `license.cod` | Holding only `license.clam` |
| FishFromDory | `st_peters.fact.fish_from_dory`, from a successful licensed fresh finfish landing while occupying the configured, already-repaired dory | Release, protected catch, failed delivery, shellfish, shore or pier catch carried aboard, another hull, or a pre-repair landing |
| ShellfishSold | `st_peters.fact.shellfish_sold`, from positive shellfish units in an actual successful sale | Digging, carrying, gifting, dumping or an empty sale |
| DayReached | `dayIndex >= EarliestDay - 1`; current data has `EarliestDay: 7`, so index 6 qualifies | Index 5; seven completed days; a season day or stale saved day cache |

Day seven means the **start of the seventh day**, with Ginny's word still waiting for an evening conversation. There is no seventh-day deadline. If a fact is still missing, he continues island life and can hear her word on a later eligible evening. Selling one kept clam before buying the dory counts; no full-pail quota, catch price, daily streak or repeat sale is added. Sleeping through each earlier day is not required.

The recorder uses landing provenance rather than the Notebook's bounded handoff memory or caught-species totals. A previously stored shore fish cannot qualify after being moved into a hold, even if the Notebook has forgotten that handoff. The repaired identity remains generic `boat.dory`; the current repaired list also admits older paid repairs. Distinguishing the physical purchased hull and integrating the three hand jobs remain dependencies. Existing historical flags are not silently cleared.

The shellfish route's existing data names `license.clam` at `seller.leblancs` and the buyer at `shop.st_peters_general`. The licence covers keeping the catch; it never blocks trying to dig or fish. The current cod licence still names `seller.nmc_harbourmaster`, so an island seller is required before this week can ship. Do not send the player off the island to supply that missing content. Counter selection, carried-pail persistence, counted rods and fee recovery are separate changes.

## Encounter and personal progress

Ginny is the speaker. Basil sells the dory, guides hand work and reads the weather in his separate conversation. [Ginny's opening dialogue](../../Assets/_Project/Data/NPCs/Dialogue/GinnyOpening.asset) supplies the warm, practical voice; [her routine](../../Assets/_Project/Data/Routines/AuntGinnyRoutine.asset) sends her to the cottage garden at 17:00 and cottage step at 20:00. Those are departure windows, with travel and schedule jitter, not guaranteed appointments.

Choose the first eligible ordinary evening talk at her cottage garden or step, once she has arrived. Author the evening window as encounter data; do not hard-code a time from her routine. Before the window use the daytime invitation; after it, keep the word available for the next eligible evening. A guaranteed meeting on day seven is not required or provided here. The player is male; use “the player” or “he” in narration.

The demo supports two to four players. Fact evaluation, selected reply, pending conversation and word completion belong to the addressed player. A future adapter must take explicit participant identity; no static or singleton may stand for the only player. The existing fact recorder's save/event wiring does not itself prove multiplayer attribution. Another player's catch, talk, cancellation, disconnect or replayed callback cannot complete this player's word.

Design decision for the ending: completion closes the addressed player's playable demo and presents his closing beat; it does not manufacture the remaining participants' completion. Others can finish their own island work. A shared-session closing presentation waits for its participating players to finish; join, leave and reconnect presentation details need a multiplayer integration test. This is a session policy, not a new all-players-ready fact or a host-only eligibility shortcut. No participant can continue playable demo time after his own word.

## Completion and the full-game boundary

| Event | Required result |
|---|---|
| All facts hold before evening | Return-evening line; zero completion or passage writes |
| Eligible evening line finishes in the demo | Mark that player's session word completed once; end his playable demo with its closing beat; no next task or passage opening |
| Eligible evening line finishes in the full game | Mark that player's session word completed once; capture the due morning once; leave passage closed that evening |
| Line cancelled, participant changed, or eligibility lost | No completion; no pending morning; reevaluate on the next conversation |
| Duplicate completion callback | No repeated closing beat, state write or change to the captured due morning |
| Full-game qualifying sleep completes | Passage opens on waking; ordinary tide and sea conditions still apply |

The full-game continuation is outside the first-week/demo task sequence. Its route to Nine Mile Creek opens after actual sleep following the evening word reaches the next qualifying morning. It does not open at word completion, on a day tick, during a short nap or merely because the player stayed awake. Use the existing Core clock, `RestMorning.WakeSeconds`, `GameConfig.WakeHour` and `SecondsPerDay`; add no wall-clock timer or second calendar.

For a later session prototype, expose a plain snapshot per player: `WordCompleted`, `WordCompletedAtSeconds`, `DueMorningSeconds`, `PassageOpen` and demo completion, associated with an explicit participant identity. These describe transient state only. On full-game completion capture the due morning from `RestMorning.WakeSeconds(completedAt, config)`. A qualifying sleep starts at or after completion, advances time, and ends at least at both the captured due morning and `RestMorning.WakeSeconds(sleepStart, config)`. If he stays awake beyond the due morning, a later sleep must still reach its own following morning. The demo never creates a pending-passage state.

Reuse the Core sleep flow: both `SleepFlow.Wake` and the instant `RestSaveResponder.Fulfil` path reach `RestSaveResponder.Keep`. The later adapter must receive actual sleep start, end and outcome before the wake save is captured. `DayStarted`, `SleepFlowChanged(Awake)` and `GameSaved` alone do not prove qualifying sleep. Abandonment, a save-only action and a failed sleep request cannot open passage. Multiplayer passage must use an explicit shared-world policy; this page does not equate one person's word with every person's readiness.

Main's four sea-edge barriers stand only when `st_peters.first_week` is true and `st_peters.weather_turned` is false. [CampaignBarrierDirector](../../Assets/_Project/Code/World/Campaign/CampaignBarrierDirector.cs) consumes the latter immediately. No current producer activates the first flag. Therefore neither this page nor those dormant assets prove a closed island. A later coordinator must supply activation, walking-bar surf and the full-game morning transition; dialogue must never write the immediately consumed weather flag in the evening. Keep world wind, tide and sea simulation unchanged.

## Save boundary

The demo keeps its own save file, separate from the full game. This is settled isolation, not permission to import, overwrite or reinterpret a full-game save. No demo-to-full-game transfer is promised here. Version 26 remains unchanged; this page adds no field, flag writer, migration or encoding.

New completion and morning state stays in plain per-player session snapshots until a save step is accepted. That step would need durable player identity and attribution, word-completion and demo-end state, authoritative completion time and fixed due morning, atomic word/wake writes, idempotent reconnect/reload handling and old-save rules. Existing boolean flags alone cannot preserve the promised time. Do not encode a date in an unrelated saved row or dynamic flag key. The isolated demo filename does not supply any of this persistence behavior.

A released demo must restore its completed ending without reopening playable time; a session-only prototype cannot claim that guarantee. A full-game reload must preserve a pending due morning exactly and must not relock already-open passage. Preserve existing money, licences, gear, boat work and legitimate catch facts. An old completion flag without recoverable timing needs an explicit migration decision, not a guessed date. No save is written by this proposal.

## Missing TASKS notes

Use these five notes with PR 1511's existing proposed keys and ordered missing facts. They are the single chosen copy set, all within the existing 31-character budget in [NotebookKit](../../Assets/_Project/Code/Core/Notebook/NotebookKit.cs). They are guidance, not a second evaluator. Keep one CAUGHT list in the Notebook and no FREED page; an unlicensed release makes no CAUGHT entry. Gate progress does not read either a release history or Notebook totals.

| Text key | Exact note |
|---|---|
| `tasks.st_peters.mend_dory` | Mend the dory at the slip |
| `tasks.st_peters.cod_licence` | Get a cod licence on St Peters |
| `tasks.st_peters.fish_from_dory` | Keep a fish from the dory |
| `tasks.st_peters.sell_shellfish` | Carry shellfish in to sell |
| `tasks.st_peters.first_week` | Give it a few more tides |

Do not add a waiting note when practical work is still missing. All five held before evening means seek Ginny that evening, not another day of work. The word is a conversation completion, not a sixth historical fact in `IslandWeekProgress`.

## Acceptance cases and evidence

These are requirements for the later implementation, not tests executed by this document. Extend the existing `OnboardingProgressTests`, `SleepFlowTests`, `BedBringsTheMorningTests` and campaign-barrier coverage rather than introducing parallel evaluators.

| Case | Exact required result |
|---|---|
| Index 5, other four facts true | DayReached missing; no word |
| Index 6, all five true, before evening | Return-evening line; no completion |
| First eligible talk on a later evening | Same word; no missed-deadline penalty or extra sale |
| Stored shore fish carried into repaired dory | FishFromDory remains missing |
| One fact missing | First missing reply in authored order; zero completion writes |
| Cancel or change participant before finishing | No completion for either player |
| Finish eligible demo word | One personal closing beat; playable demo ends there; no morning task |
| Repeat callback, reconnect or restore completed demo after accepted save step | No duplicate ending and no resumed playable time |
| Two to four players with different progress | Each reads his own facts; nobody else's talk, licence or catch grants his word |
| Try to speak while controlling a helm | No spoken line; no completion |
| Finish eligible full-game word | Fixed due morning; zero passage writes that evening |
| Stay awake, abandon sleep, save only or wake before morning | Passage remains closed and due morning unchanged |
| Actual later sleep reaches both required mornings | Exactly one passage transition before the wake save; surf and sea edges agree |
| Full-game reload before or after passage | Exact pending timing restored or open passage retained; no guessed state |

The source check found PR 1511's existing evaluator, recorder, rules asset and `OnboardingProgressTests`; no new class or validator is added. Its green run does not prove the future conversation, multiplayer ending, sleep adapter or save behavior. Local checks cover the ordered fact keys, selected lines, note lengths, links and document-only scope. No local compilation, game tests, dialogue playthrough, layout check, multiplayer session or save/reload run was performed for this revision.
