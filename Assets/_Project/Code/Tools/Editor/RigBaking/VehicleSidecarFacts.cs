using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>The per-artwork facts a vehicle bake cannot find in a face list</b> — where the driver
    /// stands to open the door, where she is sat, what is solid, and (for an amphibian) what floats.
    ///
    /// <para><b>Why these are READ rather than typed.</b> Until 2026-08-27 they were typed straight
    /// onto the baked assets, and the comment on
    /// <c>AmphibiousVehicleTests.HerBakedMeshCarriesTheFlotationAndDoorHerSidecarPublishes</c> says
    /// exactly why that needed a test: <i>"they are typed onto the asset, and typed numbers drift."</i>
    /// Eight more bodies is eight more chances to drift, so the bake reads the same document the test
    /// pins it against and there is one number, not two.</para>
    ///
    /// <para><b>Every "absent" here is an ANSWER, not a gap.</b> A towed body publishes no
    /// <c>drive</c> interaction because she is dragged; a hard-cab truck publishes no open seat
    /// because a figure drawn in her would be standing on the roofline; a road vehicle publishes no
    /// <c>FLOAT</c> block because she sinks. Each comes back as a flag beside the value, so the baker
    /// writes a DELIBERATE zero rather than leaving a field nobody looked at — which is the Otter's
    /// trap inverted, and it is the whole reason this type reports what it did not find.</para>
    ///
    /// <para>⚠️ <b>A sidecar whose hash does not pin its rig must never reach this class.</b> Its
    /// thresholds, colliders and seats describe some other shape. <see cref="Read"/> refuses on an
    /// empty document rather than returning empty facts, and
    /// <c>VehicleMeshAssetBaker</c> refuses outright for anything in
    /// <see cref="VehicleRigFleet.SidecarHashRefused"/> before it gets this far.</para>
    /// </summary>
    public sealed class VehicleSidecarFacts
    {
        public string SidecarPath = "";

        /// <summary>The body inside a CONTAINER sidecar these facts came from, or empty for a
        /// sidecar that describes one machine. See <see cref="Read"/>.</summary>
        public string BodyScope = "";

        /// <summary>Where the driver stands to open her door, rig metres (x, y) — the
        /// <c>INTERACT</c> entry with <c>id: "drive"</c> and its <c>reach_point</c>.</summary>
        public bool HasDriveDoor;
        public Vector2 DriveDoorLocal;

        /// <summary>Where the driver sits AND IS SEEN, rig metres — published only by a machine
        /// whose <c>drive</c> interaction happens AT a seat (see <see cref="Read"/>).</summary>
        public bool HasDriverSeat;
        public Vector3 DriverSeatLocal;

        /// <summary>The art's own solid box — <c>BODY.collider_bbox</c>.</summary>
        public bool HasCollider;
        public Vector3 ColliderMin, ColliderMax;

        /// <summary>The four flotation numbers, present only on a machine with a <c>FLOAT</c>
        /// block. All four or none: a partial set is a refusal, because
        /// <c>VehicleMeshDef.Floats</c> is an AND of two of them and a half-read machine is one
        /// that drives into the water and never floats.</summary>
        public bool HasFlotation;
        public float FloatSinkMeters, FloatDraftMeters;
        public float WatertightHalfBeamMeters, WatertightDeckHeightMeters;

        /// <summary>
        /// ⭐ <b>Every INTERACT id the sidecar publishes, and the reach point of each</b> — the
        /// handles the art drew, so a declared door group can be resolved to the place its own
        /// document says to stand rather than to a number in a table.
        ///
        /// <para>An id present here with NO entry in <see cref="ReachPoints"/> is one whose
        /// <c>reach_point</c> is not a numeric point. That is a real case, not a parse failure: the
        /// trailer kit's <c>couple</c> entry carries prose there — <i>"the ACT is the tractor
        /// backing on"</i> — because the act belongs to the other vehicle. Reading it as (0,0) would
        /// hang a handle on the machine's own origin.</para>
        /// </summary>
        public readonly List<string> InteractIds = new List<string>();
        public readonly Dictionary<string, Vector2> ReachPoints =
            new Dictionary<string, Vector2>(StringComparer.Ordinal);

        /// <summary>What was absent and why that is the right answer — logged by the bake so a
        /// deliberate zero reads as one in the report rather than as silence.</summary>
        public readonly List<string> Absences = new List<string>();

        /// <summary>Anything that stops the bake. Non-empty = refuse; never bake past one.</summary>
        public readonly List<string> Errors = new List<string>();

        // =============================================================================================

        /// <summary>
        /// Read one machine's facts out of a gameplay sidecar.
        /// </summary>
        /// <param name="json">the sidecar document.</param>
        /// <param name="sidecarPath">repo-relative, for messages.</param>
        /// <param name="bodyScope">
        /// ⚠️ <b>Which body, on a CONTAINER sidecar.</b> The trailer set ships ONE sidecar for FOUR
        /// towed bodies and puts each one's <c>BODY</c> under <c>bodies.&lt;pick&gt;</c>, so a reader
        /// that always looked at the root would hand every trailer the same collider — the
        /// <c>(file, pick)</c> trap in the one place where being wrong is invisible (a 28 ft pup with
        /// a 53 ft box is still a box). Empty = the root, which is every single-body sidecar.
        /// </param>
        public static VehicleSidecarFacts Read(string json, string sidecarPath, string bodyScope = null)
        {
            var facts = new VehicleSidecarFacts
            {
                SidecarPath = sidecarPath ?? "",
                BodyScope = bodyScope ?? "",
            };

            object root;
            try { root = DeckSidecarJson.Parse(json); }
            catch (Exception e)
            {
                facts.Errors.Add($"unreadable JSON: {e.Message}");
                return facts;
            }

            if (DeckSidecarJson.AsObject(root) == null)
            {
                facts.Errors.Add("the sidecar's top level is not an object.");
                return facts;
            }

            // ---- the body block: the root, or one body of a container sidecar --------------------
            object body = root;
            if (!string.IsNullOrEmpty(bodyScope))
            {
                object bodies = DeckSidecarJson.Member(root, "bodies");
                body = DeckSidecarJson.Member(bodies, bodyScope);
                if (DeckSidecarJson.AsObject(body) == null)
                {
                    facts.Errors.Add(
                        $"no bodies.{bodyScope} block. This sidecar describes several bodies and the " +
                        "bake asked for one it does not carry — which is the (file, pick) trap: an " +
                        "unknown pick must fail here rather than quietly reading the first body's " +
                        "numbers onto a different trailer.");
                    return facts;
                }
            }

            ReadCollider(facts, body);
            ReadDriveDoorAndSeat(facts, root);
            ReadFlotation(facts, root);
            return facts;
        }

        // ---- the solid box ---------------------------------------------------------------------

        static void ReadCollider(VehicleSidecarFacts facts, object body)
        {
            object bodyBlock = DeckSidecarJson.Member(body, "BODY");
            object bbox = DeckSidecarJson.Member(bodyBlock, "collider_bbox");
            if (DeckSidecarJson.AsObject(bbox) == null)
            {
                facts.Absences.Add("no BODY.collider_bbox — nothing is declared solid.");
                return;
            }

            if (!TryPair(bbox, "x", out float x0, out float x1) ||
                !TryPair(bbox, "y", out float y0, out float y1) ||
                !TryPair(bbox, "z", out float z0, out float z1))
            {
                facts.Errors.Add(
                    "BODY.collider_bbox is present but one of x/y/z is not a two-number range. A " +
                    "half-read box would be a solid volume nobody measured.");
                return;
            }

            facts.ColliderMin = new Vector3(Mathf.Min(x0, x1), Mathf.Min(y0, y1), Mathf.Min(z0, z1));
            facts.ColliderMax = new Vector3(Mathf.Max(x0, x1), Mathf.Max(y0, y1), Mathf.Max(z0, z1));
            facts.HasCollider = facts.ColliderMax.x > facts.ColliderMin.x &&
                                facts.ColliderMax.y > facts.ColliderMin.y &&
                                facts.ColliderMax.z > facts.ColliderMin.z;

            if (!facts.HasCollider)
                facts.Errors.Add(
                    "BODY.collider_bbox has no volume — one of its three ranges is empty or " +
                    "inverted. A zero-sized box at the origin is worse than none: it reads as a " +
                    "collider that is simply somewhere else.");
        }

        // ---- the door, and the seat the door leads to --------------------------------------------

        /// <summary>
        /// ⭐ <b>Whether a machine SHOWS her driver is decided by the art, and this is the
        /// mechanism.</b>
        ///
        /// <para>The <c>drive</c> interaction names what it happens AT. On the Otter that is
        /// <c>"at": "front_bench"</c> — a member of her root <c>SEATS</c> array, so the seat exists,
        /// it is in the open, and its <c>seat_ref</c> is where a fisher is genuinely on screen. On
        /// every truck in the fleet it is <c>"at": "door_l"</c>, and her seats live INSIDE a
        /// <c>CAB</c> block instead: a room with a liner, a roof panel and glass that is opaque at
        /// 32 px/m. A figure drawn there would be standing on the roofline.</para>
        ///
        /// <para>So the rule is read off the documents rather than typed per vehicle: <b>a driver
        /// seat is published exactly when the drive interaction happens at a seat the sidecar lists
        /// in the open.</b> The day a hard-cab amphibian or an open-cab tractor arrives, nothing here
        /// needs to learn about her.</para>
        /// </summary>
        static void ReadDriveDoorAndSeat(VehicleSidecarFacts facts, object root)
        {
            List<object> interact = DeckSidecarJson.AsArray(DeckSidecarJson.Member(root, "INTERACT"));
            if (interact == null)
            {
                facts.Absences.Add("no INTERACT block — no published way in.");
                return;
            }

            object drive = null;
            foreach (object entry in interact)
            {
                string id = DeckSidecarJson.String(DeckSidecarJson.Member(entry, "id"));
                if (string.IsNullOrEmpty(id)) continue;

                facts.InteractIds.Add(id);

                // The reach point, WHERE IT IS ONE. Absent is a fact about the interaction, not a
                // failure to read it — see the doc on ReachPoints.
                List<object> reachArray = DeckSidecarJson.AsArray(
                    DeckSidecarJson.Member(entry, "reach_point"));
                if (reachArray != null && reachArray.Count >= 2 &&
                    DeckSidecarJson.TryDouble(reachArray[0], out double rx) &&
                    DeckSidecarJson.TryDouble(reachArray[1], out double ry))
                    facts.ReachPoints[id] = new Vector2((float)rx, (float)ry);

                if (string.Equals(id, "drive", StringComparison.Ordinal)) drive = entry;
            }

            if (drive == null)
            {
                facts.Absences.Add(
                    "no INTERACT entry with id 'drive' — she is not a machine anybody gets into. " +
                    "That is the right answer for a towed body and the wrong one for a truck.");
                return;
            }

            object reach = DeckSidecarJson.Member(drive, "reach_point");

            // ⚠️ A reach_point is not always a point. The trailer set's `couple` entry carries PROSE
            // there ("under the nose — but the ACT is the tractor backing on"), because the act
            // belongs to the tractor. Reading a string as a point silently yields (0,0), which
            // VehicleDoor treats as "no door published" — a refusal wearing the shape of a value.
            List<object> pt = DeckSidecarJson.AsArray(reach);
            if (pt == null || pt.Count < 2 ||
                !DeckSidecarJson.TryDouble(pt[0], out double dx) ||
                !DeckSidecarJson.TryDouble(pt[1], out double dy))
            {
                facts.Errors.Add(
                    "INTERACT[id=drive].reach_point is not a numeric point. It is where the driver " +
                    "STANDS, so a prose note or a short array cannot be read as one — and reading it " +
                    "as (0,0) would put her inside the cab wall while every test still passed.");
                return;
            }

            facts.HasDriveDoor = true;
            facts.DriveDoorLocal = new Vector2((float)dx, (float)dy);

            // ---- the seat, if the drive interaction happens at one ------------------------------
            string at = DeckSidecarJson.String(DeckSidecarJson.Member(drive, "at")) ?? "";
            List<object> seats = DeckSidecarJson.AsArray(DeckSidecarJson.Member(root, "SEATS"));
            if (seats == null || at.Length == 0)
            {
                facts.Absences.Add(
                    $"the drive interaction happens at '{at}', which is not a seat this sidecar " +
                    "lists in the open — her seats are inside a CAB. She keeps her driver hidden, " +
                    "as every hard-cab machine in the fleet does.");
                return;
            }

            foreach (object seat in seats)
            {
                if (!string.Equals(DeckSidecarJson.String(DeckSidecarJson.Member(seat, "id")), at,
                                   StringComparison.Ordinal)) continue;

                List<object> reference = DeckSidecarJson.AsArray(
                    DeckSidecarJson.Member(seat, "seat_ref"));
                if (reference == null || reference.Count < 3 ||
                    !DeckSidecarJson.TryDouble(reference[0], out double sx) ||
                    !DeckSidecarJson.TryDouble(reference[1], out double sy) ||
                    !DeckSidecarJson.TryDouble(reference[2], out double sz))
                {
                    facts.Errors.Add(
                        $"SEATS['{at}'] has no three-number seat_ref. The drive interaction names " +
                        "this seat, so it is where the driver is drawn — a missing cushion height " +
                        "plants her in the floor pan.");
                    return;
                }

                facts.HasDriverSeat = true;
                facts.DriverSeatLocal = new Vector3((float)sx, (float)sy, (float)sz);
                return;
            }

            facts.Errors.Add(
                $"the drive interaction happens at '{at}', and the sidecar HAS a SEATS array but no " +
                $"seat with that id. One of the two moved; guessing which would put the driver on a " +
                "bench nobody aimed the helm at.");
        }

        // ---- flotation ---------------------------------------------------------------------------

        static void ReadFlotation(VehicleSidecarFacts facts, object root)
        {
            object flotation = DeckSidecarJson.Member(root, "FLOAT");
            if (DeckSidecarJson.AsObject(flotation) == null)
            {
                facts.Absences.Add(
                    "no FLOAT block — she does not swim, and the four flotation numbers are a " +
                    "MEASURED zero rather than a field nobody filled in.");
                return;
            }

            object atFloat = DeckSidecarJson.Member(flotation, "at_float_1");
            object waterline = DeckSidecarJson.Member(flotation, "waterline_polygon_at_float_1");
            object flooding = DeckSidecarJson.Member(
                DeckSidecarJson.Member(flotation, "downflooding"), "lowest_gunwale_point");

            if (!TryNumber(flotation, "sink_m_at_float_1", out float sink) ||
                !TryNumber(atFloat, "draft_above_keel_m", out float draft) ||
                !TryNumber(waterline, "max_half_beam_m", out float halfBeam) ||
                !TryNumber(flooding, "z", out float gunwale))
            {
                facts.Errors.Add(
                    "the FLOAT block is present but one of its four numbers is missing " +
                    "(sink_m_at_float_1, at_float_1.draft_above_keel_m, " +
                    "waterline_polygon_at_float_1.max_half_beam_m, " +
                    "downflooding.lowest_gunwale_point.z). All four or none: Floats is an AND of two " +
                    "of them, so a half-read amphibian is one that drives into the water and never " +
                    "floats — and every test that builds its def in code still passes.");
                return;
            }

            facts.HasFlotation = true;
            facts.FloatSinkMeters = sink;
            facts.FloatDraftMeters = draft;
            facts.WatertightHalfBeamMeters = halfBeam;
            facts.WatertightDeckHeightMeters = gunwale;
        }

        // ---- small readers -----------------------------------------------------------------------

        static bool TryPair(object owner, string key, out float lo, out float hi)
        {
            lo = hi = 0f;
            List<object> pair = DeckSidecarJson.AsArray(DeckSidecarJson.Member(owner, key));
            if (pair == null || pair.Count < 2) return false;
            if (!DeckSidecarJson.TryDouble(pair[0], out double a)) return false;
            if (!DeckSidecarJson.TryDouble(pair[1], out double b)) return false;
            lo = (float)a; hi = (float)b;
            return true;
        }

        static bool TryNumber(object owner, string key, out float value)
        {
            value = 0f;
            if (!DeckSidecarJson.TryDouble(DeckSidecarJson.Member(owner, key), out double d)) return false;
            value = (float)d;
            return true;
        }
    }
}
