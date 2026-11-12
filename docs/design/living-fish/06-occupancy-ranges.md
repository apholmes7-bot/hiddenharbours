# Life-stage occupancy ranges

Status: design proposal for LF-02. This defines a separate occupancy contract; it supplies
no certified numeric species limits and enables nothing. P1 and P3. Numerical authoring is
blocked on evidence for the stated stage and locality, followed by content review. The
[ecology reference](../species-ecology-reference.md) remains descriptive reference data.

## Three different questions

| Question | Data | Consequence |
|---|---|---|
| Where is this species commonly reported? | Existing `FishEcologyDef.TypicalMinDepthMetres` / `TypicalMaxDepthMetres` with scope and evidence | Reference and authoring guidance only |
| Where may this population exist in this slice? | Proposed stage-specific `OccupancyRangeDef`, below | A hard constraint only after review and authorized activation |
| Will an animal take this gear now? | Existing season, hour, tide, licence, gear and lure rules | Encounter eligibility; never evidence of physical absence |

An allowed range is a reviewed **game population envelope**, not a claim about all biological
survival limits. It can intentionally represent only an inshore population. A source reporting
a common adult depth cannot establish juvenile limits, an absolute maximum, or the depth of
a different body part. Broadening a reference band with a guessed margin supplies no evidence.

## Proposed authored record

One immutable Def per species, stage and named population scope; stable append-only id such
as `occupancy.atlantic_cod.coastal_adult`. Store references by id, not enum ordinal casts.
Separate records permit different allowed intervals without changing the typical reference.

| Field | Required meaning |
|---|---|
| `Id`, `SpeciesId`, `StageId`, `PopulationScopeId` | Stable identity; explicit geographic/population scope, not a filename or current scene |
| `StageDefinition` | Evidence-backed stage description; size/age evidence if available. Catchable size is not automatically adult maturity. |
| `ReviewState` | `Unreviewed = 0`, `Reviewed`, `Withdrawn`; default cannot activate |
| `RangeState` | `Unknown = 0`, `Known`; unknown has no numeric interpretation |
| `Metric` | `Unspecified = 0`, `BodyDepthBelowSurface`, `WaterColumnDepthAtSupport` |
| `AllowedIntervalsMetres` | Finite, nonnegative, inclusive closed intervals; sorted, disjoint, non-touching. Known requires at least one interval. Empty is invalid, never an implicit whole ocean. |
| `HabitatPolicyId`, `ExposurePolicyId` | Reviewed requirements for the certificate, support and permitted exposure |
| `Evidence`, `SourceUrls`, `ReviewRationale` | State which numbers are observed, converted or game-authored, and why this population envelope was chosen |
| `Revision` | Immutable content revision bound to the population epoch; changing a live population needs an explicit reconciliation plan |

Use explicit stages: `juvenile`, `adult`, and, for shellfish, `settled_juvenile` and
`settled_adult`. `Unknown` stage never borrows the adult record. Eggs, larvae and a generic
mixed-stage population remain outside the first implementation. There is no implicit aging
or reproduction simulation. A population generator must supply a reviewed stage assignment;
the current school field does not do so. Overlapping profiles at the same scope/stage are
an authoring error, not precedence decided by load order.

For swimmers the metric measures the body reference point below the shared, undisplaced
simulation surface. Body clearance above bed and below surface are independent physical
constraints. For crawlers and settled shellfish it measures water-column depth above their
actual certified bed attachment; burial distance is separate. Do not put a clam at a nominal
midwater depth merely because that number is inside its interval.

An intertidal settled occupant can remain attached when the bed is exposed if its reviewed
exposure policy allows it. Its environmental column depth can then be negative; wet-range
membership is not evaluated as a negative body depth. It cannot feed underwater, enter a pot,
or swim on dry land. Exposure and dig accessibility are separate from token existence.

## Content readiness, all current species

Each cell below is **Unknown**, pending stage-specific numeric evidence and a reviewed game
envelope. This is deliberately a readiness ledger, not ten invented numeric limits. Existing
typical values are not copied into allowed intervals. No record in this table may activate.

| Species id | Juvenile / settled juvenile | Adult / settled adult | Authoring issue to resolve |
|---|---|---|---|
| `fish.atlantic_cod` | Unknown | Unknown | Separate nursery scope from the represented coastal adult population |
| `fish.haddock` | Unknown | Unknown | Shallower juvenile tendency and common adult depths do not specify allowed endpoints |
| `fish.pollock` | Unknown | Unknown | Juvenile tidal seaweed use must not become a compulsory adult habitat rule |
| `fish.mackerel` | Unknown | Unknown | Resolve population/stage and body depth versus total seabed depth |
| `fish.atlantic_herring` | Unknown | Unknown | Swimming stages and substrate-bound eggs need different contracts |
| `fish.striped_bass` | Unknown | Unknown | Scope coastal versus estuarine life stage; do not invent salinity from a water surface |
| `fish.winter_flounder` | Unknown | Unknown | Certify bottom support independently from water depth |
| `fish.lobster` | Unknown | Unknown | Inshore reference excludes deeper occurrences; require stage and shelter evidence |
| `fish.rock_crab` | Unknown | Unknown | Northern common depth is not a hard distribution maximum |
| `fish.soft_shell_clam` | Unknown | Unknown | Distinguish fixed post-settlement attachments, exposure and burial depth |

Primary evidence supporting the **need for the distinction**, not numeric authorization:
[NOAA haddock resource guide](https://repository.library.noaa.gov/view/noaa/3815/noaa_3815_DS1.pdf)
describes common depths and a juvenile/adult difference;
[NOAA Atlantic pollock resource guide](https://repository.library.noaa.gov/view/noaa/20206/noaa_20206_DS1.pdf)
distinguishes juvenile intertidal behaviour from offshore adults;
[DFO crab habitat assessment](https://waves-vagues.dfo-mpo.gc.ca/library-bibliotheque/248551.pdf)
reports rock crab's shallow common habitat. None alone certifies stage-specific exclusion
endpoints for this game's regions. The full source catalogue is in the ecology reference.

## Evaluation and required proof

1. Resolve exact species/stage/population scope and reviewed content revision. Missing,
   unreviewed, withdrawn or unknown yields `Unknown`, never a borrowed typical band.
2. Obtain a complete, valid [Core ecology certificate](07-core-ecology-certificate.md) for the
   full reachable footprint and explicit game time interval. Unknown or partial evidence is
   `Unknown`; a known incompatible habitat is `Unsuitable`.
3. Intersect each allowed body-depth interval with geometric surface/bed clearance bounds.
   For a supported animal, sample the real attachment instead; enforce its wet/exposure policy.
   A proven empty intersection is `Unsuitable`, distinct from absent evidence.
4. Query presence separately from feeding eligibility. Failure to bite never removes an animal.
   A temporary unsuitable patch must be reported to a persistent-population reconciler; it must
   not silently erase/recreate owned tokens or move a sessile attachment.

Future EditMode acceptance must prove exact endpoints and the next representable number
outside each endpoint; all union gaps; negative, NaN and infinite authoring; unknown stages;
duplicate scopes; mismatched revisions; and metrics at different tides. Use synthetic ranges
such as `[2, 6]` and `[9, 12]` metres to prove the arithmetic without asserting biology.
Changing reference-only depths must leave occupancy results unchanged. Unknown must remain
unknown after serialization/default construction, and known unsuitable must remain distinct.

LF-02 may activate only in the single tested slice with all consumers wired to one population;
the defaults elsewhere remain disabled. Numeric profile authoring, Core implementation,
geography certification and activation remain separate work with the relevant reviewers.
