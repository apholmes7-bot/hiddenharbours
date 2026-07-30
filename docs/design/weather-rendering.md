# Hidden Harbours — Weather rendering (fog · sky · cloud · rain VFX)

> **Status:** Design module — a **captured look-target + build-later recipe**. **Docs only: no code,
> scene, shader, material, or Core change ships with this document.** It records an owner-shared visual
> reference and maps its technique onto systems Hidden Harbours already has, so a future art pass
> (**M2/M3**) can build it without re-deriving. Subordinate to
> [`../vision-and-pillars.md`](../vision-and-pillars.md) (CANON) — when in doubt, that file wins; then
> the [`art-and-audio-bible.md`](art-and-audio-bible.md), whose **§6 (Lighting, day-night & fog)** is the
> canon home of this look and whose **§6.1** advanced-rendering roadmap this elaborates. One of three
> captured [advanced-rendering targets](advanced-rendering-targets.md) (with the seabed/terrain and
> sprite-light-response captures) — read that index for the shared rules.
>
> **Pillars served.** **P1 The Sea Has Moods** — fog, rain, cloud and sky *are* the sea's moods you read
> and respect, not set-dressing. **P5 Cozy, but with Teeth** — fog is a real navigation hazard (The
> Smother is built on it). The atmosphere half of **P3 A Living Working Coast** (a town glowing warm in
> the murk).
>
> **Sibling docs.** [`time-tides-weather.md`](time-tides-weather.md) §4.1/§4.4 (the deterministic weather
> *state* — wind, sea-state, **`fogDensity`**, **`visibility`**, `cloudCover`, barometer — and §4.8 the
> phased **M2** weather wave) · [`water-rendering.md`](water-rendering.md) (the sibling "hero look,
> deferred build" recipe this mirrors) · [`lighting-and-daynight.md`](lighting-and-daynight.md) (the
> `_SunDir`/`_SunElevation` + `_DayNightTint` globals this reads) · [`ambient-particles.md`](ambient-particles.md)
> (the **rain + sea-mist** emitters already shipped). ADRs: [`../adr/0013-dynamic-lighting.md`](../adr/0013-dynamic-lighting.md)
> (the day/night model + sun globals), [`../adr/0016-additive-2d-lights.md`](../adr/0016-additive-2d-lights.md)
> (custom-shader lights + light-into-water-shader), [`../adr/0017-weather-driven-water-palette.md`](../adr/0017-weather-driven-water-palette.md)
> (weather → sea mood, the {sea-state, visibility} axes this reuses).

---

## 1. Where this came from (the reference, credited)

**Owner-shared reference:** a Unity developer's weather/fog demo (r/Unity3D — the *"HDRP custom terrain
shader"* thread; the author states they later **ported the look to URP**). **Not our art and not to be
copied pixel-for-pixel** — captured here as a *technique target*. The author described how it works; the
value for us is that **their two stated hard constraints are things Hidden Harbours already has by
design**, so the technique ports unusually cleanly. Paraphrased from their comments:

- **Fog** is, at core, a **screen-space colour fill keyed by depth** (their Z-buffer). Its colour =
  **sky colour × fog colour**.
- The **sky colour** is a bright disc of one colour over a fill of another; the **disc's position +
  tint follow the directional light's tilt** (= time of day): sunrise → top-right, bluish; noon →
  top-centre, white; sunset → top-left, orange. Weather params (cloudiness, fog, temperature) shift the
  tone.
- The sky is **blended with a cloud layer** (stacked noise textures) so half the screen can read "under
  cloud" and half "clear", with the fog matching.
- **Point-light colour bleeds into the fog** — the lights are rendered into a texture the fog samples,
  giving scattered coloured glow inside the murk.
- **Rain is the same fog** — a stretched small-noise map = raindrops, a large-noise × small-noise map =
  uneven density patches, both scrolling down at different speeds, folded into the fog's alpha.
- **"Volumetric" depth** is faked by stacking several flat lighting textures in screen space, each
  nudged slightly upward.
- **Their caveats (verbatim intent):** *"only works for a very specific case — a fixed camera that
  can't be rotated, and custom shader-based point lights, not Unity's built-in lights."*

---

## 2. Why this is the best-aligned of the three references

The author's whole solution is gated on **a fixed camera** and **custom-shader point lights**. Those are
not compromises for us — they are **decisions Hidden Harbours already made**:

| The reference's requirement | Hidden Harbours today | Verdict |
|---|---|---|
| **A fixed, non-rotatable camera** (their #1 constraint) | ¾ top-down **orthographic**, one perspective everywhere, Pixel-Perfect Camera, no rotation (art bible §2 **LOCKED**) | **We satisfy it by design.** |
| **Custom shader-based point lights, NOT Unity built-in** (their #2 constraint) | Exactly our choice: additive-glow `SceneLight`/`BoatSpotlight` drawn above the frame, **not** URP `Light2D`; the water shader already reads the light as **published globals** (`_BoatLight*`) — ADR 0016 | **We satisfy it by design.** |

So the piece of the technique other projects fight — making a screen-space, light-aware fog that only
works under a fixed camera with shader lights — is the piece we get *for free*. The rest of the
reference maps onto systems that are **already shipped or already roadmapped**.

---

## 3. The mapping — reference technique → our stack (what we HAVE vs what's NEW)

| # | Reference technique | Hidden Harbours today | Gap / new work |
|---|---|---|---|
| 1 | Fog = screen-space fill keyed by **depth** (Z-buffer) | 2D world; "depth" = **parallax / sorting layers** + the authored **seabed height map**. Art bible §6 already specs *"a fog overlay — animated, parallax, density-graded with distance"* | **New:** a screen-space fog layer graded by **parallax depth** (we have no free scene Z-buffer — see OQ2). A translation, not a blocker. |
| 2 | Sky colour = **disc positioned by the sun's tilt**, tinted by time of day | We publish **`_SunDir` / `_SunElevation`** from the deterministic clock (ADR 0013), and `DayNightProfile.SkyTint` already paints dawn→noon→dusk→night mood | **Mostly have:** express the existing SkyTint **spatially** as a sun-positioned bright disc in the fog/sky layer — reads globals we already set. |
| 3 | Cloud layer (noise) blended into sky; "half under cloud" | **`cloudCover`** is already in the weather state; **cloud shadows** are a roadmapped **M3** item (art bible §6.1) | **New:** one scrolling cloud-noise layer feeding *both* the sky tint *and* the M3 cloud-shadows; density = `cloudCover`. |
| 4 | **Point-light colour bleeds into fog** (lights → texture → fog samples it) | One light (the boat spotlight) **already** bleeds into the **water shader** via globals (ADR 0016 follow-up 2); ADR 0016 notes the **array / light-texture** extension for many lights | **Partly have:** single light via globals. Many lights (a lit town in fog) = a **light-accumulation texture** the fog samples — the noted extension. |
| 5 | Rain = scrolling noise folded into the fog | **`RainEmitter` already ships** — pooled sprite streaks that **slant downwind** (real `WindVector`), intensity = `RainIntensity(visibility, seaState)`, day/night tinted, moonlight-caught (`ambient-particles.md`) | **Have (sprite approach).** Optional: fold rain **into** a screen-space fog layer (the reference's way) *if* we go that route — a look/perf choice, not a requirement. |
| 6 | "Volumetric" via stacked, upward-offset flat textures | Our **parallax depth layers** (art bible §2.1) + the shipped **`SeaMistEmitter`** (low drifting haze under the boats) already stack drifting depth | **Mostly have:** parallax + mist give the layered read; the fog layer adds the graded *fill* over them. |
| 7 | Sky/fog **tone** shifts with cloud / fog / temperature | Weather **already** drives the sea's *mood* by blending calm/storm/fog presets on **{sea-state, visibility}** axes (ADR 0017); `DayNightProfile` carries an overcast tint + weather-dim | **Have the pattern:** drive the fog/sky tone from the **same two axes** so **sea and sky agree** (a foggy building sea = grey choppy water *and* pale low-contrast sky). |

**Score: ~5 of 7 are already built or already the plan.** The genuinely new system is **one** thing — a
unifying **screen-space, parallax-graded fog/sky layer** (call it **SkyFog**) that ties the pieces we
already publish together. §5 specs it.

---

## 4. The taste line (technique = yes; palette = no) and the phase

Two honest caveats, the same two that governed the terrain and tree-lighting references:

- **We take the *technique*, not the *palette*.** The reference reads as a **neon fantasy garden** —
  saturated magenta trees, electric green, teal murk. Our identity is the **restrained salt-stained North
  Atlantic**: *"lift the black point, crush saturation toward grey, one red buoy glowing in fog"*
  (art bible §4.2, §6). So we use the reference's **mechanism** — a screen-space fog that takes its
  colour from the sun, the clouds, and nearby lights — dialled all the way down to our master ramp. **The
  Smother is this look's killer app and its canon home:** a near-monochrome, range-collapsing fog you
  navigate by instrument and sound (canon §5.3; art bible §4.3) — genuinely moody, genuinely dangerous,
  and *on-brand*. The signature target image is **a lit Nine Mile Creek glowing warm through a grey fog at
  dusk**, not a glowing willow.
- **Phase: M2 → M3, not M1** (CLAUDE.md rule 8). M1 ships the day/night grade + weather water-mood +
  rain/mist — the backbone. Fog-as-hazard belongs to the **M2 weather wave** (time-tides-weather §4.8);
  the cloud layer, cloud shadows, and many-lights-in-fog are **M3** advanced rendering (art bible §6.1).
  This doc is the capture; the build is owner-steered later.

---

## 5. Build-later spec — the **SkyFog** layer (the one new system)

> A single screen-space layer, in the spirit of the ADR 0013 day/night overlay and the ADR 0016 additive
> lights: **one full-screen pass that composes above the world and below the HUD**, reading globals we
> already publish. Buildable in the M2 weather wave; deepened in M3. **Every value a tunable** (rule 6).

It reads, and only reads:

1. **`_SunDir` / `_SunElevation`** (ADR 0013) → a **sun-positioned bright disc** over a fill; disc
   position and warm/cool tint come straight from the sun the water specular already uses, so sky and sea
   light from the same source. Colours sampled from `DayNightProfile.SkyTint` (dawn → noon → dusk →
   night) so the owner art-directs the sky with the **same asset** they already tune the day with.
2. **`EnvironmentSample.FogDensity` / `.Visibility`** (deterministic, from `time-tides-weather.md` §4.4)
   → **fog strength**. This is the same `fogDensity`/`visibility` the day/night **weather-dim** and the
   ADR 0017 water **fog mood** already read — so the fog you *see* and the fog that *gates The Smother's
   instruments* are one truth (the P1 render==sim discipline the whole water stack holds).
3. **`cloudCover` + a scrolling cloud-noise** → cloud tint blended into the sky (and, in M3, the
   drifting **cloud shadows** on land/water from art bible §6.1). One noise field, keyed by
   `(seed, gameTime)`, gives "half under cloud, half clear".
4. **The published light globals** (`_BoatLight*` today; an array or a **light-accumulation texture**
   later) → **coloured light-glow inside the fog**, exactly the way the water shader already adds the
   boat cone (ADR 0016 follow-up 2). One warm pool of harbour light bleeding into a grey fog is the
   image we want; it composes by the same idiom that already works on the water.
5. **Parallax / sorting depth** → **aerial-perspective grade**: distant parallax layers wash toward the
   fog colour and lose contrast with distance (art bible §6 "reduced view range / aerial-perspective
   tint on parallax layers"). This is our stand-in for the reference's Z-buffer fill.

Composition & discipline:

- **Pixel-snapped** (like the water surface noise) so the fog reads as **pixel art**, not a soft gradient
  that fights the palette (PPU discipline, art bible §2/§9.2).
- Draws **above the world, below the screen-space HUD** — you must still read tide/wind/time in fog (the
  same rule the day/night overlay and lights already follow).
- Composes **downstream** of the ADR 0017 water mood and **with** the ADR 0013 day/night multiply — both
  established, both compatible.
- **Presentation-only:** drives no simulation, changes no `WaterLevel`/depth/clip, enters no save. Fog
  density is a **gameplay value** owned by the sim (it gates Smother instruments); SkyFog only *renders*
  it.

---

## 6. Determinism · performance · seams (the invariants any build must hold)

- **Deterministic (rule 5).** Everything SkyFog reads is a pure function of `(worldSeed, gameTime)` — the
  weather state, the sun, the cloud noise (a `(seed, time)` hash, **never** `System.Random`, mirroring
  `RainEmitter`/`AmbientParticleMath`). It **saves nothing**; the tide/weather stay recomputed, never
  serialized (`time-tides-weather.md` §9.2).
- **Core-only reads (rule 4).** Weather comes through `GameServices.Environment` (the `EnvironmentSample`
  accessor); the sun/light via the already-published globals. No feature-module coupling; SkyFog lives in
  the **Art** lane like the day/night controller, the lights, and the emitters.
- **Performance (rule 7) — the open cost question.** A full-screen graded pass + a cloud layer + a
  light-accumulation texture is **real GPU budget** on top of animated water + 2D-ish lights + the HUD.
  The reference author themselves said they *"only recently began focusing on optimization."* So the
  layer count and whether we need a light-accum texture are a **profiled spike**, against the 60fps
  desktop baseline, mobile-portable (ADR 0005) — this **extends the water shader-vs-overlay OQ2** (art
  bible §10). Prefer: one fog pass + one cloud noise + single-light globals first; add the light-accum
  texture only where a scene needs many lights in fog (Nine Mile Creek at dusk). Throttle uniform pushes to the
  slow tick; no per-frame allocation.
- **One perspective / one scale (LOCKED).** SkyFog must not break §2 (¾ top-down) or §3 (PPU) — it is a
  screen-space tint + pixel-snapped noise, not geometry.

---

## 7. Phased plan

| Phase | What lands | Why here |
|---|---|---|
| **M1 (now)** | *Nothing new.* Day/night grade + weather water-mood (ADR 0017) + `RainEmitter` + `SeaMistEmitter` already ship the atmosphere backbone. | Stay in phase; the slice is Coddle Cove polish. |
| **M2 — weather wave** (`time-tides-weather.md` §4.8) | **SkyFog v1:** fog fill graded by parallax depth + the sun-positioned sky tint + fog tone from the {sea-state, visibility} axes. Wire **The Smother's** persistent fog and **heavy-rain** visibility. Fog becomes the **P5 navigation hazard**. | This is when fog/rain/storm become *gameplay*, not just look. |
| **M3 — advanced rendering** (art bible §6.1) | **Cloud layer + cloud shadows**; **many-lights-in-fog** via a light-accumulation texture (a **lit Nine Mile Creek glowing in fog at dusk** — the signature image); optional shader-fog **rain integration**; the profiled layer-count/cost decision. | The heavier, world-scaling rendering, after the slice proved fun. |

---

## 8. What the owner can see of this **today** (already live)

No build needed to feel the direction:

- **Rain and sea-mist are self-installing** — they appear in every scene and already respond to the
  deterministic weather. As the sea-state climbs and **visibility drops**, rain builds and slants
  downwind; the mist drifts under the boats. (Scrub the weather / sea-state to watch it come on.)
- **The sea already moves through its moods** with the weather (ADR 0017) — a clear calm reads serene, a
  building sea greys and choppens, a smother washes it pale.
- **The day/night sky already warms and darkens** on the clock, and a boat spotlight already **bleeds its
  colour into the water** at night (ADR 0016).
- An **Atmosphere test scene builder** exists in the tools/editor menu for staging these together.

SkyFog is the layer that unifies all of the above into the reference's **fog-that-takes-its-colour-from-
the-sky-the-clouds-and-the-lights** look — restrained to our palette.

---

## 9. Open questions (for the M2/M3 art pass / owning lanes)

1. **Screen-space fog vs the existing parallax-overlay + mist.** How much is a single full-screen graded
   pass worth over the layered-sprite approach already shipped? A **profiled spike** decides (extends art
   bible **OQ2**). We may find mist + a light aerial-perspective tint on parallax layers gets 80% of the
   look for a fraction of the cost.
2. **The depth source for the grade.** 2D has no free scene Z-buffer. Grade by **parallax/sorting-layer
   index**, by the authored **seabed height map**, or by a cheap coarse depth pass? Pick the cheapest
   that reads; likely the parallax layer index for sky/distance + the height map near the shore.
3. **Many-lights-in-fog.** Single-light globals (open water, one boat) vs a **light-accumulation texture**
   (Nine Mile Creek at dusk, many windows/lamps). When does the texture earn its cost? (ADR 0016 already flags
   the extension.)
4. **Fog vs readability.** How thick can fog get before it hurts play readability on a small screen? Set
   **value/contrast floors** and a reduce-motion/high-contrast path *with* ui-ux — the same night/fog
   readability question the art bible **§10 OQ6** raises.
5. **Palette lock for fog.** Pin the **Smother's** near-monochrome fog family + the reserved-pop rule
   (*one* warm light in the murk) so fog stays on-identity, not neon (art bible §4.2/§4.3).
6. **Whether SkyFog ever becomes an ADR.** This doc is the captured **reference + recipe** (mirroring
   `water-rendering.md`). When the owner greenlights the build, the cross-cutting render decision gets an
   **ADR** (as `0010`/`0013`/`0016`/`0017` did for their seams) — the lead-architect "Water/fog/lighting"
   seam.

---

## 10. Not in scope for this document

Per CLAUDE.md rule 8 and the "docs only" status: **no shader, material, scene, component, Core surface,
or save change** ships here. This is the map and the recipe. The build is the M2/M3 art pass, owner-
steered, in milestone order — exactly as the water hero-look (`water-rendering.md`) and the advanced
rendering roadmap (art bible §6.1) are sequenced.
</content>
</invoke>
