# Small-craft paint — swatch reference

The punt's six and the console skiff's nine hull schemes, rendered from the rigs themselves. These
are a **human-readable reference**, not an input to anything: the game reads baked
`HullPaintSchemeDef` assets under `Assets/_Project/Data/Boats/PaintSchemes/`, and those are read out
of each rig's own `palette({scheme:id}).mats` resolver, never from a picture.

| File | What it shows |
|---|---|
| `punt-all-schemes.png` | all 6 punt schemes at one facing |
| `console-all-schemes.png` | all 9 console-skiff schemes at one facing |
| `punt-before-after.png` | `harbour-white` (the default — unchanged, today's boat) above `squall-grey` (Dan Peters's), across 4 facings |
| `console-before-after.png` | the same for `harbour-white` above `sand-canvas` |
| `wharf-smallcraft-crop.png` | the small-craft end of the Nine Mile Creek proof render — Marie Gallant's unpainted Cape Islander beside Peters's grey punt |

**Provenance.** Rendered in `RigProbe`, a throwaway `net8.0` console app that references the repo's
own ClearScript (`Assets/_Project/Plugins/Editor/JsEngine/`) in place and runs
`docs/art/rigs/{puntIsoRig,consoleIsoRig}.js` **unmodified** — the same engine `V8RigScriptHost` uses,
so a render here is what the baker would produce. Verified against a committed artefact before being
trusted: the **pre-#292** rigs reproduce `Assets/_Project/Art/Boats/PuntIso.png` and `ConsoleIso.png`
byte-for-byte (0 of 989,184 B and 0 of 1,686,528 B).

**⚠️ Those two shipped sheets are STALE, and it is pre-existing.** They are the pass-1 rig's output —
hand-exported in July 2026, before the rigs were imported — and have never been re-baked since the
pass-2 rewrite (#292) turned paint into data. Today's rigs differ from them by 1.34% (punt) and 4.00%
(console) of sheet bytes. They are also **counter-clockwise** (`FacingsAreCounterClockwise: 1`, cell
*k* = `render(k)`), unlike anything `RigBaker` produces. The hulls draw from their **meshes** in game,
so this does not affect what the player sees; it does mean the sprite fallback and the mesh no longer
agree, and it means a committed sheet cannot serve as a control for these two hulls.

The paint itself moves no geometry: each hull's face list is byte-identical across every scheme
(vertices compared as f64), and one alpha mask serves all schemes at all 8 facings.
