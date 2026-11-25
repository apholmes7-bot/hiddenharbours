# Shared Core ecology certificate

Status: proposed Core boundary for LF-02, for architecture and world-content review. Design
only; it adds no interface, service registration, geography, save field or runtime reader.
P1 and P3. It complements [occupancy ranges](06-occupancy-ranges.md) and the
[capture phases](../living-fish-capture-phases.md).

## Baseline and ownership

`ITidalTerrain` answers elevation for the active scene; `IEnvironmentService` supplies
deterministic regional water level. Neither certifies substrate, vegetation, connectivity
or the bed maximum over an area. In particular, `ITidalTerrain`'s legacy null-as-open-water
fallback is **not** a habitat certificate. Preserve that API's existing callers; the new
boundary must report unknown independently.

`IStillWater` composes a surface for its approved consumers. ADR 0046 currently leaves clams,
traps and fish presentation on the tide. This proposal does not extend those consumers into
ponds. Every certificate explicitly names its water-body and surface policy; fresh water
cannot be inferred from the existence of a still-water texture.

Core owns values and interfaces. World implements immutable region queries from authored
geometry and ecological data, available without a loaded scene. Fishing consumes Core values
through a reviewed adapter. Art can read the same evidence without deciding biological
presence. No feature module references World or Fishing concrete types across the boundary.

## Values and unknowns

Proposed names below are contract sketches, not existing APIs. Default state is unknown.

| Value | Contract |
|---|---|
| `EcologyRequest` | Region id, water-body id, explicit game-time interval, requested disc or supported route, requested evidence dimensions, expected immutable content revision |
| `CertificateId` | Stable authored patch/route id plus content revision, region and water-body; no instance id, hash-only identity or scene handle |
| `QueryStatus` | `Unknown = 0`, `Complete`, `InvalidRequest`, `Incomplete`, `RevisionMismatch`. A complete response may prove unsuitable; unknown never means empty. |
| `Knowledge<T>` | `Unknown = 0` or `Known(value)`, independently for each dimension; no magic zero/NaN sentinel |
| `Substrate` | Known substrate set (sand, mud, gravel, rock) and its homogeneous footprint or explicit subdivisions |
| `WaterColumn`, `CoastalSetting`, `Cover` | Separate dimensions: open column; estuary/coastal setting; pilings/shelter. A rock value must not also mean an estuary or a piling. |
| `VegetationEvidence` | Independent eelgrass and kelp states: `Unknown`, `Absent`, `Present`, each with its certified extent. Known absent is not unknown; absence of a preference is not a survey. |
| `SupportEvidence` | Known bound on highest bed elevation over the complete disc, in metres above chart datum; supported routes additionally carry actual bed samples/analytic support and a conservative interpolation error bound |
| `Validity` | Half-open game-time interval `[validFrom, validUntil)`, content/terrain revisions, surface policy and required feature bits. Explicit unbounded validity is allowed only for certified static evidence. |

Depth is **derived** from the approved water surface at the requested instant and certified
bed, not stored as a timeless property. Static substrate does not imply static wetness.
Oxygen, salinity and temperature remain unknown unless a future reviewed provider supplies
them. A profile requiring one of them cannot silently pass without it.

Substrate sets in a disc certify the same acceptable classification throughout that disc;
they are not the union of tags observed somewhere inside. A disc half rock and half mud
cannot satisfy an all-rock requirement from that union. Return homogeneous subdivisions or
refuse the disc. Likewise vegetation preference may use only the certified location/extent;
one eelgrass sample cannot grant a bonus to a whole cell.

## Query and geometry guarantees

Proposed `IRegionEcology.Query(in request, callerBuffer)` returns status, evidence and a
continuation cursor if incomplete. Validate finite coordinates, nonnegative radii, ordered
finite game times, region/surface/revision agreement and supported feature bits. Do not
resolve a negative radius or an unknown region by clamping to an unrelated place.

Return certificates in stable ordinal id order. Buffer exhaustion is `Incomplete` with the
same snapshot revision and an exact continuation; it cannot become a known-empty response.
Consumers needing absence must finish the query. Unsupported geometry and unloaded provider
data are unknown, not complete with zero patches. The time interval cannot be silently
shortened to now. A certificate cannot outlive its content revision.

For a swimming group, certify the entire reachable disc: patrol radius + maximum reaction
displacement + maximum member footprint. Highest-bed elevation bounds **every** point inside;
a centre-only sample or a few unchecked perimeter samples cannot establish this maximum.
World must use an authored conservative bound or a bounded geometry proof, including thin
shoals/holes between samples. Numerical error widens the unsafe region rather than the safe
disc. Adjacent disc certificates do not imply a connected travel corridor.

For benthic routes, certify attachment support across every segment and junction, maximum
slope/step and interpolation error, and exposure over the required interval. Fixed clam
attachments never become a moving route. Pot entrances need this same supported geometry;
the school field's conservative patch depth cannot stand in for actual feet on a seabed.

These are evidence queries, not permission to project a creature onto nearby ground, lift a
boat above support, alter terrain, or fabricate ecological data from paint colours. World
owns authoring and can refuse a certificate without modifying its scene.

## Adapting today's two vocabularies

The reference `EcologyHabitat` and inactive `FishHabitat` are distinct Fishing enums with
different bit values. Numeric casts would misclassify gravel as rock and rock as pilings.
Map by explicit named cases through Core, rejecting unknown/future bits. Reference
`Estuary` is a setting; reference `OpenWater` is a column classification. The inactive field
has no gravel bit: do not quietly call it sand or discard it. Return unsupported until the
adapter and reviewed profile express that substrate. `WharfPilings` needs authored cover
evidence; neither rock nor the existence of a wharf object certifies it.

`NoDocumentedPreference = 0` in ecology reference data is not `Known(Absent)` vegetation.
The SchoolField adapter may pass `VegetationKnown = true` only when **all** vegetation kinds
it summarizes are known over the relevant footprint. Unknown vegetation may withhold a
preference bonus for a profile with no hard vegetation requirement. A hard requirement fails
closed as unknown. Do not conflate preferred with required habitat.

## Acceptance and release gate

Required future EditMode tests: default/absent provider; known bare bed; one unknown plant
dimension; malformed and mixed revisions; unsupported flags; exact buffer overflow and resume;
same queries with reordered cells and unloaded regions; mixed-substrate subdivisions; a thin
shoal missed by centre sampling; maximum footprint; route gap and support discontinuity;
stale content; water-body mismatch; and tide at the event instant. Expected statuses must be
exact, including `Unknown` versus known incompatible versus complete empty.

Implementation needs architecture review of the Core contract and world-content review of
the certificate producer. Validation must not mutate reference data, existing tunables or
geometry. Activation remains off until the authorized LF-02 slice proves sonar, bite and
visible readers share one population and no fallback invents animals. Approved visual proof
belongs to the art/world owners; this document claims no plate or renderer validation.
