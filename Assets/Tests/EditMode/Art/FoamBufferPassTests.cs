using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.Rendering;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The ADR 0027 #6 foam buffer's PASS-LEVEL guards — the half of the item that lives in the
    /// renderer feature rather than in <see cref="FoamBuffer"/>'s maths:
    ///
    /// <list type="bullet">
    /// <item><b>the magenta guard for the new shader.</b> <c>WaterShaderCompileGuardTests</c> discovers
    /// shaders through the MATERIALS that reference them, and the foam advect material is created at
    /// runtime (<c>CoreUtils.CreateEngineMaterial</c>), so no material asset points at it and the
    /// existing guard would never see it. This forces its import and fails red on any compile error —
    /// headless, through the shader COMPILER and the asset importer, so it runs on CI exactly as the
    /// water guard does;</item>
    /// <item><b>zero cost when idle</b> — the <c>IsoFacetUrpPassTests</c> contract, and this item ships
    /// with BOTH of its gates shut.</item>
    /// </list>
    ///
    /// <para>⚠️ CI has NO graphics device, where recording a raster pass can crash the editor outright
    /// rather than failing cleanly. Anything that touches real render state is gated behind
    /// <see cref="RequireAGraphicsDevice"/> as its FIRST statement and skips LOUDLY.</para>
    /// </summary>
    public class FoamBufferPassTests
    {
        const string AdvectShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursFoamBufferAdvect.shader";
        const string AdvectShaderName = "Hidden/HiddenHarbours/FoamBufferAdvect";

        /// <summary>Must be the FIRST statement of any test that touches render state: on a Null
        /// Device the crash happens in native rendering code that no assertion can intercept.</summary>
        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore(
                    "SKIPPED, NOT VERIFIED — this run has no graphics device (Renderer: Null Device), " +
                    "so the ADR 0027 #6 foam buffer pass could not be exercised and proved nothing. " +
                    "Expected on CI; the pass-level checks only run on a machine with a GPU. The " +
                    "buffer's MATHS is pinned headless by FoamBufferTests, and the shader's compile " +
                    "is pinned headless by the magenta guard in this same fixture.");
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Leave the static registry exactly as found — it is process-wide and other fixtures read it.
            FoamInjectionRegistry.PublishLookStrength(0f);
            FoamInjectionRegistry.PublishDriftVelocity(Vector2.zero);
        }

        // ================= THE MAGENTA GUARD (headless — runs on CI) ============================

        [Test]
        public void FoamAdvectShader_Imports_WithNoCompileErrors()
        {
            // The importer logs shader errors to the console, which the test runner would otherwise
            // turn into an "Unhandled log message" failure — the wrong reason, with an opaque message.
            // Our GetShaderMessages read is the single authority (the water guard's discipline).
            LogAssert.ignoreFailingMessages = true;

            // Force a fresh synchronous reimport so the compiler re-runs from the on-disk source and
            // no stale cache can hide a break.
            AssetDatabase.ImportAsset(AdvectShaderPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(AdvectShaderPath);
            Assert.IsNotNull(shader,
                $"Could not load the foam advect shader at '{AdvectShaderPath}'. It is created at " +
                "runtime through CoreUtils.CreateEngineMaterial, so NOTHING else in the project " +
                "references it — if the path moved, this guard is the only thing that would notice.");

            var errors = new StringBuilder();
            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
            {
                if (message.severity != ShaderCompilerMessageSeverity.Error) continue;
                errors.AppendLine($"  {AdvectShaderPath}({message.line}): {message.message} {message.messageDetails}");
            }

            Assert.IsEmpty(errors.ToString(),
                "The ADR 0027 #6 foam advect shader does not compile, so the wake buffer would fill " +
                "with magenta (or not at all):\n" + errors +
                "\n⚠️ Two known traps in this file: an [unroll] over a RUNTIME bound (FOAM_MAX_INJECTORS " +
                "must stay a #define), and a '+' character inside a [Header(...)] label.");
        }

        [Test]
        public void FoamAdvectShader_IsFindableByName()
        {
            // The feature falls back to Shader.Find when no shader is assigned on the feature asset,
            // which is how every shipped renderer gets it. A renamed Shader "..." line would leave the
            // buffer silently unfilled with only an error log to show for it.
            Assert.IsNotNull(Shader.Find(AdvectShaderName),
                $"Shader.Find(\"{AdvectShaderName}\") returned null. IsoFacetHullFeature uses exactly " +
                "this name as its fallback when the feature asset has no shader assigned.");
        }

        // ================= ZERO COST WHEN IDLE (headless — runs on CI) ==========================

        [Test]
        public void NoInjector_MeansTheBufferPassIsNeverRecorded_WhateverTheLookDial()
        {
            // Half one of the contract: no hull is churning water. Every FoamInjector unregisters the
            // moment it is off the water, so a boat hauled out (or a scene with no boats at all) costs
            // nothing — no render target, no blit, nothing.
            Assume.That(FoamInjectionRegistry.Count, Is.Zero,
                "This fixture assumes the shipped state: nothing in the project carries a FoamInjector.");

            FoamInjectionRegistry.PublishLookStrength(2f);
            Assert.IsFalse(FoamInjectionRegistry.ShouldRun,
                "The foam buffer pass would be recorded with NO hull churning water. That breaks the " +
                "zero-cost-when-idle contract (CLAUDE.md rule 7, the IsoFacetUrpPassTests contract).");
        }

        [Test]
        public void LookDialAtZero_MeansTheBufferPassIsNeverRecorded()
        {
            // Half two: the effect off. This is the SHIPPED state — _WakeFoamStrength is 0 in Water.mat
            // and in all eight presets — so there is no "foam buffer off" branch to keep byte-identical,
            // only an absent pass. That IS this item's passthrough proof.
            FoamInjectionRegistry.PublishLookStrength(0f);
            Assert.IsFalse(FoamInjectionRegistry.ShouldRun,
                "With the owner's look dial at 0 the water shader draws nothing from the buffer, so " +
                "filling one is pure waste.");
        }

        [Test]
        public void LookStrength_DefaultsToOff_AndClampsNegatives()
        {
            FoamInjectionRegistry.PublishLookStrength(-3f);
            Assert.AreEqual(0f, FoamInjectionRegistry.LookStrength,
                "A negative look dial must read as OFF. The fail state for this item is always 'the " +
                "effect vanishes', never 'the effect is stuck on'.");
        }

        [Test]
        public void DriftVelocity_RoundTrips_SoTheBufferTravelsWithTheWeather()
        {
            var drift = new Vector2(0.42f, -0.17f);
            FoamInjectionRegistry.PublishDriftVelocity(drift);
            Assert.AreEqual(drift, FoamInjectionRegistry.DriftVelocity,
                "The buffer advects along whatever WaterSurface publishes here — the same wind/current " +
                "blend the shader's own foam drifts with.");
        }

        [Test]
        public void CollectInjections_HandlesADegenerateBuffer_WithoutThrowing()
        {
            Assert.AreEqual(0, FoamInjectionRegistry.CollectInjections(null));
            Assert.AreEqual(0, FoamInjectionRegistry.CollectInjections(new FoamInjection[0]));
        }

        // ================= ON A REAL DEVICE ONLY ================================================

        [Test]
        public void FoamAdvectShader_IsSupportedOnThisDevice_AndHasItsPass()
        {
            RequireAGraphicsDevice();   // MUST be first — see the method's doc

            var shader = Shader.Find(AdvectShaderName);
            Assert.IsNotNull(shader, $"Shader.Find(\"{AdvectShaderName}\") returned null.");
            Assert.IsTrue(shader.isSupported,
                "The foam advect shader is not supported on this graphics device, so the wake buffer " +
                "would never fill on it. Note the pass is SM 3.5 and writes a single R8 target.");

            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                Assert.GreaterOrEqual(material.passCount, 1,
                    "The foam advect material has no passes, so the blit would draw nothing.");
                Assert.AreEqual("HHFoamBufferAdvect", material.GetPassName(0),
                    "The advect pass has been renamed or reordered. IsoFacetHullFeature blits pass 0.");
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }
    }
}
