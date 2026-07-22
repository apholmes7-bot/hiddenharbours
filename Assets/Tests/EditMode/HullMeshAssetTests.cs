using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The committed lobster hull-mesh asset (ADR 0022 phase 4)</b> — content validation with the
    /// same teeth as the visual-def tests: not "the code could bake one", but "the asset ON DISK,
    /// the one the game loads, is complete and carries the measured facts". Builder-generated assets
    /// go stale (see the punt saga); these fail the moment the committed def stops matching the rig
    /// facts the runtime depends on.
    /// </summary>
    public class HullMeshAssetTests
    {
        const string AssetPath = "Assets/_Project/Data/Boats/HullMeshes/LobsterBoatIsoHullMesh.asset";
        const string VisualPath = "Assets/_Project/Data/Boats/Visuals/LobsterBoatIso.asset";

        static HullMeshDef Load()
        {
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(AssetPath);
            Assert.IsNotNull(def, $"missing committed hull-mesh asset at {AssetPath} — run " +
                                  "Hidden Harbours ▸ Art ▸ 3D Hulls ▸ Bake Lobster Boat hull-mesh asset");
            return def;
        }

        [Test]
        public void LobsterHullMesh_IsUsable_AndCarriesTheRigFacts()
        {
            var def = Load();
            Assert.IsTrue(def.IsUsable(), "the committed def must be drawable end to end");
            Assert.AreEqual("hullmesh.lobster_boat_iso", def.Id, "stable id (append-only)");
            Assert.AreEqual("docs/art/rigs/lobsterBoatIsoRig.js", def.SourceRigPath);

            // The rig facts the runtime poses through — transcriptions, so exact.
            Assert.AreEqual(32, def.PxPerMetre, "PPU 32 everywhere (project convention)");
            Assert.AreEqual(40f, def.ElevationDeg, 1e-3f, "every boat rig bakes at 40°");
            Assert.AreEqual(2.8f, def.RockRollDegrees, 1e-3f, "the lobster rig's ROCK.rollA");
            Assert.AreEqual(1.6f, def.RockPitchDegrees, 1e-3f, "ROCK.pitchA");
            Assert.AreEqual(1.2f, def.RockHeavePixels, 1e-3f, "ROCK.heaveA");

            // The MEASURED azimuth convention: every boat rig probed so far turns CCW per +dir —
            // including hers (the SHEET is clockwise only because the sprite baker corrected at bake
            // time; the LIVE rig, which the mesh runs, is not corrected by anyone but this flag).
            Assert.IsTrue(def.AzimuthCounterClockwise,
                "the lobster rig measures COUNTER-CLOCKWISE — if this flipped, either the art " +
                "director changed the rig (re-verify by eye) or the probe broke. The mesh boat " +
                "would sail stern-first at E/W with this wrong.");
        }

        [Test]
        public void LobsterHullMesh_MeshSubAsset_IsRealGeometry()
        {
            var def = Load();
            Assert.IsNotNull(def.Mesh, "the mesh sub-asset");
            Assert.AreEqual(AssetPath, AssetDatabase.GetAssetPath(def.Mesh),
                "the mesh must be a SUB-ASSET of the def — a loose scene reference would not survive a commit");
            // ADR 0022's cost table: the lobster is 1,384 tris / ~350 faces. Pin loose bounds, not
            // exact counts — the art director may refine the rig; an empty or exploded mesh is the bug.
            Assert.That(def.Mesh.triangles.Length / 3, Is.InRange(500, 10000), "triangle count sanity");
            Assert.That(def.Mesh.vertexCount, Is.InRange(500, 30000), "vertex count sanity");
        }

        [Test]
        public void LobsterVisual_IsTheMeshVariant_WithHerCompassStillWired()
        {
            var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(VisualPath);
            Assert.IsNotNull(visual);
            Assert.AreEqual(BoatHullVariant.Mesh, visual.Variant,
                "phase 4 flips the lobster to the mesh variant — the owner mandate");
            Assert.IsTrue(visual.HasHullMesh(), "her HullMesh ref must be the usable committed def");
            Assert.AreSame(Load(), visual.HullMesh, "and point at THE committed asset");
            Assert.IsTrue(visual.HasFullCompass(),
                "her 32-facing sprite compass STAYS wired — she is the one hull with both " +
                "representations, and the dev A/B comparison (V at the helm) depends on it");
            Assert.IsTrue(visual.HasRockGrid(), "the sprite rock grid stays too (the A/B's rock side)");
        }

        [Test]
        public void HullMesh_CellGeometry_MatchesHerBakedSheet()
        {
            // The mesh and the sheet must agree on the cell, or the two representations draw at
            // different scales/offsets and the A/B comparison (and the acceptance test) is meaningless.
            var def = Load();
            var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Boats/LobsterBoatIso.png")
                                       .OfType<Sprite>().ToArray();
            Assert.IsNotEmpty(sprites, "her baked sheet must slice");
            var first = sprites[0];
            Assert.AreEqual(def.CellW, (int)first.rect.width,
                "sheet cell width ≠ mesh cell width — if the sheet is DOWNSCALED, check the texture " +
                "max-size cap (the >2048 import trap); if the rig changed, re-bake BOTH outputs");
            Assert.AreEqual(def.CellH, (int)first.rect.height, "sheet cell height ≠ mesh cell height");
            // The shared waterline pivot, in pixels from the top-left (sprite pivots are from the
            // BOTTOM-left, so flip y).
            Vector2 spritePivotTopLeft = new Vector2(first.pivot.x, first.rect.height - first.pivot.y);
            Assert.AreEqual(def.PivotPx.x, spritePivotTopLeft.x, 0.51f, "pivot x agrees across representations");
            Assert.AreEqual(def.PivotPx.y, spritePivotTopLeft.y, 0.51f, "pivot y agrees across representations");
        }
    }
}
