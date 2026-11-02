using System.Collections.Generic;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public sealed class PlantShadowGround : MonoBehaviour, IPlantFieldGround
    {
        public float Water;
        public int PlantSeason => 1;
        public PlantGroundSample Sample(float x, float y) => new PlantGroundSample(-1, 0, 1, true);
        public float WaterLevelAt(float x, float y) => Water;
    }

    public class PlantFieldSunShadowTests
    {
        readonly List<Object> _owned = new List<Object>();
        Vector4 _sun;
        float _elevation, _strength;

        [SetUp] public void SetUp()
        {
            _sun = Shader.GetGlobalVector("_SunDir");
            _elevation = Shader.GetGlobalFloat("_SunElevation"); _strength = Shader.GetGlobalFloat("_ShadowStrength");
            Shader.SetGlobalVector("_SunDir", new Vector4(-1, 0, 0, 0));
            Shader.SetGlobalFloat("_SunElevation", 0.5f); Shader.SetGlobalFloat("_ShadowStrength", 1);
        }
        [TearDown] public void TearDown()
        {
            for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            Shader.SetGlobalVector("_SunDir", _sun);
            Shader.SetGlobalFloat("_SunElevation", _elevation); Shader.SetGlobalFloat("_ShadowStrength", _strength);
        }
        T Own<T>(T item) where T : Object { _owned.Add(item); return item; }

        PlantField Field(int sites = 2, bool dormant = false, float strength = 1, float water = 0)
        {
            var texture = Own(new Texture2D(64, 128));
            var species = Own(ScriptableObject.CreateInstance<PlantSpeciesDef>());
            species.Code = 1; species.Family = "grass";
            species.Pages = new[] { new PlantSheetPage { Albedo = texture, Normal = texture, Light = texture, Detail = texture } };
            species.Cells = new[] { new PlantCell { Stage = 1, Season = 1, Variant = 0, Tide = 0,
                Rect = new Rect(0, 0, 32, 64), Pivot = new Vector2(16, 0), HeightM = 2, Dormant = dormant } };
            var definition = Own(ScriptableObject.CreateInstance<PlantFieldDefinition>());
            Assert.That(definition.SunShadowStrength, Is.EqualTo(1));
            definition.Width = definition.Height = 1; definition.CellSize = 0.5f;
            definition.GroundMetresPerY = 1; definition.ChunkWidth = 8; definition.SlowTickSeconds = 0.5f;
            definition.WindMargin = 0.75f; definition.SunShadowStrength = strength;
            definition.Species = new[] { species };
            var rows = new List<PlantFieldSite>();
            for (int i = 0; i < sites; i++) rows.Add(new PlantFieldSite { Id = "shadow-fixture-" + i,
                At = new Vector2(2, 2 + i * 2), Species = 1, Stage = 1, Variant = 0 });
            definition.Authored = rows.ToArray();
            var real = AssetDatabase.LoadAssetAtPath<PlantFieldDefinition>("Assets/_Project/Data/Plants5/StPetersPlantField.asset");
            Assert.That(real, Is.Not.Null); Assert.That(real.Material, Is.Not.Null);
            definition.Material = Own(new Material(real.Material));
            var host = Own(new GameObject("PlantShadowFixture"));
            var ground = host.AddComponent<PlantShadowGround>(); ground.Water = water;
            var field = host.AddComponent<PlantField>(); field.Definition = definition; field.GroundSource = ground;
            field.Rebuild(); return field;
        }

        [Test] public void DifferentYOrders_ShareOneEightVertexShadowDraw()
        {
            var field = Field();
            Assert.That(field.ChunkCount, Is.EqualTo(2));
            Assert.That(field.ShadowDrawCount, Is.EqualTo(1));
            var filters = field.ShadowRoot.GetComponentsInChildren<MeshFilter>();
            Assert.That(filters, Has.Length.EqualTo(1)); Assert.That(filters[0].sharedMesh.vertexCount, Is.EqualTo(8));
            var cells = new List<Vector4>(); filters[0].sharedMesh.GetUVs(2, cells);
            Assert.That(cells, Has.Count.EqualTo(8));
            foreach (var rect in cells) Assert.That(rect, Is.EqualTo(new Vector4(0, 0, 0.5f, 0.5f)));
        }

        [Test] public void WaterOverRoot_RemovesAndRestoresOnlyItsShadow()
        {
            var field = Field(1); var ground = field.GetComponent<PlantShadowGround>();
            var shadow = field.ShadowRoot.GetComponentInChildren<MeshFilter>().sharedMesh;
            Mesh plant = null;
            foreach (var mesh in field.GetComponentsInChildren<MeshFilter>())
                if (mesh.transform.parent == field.transform) plant = mesh.sharedMesh;
            Assert.That(plant, Is.Not.Null); Assert.That(field.ShadowDrawCount, Is.EqualTo(1));
            ground.Water = 2; field.RefreshAppearance();
            Assert.That(field.ShadowDrawCount, Is.Zero); Assert.That(shadow.vertexCount, Is.Zero);
            ground.Water = 0; field.RefreshAppearance();
            Assert.That(field.ShadowDrawCount, Is.EqualTo(1)); Assert.That(shadow.vertexCount, Is.EqualTo(4));
            Assert.That(field.ChunkCount, Is.EqualTo(1));
            Assert.That(field.ShadowRoot.GetComponentInChildren<MeshFilter>().sharedMesh, Is.SameAs(shadow));
            foreach (var mesh in field.GetComponentsInChildren<MeshFilter>())
                if (mesh.transform.parent == field.transform) Assert.That(mesh.sharedMesh, Is.SameAs(plant));
        }

        [Test] public void DormantCell_HasNoShadow()
        {
            var field = Field(1, true);
            Assert.That(field.ShadowDrawCount, Is.Zero); Assert.That(field.ChunkCount, Is.Zero);
            Assert.That(PlantField.CanCastSun(field.Species[0].Cells[0], -1), Is.False);
        }

        [Test] public void InitiallyCoveredRoot_CastsWhenTheTideDrops()
        {
            var field = Field(1, false, 1, 2);
            Assert.That(field.ShadowDrawCount, Is.Zero);
            field.GetComponent<PlantShadowGround>().Water = 0; field.RefreshAppearance();
            Assert.That(field.ShadowDrawCount, Is.EqualTo(1));
            Assert.That(field.ShadowRoot.GetComponentInChildren<MeshFilter>().sharedMesh.vertexCount, Is.EqualTo(4));
        }

        [Test] public void ZeroStrength_BuildsNoShadowMeshes_AndKeepsTheTwoPlantChunks()
        {
            var field = Field(2, false, 0);
            Assert.That(field.ShadowDrawCount, Is.Zero); Assert.That(field.ShadowRoot, Is.Null);
            Assert.That(field.ChunkCount, Is.EqualTo(2));
        }

        [Test] public void RootCoverageRule_AcceptsDryAndRejectsMissingOrWetCells()
        {
            var cell = new PlantCell();
            Assert.That(PlantField.CanCastSun(null, -1), Is.False);
            Assert.That(PlantField.CanCastSun(cell, 0), Is.True);
            Assert.That(PlantField.CanCastSun(cell, -0.01f), Is.True);
            Assert.That(PlantField.CanCastSun(cell, 0.01f), Is.False);
        }

        [Test] public void Material_UsesTheSameShadeStencilAndBlend()
        {
            var field = Resources.Load<Material>("PlantFieldSunShadow");
            var sprite = Resources.Load<Material>("SpriteShadowShade");
            Assert.That(field, Is.Not.Null); Assert.That(sprite, Is.Not.Null);
            foreach (string name in new[] { "_StencilRef", "_StencilComp", "_StencilPass", "_ScreenShade", "_SrcBlend", "_DstBlend" })
                Assert.That(field.GetFloat(name), Is.EqualTo(sprite.GetFloat(name)), name);
        }

        [Test] public void ShadowBounds_CoverTheMaximalShearAndWindMargin()
        {
            var field = Field(1); var mesh = field.ShadowRoot.GetComponentInChildren<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices; var raw = new Bounds(vertices[0], Vector3.zero);
            foreach (var vertex in vertices) raw.Encapsulate(vertex);
            float margin = 2 * SpriteShadow.SharedProfile.MaxLength + field.Definition.WindMargin;
            Assert.That(mesh.bounds.min.x, Is.LessThanOrEqualTo(raw.min.x - margin));
            Assert.That(mesh.bounds.max.y, Is.GreaterThanOrEqualTo(raw.max.y + margin));
        }
    }
}
