using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Only this fixture adapter touches Unity. The same fixtures run against YAML stand-ins on .NET 8.</summary>
    public sealed class StPetersVillageTestData
    {
        public const string Folder = "Assets/_Project/Data/Regions/StPetersVillage";
        public VillagePlanDef Plan;
        public LotDef[] Lots;
        public YardDef[] Yards;
        public RouteDef[] Routes;
        public CommonsDef[] Commons;
        public LightPostDef[] Lights;
        public LampShadowProfile Budget;

        public static T[] Read<T>() where T : UnityEngine.Object => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { Folder })
            .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).ToArray();

        public static StPetersVillageTestData Load()
        {
            // HEADLESS_LOAD_BEGIN: the offline runner substitutes this adapter in a scratch copy only.
            return new StPetersVillageTestData
            {
                Plan = Read<VillagePlanDef>().Single(), Lots = Read<LotDef>(), Yards = Read<YardDef>(),
                Routes = Read<RouteDef>(), Commons = Read<CommonsDef>(), Lights = Read<LightPostDef>(),
                Budget = AssetDatabase.LoadAssetAtPath<LampShadowProfile>("Assets/_Project/Resources/LampShadowProfile.asset")
            };
            // HEADLESS_LOAD_END
        }

        public VillagePlanDerivation.Result Derive() => VillagePlanDerivation.Derive(Plan, Lots, Yards, Routes, Lights);
        public float T(string name) => VillagePlanDerivation.Tunable(Plan, name);
        public static void Check(bool condition, int guard, string name, string explanation) =>
            Assert.That(condition, Is.True, $"V1 guard {guard}: {name}: {explanation}");
    }
}
