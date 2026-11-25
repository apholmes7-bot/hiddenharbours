#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Nine Mile Creek's own routine stations</b>: the two places a visitor's day names on the mainland that are
    /// not a wholesaler's counter. Today that is Junior Poirier's Wednesday run (over the bar on the quad, fuel and
    /// the week's order, back over the bar), whose blocks name <see cref="BarLandingId"/> and <see cref="FuelStopId"/>.
    ///
    /// <para><b>Both positions are derived from what the region already holds</b>, never typed:</para>
    /// <list type="bullet">
    /// <item><b>The bar landing</b> is where the bar's own axis (<see cref="NineMileCreekMainland.BarTo"/> toward
    /// <see cref="NineMileCreekMainland.BarFrom"/>) first crosses the region's drawn coastline
    /// (<see cref="NineMileCreekMainland.CoastPoints"/>, whose vertex there is marked THE BAR LANDING). That is the top
    /// of the beach the bar comes ashore on, at the foot of the bar road and the shore path: dry above spring high
    /// water on the painted ground, so a crossing held shut by the tide holds its walker on land, not on the crest.
    /// The crossing <c>crossing.st_peters_nine_mile_creek_bar</c> ends its path here.</item>
    /// <item><b>The fuel stop</b> is beside the forecourt's gas hose nearest the store (the quad runs on gas, and the
    /// week's order is bought in that store), on the hose's outer side, half the hose's reach off it, facing it.</item>
    /// </list>
    ///
    /// <para><b>Additive only.</b> The table hangs on its own root, <see cref="RootName"/>, written into the committed
    /// scene as text (see <see cref="Plan"/>); nothing else in the scene is touched, and the wholesalers' tables stay
    /// theirs. No lane tree yet: the creek's lanes for a visitor are later
    /// work (<c>design/npcs-and-routines.md</c> §2.9), and while he is in the creek he is computed, not drawn.</para>
    /// </summary>
    public static class NmcRoutineStationsPatch
    {
        public const string RootName = "NmcRoutineStations";
        public const string BarLandingId = "station.nine_mile_creek.bar_landing";
        public const string FuelStopId = "station.nine_mile_creek.fuel_stop";

        /// <summary>The key scene row the order is bought in.</summary>
        public const string StoreRowId = "station.nmc_store";
        /// <summary>The grade the quad burns.</summary>
        public const string QuadGrade = "gas";
        /// <summary>The reach a <c>FuelPump</c> hose has by default (its serialized default), in metres.</summary>
        public const float HoseReachMetres = 2f;
        /// <summary>How far off the hose the customer stands, as a fraction of its reach: well inside it, and clear of
        /// the pump island's wall (measured 0.6 m out from the nozzle).</summary>
        public const float StandOffFractionOfReach = 0.5f;

        // ---- the derivations (pure: no scene) --------------------------------------------------------------------

        /// <summary>Where the bar's axis, walked from the seam end ashore, first meets the drawn coastline.</summary>
        public static Vector2 BarLanding()
        {
            Vector2 from = NineMileCreekMainland.BarTo, axis = (NineMileCreekMainland.BarFrom - from).normalized;
            Vector2[] coast = NineMileCreekMainland.CoastPoints;
            float best = float.PositiveInfinity;
            for (int i = 1; i < coast.Length; i++)
            {
                Vector2 a = coast[i - 1], e = coast[i] - a, r = a - from;
                float den = axis.x * -e.y - axis.y * -e.x;
                if (Mathf.Abs(den) < 1e-9f) continue;
                float t = (r.x * -e.y - r.y * -e.x) / den;
                float s = (axis.x * r.y - axis.y * r.x) / den;
                if (s < 0f || s > 1f || t < 0f) continue;
                best = Mathf.Min(best, t);
            }
            if (float.IsPositiveInfinity(best))
                throw new InvalidOperationException("the bar's axis never meets the coastline: the bar has no landing.");
            return from + axis * best;
        }

        /// <summary>The bar landing's stance: looking back out along the bar, the way home.</summary>
        public static float BarLandingHeading() =>
            StPetersRoutines.HeadingTo(BarLanding(), NineMileCreekMainland.BarTo);

        /// <summary>The gas hose nearest the store: its fixture id, nozzle point, and the twin hose on the same
        /// machine's other side (which says which side is OUT).</summary>
        public static (string Id, Vector2 Nozzle, Vector2 Twin) FuelHose()
        {
            KeySceneDef scene = AssetDatabase.LoadAssetAtPath<KeySceneDef>(NineMileCreekWharfLandSide.FuelStopKeyScenePath);
            if (scene == null) throw new InvalidOperationException("no fuel stop key scene at " + NineMileCreekWharfLandSide.FuelStopKeyScenePath);
            Vector2 store = scene.Pieces.Single(p => p.Id == StoreRowId).At;
            var hoses = NineMileCreekWharfLandSide.FuelPumps
                .Where(h => h.Grade == QuadGrade)
                .Select(h => (h.Id, At: (Vector2)NineMileCreekWharfLandSide.FuelStopRowAnchor(h.Row, h.Anchor)))
                .ToList();
            var hose = hoses.OrderBy(h => Vector2.Distance(h.At, store)).ThenBy(h => h.Id, StringComparer.Ordinal).First();
            string twinId = hose.Id.EndsWith("a_" + QuadGrade, StringComparison.Ordinal)
                ? hose.Id.Substring(0, hose.Id.Length - QuadGrade.Length - 2) + "b_" + QuadGrade
                : hose.Id.Substring(0, hose.Id.Length - QuadGrade.Length - 2) + "a_" + QuadGrade;
            var twin = hoses.Single(h => h.Id == twinId);
            return (hose.Id, hose.At, twin.At);
        }

        /// <summary>Beside the hose on its outer side, half its reach off it.</summary>
        public static Vector2 FuelStop()
        {
            var hose = FuelHose();
            Vector2 outward = (hose.Nozzle - hose.Twin).normalized;
            return hose.Nozzle + outward * (HoseReachMetres * StandOffFractionOfReach);
        }

        /// <summary>Facing the hose.</summary>
        public static float FuelStopHeading() => StPetersRoutines.HeadingTo(FuelStop(), FuelHose().Nozzle);

        /// <summary>The table, as the patch writes it.</summary>
        public static RoutineStationEntry[] Stations() => new[]
        {
            new RoutineStationEntry
            {
                Id = BarLandingId, Position = BarLanding(), StandHeadingDegrees = BarLandingHeading(),
                Interior = null, LaneNodeName = "",
                Why = "the top of the beach where the bar comes ashore, by the bar road",
            },
            new RoutineStationEntry
            {
                Id = FuelStopId, Position = FuelStop(), StandHeadingDegrees = FuelStopHeading(),
                Interior = null, LaneNodeName = "",
                Why = "beside the gas hose nearest the store, fuel for the quad",
            },
        };

        // ---- the step: a text patch on the committed scene ------------------------------------------------------
        //
        // The creek cannot be rebuilt (its builder wipes the scene, and the scene carries later steps' work), and an
        // editor save re-serializes every document in it. So, as StoreWiringPatch and NmcBuildingShells do, the root is
        // written into the file as three new documents (GameObject, Transform, the station table) plus one line in the
        // SceneRoots list, through StPetersLayerRefresh's sealed patches. Nothing else in the file changes. A second
        // run finds the root and rewrites its table in place (an empty patch when nothing moved).

        public const string Step = "nmc-routine-stations";
        public const string PatchFolder = "artifacts/nmc-routine-stations";
        const string StationsScriptGuid = "b62134dab38776c42951df356e3588ca";   // RoutineStations.cs.meta

        public static LayerPatch Plan(SceneYaml scene)
        {
            Doc root = scene.Docs.SingleOrDefault(d => d.ClassId == 1 && !d.Stripped && d.Field("m_Name") == RootName &&
                                                       scene.TransformOf(d).FieldRef("m_Father") == 0);
            Doc rootTr = root != null ? scene.TransformOf(root) : null;
            if (rootTr != null) RequireIdentity(rootTr, RootName);
            Doc table = root == null ? null
                : scene.ComponentsOf(root).SingleOrDefault(d => d.ClassId == 114 && d.Text.Contains("guid: " + StationsScriptGuid));
            if (root != null && table == null) throw new Refusal($"{Step}: '{RootName}' stands without its station table.");

            var ids = new IdAllocator(scene);
            long go = root?.FileId ?? ids.Next($"{Step}/{RootName}/GameObject");
            long tr = rootTr?.FileId ?? ids.Next($"{Step}/{RootName}/Transform");
            long mb = table?.FileId ?? ids.Next($"{Step}/{RootName}/RoutineStations");
            var patch = new LayerPatch(Step, RootName, go);
            Put(patch, root, GameObjectDoc(go, tr, mb), RootName);
            Put(patch, rootTr, TransformDoc(tr, go), RootName + "/Transform");
            Put(patch, table, StationsDoc(mb, go, Stations()), RootName + "/RoutineStations");
            if (root == null)
            {
                Doc list = SceneRootsOf(scene);
                patch.Edit(list, WithRootListed(list.Text, tr), $"the scene's SceneRoots list ('{RootName}' joins it)");
                foreach (RoutineStationEntry s in Stations())
                    patch.Note(s.Id, "a routine station", $"({F(s.Position.x)}, {F(s.Position.y)}) facing {F(s.StandHeadingDegrees)}: {s.Why}");
            }
            return patch.Seal(scene);
        }

        [MenuItem("Hidden Harbours/World/Nine Mile Creek Buildings/Routine Stations (dry run)")]
        public static void DryRun() => Run(false);

        [MenuItem("Hidden Harbours/World/Nine Mile Creek Buildings/Routine Stations (apply)")]
        public static void Apply() => Run(true);

        /// <summary>The <c>-executeMethod</c> entry. Exits 1 on any refusal.</summary>
        public static void ApplyBatch() => HeronViewRoutinesBuilder.RunCli(() => Run(true));

        /// <summary>Plan (and with <paramref name="apply"/> write) the patch on the committed scene. Refuses an open
        /// scene: the editor could later overwrite the file.</summary>
        public static LayerPatch Run(bool apply)
        {
            string path = StoreWiringPatch.NineMileCreekScenePath;
            if (apply)
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).path == path)
                        throw new Refusal($"{path} is open. Close it before applying text patches.");
            string before = File.ReadAllText(path);
            LayerPatch patch = Plan(SceneYaml.Parse(before));
            string after = patch.ApplyTo(before);
            Directory.CreateDirectory(PatchFolder);
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(PatchFolder, $"00.{Step}.{RootName}.yaml"), patch.ToYaml(), utf8);
            if (apply && after != before) File.WriteAllText(path, after, utf8);
            if (apply) AssetDatabase.Refresh();
            Debug.Log($"[NmcRoutineStationsPatch] {(apply ? "Applied" : "Dry run")}:\n" + patch.Summary());
            return patch;
        }

        // ---- the documents, as Unity writes them ------------------------------------------------------------------

        const string Unlinked = "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" +
                                "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}";

        static string GameObjectDoc(long id, long tr, long mb) => string.Join("\n",
            $"--- !u!1 &{Id(id)}", "GameObject:", Unlinked, "  serializedVersion: 6", "  m_Component:",
            $"  - component: {Ref(tr)}", $"  - component: {Ref(mb)}", "  m_Layer: 0", "  m_Name: " + RootName,
            "  m_TagString: Untagged", "  m_Icon: {fileID: 0}", "  m_NavMeshLayer: 0", "  m_StaticEditorFlags: 0",
            "  m_IsActive: 1");

        static string TransformDoc(long id, long go) => string.Join("\n",
            $"--- !u!4 &{Id(id)}", "Transform:", Unlinked, $"  m_GameObject: {Ref(go)}", "  serializedVersion: 2",
            "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}", "  m_LocalPosition: {x: 0, y: 0, z: 0}",
            "  m_LocalScale: {x: 1, y: 1, z: 1}", "  m_ConstrainProportionsScale: 0", "  m_Children: []",
            "  m_Father: {fileID: 0}", "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}");

        static string StationsDoc(long id, long go, RoutineStationEntry[] rows)
        {
            var lines = new List<string>
            {
                $"--- !u!114 &{Id(id)}", "MonoBehaviour:", Unlinked, $"  m_GameObject: {Ref(go)}", "  m_Enabled: 1",
                "  m_EditorHideFlags: 0", $"  m_Script: {{fileID: 11500000, guid: {StationsScriptGuid}, type: 3}}",
                "  m_Name: ", "  m_EditorClassIdentifier: HiddenHarbours.World::HiddenHarbours.World.RoutineStations",
                "  _stations:",
            };
            foreach (RoutineStationEntry s in rows)
            {
                if (s.Interior != null) throw new Refusal($"{Step}: {s.Id} is indoors; this table holds outdoor stations only.");
                lines.Add("  - Id: " + Plain(s.Id));
                lines.Add($"    Position: {{x: {F(s.Position.x)}, y: {F(s.Position.y)}}}");
                lines.Add("    StandHeadingDegrees: " + F(s.StandHeadingDegrees));
                lines.Add("    Interior: {fileID: 0}");
                lines.Add("    OutOfSight: " + (s.OutOfSight ? "1" : "0"));
                lines.Add("    LaneNodeName: " + (string.IsNullOrEmpty(s.LaneNodeName) ? "" : Plain(s.LaneNodeName)));
                lines.Add("    Why: " + Plain(s.Why));
            }
            return string.Join("\n", lines);
        }

        static void Put(LayerPatch patch, Doc before, string after, string name)
        {
            if (before == null) patch.Add(after, name); else patch.Edit(before, after, name);
        }

        static string Id(long v) => v.ToString(CultureInfo.InvariantCulture);
        static string Ref(long v) => "{fileID: " + Id(v) + "}";
        static string F(float v) => v == 0f ? "0" : v.ToString("R", CultureInfo.InvariantCulture);

        static string Plain(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Trim() != s || s.Contains(": ") || s.Contains(" #") || s.Length > 72 ||
                "!&*|>'\"%@`#,[]{}-?:".IndexOf(s[0]) >= 0)
                throw new Refusal($"{Step}: '{s}' would need quoting or folding in the scene.");
            return s;
        }
    }
}
#endif
