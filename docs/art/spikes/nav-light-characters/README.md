# Nav light characters — the acceptance plates (boat-lights PR 2b, 2026-09-04)

Shot in the **running game** at Nine Mile Creek on the **shipped exposure** — the day/night profile's
own 02:00, the post-#709 moonlit night, the shipped `NavLightPresets`. Nothing here is a mock-up or a
lit rig: every frame is `Camera.main` rendered through the same overlay and the same additive light
quads the player sees, at the game's own pixel-perfect zoom, read back through the linear→gamma
conversion the project needs (a raw float readback saves far too dark).

**The clock is frozen and SEEKED.** `TimeScale = 0` stops the world; `SeekTo(t)` then chooses the
instant. That is only possible *because* a mark's light is `IsOn(totalSeconds, phase)` — a pure
function of the master clock and nothing else. Every strip below is one scene at a series of exact
instants, not a recording, and the LIT/dark label under each frame was **read off the running
component** (`NavLight.IsBurning`), not asserted by hand.

---

## 1 · `Fl G 4s` — a port hand, one second lit in four

`channel.nmc_entrance.p2`, sampled every 0.5 s across a whole period.

![Fl G 4s](strip-flg4.png)

Two frames of eight are lit, which is the character: one second of green, three of dark.

## 2 · `Q(3) 10s` — the east cardinal, three quicks then a pause

`mark.nmc_breakwater_head`, sampled every 0.5 s. Three quick flashes at 0, 1 and 2 seconds, then dark
for the rest of her ten. The double-cone topmark stays readable through the flash — see §4.

![Q(3) 10s](strip-q3.png)

## 3 · The approach at 02:00

The buoyed channel from seaward, no boat under way. Two **red** starboard-hand marks and one **green**
port hand are burning; a **white** cardinal burns astern of them; two more marks sit dark, waiting
their turn in their own periods. The lights along the wharf at top-right are the moored fleet's anchor
lights from PR 2a (#716) — the two features compose.

![NMC approach](wide-02h.png)

*(This one frame is shot at a wider zoom than the game's, so the whole channel fits; the strips above
are at the shipped pixel-perfect framing.)*

## 4 · Why the reach is 1.1 m and not 1.6 m

The first pass reasoned the halo should be a little bigger than the buoy's girth and set 1.6 m.
Photographed, it swallows the can:

![reach A/B](reach-ab.png)

**A mark's shape is part of what she signals** — her own gloss says *"the SHAPE is the mark"* — so a
cardinal whose topmark you cannot see at night has lost half her meaning. At 1.1 m the lantern sits
*on* the mark and both read. The upper bound is separate and measured: the closest two marks anywhere
in the two harbours are 8.29 m apart, so a pair clears by 6.09 m and no two lanterns can ever overlap
into a colour that means nothing.

---

## How to re-shoot these

1. Open the region scene and run **`Hidden Harbours ▸ Art ▸ Phase Nav Lights in Open Scene`** — the
   marks standing in the committed scenes were placed before their lights existed and carry no
   assigned phase; this hands them the planned ones without rebuilding the region. (It reported
   *"phased 12 of 12 marks (12 lit); added 0 missing lamp component(s)"* for these plates, which is
   also how the prefab wiring was confirmed to reach every placed instance.)
2. Enter play mode.
3. `GameServices.Clock.TimeScale = 0`, then `SeekTo((day + hour/24) * GameConfig.SecondsPerDay)`;
   02:00 is `t = 150` at the shipped `SecondsPerDay = 1800`.
4. For a strip, seek in steps and capture each frame — the light is a pure function of the clock, so
   the strip is exact rather than approximate. Read `NavLight.IsBurning` for the label rather than
   trusting the arithmetic.
