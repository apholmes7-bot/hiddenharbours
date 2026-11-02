#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.App.Editor
{
    public static partial class NineMileCreekWharfLandSide
    {
        public static LayerPatch MoveHydrant(SceneYaml scene)
        {
            Doc go = scene.Require(637725247, "the yard hydrant");
            if (go.Field("m_Name") != "yardHydrant_15") throw new Refusal("The yard hydrant's identity changed.");
            Doc tr = scene.TransformOf(go);
            Vector2 at = NineMileCreekDressing.Services().Single(p => p.Key == "yardHydrant").Position;
            var patch = new LayerPatch(PlacementStep, "NineMileCreekDressing/yardHydrant_15", go.FileId);
            patch.Edit(tr, SetField(tr.Text, "m_LocalPosition", Vec3Yaml(at.x, at.y, 0)), "yardHydrant_15/north of west parking");
            return patch.Seal(scene);
        }

        // Ruling 13 withdraws the run; the original machine stays drivable in its row's bay.
        public static LayerPatch PlaceBuyerTruck(SceneYaml scene)
        {
            Doc go = scene.RootNamed(NineMileCreekTruckPark.TruckName);
            Doc tr = scene.TransformOf(go);
            var patch = new LayerPatch(PlacementStep, NineMileCreekTruckPark.TruckName, go.FileId);
            Vector3 at = BuyerTruck;
            patch.Edit(tr, SetField(tr.Text, "m_LocalPosition", Vec3Yaml(at.x, at.y, at.z)), "DuallyAtThePark/buyer row bay");
            var trips = scene.ComponentsOf(go).Where(d => d.IsScript("HiddenHarbours.Vehicles.ScheduledTrip")).ToArray();
            if (trips.Length > 1) throw new Refusal("The buyer truck carries more than one run.");
            if (trips.Length == 1)
            {
                Doc trip = trips[0];
                patch.Delete(trip, "DuallyAtThePark/withdrawn buyer run");
                string list = "  m_Component:\n" + string.Join("\n", scene.ComponentsOf(go).Where(d => d.FileId != trip.FileId)
                    .Select(d => "  - component: {fileID: " + Id(d.FileId) + "}"));
                patch.Edit(go, Regex.Replace(go.Text, @"(?m)^  m_Component:(?:\n  - component: [^\n]*)*", _ => list),
                    "DuallyAtThePark/components after withdrawn run");
            }
            return patch.Seal(scene);
        }
    }
}
#endif
