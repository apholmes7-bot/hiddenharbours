#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>No plant grows on the trolley stage, over the line's last run, or in the net shed's doorway.</b> The
    /// plant field draws every plant by its root's y, and the stage, the line and the shed's landing are floor
    /// pieces drawn under everything that stands, so a plant rooted on them, or just in front of them, paints over
    /// the deck, the cart and the winch, or stands in the shed's doors.
    ///
    /// <para>The clearing is the stage's drawn footprint grown by <see cref="StagePadMetres"/> on every side, and
    /// a corridor <see cref="LineHalfWidthMetres"/> either side of the line's last <see cref="LineClearMetres"/>
    /// as drawn, and the doorway: the shed's ramp and its landing, each drawn and grown by
    /// <see cref="DoorwayPadMetres"/>. All are read from the key scenes, the stage's and the landing's Defs and the
    /// shed's sidecar, so the clearing follows the pieces if they move. The field's authored sites inside it are removed and its ground grid is closed (<c>255</c>)
    /// under it, so the meadow recipe never seeds there. Idempotent: a second run changes nothing.</para>
    /// </summary>
    public static class StPetersTrolleyStageClearance
    {
        public const string SceneId = "keyscene.stp_cannery";
        public const string StageId = "structure.stp_cannery_trolley_stage";
        public const string LineId = "structure.stp_cannery_trolley_line";
        public const string StageDefPath = "Assets/_Project/Data/SetPieces/SetPiece_trolleyStage.asset";
        public const string DoorwaySceneId = "keyscene.stp_east_end";
        public const string ShedId = "structure.stp_net_shed";
        public const string LandingId = "structure.stp_net_shed_landing";
        public const string LandingDefPath = "Assets/_Project/Data/SetPieces/SetPiece_shedLanding.asset";
        public const string ShedSidecarPath = "docs/art/rigs/wharf-building-kit-v2/gameplay/wharfBuilding2.netShed.gameplay.json";
        public const string RampFootprintId = "ramp";

        /// <summary>Bare ground kept round the stage's drawn footprint, in metres: room for a man to stand at
        /// the edge, and enough that a knee-high plant rooted in front of the stage does not reach its deck.</summary>
        public const float StagePadMetres = 1f;

        /// <summary>How much of the line, back from where it meets the stage, is kept clear, in drawn metres:
        /// the last two runs, where the cart rolls on and the eye follows the rails onto the deck.</summary>
        public const float LineClearMetres = 6f;

        /// <summary>The corridor kept clear either side of the rails' centre line, in metres: the 0.6 m gauge,
        /// the sleepers' ends and a knee-high plant's reach.</summary>
        public const float LineHalfWidthMetres = 0.9f;

        /// <summary>Bare ground kept round the shed's ramp and its landing as drawn, in metres: enough that a
        /// knee-high plant rooted beside the boards does not lean into the doors or across the step.</summary>
        public const float DoorwayPadMetres = 0.3f;

        /// <summary>The clearing, drawn: a box round the stage and a run of line.</summary>
        public sealed class Clearing
        {
            public Rect Stage;
            public List<Vector2> Line = new List<Vector2>();
            /// <summary>The net shed's doorway, drawn and padded: its ramp, then its landing.</summary>
            public List<Rect> Doorway = new List<Rect>();

            static bool InRect(Rect s, Vector2 at, float pad) =>
                at.x >= s.xMin - pad && at.x <= s.xMax + pad && at.y >= s.yMin - pad && at.y <= s.yMax + pad;

            public bool Contains(Vector2 at, float pad = 0f)
            {
                if (InRect(Stage, at, pad)) return true;
                foreach (Rect d in Doorway) if (InRect(d, at, pad)) return true;
                for (int i = 0; i + 1 < Line.Count; i++)
                    if (DistanceToSegment(at, Line[i], Line[i + 1]) <= LineHalfWidthMetres + pad) return true;
                return false;
            }

            public Rect Bounds(float pad)
            {
                float xMin = Stage.xMin, xMax = Stage.xMax, yMin = Stage.yMin, yMax = Stage.yMax;
                foreach (var p in Line)
                {
                    xMin = Mathf.Min(xMin, p.x - LineHalfWidthMetres); xMax = Mathf.Max(xMax, p.x + LineHalfWidthMetres);
                    yMin = Mathf.Min(yMin, p.y - LineHalfWidthMetres); yMax = Mathf.Max(yMax, p.y + LineHalfWidthMetres);
                }
                foreach (Rect d in Doorway)
                {
                    xMin = Mathf.Min(xMin, d.xMin); xMax = Mathf.Max(xMax, d.xMax);
                    yMin = Mathf.Min(yMin, d.yMin); yMax = Mathf.Max(yMax, d.yMax);
                }
                return Rect.MinMaxRect(xMin - pad, yMin - pad, xMax + pad, yMax + pad);
            }
        }

        static float Depth => Mathf.Sin(SpriteLightMath.CameraElevationDeg * Mathf.Deg2Rad);

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>The clearing as the key scene and the stage's Def place it today.</summary>
        public static Clearing Read()
        {
            KeySceneDef scene = StPetersLayerRefresh.LoadStPetersKeyScenes().Single(s => s.Id == SceneId);
            KeyScenePiece stage = scene.Pieces.Single(p => p.Id == StageId);
            KeyScenePiece line = scene.Pieces.Single(p => p.Id == LineId);
            var def = AssetDatabase.LoadAssetAtPath<SetPieceDef>(StageDefPath);
            if (def == null) throw new InvalidOperationException(StageDefPath + " does not load.");

            var clearing = new Clearing();
            var corners = new[]
            {
                new Vector2(def.BoundsMin.x, def.BoundsMin.y), new Vector2(def.BoundsMax.x, def.BoundsMin.y),
                new Vector2(def.BoundsMax.x, def.BoundsMax.y), new Vector2(def.BoundsMin.x, def.BoundsMax.y),
            }.Select(c => { Vector2 w = SetPieceDef.ToWorld(c, stage.Dir); return stage.At + new Vector2(w.x, w.y * Depth); }).ToArray();
            clearing.Stage = Rect.MinMaxRect(corners.Min(c => c.x) - StagePadMetres, corners.Min(c => c.y) - StagePadMetres,
                                             corners.Max(c => c.x) + StagePadMetres, corners.Max(c => c.y) + StagePadMetres);

            string path = line.Options.Single(o => o.Key == "path").Value;
            var drawn = new List<Vector2>();
            foreach (Match m in Regex.Matches(path, @"\[\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*\]"))
                drawn.Add(line.At + new Vector2(float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                                                float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) * Depth));
            if (drawn.Count < 2) throw new InvalidOperationException(LineId + "'s path has no run.");
            // Walk back from the stage end until LineClearMetres of drawn line are covered.
            float left = LineClearMetres;
            clearing.Line.Add(drawn[drawn.Count - 1]);
            for (int i = drawn.Count - 1; i > 0 && left > 0f; i--)
            {
                float run = Vector2.Distance(drawn[i], drawn[i - 1]);
                if (run >= left) { clearing.Line.Add(Vector2.Lerp(drawn[i], drawn[i - 1], left / run)); left = 0f; }
                else { clearing.Line.Add(drawn[i - 1]); left -= run; }
            }
            clearing.Doorway.AddRange(ReadDoorway());
            return clearing;
        }

        [Serializable] sealed class ShedSidecar { public Footprint[] FOOTPRINT; }
        [Serializable] sealed class Footprint { public string id; public float x0, x1, y0, y1; public bool walk; }

        /// <summary>A local rectangle (x0..x1, y0..y1) placed at <paramref name="at"/> turned by <paramref name="dir"/>,
        /// as drawn, grown by <paramref name="pad"/>.</summary>
        public static Rect Drawn(Vector2 at, int dir, float x0, float x1, float y0, float y1, float pad)
        {
            var corners = new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) }
                .Select(c => { Vector2 w = SetPieceDef.ToWorld(c, dir); return at + new Vector2(w.x, w.y * Depth); }).ToArray();
            return Rect.MinMaxRect(corners.Min(c => c.x) - pad, corners.Min(c => c.y) - pad,
                                   corners.Max(c => c.x) + pad, corners.Max(c => c.y) + pad);
        }

        /// <summary>The shed's ramp, local, from its sidecar's footprint.</summary>
        public static Rect ShedRampLocal()
        {
            string path = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, ShedSidecarPath);
            var sidecar = JsonUtility.FromJson<ShedSidecar>(System.IO.File.ReadAllText(path));
            Footprint ramp = sidecar.FOOTPRINT?.SingleOrDefault(f => f.id == RampFootprintId)
                             ?? throw new InvalidOperationException(ShedSidecarPath + " has no ramp footprint.");
            return Rect.MinMaxRect(ramp.x0, ramp.y0, ramp.x1, ramp.y1);
        }

        /// <summary>The doorway as the east end's key scene places the shed and its landing: the ramp, then the
        /// landing, each drawn and grown by <see cref="DoorwayPadMetres"/>.</summary>
        public static Rect[] ReadDoorway()
        {
            KeySceneDef scene = StPetersLayerRefresh.LoadStPetersKeyScenes().Single(s => s.Id == DoorwaySceneId);
            KeyScenePiece shed = scene.Pieces.Single(p => p.Id == ShedId);
            KeyScenePiece landing = scene.Pieces.Single(p => p.Id == LandingId);
            var def = AssetDatabase.LoadAssetAtPath<SetPieceDef>(LandingDefPath);
            if (def == null) throw new InvalidOperationException(LandingDefPath + " does not load.");
            Rect ramp = ShedRampLocal();
            return new[]
            {
                Drawn(shed.At, shed.Dir, ramp.xMin, ramp.xMax, ramp.yMin, ramp.yMax, DoorwayPadMetres),
                Drawn(landing.At, landing.Dir, def.BoundsMin.x, def.BoundsMax.x, def.BoundsMin.y, def.BoundsMax.y, DoorwayPadMetres),
            };
        }

        /// <summary>
        /// Remove the authored plants inside the clearing and close the ground grid under it. Batch:
        /// <c>-executeMethod HiddenHarbours.App.Editor.StPetersTrolleyStageClearance.ApplyToField</c>.
        /// </summary>
        public static void ApplyToField()
        {
            Clearing clearing = Read();
            var definition = AssetDatabase.LoadAssetAtPath<PlantFieldDefinition>(StPetersBuildingClearance.DefinitionPath);
            var ground = AssetDatabase.LoadAssetAtPath<PlantFieldGroundData>(StPetersBuildingClearance.GroundPath);
            var log = new StringBuilder();

            var removed = definition.Authored.Where(row => clearing.Contains(row.At)).ToArray();
            foreach (var row in removed) log.AppendLine($"removed {row.Id} at ({row.At.x}, {row.At.y})");
            if (removed.Length > 0)
            {
                var ids = new HashSet<string>(removed.Select(r => r.Id));
                definition.Authored = definition.Authored.Where(row => !ids.Contains(row.Id)).ToArray();
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
            }

            float cell = definition.CellSize, halfDiagonal = cell * 0.70710678f;
            byte[] map = ground.Decode();
            Rect box = clearing.Bounds(cell);
            int x0 = Mathf.Max(0, Mathf.FloorToInt((box.xMin - definition.Origin.x) / cell));
            int x1 = Mathf.Min(ground.Width - 1, Mathf.FloorToInt((box.xMax - definition.Origin.x) / cell));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((box.yMin - definition.Origin.y) / cell));
            int y1 = Mathf.Min(ground.Height - 1, Mathf.FloorToInt((box.yMax - definition.Origin.y) / cell));
            int closed = 0;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var centre = new Vector2(definition.Origin.x + (x + 0.5f) * cell, definition.Origin.y + (y + 0.5f) * cell);
                if (!clearing.Contains(centre, halfDiagonal)) continue;
                int at = y * ground.Width + x;
                if (map[at] == 255) continue;
                map[at] = 255; closed++;
            }
            if (closed > 0)
            {
                ground.Payload = StPetersBuildingClearance.Encode(map);
                EditorUtility.SetDirty(ground);
                AssetDatabase.SaveAssetIfDirty(ground);
            }
            log.AppendLine($"total {removed.Length} authored plant(s) removed, {closed} ground cell(s) closed; {definition.Authored.Length} authored remain");
            Debug.Log("[StPetersTrolleyStageClearance]\n" + log);
        }
    }
}
#endif
