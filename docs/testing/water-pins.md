# Recomputing the water source pins

Run from the repository root with Python 3 (standard library only):

```sh
python tools/water_pins.py
```

On Linux, use `python3` if that is the installed command. The script also works
from another working directory: its default root is the checkout containing the
script. Use `--root /path/to/checkout` to inspect a different checkout.

Each row prints a pin label, the expected hash read from
`Assets/Tests/EditMode/SurfSequenceTests.cs`, the recomputed hash, and `OK` or
`MISMATCH`. There are 13 rows. Exit status is 0 when all agree, 1 for a mismatch
or an unreadable/invalid individual input, and 2 when the test's pin declarations
or material inventory cannot be read. Invalid reconstruction inputs report
`recomputed=ERROR` with a reason, then continue with the other pins. No files are
written, no editor starts, and no network access or packages are needed.

## What the pins protect

These are the existing `SurfSequenceTests` scope guards, not new acceptance
criteria. The script reads their expected values each time; it does not keep
another copy of the approved hashes.

| Output label | Input and protection |
| --- | --- |
| `BaseWaterHash` | The shader at the test's `WaterPath`, reconstructed to its approved control. Guards changes outside the permitted B1 fringe blocks and F2 declarations, including the edge, clip, sheet and shader twins. |
| `AdvectPath` | The entire foam-buffer advection shader at the test's `AdvectPath`. Guards the source that deposits and transports foam, including bore paths. |
| `BreakerMath.cs` | The entire file at `Assets/_Project/Code/Core/Environment/BreakerMath.cs`, including the shared breaker arithmetic. |
| `WaterSurface.cs` | The entire file at `Assets/_Project/Code/Art/WaterSurface.cs`, including CPU twins and water binding code. |
| `Water.mat` and eight `Water_*.mat` rows | Base material followed by the ordinally sorted `WaterPresets/*.mat` files under `Assets/_Project/Art/Materials`. Each is paired with the corresponding `originalHashes` entry. Guards all serialized content except the explicitly reversed additions/values below. |

The direct source pins include comments and whitespace. The water-control and
material pins also retain all text outside the specified transformations. The
test's `Code()` comment/whitespace stripper is **not** used to compute any pin.

## Exact reconstruction

All inputs are read as UTF-8 with an optional BOM removed. Only CRLF becomes LF;
lone CR, other whitespace, and final newlines are preserved. The resulting
string is encoded as UTF-8 without a BOM, hashed with SHA-256, and printed as
lowercase hexadecimal. Thus CRLF and LF checkouts produce the same hashes as
the test's `Read()` and `Hash()` helpers.

For `BaseWaterHash`, `MainSource()` is mirrored:

1. Require the `PROPERTY`, `UNIFORM`, `DEPTH` and `COVER` B1 FRINGE marker blocks.
   Remove each complete block, including markers; replace `DEPTH` with the exact
   `LegacyDepth` string read from the test, including its comments and newlines.
2. Require each exact F2 declaration line once and remove it with its newline:
   the policy comment, the three transport properties, and their uniform line.
   The declaration strings are read from the test.
3. Hash the reconstructed source. The other three source files are hashed
   directly after the shared newline/BOM handling.

For each material, mirror `SurfSequence_FringeDialShipsAtZeroOnAllNineMaterials`:

- Require one `_SurfFringeSequenceStrength: 0` row and remove it before hashing.
- Require and remove the three zero `_FoamTransportStrain`, `_FoamTransportCurl`
  and `_FoamTransportCollection` rows, and `_SwellReadRelative: 1`.
- For `Water_StormGrey.mat`, require `_PaletteValueFloor: 0.16` and map it back
  to `0.07`.
- For base `Water.mat`, require and map `_RippleStrength: 0.25` to `0.45` and
  `_SunGlitterStrength: 0.25` to `0.6`. Require and remove
  `_ObjectReflectFadeChop: 0.75` and `_ObjectReflectLowLight: 4`.
- For base `Water.mat`, require exactly one row for each of the ten low-sun keys
  and remove it regardless of value: `_SunGlitterForm`, `_SunGlitterSpark`,
  `_LowSunHueHold`, `_LowSunWakeHold`, `_SunGlitterCell`, `_SunGlitterDash`,
  `_LowSunWindow`, `_LowSunSheen`, `_LowSunSkyNear`, `_LowSunSkyFar`. Their list is
  read from the test. `WaterLowSunTests` separately guards the look-bearing values.

## When re-pinning is legitimate

A mismatch is a reason to review the changed input, not permission to replace a
hash. First identify the exact source/material change and the rule or approved
premise that moved. A deliberate, reviewed change may justify a new pin only
after the relevant logic tests and any required visual acceptance have passed.
Record that reason and evidence beside the pin and in the change description.
Unexpected drift, a broken marker, a wrong material default, or a failed visual
check must be fixed rather than hidden by a new baseline. A matching hash alone
does not certify the rendered appearance.

Use the recomputed value to prepare a legitimate pin update in the owning
change, then run this command again and run the relevant EditMode tests. This
command does not update pins, approve a look, or replace `SurfSequenceTests`:
it does not run the non-hash assertions or GPU cases.

The parser deliberately supports the current literal declarations and fails
when the pin layout or material count changes. When the C# reconstruction,
normalization, or material reversal rules change, review and update the Python
mirror alongside them; the script is not a general C# interpreter and cannot
detect every future algorithm change. Keep `Read`, `Hash`, `Section`,
`MainSource`, and the two source/material guard methods as the reference.
