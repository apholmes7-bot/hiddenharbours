# ADR 0066 — Save v28: the photo album, and the camera's key

- **Status:** Proposed (owner 2026-11-26: "yes build the camera now with photo record added to save"; he
  accepts this ADR by merging its PR). Numbered 0066 on the camera branch; 0065 is free on this branch but may
  be taken on main by the time this lands. Renumber freely; nothing in code depends on the number beyond
  comments.
- **Owner:** lead-architect
- **Touches:** Core save (`SaveData`, `SaveMigration`, new `SaveRowsV28.cs`, `PhotoAlbumRows`), Core input
  (`CameraIntents`, `ControlIntentGates`), Core photography (`PhotoCameraDef`, `PhotoScoring`,
  `PhotoViewfinder`, `PhotoTaken`), the wildlife seam (`WildlifePresences.CopyInFrame`, additive).

## In plain words

**What gets added to your save.** One list: the pictures you keep. For each picture it records who took it,
its number in the album, when and where it was taken, how good a picture it is, and what was in it (which
animals, how many, what the best of them was doing), and one very small picture of it, a few dozen pixels
across, which the notebook's album page shows as a little Polaroid (owner 2026-11-26: "yes photoalbum should
save small pictures as little poloraids"). Each picture adds about 1.1 KB to the save; the album holds at most
120 pictures per player, so the most it can ever add is about 131 KB per player.

**What happens to your existing save.** It loads as before, with an empty album. Nothing else changes. Tide,
wind and weather are still never stored.

**Going back to an older build.** An older build does not know the album; if it saves a v28 file, the album
is lost from that file and everything else stays.

## Context

The owner ruled (2026-11-26) that the seals on the Whelps are the first camera practice and that Odile
Marchand, in the Whelps cottage, asks for a picture of them; then that the camera be built now, with the photo
record added to the save. What a player has photographed cannot be worked out again from the seed and the
clock, so it must be saved to last. Main is at v27 (ADR 0064); every earlier bump followed one pattern, and
v28 follows it: an append-only field, flat JsonUtility DTOs, one guarded step, an idempotent asset-free load
repair, `SaveMigrationV28Tests`, and the reflection round trip.

## Decision

### 1. The field (appended to `SaveData` after `RodFittings`)

| Field | Type | Default |
|---|---|---|
| `Photos` | `List<PhotoRecordDto>` | empty |
| `PhotoRecordDto.PlayerKey` | `string` | `"player.local"` |
| `PhotoRecordDto.Number` | `int` (album number, unique, never reused) | 0 (a kept row has at least 1) |
| `PhotoRecordDto.GameTimeSeconds` | `double` (`IGameClock.TotalSeconds`) | 0 |
| `PhotoRecordDto.RegionId` | `string` (`region.*`) | `""` |
| `PhotoRecordDto.Score` | `int` 0..100 (the best subject's) | 0 |
| `PhotoRecordDto.Subjects` | `List<PhotoSubjectDto>`, best first | empty |
| `PhotoSubjectDto.SubjectId` | `string` (`wildlife.*`) | `""` |
| `PhotoSubjectDto.Count` | `int` | 1 |
| `PhotoSubjectDto.Pose` | `int` (`WildlifePose`: 0 resting … 4 diving) | 0 |
| `PhotoSubjectDto.Score` | `int` 0..100 | 0 |
| `PhotoRecordDto.Thumbnail` | `string` (Base64: width byte, height byte, RGB565 little-endian, rows bottom to top) | `""` |

Ids and numbers, and one thumbnail. The thumbnail's size is the camera def's (`ThumbnailWidthPx`,
`ThumbnailHeightPx`, shipped 24 x 17: 818 bytes, 1,092 Base64 characters), and it is carried in the header, so
a later retune never breaks an old picture. It is taken once per kept shutter: the frame rendered at the art
grid (32 px a metre) and point-sampled down, never blended. The album draws it one kit pixel per thumbnail
pixel inside a Polaroid card (`PolaroidBorderPx`, the wider `PolaroidBottomBorderPx`, `PolaroidFrameColour`),
point filtered. Worst case: `AlbumCapacity` (120) x 1,092 = 131,040 characters of thumbnail per player, plus the
record rows. Every size and colour is a camera def tunable. Names, pose words, quality words and place names come from the camera def, so a retune
or a reword moves every save with it. Per-player rows (`PlayerKey`) so co-op adds rows, not a version.

### 2. Migration and repair

`if (SchemaVersion < 28) { Photos = new(); SchemaVersion = 28; }`. The load repair (every load): drop null
rows, subjects with no id, and photos left with no subject; normalise the key; non-finite or negative time
reads 0; count under 1 reads 1; an unknown pose reads Diving; scores clamp to 0..100; the first holder of a
number keeps it, a repeat or a number under 1 is renumbered above the highest; rows sorted by number. It never
raises a score or invents a subject. A thumbnail that does not decode (bad Base64, or a length that does not
match its header) reads empty; the picture's record is kept.

### 3. A picture is deterministic

A picture's record is a pure function of the frame, where the camera stands, what the wildlife seam reports and
the camera def (`PhotoScoring`): no randomness, no clock beyond the game time stamped on it. The shutter's
cooldown uses unscaled time and decides only whether a press counts, never what the picture holds.

### 4. The camera's key (amends ADR 0043's intent table)

A new `Camera` action map: `Raise` (P / gamepad north), `Shutter` (Space / right trigger), `Aim` (WASD and
arrows / left stick). Read through `CameraIntents` and gated by `ControlIntentGates` like every other source.
P and north are shared with `Helm/Autopilot`; that is safe because the camera answers only on foot and on deck,
never at the helm. Space is shared with `Helm/Brace` and `Drive/Brake` (same reason) and with the rod's cast
and the mooring throw: the camera will not come up with anything in his hands, which keeps it off the rod;
mooring at a cleat is an open edge (owner question).

### 5. Not saved

The viewfinder's state, the aim, the cooldown, the last line: presentation only. Photo requests keep no state:
asked, pictured and done are flags; "will a picture do" is read from the album.

## Consequences

- One bump, one list; save files grow by about 1.1 KB a picture (the thumbnail and its record), capped by
  `AlbumCapacity`.
- The thumbnail is the only image the save carries (owner 2026-11-26); a larger print would be a new ruling.
- Opening a v28 save in an older build loses the album.
