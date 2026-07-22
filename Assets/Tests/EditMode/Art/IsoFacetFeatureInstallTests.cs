using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Anchors the WIRING of ADR 0022 phase 3 (headless-safe — no rendering): the mesh-hull
    /// renderer feature must actually be installed on the project's 2D renderer asset, with its
    /// resolve shader pinned by reference. Without this, every hull silently renders nothing —
    /// the overlay quads discard against the registry's clear fallback texture — which looks like
    /// "the boat is invisible" and would send someone hunting the shader instead of the asset.
    /// </summary>
    public class IsoFacetFeatureInstallTests
    {
        private const string RendererAssetPath = "Assets/Settings/Renderer2D.asset";

        [Test]
        public void Renderer2D_HasTheIsoFacetHullFeature_WithItsResolveShaderPinned()
        {
            var data = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererAssetPath);
            Assert.IsNotNull(data, $"No Renderer2DData at {RendererAssetPath} — the 2D renderer moved?");

            var feature = data.rendererFeatures.OfType<IsoFacetHullFeature>().FirstOrDefault();
            Assert.IsNotNull(feature,
                "IsoFacetHullFeature is NOT installed on Renderer2D.asset. Mesh hulls render " +
                "nothing without it (their overlay quads discard everywhere). Re-install via " +
                "'Hidden Harbours/Setup/Install IsoFacet Hull Feature'.");
            Assert.IsTrue(feature.isActive, "IsoFacetHullFeature is installed but DISABLED.");

            var so = new SerializedObject(feature);
            var shader = so.FindProperty("_resolveShader").objectReferenceValue as Shader;
            Assert.IsNotNull(shader,
                "The feature's resolve shader reference is empty. Shader.Find would still work " +
                "in the editor, but the shader gets STRIPPED from player builds when nothing " +
                "references it — pin it (the setup utility does).");
            Assert.AreEqual("Hidden/HiddenHarbours/IsoFacetResolve", shader.name);
        }
    }
}
