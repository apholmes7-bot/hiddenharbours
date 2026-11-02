#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>The measured walking top and the retained terrain datum of the landing picture.</summary>
    public static class StPetersLandingLayout
    {
        static readonly object Contract = MiniJson.Parse(File.ReadAllText(StPetersLandingDeckBaker.ContractPath));
        static readonly Dictionary<string, object> Surface = MiniJson.Dict(Contract, "surface");
        public const string PicturePath = StPetersLandingDeckBaker.SheetPath;
        public const string HeightPath = StPetersLandingDeckBaker.HeightPath;
        public static (float originY, float pivotY, float pixelsPerUnit, Vector3 heightDecode) Picture =>
            (MiniJson.Float(Contract, "originY"), MiniJson.Float(Contract, "pivotY"), MiniJson.Float(Contract, "pixelsPerUnit"),
             new Vector3(MiniJson.Float(Contract, "heightMinRigZ"), MiniJson.Float(Contract, "heightRange"), MiniJson.Float(Contract, "datumShift")));
        static float Number(string key) => MiniJson.Float(Surface, key);
        public static Rect Floor => new Rect(Number("x"), Number("y"), Number("width"), Number("depth"));
        public static Vector2 Datum => new Vector2(Number("datumX"), Number("datumY"));
        public static float DatumElevation => Number("datumElevation");
        public static Vector2 Entry => new Vector2(Floor.xMin + Number("entryInset"), Floor.center.y);
        public static float StandingInset => Number("standingInset");
        public static float LadderInset => Number("ladderInset");
        public static float CleatInset => Number("cleatInset");
        public static float LampRootInset => Number("lampRootInset");
        public static Vector2 SouthPocketFrom => new Vector2(Number("southPocketFromX"), Number("southPocketY"));
        public static Vector2 SouthPocketTo => new Vector2(Number("southPocketToX"), Number("southPocketY"));
        public static Vector2 Basil => new Vector2(Floor.xMax - Number("basilEndInset"), Floor.yMax - Number("basilNorthInset"));

        // The painted road remains fixed. Only its last walking leg takes the dry connector.
        public static Vector2[] WestEntryWalk()
        {
            var road = StPetersStarterSplat.VillageToSlipPath();
            var result = new List<Vector2>();
            for (int i = 0; i < road.Length - 1; i++) result.Add(road[i]);
            foreach (object item in MiniJson.List(Surface, "westConnector"))
            {
                result.Add(new Vector2(MiniJson.Float(item, "x"), MiniJson.Float(item, "y")));
            }
            result.Add(Entry);
            return result.ToArray();
        }

        public static IEnumerable<Vector2> WalkingSamples(float spacing = 1f)
        {
            Rect floor = Floor;
            for (float y = floor.yMin + StandingInset; y < floor.yMax; y += spacing)
            for (float x = floor.xMin + StandingInset; x < floor.xMax; x += spacing)
                yield return new Vector2(x, y);
        }
    }
}
#endif
