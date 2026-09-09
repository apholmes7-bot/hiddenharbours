# Audio licence ledger

**Every file listed here is redistributed.** This repository is public, so each byte of audio we commit
is handed to everyone who clones it. That is only lawful if the source licence permits redistributing
the raw file, and only honest if we can say where it came from. This ledger is that record: **one row
per committed clip**. A clip without a row does not merge, and an EditMode test enforces it.

**Licence policy.** CC0 first; CC-BY second with the attribution recorded here. **No NC** (this is a
commercial game) and **no SA** (a share-alike sample would reach into the whole project). No
"royalty-free" library whose terms forbid redistributing the raw files — which rules out the Sonniss GDC
bundles, BBC RemArc, and the Pixabay-style content licence, however good those recordings are.
Everything below is **CC0 1.0**, so no attribution is legally required. It is recorded anyway, because
taking someone else's work without naming them is a choice we would rather not make.

Every source is from [OpenGameArt.org](https://opengameart.org). Nothing executable was downloaded at
any point — audio files only, opened by a decoder, never run.

## What "changed" means

Nothing here is a raw download. Every clip was processed to meet the source contract in
`docs/audio/foley-production-guide.md` §2 — dry, mono, even, with headroom. Two recipes, referenced by
key in the table:

- **L (loop)** — fold to mono · zero-phase FFT high-pass at 70 Hz (removes rumble and DC without
  smearing the transients a minimum-phase filter would) · band-limited FFT resample to 44,100 Hz · cut
  the stated window · **equal-power 60 ms crossfade loop**, the loop point searched over the last 30 ms
  for the smoothest sample pair · peak normalise to **−12 dBFS**.
- **S (one-shot)** — fold to mono · 70 Hz high-pass · resample to 44,100 Hz · trim silence below
  −46 dBFS with an 8 ms pad · peak normalise to **−3 dBFS**.

**No compression, no limiting, no reverb, no stereo widening and no pitch shift** was applied to any
file. The dynamics you hear are the dynamics that were recorded. The mix rides gain and pitch at
runtime, and baking either into a clip would take that control away from it.

## The ledger

| # | File | Slot | Source (OpenGameArt item) | Original file | Author | Licence | Fetched | Changed |
|---|---|---|---|---|---|---|---|---|
| 1 | `Ambient/calm_sea_bed.ogg` | `_calmBed` | [Sea and river wave sounds](https://opengameart.org/content/sea-and-river-wave-sounds) | `Vistula.mp3` | RandomMind | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | 2026-09-09 | **L**, window t=3900.0 s, 20.06 s — chosen for being the most eventless stretch in a 78-minute recording |
| 2 | `Ambient/gulls.ogg` | `_gulls` | [Solo Seagull Sound Effects](https://opengameart.org/content/solo-seagull-sound-effects) | `gull_amb_1.wav` … `gull_amb_7.wav` | Rango Mango | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | 2026-09-09 | **S** per call, then **arranged**: the seven calls placed at fixed times over 24 s of true digital silence, so the layer adds birds without adding a second sea under the bed |
| 3 | `Ambient/outboard_engine.wav` | `_outboardEngine` | [Steam boiler sound loop](https://opengameart.org/content/steam-boiler-sound-loop) | `generator_loop.wav` | bart | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | 2026-09-09 | **L**, window t=1.50 s, 8.06 s — one steady rev with no acceleration in it, because the director pitches it by speed over ground |
| 4 | `Ambient/wind_tell.wav` | `_windTell` | [wind whoosh loop](https://opengameart.org/content/wind-whoosh-loop) | `wind woosh loop.ogg` | SketchMan3 | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | 2026-09-09 | **L**, window 0–5.90 s — chosen for being **gustless**, so the loudness ramp is the only thing the player hears change |
| 5 | `SFX/rod_creak.ogg` | `_rodCreakLoop` | [Tree Creaking](https://opengameart.org/content/tree-creaking) | `tree_creak.flac` | AntumDeluge (Jordan Irwin) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | 2026-09-09 | **L**, window t=0.50 s, 3.06 s |
| 6 | `SFX/payout_tick.ogg` | `_payoutTickLoop` | [Fisheefects](https://opengameart.org/content/fisheefects) | `fish_reel.wav` | You're Perfect Studio | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) — the item is multi-licensed CC-BY 4.0 / OGA-BY 3.0 / CC0; **taken under CC0** | 2026-09-09 | **L**, window t=1.00 s, 2.06 s — a steady tick rate with no ritardando, because the runtime slows the pitch as the rig sinks |
| 7 | `SFX/strain_groan.wav` | `_strainGroanLoop` | [30 CC0 SFX loops](https://opengameart.org/content/30-cc0-sfx-loops) | `sfxloops/ambient_01.ogg` | rubberduck | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | 2026-09-09 | **L**, window t=0.75 s, 4.06 s |
| 8 | `SFX/reel_clicks.ogg` | `_reelClickLoop` | [Fisheefects](https://opengameart.org/content/fisheefects) | `fish_reel.wav` | You're Perfect Studio | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) — multi-licensed as row 6, **taken under CC0** | 2026-09-09 | **L**, window t=5.50 s, 2.06 s |
| 9 | `SFX/surface_thrash.ogg` | `_surfaceThrashLoop` | [40 CC0 water / splash / slime SFX](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) | `watersplash/loop_water_02.ogg` | rubberduck | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | 2026-09-09 | **L**, window 0–6.96 s |
| 10 | `SFX/cast_whoosh.wav` | `_castWhoosh` | [Swishes Sound Pack](https://opengameart.org/content/swishes-sound-pack) | `swishes/swish-5.wav` | artisticdude | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | 2026-09-09 | **S** |
| 11 | `SFX/splash_down.wav` | `_splashDown` | [40 CC0 water / splash / slime SFX](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) | `watersplash/splash_04.ogg` | rubberduck | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | 2026-09-09 | **S** |
| 12 | `SFX/bobber_plop.wav` | `_bobberPlop` | [40 CC0 water / splash / slime SFX](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) | `watersplash/bubble_02.ogg` | rubberduck | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | 2026-09-09 | **S** |
| 13 | `SFX/rod_knock.wav` | `_rodKnock` | [Thwack Sounds](https://opengameart.org/content/thwack-sounds) | `PCM/thwack-03.wav` | AntumDeluge (Jordan Irwin) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) — also stated in the pack's own `LICENSE.txt` | 2026-09-09 | **S** |
| 14 | `SFX/bottom_settle.wav` | `_bottomSettle` | [100 CC0 SFX #2](https://opengameart.org/content/100-cc0-sfx-2) | `sfx100/sfx100v2_footstep_wet_02.ogg` | rubberduck | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | 2026-09-09 | **S** |
| 15 | `SFX/slack_release.wav` | `_slackRelease` | [Swishes Sound Pack](https://opengameart.org/content/swishes-sound-pack) | `swishes/swish-11.wav` | artisticdude | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | 2026-09-09 | **S** |
| 16 | `SFX/snap_sting.wav` | `_snapSting` | [Thwack Sounds](https://opengameart.org/content/thwack-sounds) | `PCM/thwack-05.wav` | AntumDeluge (Jordan Irwin) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) — pack `LICENSE.txt` | 2026-09-09 | **S** |
| 17 | `SFX/landed_flourish.wav` | `_landedFlourish` | [40 CC0 water / splash / slime SFX](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) | `watersplash/slime_09.ogg` | rubberduck | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | 2026-09-09 | **S** |

**Committed audio: 2,301,257 bytes = 2.195 MB across 17 files** (per-PR ceiling 6 MB). All under Git LFS.

## Why some clips are `.ogg` and some are `.wav`

A loop is seamless only if the sample step across the wrap is no bigger than the steps inside it, which
is checked numerically by `AudioClipSetContentTests.LoopSlots_WrapWithoutAStep`. Vorbis coding noise is
uncorrelated between a clip's first and last sample, so on a **quiet, smooth** loop — one whose median
step between neighbouring samples is below the codec's own noise floor — no choice of loop point can
meet that bar in a lossy format. **Three** clips are in that position (`outboard_engine`, `wind_tell`,
`strain_groan`) and ship as exact 16-bit PCM, where the join is sample-accurate. The rest ship as Vorbis
(~q6) and clear the bar with better than 2× margin. All-WAV would have been about 6.9 MB, over the
ceiling.

For the same reason the importers are set to **PCM** (`compressionFormat: 0`) with `normalize: 0`: the
clip Unity hands the game is byte-for-byte the file in this folder, so the seam we measured is the seam
that plays, and the −12 / −3 dBFS peaks survive import. Repo cost stays 2.195 MB; decoded, that is about
6.9 MB of RAM. Moving the beds to Vorbis or streaming later is an **importer setting**, not a re-export.

## Slots deliberately left empty

An empty slot is honest; a wrong licence is not. These three keep the procedural placeholder from
`ProceduralAudio.cs` that has been covering them all along, and the game is complete without them.

| Slot | Why it is empty | What would fill it |
|---|---|---|
| `_hullRow` | No CC0 recording of **oars working in water** was found on OpenGameArt or Wikimedia Commons. Every rowing recording located was NC, SA, or from a pack whose terms forbid redistributing the raw files. | **Owner shopping list.** Freesound requires an account, and this lane does not create accounts. Search Freesound for `rowing oar boat`, filter to **CC0**, and confirm the licence on the item page rather than the search chip; drop what you like into `_inbox/` and this lane will process and slot it. |
| `_catchSting` | Musical, not foley. `docs/audio/foley-production-guide.md` §9 holds both stings until there is a score for them to sit inside, so they land in the same tonal world instead of clashing with it. | Kenney's [Music Jingles](https://kenney.nl/assets/music-jingles) (CC0) is a ready placeholder if the owner wants one now — an inspector edit and a row here, no code. |
| `_homeWarmth` | As above. The earned made-it-home exhale is the warmest moment in the game and deserves the score's key. | The same Kenney set. |

## Adding a clip later

1. Drop the file in `Ambient/` or `SFX/` — `.gitattributes` already routes it through Git LFS.
2. Assign it to its slot on `Assets/_Project/Audio/Resources/AudioClipSet.asset` in the inspector.
3. **Add its row here.** `AudioClipSetContentTests.EveryFilledSlot_HasALicenceRow` goes red without one.

No C# at any step — the clip set is data (rule 2).
