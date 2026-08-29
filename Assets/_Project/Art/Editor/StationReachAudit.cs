using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// <b>THE WHOLE-SCENE REACH PASS — the one the kit's own README says is still owed.</b>
    ///
    /// <para>Every <see cref="StationInteractable.ReachPoint"/> in this kit was TESTED at bake time
    /// rather than requested: the rig marches a 0.22 m body out along the fitting→point line in 6 cm
    /// steps until it clears every blocker, and publishes a null with a reason when it cannot. That audit
    /// is <b>scoped to one piece</b> — <see cref="StationInteractable.ReachTestedAgainstThisPieceOnly"/>
    /// says so in as many words, and the kit's README repeats it: <i>"a dispenser is not told the island
    /// is under it, so a laid-out forecourt still needs a whole-scene pass after plan()"</i>. This is that
    /// pass.</para>
    ///
    /// <para><b>It runs the SAME march, over the whole forecourt.</b> Same body radius, same arm lengths,
    /// same 6 cm step, same order of attempts (as asked → flipped → the four axes), because a second
    /// rule would mean two definitions of "can a body stand here" and the one that ships would be
    /// whichever ran last. What changes is only the set of solids: every blocking blocker of every placed
    /// piece at the same LEVEL, plus whatever else the region says is in the way — a quay wall, a road
    /// corridor, a building.</para>
    ///
    /// <para><b>⚠️ Hit-testing happens in each blocker's OWN frame, not in world.</b> The rig inflates a
    /// footprint's axis-aligned bounding box by the body radius rather than offsetting the polygon,
    /// because "footprints in this kit are axis-aligned rectangles". Turned into the world at a facing
    /// they are no longer axis-aligned, and inflating a world bbox would over-block a piece stood on a
    /// diagonal by up to 41%. So a candidate point is taken back into the owning piece's local frame and
    /// tested there, which is exactly the rig's own test on exactly the rig's own numbers.</para>
    ///
    /// <para><b>Pure.</b> No scene, no assets beyond the Defs handed in, no time, no randomness — feed it
    /// a placement list and it returns the same verdicts every time, which is what lets an EditMode test
    /// assert a forecourt nobody has opened Unity to look at.</para>
    /// </summary>
    public static class StationReachAudit
    {
        // ---- the rig's own constants, carried rather than re-chosen (sidecar `reach_audit`) --------

        /// <summary>Body radius (m) the march clears. The rig's <c>BODY_R</c>.</summary>
        public const float BodyRadiusMetres = 0.22f;

        /// <summary>How far a hand arrives for an ordinary action. The rig's <c>REACH.grasp</c>.</summary>
        public const float ArmGraspMetres = 1.20f;

        /// <summary>…and for one that is READ or PAID at rather than touched. The rig's
        /// <c>REACH.read</c>, and the reason a price sign does not need a body against it.</summary>
        public const float ArmReadMetres = 1.90f;

        /// <summary>The march's step (m). The rig's <c>REACH.step</c>.</summary>
        public const float StepMetres = 0.06f;

        /// <summary>Actions the rig gives the longer arm to.</summary>
        public static float ArmFor(string action) =>
            action == "read" || action == "pay" ? ArmReadMetres : ArmGraspMetres;

        // =====================================================================================
        //  WHAT IS ON THE GROUND
        // =====================================================================================

        /// <summary>One piece standing somewhere, at some facing. The unit a forecourt is a list of.</summary>
        public readonly struct Placed
        {
            /// <summary>The piece. Never null in a well-formed list; a null is skipped rather than thrown on.</summary>
            public readonly StationPieceDef Def;

            /// <summary>Its ground centre, in world metres.</summary>
            public readonly Vector2 Origin;

            /// <summary>Its facing cell (0…7) — see <see cref="StationCatalog"/> for what that means.</summary>
            public readonly int Facing;

            /// <summary>What to call it in a report. The scene object's name, so a verdict names something
            /// a person can select.</summary>
            public readonly string Name;

            public Placed(StationPieceDef def, Vector2 origin, int facing, string name = null)
            {
                Def = def;
                Origin = origin;
                Facing = StationCatalog.Wrap(facing);
                Name = string.IsNullOrEmpty(name) ? (def != null ? def.name : "?") : name;
            }
        }

        /// <summary>
        /// The three levels a forecourt has, and the rule is the rig's: a body standing on one is stopped
        /// only by solids on the same one. A roof plant does not block the apron, and an apron bollard
        /// does not block the sales floor.
        /// </summary>
        public enum Level
        {
            /// <summary>The apron, the service stoop, the island kerb — everything at grade.</summary>
            Ground,
            /// <summary>Inside a storefront.</summary>
            SalesFloor,
            /// <summary>On the roof deck.</summary>
            Roof,
        }

        /// <summary>The level a sidecar word names. Anything unrecognised is <see cref="Level.Ground"/> —
        /// the level a forecourt mostly is, and the safe way to be wrong (it blocks more, never less).</summary>
        public static Level LevelOf(string sidecarLevel)
        {
            switch (sidecarLevel)
            {
                case "sales_floor": return Level.SalesFloor;
                case "roof":
                case "roof_deck": return Level.Roof;
                default: return Level.Ground;
            }
        }

        // =====================================================================================
        //  THE VERDICT
        // =====================================================================================

        /// <summary>What the whole-scene pass decided about one fitting.</summary>
        public enum Verdict
        {
            /// <summary>The bake's own point still stands: nothing else on the forecourt reaches it.</summary>
            Ok,
            /// <summary>Something else on the forecourt was standing there, and a clear spot was found by
            /// marching — the same repair the rig makes, at scene scope.</summary>
            Moved,
            /// <summary>Nowhere within arm's reach is clear. Carried as a refusal, never zeroed to the
            /// origin — the same rule <see cref="StationInteractable.HasReachPoint"/> follows.</summary>
            NoClearSpot,
            /// <summary>The bake itself published no point for this fitting. Reported rather than
            /// re-derived: the rig already refused, and inventing one here would overrule it.</summary>
            NotPublished,
        }

        /// <summary>One fitting's answer.</summary>
        public readonly struct Result
        {
            /// <summary>Index of the piece this belongs to, in the placement list handed to
            /// <see cref="Audit"/>. Carried so a caller can hang something on the right piece without
            /// re-deriving the order the verdicts came out in.</summary>
            public readonly int PieceIndex;

            public readonly string PieceName;
            public readonly string PieceKey;
            public readonly string InteractableId;
            public readonly string Action;
            public readonly Level OnLevel;

            /// <summary>Where the fitting is, in world metres.</summary>
            public readonly Vector2 Fitting;

            /// <summary>Where the BAKE said to stand, in world metres.</summary>
            public readonly Vector2 Requested;

            /// <summary>Where the whole-scene pass says to stand. Equals <see cref="Requested"/> on
            /// <see cref="Verdict.Ok"/>; meaningless on a refusal (check <see cref="Outcome"/> first).</summary>
            public readonly Vector2 Point;

            /// <summary>How far the pass had to move it (m). 0 on <see cref="Verdict.Ok"/>.</summary>
            public readonly float MovedMetres;

            public readonly Verdict Outcome;

            /// <summary>What was standing on the requested spot — "island_s2/bollard", or the region's
            /// own word for a scene obstruction. Empty when nothing was.</summary>
            public readonly string BlockedBy;

            public Result(int pieceIndex, string pieceName, string pieceKey, string id, string action,
                          Level level, Vector2 fitting, Vector2 requested, Vector2 point, float moved,
                          Verdict outcome, string blockedBy)
            {
                PieceIndex = pieceIndex;
                PieceName = pieceName; PieceKey = pieceKey; InteractableId = id; Action = action;
                OnLevel = level; Fitting = fitting; Requested = requested; Point = point;
                MovedMetres = moved; Outcome = outcome; BlockedBy = blockedBy ?? "";
            }

            /// <summary>True when a body can stand somewhere and use this.</summary>
            public bool Usable => Outcome == Verdict.Ok || Outcome == Verdict.Moved;

            public override string ToString() =>
                $"{PieceName}/{InteractableId} {Outcome}" +
                (MovedMetres > 0f ? $" {MovedMetres:0.00} m" : "") +
                (string.IsNullOrEmpty(BlockedBy) ? "" : $" (was inside {BlockedBy})");
        }

        // =====================================================================================
        //  THE PASS
        // =====================================================================================

        /// <summary>
        /// Audit a laid-out forecourt. One <see cref="Result"/> per interactable on every placed piece,
        /// in placement order.
        /// </summary>
        /// <param name="pieces">The forecourt as placed.</param>
        /// <param name="sceneBlocks">Optional: is this world point occupied by something that is not part
        /// of the forecourt? The region's own obstructions — a quay wall, a road corridor, a building —
        /// which this class cannot know about and must not guess at. Null means "the forecourt is all
        /// there is". ⚠️ It is asked about the BODY'S CENTRE; a caller that wants a body radius honoured
        /// must inflate its own shapes, because only the region knows which of its shapes are solid.</param>
        public static List<Result> Audit(IReadOnlyList<Placed> pieces,
                                         Func<Vector2, Level, bool> sceneBlocks = null)
        {
            var results = new List<Result>();
            if (pieces == null || pieces.Count == 0) return results;

            List<Solid> solids = SolidsOf(pieces);

            for (int p = 0; p < pieces.Count; p++)
            {
                Placed placed = pieces[p];
                if (placed.Def == null || placed.Def.Interactables == null) continue;

                foreach (StationInteractable it in placed.Def.Interactables)
                {
                    if (it == null) continue;

                    Level level = LevelOf(it.Level);
                    Vector2 fitting = StationCatalog.LocalToWorld(it.Position, placed.Origin, placed.Facing);

                    if (!it.HasReachPoint)
                    {
                        results.Add(new Result(p, placed.Name, placed.Def.name, it.Id, it.Action, level,
                                               fitting, fitting, fitting, 0f, Verdict.NotPublished,
                                               it.ReachVerdict));
                        continue;
                    }

                    Vector2 asked = StationCatalog.LocalToWorld(it.ReachPoint, placed.Origin, placed.Facing);
                    results.Add(March(placed, p, it, level, fitting, asked, solids, sceneBlocks));
                }
            }
            return results;
        }

        /// <summary>
        /// <b>Is there room to stand — or to STAND SOMETHING — at this world point?</b> Returns the
        /// name of the first placed blocker it is inside (<c>"piece/kind"</c>), or null for clear.
        ///
        /// <para>The same solids the reach pass marches against, asked one question instead of a whole
        /// march. It exists because a forecourt gained a second kind of occupant: the pass above only
        /// ever asks about a body reaching a fitting the KIT published, and a jerry can set down beside
        /// the pumps is neither. Standing one inside the island kerb or under a canopy post would draw
        /// perfectly and read as a bug.</para>
        ///
        /// <para>⚠️ There is no host exemption here, and that is the difference from the march: a
        /// fitting is allowed to be right up against the machine it belongs to, and a loose object
        /// belongs to nothing. Everything is tested inflated.</para>
        ///
        /// <para>⚠️ And this asks only about the FORECOURT. Whether there is ground under the point
        /// at all is the REGION's question — ask its own obstruction predicate as well, the way
        /// <see cref="Audit"/> does. A pass that tested only solids called a pump standing in mid-air
        /// OK once already, and only a positive control caught it.</para>
        /// </summary>
        public static string BlockerAt(IReadOnlyList<Placed> pieces, Vector2 world, Level level,
                                       float bodyRadius)
        {
            if (pieces == null || pieces.Count == 0) return null;

            foreach (Solid s in SolidsOf(pieces))
            {
                if (s.OnLevel != level) continue;
                if (s.Hits(world, bodyRadius)) return $"{s.OwnerName}/{s.Kind}";
            }
            return null;
        }

        /// <summary>
        /// The march itself, and it is the rig's: try the direction the bake asked for, then the same line
        /// flipped, then the four piece-local axes; step out from the body radius to the arm in 6 cm
        /// steps; take the first point that clears everything.
        ///
        /// <para>The axes are the OWNING PIECE's local ones mapped to world, not world x/y, so a forecourt
        /// stood on a diagonal repairs the same way the bake did.</para>
        /// </summary>
        static Result March(Placed placed, int placedIndex, StationInteractable it, Level level,
                            Vector2 fitting, Vector2 asked, List<Solid> solids,
                            Func<Vector2, Level, bool> sceneBlocks)
        {
            float arm = ArmFor(it.Action);

            Vector2 line = asked - fitting;
            float L = line.magnitude;
            Vector2 dir = L < 1e-6f ? StationCatalog.ForwardOf(placed.Facing) : line / L;
            if (L < 1e-6f) L = BodyRadiusMetres;

            Vector2 right = StationCatalog.RightOf(placed.Facing);
            Vector2 fwd = StationCatalog.ForwardOf(placed.Facing);

            var attempts = new (Vector2 dir, float from)[]
            {
                (dir, L), (-dir, BodyRadiusMetres),
                (fwd, BodyRadiusMetres), (-fwd, BodyRadiusMetres),
                (right, BodyRadiusMetres), (-right, BodyRadiusMetres),
            };

            string firstBlocker = null;

            foreach (var a in attempts)
            {
                for (float t = Mathf.Max(a.from, BodyRadiusMetres); t <= arm + 1e-6f; t += StepMetres)
                {
                    Vector2 q = fitting + a.dir * t;

                    string hit = FirstHit(q, level, solids, placedIndex, it);
                    if (hit == null && sceneBlocks != null && sceneBlocks(q, level)) hit = "the region";
                    if (hit != null) { firstBlocker ??= hit; continue; }

                    float moved = (q - asked).magnitude;
                    return new Result(placedIndex, placed.Name, placed.Def.name, it.Id, it.Action, level,
                                      fitting, asked, q, moved,
                                      moved > 0.005f ? Verdict.Moved : Verdict.Ok,
                                      moved > 0.005f ? firstBlocker : null);
                }
            }

            return new Result(placedIndex, placed.Name, placed.Def.name, it.Id, it.Action, level,
                              fitting, asked, asked, 0f, Verdict.NoClearSpot, firstBlocker);
        }

        // =====================================================================================
        //  THE SOLIDS
        // =====================================================================================

        /// <summary>One blocking blocker, kept in its OWNER's frame — see the class remarks for why the
        /// hit test does not happen in world.</summary>
        readonly struct Solid
        {
            public readonly int Owner;          // index into the placement list
            public readonly string OwnerName;
            public readonly string Kind;
            public readonly Level OnLevel;
            public readonly Vector2 Origin;
            public readonly int Facing;
            public readonly bool IsCircle;
            public readonly Vector2 Center;     // local
            public readonly float Radius;
            public readonly Vector2 Min, Max;   // local axis-aligned bounds of the footprint

            public Solid(int owner, string ownerName, string kind, Level level, Vector2 origin, int facing,
                         bool isCircle, Vector2 center, float radius, Vector2 min, Vector2 max)
            {
                Owner = owner; OwnerName = ownerName; Kind = kind; OnLevel = level;
                Origin = origin; Facing = facing;
                IsCircle = isCircle; Center = center; Radius = radius; Min = min; Max = max;
            }

            /// <summary>A world point taken back into this blocker's own frame.</summary>
            public Vector2 ToLocal(Vector2 world)
            {
                Vector2 d = world - Origin;
                // The inverse of x·right + y·forward, which is orthonormal, so the transpose does it.
                Vector2 right = StationCatalog.RightOf(Facing), fwd = StationCatalog.ForwardOf(Facing);
                return new Vector2(Vector2.Dot(d, right), Vector2.Dot(d, fwd));
            }

            /// <summary>Does a body of <paramref name="r"/> at <paramref name="world"/> hit this?</summary>
            public bool Hits(Vector2 world, float r)
            {
                Vector2 p = ToLocal(world);
                if (IsCircle) return (p - Center).sqrMagnitude < (Radius + r) * (Radius + r);
                return p.x > Min.x - r && p.x < Max.x + r && p.y > Min.y - r && p.y < Max.y + r;
            }

            /// <summary>Is this the fitting's own HOST — the thing it is mounted on? The rig tests a host
            /// at its true footprint rather than inflated, or a cooler door could never be opened.</summary>
            public bool Hosts(Vector2 fittingLocalPos) =>
                !IsCircle &&
                fittingLocalPos.x >= Min.x && fittingLocalPos.x <= Max.x &&
                fittingLocalPos.y >= Min.y && fittingLocalPos.y <= Max.y;
        }

        static List<Solid> SolidsOf(IReadOnlyList<Placed> pieces)
        {
            var solids = new List<Solid>();
            for (int i = 0; i < pieces.Count; i++)
            {
                Placed p = pieces[i];
                if (p.Def?.Blockers == null) continue;

                foreach (StationBlocker b in p.Def.Blockers)
                {
                    if (b == null || !b.Blocks) continue;      // step_over and flat stop nobody

                    Level level = LevelOf(b.Level);
                    if (b.IsCircle)
                    {
                        solids.Add(new Solid(i, p.Name, b.Kind, level, p.Origin, p.Facing,
                                             true, b.Center, b.Radius, Vector2.zero, Vector2.zero));
                        continue;
                    }
                    if (b.Footprint == null || b.Footprint.Length < 3) continue;

                    var min = new Vector2(float.MaxValue, float.MaxValue);
                    var max = new Vector2(float.MinValue, float.MinValue);
                    foreach (Vector2 v in b.Footprint)
                    {
                        min = Vector2.Min(min, v);
                        max = Vector2.Max(max, v);
                    }
                    solids.Add(new Solid(i, p.Name, b.Kind, level, p.Origin, p.Facing,
                                         false, Vector2.zero, 0f, min, max));
                }
            }
            return solids;
        }

        /// <summary>The first solid a body at <paramref name="world"/> is inside, named, or null.</summary>
        static string FirstHit(Vector2 world, Level level, List<Solid> solids, int ownerIndex,
                               StationInteractable it)
        {
            for (int i = 0; i < solids.Count; i++)
            {
                Solid s = solids[i];
                if (s.OnLevel != level) continue;

                // The host exemption, and only on the fitting's OWN piece: a body stands right up against
                // the thing it is using, so its host is tested at its true footprint.
                float r = BodyRadiusMetres;
                if (s.Owner == ownerIndex && s.Hosts(new Vector2(it.Position.x, it.Position.y))) r = 0f;

                if (s.Hits(world, r)) return $"{s.OwnerName}/{s.Kind}";
            }
            return null;
        }

        // =====================================================================================
        //  THE REPORT
        // =====================================================================================

        /// <summary>The pass in one line, for a build log — counts first, then anything that is not
        /// <see cref="Verdict.Ok"/> by name, because those are the only ones worth reading.</summary>
        public static string Report(IReadOnlyList<Result> results)
        {
            if (results == null || results.Count == 0) return "no interactables to audit";

            int ok = 0, moved = 0, none = 0, unpublished = 0;
            var notable = new List<string>();
            foreach (Result r in results)
            {
                switch (r.Outcome)
                {
                    case Verdict.Ok: ok++; break;
                    case Verdict.Moved: moved++; notable.Add(r.ToString()); break;
                    case Verdict.NoClearSpot: none++; notable.Add(r.ToString()); break;
                    default: unpublished++; notable.Add(r.ToString()); break;
                }
            }

            string head = $"{results.Count} checked / {ok} as baked / {moved} moved / {none} with " +
                          $"nowhere to stand / {unpublished} the bake never published";
            return notable.Count == 0 ? head : head + " — " + string.Join(" · ", notable);
        }
    }
}
