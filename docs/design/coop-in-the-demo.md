# Co-op in the demo

Status: **Proposed. Study and plan only.** No code, scene, art, package or save change is made here.
Checked against main `f808af6ae5` (save version 26). Nothing was built or run for this document.
The decisions it proposes are recorded in [ADR 0063](../adr/0063-coop-demo-authority-and-sync.md), also Proposed.

Words used: the **host** is the player whose computer runs the world. A **guest** is a friend who joins.
A **work day** is one focused day of one worker. Work days are rough estimates of mine, not measurements,
and they leave out review, waiting for tests, and new art.

---

## 1. The answer

**Two players in the demo: yes, realistic.** It is a real project, not a switch. About **55 to 95 work
days**. With three or four workers in parallel that is roughly two to three months, if the decisions in
section 8 are made early. Risk: **medium to high**, and almost all of it sits in one job: the game was
built around one player, and that has to be taken apart carefully (section 4).

**Four players in the demo: possible, and it uses the same design.** It is honest to call it **"two
first, four after"**. The plan below never writes the number two into the game, so four is a matter of
raising a limit, not of rebuilding. What four adds on top: the bunkhouse (section 5), an answer to "how
do four people use one dory", wider testing, and a speed check with four characters on screen. About
**10 to 18 work days more**, plus bunkhouse art. Risk: medium. I would ship the demo to you and your
friend with two, and open three and four once the bunkhouse and the dory question are settled.

**What you and your friend can test this month** is much smaller: the old trial, brought up to today's
game. You each sail your own dory at St Peters, see each other, and share one clock, tide and weather.
No shared catch, no shared money, nothing saved, no walking the village together. About **3 to 5 work
days**. That is slice 0 in section 7. It is a bug hunt and a first look, not the co-op demo.

**Playing with a friend through Steam invites** costs about **5 to 8 work days more**, and needs accounts
and choices that are yours to make (section 6). Until then, two PCs connect by typing an address.

| What | Rough effort | Risk | When |
|---|---|---|---|
| Two-PC test: two dories, one sea | 3 to 5 work days | Low to medium | First |
| Two-player co-op demo, host saves, guest remembered | 55 to 95 work days in all | Medium to high | After the decisions in section 8 |
| Four players and the bunkhouse | +10 to 18 work days, plus art | Medium | After two players work |
| Steam invites for the demo | +5 to 8 work days, plus your accounts | Medium | Any time after the two-PC test |

Why this is larger than the earlier estimate (35 to 60 work days in the campaign plan of 13 November):
the game has grown. The single shared "player" service is now read in about 1,500 places in 359 files
(it was 535 places in August). Hunger, body, cleanliness, the paper bag and the shelf ledger have all
arrived since, and each is one more thing that belongs to one person.

Why it is smaller than it could be: the demo is one island. Everyone is in the same scene. The hardest
part of the campaign plan, travelling between regions together, is not needed for the demo.

---

## 2. Stardew Valley as the yardstick

Stardew facts are written from what I know of the game. I have marked the ones I am less sure of.
"Drafts" means the open co-op work here: the trial (#1182), chat (#1183), the sleep rule (#1526), the
money and goods comparison (#1529) and the ownership map (#1521).

| Question | Stardew Valley | Our drafts today | Recommended for the demo | Your decision? |
|---|---|---|---|---|
| Who owns the world and the save | The host. The farm and every friend's character live in the host's save file. | The trial keeps no save at all. The plan documents propose one save on the host. | Same as Stardew: one world, one save, on the host's PC. | **Yes** (Q6) |
| How friends join | Steam or GOG friend invite, an invite code, or LAN and typed address. Split screen on one PC since 1.5. | Typed IPv4 address and a launch command, UDP port 7777. No menu, no invite. | Host and Join on the title menu, typed address first. Steam invites as a later slice. No split screen. | **Yes** for Steam (section 6) |
| Where friends live | Each friend needs a cabin on the farm. Cabins are built, or chosen when the farm is made. | Nothing. One guest bed exists at Ginny's. | Your ruling: two bunks in Ginny's guest room and in every home; a bunkhouse with two more for players three and four. | Ruled. Details in Q9 |
| Money | Shared by default. Separate wallets is an option, switched at the mayor's house. | One purse (your ruling of 4 August). #1529 compares the choices and leans the same way. | One shared purse. | **Yes** (Q1), to confirm |
| Sleep and the end of the day | The day ends when every player is in bed and agrees. Staying up to 2 a.m. makes a player pass out. | #1526: the night passes only when every player lies in a separate bunk. Sleep is the save. | Keep #1526. It already matches your ruling. | Ruled |
| Time | The clock keeps running while a player has a menu open. I believe it stops only when every player is in a menu or scene at once (*not certain*). | The trial copies the host's pause to the guest, so a host menu stops everyone. | The clock runs for everyone. A menu never stops a friend's day. The host's pause menu may stop the world; a guest's cannot. | **Yes** (Q10) |
| Who moves the story on | The big goals are shared. Friendships, skills and marriage are personal. Many quests are personal (*details not certain*). | Story flags are one list with no owner (#1521 names it as a debt). | Island facts are shared: the dory bought, the dory mended, Ginny's word. What each player has met, read and caught is his own. | No, unless you disagree |
| When the host leaves | The session ends for everyone. Friends cannot play that farm without the host. | Same in every draft. | Same. No handing the world to a guest. | No |
| Player count | Four as standard. Up to eight on PC from version 1.6. | Two, written into the trial's code. | Built for up to four. Demo opens with two, then four. | **Yes** (Q8) |
| Joining part-way | A friend can join a running game if a cabin is free. | The trial lets a second player connect at any time. | Join at any time ashore. Not during the opening at sea, not during a sleep. | **Yes** (Q4) |
| What a guest keeps | His character, items and skills, inside the host's save. Nothing goes home to his own farm. | Proposed: the guest keeps only his look and name. | Same as Stardew: the guest's character is remembered in the host's save. Nothing travels to his own game. | **Yes** (Q5, Q6) |
| A demo | Stardew has no demo. | A demo with its own save is drafted (#1510). | Co-op uses the demo's own save file, never the full game's. | No |

The lesson from Stardew that matters most here: **it added co-op about two years after release, with
outside help, because the game had been built for one player** (*as I recall it*). We are in the same
position, earlier. That is the cost in section 4.

---

## 3. What the trial (#1182) actually is

**What it does.** Two computers, each started with a launch command. Each player sits at the helm of
his own dory in open water (the East Water scene). Each sees the other's boat and skipper. Both see the
same hour, tide, wind, weather and fish schools.

**How it connects.** It uses Unity's own networking package (Netcode for GameObjects 2.13.3) over
Unity Transport, as plain UDP on port 7777 to a typed IPv4 address. There is no menu, no invite, no
relay and no lobby. Across two houses it needs a forwarded port or a private network; that has never
been proved. A third player is refused: the limit of two is written in the code.

**Who decides what.**
- Each player's own computer decides where his boat and skipper are, and tells the other. Nobody checks it.
- The host decides the clock. Five numbers are sent, five times a second: the world seed, the time, the
  clock's speed, whether it is paused, and a "ready" mark.
- Nothing else is decided by anyone, because nothing else is shared.

**What is sent.** Boat position and heading. Which hull it is. Where the skipper stands on it, which
way he faces and how he stands. The clock sample above. That is all.

**Why so little is enough.** In this game the sea is worked out, not stored. Tide, wind, weather, waves,
where the fish are, and where every villager stands are all calculated from the world seed and the
time. Two computers that agree on those two numbers agree on the whole sea without sending it. In most
games this is the largest and most fragile part of playing together. Here it is five numbers. This is
the single best thing the project has going for it, and the plan leans on it throughout.

**What is not shared.** Catch. Hands. The pail. Money. Shops and stock. Licences. The Notebook. Talk with
islanders. Quests. Sleep. Walking ashore together. Each player has his own free dory, which the real
game must never do. Nothing is saved: the trial replaces the save with one that lives only in memory,
and it skips the opening.

**Known faults.** It crashes when the window is closed in the ordinary way. A hull change can flood the
log with errors. It also adds a third package, Unity Relay, that it never uses.

**What it would take to bring it to today's main.**
- It is 264 commits behind. Six files were changed on both sides (the test workflow, one test, and four
  documents). The code itself is new files, so the clash is small.
- Its proposed ADR is numbered 0057, which main has since used for something else. #1243 moves it to
  0058. ADR 0063 here takes its place for the demo.
- Its three packages need your approval and the lead architect's. I recommend dropping Relay, leaving two.
- It reads the player, the dory and the helm in today's shapes, which have moved in 264 commits. It must
  be compiled and its own edit and play tests run again. That needs one editor session.
- For the demo it must start at St Peters, not East Water, because the demo is St Peters only.
- About 3 to 5 work days, plus 1 to 2 for the crash on close.

**How much of it survives into the real thing.** The connection setup, the address checking, the clock
sample and the tests are worth keeping. The rest is a sketch. In the real thing the host must decide
everything that is counted, and "the player" must become "this player" throughout.

---

## 4. What breaks with two to four players

The game has one of everything that belongs to a person. With two people, whichever was set up last
wins, and the other player's actions land on the wrong body. File and line references are in the appendix.

| Area | What main does today | What goes wrong with two | What it needs |
|---|---|---|---|
| The shared "player" services | `GameServices` holds one body, one pair of hands, one catch hands, one pail, one helm, one active boat, one set of needs, one look. | Your friend's dig checks **your** hands. His clam lands in **your** pail. | One record per player, and every question says which player is asking. This is the big job. |
| The camera | One follow camera. `Camera.main` is read in 26 places. | Fine on two PCs, since each has its own screen. Wrong only where code uses the camera to decide what is near "the player". | The camera follows the local player. Checks about a player use that player, not the camera. Small. |
| The save | One name, one look, one set of notes, one caught list, one rest place, one pail list, one "active boat". | A second player has nowhere to be written. | One small record per player inside the host's save. **A save format change.** |
| Hands and the paper bag | One hands component publishes itself as everyone's hands. The save marks a bag "held" but not by whom. | Two bags, two carriers, one slot. | Each object has one holder at a time, named. The host settles two grabs at once. |
| The chooser and input | The keyboard, mouse and pad are read directly in 84 places. The chooser is local, which is right. | A friend's choices must reach the host as requests. | Local menus stay local. The few counted actions (take, buy, sell, dig, sleep) are sent to the host. |
| The Notebook | One presenter, one notes list, one caught list. | A friend's catch is written into your Notebook. | Per-player notes and caught list. Shared tasks stay shared. |
| Talking to islanders | One conversation director. The relationship code names a single fixed "player". | Two people talking to Ginny at once fight over her. Gifts and friendship merge into one. | One conversation claim at a time per islander. Friendship keyed by player. |
| Shop stock and money | One stock per shelf line and one purse. No buyer is recorded. | Two buyers take the last loaf, or spend the same last coins. | The host puts purchases in order: one succeeds, one is politely refused. |
| Licences | One set, held by the outfit. | Works as shared. Wrong only if you want personal licences. | Nothing, if shared (Q2). |
| The dory | One fleet with one live boat, one hold and one "aboard" flag. One helm. | Two players cannot both be "aboard" in the code, and both would steer. | One helm holder at a time. A list of who is aboard. Where the second person sits or stands (*not checked on the dory's art*). |
| Sleep and the save | `RestQuorum` says the player count is one. One rest place is saved. | Night would pass with one player still awake. | #1526 fixes the rule. The per-player rest place needs the save change. |
| Scenes | One "current region". | Not a demo problem: St Peters is one scene, and its house interiors are storeys of that scene. | Each player's storey and cutaway is his own. The opening at sea moves between scenes, so guests join after landfall (Q4). |
| Pausing | One pause, used by every menu. | A guest opening a menu would stop the world. | Menus stop only the local player's input. |
| The demo's end | One story flag list. | Who triggers Ginny's word, and who sees it? | A shared fact, shown to every player present. The demo's ending rule is itself still open (#1533, #1535). |
| Events | No event says who did it. | "A fish was caught" cannot tell whose. | Add the player to the events that are counted. Additive. |

The ownership map in #1521 lists 77 named debts of this kind and adds a guard so new ones are caught.
Merging it first gives every later slice a way to measure progress. I checked its headline entries
against main `f808af6ae5`; they hold. Two things it could not know: main has since moved to save
version 26, and the paper bag and shelf ledger are now saved (#1529 covers both).

---

## 5. Beds

Your ruling: two bunks in the guest room at Ginny's; two in the trailer and in any home the player can
buy; a bunkhouse outside with two more, which exists only when three or four players have joined. The
earlier ruling stands: every player sleeps in a separate bunk.

**What exists today.**
- Ginny's cottage, in the St Peters scene: two beds. One is Ginny's. One is the player's, upstairs.
- The camper: one bed, in its prefab.
- Boats: bunks exist on larger hulls and already count as beds.
- Beds are marked "the player's bed: yes or no". There is no idea yet of "which player's".

| Place | Scene or prefab work | Art | Save work |
|---|---|---|---|
| Ginny's guest room | Add a second bed upstairs in the St Peters scene. This is a hand edit of a saved scene, done in the editor. Whether the room has floor space for two beds is **not checked**; a bunk bed may be needed. | A second single bed re-uses existing art. A bunk bed is new art. | None for the bed itself. |
| The camper | Add a second bed to the camper prefab. | The camper's interior art has one berth. A second needs a change to its drawing source. | None for the bed itself. |
| Any home for sale | A rule in data: every home a player can own has at least two player beds. A test refuses a home without them. | Per home, as each is drawn. | None. |
| The bunkhouse | A new prefab, placed from data, not from the scene (below). | **New art**: a small outbuilding, its inside, two beds, a door. | None for the building. Its storage uses keys that already exist in the save. |
| Every bed | "The player's bed" becomes "a bed any player may claim, one sleeper each". #1526 already carries the claim rule. | None. | Each player's waking place: **a save format change**, part of slice 11. |

**How the bunkhouse appears and disappears without touching the saved scene.**

The bunkhouse is not in the scene file. It is a prefab, with its position kept in a data file. When the
world loads, one small piece of code asks: how many players does this save know? If three or four, it
places the bunkhouse. If two or fewer, it does not. The building is worked out from the player count
each time, in the same spirit as the tide: recomputed, not saved.

I recommend counting **players known to the save**, not players connected right now. Otherwise the
bunkhouse would vanish every evening the third friend is busy, with his things inside it.

**What happens to things left inside.** Storage in this game is saved by a key (a name for the
container), not by the object in the scene. So a chest in the bunkhouse keeps its contents in the save
whether or not the building is standing. If the third player is removed from the save and the count
drops to two, three things are possible:

1. The bunkhouse goes, and its contents wait unseen in the save until it returns. Nothing is lost, but
   nothing can be reached.
2. The bunkhouse goes, and its contents are set out in a crate by Ginny's door. Stardew does something
   like this when a cabin is removed (*not certain of the detail*).
3. The bunkhouse stays for the life of that save once it has appeared.

I recommend **2**: nothing is lost and nothing is hidden. It is your call (Q9). A player whose saved
waking place was in the bunkhouse wakes in Ginny's guest room instead.

---

## 6. The demo and Steam

**Can a free demo be played with a friend? Yes.** On Steam a demo is its own small app with its own
App ID, attached to the main game's store page. Steam's friend invites, lobbies and relay service work
for a demo as they do for a full game. Both friends install the free demo; neither needs to own
anything. This is how free co-op demos spread: one player invites another, who needs only to click.

**Schedule I.** The full game has co-op for up to four through Steam friends. I could **not confirm**
from what I know that its free demo itself had co-op. Please treat that as unverified. The argument
does not depend on it: a free demo that friends can join together is allowed and is technically ordinary.

**Three ways two PCs can find each other.**

| Way | What the players do | What it needs from us | What it needs from you |
|---|---|---|---|
| Typed address, same house or forwarded port | The host reads out an address. The guest types it. | Already in the trial. | Nothing. Across two houses: a forwarded UDP port on the host's router, or a private network app on both PCs. Unproven so far. |
| Steam invite | The host clicks a friend's name. The friend clicks Accept. | A Steam connection layer under the same networking, and lobby code. | A Steamworks account, an App ID for the game and one for the demo. |
| Unity's relay service | The host reads out a short code. The guest types it. | Relay and sign-in code. | A Unity services account and its terms. |

For your own two-PC bug test this month, the typed address is enough and costs nothing. If your friend
is in another house, the cheapest route is a private network app on both PCs; choosing one is yours.

**Questions on cost and accounts. These are yours; I have assumed nothing.**

- S1. Is there a Steamworks partner account yet? I believe Steam charges a one-time fee per game, which
  is returned after a level of sales (*amount and terms to be checked on Steam's own pages*).
- S2. May we ask for a demo App ID once the game's App ID exists? I believe a demo needs the main
  game's store page to be public as "coming soon" first, and Steam reviews both (*to be checked*).
- S3. Steam invites or Unity relay, or both? I recommend Steam only: the players are on Steam already,
  and I believe Steam's relay has no charge to the developer (*to be checked*). Unity's relay has a
  free allowance and then charges by use (*to be checked*).
- S4. Before an App ID exists, developers commonly test Steam networking under Steam's public test
  app. Are you content for the two-PC test to do that, or should it wait for the real App ID?
- S5. Should the demo and the full game be able to play together? I recommend no: demo with demo only.
- S6. Is a typed address acceptable for the first test with your friend, with no Steam at all?

---

## 7. The build order

Each slice is one pull request unless marked, and can be tested alone. Smallest first. "Editor" means it
needs a running editor session. "Core" means it adds shared contracts and needs the lead architect.

| # | Slice | After it | Work days | Editor | Art | Save format | Runs beside |
|---|---|---|---|---|---|---|---|
| **0** | **The two-PC test.** Bring the trial (#1182) to today's main, off unless asked for. Start at St Peters. Drop the unused Relay package. Fix the crash on close. Build one zip with Host and Join launchers. | You and your friend each sail a dory at St Peters, see each other, and share the sea. Nothing else is shared. Nothing is saved. | 3 to 5, +1 to 2 for the crash | Yes | No | No | 1, 6 |
| 1 | **The player list.** A Core contract: who is in the session, which one is local, and the most allowed (a tunable, four). Merge the ownership guard (#1521) and the sleep rule (#1526) with it. | Nothing visible. Sleep already asks "is everyone in a separate bunk". | 2 to 3 | No | No | No | 0, 6 |
| 2 | **One record per player.** Two PRs. First the Core record (body, hands, pail, helm claim, needs, look) with today's single-player reads kept working. Then the features moved over, group by group. | Nothing visible with one player. Two bodies can exist without stealing each other's hands. | 8 to 12 | No for the first PR, yes for the second | No | No | 3, 4, 6 |
| 3 | **Host and Join on the title menu.** A guest's game cannot write a save. Wrong version or wrong seed is refused with a plain message. | Start a session from the menu with a typed address. | 3 to 5 | Yes | Small (menu) | No | 2, 4, 6 |
| 4 | **The whole clock.** Send the clock's hold and flow as well as the time. A guest's menu stops nobody. | Same hour on both screens in every case, including a held hour. | 2 to 4 | No | No | No | 2, 3, 6 |
| 5 | **Two bodies ashore.** The friend is seen walking the village, on the right storey indoors, with the right look. Each camera follows its own player. | Walk St Peters together. | 5 to 8 | Yes | Check only | No | 6 |
| 6 | **Two bunks.** A second bed in Ginny's guest room and in the camper. The rule and test for every home. | Two beds to sleep in. Works for one player too. | 3 to 5 | Yes | **Yes** if a bunk bed or a new camper berth is drawn | No | Anything. **Can start at once.** |
| 7 | **One dory, two people.** One at the helm or oars, one aboard. Boarding and leaving. | Row out together. **This is the go or stop point**: does standing on a moving boat feel right for the guest? | 5 to 8 | Yes | Check, perhaps a seated pose | No | 8 after 2 |
| 8 | **The host decides what is counted.** Two PRs. Catch, dig, pick up, put down, pail and paper bag each go to the host as a request and happen once. | Fish and dig together. Pass a pail. | 7 to 12 | Yes | Check only | No | 9, 10 |
| 9 | **One purse, one shelf.** Buying and selling put in order by the host. | Buy the last loaf once. Sell a pail once. | 4 to 7 | Yes | No | No | 8, 10 |
| 10 | **One story.** Two PRs. Island facts shared, personal facts personal. One talk at a time per islander. Notebook per player. The demo's end shown to all. | Be taught by Ginny together. Reach her word together. | 6 to 10 | Yes | No | No | 8, 9 |
| 11 | **The guest is remembered.** One small record per player in the host's demo save. Old saves still load. | Quit at night, come back as the same people in the same bunks. | 5 to 8 | No for the change, yes for one proof | No | **Yes. Your approval and a new version number.** | After 2, 8, 9, 10 |
| 12 | **Three and four.** The bunkhouse from the player count. The limit raised to four. | Four friends, four bunks. | 4 to 6 | Yes | **Yes: the bunkhouse** | Uses 11 | After 6, 11 |
| 13 | **Steam invites.** A Steam connection under the same networking, lobbies, the demo's App ID. | Invite a friend from the Steam list. | 5 to 8 | Yes | No | No | Any time after 3 |
| 14 | **Chat.** Bring #1183 to main on top of slice 0. | Type to each other. | 2 to 3 | Yes | Small | No | Any time after 0 |
| 15 | **The proving run.** Two real PCs, start to Ginny's word, sleep and resume. Frame rate and network use measured. | A demo you can hand to friends. | 3 to 5 | Yes | No | No | Last |

**What can start tomorrow, side by side:** slice 0 (one worker, one editor), slice 1 (no editor), the
first half of slice 2 (no editor), and slice 6 (editor and perhaps art). Slices 0 and 6 both need an
editor, so they take turns or use separate machines.

**Where the save format changes:** slice 11 only, and slice 12 leans on it. Until then a guest is not
remembered between sessions and joins with empty hands. Each of those is a question for you (Q6, Q7).

**Where to stop and look:** after slice 7. If two people in one dory does not feel right on your
friend's real connection, the plan changes there, cheaply, before the expensive slices.

**What I would not do:** build the opening at sea for two before the island works for two. It moves
between scenes and holds the clock, and it is the hardest place to join. The earlier draft plan put it
early. For the demo I recommend the guest arrives after the host is ashore (Q4).

---

## 8. Decisions only you can make

1. **One purse or one each?** Recommend: **one shared purse**, as you ruled on 4 August and as Stardew
   does by default. Separate purses roughly doubles the money work and splits the dory fund.
2. **Licences shared or personal?** Recommend: **shared** for ordinary licences. The scarce lobster and
   crab licences stay one-of-a-kind and are not part of the first week.
3. **Whose dory is it, and who rows?** Recommend: **the outfit's dory**. Either player may take the oars
   when aboard; one at a time. No second free dory. Nobody can move it from a distance.
4. **Join at any time, or only from a lobby before the day starts?** Recommend: **join at any time
   while the host is ashore.** Not during the opening at sea, and not during a sleep. A guest arrives at
   Ginny's guest room. This sets aside the earlier wish for both to be aboard the opening boat; that
   can come after the demo.
5. **What does a guest keep when he leaves?** Recommend: **his character stays in the host's world**:
   look, name, Notebook, what he has caught, where he sleeps. What he is carrying is set down where he
   stood. Nothing goes home to his own game.
6. **Is a guest's progress saved on his own PC, or only on the host's?** Recommend: **only the host's**,
   as in Stardew. The guest's PC writes no save at all. This needs your yes to a save format change
   (slice 11). Without it, guests start fresh each session.
7. **May the save format change for this, and when?** Recommend: **yes, once, at slice 11**, with a new
   version number you hand out, and only in the demo's own save file first.
8. **How many players in the demo?** Recommend: **two at first, four once the bunkhouse is in.** Built
   for four from the start.
9. **When the third player is gone for good, what happens to the bunkhouse and his things?** Recommend:
   the bunkhouse goes when the save knows fewer than three players, and his things are set out in a
   crate by Ginny's door.
10. **Whose menu can stop the clock?** Recommend: **only the host's pause menu.** Nobody else's.
11. **May the host play on alone when the friend is away?** Recommend: **yes.** The friend's character
    sleeps in his bunk and catches up with the world when he returns.
12. **May either player make a large purchase alone?** Recommend: everyday supplies, yes. Buying or
    selling a boat needs both players present to agree. (From #1529.)
13. **May we add two networking packages?** Recommend: **yes** to Unity's networking and transport
    packages, **no** to Relay for now. This also needs the lead architect.
14. **The Steam questions S1 to S6 in section 6.**

---

## What I could not verify

- Nothing was built or run. No two-PC session, no editor, no test run.
- Whether the trial still compiles against today's main. It is 264 commits behind.
- Whether Ginny's guest room has floor space for a second bed, and whether a dory has room for two.
- Whether an old trial zip still exists to play today. The friend guide of 13 November describes one.
- Schedule I's demo having co-op. Steam's and Unity's fees and terms. Stardew details marked above.
- How the demo ends (#1533, #1535) and the demo's own save file (#1510) are open drafts, not on main.
- All work-day figures are estimates.

---

## Appendix: where the claims come from

Main `f808af6ae5`. Paths are under `Assets/_Project/Code/` unless they start with another folder.

**Single-player assumptions on main**

| Claim | File and line |
|---|---|
| Save version is 26 | `Core/Save/SaveMigration.cs:16` |
| One wallet, one licence set, one active boat, one helm | `Core/Services/GameServices.cs:37`, `:47`, `:56`, `:69`, `:89` |
| One look, needs, body, strength, cleanliness | `Core/Services/GameServices.cs:294`, `:326`, `:342`, `:369`, `:396` |
| One body, hands, catch hands, pail | `Core/Services/GameServices.cs:678`, `:714`, `:738`, `:788` |
| One current region, one arrival key, one whereabouts | `Core/Services/GameServices.cs:807`, `:828`, `:876` |
| Hands publish themselves as everyone's | `Player/CarryHands.cs:242-243` |
| The pail publishes itself as everyone's | `Player/ClamBucket.cs:91` |
| The body is published by the travel coordinator | `App/RegionTravelCoordinator.cs:124` |
| The boat that enabled last is the active boat | `Boats/ActiveBoatProbe.cs:48` |
| One live boat, hold and aboard flag | `Boats/OwnedFleet.cs:31`, `:32`, `:44` |
| The shared service is read 1,509 times in 359 files | counted with `git grep "GameServices\."` |
| `Camera.main` is read 26 times in 21 files | counted with `git grep "Camera\.main"` |
| Devices are read directly 84 times in 42 files | counted with `git grep` for `Keyboard`, `Mouse`, `Gamepad` `.current` |
| No event names a player | `git grep -i "PlayerId\|ActorId\|SenderId"` in `Core/Events/` finds nothing |
| Catch, sale and money events | `Core/Events/GameSignals.cs:43`, `:50`, `:84` |
| One fixed "player" in relationships, used on talk | `Core/Relationships/RelationshipGraph.cs:77`, `World/WorldInteractor.cs:555` |
| One catch log | `Core/Fishing/CatchRecord.cs:275` |
| One gift ledger | `World/Gifts/GiftLedger.cs:25` |
| One licence service | `Economy/LicenseService.cs:25` |
| One Notebook presenter | `World/NotebookPresenter.cs:222` |
| One conversation director | `World/Conversations/NpcConversationDirector.cs:62` |
| One shelf stock per line, no buyer | `Economy/Island/IslandStoreShelves.cs:145` |
| Player count is one; one local sleeper | `Core/Save/RestQuorum.cs:20`, `:24`; `Core/Save/SleepFlow.cs:90`, `:132` |
| One pause path | `Core/Shell/ShellPause.cs:7`, `Core/Shell/ShellFlow.cs:75` |
| Save holds one of each personal fact | `Core/Save/SaveData.cs:32` (money), `:44` (active hull), `:105` (pail), `:210` (notes), `:283`, `:309` (rest), `:354` (name), `:445` (caught) |
| Shelf withdrawals and paper bags are saved, with no buyer or carrier | `Core/Save/SaveData.cs:489`, `:512` |
| Storage is saved by key | `Core/Save/StoredContainers.cs:19` |
| The one save file name | `Core/Save/SaveStore.cs:24` |
| No demo switch or demo save in code on main | `git grep -i demo` under the code folder finds only comments |
| No networking package on main | `Packages/manifest.json` has no netcode, transport, relay or Steam entry |

**What helps**

| Claim | File and line |
|---|---|
| The clock can be set to a given time | `Environment/GameClock.cs:76`, `Core/Services/IGameClock.cs:48` |
| Tide, weather, fish schools and village routines are worked out | `Environment/TideModel.cs:11`, `Environment/WeatherModel.cs:17`, `Fishing/FishSchoolMath.cs:43`, `World/Routines/RoutineSchedule.cs:26` |
| The interact verb already takes "who is reaching" as a value | `Core/Interaction/InteractActor.cs:14` |
| Input intents exist for the chooser, controls and HUD | `Core/Input/` (six files) |
| Beds claim one sleeper each | `Core/Save/RestQuorum.cs:39` |

**Beds**

| Claim | File and line |
|---|---|
| Ginny's cottage has an owner's bed and a player's bed | `Assets/_Project/Scenes/StPeters.unity:72182`, `:77448-77449` |
| They are placed by the interiors builder; the player's is upstairs | `App/Editor/StPetersInteriors.cs:1024-1040` |
| The camper has one player's bed | `Assets/_Project/Prefabs/Dwellings/CamperClipperDwelling.prefab:474`, `App/Editor/DwellingPrefabBuilder.cs:182-186` |
| A bed is "the player's" by a yes or no field | `World/InteriorBed.cs:50` and the field below it |
| Boat bunks are beds | `Boats/BoatBunk.cs:49` |

**The trial, at `f1df445194` (#1182)**

| Claim | File and line on that branch |
|---|---|
| Packages added: netcode 2.13.3, transport 2.7.4, relay 1.2.0 | `Packages/manifest.json` |
| Limit of two players | `App/CoopComposition/CoopDevStart.cs`, the connection approval callback |
| Typed IPv4 address, port 7777, launch commands | `App/CoopComposition/CoopDevStart.cs` (`CoopLaunchOptions`), `CoopSpikeSettings.cs` |
| Memory-only save; opening switched off | `App/CoopComposition/CoopDevStart.cs` (`SessionSave`, `Awake`, `Start`) |
| Starts in East Water | `App/CoopComposition/CoopSpikeSettings.cs` (`Scene = "EastWater"`) |
| The owner of a boat writes its state; the host writes the clock | `App/CoopSpike/CoopBoatReplica.cs` |
| What the boat state holds | `App/CoopSpike/CoopBoatState.cs` |
| What the clock sample holds; refused on a different seed | `App/CoopSpike/CoopSeaState.cs` |
| Guest copies the host's pause and speed | `App/CoopSpike/CoopBoatReplica.cs`, `Update` |
| 264 commits behind; six files changed on both sides | `git rev-list --count` and `git diff --name-only` against the merge base `513761a57b` |

**Inputs read:** #1182, #1183, #1243, #1521 (at `b54f3af69d`), #1526, #1529,
[campaign-coop-plan.md](campaign-coop-plan.md), [multiplayer-scoping.md](multiplayer-scoping.md),
[ADR 0062](../adr/0062-the-first-week-is-closed-by-weather.md).
