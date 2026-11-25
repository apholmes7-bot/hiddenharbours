using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// A bolted fitting reaches the screen only through its hull's composite window, so the window must
    /// cover the fitting's own cell: the skiff outboard's cell is 272 px against the console skiff's
    /// 244 px hull, and on east and west headings the cowl was cut off square at the hull's cell edge.
    /// </summary>
    public sealed class HullFittingWindowTests
    {
        const string HullPath = "Assets/_Project/Data/Boats/HullMeshes/ConsoleIsoHullMesh.asset";
        const string MotorPath = "Assets/_Project/Data/Boats/HullProps/SkiffMotorWorkPropMesh.asset";
        readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects) if (o != null) Object.DestroyImmediate(o);
            _objects.Clear();
        }

        IsoFacetHullRenderer Hull()
        {
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(HullPath);
            Assert.That(def, Is.Not.Null, HullPath);
            var go = new GameObject("fitting window fixture");
            _objects.Add(go);
            var hull = go.AddComponent<IsoFacetHullRenderer>();
            hull.Configure(new IsoFacetHullSetup {
                Mesh = def.Mesh, DoorLeafClosed = def.DoorLeafClosed, DoorLeafOpen = def.DoorLeafOpen,
                Ramps = def.Ramps.Select(r => r.Colors).ToArray(), RampOffsets = def.Ramps.Select(r => r.Offset).ToArray(),
                InteriorRamps = (def.InteriorRamps ?? Array.Empty<HullMeshDef.Ramp>()).Select(r => r.Colors).ToArray(),
                InteriorRampOffsets = (def.InteriorRamps ?? Array.Empty<HullMeshDef.Ramp>()).Select(r => r.Offset).ToArray(),
                LightN = def.LightN, Gain = def.Gain, Bias = def.Bias, Bayer16 = def.Bayer16,
                Keyline = def.Keyline, PivotPx = def.PivotPx, PxPerMetre = def.PxPerMetre,
                CellW = def.CellW, CellH = def.CellH, ElevationDeg = def.ElevationDeg });
            return hull;
        }

        static Bounds Window(IsoFacetHullRenderer hull) => hull.OverlayRenderer.GetComponent<MeshFilter>().sharedMesh.bounds;

        IsoFacetPropRenderer Bolt(IsoFacetHullRenderer hull, HullPropMeshDef def, string slot)
        {
            var go = new GameObject(slot);
            go.transform.SetParent(hull.PosedMesh, false);
            var prop = go.AddComponent<IsoFacetPropRenderer>();
            prop.Configure(IsoFacetHullPresentationService.ToPropSetup(def));
            return prop;
        }

        [Test]
        public void TheWindowCoversTheOutboardsCell_NotJustTheHulls()
        {
            var hullDef = AssetDatabase.LoadAssetAtPath<HullMeshDef>(HullPath);
            var motor = AssetDatabase.LoadAssetAtPath<HullPropMeshDef>(MotorPath);
            Assert.That(motor, Is.Not.Null, MotorPath);
            Assert.That(motor.CellW, Is.GreaterThan(hullDef.CellW), "the premise: the engine's cell is wider than the hull's");

            var hull = Hull();
            float ppu = hullDef.PxPerMetre;
            Bounds bare = Window(hull);
            Assert.That(bare.size.x, Is.EqualTo((hullDef.CellW + 2f) / ppu).Within(1e-4f), "a bare hull keeps its own cell + the keyline pixel");

            Bolt(hull, motor, "MotorA");
            Bounds fitted = Window(hull);
            float mppu = motor.PxPerMetre;
            Assert.That(fitted.min.x, Is.LessThanOrEqualTo(-motor.PivotPx.x / mppu - 1f / ppu + 1e-4f), "the engine's left edge is inside the window");
            Assert.That(fitted.max.x, Is.GreaterThanOrEqualTo((motor.CellW - motor.PivotPx.x) / mppu + 1f / ppu - 1e-4f), "the engine's right edge is inside the window");
            Assert.That(fitted.max.y, Is.GreaterThanOrEqualTo(bare.max.y - 1e-4f), "and nothing of the hull's own cell was given up");
            Assert.That(fitted.min.y, Is.LessThanOrEqualTo(bare.min.y + 1e-4f));
        }

        [Test]
        public void ATwinsClampOffset_WidensTheWindowByThatMuch()
        {
            var motor = AssetDatabase.LoadAssetAtPath<HullPropMeshDef>(MotorPath);
            var hull = Hull();
            var prop = Bolt(hull, motor, "MotorA");
            float before = Window(hull).max.x;

            prop.LateralOffsetMeters = 0.34f;
            prop.EnsureWindow();
            Assert.That(Window(hull).max.x, Is.EqualTo(before + 0.34f).Within(1e-4f));
        }

        [Test]
        public void TheWindowOnlyGrows()
        {
            var hull = Hull();
            hull.EnsureFittingCell(new Vector2(200f, 150f), 400, 300, 32, 0f);
            Bounds big = Window(hull);
            hull.EnsureFittingCell(new Vector2(10f, 10f), 20, 20, 32, 0f);
            Assert.That(Window(hull).size, Is.EqualTo(big.size), "a smaller fitting never takes room from a bigger one");
        }
    }
}
