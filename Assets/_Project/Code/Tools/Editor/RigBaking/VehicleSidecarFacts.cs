using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
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

        /// <summary>Where the driver stands to get on, rig metres (x, y) — the <c>INTERACT</c>
        /// entry with <c>id: "drive"</c> (a cab) or <c>id: "ride"</c> (a saddle) and its
        /// <c>reach_point</c>. See <see cref="ReadWayIn"/>.</summary>
        public bool HasDriveDoor;
        public Vector2 DriveDoorLocal;

        /// <summary>
        /// ⭐ <b>The OTHER side, where the art publishes one</b> — a saddle's
        /// <c>alt_reach_point</c>.
        ///
        /// <para>A cab has one driver's door and the question does not arise. A machine you sit
        /// astride can be mounted from either side, and the two sides are not equivalent: the
        /// bike's side stand is on the STREET side, which is why her own sidecar says
        /// <c>mount.preferred: "street"</c> and the quad's says <c>"either"</c>. The preferred side
        /// is <see cref="DriveDoorLocal"/>; this is the other one.</para>
        ///
        /// <para>Absent on every cab in the fleet, and that is the answer rather than a gap.</para>
        /// </summary>
        public bool HasAltDriveDoor;
        public Vector2 AltDriveDoorLocal;

        /// <summary>Where the driver sits AND IS SEEN, rig metres — published by a machine whose way
        /// in happens AT a seat the sidecar lists in the open, or at a <c>SADDLE</c>
        /// (see <see cref="ReadWayIn"/>).</summary>
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

        /// <summary>
        /// ⭐ <b>WHICH way in was read</b> — <c>"drive"</c> when the cab arm ran, <c>"ride"</c> when
        /// the saddle arm did, and empty when neither could be found.
        ///
        /// <para>The choice is already made below (<see cref="ReadWayIn"/> takes one arm and
        /// returns); this records it, so <c>VehicleMeshDef.WayInInteractId</c> carries the art's own
        /// word downstream and nothing has to re-derive "is this a saddle" from a shape. Set on the
        /// arm that SUCCEEDS, never on the scan above it: an <c>INTERACT</c> block that lists
        /// <c>ride</c> but publishes prose where its reach point should be is a machine with no way
        /// on, and it must not come out of here claiming to be a saddle.</para>
        /// </summary>
        public string WayInId = "";

        /// <summary>Her fifth wheel, if she tows. Absent is the answer for everything but the two
        /// semis.</summary>
        public bool HasFifthWheel;
        public VehicleFifthWheel FifthWheel;

        /// <summary>Her kingpin and follow inputs, if she is towed.</summary>
        public bool HasKingpin;
        public VehicleKingpin Kingpin;

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
            ReadWayIn(facts, root, body);
            ReadFlotation(facts, Scoped(root, body, "FLOAT"));
            ReadFifthWheel(facts, Scoped(root, body, "TOW"));
            ReadKingpin(facts, Scoped(root, body, "KINGPIN"), bodyScope);
            return facts;
        }

        /// <summary>
        /// ⭐ <b>One named block, looked for in the BODY first and then at the root.</b>
        ///
        /// <para><b>Why the order, and why this is not a free choice.</b> A container sidecar can put
        /// a block at either level and the level means something. The trailer set publishes ONE
        /// <c>INTERACT</c>, one <c>KINGPIN</c> and one <c>GEAR</c> at the root, because all four
        /// bodies really do share them — only <c>BODY</c>, <c>CARGO</c> and <c>THRESHOLD</c> differ.
        /// The ATV pack publishes <c>INTERACT</c>, <c>SADDLE</c> and <c>TOW</c> <b>per body</b>,
        /// because a dirtbike's way on is not a quad's and only the quad has a hitch.</para>
        ///
        /// <para>⚠️ The root-only reader this replaced would have handed the bike the quad's
        /// interactions, or more likely nothing at all — and "nothing at all" is the dangerous half:
        /// <see cref="ReadWayIn"/> records an absent way in as an ABSENCE and returns, so the bake
        /// proceeds with no door, no seat, <c>ShowsDriver</c> false, and nobody able to get on. Not a
        /// refusal; a silent nothing.</para>
        ///
        /// <para>Body-first is safe for every sidecar already committed: their body blocks carry
        /// none of these keys, so each one resolves to the root exactly as before.</para>
        /// </summary>
        static object Scoped(object root, object body, string key)
        {
            if (!ReferenceEquals(body, root))
            {
                object inBody = DeckSidecarJson.Member(body, key);
                if (inBody != null) return WrapAs(key, inBody);
            }
            return root;
        }

        /// <summary>The readers below take an OWNER and ask it for their own key, so a block found in
        /// the body has to be handed back inside something that answers to that key. Cheapest honest
        /// way to say "look here instead" without rewriting five readers' signatures.</summary>
        static object WrapAs(string key, object value) =>
            new Dictionary<string, object>(StringComparer.Ordinal) { [key] = value };

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

        // ---- the way in, and the seat it leads to -------------------------------------------------

        /// <summary>
        /// ⭐ <b>Whether a machine SHOWS her driver is decided by the art, and this is the
        /// mechanism.</b>
        ///
        /// <para>The way-in interaction names what it happens AT, and the answer is read off the
        /// document rather than typed per vehicle. Three shapes occur in the fleet, and all three
        /// are the same question:</para>
        ///
        /// <list type="bullet">
        ///   <item>the Otter's <c>drive</c> happens at <c>"front_bench"</c>, a member of her root
        ///   <c>SEATS</c> array — the seat exists, it is in the open, and its <c>seat_ref</c> is
        ///   where a fisher is genuinely on screen;</item>
        ///   <item>every truck's <c>drive</c> happens at <c>"door_l"</c>, and her seats live INSIDE
        ///   a <c>CAB</c> block: a room with a liner, a roof panel and glass that is opaque at
        ///   32 px/m. A figure drawn there would be standing on the roofline, so she publishes
        ///   none;</item>
        ///   <item>⭐ the ATV pack's <c>ride</c> happens at <c>"SADDLE.seat_ref"</c> — a POINTER
        ///   into the same body block. There is no room to be inside: the way in is a leg over the
        ///   saddle, and a rider is the missing half of the silhouette.</item>
        /// </list>
        ///
        /// <para>⚠️⚠️ <b>The saddle arm is the fix for a SILENT NOTHING, not a widening for its own
        /// sake.</b> Before it, a sidecar with no <c>drive</c> entry recorded
        /// <i>"no INTERACT entry with id 'drive'"</i> as an ABSENCE and returned — so an ATV would
        /// have baked with no door, no seat, <c>ShowsDriver</c> false, and nobody able ever to get
        /// on. Not a refusal: a bake that succeeds and produces a machine that cannot be ridden.
        /// The <c>drive</c> path below is unchanged byte-for-byte, so the trucks and the Otter read
        /// exactly as they did.</para>
        ///
        /// <para>⚠️ A TOWED body publishes neither id, and still gets the absence. That is the right
        /// answer for something that is dragged, and it is why this arm keys on the art's own ids
        /// rather than on "is there any INTERACT at all".</para>
        /// </summary>
        static void ReadWayIn(VehicleSidecarFacts facts, object root, object body)
        {
            object interactOwner = Scoped(root, body, "INTERACT");
            List<object> interact =
                DeckSidecarJson.AsArray(DeckSidecarJson.Member(interactOwner, "INTERACT"));
            if (interact == null)
            {
                facts.Absences.Add("no INTERACT block — no published way in.");
                return;
            }

            object drive = null, ride = null;
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
                else if (string.Equals(id, "ride", StringComparison.Ordinal)) ride = entry;
            }

            // ⚠️ `drive` FIRST and unconditionally. Nothing in the fleet publishes both, and if
            // something ever does, a cab is the more specific claim — but the real reason for the
            // order is that the trucks' path must not become conditional on a second id existing.
            if (drive != null) { ReadCabWayIn(facts, root, drive); return; }
            if (ride != null) { ReadSaddleWayIn(facts, root, body, ride); return; }

            facts.Absences.Add(
                "no INTERACT entry with id 'drive' or 'ride' — she is not a machine anybody gets " +
                "onto or into. That is the right answer for a towed body and the wrong one for a " +
                "truck or a saddle. Published ids: " +
                (facts.InteractIds.Count == 0 ? "none" : string.Join(", ", facts.InteractIds)) + ".");
        }

        /// <summary>The cab arm — a <c>drive</c> interaction, and the open seat it names if it names
        /// one. Unchanged from the reader the trucks and the Otter have always had.</summary>
        static void ReadCabWayIn(VehicleSidecarFacts facts, object root, object drive)
        {
            // ⚠️ A reach_point is not always a point. The trailer set's `couple` entry carries PROSE
            // there ("under the nose — but the ACT is the tractor backing on"), because the act
            // belongs to the tractor. Reading a string as a point silently yields (0,0), which
            // VehicleDoor treats as "no door published" — a refusal wearing the shape of a value.
            if (!TryPointXY(drive, "reach_point", out Vector2 door))
            {
                facts.Errors.Add(
                    "INTERACT[id=drive].reach_point is not a numeric point. It is where the driver " +
                    "STANDS, so a prose note or a short array cannot be read as one — and reading it " +
                    "as (0,0) would put her inside the cab wall while every test still passed.");
                return;
            }

            facts.HasDriveDoor = true;
            facts.DriveDoorLocal = door;
            facts.WayInId = "drive";

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

                if (!TryVector3(seat, "seat_ref", out Vector3 reference))
                {
                    facts.Errors.Add(
                        $"SEATS['{at}'] has no three-number seat_ref. The drive interaction names " +
                        "this seat, so it is where the driver is drawn — a missing cushion height " +
                        "plants her in the floor pan.");
                    return;
                }

                facts.HasDriverSeat = true;
                facts.DriverSeatLocal = reference;
                return;
            }

            facts.Errors.Add(
                $"the drive interaction happens at '{at}', and the sidecar HAS a SEATS array but no " +
                $"seat with that id. One of the two moved; guessing which would put the driver on a " +
                "bench nobody aimed the helm at.");
        }

        /// <summary>
        /// ⭐⭐ <b>The saddle arm — a <c>ride</c> interaction, and it publishes a VISIBLE rider.</b>
        ///
        /// <para>Where a cab may or may not show her driver, a saddle always does: there is nothing
        /// to be inside. The ATV pack's own <c>SADDLE._what</c> says it in as many words —
        /// <i>"THE BAKE CARRIES NO RIDER. These are the mount points the character rig sits on."</i>
        /// So a <c>ride</c> whose <c>at</c> resolves to a three-number reference is
        /// <see cref="HasDriverSeat"/>, which is what makes <c>VehicleMeshDef.ShowsDriver</c> true —
        /// from the sidecar, never from a rule about kinds.</para>
        ///
        /// <para><b><c>at</c> is followed as a PATH, not matched as a string.</b> The pack writes
        /// <c>"at": "SADDLE.seat_ref"</c>, and resolving it means walking the document the way the
        /// document says to. Matching the literal would work today and go quietly wrong the first
        /// time a body names a different point; following the path fails LOUDLY instead, which is
        /// the whole difference.</para>
        ///
        /// <para>⚠️ <b>The reach point is the PREFERRED side and there is a second one.</b> The bike's
        /// stand is on the street side, so her sidecar's <c>mount.preferred</c> is <c>"street"</c>
        /// and a rider mounts over the stand; the quad's is <c>"either"</c>. Taking only the first
        /// would silently make every machine one-sided.</para>
        /// </summary>
        static void ReadSaddleWayIn(VehicleSidecarFacts facts, object root, object body, object ride)
        {
            if (!TryPointXY(ride, "reach_point", out Vector2 near))
            {
                facts.Errors.Add(
                    "INTERACT[id=ride].reach_point is not a numeric point. It is where the rider " +
                    "STANDS to swing a leg over, so a prose note cannot be read as one — and reading " +
                    "it as (0,0) would stand her on the machine she is trying to mount.");
                return;
            }

            facts.HasDriveDoor = true;
            facts.DriveDoorLocal = near;
            facts.WayInId = "ride";

            if (TryPointXY(ride, "alt_reach_point", out Vector2 far))
            {
                facts.HasAltDriveDoor = true;
                facts.AltDriveDoorLocal = far;
            }
            else
            {
                facts.Absences.Add(
                    "INTERACT[id=ride] publishes no numeric alt_reach_point — she is mounted from " +
                    "one side only.");
            }

            // ---- the saddle the ride happens at ------------------------------------------------
            string at = DeckSidecarJson.String(DeckSidecarJson.Member(ride, "at")) ?? "";
            if (at.Length == 0)
            {
                facts.Errors.Add(
                    "INTERACT[id=ride] names no 'at'. A saddle machine has nothing to be inside, so " +
                    "the seat is not optional the way a cab's is: without it she would bake with " +
                    "ShowsDriver false and nobody would ever appear on her.");
                return;
            }

            object target = ResolvePath(at, body, root);
            if (target == null)
            {
                facts.Errors.Add(
                    $"INTERACT[id=ride].at is '{at}', which this sidecar does not carry. The path is " +
                    "followed through the body block and then the root; guessing a seat instead " +
                    "would sit the rider somewhere nobody drew.");
                return;
            }

            // The path may land on the point itself (SADDLE.seat_ref) or on the block that holds it
            // (SADDLE) — both are honest ways for the art to write it, and neither is a guess.
            if (!TryVector3Value(target, out Vector3 seat) &&
                !TryVector3(target, "seat_ref", out seat))
            {
                facts.Errors.Add(
                    $"INTERACT[id=ride].at is '{at}', which resolves to something that is neither a " +
                    "three-number point nor a block with a three-number seat_ref. That is where the " +
                    "rider is DRAWN; a missing cushion height plants her in the frame rails.");
                return;
            }

            facts.HasDriverSeat = true;
            facts.DriverSeatLocal = seat;
        }

        /// <summary>Follow a dotted path (<c>SADDLE.seat_ref</c>) through the first owner that
        /// carries its head, then the second. Null when neither does — a refusal, never a guess.</summary>
        static object ResolvePath(string path, params object[] owners)
        {
            string[] parts = path.Split('.');
            foreach (object owner in owners)
            {
                object at = owner;
                bool ok = true;
                foreach (string part in parts)
                {
                    at = DeckSidecarJson.Member(at, part);
                    if (at == null) { ok = false; break; }
                }
                if (ok) return at;
            }
            return null;
        }

        /// <summary>The first two numbers of a member array, as a point. False for an absent member,
        /// a short array, or prose — all three of which are facts about the interaction rather than
        /// parse failures.</summary>
        static bool TryPointXY(object owner, string key, out Vector2 p)
        {
            p = default;
            List<object> a = DeckSidecarJson.AsArray(DeckSidecarJson.Member(owner, key));
            if (a == null || a.Count < 2) return false;
            if (!DeckSidecarJson.TryDouble(a[0], out double x)) return false;
            if (!DeckSidecarJson.TryDouble(a[1], out double y)) return false;
            p = new Vector2((float)x, (float)y);
            return true;
        }

        /// <summary>A value that IS a three-number array, rather than a member of one.</summary>
        static bool TryVector3Value(object value, out Vector3 v)
        {
            v = default;
            List<object> a = DeckSidecarJson.AsArray(value);
            if (a == null || a.Count < 3) return false;
            if (!DeckSidecarJson.TryDouble(a[0], out double x)) return false;
            if (!DeckSidecarJson.TryDouble(a[1], out double y)) return false;
            if (!DeckSidecarJson.TryDouble(a[2], out double z)) return false;
            v = new Vector3((float)x, (float)y, (float)z);
            return true;
        }

        // ---- flotation ---------------------------------------------------------------------------

        static void ReadFlotation(VehicleSidecarFacts facts, object owner)
        {
            object flotation = DeckSidecarJson.Member(owner, "FLOAT");
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


        // ---- coupling ----------------------------------------------------------------------------

        /// <summary>
        /// A tractor's plate, off <c>TOW.fifth_wheel</c>. Every number the capture test needs and not
        /// one more: the seat, the throat, the reach, the ramp mouth, the release handle and the
        /// clearance the jackknife cap is solved against.
        /// </summary>
        static void ReadFifthWheel(VehicleSidecarFacts facts, object owner)
        {
            object tow = DeckSidecarJson.Member(owner, "TOW");
            object fw = DeckSidecarJson.Member(tow, "fifth_wheel");
            if (DeckSidecarJson.AsObject(fw) == null)
            {
                facts.Absences.Add("no TOW.fifth_wheel - she does not pull a trailer.");
                return;
            }

            object slot = DeckSidecarJson.Member(fw, "slot");
            object ramps = DeckSidecarJson.Member(fw, "ramps");
            object handle = DeckSidecarJson.Member(fw, "release_handle");
            object swing = DeckSidecarJson.Member(tow, "swing_clearance");

            if (!TryVector3(fw, "coupling_point", out Vector3 seat) ||
                !TryNumber(slot, "half_width", out float halfWidth) ||
                !TryPair(slot, "reach_y", out float reachA, out float reachB) ||
                !TryPairAt(ramps, "to", out float rampY, out float _) ||
                !TryVector3(handle, "pos", out Vector3 release) ||
                !TryNumber(swing, "kingpin_to_cabback_m", out float clearance))
            {
                facts.Errors.Add(
                    "TOW.fifth_wheel is present but incomplete - the capture test needs the seat, " +
                    "the slot's half_width and reach_y, the ramps' aft end, the release handle and " +
                    "swing_clearance.kingpin_to_cabback_m. A partial plate would couple on numbers " +
                    "nobody published.");
                return;
            }

            facts.HasFifthWheel = true;
            facts.FifthWheel = new VehicleFifthWheel
            {
                Published = true,
                CouplingPointLocal = seat,
                SlotHalfWidthMeters = halfWidth,
                SlotMouthY = Mathf.Min(reachA, reachB),
                SlotSeatY = Mathf.Max(reachA, reachB),
                RampMouthY = rampY,
                ReleaseHandleLocal = new Vector2(release.x, release.y),
                CabClearanceMeters = clearance,
            };
        }

        /// <summary>
        /// A towed body's pin, off <c>KINGPIN</c> — and ⚠️ PER BODY: her coupling point and both her
        /// follow inputs are published as maps keyed by body id, because one sidecar serves four
        /// lengths. Reading the map instead of the body is the (file, pick) trap again, in the one
        /// place where getting it wrong makes a 28 ft pup track like a 53.
        /// </summary>
        static void ReadKingpin(VehicleSidecarFacts facts, object owner, string bodyScope)
        {
            object kp = DeckSidecarJson.Member(owner, "KINGPIN");
            if (DeckSidecarJson.AsObject(kp) == null)
            {
                facts.Absences.Add("no KINGPIN - she is not something anybody tows.");
                return;
            }

            if (string.IsNullOrEmpty(bodyScope))
            {
                facts.Errors.Add(
                    "KINGPIN is published but no body was named. Her coupling point and follow " +
                    "inputs are maps keyed by body id; without the pick there is nothing to read " +
                    "but somebody else's trailer.");
                return;
            }

            object byBody = DeckSidecarJson.Member(kp, "coupling_point_by_body");
            object inputs = DeckSidecarJson.Member(kp, "off_tracking_inputs");

            if (!TryVector3(byBody, bodyScope, out Vector3 pin) ||
                !TryNumber(kp, "nose_swing_r_m", out float noseSwing) ||
                !TryNumber(DeckSidecarJson.Member(inputs, "kingpin_to_axle_centre_m"), bodyScope,
                           out float toAxle) ||
                !TryNumber(DeckSidecarJson.Member(inputs, "tail_swing_r_m"), bodyScope,
                           out float tailSwing) ||
                !TryNumber(kp, "width_m", out float width) ||
                !TryNumber(kp, "set_from_nose_m", out float set) ||
                !TryNumber(DeckSidecarJson.Member(kp, "pin"), "r", out float pinRadius))
            {
                facts.Errors.Add(
                    $"KINGPIN is present but carries nothing for '{bodyScope}'. It needs " +
                    "coupling_point_by_body, nose_swing_r_m, pin.r, and both off_tracking_inputs " +
                    "maps - " +
                    "the last is the length her whole follow is solved on, and a missing one would " +
                    "make her track like whichever trailer was read instead.");
                return;
            }

            facts.HasKingpin = true;
            facts.Kingpin = new VehicleKingpin
            {
                Published = true,
                CouplingPointLocal = pin,
                NoseSwingRadiusMeters = noseSwing,
                KingpinToAxleCentreMeters = toAxle,
                TailSwingRadiusMeters = tailSwing,
                // The two the cap is solved from. Her swing radius is sqrt of their squares, which
                // the sidecar states as its own swing_basis - so reading both is a cross-check as
                // well as an input, and VehicleCouplingTests asserts they reproduce it.
                NoseHalfWidthMeters = width * 0.5f,
                KingpinSetMeters = set,
                // Her shaft radius. The capture test asks whether PIN METAL is in the slot, so the
                // size of the pin is an input to it - see VehicleCouplingMath.IsCaptured.
                PinRadiusMeters = pinRadius,
            };
        }

        static bool TryVector3(object owner, string key, out Vector3 v)
        {
            v = default;
            List<object> a = DeckSidecarJson.AsArray(DeckSidecarJson.Member(owner, key));
            if (a == null || a.Count < 3) return false;
            if (!DeckSidecarJson.TryDouble(a[0], out double x)) return false;
            if (!DeckSidecarJson.TryDouble(a[1], out double y)) return false;
            if (!DeckSidecarJson.TryDouble(a[2], out double z)) return false;
            v = new Vector3((float)x, (float)y, (float)z);
            return true;
        }

        /// <summary>The first two numbers of an array member - `ramps.to` is [y, z].</summary>
        static bool TryPairAt(object owner, string key, out float a, out float b) =>
            TryPair(owner, key, out a, out b);

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
