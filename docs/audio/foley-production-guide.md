# Hidden Harbours — Foley Production Guide

> **Status:** Working guide. Written 2026-08-18 for an owner with **Ableton Live 9** and no
> audio-production background required.
> **Canon:** [`../vision-and-pillars.md`](../vision-and-pillars.md) wins, then
> [`../design/art-and-audio-bible.md`](../design/art-and-audio-bible.md) §8 (audio direction).
> **Companion:** [`../../Assets/_Project/Audio/AUDIO-MANIFEST.md`](../../Assets/_Project/Audio/AUDIO-MANIFEST.md)
> — the authoritative list of clip slots. This guide tells you how to *fill* them.
> **Why this exists:** [`../marketing/first-trailer-assessment.md`](../marketing/first-trailer-assessment.md)
> §6 B1 names audio as the hard blocker. This is the cheapest way to clear most of it.

---

## 1. The one rule

**Record dry, flat and quiet. The game does the rest.**

Every instinct from music production — make it loud, compress it, add reverb, widen the stereo —
is *wrong here*, and every one of them is destructive because it cannot be undone at runtime. The
manifest already says it: *"Keep beds quiet — they sit under gameplay. Loudness is mixed at
runtime."*

The game is not a passive player of your files. It pitches them, rides their gain, ducks them,
crossfades them and pans them, live, from simulation values. Your job is to hand it **neutral raw
material with headroom in every direction.**

---

## 2. What the runtime does to your audio

This is the part a generic foley guide won't tell you, and it's the part that decides whether a
recording works or has to be done again. Read this before you record anything.

| The runtime does this | So the source must be |
|---|---|
| **Pitches `_payoutTick` down** as the line sinks | Recorded at a **mid** pitch with room to fall — not already low. Even tempo, no ritardando baked in. |
| **Tightens `_strainGroan`'s pitch** and rides its gain on `Tension01^1.6` | **Even and eventless.** Any drama you record fights the curve. Neutral pitch, steady level. |
| **Rides `_outboardEngine`'s pitch and volume** with speed over ground | Recorded at **steady mid RPM**. No drift, no rev. Short loop, perfectly seamless — engine loops expose seams worse than anything else. |
| **Rides `_windTell`'s loudness** from wind strength — the canon-sacred cue | **Completely even.** No gusts, no events. A recorded gust arriving under a runtime fade sounds broken. See §9. |
| **Pans `_surfaceThrash`** on the fish's screen offset | **Mono, dry, no stereo width, no reverb.** Baked stereo makes panning incoherent. |
| **Deepens `_rodCreak`** as the rod loads | Neutral flex, mid-range, no extremes at either end. |
| **Crossfades `_hullRow` ↔ `_outboardEngine`** on a hull swap | Both at **matched perceived loudness**, or the swap lurches. |
| **Ducks beds under cues**, and thins the calm bed as wind rises | Beds carry **no transients** — nothing that pokes through a duck. |

**The single most common mistake this list prevents:** recording something expressive and
dynamic because it sounds better in isolation. In this engine, expressive source material fights
the simulation. Flat source + live modulation is what makes the sea feel responsive.

---

## 3. The kit

You need much less than you think.

- **A recorder.** Your phone's voice memo app is genuinely fine for most of this list. If you have
  any USB mic or an interface, better — but don't let not having one stop you.
- **A quiet room.** The enemy is room noise and traffic rumble, not mic quality. Record late,
  fridge off, windows shut. A duvet over the mic-and-object area kills reflections for free.
- **Get close.** Small sounds recorded close and quiet beat big sounds recorded far away. Most of
  this list wants the mic 15–30 cm from the object.
- **Record long.** Take thirty seconds of every sound, not three. Loops need material to hunt
  through, and re-setting up costs more than tape does.
- **Record the room alone for 30 seconds.** You'll want that noise print later.

**The one thing worth leaving the house for:** if you're anywhere near the actual coast — and the
game is set on a variant of Hillsborough Bay — **go and record the real shoreline and real gulls.**
Those two are the hardest to fake and the most valuable to have honest. A phone in a pocket-lined
windshield on a calm morning gets you a usable calm bed and a gull bank in one trip.

---

## 4. Priority order

Don't record all twenty. Work down this list and stop when you run out of appetite — each tier is
useful on its own.

**Tier 1 — the trailer and the canon-sacred cues.** These are what the trailer lives on and what
canon leans hardest on. `_calmBed`, `_gulls`, `_hullRow`, `_windTell` (but read §9 first).

**Tier 2 — the highest-contact loop.** The rod-fishing layer is what a player hears hundreds of
times an hour: `_castWhoosh`, `_splashDown`, `_reelClickLoop`, `_strainGroanLoop`, `_landedFlourish`'s
wet slap.

**Tier 3 — everything else in the manifest.**

**Tier 4 — do not foley these at all.** `_catchSting`, `_homeWarmth`, and the musical half of
`_landedFlourish` are *musical* cues, not sounds. They belong to whoever writes the theme. See §9.

---

## 5. The shot list

### Ambience — the beds (all loop, all quiet, all eventless)

| Slot | Record it from | Technique | Watch for |
|---|---|---|---|
| `_calmBed` | Real shoreline on a calm day, or a shallow tray of dried rice/split peas tilted slowly | Real is far better. If faking: tilt slowly and *continuously*, mic close, no rhythm | Must be **even** — no wave events. A recognisable wave becomes a tell that repeats. Avoid bathtubs; small water reads as "sink" |
| `_gulls` | Real gulls | No good fake exists. If you can't get them, leave the procedural placeholder in | **Sparse over silence.** Gate the gaps to true silence so the loop point is inaudible |
| `_hullRow` | Wooden oar in a rowlock + hand pulled through a bucket of water, layered | Two passes, two tracks. Wood-on-wood creak with a little rope; water pull underneath | Rhythmic — the loop must be a **whole number of strokes**, or it limps |
| `_outboardEngine` | A real small outboard if you can get near one. Otherwise a small electric motor — cordless drill, desk fan, electric toothbrush — pitched down hard | Steady throttle, mic 30 cm off, thirty seconds | **Steady mid RPM, no drift.** Pitch down in Live until it has weight. Short, perfect loop — see §7 |
| `_windTell` | **Recommend synthesis, not foley** — see §9 | — | Real wind on a mic is rumble, not wind |

### The rod-fishing layer

| Slot | Record it from | Technique | Watch for |
|---|---|---|---|
| `_rodCreakLoop` | A bamboo cane or wooden dowel flexed slowly; or a leather belt twisted; or a wicker basket | Slow, continuous, close mic | Neutral flex — the runtime deepens it. Don't record the extremes |
| `_castWhoosh` | A thin cane or dowel swished past the mic | Several passes at different speeds; pick the one with air, not wind-blast | Keep the mic *beside* the arc, not in it, or you get a thump |
| `_splashDown` | A stone dropped into a full bucket | A dozen takes; pick for a clean plop with no bucket ring | Fill the bucket high — a low bucket rings like a drum |
| `_payoutTickLoop` | A bike freewheel spun slowly, a card in spokes, a comb stroked, or a real fishing reel | **Even tempo**, mid speed, thirty seconds | Runtime **slows the pitch** — record at mid so it has room to fall |
| `_bottomSettle` | A soft weight (sock of rice) dropped onto damp sand or a folded towel | Dull, no click. Should read as "felt, not heard" | Any transient click breaks the "slack" feeling |
| `_bobberPlop` | Finger hooked in cheek and popped; or a cork into water | The cheek-pop is the classic and it's better than the real thing | Keep it small and dry |
| `_rodKnock` | A knuckle on hollow wood — a door, a drawer, a cane | Two or three knocks, pick one | This is the *deep-path* bite tell — it should feel like it came up the rod, so favour low and woody |
| `_strainGroanLoop` | Rope under tension twisted slowly; leather creak; a balloon rubbed with a damp finger | Continuous, even, thirty seconds | **Eventless.** Gain rides tension at runtime — recorded swells fight it |
| `_reelClickLoop` | A real fishing reel is ideal. Otherwise a ratchet, a zip tie through a fan guard | Steady, even, mid speed | Loops only *while gaining* — so it must start and stop cleanly |
| `_slackRelease` | Rope released under tension; a soft whip-back of cane | Should feel like relief, not a snap | This is the diegetic "PULL now" — make it noticeable but not alarming |
| `_surfaceThrashLoop` | A hand slapping and churning water in a full tub | Continuous churn, not discrete splashes | **Mono, dry, no reverb** — the runtime pans it. Baked width breaks that |
| `_snapSting` | See §9 — half musical | A soft wooden knock as the foley half | **Cozy, never punishing.** Canon is explicit. No harsh transient, no descending "fail" gesture |
| `_landedFlourish` | **A wet cloth slapped on a wooden board** — the classic fish-on-deck foley, and it's better than a real fish | Soak a flannel, wring it half out, slap a breadboard. Vary the wetness | The *flourish* half is musical — record only the slap and let the composer layer over it |

---

## 6. Treating it in Live 9

A deliberately short chain. Every device you add is a decision you can't reverse later.

**On every clip, in this order:**

1. **EQ Eight — high-pass at 60–80 Hz.** Non-negotiable for phone recordings. Removes traffic
   rumble, handling noise and air-conditioning you can't hear on laptop speakers but which will
   pile up when twenty clips play at once.
2. **Gate** (on sparse clips only — gulls, one-shots). Set the threshold so the room floor goes to
   true silence between events. This is what makes a sparse loop's seam disappear.
3. **EQ Eight again if needed** — a gentle dip around 200–400 Hz if it sounds boxy (small-room
   recordings almost always do).
4. **Utility → set to Mono.** Do this before you judge anything, because the game plays these mono.

**What not to use:**

- **No compression or limiting.** Loudness is mixed at runtime; compressing here removes the
  headroom the engine needs and makes beds poke through their own ducking.
- **No reverb.** The game places sounds in space. Baked reverb can't be removed and will fight the
  region ambience.
- **No stereo widening.** Breaks `_surfaceThrash`'s panning and does nothing for mono playback.
- **No normalising to 0 dB.** Aim for peaks around **−6 to −12 dBFS** and leave them there.

**Pitching things down** (the engine, the rod knock): use a **Simpler** with Warp off and transpose
in semitones, or the clip's Transpose control. Going down more than about an octave starts to smear
— if you need more than that, the source is wrong.

---

## 7. Making a loop actually seamless

The single most common failure. A loop that clicks or breathes is worse than no loop, because the
player's ear locks onto the period and can't unhear it.

**The technique, in Live, for any bed:**

1. Find a stretch with no distinguishing events — no gull, no distinct wave, nothing you'd
   recognise on its return.
2. Set the loop brace to a length that is **not** a round number of seconds. 7.3 s beats 8.0 s;
   round numbers make the period easier for the ear to latch onto.
3. **Cut at zero crossings** — zoom to sample level at both ends and put the edit where the
   waveform crosses the centre line. This alone kills most clicks.
4. **Crossfade the tail into the head.** Duplicate the clip onto a second track, slide it back by
   the loop length, fade the outgoing tail down and the incoming head up across a half-second, and
   **Consolidate** (Cmd/Ctrl-J). This is the step that removes the "breath" at the seam.
5. **Audition it on repeat for two full minutes.** Not twenty seconds. Seams reveal themselves on
   the third and fourth pass, which is exactly when a player would notice.

If your export dialog offers a **Render as Loop** option, use it — it wraps the tail correctly.
Check whether your version has it rather than assuming.

**For rhythmic loops** (`_hullRow`, `_reelClicks`, `_payoutTick`): the loop length must be a whole
number of events, and the gap before the first event must equal the gap after the last. Count the
strokes and trust the count over your ear.

---

## 8. Export, and getting it into the game

**Export settings:**

| Setting | Value | Why |
|---|---|---|
| File type | **WAV** | The manifest's standard; Unity compresses on import |
| Sample rate | **44,100 Hz** | Manifest standard |
| Bit depth | **16-bit** | Plenty for game SFX |
| Dither | **Off** at 16-bit for source material | Avoid stacking dither noise across twenty clips |
| Normalize | **Off** | Loudness is a runtime decision |

**Getting to true mono:** put a **Utility set to Mono on the master** before export. If the
resulting file is still dual-mono stereo, that's fine — tick **Force To Mono** in Unity's import
settings and it resolves cleanly. Belt and braces; do both.

**Filing:** the manifest already names every path. Drop files at exactly those names —
`Assets/_Project/Audio/Ambient/calm_sea_bed.wav`, `Assets/_Project/Audio/SFX/cast_whoosh.wav`, and
so on.

**The swap itself needs no code.** Select the `[AudioDirector]` (or `[FishingAudio]`) object and
drag each `.wav` onto its matching serialized field. The procedural placeholder is only used when
the field is empty, so an empty slot keeps working and a filled slot takes over. **This means you
can ship one clip at a time** — there's no all-or-nothing moment.

> **Repo hygiene:** already handled — `.gitattributes` tracks `*.wav`, `*.ogg` and `*.mp3` through
> Git LFS (lines 105–108), so committing audio is safe as-is. Nothing to set up.

**Test in the actual game, not in Live.** A bed that sounds thin and quiet in Live is usually
correct; a bed that sounds great in Live is usually too loud and too processed for the mix.

---

## 9. What not to record, and why

**The wind tell should stay procedural.** This is the counter-intuitive one, and it's worth
holding to. Canon calls the rising wind the primary early-warning channel — the player must hear
trouble coming, continuously, proportionally to actual wind strength. A recorded loop can only be
faded up and down; **synthesis actually tracks the value**, and filtered noise with moving resonant
peaks is how shipped games do wind anyway. Recording real wind mostly captures mic rumble, not
wind. Leave this one to code and spend the effort elsewhere. (The same logic protects the engine's
speed-reactive behaviour, though there a real recording pitched at runtime works well.)

**`_catchSting` and `_homeWarmth` are music, not sound.** A sting and an exhale are melodic,
harmonic gestures that need to sit in the same tonal world as the theme. Recording them as foley
produces something that clashes with whatever score arrives later. **Hold these for the composer**
and let the procedural placeholders stand until then — they're the two least-heard cues on the
list.

**`_snapSting` is the trap.** Canon is explicit that losing a fish must be *cozy, never a
punishment sound* — no harsh transient, no descending fail gesture. Every instinct will pull you
toward a sad trombone. Record only a soft wooden knock and let the composer decide the rest.

---

## 10. If you get through Tier 1 and 2

You'll have converted roughly half the manifest from placeholder to real, for the cost of a quiet
evening and a bucket of water — and crucially you'll have the **ambience the trailer needs**, which
is the part that can't be commissioned quickly.

What remains after that is a genuinely smaller, cheaper commission: **the theme, its trailer cut,
and the handful of musical cues.** That is a much better conversation to have with a composer than
"we need all the audio."
