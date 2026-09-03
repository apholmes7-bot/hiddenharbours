# Lights PR B — the lamps cast shadows: the eyeball pack

The owner's sentence, 2026-08-28: *"the spotlights and headlights need to put shadows ... The light
needs to affect the environment, create shadows."* PR A (#691) lit the sea by relief; this pack is the
second half — plates for the owner's eye, every one at the **SHIPPED** lamp (`BoatSpotlight` defaults:
intensity 1.5, range 9 m, half-cone 26°, 2.5 m up; the nav lamps' shipped reach) and the day/night
profile's **own** tint at the hour named, measured off the published `_DayNightTint` at the moment of
capture. Nothing was brightened to make the feature show; where a plate reads dark, that is evidence for
the owner's night ruling (water-fidelity register row 12), not a reason to retune.

## The arrival, live (a second editor on the lane's worktree, the real game, the game's own camera)

The pilotage is real-time and the clock runs at 75 real seconds per game hour, so she docks around
06:50 at dawn light; for these plates the clock was set to **06:13 and held there** during the whole
approach (the light is the profile's 06:13, `(0.404, 0.300, 0.237)`; the boat's motion is unchanged).
The committed scene predates the builder change that gives the wharf's standers a caster, so the six
bollards and pileheads were given their `SpriteShadow` at runtime — what `StPetersWharf.Place` now does on
the owner's next rebuild. 1920×1080, read back from the main camera after the overlay and the glows.

| plate | hour · tint | what to look for |
|---|---|---|
| `arrival-0613-01-searchlight-rakes-the-dory.png` (+ `-crop.png`) | 06:13 · (0.40, 0.30, 0.24) | Coming alongside: the searchlight rakes the wharf head and the **moored dory throws her striped silhouette down the beam**, away from the lamp — the mesh hull cast through the resolved screen texture. 4 shadows live. |
| `arrival-0613-02-beam-along-the-wharf-face.png` | 06:13 | Three seconds on, turned onto the berth heading: the beam lies along the wharf's south face, the dory is out of the cone and her shadow is gone with it; the standers on the lip are in it. 8 shadows live. |
| `berth-0613-moored-beam-at-the-way-gate-floor.png` | 06:13 | Tied up. `BoatSpotlight` dims the beam toward its 0.15 floor at a standstill, and the shadows fade with it (`IntensityShare`) — a shadow is only as strong as the light it blocks. 7 shadows live, faint by design. |
| `berth-0200-shipped-exposure.png` | 02:00 · (0.039, 0.046, 0.072) with the new game's full moon | **The night-ruling evidence.** At the shipped lamp and the shipped night the frame is the nav lights, the cabin glow and a faint amber wedge; the wharf beside her does not read, so nothing in it can be seen to shadow. Reported as found, not retuned. |
| `berth-1200-noon-control.png` | 12:00 · (0.90, 0.92, 0.93) | The noon control: the gate is shut, every lamp is dark, **0 lamp shadows** (the sun's own shadows are `SpriteShadow`'s and untouched). |
| `berth-0613-budget-pool-full-24-of-24.png` | 06:13 + a temporary 80 m radial flood lamp at the berth | **The budget plate**, blown out on purpose: the flood lamp fills the pool — **24 / 24** shadows, 8 lamps, 447 casters registered — and every silhouette that casts is legible: her own hull (the big wedge), the dory, the standers, the shore trees. Smoothed editor frame 11.6 ms against 7.6 ms with 7 shadows on the same view (RTX 4060, editor play mode, Game view open). |

## The render fixture (`LampShadowRenderTests`, deterministic, measured)

| plate | what it shows |
|---|---|
| `fixture-sprite-post.png` | A post under a round lamp 3 m west, strength 0.8 (left) against strength 0 (right): 575 px darkened against a 600 px post, 0 outside the predicted sheared box, 0 on the post, centroid 1.58 m east. Strength 0 is byte-identical to the system absent. |
| `fixture-sprite-sweep.png` | The lamp west (left) then east (right): the shadow swings to the other side — cast BY the lamp. |
| `fixture-hull-dory.png` | The dory as a mesh hull, lit from the west: 1,830 px darkened, 1,406 of them east of her cell, through her id block — no sprite, no bake. |

Every fixture plate is an A|B contact sheet with a hairline divider, written by the fixture into the
gitignored `artifacts/lamp-shadows/` and copied here by hand; the fixture reads its own published file
back and asserts it is the right way up (the #688 lesson).
