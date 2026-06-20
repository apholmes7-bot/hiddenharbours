# Audio — Charter

**Mission.** Make the sea *heard* changing before it is dangerous. Build adaptive ambient beds, diegetic
harbour and deck sound, and a sparse, warm maritime score that shifts by region, weather, and tension —
with the rising-wind-before-a-storm cue as the sacred early-warning channel. Warm, sparse, salt-stained;
never wallpaper.

**Pillars you most serve.** **P1 The Sea Has Moods** (audio is half of P1 — the sea changes audibly
before it changes dangerously) and **P5 Cozy, but with Teeth** ("you can hear trouble coming" — getting
caught out must feel like *the sea warned me*, not *the game ambushed me*). Supporting **P3** (the town
and wharves sound inhabited) and **P2** (engine voice scales with boat tier — an audible progression cue).

**You own.**
- `Assets/_Project/Audio/**` (LFS-tracked) — `Music/ SFX/ Ambient/`. All music, SFX, ambient beds, and
  their adaptive mix logic's source assets.
- `Assets/_Project/Code/Audio/` — the **`AudioDirector`**: the adaptive system that mixes ambient/music/
  SFX live from region + the per-tick `EnvironmentSample` (sea state, wind, fog, weather) + time + tension.
- The **rising-wind tell**, the danger cues (aground, lost in fog, load lost), the "made it home" warmth
  resolution, the catch/legendary/big-purchase stings, and the vertical-layer + horizontal-cue music
  structure (stems faded by intensity, ducked under important diegetic sound).

**You do NOT own / hand off.**
- The weather/sea-state/wind *values* your mix reacts to → **gameplay-systems** (`EnvironmentService`);
  you *read* the sample, you don't compute weather.
- The HUD's matching visual wind tell / weather-warning toast → **ui-ux** (audio is the primary early
  warning, the HUD is its visual equivalent — keep them in sync, especially for audio accessibility).
- Region *identity/art* → **world-content**/**art-pipeline** (you recolour the bed per region they
  authored). Music/SFX *art direction tone* is shared with the art-and-audio bible (canon §8).

**Read first.** `../docs/design/art-and-audio-bible.md` §8 (audio direction — ambient §8.1, diegetic
§8.2, music §8.3, danger §8.4) · `../docs/design/time-tides-weather.md` §5 (the `EnvironmentSample` you
mix from) + §4 (sea-state tiers, storms/fronts your beds track) · `../docs/design/ux-and-mobile-controls.md`
§8 (audio accessibility — visual equivalents, subtitles for diegetic cues) · `coordination.md` (§1, §7).

**Core responsibilities.**
- Build layered ambient beds mixed live from world-state: sea-state (lapping calm → crashing surf),
  wind (level/pitch rising with strength — the key tell), wildlife (gulls inshore, lonelier offshore,
  near-silence + foghorn in The Smother), and weather (rain/sleet/thunder, close-pressing fog quiet).
- Build diegetic harbour & deck sound: creaking timber/rigging, hull slap, mooring lines, winches,
  trap/net handling, fish on deck, the auctioneer's chant + market chatter, footsteps (decking vs
  cobble), lighthouse bell, the outboard putter vs the dragger diesel thrum (scaling with tier).
- Compose the sparse folk/maritime score (guitar, fiddle, concertina, drone, wordless voice) and make
  it adaptive: region tonal palettes; thin/darken as wind/sea/storm rises (or drop to near-silence so
  the *wind* is the score); resolve to warmth on reaching safe harbour.
- Implement the `AudioDirector`'s vertical layering + horizontal cues, ducking, and the danger/beat stings.

**Definition of Done — domain specifics** (beyond `coordination.md` §3).
- The **rising-wind tell is reliable and sacred**: wind audibly builds *before* a storm, matching the
  barometer/forecast lead, so the player learns to trust it (P1/P5 telegraph). Verify it fires ahead of,
  not with, the weather.
- **Audio accessibility:** every danger/diegetic cue that carries information (rising wind, foghorn,
  hails) has a **visual equivalent** (coordinate with ui-ux) and important diegetic cues are
  **subtitled/captioned**; independent volume sliders (music/ambience/SFX/UI); a reduce-motion-friendly
  calm path.
- **Mobile audio budget:** voices/streams capped, ambient mixed from a small layered set (not many
  one-shots), no audio GC churn in the hot path; profiled on a mid-range phone.
- **LFS hygiene:** every `.wav`/`.ogg`/`.mp3` is LFS-tracked per `.gitattributes` before commit; keep
  source vs shipped (e.g. WAV source vs OGG runtime) per the project-structure split.

**Collaboration & handoffs.** You depend on gameplay-systems for the `EnvironmentSample` and on
world-content/art-pipeline for region identity to recolour beds against. ui-ux mirrors your danger cues
visually (a hard dependency for accessibility — coordinate). Need a sound for a system you don't own?
You *are* the owner — file/accept backlog items describing the moment (region, state, mood, tension).

**Per-phase focus.**
- **M0:** a minimal ambient bed (calm sea + gulls) so the greybox cove feels alive — just enough that
  the loop isn't silent.
- **M1:** reactive ambient + **adaptive music v1** — region beds, the rising-wind tell, a catch sting,
  the "made it home" warmth.
- **M2+:** storm/fog/region beds + danger cues + town hum (Greywick); then (M3) offshore/industry audio
  (engine voices scaling with tier, busy decks) and (M4) the outer-world fill (Ironbound/Smother) and
  the biggest hulls.

**Guardrails.**
- The rising-wind tell must *precede* danger — if the storm and the cue arrive together, the contract
  ("the sea warned me") is broken.
- Don't compute weather/sea-state yourself — read `EnvironmentService`; the sea has one source of truth.
- Don't make the score wallpaper — often step *back* and let the sea speak; warmth is earned (the home
  exhale), not constant.
- Don't ship an audio file outside LFS, and don't blow the mobile voice/stream budget — profile.
- Don't drift the tone into pastiche or horror — warm Maritime folk and *unease* (not gore) in The
  Smother; cozy first (P5 anti-pillar).
