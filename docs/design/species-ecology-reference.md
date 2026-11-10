# Species ecology reference

Status: source-backed reference data for the ten species currently authored in `Data/Fish`.
This serves P1 (read the sea) and P3 (a living coast). Each species has one `FishEcologyDef`
in `Data/FishEcology`, linked by its existing stable species id. No live system reads these
records, and no current species, school setting, catch weight, scene or save field changes.

## What the fields mean

`TypicalMinDepthMetres` and `TypicalMaxDepthMetres` describe a **reference band for a stated
life stage and locality**, measured down from the surface. They are not burial depth, seabed
elevation, whole-life distribution limits, survival limits, or hard spawning/catch gates.
Each row's `DepthEvidence` distinguishes a source's reported common range from an authored
coastal approximation. Most numeric bands below are explicitly provisional authoring choices:
the qualitative source does not establish their endpoints. They must be reviewed against a
specific life stage and region before any physical occupancy system consumes them.

Habitat flags describe associations. `OpenWater` refers to the water column; sand, mud,
gravel and rock describe substrate; `Estuary` describes a coastal setting. An eventual ecology
query must distinguish these dimensions rather than infer cover from a terrain paint colour.
This reference does not certify any terrain patch as suitable.

Zero `PreferredVegetation` means **no eelgrass/kelp preference established by these sources**.
It is not proof that a species avoids vegetation. Pollock juveniles' documented intertidal
seaweed use is recorded in notes, not generalized into an adult kelp requirement. Plant food
for a filter feeder is not an eelgrass habitat preference.

Grouping describes a useful reference tendency, not an immutable social rule or a rendered
school size. Seasonal spawning aggregations and life-stage differences remain possible.
`Variable` day activity and `NoHardRestriction` tide habits record uncertainty and prevent
an unsupported exclusion. A feeding or sheltering preference never proves physical absence.
Existing dawn/dusk, moving-water and clam-dig availability rules remain separate gameplay data.

## The table

Depth values marked * are provisional coastal reference choices, not measured endpoints in
the linked profile. Only haddock's rounded common band and rock crab's northern common band
are directly numeric summaries of the cited sources. All entries concern post-larval,
fishable animals; planktonic eggs/larvae are outside this table.

| Stable species id | Reference depth (m) | Layer / habitat | Grouping | Day / tide habit |
|---|---:|---|---|---|
| `fish.atlantic_cod` | 5–150* | Demersal; sand, mud, gravel, rock | Loose aggregation | Variable; no hard tide restriction |
| `fish.haddock` | 40–150 | Demersal; sand, mud, gravel | Loose aggregation | Variable; no hard tide restriction |
| `fish.pollock` | 5–200* | Pelagic; open water and rocky coast | Schooling | Variable; juvenile tidal cover use noted |
| `fish.mackerel` | 0–50* | Pelagic; open water | Schooling | Variable; no hard tide restriction |
| `fish.atlantic_herring` | 0–200* | Pelagic; open water | Schooling | Variable; no hard tide restriction |
| `fish.striped_bass` | 0–30* | Pelagic; coastal water and estuary | Loose aggregation | Variable; bite gates do not imply absence |
| `fish.winter_flounder` | 0–50* | Benthic; sand, mud, gravel, estuary | Solitary movement | Variable; seasonal aggregations possible |
| `fish.lobster` | 0–50* | Benthic; sand, mud, gravel, rock | Solitary movement | Mainly nocturnal shelter/foraging preference |
| `fish.rock_crab` | 0–20 | Benthic; sand, mud, gravel, rock | Solitary movement | Variable; no hard tide restriction |
| `fish.soft_shell_clam` | 0–15* | Benthic; sand, mud, gravel, estuary | Sessile bed | Filter feeding while inundated |

The lobster band deliberately describes an inshore reference, although the species also lives
much deeper. Rock crab's northern common band likewise excludes deeper documented occurrences
from the *typical* summary, not from the species' potential habitat. Sessile clam movement is
a post-settlement simplification, not a claim that larvae or small juveniles cannot disperse.

## Biology sources and limits

The machine-readable rows carry their source URLs and evidence notes. Sources consulted:

- [NOAA: Atlantic cod](https://www.fisheries.noaa.gov/species/atlantic-cod): bottom-associated predation.
- [NOAA: haddock](https://www.fisheries.noaa.gov/species/haddock): bottom types, common adult depths and shallower juveniles.
- [NOAA: Atlantic pollock](https://www.fisheries.noaa.gov/species/atlantic-pollock): schools and juvenile tidal seaweed use. This is *Pollachius virens*.
- [NOAA: Atlantic mackerel](https://www.fisheries.noaa.gov/species/atlantic-mackerel): near-surface shelf schools and seasonal movements.
- [NOAA: Atlantic herring](https://www.fisheries.noaa.gov/species/atlantic-herring): water-column schools versus eggs laid on substrate.
- [NOAA: Atlantic striped bass](https://www.fisheries.noaa.gov/species/atlantic-striped-bass): anadromy and estuarine life stages. No compulsory tide/light occupancy gate is inferred.
- [NOAA: winter flounder](https://www.fisheries.noaa.gov/species/winter-flounder): coastal bottom associations.
- [NOAA: American lobster](https://www.fisheries.noaa.gov/species/american-lobster) and [New York Sea Grant: lobster biology](https://seagrant.sunysb.edu/articles/1175/nysg-lis-lobster-research-initiative-about-li-sound-lobsters): substrate, shelter and nocturnal tendencies; offshore depth differs from the coastal reference.
- [DFO: Atlantic rock crab](https://www.dfo-mpo.gc.ca/species-especes/profiles-profils/atl-rock-crab-crabe-commun-eng.html): benthic habitat, preferred substrates and northern common depths.
- [Maine DMR: shellfish handbook](https://www1.maine.gov/dmr/sites/maine.gov.dmr/files/docs/MaineShellfishHandbook-AccessibleVersion.pdf): soft-shell clam sediment habitat and filter feeding.

## Validation and future use

`FishEcologyValidation` checks identity, explicit classifications, finite ordered depth bands,
evidence text and unique HTTPS source URLs. It reports all errors without mutating the data.
`FishEcologyReferenceTests` compares the actual complete species catalogue with the reference
catalogue: duplicates, orphans or missing rows fail. Tests also sabotage authoring, check
sessile/benthic body plans and verify that validation preserves prior findings and data.

These checks establish reference integrity, not empirical proof of every biological detail.
They do not validate actual spawned schools. The current school model has depth-band fallback
behaviour and no habitat query; converting these reference bands into exclusions would silently
change fish presence and rendering. Physical occupancy needs separately justified allowed
ranges, certified habitat data, the shared Core ecology contract, and an authorized integration.
Do not remove the fallback or retune existing species merely to make this table look enforced.
