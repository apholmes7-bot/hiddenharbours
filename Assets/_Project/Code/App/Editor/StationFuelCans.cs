#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;               // FuelContainerDef / FuelLevelPresenter
using HiddenHarbours.Art.Editor;        // StationReachAudit
using HiddenHarbours.Player;            // CarriableFuelContainer

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>EMPTY CANS STANDING AT THE PUMPS</b> — the last thing the fuel loop was missing, and it was
    /// never a mechanism. <c>FuelPump</c> fills any <c>IFuelVessel</c> and charges for it;
    /// <c>CarriableFuelContainer</c> carries one; eighty-four container Defs are baked. What did not
    /// exist was WORLD TRUTH: nowhere in the game could you find a can. The only way to hold one was a
    /// dev menu item, so the owner could not walk up to a station and try the thing the station is for.
    ///
    /// <para><b>Empty is the whole point.</b> A can placed full proves nothing — the fill is the verb
    /// under test, and a gauge that was already at the top cannot show it moved. These stand at zero,
    /// which the fuel kit bakes a real frame for (the ladder's first rung), so an empty can LOOKS
    /// empty before the press and full after it.</para>
    ///
    /// <para><b>⚠️⚠️ EVERY SPOT IS PROVED, NOT CHOSEN.</b> The gas-station arc paid for this once: a
    /// reach audit that asks "is this point inside a solid" and never "is there anything to stand ON"
    /// passed a pump standing 1.06 m out over a tidal basin. So each can is checked three ways before
    /// it is placed — the REGION's own obstruction predicate (ground above spring high, clear of walls
    /// and carriageways), the FORECOURT's blockers, and whether a body can actually get within reach of
    /// it. A spot that fails any of them is REFUSED with its reason and no can is placed there. An
    /// object standing somewhere unreachable is the failure mode that reads as working.</para>
    ///
    /// <para><b>Region-agnostic:</b> nothing here knows a tide, a road or a forecourt. Where the cans
    /// go is the SITE's business (<see cref="NineMileCreekStation"/>), the same division
    /// <see cref="StationForecourt"/> draws.</para>
    /// </summary>
    public static class StationFuelCans
    {
        /// <summary>Where the container Defs live (#496).</summary>
        public const string ContainerFolder = "Assets/_Project/Data/FuelContainers";

        /// <summary>
        /// How much room a can wants around it (m) — <b>a body's worth, and that is the derivation</b>:
        /// the reason a can needs clearance at all is that somebody has to be able to get to it and bend
        /// down. Read from the reach audit rather than typed, so a change to what a body is moves this
        /// with it.
        /// </summary>
        public static float CanClearanceMetres => StationReachAudit.BodyRadiusMetres;

        /// <summary>Directions tried when looking for somewhere to stand beside a can. Sixteen is the
        /// same 22.5° lattice the kit's own passes use, and it is fine enough that no gap wide enough
        /// for a body is missed between two rays.</summary>
        public const int StandingProbeRays = 16;

        // =====================================================================================
        //  A SPOT
        // =====================================================================================

        /// <summary>One can: which Def, where it stands in the layout's own frame, and why it is
        /// there.</summary>
        public readonly struct Spot
        {
            /// <summary>The container Def's stable id (<c>fuelstore.gas_jerry_s20</c>-style), NOT the
            /// asset filename — ids are the append-only contract and a filename is not.</summary>
            public readonly string DefId;

            /// <summary>Metres in the LAYOUT's frame, the same frame <c>StationIso.plan()</c> emits, so
            /// re-aiming the forecourt at a different road carries the cans round with it.</summary>
            public readonly Vector2 Local;

            public readonly string Reason;

            public Spot(string defId, Vector2 local, string reason)
            {
                DefId = defId; Local = local; Reason = reason;
            }
        }

        /// <summary>What a placement did.</summary>
        public sealed class Result
        {
            public GameObject Root;
            public readonly List<GameObject> Placed = new List<GameObject>();

            /// <summary>Spots that were refused, each with the sentence saying why. ⚠️ A refusal is a
            /// siting fault to be FIXED, not a note to be lived with — the acceptance for this lane is
            /// that cans stand on proven ground.</summary>
            public readonly List<string> Refused = new List<string>();

            public int Count => Placed.Count;
        }

        // =====================================================================================
        //  PLACEMENT
        // =====================================================================================

        /// <summary>
        /// Stand a cluster of empty cans. Returns what went down and what was refused.
        ///
        /// <para><b>Re-runnable</b> — the root is rebuilt from scratch, so a second builder pass leaves
        /// one of everything.</para>
        /// </summary>
        /// <param name="forecourt">The station as placed, so a can is not stood inside a canopy post.
        /// May be null when there is no forecourt to clear.</param>
        /// <param name="sceneBlocks">The region's own obstructions — the predicate that knows about
        /// ground, walls and roads. ⚠️ Null means this cannot check for ground at all, which is exactly
        /// the blindness that put a pump over the water; it is allowed only so a caller with no terrain
        /// can still place, and it is reported.</param>
        public static Result Place(Transform parent, string rootName,
                                   Vector2 origin, int facing, IReadOnlyList<Spot> spots,
                                   IReadOnlyList<StationReachAudit.Placed> forecourt,
                                   Func<Vector2, StationReachAudit.Level, bool> sceneBlocks,
                                   string tag)
        {
            var result = new Result();
            if (spots == null || spots.Count == 0) return result;

            result.Root = new GameObject(rootName);
            if (parent != null) result.Root.transform.SetParent(parent, worldPositionStays: false);

            if (sceneBlocks == null)
                Debug.LogWarning(
                    $"{tag} the cans are being placed with NO region obstruction predicate, so nothing " +
                    "here can tell dry land from open water. Pass the region's own.");

            foreach (Spot spot in spots)
            {
                FuelContainerDef def = LoadContainer(spot.DefId);
                if (def == null)
                {
                    result.Refused.Add(
                        $"'{spot.DefId}' — no such FuelContainerDef under {ContainerFolder}, so the can " +
                        $"that would stand {spot.Reason} is missing. Run the fuel bake.");
                    continue;
                }

                if (!def.Carriable)
                {
                    result.Refused.Add(
                        $"'{spot.DefId}' is a {def.VesselType}, which is not carriable — standing one " +
                        "here would be a can the player can see and never pick up.");
                    continue;
                }

                Vector2 world = StationCatalog.LocalToWorld(spot.Local, origin, facing);

                if (!Standable(world, forecourt, sceneBlocks, out string why))
                {
                    result.Refused.Add($"'{spot.DefId}' at ({world.x:0.##},{world.y:0.##}) — {why}.");
                    continue;
                }

                GameObject go = Stand(def, world, spot);
                go.transform.SetParent(result.Root.transform, worldPositionStays: true);
                result.Placed.Add(go);
            }

            Report(result, tag);
            return result;
        }

        /// <summary>
        /// One empty can, built the way the owner's own dev menu builds one.
        ///
        /// <para><b>⚠️ Deliberately <c>FuelContainerSpawnMenu.Build</c> and not a hand-assembled
        /// look-alike.</b> That method is public for exactly this reason — there is ONE answer to "what
        /// components does a fuel container have", and a second copy here would drift the first time
        /// one was added. Two things are then overridden, and only two: the level, and the id.</para>
        /// </summary>
        static GameObject Stand(FuelContainerDef def, Vector2 world, in Spot spot)
        {
            GameObject go = FuelContainerSpawnMenu.Build(def, new Vector3(world.x, world.y, 0f));

            // EMPTY. The menu spawns half-full so a carried can shows a level; a can standing at a pump
            // is there to be FILLED, and a gauge already off the stop proves nothing about the verb.
            var presenter = go.GetComponent<FuelLevelPresenter>();
            if (presenter != null) presenter.Fill = 0f;

            // ⚠️ A STABLE id, not the menu's session counter. This can is placed content that a builder
            // re-run must reproduce identically; an id that counted spawns would differ between two
            // builds of the same scene and could collide with a dev-spawned can in the same session.
            var carriable = go.GetComponent<CarriableFuelContainer>();
            if (carriable != null) carriable.Configure($"{def.Id}.{Slug(spot.Local)}");

            go.name = $"EmptyCan_{def.Id}";
            return go;
        }

        /// <summary>A can's id suffix from where it stands — stable across builds, readable in a log,
        /// and unique among a cluster because no two cans share a spot. Invariant culture: an id is a
        /// contract, and a comma decimal separator would make one machine's ids differ from
        /// another's.</summary>
        static string Slug(Vector2 local)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return $"at_{local.x.ToString("0.##", ci)}_{local.y.ToString("0.##", ci)}"
                   .Replace('.', 'p').Replace('-', 'm');
        }

        // =====================================================================================
        //  THE THREE CHECKS
        // =====================================================================================

        /// <summary>
        /// Can a can stand here — and can somebody get to it? All three questions, in the order that
        /// makes the failure readable.
        /// </summary>
        public static bool Standable(Vector2 world,
                                     IReadOnlyList<StationReachAudit.Placed> forecourt,
                                     Func<Vector2, StationReachAudit.Level, bool> sceneBlocks,
                                     out string why)
        {
            why = null;

            // 1. Is there GROUND — dry at the top of the tide, clear of walls and carriageways? The
            //    region is the only thing that knows, and this is the check whose absence let a pedestal
            //    publish a standing spot out over the basin.
            if (sceneBlocks != null && sceneBlocks(world, StationReachAudit.Level.Ground))
            {
                why = "the region says there is nothing to stand on here (no dry ground at spring high, " +
                      "or inside a wall or a carriageway)";
                return false;
            }

            // 2. Is the forecourt itself in the way?
            string hit = StationReachAudit.BlockerAt(forecourt, world, StationReachAudit.Level.Ground,
                                                     CanClearanceMetres);
            if (hit != null)
            {
                why = $"it is inside {hit}";
                return false;
            }

            // 3. ⚠️ AND CAN A BODY REACH IT. The first two say the can may exist; only this says the can
            //    can be PICKED UP. A can in the middle of a walled pen passes both of the above.
            if (!HasStandingSpot(world, forecourt, sceneBlocks))
            {
                why = "nothing can stand within reach of it — it would be a can you can see and never lift";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Is there anywhere a body can stand within arm's length of a can here?
        ///
        /// <para>Rays on the kit's own 22.5° lattice, stepped out from touching to the carriable's own
        /// reach in the audit's 6 cm steps — the same march the reach pass runs, asked about an object
        /// rather than a fitting.</para>
        /// </summary>
        public static bool HasStandingSpot(Vector2 world,
                                           IReadOnlyList<StationReachAudit.Placed> forecourt,
                                           Func<Vector2, StationReachAudit.Level, bool> sceneBlocks)
        {
            float from = CanClearanceMetres + StationReachAudit.BodyRadiusMetres;
            float to = Mathf.Max(from, CarriableFuelContainer.LooseReachMetres);

            for (int r = 0; r < StandingProbeRays; r++)
            {
                float a = r * (2f * Mathf.PI / StandingProbeRays);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));

                for (float d = from; d <= to + 1e-4f; d += StationReachAudit.StepMetres)
                {
                    Vector2 p = world + dir * d;
                    if (sceneBlocks != null && sceneBlocks(p, StationReachAudit.Level.Ground)) continue;
                    if (StationReachAudit.BlockerAt(forecourt, p, StationReachAudit.Level.Ground,
                                                    StationReachAudit.BodyRadiusMetres) != null) continue;
                    return true;
                }
            }
            return false;
        }

        // =====================================================================================
        //  LOADING & REPORTING
        // =====================================================================================

        /// <summary>The container Def with this id, or null. By ID rather than by path: the ids are the
        /// append-only contract, and a Def that was renamed on disk still answers to its id.</summary>
        public static FuelContainerDef LoadContainer(string id)
        {
            if (string.IsNullOrEmpty(id) || !AssetDatabase.IsValidFolder(ContainerFolder)) return null;
            foreach (string guid in AssetDatabase.FindAssets("t:FuelContainerDef", new[] { ContainerFolder }))
            {
                var def = AssetDatabase.LoadAssetAtPath<FuelContainerDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null && string.Equals(def.Id, id, StringComparison.Ordinal)) return def;
            }
            return null;
        }

        static void Report(Result result, string tag)
        {
            if (result.Count > 0)
                Debug.Log(
                    $"{tag} {result.Count} empty can(s) standing at the pumps, each on ground proved " +
                    "three ways: the region says it is dry at spring high and clear of walls and roads, " +
                    "the forecourt's own blockers are clear of it, and a body can reach it. Walk up, " +
                    "press to lift one, carry it to a hose and press again.");

            foreach (string refused in result.Refused)
                Debug.LogError(
                    $"{tag} NO CAN PLACED — {refused} That is a siting fault: move the spot rather " +
                    "than standing a can somewhere nobody can get to it.");
        }
    }
}
#endif
