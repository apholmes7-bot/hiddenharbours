#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Vehicles;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.App.Editor
{
    public static partial class NineMileCreekWharfLandSide
    {
        // The Modern 3500's committed six-document authoring is the driveable-machine template.
        static void AddTruckDriveParts(SceneYaml scene, IdAllocator ids, string row, long go, ObjRef vehicle,
            List<long> components, List<(string name, string text)> parts)
        {
            Doc template = scene.RootNamed(NineMileCreekTruckPark.Modern3500Name);
            foreach (string kind in new[] { "body", "controller", "door" })
            {
                Doc source = scene.ComponentsOf(template).Single(d => kind == "body" ? d.ClassId == 50 :
                    d.IsScript("HiddenHarbours.Vehicles." + (kind == "controller" ? "VehicleController" : "VehicleDoor")));
                long id = Allocate(ids, RootName, row + "/" + kind);
                string text = Regex.Replace(source.Text, @"\A(--- !u!\d+ &)-?\d+", m => m.Groups[1].Value + Id(id));
                text = SetField(text, "m_GameObject", new ObjRef(go, null, 0).Yaml);
                if (kind == "controller") text = SetField(text, "_vehicle", vehicle.Yaml);
                components.Add(id); parts.Add((row + "/" + kind, text));
            }
        }

        static Doc BuyerObject(SceneYaml scene) => scene.Docs.Single(d =>
            d.ClassId == 1 && d.Field("m_Name") == NineMileCreekTrips.FishBuyerAssetName);

        sealed class BuyerRunCapture
        {
            public string[] Fields;
            public bool Dynamic;
        }

        // Inactive projections of the two named source objects keep the committed scene closed.
        // Place alone chooses the trip's bindings, routes and posts; generic serialization maps its
        // resulting references back to those exact source documents. No trip recipe is repeated here.
        static BuyerRunCapture CaptureBuyerRun(SceneYaml scene)
        {
            Doc truckDoc = scene.Docs.Single(d => d.ClassId == 1 && d.Field("m_Name") == BuyerRunRowId), personDoc = BuyerObject(scene);
            Doc machineDoc = scene.ComponentsOf(truckDoc).Single(d => d.IsScript("HiddenHarbours.Vehicles.ParkedVehicle"));
            Doc talkDoc = scene.ComponentsOf(personDoc).Single(d => d.IsScript("HiddenHarbours.World.Interactable"));
            Doc spriteDoc = scene.ComponentsOf(personDoc).Single(d => d.ClassId == 212);
            var truck = new GameObject(truckDoc.Field("m_Name")) { hideFlags = HideFlags.HideAndDontSave };
            var person = new GameObject(personDoc.Field("m_Name")) { hideFlags = HideFlags.HideAndDontSave };
            truck.SetActive(false); person.SetActive(false);
            try
            {
                string guid = Regex.Match(machineDoc.Field("_vehicle") ?? "", @"guid: ([0-9a-f]{32})").Groups[1].Value;
                var vehicle = AssetDatabase.LoadAssetAtPath<VehicleDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (vehicle == null) throw new Refusal("The buyer truck's committed VehicleDef is unavailable.");
                var machine = truck.AddComponent<ParkedVehicle>();
                machine.Configure(vehicle, machineDoc.Field("_drivable") == "1");
                var talk = person.AddComponent<Interactable>();
                var sprite = person.AddComponent<SpriteRenderer>();
                var sort = person.AddComponent<YSortSprite>();
                var local = new Dictionary<UnityEngine.Object, long>
                {
                    [truck] = truckDoc.FileId, [truck.transform] = scene.TransformOf(truckDoc).FileId,
                    [machine] = machineDoc.FileId, [person] = personDoc.FileId,
                    [person.transform] = scene.TransformOf(personDoc).FileId,
                    [talk] = talkDoc.FileId, [sprite] = spriteDoc.FileId,
                };
                ScheduledTrip trip = NineMileCreekTrips.Place(truck, new[] { talk });
                if (trip == null) throw new Refusal("NineMileCreekTrips.Place refused the buyer's run; see its warning.");
                string Reference(UnityEngine.Object value)
                {
                    if (value == null) return "{fileID: 0}";
                    if (local.TryGetValue(value, out long fileId)) return "{fileID: " + Id(fileId) + "}";
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string assetGuid, out long assetId))
                        return new ObjRef(assetId, assetGuid, 2).Yaml;
                    throw new Refusal("Place produced an unbound scene reference: " + value.name);
                }
                var fields = new List<string>();
                var serialized = new SerializedObject(trip);
                SerializedProperty property = serialized.GetIterator();
                bool enter = true;
                while (property.NextVisible(enter))
                {
                    enter = false;
                    if (!property.name.StartsWith("_", StringComparison.Ordinal)) continue;
                    string prefix = "  " + property.name + ": ";
                    if (property.isArray && property.propertyType != SerializedPropertyType.String)
                    {
                        fields.Add(prefix.TrimEnd() + (property.arraySize == 0 ? " []" : ""));
                        for (int i = 0; i < property.arraySize; i++)
                        {
                            var item = property.GetArrayElementAtIndex(i);
                            if (item.propertyType != SerializedPropertyType.Vector2)
                                throw new Refusal("Unsupported trip array from Place: " + property.name);
                            fields.Add("  - " + Vec2Yaml(item.vector2Value));
                        }
                    }
                    else if (property.propertyType == SerializedPropertyType.ObjectReference) fields.Add(prefix + Reference(property.objectReferenceValue));
                    else if (property.propertyType == SerializedPropertyType.Vector2) fields.Add(prefix + Vec2Yaml(property.vector2Value));
                    else throw new Refusal("Unsupported serialized trip field from Place: " + property.name);
                }
                return new BuyerRunCapture { Fields = fields.ToArray(), Dynamic = sort.Dynamic };
            }
            finally { UnityEngine.Object.DestroyImmediate(truck); UnityEngine.Object.DestroyImmediate(person); }
        }
    }
}
#endif
