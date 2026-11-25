# Opening words for the island week

Keep the player's first week on St Peters in the words as well as in the weather. Ginny welcomes him into her home; the flats give him his first work. He signs for a shellfish licence, digs clams, sells them at the island store, buys his fishing gear from the shelf, and learns from shore. He earns Basil's broken dory on the slip, mends her with his own hands, then rows and fishes in island water. The demo ends when Ginny finishes her word in the evening on day seven or later. There is no crossing or following-morning task in the first-week sequence. The full game's later passage is a separate continuation.

This is proposed wording, not a change to the game. The tables pair every line in the opening conversations, its pinned hints and its Tasks entries with the words to keep or replace. They also cover the shop conversation, licence and shelf messages that the opening sends him to, and the notebook's three available instruction pages. Unrelated island greetings, gifts and later-region conversations are outside this opening-copy inventory; Basil's introduction is included because he now owns the dory beat. A matching right-hand entry means keep the words.

Read against pinned main `f808af6ae5d5a0041f3d2147d74b1ddef7dce609`, on 2026-11-25, and [ADR 0062](../adr/0062-the-first-week-is-closed-by-weather.md). The first-week amendment in [the canon](../vision-and-pillars.md) controls the sequence. The later direction that the island store carries basic fishing gear and **no boat hardware** controls the shop wording; basic repair materials come from the St Peters store and are carried to the slip for hand work. The three-job hand-mending plan is accepted; exact material rows, quantities and prices remain content work; Basil guides the work instead of supplying a free repair pile. The draft deliberately promises no repair shopping list, price or fixed delivery day.

## What the player should hear first

Ginny's complete proposed first conversation, with the existing fee reminder appended when she has fronted it:

> Oh, child — you made it out to the island. Your uncle Ned thought the world of you, you know.
>
> He's gone to the deep now, rest him. He left you his cottage and his memory. You'll stay here with me while you find your feet.
>
> We earn our way on this coast. Get your shellfish licence signed at the store, then take the shovel and pail down at low water. Two little squirts in the sand — that's your clam.
>
> Bring your clams to LeBlanc's. The rod and bait are on the shelf when you're ready. Start from shore; there's plenty to learn with dry boots.
>
> Basil's broken dory lies on the slip. Save for her, then put her right with your own hands. A little boat feels different when you've earned her.
>
> Keep to island water for now. That surf isn't giving way today. Come and see me when you've found your feet. We'll talk about the way off then.
>
> And that shellfish licence — I've put the fee in your hand. It's your name that goes on the paper, not mine.
>
> It's in the book at the counter, mind. Square it with me when the flats start paying you.

This keeps her warmth and the uncle's place in the story, then gives one immediate job. Licence means permission to **keep** the catch, not permission to try. The separate repeat line below says that plainly. The fee reminder stays conditional; it must never claim she gave him money when she did not.

## Where today's words come from

- `StPeters.unity` references `npc.aunt_ginny` and `npc.ned_letter` through their Npc assets. `AuntGinny.asset` points to `GinnyOpening.asset`, and `NPCs/NedLetter.asset` points to `Dialogue/NedLetter.asset`. `WorldInteractor` uses those dialogue assets ahead of the old `WorldStrings.Conversation` arrays. Ginny's completion key is `met_ginny`; the letter still uses `read_logbook` despite being a letter. These identities need no renaming for a copy edit.
- `DialogueDef.Lines` appends the conditional lines to either the first or repeat pool when `ginny_fronted_clam_fee` is true. It does not substitute a new conversation, check whether the licence was already bought, or clear the debt. The wording below uses the past tense so the repeated reminder remains true.
- The small pinned **TASKS** note reads `OnboardingProgress.LineFor` through `OnboardingDirector` and `QuestPanelPresenter`. The notebook's **TASKS** page separately reads `QuestDef` assets through `NotebookContentSource`, `QuestBoard` and `NotebookContent`. Changing one does not change the other.
- `OnboardCrossBar` exists but has no step in the current evaluator. After a live repair, the director briefly shows `OnboardDone`, then `OnboardSailHome`, then hides the note. On Continue with a repaired dory it goes straight to hidden. None of those states proves the weather gate has opened.

Source files for that routing: [WorldInteractor.cs](../../Assets/_Project/Code/World/WorldInteractor.cs), [DialogueDef.cs](../../Assets/_Project/Code/World/DialogueDef.cs), [OnboardingProgress.cs](../../Assets/_Project/Code/World/OnboardingProgress.cs), [OnboardingDirector.cs](../../Assets/_Project/Code/World/OnboardingDirector.cs), [QuestPanelPresenter.cs](../../Assets/_Project/Code/World/QuestPanelPresenter.cs), [NotebookContentSource.cs](../../Assets/_Project/Code/World/NotebookContentSource.cs). This is a source trace, not a played or visually verified opening.

## Ginny: the welcome, the repeat and the fee

Source: [`Assets/_Project/Data/NPCs/Dialogue/GinnyOpening.asset`](../../Assets/_Project/Data/NPCs/Dialogue/GinnyOpening.asset); asset `GinnyOpening`, id `dialogue.ginny_first`. These are editable dialogue data. Array positions below start at zero. Condition key: `ginny_fronted_clam_fee`; completion key on `npc.aunt_ginny`: `met_ginny`.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `FirstLines[0]` | Oh, child — you made it out to the island. Your uncle Ned thought the world of you, you know. | Oh, child — you made it out to the island. Your uncle Ned thought the world of you, you know. |
| `FirstLines[1]` | He's gone to the deep now, rest him. He left you his cottage and his memory — but the old dory of his is past mending, and there's no boat handed to you here. | He's gone to the deep now, rest him. He left you his cottage and his memory. You'll stay here with me while you find your feet. |
| `FirstLines[2]` | No matter. We earn our way on this coast. When the big tide drops, the flats out front bare — get the shovel and dig CLAMS where you see the two little squirts. | We earn our way on this coast. Get your shellfish licence signed at the store, then take the shovel and pail down at low water. Two little squirts in the sand — that's your clam. |
| `FirstLines[3]` | Then the sandbar to Nine Mile Creek opens up, dry enough to walk. Go before the flood seals it, mind — dawdle and you're stuck till the next low. | Bring your clams to LeBlanc's. The rod and bait are on the shelf when you're ready. Start from shore; there's plenty to learn with dry boots. |
| `FirstLines[4]` | In Nine Mile Creek, sell your clams, buy a COD LICENCE and a ROD, and save. There's a damaged dory for sale at the shipwright — buy her, pay to put her right, and she's truly yours. | Basil's broken dory lies on the slip. Save for her, then put her right with your own hands. A little boat feels different when you've earned her. |
| `FirstLines[5]` | Then sail her home to Coddle Cove. That's where your harbour starts. Off you go, now — the tide won't wait on us. | Keep to island water for now. That surf isn't giving way today. Come and see me when you've found your feet. We'll talk about the way off then. |
| `RepeatLines[0]` | Back already? Good. Watch the tide — clams on the flats at low water, then the bar to Nine Mile Creek. | Back already? Good. Clams on the falling tide, then the pail up to LeBlanc's. Keep an eye on the water behind you. |
| `RepeatLines[1]` | Sell, licence, rod, then buy and mend that dory. Earn her, and she'll be yours for good. | A licence for every catch you keep, mind. You can always try your luck; what you aren't licensed for goes back alive. |
| `ConditionalLines[0]` | And your CLAM LICENCE — I've put the fee in your hand, so walk up to the store and sign for it yourself. It's your name that has to go on the paper, not mine. | And that shellfish licence — I've put the fee in your hand. It's your name that goes on the paper, not mine. |
| `ConditionalLines[1]` | It's not a gift, mind. It goes in the book at the counter, and that book keeps better than I do. Square it with me when the flats start paying you. | It's in the book at the counter, mind. Square it with me when the flats start paying you. |

## Ned's letter

Source: [`Assets/_Project/Data/NPCs/Dialogue/NedLetter.asset`](../../Assets/_Project/Data/NPCs/Dialogue/NedLetter.asset); asset `NedLetter`, id `dialogue.ned_letter`. These are editable dialogue data. Array positions below start at zero. Completion key: `read_logbook`. Keep the letter as a remembered voice, not a timetable or a promise of an inherited boat.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `FirstLines[0]` | A weathered letter from your uncle Ned waits on the cottage table, in a careful, salt-stained hand. | A weathered letter from your uncle Ned waits on the cottage table, in a careful, salt-stained hand. |
| `FirstLines[1]` | "If you're reading this, I've gone on ahead. I'm sorry there's no boat to leave you — my old dory's rotted past saving, and I'd not have you risk your neck in her." | "If you're reading this, I've gone on ahead. I'm sorry there's no boat to leave you — my old dory's rotted past saving, and I'd not have you risk your neck in her." |
| `FirstLines[2]` | "So I'll leave you something better than a boat: the way to earn one. Dig clams on the low-water flats. Walk the bar to Nine Mile Creek. Buy a licence, a rod, and a hull worth mending." | "So I'll leave you something better than a boat: the way to earn one. Dig clams on the low-water flats. Sell them at the island store. Save for a hull worth mending." |
| `FirstLines[3]` | "A boat you've bought and put right with your own hands is worth ten that were handed to you. Mind the tide, watch the sky, and don't fish angry." | "A boat you've bought and put right with your own hands is worth ten that were handed to you. Mind the tide, watch the sky, and don't fish angry." |
| `FirstLines[4]` | "There's a whole coast out there waiting. Go and meet it. — Ned" | "There's a whole coast out there waiting. Learn your own bit of water first. The rest will keep. — Ned" |
| `RepeatLines[0]` | Ned's letter. "Earn your boat, then sail her home. Mind the tide, and go and meet the coast. — Ned" | Ned's letter. "Earn your boat, put her right, and learn the water close to home. Mind the tide. — Ned" |

## The pinned Tasks note: every opening constant

Source: [`Assets/_Project/Code/World/WorldStrings.cs`](../../Assets/_Project/Code/World/WorldStrings.cs); class `WorldStrings`; no asset currently owns these sentences. They should become editable opening-text data, retaining the existing accessors during a later migration. The stable text ids below are proposed, not existing lookups. `OnboardCrossBar` is excluded from first-week selection. Any full-game crossing copy belongs to later content after passage opens; the proposed first-week lines below describe only island activities, subject to the route gaps below. The Today column records obsolete source text, not endorsed dialogue.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `OnboardTalkGinny` | Find Aunt Ginny on the island and press E — she'll set you straight. | Find Aunt Ginny on the island — she'll set you straight. |
| `OnboardDigClams` | Tab shows the tide table. First buy the Shellfish Licence at LeBlanc's (Ginny fronted the fee), then dig clams on the bared flats at low water (E by a hole). | Tab shows the tide table. Sign for the Shellfish Licence at LeBlanc's — Ginny fronted the fee. Take the shovel and pail to the flats at low water; E by a clam hole digs. |
| `OnboardCrossBar` | Walk the bared sandbar east to Nine Mile Creek before the tide floods it back. | No first-week line; leave this key unselected until separate full-game passage content. |
| `OnboardBuyLicence` | At Nine Mile Creek: sell your clams, then buy a COD LICENCE to fish. | Sell your clams at LeBlanc's. Sign for a cod licence on the island to keep your cod. Without it, the catch goes back. |
| `OnboardBuyRod` | Buy a fishing ROD from the shop — then you can hand-line cod. | Buy a rod and bait from the store's shelf. Try a cast from shore before you take to the dory. |
| `OnboardBuyDory` | Save up, then BUY the damaged dory at the Nine Mile Creek shipwright. | Save for Basil's broken dory on the island slip. Buy her when you've earned the price. |
| `OnboardRepairDory` | She's yours but broken — pay the shipwright to REPAIR her. | She's yours, but she isn't ready for water. Mend her by hand at the slip. |
| `OnboardSailHome` | Seaworthy at last — sail your dory home to Coddle Cove. | Seaworthy at last. Launch from the slip and keep close to the island while you learn her. |
| `OnboardDone` | You earned her, plank and rope. Fair winds, skipper! | You earned her, plank and rope. Keep an eye on the weather, skipper. |

Suggested ids in the same order: `opening.talk_ginny`, `opening.dig_clams`, `opening.cross_bar_after_weather`, `opening.buy_cod_licence`, `opening.buy_rod`, `opening.buy_dory`, `opening.mend_dory`, `opening.launch_dory`, `opening.dory_ready`. These are text ids, not save flags. Keep E and Tab aligned with actual bindings; removing those existing aids requires a separate readable replacement, not simply deleting them.

The cod line intentionally does not name a counter yet. It is **not ready to ship** until an island counter sells that licence and the owner chooses the place. The rod line describes the intended shelf route. The store's stock data includes `line.rod`, but its current shelf-slot asset has no `gear.rod` slot; that purchase point needs authoring before the line can direct him to it. Changing `OnboardSailHome` to a launch line does not create a launch interaction. The current repair timer also cannot become a first-week completion timer by changing its words.

## The notebook Tasks page: both shipped quest notes

These are editable data. Preserve each quest id and step flag; changing a label must not change what marks it complete. The camper remains optional and locked until bought. Its note does not promise free lodging in the camper.

Source: [`Assets/_Project/Data/Resources/Quests/TheLetter.asset`](../../Assets/_Project/Data/Resources/Quests/TheLetter.asset); asset `TheLetter`, id `quest.the_letter`.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `Title` | The dory he left you | The letter he left |
| `Context` | Aunt Ginny, at the house | Aunt Ginny, on St Peters |
| `Steps[0].Label; flag read_logbook` | read the logbook he left | read the letter Ned left |
| `Steps[1].Label; flag met_ginny` | find Ginny up at the house | find Ginny up at the house |
| `Steps[2].Label; flag onboarded` | get your bearings | buy the dory and put her right |

Source: [`Assets/_Project/Data/Resources/Quests/ARoofOfYourOwn.asset`](../../Assets/_Project/Data/Resources/Quests/ARoofOfYourOwn.asset); asset `ARoofOfYourOwn`, id `quest.a_roof_of_your_own`.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `Title` | A roof of your own | A roof of your own |
| `Context` | the camper on Ginny's back lot | the camper on Ginny's back lot |
| `Steps[0].Label; flag met_ginny` | talk to Ginny about the lot | get to know Ginny |
| `Steps[1].Label; flag deed.home.ginny_lot_camper` | buy the camper | buy the camper when you can afford it |

`quest.the_letter` ends at `onboarded`, which today's director sets after dory repair. It must not be retitled “The first week” or “The weather turns” while that remains its completion rule. The camper's first step currently completes on `met_ginny`, not a conversation about buying land; “get to know Ginny” matches that limited evidence.

The week needs later notes for shore practice, selling clams, a kept fish from the dory, and the islander's word. These do **not** exist as displayed first-week notes on pinned main. For the fact keys proposed in PR 1511, suitable words are:

| Proposed text key | Proposed note |
| --- | --- |
| `tasks.st_peters.mend_dory` | Mend the dory at the slip |
| `tasks.st_peters.cod_licence` | Get a cod licence on St Peters |
| `tasks.st_peters.fish_from_dory` | Keep a fish from the dory |
| `tasks.st_peters.sell_shellfish` | Carry shellfish in to sell |
| `tasks.st_peters.first_week` | Give it a few more tides |

Do not show all five as an order to finish on day one. The last line gives no countdown and does not claim that time alone opens the bar. All five fit the existing 31-character note budget and transcribe the chosen set in the [single source for Ginny's word](https://github.com/apholmes7-bot/hiddenharbours/blob/4b2d7f62f5d05f4b523c9f333fc3d1928914f472/docs/design/first-week-gate-word.md). That page controls changes to these notes. PR 1511 at `f6006e9fb9` uses zero-based clock index 6 for the start of day seven, not seven completed days. It supplies a reading of facts; it does not display these sentences or implement the conversation. Its fish fact requires a fresh licensed landing from the already-repaired dory; a stored shore fish, another hull or a pre-repair catch cannot supply it. Copy does not relax that rule.

## LeBlanc's: buy at the shelf, sell at the counter

Source: [`Assets/_Project/Data/NPCs/Dialogue/MargueriteFirst.asset`](../../Assets/_Project/Data/NPCs/Dialogue/MargueriteFirst.asset); asset `MargueriteFirst`, id `dialogue.marguerite_first`. These are editable dialogue data. Array positions below start at zero.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `FirstLines[0]` | You'll be the one stopping up at Ginny's. Word gets round this island faster than the tide does. | You'll be the one stopping up at Ginny's. Word gets round this island faster than the tide does. |
| `FirstLines[1]` | LeBlanc's, such as it is. Shovel, line, bait, and whatever the boat brings me. If I haven't got it, you don't need it yet. | LeBlanc's, such as it is. Rods, line, bait, and a few groceries. Have a look at the shelf — the price is there with each thing. |
| `FirstLines[2]` | And I'll not price my neighbours the way they do over at Nine Mile Creek. A store that squeezes a small place kills it — and then where's the store? | I price for the neighbours I have. A store that squeezes a small place kills it — and then where's the store? |
| `RepeatLines[0]` | Back again. If you're for the flats, go on the falling tide — I'll still be here after. | Back again. If you're for the flats, go on the falling tide — I'll still be here after. |

Options on the same shop conversation, keyed by their option ids:

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `option.browse_wares.Label` | What have you got in? | What can I order? |
| `option.sell_catch.Label` | Will you take what's in my pail? | Will you take what's in my pail? |
| `option.sell_catch.ReplyLines[0]` | ₲{payout} for the lot, and it's on the scale there where you can see it. | ₲{payout} for the lot, and it's on the scale there where you can see it. |
| `option.sell_catch.ReplyLines[1]` | I'll be straight with you — Wendell would give you more for that over at the creek, and he'd be right to. I sell to the same island I buy from, so I can't. | I sell to the same island I buy from. I can't give you a fortune, but I'll give you a fair weighing. |
| `option.sell_catch.ReplyLines[2]` | But you're standing here and he isn't, and it's off your hands. Some days that is the whole of it. | You're here, the pail's here, and we can settle it now. Some days that is the whole of it. |
| `option.sell_catch.NothingToSellLines[0]` | There's nothing in that pail but air, my dear. Bring it back with something in it and I'll weigh it gladly. | There's nothing in that pail but air, my dear. Bring it back with something in it and I'll weigh it gladly. |
| `option.ask_about_store.Label` | How do you keep a shop going out here? | How do you keep a shop going out here? |
| `option.ask_about_store.ReplyLines[0]` | Badly, and on purpose. I stock what five families and a schoolteacher actually use, and I'd sooner be out of a thing than sat on it all winter. | Badly, and on purpose. I stock what five families and a schoolteacher actually use, and I'd sooner be out of a thing than sat on it all winter. |
| `option.ask_about_store.ReplyLines[1]` | The boat comes Thursdays. If you want something particular, tell me Wednesday and I'll put it on the list. That is the entire ordering system, and it has never once failed me. | Each thing runs out in its own time. I'll put what I can on the next boat. Weather gets the last word. |

The browse option currently opens `seller.leblancs` with `CatalogSection: gear`; “What can I order?” is proposed for the later special-order conversation, **not a safe label-only edit** while the row still opens ordinary wares. Until that routing is ready, retain “What have you got in?” for the remaining catalogue services, including licences. A new short shop reply can teach the bag: “Groceries go in the paper bag. You'll want a hand free to carry it.” This is an additional proposed line, not a field already present.

No reply directs him to another buyer during the closed week. No line offers outboards, fuel fittings, a sounder or boat hardware at this store. The hard Thursday/Wednesday guarantee is removed because weather and the actual stock calendar govern delivery. Selling still names the pail; on pinned main the island counter reads its wired pail provider, so this wording does not certify carried-container selection.

## Basil at the slip: keep his voice

Source: [`Assets/_Project/Data/NPCs/Dialogue/BasilFirst.asset`](../../Assets/_Project/Data/NPCs/Dialogue/BasilFirst.asset); asset `BasilFirst`, id `dialogue.basil_first`. These are editable dialogue data. Array positions below start at zero. PR 1498 owns this dialogue asset; this document changes none of it.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `FirstLines[0]` | Basil Samson. Forty-one years I went out that channel, and now I come down of a morning to watch the water do it without me. | Basil Samson. Forty-one years I went out that channel, and now I come down of a morning to watch the water do it without me. |
| `FirstLines[1]` | They'll tell you the sea is fair. She isn't. She's honest — that's a different animal. She'll feed you and drown you the same week and think nothing of either. | They'll tell you the sea is fair. She isn't. She's honest — that's a different animal. She'll feed you and drown you the same week and think nothing of either. |
| `FirstLines[2]` | So read her yourself. Don't take my word for it, and don't take the boat's. Yours. | So read her yourself. Don't take my word for it, and don't take the boat's. Yours. |
| `RepeatLines[0]` | Tide's doing what it said it would. That's the only promise you'll get out here. | Tide's doing what it said it would. That's the only promise you'll get out here. |

A proposed added reply when the player asks about the dory: “That one on the slip is mine. Broken, as you can see. Pay me for her and the work is yours — no boat worth having mends herself.” It needs a properly authored conversation row; it is not in the current first or repeat lines. Keep the price in the sale's data, not in this speech.

PR 1539 supplies the slip sale separately. Its draft asset `Assets/_Project/Data/Regions/StPetersVillage/slipsale.basils_dory.asset` (`slipsale.basils_dory`) has the following words. These are **draft-branch words, not pinned-main words**; the proposal should follow the owning work rather than duplicate it:

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `BuyLabel` | Buy Basil's dory — {price} | Buy Basil's dory — {price} |
| `OwnedLabel` | Look over the dory | Look over the dory |
| `TooDearLine` | Basil wants {price} for her. I haven't got that yet. | Basil wants {price} for her. I haven't got that yet. |
| `BoughtLine` | She's mine now. Broken, but mine. | She's mine now. Broken, but mine. |
| `NeedsMendingLine` | She wants a plank, caulking and paint before she'll swim. | She wants mending before she'll swim. Best look her over, then put my hands to it. |

The three-job hand-mending plan is accepted. Keep the specific plank/caulking/paint line only when its final material rows are obtainable on the island; the price remains unsettled. The draft price is data and is not copied into the opening speech. Buying her is not repairing or launching her.

## The licence and shelf replies already in data

The unlicensed-catch sentence is in the player's own voice and names the catch. Nobody speaks at the helm, including inner voice: while he controls it, release the catch without displaying or queuing speech. Ashore, the existing sentence can explain the release. Licence enforcement never waits for speech; it never prevents trying a dig, cast or haul. This is a required presentation condition, not a claim that every current cue already enforces it.

Source: [`Assets/_Project/Data/NPCs/InnerVoice/UnlicensedCatch.asset`](../../Assets/_Project/Data/NPCs/InnerVoice/UnlicensedCatch.asset); asset `UnlicensedCatch`, id `innervoice.unlicensed_catch`, cue `cue.fishing.unlicensed_catch`. Editable data.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `Line` | I don't have a licence for this {name} yet. | I don't have a licence for this {name} yet. |

Source: [`Assets/_Project/Data/NPCs/InnerVoice/ShelfBought.asset`](../../Assets/_Project/Data/NPCs/InnerVoice/ShelfBought.asset); asset `ShelfBought`, id `innervoice.shelf_bought`, cue `cue.shop.shelf_bought`. Editable data.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `Line` | Paid, and put by. | Paid, and put by. |

Source: [`Assets/_Project/Data/NPCs/InnerVoice/ShelfBagged.asset`](../../Assets/_Project/Data/NPCs/InnerVoice/ShelfBagged.asset); asset `ShelfBagged`, id `innervoice.shelf_bagged`, cue `cue.shop.shelf_bagged`. Editable data.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `Line` | Paid. In the bag. | Paid. In the bag. |

Source: [`Assets/_Project/Data/NPCs/InnerVoice/ShelfSoldOut.asset`](../../Assets/_Project/Data/NPCs/InnerVoice/ShelfSoldOut.asset); asset `ShelfSoldOut`, id `innervoice.shelf_sold_out`, cue `cue.shop.shelf_sold_out`. Editable data.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `Line` | Shelf's bare. Next run, maybe. | Shelf's bare. Next run, maybe. |

Source: [`Assets/_Project/Data/NPCs/InnerVoice/ShelfTooDear.asset`](../../Assets/_Project/Data/NPCs/InnerVoice/ShelfTooDear.asset); asset `ShelfTooDear`, id `innervoice.shelf_too_dear`, cue `cue.shop.shelf_too_dear`. Editable data.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `Line` | Can't afford that. | Can't afford that. |

Source: [`Assets/_Project/Data/NPCs/InnerVoice/ShelfHandsFull.asset`](../../Assets/_Project/Data/NPCs/InnerVoice/ShelfHandsFull.asset); asset `ShelfHandsFull`, id `innervoice.shelf_hands_full`, cue `cue.shop.shelf_hands_full`. Editable data.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `Line` | My hands are full. | My hands are full. |

Source: [`Assets/_Project/Data/NPCs/InnerVoice/ShelfBagFull.asset`](../../Assets/_Project/Data/NPCs/InnerVoice/ShelfBagFull.asset); asset `ShelfBagFull`, id `innervoice.shelf_bag_full`, cue `cue.shop.shelf_bag_full`. Editable data.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `Line` | The bag's full. | The bag's full. |

Source: [`Assets/_Project/Data/NPCs/InnerVoice/ShelfBagElsewhere.asset`](../../Assets/_Project/Data/NPCs/InnerVoice/ShelfBagElsewhere.asset); asset `ShelfBagElsewhere`, id `innervoice.shelf_bag_elsewhere`, cue `cue.shop.shelf_bag_elsewhere`. Editable data.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `Line` | I left my bag somewhere. I'll fetch it first. | I left my bag somewhere. I'll fetch it first. |

`{name}` in the licence sentence is the species' displayed name. `UnlicensedCatch` publishes the release before kept-catch delivery; no catch-page entry is earned for that release. Species coverage is driven by the licence lists: code does not automatically require a licence for an unlisted species. The owner’s rule covers every catch, so a future content pass must verify those lists rather than treating this copy as proof of universal coverage.

Shelf labels are still constants in [ShelfGood.cs](../../Assets/_Project/Code/World/Shops/ShelfGood.cs), with no text asset. Preserve the placeholders when moving them to data:

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `BuyLabelFormat` | Buy {0} — {1} | Buy {0} — {1} |
| `SoldOutLabelFormat` | {0} — sold out | {0} — sold out |

Here `{0}` is the item name and `{1}` its formatted price. A sold-out label refers to that item only, not the whole shop. Bag outcomes apply to goods configured for the paper bag; do not promise that a rod is tucked into a grocery bag.

## Licence and rod descriptions that can contradict the welcome

These catalogue descriptions are editable data; preserve their ids. They explain why a dialogue-only change is insufficient.

Source: [`Assets/_Project/Data/Resources/Catalog/Licenses/ClamLicense.asset`](../../Assets/_Project/Data/Resources/Catalog/Licenses/ClamLicense.asset); asset `ClamLicense`, id `license.clam`.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `DisplayName` | Shellfish Licence | Shellfish Licence |
| `Flavor` | A shellfish licence for the St Peters shore - clams off the flats, mussels and oysters picked by hand at low water - signed over the counter at the general store. The fee is small and the paperwork is one line, but no buyer on the island will weigh a bucket without it. The first licence of your life - and not the one you pay for yourself. | Signed at the island store. Covers clams, mussels and oysters from the shore. Without it, the catch goes back alive. Ginny fronts the first fee; your name goes on the paper. |

Source: [`Assets/_Project/Data/Resources/Catalog/Licenses/CodLicense.asset`](../../Assets/_Project/Data/Resources/Catalog/Licenses/CodLicense.asset); asset `CodLicense`, id `license.cod`.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `DisplayName` | Cod Fishing License | Cod Fishing Licence |
| `Flavor` | Nine Mile Creek's harbourmaster signs you off to take cod on rod and line. | The paper that lets you keep Atlantic cod. Sign for it before you bring one home. |

Source: [`Assets/_Project/Data/Resources/Catalog/Gear/Rod.asset`](../../Assets/_Project/Data/Resources/Catalog/Gear/Rod.asset); asset `Rod`, id `gear.rod`.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `DisplayName` | Fishing Rod | Fishing Rod |
| `Flavor` | A proper rod and reel - the step up from a hand-line. Fishes cod once you hold the cod licence. gameplay-systems maps gear.rod to the Rod Gear capability. | A rod and reel for learning the island water. Try your first casts from shore; a cod needs its licence if you're keeping it. |

The clam flavour currently implies a buyer refuses an unlicensed sale; the enforced licence check is at keeping the catch. The replacement names that actual rule. `license.cod` still lists only `seller.nmc_harbourmaster` on pinned main. `gear.rod` lists `seller.leblancs` and `seller.nmc_chandlery`; [StPetersGeneral.asset](../../Assets/_Project/Data/Shops/StPetersGeneral.asset) stocks `line.rod`, while [StPetersGeneralShelfSlots.asset](../../Assets/_Project/Data/Resources/ShopShelfSlots/StPetersGeneralShelfSlots.asset) has no rod purchase slot. Bait and bagged groceries do have slots. A listing and stock row alone do not put a rod purchase within reach. The old `DamagedDoryOffer.asset` still lists `seller.nmc_yard`, with paid repair. That offer is not the island slip sale and must not be silently relabelled as Basil's boat.

## The instruction pages already in the notebook

These three pages are loaded from Resources without a first-week unlock condition. Their headings and body text are editable data. Keep their existing local seamanship advice; no sentence currently sends the player to the mainland. A short additional licence sentence belongs with the fishing instructions: “Keep only what your licence covers. The rest goes back alive.” It must not imply a trap licence is for sale at the island counter.

Source: [`Assets/_Project/Data/Resources/Knowledge/HandLining.asset`](../../Assets/_Project/Data/Resources/Knowledge/HandLining.asset); asset `HandLining`, id `knowledge.hand_lining`, tab `tab.gear`.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `TabLabel` | GEAR | GEAR |
| `Head` | Hand-lining | Hand-lining |
| `Body[0]` | A line, a weight and a hook. Let it down until you feel the bottom, then bring it up a foot so the hook rides clear. | A line, a weight and a hook. Let it down until you feel the bottom, then bring it up a foot so the hook rides clear. |
| `Body[1]` | Fish take on the drop more often than the lift. Keep a finger on the line and you will feel it before you see it. | Fish take on the drop more often than the lift. Keep a finger on the line and you will feel it before you see it. |

Source: [`Assets/_Project/Data/Resources/Knowledge/ReadingTheTide.asset`](../../Assets/_Project/Data/Resources/Knowledge/ReadingTheTide.asset); asset `ReadingTheTide`, id `knowledge.reading_the_tide`, tab `tab.water`.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `TabLabel` | TIDE | TIDE |
| `Head` | Reading the tide | Reading the tide |
| `Body[0]` | The water does not sit still. Twice a day it climbs the shore and twice it falls back, and the difference between the two is greater here than almost anywhere. | The water does not sit still. Twice a day it climbs the shore and twice it falls back, and the difference between the two is greater here than almost anywhere. |
| `Body[1]` | Slack water is the hour either side of the turn. Little moves; gear set then stays close to where you put it. | Slack water is the hour either side of the turn. Little moves; gear set then stays close to where you put it. |
| `Body[2]` | The run is the rest of it. Set across a running tide and your gear will not be where you left it. | The run is the rest of it. Set across a running tide and your gear will not be where you left it. |

Source: [`Assets/_Project/Data/Resources/Knowledge/SettingATrap.asset`](../../Assets/_Project/Data/Resources/Knowledge/SettingATrap.asset); asset `SettingATrap`, id `knowledge.setting_a_trap`, tab `tab.gear`.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `TabLabel` | GEAR | GEAR |
| `Head` | Setting a trap | Setting a trap |
| `Body[0]` | Bait it, sink it, and mark it. A trap you cannot find again is a trap you have given away. | Bait it, sink it, and mark it. A trap you cannot find again is a trap you have given away. |
| `Body[1]` | Set on the slack where you can. Set on the run and the tide will walk your gear down the shore. | Set on the slack where you can. Set on the run and the tide will walk your gear down the shore. |

## Interface words: retain their meaning

These are shared constants, not island dialogue data. Move reusable labels to shared text data in a later text migration; leave their semantics alone in this proposal.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `WorldStrings.TalkVerb / ReadVerb` | Talk / Read | Talk / Read |
| `WorldStrings.LabelSeparator` |  —  |  —  |
| `WorldStrings.ContinueHint` | E ▸ | E ▸ |
| `WorldStrings.CloseConversationOption` | See you later. | See you later. |
| `WorldStrings.TasksFoldMark / TasksUnfoldMark` | - / + | - / + |
| `NotebookContent.LabelQuests / LabelDone` | TASKS / DONE | TASKS / DONE |
| `ShellStrings.TasksNoteLabel` | Tasks note | Tasks note |
| `ShellStrings.TasksNoteShown / TasksNoteChip / TasksNoteHidden` | Shown / Chip / Hidden | Shown / Chip / Hidden |

Files: [WorldStrings.cs](../../Assets/_Project/Code/World/WorldStrings.cs), [NotebookContent.cs](../../Assets/_Project/Code/World/NotebookContent.cs), [ShellStrings.cs](../../Assets/_Project/Code/UI/Shell/ShellStrings.cs). `QuestPanelPresenter` composes the folded “TASKS 1” label from `LabelQuests` and the task count; there is no separate sentence to replace. Display names “Aunt Ginny” and “Ned's Letter” come from the Npc assets on the island and remain unchanged. `ArrivalSea.asset` contains routing and positions, no authored opening speech.

## Old conversation fallbacks: not the island's active words

The following arrays are in `Assets/_Project/Code/World/WorldStrings.cs`, without an asset or localisation key. They are selected by `Conversation("ginny", ...)` or `Conversation("logbook", ...)` only when the interactable lacks NPC dialogue data. The St Peters references above do have that data. Keep this distinction: finding these lines does not prove the island currently speaks them.

If these fallbacks remain supported, give them data-backed versions too. They must not reintroduce an inherited dory if a reference is missing. For a later cove visit, author a cove-specific conversation separately; do not reuse an island welcome just because both speakers are Ginny.

Fallback array: `GinnyFirst`.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `GinnyFirst[0]` | Oh, child — you came. I'm so glad. Your uncle Ned thought the world of you, you know. | Oh, child — you came. I'm so glad. Your uncle Ned thought the world of you, you know. |
| `GinnyFirst[1]` | He's gone to the deep now, rest him. But he left you his dory — she's moored at the dock's end. | He's gone to the deep now, rest him. His cottage and his memory are yours. You'll earn your boat in your own time. |
| `GinnyFirst[2]` | She's small, and she's stubborn, but she's honest. Ned hand-lined a living from her for forty years. | Start with the clams at low water. Get the shellfish licence signed at the island store, and take your shovel and pail. |
| `GinnyFirst[3]` | Go on — take her out past the dock and drop a line. See what bites. The sea will teach you the rest. | Basil's broken dory is on the slip. Buy her when you can, then mend her by hand. |
| `GinnyFirst[4]` | When your hold's full, bring it back and sell at the wharf. We'll make a fisher of you yet. | Keep to island water while you learn. Come and see me when you've found your feet. We'll talk about the way off then. |

Fallback array: `GinnyAgain`.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `GinnyAgain[0]` | Back already? Good. The cove's kind today — take Ned's dory out and wet a line. | Back already? Good. There's work enough here while the weather has its say. |
| `GinnyAgain[1]` | Sell what you land at the wharf. Little by little, that's how a harbour gets built. | Clams at low water, then your pail up to the store. Little by little, you'll earn that boat. |

Fallback array: `LogbookFirst`.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `LogbookFirst[0]` | Ned's logbook lies open on the sill — the last pages, in a careful, weathered hand. | Ned's logbook lies open on the sill — the last pages, in a careful, weathered hand. |
| `LogbookFirst[1]` | "Squally out of the sou'west. Came home light. Some days the sea keeps her own." | "Squally out of the sou'west. Came home light. Some days the sea keeps her own." |
| `LogbookFirst[2]` | "The young one's coming to the island this spring. I've kept the dory sound for them." | "The young one's coming to the island. I wish I had a sound boat to leave him." |
| `LogbookFirst[3]` | "If you're reading this — she's yours now. Mind the tide, watch the sky, and don't fish angry." | "If you're reading this — earn a boat worth mending. Mind the tide, watch the sky, and don't fish angry." |
| `LogbookFirst[4]` | "There's a whole coast out there waiting. Go and meet it. — Ned" | "There's a whole coast out there waiting. Learn your own bit of water first. — Ned" |

Fallback array: `LogbookAgain`.

| Key / field | Today | Proposed wording |
| --- | --- | --- |
| `LogbookAgain[0]` | Ned's logbook. "Mind the tide, watch the sky, and go and meet the coast. — Ned" | Ned's logbook. "Mind the tide, watch the sky, and learn the water close to home. — Ned" |

## Ginny's word: one source, one endpoint

The [single source for Ginny's word](https://github.com/apholmes7-bot/hiddenharbours/blob/4b2d7f62f5d05f4b523c9f333fc3d1928914f472/docs/design/first-week-gate-word.md) owns the exact ending line, early replies, daytime invitation and full-game sleep reminder. Use `dialogue.st_peters.ginny_gate.word` from that page; this proposal supplies no alternate ending, weather-watch speech or early crossing direction.

All five facts from #1511 must hold for the addressed player, with day index 6 beginning day seven. Recheck them and the authored evening window at completion. A fresh licensed landing from the already-repaired dory counts; a stored shore fish carried aboard does not. Talking to Ginny early or finishing repairs cannot bank the later word.

The demo ends at that completed evening word. It supplies no next-morning note, crossing task or further playable time for the completed player. The full-game sleep and passage contract belongs to #1533; it is not another first-week instruction. No NPC or inner voice speaks at the helm. Talk to Ginny ashore, at her actual routine destination.

For two to four players, resolve dialogue conditions and completion against the addressed player's own facts. Do not choose speech through a singleton that means the only player, combine different players' facts, or complete another player's word. A participant's completion ends his own playable demo; shared closing presentation follows #1533. This page adds no multiplayer implementation.

The Notebook has one **CAUGHT** list and no **FREED** page. An unlicensed release earns no CAUGHT entry. Pinned main currently spells `NotebookContent.LabelCaught` as `CATCH`; the settled wording for a later text change is `CAUGHT`. Do not add a second list to keep both labels. Narration uses "the player" or "he".

Main's four sea-edge barriers remain dormant until `st_peters.first_week` is set, and `st_peters.weather_turned` lifts them immediately. Evening dialogue cannot write the latter. Walking-bar surf and demo termination still need integration; this wording does not certify a closed, playable island.

Main saves shelf purchases and paper-bag contents in version 26. Those rows do not prove a rod purchase point, carried-pail selection, repair jobs or word completion. The demo uses its own save file. This page changes no saved field or writer; proposed word/pending state stays in per-player session snapshots. A future save step needs player attribution, durable demo completion, authoritative full-game morning timing, atomic writes and exact reload behavior. No cross-edition transfer is promised.

## What can be edited as words, and what needs more work

| Surface | Editable today | Later work needed before the proposed words are true |
| --- | --- | --- |
| Ginny, the letter, the shopkeeper, Basil | Dialogue arrays and option labels/replies in the named assets | Coordinate Basil's asset with PR 1498. The shop order row needs appropriate routing. More detailed repair or gate speech needs its actual conditions. |
| Notebook quests and instruction pages | Titles, contexts, step labels, headings and body arrays | New week milestones need correct existing facts or flags; labels cannot create evidence. No save field is proposed here. |
| Licence, rod and shelf outcome descriptions | Catalogue `DisplayName`/`Flavor`; `InnerVoiceLineDef.Line` | Island cod-licence sale, universal species coverage and the missing rod purchase slot must be supplied. Price and stock stay data. |
| Pinned opening hints and old conversation arrays | Constants and arrays in code | Move authored sentences to a small opening-text asset or the established dialogue data, with stable text ids and validation. No runtime text catalogue for these ids exists today. |
| Shelf labels and notebook/shared interface labels | Constants in code | Shared text data should retain format tokens; no layout or appearance work is proposed. |
| Basil's sale messages | Editable data in PR 1539, not pinned main | Owning work supplies the slip sale. Store-sourced repair materials must be carried to the slip; the three hand jobs are accepted, while final material rows, quantities and prices remain separate content work. |
| Ginny's word | Exact copy owned by #1533 | Per-player five-fact eligibility, ashore evening talk and demo termination must be wired. Full-game crossing content is outside this first-week page. |

The existing sources above were searched before proposing any new text home. A later implementation should extend them, preserve stable identities and the existing flag store, and prove its words are selected at the right time. This document creates no class, validator, asset, saved field or game-line change. No local compilation, game tests, playthrough or layout check ran for this document; the verification is source-to-table transcription and repository links.

## Remaining content dependencies

- The first cod licence must be obtainable on St Peters. Its exact counter remains separate content work; the opening names only the island.
- Three hand jobs are accepted. Ordinary repair materials come from the island store and are carried to the slip; final rows, quantities and prices must make the words true.
- Four rods on the island shelf at 60 are accepted in #1554. Counted stock and the purchase point remain separate implementation; no fixed price is repeated in speech.
- The one-time shellfish-licence shortfall support in #1555 must not charge twice. The existing conditional reminder is flavour, not a new saved debt or repayment interaction.
- Ginny's first eligible ordinary evening talk and exact ending follow #1533. A missed evening remains available later; a guaranteed day-seven appointment is not supplied by current routines.

The speaker, evening endpoint, separate demo save, male player, single CAUGHT list, silence at the helm, island-only week and hand work are settled. This proposal changes only this document; it leaves all runtime and content sources for their own implementation.
