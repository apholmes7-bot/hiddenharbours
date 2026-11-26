# Co-op in the demo

Status: **Plan, revised to the owner's answers of 2026-11-25. Study and plan only.** No code, scene, art, package or save change is made here.
Checked against main `f808af6ae5` (save version 26). Nothing was built or run for this document.
The decisions it proposes are recorded in [ADR 0063](../adr/0063-coop-demo-authority-and-sync.md), also Proposed.

Words used: the **host** is the player whose computer runs the world. A **guest** is a friend who joins.
A **work day** is one focused day of one worker. Work days are rough estimates of mine, not measurements,
and they leave out review, waiting for tests, and new art.

---

## 1. The answer

*Revised 2026-11-25 after the owner answered the questions in section 8. His answers are recorded there.*

**Two players in the demo: yes, realistic.** It is a real project, not a switch. About **70 to 115 work
days**. With three or four workers in parallel that is roughly two and a half to four months. Risk:
**medium to high**. Most of it sits in two jobs: the game was built around one player (section 4), and
the owner's answer on joining means the host's computer must keep **two places running at once** when
the players are apart (section 7, slice 8).

**The estimate went up from 55 to 95.** Three of his answers add work, and I would rather say so:
- *Join at any time, wherever the host is, and the players may be in different scenes:* about 10 to 16
  work days. This is the largest single addition.
- *A guest's held things and last bed are remembered on the host:* about 2 to 3 more in the save slice.
- *One rows while the other fishes:* about 1 to 2 more in the dory slice.

**Four players: possible with the same design, as "two first, four after".** He chose two at first. The
plan never writes the number two into the game, so four is a matter of raising a limit plus the
bunkhouse (section 5), wider testing and a speed check. About **10 to 18 work days more**, plus art.

**What he and his friend can test first** is slice 0: each sails his own dory at St Peters, they see
each other, and they share one clock, tide and weather. No shared catch, no shared money, nothing
saved. It connects by a typed address, with no Steam, as he asked.

**Steam is parked.** He wants the demo working for two without Steam for now. Steam invites stay in the
plan as a later, separate slice of about 5 to 8 work days.

| What | Rough effort | Risk | When |
|---|---|---|---|
| Two-PC test: two dories, one sea | 3 to 5 work days | Low to medium | Now (slice 0) |
| Two-player co-op demo, join anywhere, host saves, guest remembered | 70 to 115 work days in all | Medium to high | Slices 1 to 12 and 16 |
| Four players and the bunkhouse | +10 to 18 work days, plus art | Medium | After two players work |
| Steam invites for the demo | +5 to 8 work days, plus accounts | Medium | Parked |

Why this is larger than the campaign plan of 13 November (35 to 60): the game has grown. The single
shared "player" service is now read in about 1,500 places in 359 files (it was 535 places in August).
Hunger, body, cleanliness, the paper bag and the shelf ledger have all arrived since, and each is one
more thing that belongs to one person.

---

## 2. Stardew Valley as the yardstick

Stardew facts are written from what I know of the game. I have marked the ones I am less sure of.
"Drafts" means the open co-op work here: the trial (#1182), chat (#1183), the sleep rule (#1526), the
money and goods comparison (#1529) and the ownership map (#1521). The last column is now the owner's
answer of 2026-11-25 where he gave one.

| Question | Stardew Valley | Our drafts today | What this game will do | Settled? |
|---|---|---|---|---|
| Who owns the world and the save | The host. The farm and every friend's character live in the host's save file. | The trial keeps no save at all. | Same as Stardew: one world, one save, on the host's PC. A guest's PC saves nothing. | **Ruled** |
| How friends join | Steam or GOG friend invite, an invite code, or LAN and typed address. Split screen on one PC since 1.5. | Typed IPv4 address and a launch command, UDP port 7777. | A typed address, with no Steam, for now. Steam invites later. No split screen. | **Ruled** (no Steam for now) |
| Where friends live | Each friend needs a cabin on the farm. | One guest bed exists at Ginny's. | Two bunks in Ginny's guest room and in every home. A bunkhouse with two more for players three and four. | **Ruled** |
| Money | Shared by default. Separate wallets is an option. | One purse. | One shared purse. Either player may buy anything alone. | **Ruled** |
| Licences | Not a Stardew idea. | One set held by the outfit. | Shared. | **Ruled** |
| The boat | Not a Stardew idea. | Each player has his own free dory in the trial. | One shared dory. Two can ride. One rows. The other may fish while he is rowed. Both may fish when nobody rows. The same on any boat with someone at the helm, though fishing at speed may not be workable. | **Ruled** |
| Sleep and the end of the day | The day ends when every player is in bed and agrees. | #1526: the night passes only when every player lies in a separate bunk. Sleep is the save. | Keep #1526. | **Ruled** |
| Time and pausing | The clock keeps running while a player has a menu open. I believe it stops only when every player is in a menu at once (*not certain*). | The trial copies the host's pause to the guest. | The clock runs for everyone. **Only the host can pause.** | **Ruled** |
| Being apart | Players go anywhere on their own: farm, town, mine. The host's computer keeps every place a player is in running. | The earlier plan kept both players in one region. | Players may be in **different scenes at once**. The host keeps both running. | **Ruled**. Cost in section 7, slice 8 |
| Joining part-way | A friend can join a running game if a cabin is free. He wakes in his cabin. | The trial lets a second player connect at any time. | **Join at any time, wherever the host is.** The joiner appears at his last saved bed. If that bed was on a boat and the boat is gone, at his last bed on land. | **Ruled** |
| What a guest keeps | His character, items and skills, inside the host's save. Nothing goes home to his own farm. | Proposed: only his look and name. | When he leaves, **his character and whatever he holds vanish with him** and come back with him, the same, next time. Kept on the host's PC only. | **Ruled**. Needs a save change (slice 12): his separate yes |
| Who moves the story on | The big goals are shared. Friendships, skills and marriage are personal (*details not certain*). | Story flags are one list with no owner. | Island facts are shared: the dory bought, the dory mended, Ginny's word. What each player has met, read and caught is his own. | Recommended, not asked |
| When the host leaves | The session ends for everyone. | Same in every draft. | Same. The host may also play alone when his friend is away. | **Ruled** (host alone: yes) |
| Player count | Four as standard. Up to eight on PC from version 1.6. | Two, written into the trial's code. | **Two at first.** Built for up to four. | **Ruled** |
| A demo | Stardew has no demo. | A demo with its own save is drafted (#1510). | Co-op uses the demo's own save file, never the full game's. | Recommended, not asked |

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
| Licences | One set, held by the outfit. | Works as shared. The owner ruled shared. | Nothing. |
| The dory | One fleet with one live boat, one hold and one "aboard" flag. One helm. | Two players cannot both be "aboard" in the code, and both would steer. | One helm holder at a time. A list of who is aboard. Where the second person sits or stands (*not checked on the dory's art*). |
| Sleep and the save | `RestQuorum` says the player count is one. One rest place is saved. | Night would pass with one player still awake. | #1526 fixes the rule. The per-player rest place needs the save change. |
| Scenes | One "current region", one arrival key, one active scene. | The owner ruled that players may be in different scenes at once. The host must then run two. | Slice 8: a region per player, and the host keeps the second scene running unseen. House interiors are storeys of the same scene, so each player's storey and cutaway is simply his own. |
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

**The owner chose 3** (2026-11-25): "bunkhouse stays, things stay with player until return." Once a save
has known a third player, the bunkhouse is always placed for that save, and nothing is moved out of it.

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

*Revised 2026-11-25 to the owner's answers. Slice 8 is new. Slices 7 and 12 grew. Steam moved to the end.*

Each slice is one pull request unless marked, and can be tested alone. Smallest first. "Editor" means it
needs a running editor session. "Core" means it adds shared contracts and needs the lead architect.

| # | Slice | After it | Work days | Editor | Art | Save format | Runs beside |
|---|---|---|---|---|---|---|---|
| **0** | **The two-PC test.** The trial (#1182) rebuilt on today's main, off unless asked for. Starts at St Peters. Two packages, not three. A plain Host and Join entry that exists only in a development or demo build. Typed address. | Two friends each sail a dory at St Peters, see each other, and share the sea. Nothing else is shared. Nothing is saved. | 3 to 5 | Yes | No | No | 1, 6 |
| 1 | **The player list.** A Core contract: who is in the session, which one is local, and the most allowed (a tunable). Merge the ownership guard (#1521) and the sleep rule (#1526) with it. | Nothing visible. Sleep already asks "is everyone in a separate bunk". | 2 to 3 | No | No | No | 0, 6 |
| 2 | **One record per player.** Two PRs. First the Core record (body, hands, pail, helm claim, needs, look, **which region he is in**) with today's single-player reads kept working. Then the features moved over, group by group. | Nothing visible with one player. Two bodies can exist without stealing each other's hands. | 8 to 12 | No, then yes | No | No | 3, 4, 6 |
| 3 | **Host and Join on the real title menu.** A guest's game cannot write a save. Wrong version or wrong seed is refused with a plain message. | Start a session from the menu with a typed address. | 3 to 5 | Yes | Small (menu) | No | 2, 4, 6 |
| 4 | **The whole clock.** Send the clock's hold and flow as well as the time. Only the host's pause stops the world. | Same hour on both screens in every case. | 2 to 4 | No | No | No | 2, 3, 6 |
| 5 | **Two bodies ashore.** The friend is seen walking the village, on the right storey indoors, with the right look. Each camera follows its own player. Until slice 12 a joiner appears at Ginny's guest room with empty hands. | Walk St Peters together. | 5 to 8 | Yes | Check only | No | 6 |
| 6 | **Two bunks.** A second bed in Ginny's guest room and in the camper. The rule and test for every home. | Two beds to sleep in. Works for one player too. | 3 to 5 | Yes | **Yes** if a bunk bed or a new camper berth is drawn | No | Anything. **Can start at once.** |
| 7 | **One dory, two people.** One rows, one rides. The rider may fish while rowed. Both may fish when nobody rows. The same rule for any boat with someone at the helm; whether fishing works at speed is tested, not promised. | Row out together and fish. **This is the go or stop point**: does standing and fishing on a moving boat feel right for the guest? | 6 to 10 | Yes | Check, perhaps a seated pose | No | 8 |
| **8** | **Two places at once.** Two PRs. The host's computer keeps a second scene running when a player is in it: its tide and weather, its islanders, its boats and its counted things. Each computer shows and hears only the place its own player is in. | One player stays ashore while the other is somewhere else. A friend can join wherever the host is. | 10 to 16 | Yes | No | No | 7, 9 |
| 9 | **The host decides what is counted.** Two PRs. Catch, dig, pick up, put down, pail and paper bag each go to the host as a request and happen once. | Fish and dig together. Pass a pail. | 7 to 12 | Yes | Check only | No | 10, 11 |
| 10 | **One purse, one shelf.** Buying and selling put in order by the host. Either player may buy anything alone. | Buy the last loaf once. Sell a pail once. | 4 to 7 | Yes | No | No | 9, 11 |
| 11 | **One story.** Two PRs. Island facts shared, personal facts personal. One talk at a time per islander. Notebook per player. The demo's end shown to all. | Be taught by Ginny together. Reach her word together. | 6 to 10 | Yes | No | No | 9, 10 |
| **12** | **The guest is remembered.** One record per player in the host's demo save: look, Notebook, what he caught, hunger and body, **what he holds**, **his last bed on land and his last bed on a boat**. Leaving removes his character and held things from the world. Returning puts him at his last saved bed with the same things; if that was a boat bed and the boat is gone, at his last land bed. Old saves still load. | Quit at night, come back as the same people in the same bunks with the same things. | 7 to 11 | No for the change, yes for one proof | No | **Yes. This is the slice where the save changes. It needs his separate yes and a new version number.** | After 2, 9, 10, 11 |
| 13 | **Three and four.** The bunkhouse appears when the save knows a third player, and then stays. The limit raised to four. | Four friends, four bunks. | 4 to 6 | Yes | **Yes: the bunkhouse** | Uses 12 | After 6, 12 |
| 14 | **Chat.** Bring #1183 to main on top of slice 0. | Type to each other. | 2 to 3 | Yes | Small | No | Any time after 0 |
| 15 | **The proving run.** Two real PCs, start to Ginny's word, apart and together, sleep and resume. Frame rate and network use measured. | A demo to hand to friends. | 3 to 5 | Yes | No | No | Last |
| 16 | **Steam invites.** Parked by the owner for now. | Invite a friend from the Steam list. | 5 to 8 | Yes | No | No | When he asks |

**What can run side by side now:** slice 0, slice 1 (no editor), the first half of slice 2 (no editor),
and slice 6 (editor and perhaps art).

**Where the save format changes:** slice 12 only, and slice 13 leans on it. Until then a guest is not
remembered between sessions, appears at Ginny's guest room and has empty hands.

**Where to stop and look:** after slice 7, and again after slice 8.

### What "two places at once" needs (slice 8)

Loading a second region scene beside the first already works: that is how travel loads a region today.
What does not exist is two scenes that are both *lived in*. Honestly, this is what it takes.

- **Who runs the second place.** The host's computer does, because the host decides everything that is
  counted. So when the guest is somewhere the host is not, the host loads that scene too and keeps it
  running without drawing it. The guest's computer loads only the place the guest is in.
- **One "current region" becomes one per player.** Today there is a single current region, a single
  arrival key and a single "where is the player" answer (`GameServices.cs:807`, `:828`, `:876`), and
  the scene loader makes one scene the active one (`RegionSceneLoader.cs:85`). Each of these has to
  ask "for which player".
- **Tide, wind and weather.** These are worked out from seed and time, so a second place costs no
  network traffic. But the service that answers them holds one region's profile at a time. It has to
  answer for a named region, so that an islander or a boat in the unseen scene reads its own tide.
- **Islanders.** Their day is worked out from the clock, which is cheap. The parts that are not are a
  conversation in progress and anything an islander does that changes goods or money. The host must
  run those for both places. The code that drives islanders asks for one current region today.
- **Things that tick.** Boats, traps, spoiling catch, shelf stock. In the unseen scene the host keeps
  the counted parts on the slow tick and switches off everything that only exists to be looked at:
  water, particles, wakes, lights, animation.
- **The host's camera.** It follows the host only and never draws the other scene. Regions sit in one
  shared world frame (ADR 0057, proposed, as I read it), so two scenes do not lie on top of each other, which
  makes this a matter of what is switched on, not of moving anything.
- **Sound.** Each computer hears only its own player's place. The unseen scene on the host is silent.
- **Speed and memory.** The host pays for a second scene's objects and textures. This is the real
  risk: the target is 60 frames a second on an ordinary laptop, and the host with a friend in a second
  scene is the worst case. It must be measured on a real machine before the slice is accepted. If it
  does not fit, the unseen scene is thinned further; the picture the host sees is not degraded.
- **Joining anywhere.** A joiner asks the host for his last saved bed. If that place is not loaded, the
  host loads it, then lets him in. Until slice 12 there is no saved bed, so he appears at Ginny's.
- **For the demo this is smaller than for the full game.** The demo is St Peters, plus the water scene
  of the opening. Two live scenes is the limit that needs proving; four players in four scenes is not
  promised by this slice.

**What I would not do:** build the opening at sea for two as its own special case. With slice 8 in
place it is simply "the host is in another scene", and a friend who joins then appears at his bed on
the island and waits for the boat to come in.

---

## 8. The owner's decisions

Answered 2026-11-25. His words are quoted where he gave them.

| # | Question | His answer | What it means for the build |
|---|---|---|---|
| 1 | One purse or one each? | "one shared" | One balance. The host puts spending in order. |
| 2 | Licences shared or personal? | "shared" | One set for the outfit. |
| 3 | Whose dory, and who rows? | "one shared, 2 players can ride, one rows, one can fish if the other is rowing, both can fish if they arent rowing, same rules to any boat if someone as at the helm but fishing while sailing fast may not be viable." | Slice 7. One helm or oars holder at a time. Fishing is allowed for anyone aboard who is not rowing or steering. Fishing at speed is tested before it is promised. |
| 4 | Join at any time, or lobby only? | "join anytime regardless where host is, we need 2 scenes to be functinal at once incase player is another scene. loaded player spawns at last saved bed, if last saved bed was a boat and boat is somehow destroyed they will spawn at their last land bed." | Slice 8 (two places at once) and slice 12 (two saved beds per player: last on land, last on a boat). |
| 5 | What does a guest keep when he leaves? | "character disappears and anything held dissapears with him until returned to game then they still have same items." | Slice 12. Held things are stored with his record, not set down in the world. |
| 6 | Where is a guest's progress saved? | The host's PC only. | A guest's game never writes a save. |
| 7 | How many players in the demo? | Two at first. | Built for a list, limit set to two, raised later. |
| 8 | The bunkhouse when a third player is gone | "bunkhouse stays, things stay with player until return." | Once a save has known a third player, the bunkhouse is always placed. Nothing is moved out of it. |
| 9 | Whose menu can stop the clock? | Only the host can pause. | Slice 4. |
| 10 | May the host play on alone? | Yes. | The absent friend's character is not in the world. |
| 11 | May one player make any purchase alone? | "yes for everthing" | No agreement step, even for a boat. |
| 12 | Steam | "yes i need demo to be functianl for me and my friend without steam for now" | A typed address. Steam is parked (slice 16). |

**Still his to decide, each when its slice comes up:**

- **The save format change at slice 12.** One record per player on the host: look, Notebook, catch
  list, needs, what he holds, his last land bed and last boat bed. It needs his separate yes and a
  version number handed out then. Nothing in this plan changes the save before that.
- **The two networking packages.** They arrive on the slice 0 draft PR and are not merged without his
  explicit yes.
- **Two live scenes, measured.** If the host's frame rate with a friend in a second scene does not
  hold on his laptop, he chooses between a thinner unseen scene and a lower promise.
- **A lost boat bed.** His rule covers the boat being destroyed. Not yet covered: the boat exists but
  is in a scene nobody is in, or is under way with another player at the helm. I recommend the joiner
  appears aboard if the boat exists anywhere, and that is checked with him at slice 12.
- **What "leaving" does to a pail or bag a guest was carrying for the outfit.** His answer means the
  shared clams leave the world with him until he returns. I recommend keeping exactly that, as ruled,
  and saying so on screen when a player leaves holding goods.
- **Steam questions S1 to S6** in section 6, whenever Steam is wanted.

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
