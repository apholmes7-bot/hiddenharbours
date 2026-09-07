"""The tide, as DECLARED TERMS — water from elevation, faces cut at the waterline, hulls that ride.

The owner, 2026-09-07: *"i want to represent water at high tide in the editor"* → *"ok yes i want
faces and hulls to ride it too"*. All three of those are **already in the game**. This module's whole
job is to **state** them in the package so a reader can apply them; it never evaluates one.

  * **Water** is wherever ``groundElevation < seaLevel``, and
    ``seaLevel = terrain.waterLevelMeters + terrain.x-tide.amplitudeMeters × carrier`` for a carrier
    the reader chooses in [−1, 1]. The exporter supplies neither a carrier nor a time: rule 5 says
    the tide is recomputed from ``(worldSeed, gameTime)`` and never stored, so a package that shipped
    "the water at 06:11" would be shipping a saved tide.
  * **A vertical face** is drawn only where the water is not over it (``TidalFaceWaterline``, #765):
    ``waterlineWorldY = lipWorldY + (seaLevel − lipElevation) × heightScale``.
  * **A hull's picture rides** (``TidalRide``, #753): ``waterline = max(sea − draught, bed) + draught``
    then ``screenRise = (waterline − bakedWaterlineElevation) × heightScale``. Her plan never moves.

**Every constant is read out of the C# that declares it** (rule 6, and the handoff's law: never
typed twice). What this module composes are the repo's own one-line expressions — ``BakedDeckZMetres
=> BakedRigTideRange + BakedRigClearance`` — which ``csharp.number`` will not follow because they add
two members rather than scaling one. Each leaf is pinned by a test, and each composition ships its
C# expression as a string beside the number so a reader can check the arithmetic rather than trust it.

⚠ **What this module will NOT do is measure a face's lip against the analytic terrain.** Nine Mile
Creek's ``ITidalTerrain`` is ``MainlandTidalTerrain`` — a floor, a coast run, a bar, carves, fills
and channels composed in a fixed order — and ``NineMileCreekDressing.FaceLipElevation`` samples it.
Reimplementing that composition in Python would be a second definition of the coastline, which is
the review's §7(c) trap and the same one ``heightmap``'s docstring refuses. So the lip elevation is
the DECLARED deck constant the run stands on, and the committed height map — which
``TerrainPaintTool.BakeNineMileCreekSeabed`` rasterises out of that very terrain — is sampled at the
same footprint centre the game samples and shipped beside it as a CROSS-CHECK. The two agree to
within the map's own 8-bit quantum on every course, and the package says so per piece.
"""

import math
import os

from .csharp import CSharpSource

# The four files the tide's terms are declared in. Located by name rather than by path, the same
# way heightmap.read_bands finds the shore map, so a file that moves does not silently zero a term.
ISO_GROUND = "IsoGround.cs"
QUAY_FACE = "NineMileCreekQuayFace.cs"
MAINLAND = "NineMileCreekMainland.cs"
DRESSING = "NineMileCreekDressing.cs"


def _source(repo, filename):
    for candidate in repo._find_by_name(filename):
        source = CSharpSource(repo, candidate)
        if source.present:
            return source
    return None


class TideTerms:
    """The declared constants, plus the compositions the repo writes as one-line expressions.

    ``ok`` is False when any leaf is missing, and the caller then states the tide as unavailable
    rather than emitting a block built on a defaulted zero — a face cut at a heightScale of 0 draws
    the whole wall under water, which is a picture nobody would question.
    """

    def __init__(self, repo):
        self.missing = []
        iso = _source(repo, ISO_GROUND)
        face = _source(repo, QUAY_FACE)
        land = _source(repo, MAINLAND)

        # IsoGround.HeightScale is `Mathf.Cos(CameraElevationDegrees * Mathf.Deg2Rad)` — a computed
        # readonly, so the LITERAL to read is the camera's elevation and the cosine is ours. Height
        # is not depth: the sine of the same angle is GroundDepthScale (0.643), 19% short, and the
        # lip's own half-width offset is the one place in this module that wants it.
        self.camera_elevation_deg = self._need(iso, "CameraElevationDegrees", f"{ISO_GROUND}")
        self.height_scale = (math.cos(math.radians(self.camera_elevation_deg))
                             if self.camera_elevation_deg is not None else None)
        self.ground_depth_scale = (math.sin(math.radians(self.camera_elevation_deg))
                                   if self.camera_elevation_deg is not None else None)

        self.baked_tide_range = self._need(face, "BakedRigTideRange", QUAY_FACE)
        self.baked_clearance = self._need(face, "BakedRigClearance", QUAY_FACE)
        self.baked_mud_z = self._need(face, "BakedRigMudZ", QUAY_FACE)
        self.face_course_width = self._need(face, "FaceCourseWidthMetres", QUAY_FACE)
        self.wharf_deck_elevation = self._need(land, "WharfDeckElevation", MAINLAND)
        self.breakwater_crest_elevation = self._need(land, "BreakwaterCrestElevation", MAINLAND)

    @property
    def ok(self):
        return not self.missing

    def _need(self, source, name, where):
        value = source.number(name) if source is not None else None
        if value is None:
            self.missing.append(f"{where}#{name}")
        return value

    # --- the repo's own one-line compositions ---------------------------------------------------

    @property
    def baked_deck_z(self):
        """``NineMileCreekQuayFace.BakedDeckZMetres => BakedRigTideRange + BakedRigClearance`` — 5.20 m,
        the height a fixed family's deck stands at in the RIG's frame, and therefore the height of
        the drawn quay face. The wharf contract states the same number under ``deck.deckZ``."""
        return self.baked_tide_range + self.baked_clearance

    @property
    def drawn_face_height(self):
        """How much HEIGHT the drawn face spans, deck lip to the bottom of its feet:
        ``BakedDeckZMetres − BakedRigMudZ`` = 6.60 m. Times ``heightScale`` this is
        ``DrawnFaceDropMetres`` ≈ 5.06 units, which is what #765 measured on the committed sheet."""
        return self.baked_deck_z - self.baked_mud_z

    @property
    def pack_datum_rise(self):
        """``NineMileCreekQuayFace.PackDatumRise => BakedDeckZMetres * SpriteLightMath.HeightScale``
        ≈ 3.983 units — where every wharf-pack piece's chart datum sits above its own pivot."""
        return self.baked_deck_z * self.height_scale

    def lip_world_y(self, pivot_y, seaward):
        """``lip = pivot + NineMileCreekQuayFace.LipRiseFromPivot(seaward)``, the Y half of it.

        Two terms at DIFFERENT scales, which is the whole of the arithmetic: the pack's datum rise is
        HEIGHT (0.766 a metre) and the lip's half-width offset is PLAN (0.643 in Y, and none at all
        in X). On the north wall — seaward due south — that is 84.6235 + 3.9834 − 1.6070 = 87.0000,
        which is the wall's own plan lip and the line #765 cut the sea at.
        """
        half = self.face_course_width * 0.5
        return pivot_y + self.pack_datum_rise + half * seaward[1] * self.ground_depth_scale

    def underfoot(self, lip, seaward):
        """Where ``NineMileCreekDressing.FaceLipElevation`` samples the ground: the piece's footprint
        centre, ``lip − seaward × half``. Half a course in from the lip, because the lip is the very
        edge of a 5 m step and a sample taken there sits in the fill's own falloff."""
        half = self.face_course_width * 0.5
        return (lip[0] - seaward[0] * half, lip[1] - seaward[1] * half)

    def foot_elevation(self, lip_elevation):
        """Where this piece's drawn feet stop, in metres above datum — the lip less the 6.60 m of
        height the sheet draws below it.

        ⚠ **Per piece, never a constant.** ``springLow + BakedRigMudZ`` (−2.2 + −1.4 = −3.6) is the
        same number for a run whose deck stands at ``springLow + BakedDeckZMetres``, which the quay's
        3.00 m does exactly — but the breakwater's crest is 3.40 and the identity is 0.40 m out
        there. That is the same 0.40 a constant lip would have drawn the arm's waterline wrong by
        "for as long as nobody looked" (``FaceLipElevation``'s own warning), so the foot is derived
        from the piece's own lip.
        """
        return lip_elevation - self.drawn_face_height


# --- which pieces are faces, and which way they look --------------------------------------------

def plan_direction_of(heading_degrees):
    """``NineMileCreekDressing.PlanDirectionOf`` — ``(sin θ, cos θ)`` for a compass heading."""
    radians = math.radians(heading_degrees)
    return (math.sin(radians), math.cos(radians))


def draws_a_face_at_this_camera(seaward):
    """``NineMileCreekQuayFace.DrawsAFaceAtThisCamera`` — ``seaward.y < −1e-3``, and nothing else.

    False for every north–south run, and that is not a technicality: screen X is world X, so a wall
    at constant X projects to a line and has no drawn face for the sea to climb. What such a course
    contributes below its lip is its own southern deck edge and its built end, standing at their own
    plan northings — facings 2 and 6 draw 4.156 units under the pivot against facing 4's 2.625, and
    the extra is PLAN. Cutting those by a world-Y line would eat the apron's deck.
    """
    x, y = seaward
    length = math.hypot(x, y)
    if length < 1e-6:
        return False
    return y / length < -1e-3


def heading_of_facing(facing_index, facings, counter_clockwise):
    """Invert ``IsoPackSprites.FacingForHeading`` — which cell depicts which compass heading.

    The forward rule is ``step = round(heading × facings / 360); facing = wrap(∓step, facings)``, so
    a cell's heading is ``∓facing × 360 / facings``. The sign is the pack's registered convention and
    is READ from the contract, never assumed: every directional family in the wharf pack is
    registered counter-clockwise while its ``order`` array reads clockwise, and that exact mislabel
    has shipped defects in five kits. Cell 4 of an 8-facing counter-clockwise family is therefore
    heading 180° — due south — which is what ``logCrib_4`` is and why it draws a face.
    """
    step = -facing_index if counter_clockwise else facing_index
    return (step * 360.0 / facings) % 360.0


def facing_index_of_sprite(sprite_name):
    """The trailing slice index of ``<stem>_<index>``, the name every slicer writes. ``None`` when the
    name does not carry one — a sheet whose cells are not a facing turntable."""
    if not sprite_name or "_" not in sprite_name:
        return None
    tail = sprite_name.rsplit("_", 1)[1]
    return int(tail) if tail.isdigit() else None


# --- the blocks the package ships ---------------------------------------------------------------

def rules(terms):
    """``x-tideRules`` — the three laws as strings plus the one number they all turn on.

    Given whole so a reader never has to re-derive a law from prose, and given as STATIC TEXT so the
    package stays byte-identical for a commit: there is no time in here and no evaluated tide.
    """
    return {
        "heightScale": round(terms.height_scale, 6),
        "groundDepthScale": round(terms.ground_depth_scale, 6),
        "seaLevel": "seaLevel = terrain.waterLevelMeters + terrain.x-tide.amplitudeMeters * carrier, "
                    "carrier in [-1, 1]. Spring low is carrier -1, mean 0, spring high +1.",
        "water": "water is wherever groundElevation < seaLevel. Threshold terrain.x-heightFieldFull "
                 "(or the coarser x-heightField) at seaLevel and what is below it is sea.",
        "faceWaterline": "waterlineWorldY = x-tidalFace.lipWorldY + (seaLevel - "
                         "x-tidalFace.lipElevation) * heightScale. Rows of the face BELOW that world "
                         "y are under water and are not drawn.",
        "hullWaterline": "waterline = max(seaLevel - x-tidalRide.draughtMetres, "
                         "x-tidalRide.bedElevation) + x-tidalRide.draughtMetres. Afloat that is "
                         "seaLevel; aground she stops falling and the water keeps going without her.",
        "hullScreenRise": "screenRise = (waterline - x-tidalRide.bakedWaterlineElevation) * "
                          "heightScale, ADDED to the picture's screen y. Her PLAN never moves.",
        "x-heightScaleSource": "IsoGround.HeightScale = cos(CameraElevationDegrees), "
                               f"CameraElevationDegrees = {_plain(terms.camera_elevation_deg)}. "
                               "Height is NOT depth: a metre north draws groundDepthScale = sin of "
                               "the same angle, 19% less, and running a height through it lands "
                               "short. TidalRide, TidalFaceWaterline and the float all use the "
                               "cosine.",
        "x-note": "DECLARED TERMS, never an evaluated tide. The exporter has no clock and rule 5 "
                  "says the tide is recomputed from (worldSeed, gameTime) and never stored, so the "
                  "carrier is the reader's to choose. These strings are static: they cannot move "
                  "the package's bytes between two runs on one commit.",
        "x-sources": {
            "water": "MainlandTidalTerrain / TidalTerrain (ITidalTerrain.ElevationAt) against "
                     "TideModel.Height",
            "faceWaterline": "HiddenHarbours.Art.TidalFaceWaterline.WaterlineWorldY (#765)",
            "hullWaterline": "HiddenHarbours.Core.TidalRide.Waterline (#753)",
            "hullScreenRise": "HiddenHarbours.Core.TidalRide.ScreenRise (#753)",
        },
    }


def face_block(terms, pivot_y, seaward, lip_elevation, lip_elevation_source, sample=None):
    """``x-tidalFace`` for one drawn course — everything ``WaterlineWorldY`` needs and nothing else."""
    lip_y = terms.lip_world_y(pivot_y, seaward)
    block = {
        "lipWorldY": _round(lip_y),
        "lipElevation": _round(lip_elevation),
        "footElevation": _round(terms.foot_elevation(lip_elevation)),
        "heightScale": round(terms.height_scale, 6),
        "x-lipWorldYFrom": "the placed pivot plus NineMileCreekQuayFace.LipRiseFromPivot: pivotY + "
                           f"PackDatumRise ({_plain(terms.pack_datum_rise)}) + half a course "
                           f"({_plain(terms.face_course_width * 0.5)} m) * seaward.y * "
                           "groundDepthScale. Height and plan at DIFFERENT scales.",
        "x-lipElevationFrom": lip_elevation_source,
        "x-footElevationFrom": "lipElevation - (BakedDeckZMetres - BakedRigMudZ) = lipElevation - "
                               f"{_plain(terms.drawn_face_height)} m of drawn height. Equal to "
                               "springLow + BakedRigMudZ only where the deck stands at springLow + "
                               "BakedDeckZMetres, which the quay does and the breakwater's crest "
                               "does not.",
        "x-seaward": [_round(seaward[0]), _round(seaward[1])],
    }
    if sample is not None:
        block["x-heightMapSample"] = sample
    return block


def hull_block(terms, draught_metres, bed_elevation, draught_source, bed_source,
               baked_waterline_elevation=0.0, baked_source=None):
    """``x-tidalRide`` for one hull — ``Waterline`` then ``ScreenRise``, in that order.

    ``bakedWaterlineElevation`` is the elevation the art was DRAWN at. Every hull in both shipped
    scenes is drawn at mean water, which on both regions is 0.0 m above chart datum, so the rise is
    the whole of the waterline. It ships as a field rather than as an assumption because a hull baked
    at any other level would otherwise ride from the wrong line and look merely "a bit high".
    """
    return {
        "bakedWaterlineElevation": _round(baked_waterline_elevation),
        "draughtMetres": _round(draught_metres),
        "bedElevation": None if bed_elevation is None else _round(bed_elevation),
        "heightScale": round(terms.height_scale, 6),
        "x-draughtFrom": draught_source,
        "x-bedFrom": bed_source,
        "x-bakedWaterlineFrom": baked_source or
            "0 - HullTideRide._bakedWaterlineElevation's own default, and its tooltip says what "
            "that means: 'a builder that places a berth as a plain plan point (every one of them "
            "does) has drawn her as she floats at mean tide'. Chart datum IS mean water on both "
            "shipped regions (RegionDef.TideMeanLevel = 0), so her picture is correct where her "
            "plan point puts her and the whole waterline is the rise. A region whose plan lines "
            "were struck at some other state of tide would say so on the component.",
        "x-agroundWhen": "seaLevel - draughtMetres < bedElevation (TidalRide.IsAground). A null "
                         "bedElevation is open water with no height map under it: she can never "
                         "take the ground, which is the honest reading and not a zero.",
    }


# --- formatting ---------------------------------------------------------------------------------

def _round(value):
    """Six decimals, and an integer where the value is one — matching ``package._num``'s shape so a
    tide field and a position read alike in the file."""
    if value is None:
        return None
    rounded = round(float(value), 6)
    if rounded == int(rounded):
        return int(rounded)
    return rounded


def _plain(value):
    """A number for prose: no trailing zeros, no exponent, so a note reads like a note."""
    if value is None:
        return "?"
    text = f"{float(value):.4f}".rstrip("0").rstrip(".")
    return text or "0"


# --- stamping the scene =========================================================================

# The face placement's own root names, so "which entities are quay face" is a READ of the builder
# rather than a path pattern of ours. NineMileCreekDressing.PlaceFace parents every course under
# `RootName/FaceRootName` and names it `{Wall}_{Key}_{n}` — the wall is the run, and the run is what
# decides which deck the course stands on.
FACE_ROOT_OWNER = DRESSING
FACE_ROOT_CONSTANTS = ("RootName", "FaceRootName")

# Which declared deck each run stands on. The runs are named by their own C# constants (NorthWallRun,
# WestWallRun, BreakwaterRun …), read below; the ELEVATION each stands at is the terrain constant the
# region filled that ground to. Six runs stand on three decks, and a constant lip would draw the
# arm's waterline 0.40 m out — which is exactly what FaceLipElevation refuses to do.
_RUN_DECKS = {
    "NorthWallRun": "WharfDeckElevation",
    "WestWallRun": "WharfDeckElevation",
    "ApronWestRun": "WharfDeckElevation",
    "ApronSouthRun": "WharfDeckElevation",
    "QuayHeadRun": "WharfDeckElevation",
    "BreakwaterRun": "BreakwaterCrestElevation",
}

# The pack's facing turntable. Defaults are the wharf ISO pack's registered values; `facing_convention`
# re-reads them from the committed contract, because the pack's `order` array reads CLOCKWISE while
# every directional family in it is registered COUNTER-clockwise, and that exact mislabel has shipped
# defects in five separate kits. Assuming it here would be the sixth.
WHARF_CONTRACT = "Assets/_Project/Art/Sprites/Wharf/Iso/wharfIsoRig.contract.json"
DEFAULT_FACINGS = 8
DEFAULT_CONVENTION = "counterClockwise"


def facing_convention(repo):
    """``{facings, convention, counterClockwise, source}`` from the wharf pack's committed contract.

    Falls back to the pack's registered values when the contract is absent, and says which it used,
    so a checkout without the contract still exports and the package still states what it assumed.
    """
    import json
    out = {
        "facings": DEFAULT_FACINGS,
        "convention": DEFAULT_CONVENTION,
        "source": None,
        "x-note": "IsoPackSprites.FacingForHeading reads azimuth.convention from the pack's own "
                  "contract and never assumes it; so does this. The sheet's order array reads "
                  "clockwise (N NE E SE S SW W NW) while the family is registered counter-clockwise, "
                  "which means cell i depicts heading -45*i.",
    }
    if repo.exists(WHARF_CONTRACT):
        with open(repo.abs(WHARF_CONTRACT), encoding="utf-8", errors="replace") as handle:
            data = json.load(handle)
        facings = data.get("facings")
        if isinstance(facings, int) and facings > 0:
            out["facings"] = facings
        convention = (data.get("azimuth") or {}).get("convention")
        if convention:
            out["convention"] = convention
        out["source"] = WHARF_CONTRACT
    out["counterClockwise"] = out["convention"].lower() == "counterclockwise"
    return out


def face_placement(repo):
    """``(hierarchyPrefix, {wallName: deckConstant})`` — where the faces are and what they stand on.

    Both halves are read: the prefix out of ``NineMileCreekDressing``'s two root constants, and each
    run's own name constant out of the same file. A ``None`` prefix is a region with no face
    placement, which is a fact about the region rather than a gap in the export.
    """
    source = _source(repo, FACE_ROOT_OWNER)
    if source is None:
        return None, {}
    parts = [source.string(name) for name in FACE_ROOT_CONSTANTS]
    if not all(parts):
        return None, {}
    walls = {}
    for constant, deck in _RUN_DECKS.items():
        name = source.string(constant)
        if name:
            walls[name] = deck
    return "/".join(parts) + "/", walls


def stamp_faces(terms, entities, prefix, walls, centre, sampler, deck_elevations, convention):
    """Give every placed course its ``x-tidalFace``, or a null and the reason it draws whole.

    Returns the tally the package reports. A course that resolves no lip elevation is NEVER given a
    zero: it carries ``null`` and says why, because a face cut at chart datum draws the whole wall
    under water at every tide and reads as a decision somebody made.
    """
    tally = {"pieces": 0, "cut": 0, "whole": 0, "unresolved": 0, "runs": {}}
    if prefix is None:
        return tally
    facings = convention["facings"]
    widdershins = convention["counterClockwise"]
    for record in entities:
        path = record.get("x-path") or ""
        if not path.startswith(prefix):
            continue
        wall = (record.get("x-name") or "").split("_", 1)[0]
        run = tally["runs"].setdefault(wall, {"pieces": 0, "cut": 0, "whole": 0})
        run["pieces"] += 1
        tally["pieces"] += 1

        facing = facing_index_of_sprite((record.get("x-sprite") or {}).get("name"))
        # ⚠ IN RANGE, not merely present. `heading_of_facing` takes a modulo, so a trailing slice
        # index off a sheet that is NOT a facing turntable — St Peters' `WharfAtlas_25` is one —
        # would wrap to a perfectly plausible heading and cut a wall by it. Out of range means the
        # sheet does not answer the question, which is not the same as answering it.
        if facing is not None and not 0 <= facing < facings:
            _leave_whole(record, tally, run, "unresolved",
                         f"this piece's sprite is cell {facing} of a sheet whose pack declares "
                         f"{facings} facings, so its trailing index is a slice number and not a "
                         "facing. Which way its face looks is therefore not stated, and no row of "
                         "it can be read as an elevation.")
            continue
        seaward = (None if facing is None
                   else plan_direction_of(heading_of_facing(facing, facings, widdershins)))
        if seaward is None:
            _leave_whole(record, tally, run, "unresolved",
                         "this piece's sprite name carries no trailing facing index, so which way "
                         "its face looks is not stated by the scene and no row of it can be read "
                         "as an elevation.")
            continue
        if not draws_a_face_at_this_camera(seaward):
            _leave_whole(record, tally, run, "whole",
                         f"a north-south run: seaward is {_compass(seaward)}, so this course has no "
                         "drawn face at this camera and #765 leaves it whole "
                         "(NineMileCreekQuayFace.DrawsAFaceAtThisCamera). What it draws below its "
                         "lip is its own southern deck edge and its built end, standing at their "
                         "own plan northings - facings 2 and 6 draw 4.156 units under the pivot "
                         "against facing 4's 2.625, and the extra is PLAN. A world-y cut here "
                         "would eat the apron's deck.")
            continue

        constant = walls.get(wall)
        lip_elevation = deck_elevations.get(constant) if constant else None
        if lip_elevation is None:
            _leave_whole(record, tally, run, "unresolved",
                         f"this course draws a face, but the run '{wall}' resolves to no declared "
                         "deck elevation in NineMileCreekMainland, so the height of its lip above "
                         "datum is not stated here. Left null rather than zero: a face cut at chart "
                         "datum draws the whole wall under water at every tide.")
            continue

        pivot_y = record["pos"][1] + centre[1]
        lip_y = terms.lip_world_y(pivot_y, seaward)
        record["x-tidalFace"] = face_block(
            terms, pivot_y, seaward, lip_elevation,
            f"NineMileCreekMainland.{constant} - the elevation this region's terrain is FILLED to "
            f"under the '{wall}' run. The game measures the lip off the authored terrain "
            "(NineMileCreekDressing.FaceLipElevation -> ITidalTerrain.ElevationAt), which is "
            "MainlandTidalTerrain's zone composition and cannot run outside Unity; this constant is "
            "what that composition fills the ground to, and x-heightMapSample is the committed "
            "raster of the same terrain read at the same point.",
            _lip_sample(terms, record, lip_y, seaward, centre, sampler, lip_elevation))
        tally["cut"] += 1
        run["cut"] += 1
    return tally


def _leave_whole(record, tally, run, bucket, reason):
    record["x-tidalFace"] = None
    record["x-tidalFaceNote"] = reason
    tally[bucket] += 1
    run["whole"] += 1


def _lip_sample(terms, record, lip_y, seaward, centre, sampler, declared):
    """The committed height map read where the game reads the terrain — a cross-check, not a source."""
    if sampler is None or not sampler.ready:
        return None
    spot = terms.underfoot((record["pos"][0] + centre[0], lip_y), seaward)
    metres = sampler.at(*spot)
    return {
        "atWorld": [_round(spot[0]), _round(spot[1])],
        "metres": metres,
        "quantumMeters": round(sampler.quantum, 6),
        "agreesWithDeclared": (metres is not None
                               and abs(metres - declared) <= sampler.quantum),
        "x-note": "the committed height map read at the SAME footprint centre "
                  "NineMileCreekDressing.FaceLipElevation samples the analytic terrain at - half a "
                  "course inboard of the lip, clear of the fill's falloff. A CROSS-CHECK, not the "
                  "source: the map is a raster of that same terrain "
                  "(TerrainPaintTool.BakeNineMileCreekSeabed) and carries its own 8-bit "
                  "quantisation, so agreement is only ever meaningful to within one quantum.",
    }


_COMPASS = ((0.0, "north"), (90.0, "east"), (180.0, "south"), (270.0, "west"))


def _compass(direction):
    """The nearest cardinal name for a plan direction — for a note, never for arithmetic."""
    heading = math.degrees(math.atan2(direction[0], direction[1])) % 360.0
    def gap(point):
        raw = abs(heading - point[0])
        return min(raw, 360.0 - raw)
    return min(_COMPASS, key=gap)[1]


# --- the hulls ==================================================================================
# ⚠ A hull in these two scenes is USUALLY NOT AN ENTITY. `MooredBoat` is a drawer: it builds its
# picture at runtime from the owner's hull def, so the seven boats lying at Nine Mile Creek's wall
# and the harbour float carry no SpriteRenderer in the committed scene and the entity walk — which
# is a walk of sprites — cannot see one of them. They are still placed, declared objects with a
# draught and a bed, and they are exactly what the owner asked to watch ride the tide. So the tide
# blocks are collected from the SCENE, shipped whole under `x-tidalHulls`, and stamped onto the
# entity of any hull that does happen to draw one (St Peters' dory does).

# What a placed hull IS, for a reader that wants the working harbour and not the display lineup.
_KINDS = {
    "MooredBoat.cs": "moored",
    "FloatingPlatform.cs": "float",
    "BoatController.cs": "piloted",
}

MOORED_BOAT = "MooredBoat.cs"
FLOATING_PLATFORM = "FloatingPlatform.cs"
BOAT_CONTROLLER = "BoatController.cs"
HULL_TIDE_RIDE = "HullTideRide.cs"
HULL_COMPONENTS = (MOORED_BOAT, FLOATING_PLATFORM, BOAT_CONTROLLER)


def collect_hulls(repo, scene, centre, terms, sampler):
    """Every declared hull in one scene, each with the ``x-tidalRide`` block a reader needs.

    Ordered by the scene's own walk, like the entities, so the list is stable across runs.
    """
    from . import unityyaml as U

    hulls = []
    for game_object in scene.walk():
        found = {}
        for component in scene.components_of(game_object):
            if component.type_name != "MonoBehaviour":
                continue
            guid = U.ref_guid(component.data.get("m_Script"))
            path = repo.path_for_guid(guid) if guid else None
            if path:
                found[os.path.basename(path)] = component
        kind = next((name for name in HULL_COMPONENTS if name in found), None)
        if kind is None:
            continue

        x, y = scene.world_of_game_object(game_object)[:2]
        ride, identity = _ride_for(repo, terms, sampler, kind, found, (x, y))
        hulls.append({
            "x-path": scene.hierarchy_path(game_object),
            "x-name": scene.name_of(game_object),
            "x-component": kind[:-3],
            "pos": [_round(x - centre[0]), _round(y - centre[1])],
            "x-active": scene.active_in_hierarchy(game_object),
            "x-tidalRide": ride,
            **identity,
        })
    return hulls


def _ride_for(repo, terms, sampler, kind, found, position):
    """``(x-tidalRide, identityFields)`` for one placed hull, by what the component declares."""
    from . import unityyaml as U

    component = found[kind]
    identity = {}
    draught = bed = None
    draught_source = bed_source = None

    identity["x-kind"] = _KINDS[kind]

    if kind == FLOATING_PLATFORM:
        # The float states all three of her own numbers, so nothing here is resolved or sampled.
        # She is the one hull in either scene whose bed was measured at BUILD time and kept.
        draught = U.as_float(component.data.get("_draughtMetres"), None)
        bed = U.as_float(component.data.get("_bedElevation"), None)
        draught_source = (f"{FLOATING_PLATFORM[:-3]}._draughtMetres, serialized on the placement. "
                          "Derived at build time from the rig's own drawing: "
                          "NineMileCreekQuayFace.BakedRigFloatDraughtMetres = hull depth less "
                          "freeboard, so a re-bake that deepens her billets grounds her earlier.")
        bed_source = (f"{FLOATING_PLATFORM[:-3]}._bedElevation, serialized on the placement — the "
                      "ground measured under her at build time. Preferred over a height-map sample "
                      "because it is what FloatingPlatform itself reads.")
        identity["x-id"] = component.data.get("_id")
        identity["x-freeboardMetres"] = _round(U.as_float(component.data.get("_freeboardMetres"), None))

    elif kind == MOORED_BOAT:
        owner_guid = U.ref_guid(component.data.get("_owner"))
        visual_guid = U.ref_guid(component.data.get("_reviewVisual"))
        if not owner_guid and visual_guid:
            # A REVIEW hull, and her draught is missing BY DESIGN rather than by omission.
            # MooredBoat's own remarks say so: the fleet-review lineup names a BoatVisualDef because
            # hull defs are a later phase deliberately, and "fabricating owners would have meant
            # fabricating hull defs, i.e. building the next phase early to satisfy a display". The
            # game agrees — MooredBoat.Owner is null there, so HullTideRide.DraughtMetres returns 0
            # and she never takes the ground. Saying "unresolved" would report a defect where there
            # is a decision, which is the same distinction resolutionExcluded draws for the fittings.
            identity["x-kind"] = "review"
            identity["x-reviewVisual"] = repo.path_for_guid(visual_guid)
            review_bed, review_bed_source = _bed_of(sampler, position)
            return (hull_block(
                terms, None, review_bed,
                "none: this is a fleet-review hull. She names a BoatVisualDef and no BoatOwnerDef, "
                "and MooredBoat's remarks say review hulls carry no BoatHullDef on purpose - hull "
                "defs are a later phase. The repo therefore states no draught for her, and "
                "HullTideRide would read 0 the same way. Null, never zero: a zero-draught hull "
                "never takes the ground, which is a claim, and this is an absence.",
                review_bed_source), identity)
        owner_rel = repo.path_for_guid(owner_guid) if owner_guid else None
        owner = _asset(repo, owner_rel)
        hull_guid = U.ref_guid(owner.get("Boat")) if owner else None
        hull_rel = repo.path_for_guid(hull_guid) if hull_guid else None
        hull = _asset(repo, hull_rel)
        if hull:
            draught = U.as_float(hull.get("DraughtMeters"), None)
            draught_source = (f"{hull_rel}#DraughtMeters, reached the way HullTideRide reaches it: "
                              f"MooredBoat._owner -> BoatOwnerDef.Boat -> BoatHullDef.")
            identity["x-hull"] = {"id": hull.get("Id"), "asset": hull_rel}
        else:
            draught_source = ("unresolved: MooredBoat._owner names no BoatOwnerDef with a Boat this "
                              "checkout can read, so her draught is not stated rather than zero — a "
                              "hull with a zero draught never takes the ground.")
        if owner:
            identity["x-owner"] = {"id": owner.get("Id"), "asset": owner_rel}
        heading = U.as_float(component.data.get("_headingDegrees"), None)
        if heading is not None:
            identity["x-headingDegrees"] = _round(heading)

    else:   # BOAT_CONTROLLER — a piloted hull, the way HullTideRide asks a BoatController for one
        hull_guid = U.ref_guid(component.data.get("_hull"))
        hull_rel = repo.path_for_guid(hull_guid) if hull_guid else None
        hull = _asset(repo, hull_rel)
        if hull:
            draught = U.as_float(hull.get("DraughtMeters"), None)
            draught_source = (f"{hull_rel}#DraughtMeters, reached the way HullTideRide reaches it: "
                              "BoatController.Hull -> BoatHullDef.")
            identity["x-hull"] = {"id": hull.get("Id"), "asset": hull_rel}
        else:
            draught_source = ("unresolved: BoatController._hull names no BoatHullDef this checkout "
                              "can read, so her draught is not stated rather than zero.")

    # A HullTideRide on the object overrides both, and it is the authored answer where one exists.
    # None is placed in either committed scene — both were banked before #753 — but a re-bank will
    # bring them, and a reader must get the authored numbers rather than these resolved ones.
    baked = 0.0
    baked_source = None
    ride_component = found.get(HULL_TIDE_RIDE)
    if ride_component is not None:
        baked = U.as_float(ride_component.data.get("_bakedWaterlineElevation"), 0.0)
        baked_source = (f"{HULL_TIDE_RIDE[:-3]}._bakedWaterlineElevation, serialized on the "
                        "placement — the elevation her art was drawn at.")
        override = U.as_float(ride_component.data.get("_draughtOverrideMetres"), -1.0)
        if override is not None and override >= 0.0:
            draught = override
            draught_source = (f"{HULL_TIDE_RIDE[:-3]}._draughtOverrideMetres, which the component "
                              "prefers over the hull she is.")

    if bed is None:
        bed, bed_source = _bed_of(sampler, position)

    return hull_block(terms, draught, bed, draught_source, bed_source, baked, baked_source), identity


def _bed_of(sampler, position):
    """``(metres, why)`` for the ground under a hull — the height map at her plan point.

    The same read ``HullTideRide.BedElevation`` makes against ``ITidalTerrain``, on a raster of that
    terrain. ``None`` is the honest answer both when the bytes are absent and when she floats off
    the painted map: ``TidalRide`` treats no-bottom as "she can never take the ground", and an
    elevation of zero would instead ground her at every tide.
    """
    if sampler is not None and sampler.ready:
        metres = sampler.at(*position)
        if metres is not None:
            return metres, ("the committed height map at her plan point - the same read "
                            "HullTideRide.BedElevation makes against ITidalTerrain, on a raster of "
                            f"that terrain, quantised to {round(sampler.quantum, 6)} m.")
        return None, ("her plan point falls outside the painted height map, so there is no bottom "
                      "stated under her. TidalRide's reading of that is open water: she can never "
                      "take the ground. It is NOT an elevation of zero.")
    reason = sampler.reason if sampler is not None else "no height map for this region"
    return None, (f"not sampled: {reason}. Null means no bottom under her - open water, never "
                  "aground - and NOT an elevation of zero.")


def _asset(repo, rel):
    """The first document of a committed ``.asset``, as a mapping. ``None`` when it is not there."""
    if not rel or not repo.exists(rel):
        return None
    from . import unityyaml as U
    docs = U.parse_file(repo.abs(rel))
    return docs[0].data if docs else None


def stamp_hulls(entities, hulls):
    """Put each hull's ride on the entity that IS it, and point its pictures at that entity.

    A hull's descendants are separate flat entities here — the export carries world positions, not a
    live hierarchy — so a picture parented under a hull would otherwise not know it rides. It gets
    ``x-tidalRideOf`` naming the hull rather than a copy of the block: one fact, one place, and a
    reader that applies a rise twice would lift a dory's oars off her.
    """
    by_path = {hull["x-path"]: hull for hull in hulls if hull.get("x-path")}
    stamped = 0
    inherited = 0
    for record in entities:
        path = record.get("x-path")
        if not path:
            continue
        if path in by_path:
            record["x-tidalRide"] = by_path[path]["x-tidalRide"]
            stamped += 1
            continue
        owners = [p for p in by_path if path.startswith(p + "/")]
        if owners:
            owner = max(owners, key=len)
            record["x-tidalRideOf"] = owner
            inherited += 1
    return {"entitiesWithRide": stamped, "entitiesRidingWithAHull": inherited}
