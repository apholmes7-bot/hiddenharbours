using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public partial class WakeCentrePhotographPlayTests
    {
        // Diagnostic only. No production shader, material asset, or injector is changed.
        bool _probeFoamVisibility;
        FoamInjector _foamProbeSubject;
        int _foamEligibleFrames, _foamSelectedFrames, _foamOverflowFrames;
        string _foamLastPacking;
        const float FoamProbeStrength = 0.85f;
        static readonly int FoamStrengthId = Shader.PropertyToID("_WakeFoamStrength");
        static readonly int FoamLaceId = Shader.PropertyToID("_WakeFoamLace");
        static readonly FieldInfo FoamMembers = typeof(FoamInjectionRegistry).GetField(
            "s_Live", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly FieldInfo FoamPending = typeof(FoamInjector).GetField(
            "_pending", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo FoamHasPending = typeof(FoamInjector).GetField(
            "_hasPending", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo FoamPendingFrame = typeof(FoamInjector).GetField(
            "_pendingFrame", BindingFlags.Instance | BindingFlags.NonPublic);

        [UnityTest]
        public IEnumerator TheCape_FoamSheet_ReachesTheDrawingChannelOrNamesTheMissingInput()
        {
            RequireAGraphicsDevice();
            Assert.NotNull(FoamMembers, "registry inspection seam changed");
            Assert.NotNull(FoamPending, "injection inspection seam changed");
            Assert.NotNull(FoamHasPending, "injection inspection seam changed");
            Assert.NotNull(FoamPendingFrame, "injection inspection seam changed");
            _probeFoamVisibility = true;
            RenderPipelineManager.beginCameraRendering += ObserveFoamPacking;
            try
            {
                yield return Photograph("foam-visibility", CapeHullPath, CapeVisualPath, forceSprite: false);
            }
            finally
            {
                RenderPipelineManager.beginCameraRendering -= ObserveFoamPacking;
                _probeFoamVisibility = false;
                _foamProbeSubject = null;
            }
        }

        void BeginFoamVisibilityLeg(FoamInjector subject, string leg)
        {
            Assert.NotNull(subject, leg + ": the mesh subject has no foam injector");
            _foamProbeSubject = subject;
            _foamEligibleFrames = _foamSelectedFrames = _foamOverflowFrames = 0;
            _foamLastPacking = "no eligible injection observed";
        }

        void ObserveFoamPacking(ScriptableRenderContext context, Camera camera)
        {
            // After LateUpdate, before the feature collects. Inspect without consuming deposits,
            // changing membership, or triggering the registry's over-cap warning latch.
            if (camera != _cam || _foamProbeSubject == null || Time.deltaTime <= 0f) return;
            var members = (IList)FoamMembers.GetValue(null);
            int eligible = 0, subjectRank = -1;
            var packing = new List<string>();
            foreach (FoamInjector injector in members)
            {
                if (injector == null || !(bool)FoamHasPending.GetValue(injector) ||
                    (int)FoamPendingFrame.GetValue(injector) != Time.frameCount) continue;
                var injection = (FoamInjection)FoamPending.GetValue(injector);
                if (injector == _foamProbeSubject) subjectRank = eligible;
                packing.Add($"{eligible}: {HierarchyPath(injector.transform)} " +
                            $"to={injection.To} amount={injection.Amount:R}");
                eligible++;
            }
            if (subjectRank < 0) return;
            _foamEligibleFrames++;
            if (subjectRank < FoamBuffer.MaxInjectors) _foamSelectedFrames++;
            if (eligible > FoamBuffer.MaxInjectors) _foamOverflowFrames++;
            _foamLastPacking = $"frame={Time.frameCount} registered={members.Count} " +
                               $"eligible={eligible} cap={FoamBuffer.MaxInjectors} subjectRank={subjectRank}\n" +
                               string.Join("\n", packing);
        }

        sealed class FoamProbeSlot
        {
            public Renderer Renderer;
            public int Index;
            public MaterialPropertyBlock Original;
            public MaterialPropertyBlock Working;
        }

        List<FoamProbeSlot> FindFoamProbeSlots()
        {
            var slots = new List<FoamProbeSlot>();
            var materialIds = new HashSet<string>();
            int displaced = 0;
            foreach (Renderer renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (renderer == null || !renderer.gameObject.scene.IsValid() ||
                    !renderer.gameObject.activeInHierarchy || !renderer.enabled) continue;
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null || !material.HasProperty(FoamStrengthId)) continue;
                    var original = new MaterialPropertyBlock();
                    var working = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(original, i);
                    renderer.GetPropertyBlock(working, i);
                    // An indexed block overrides the renderer-wide block. Preserve the latter's
                    // tide/palette/shore uniforms when introducing an indexed block for this probe.
                    if (working.isEmpty) renderer.GetPropertyBlock(working);
                    slots.Add(new FoamProbeSlot { Renderer = renderer, Index = i,
                        Original = original, Working = working });
                    if ((renderer.renderingLayerMask & DisplacedWaterRegistry.RenderingLayer) != 0)
                        displaced++;
                    if (materialIds.Add(material.GetEntityId().ToString()))
                        _rows.Add($"DRAW CHANNEL material={material.name} id={material.GetEntityId()} " +
                                  $"renderer={HierarchyPath(renderer.transform)} slot={i} " +
                                  $"materialStrength={material.GetFloat(FoamStrengthId):R} " +
                                  $"override={working.HasFloat(FoamStrengthId)} " +
                                  $"overrideStrength={working.GetFloat(FoamStrengthId):R}");
                }
            }
            _rows.Add($"DRAW CHANNEL slots={slots.Count} displacedSlots={displaced}");
            Assert.Greater(displaced, 0, "No active displaced-water draw channel was found");
            return slots;
        }

        static void SetFoamProbeStrength(List<FoamProbeSlot> slots, float strength)
        {
            foreach (FoamProbeSlot slot in slots)
            {
                slot.Working.SetFloat(FoamStrengthId, strength);
                slot.Renderer.SetPropertyBlock(slot.Working, slot.Index);
            }
        }

        static void RestoreFoamProbeSlots(List<FoamProbeSlot> slots)
        {
            foreach (FoamProbeSlot slot in slots)
                if (slot.Renderer != null)
                    slot.Renderer.SetPropertyBlock(slot.Original.isEmpty ? null : slot.Original, slot.Index);
        }

        int RecordFoamPair(string label, byte[] first, byte[] second)
        {
            float worst = WorstChannelDelta(first, second, out int pixels);
            _rows.Add($"{label}: hashes={Hash(first)}/{Hash(second)} changedPx={pixels} worst={worst:R}");
            return pixels;
        }

        void RecordFoamTexture(string label)
        {
            Texture texture = Shader.GetGlobalTexture(FoamShaderIds.BufferTex);
            Vector4 world = Shader.GetGlobalVector(FoamShaderIds.BufferWorld);
            _rows.Add($"{label}: window={world} texture={(texture != null ? texture.name : "null")} " +
                      $"type={(texture != null ? texture.GetType().Name : "none")} " +
                      $"registryStrength={FoamInjectionRegistry.LookStrength:R} " +
                      $"shouldRun={FoamInjectionRegistry.ShouldRun}");
            if (!(texture is RenderTexture rt)) return;
            RenderTexture previous = RenderTexture.active;
            Texture2D readback = null;
            try
            {
                RenderTexture.active = rt;
                readback = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
                readback.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                readback.Apply();
                Color[] cells = readback.GetPixels();
                int covered = 0;
                float peak = 0f;
                double total = 0;
                foreach (Color cell in cells)
                {
                    if (cell.r > 0f) covered++;
                    peak = Mathf.Max(peak, cell.r);
                    total += cell.r;
                }
                _rows.Add($"BUFFER {rt.width}x{rt.height}: covered={covered} peak={peak:R} mean={total / cells.Length:R}");
            }
            finally
            {
                RenderTexture.active = previous;
                if (readback != null) Object.DestroyImmediate(readback);
            }
        }

        void CaptureFoamVisibility(string label)
        {
            // All shutters are synchronous in ONE frame. No injector/registry toggles: those
            // change the producer rather than merely testing the water's consumption of its output.
            int frame = Time.frameCount;
            _rows.Add($"FOAM VISIBILITY {label} frame={frame} " +
                      $"subjectEligible={_foamEligibleFrames} selected={_foamSelectedFrames} " +
                      $"overflow={_foamOverflowFrames}\n{_foamLastPacking}");
            List<FoamProbeSlot> slots = FindFoamProbeSlots();
            Texture2D known = null;
            try
            {
                Capture(); // Warm the current camera's buffer/targets before the first shutter.
                byte[] baseline = Capture();
                byte[] baselineRepeat = Capture();
                int baselineFloor = RecordFoamPair("untouched repeat", baseline, baselineRepeat);
                RecordFoamTexture(label);

                SetFoamProbeStrength(slots, 0f);
                byte[] off = Capture();
                SetFoamProbeStrength(slots, FoamProbeStrength);
                byte[] on = Capture();
                byte[] onRepeat = Capture();
                SetFoamProbeStrength(slots, 0f);
                byte[] offAgain = Capture();
                int livePixels = RecordFoamPair("live buffer off/on", off, on);
                int onFloor = RecordFoamPair("live on repeat", on, onRepeat);
                int offFloor = RecordFoamPair("live off repeat", off, offAgain);
                SavePlate(label + "-live-off.png", off);
                SavePlate(label + "-live-on.png", on);

                // Independent positive control: a known full-coverage/fresh sheet in a window
                // covering this camera. Bound per DRAW SLOT so render-graph globals cannot erase it.
                // This is diagnostic input, never evidence that the production producer works.
                known = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
                known.name = "FoamVisibilityKnownInput";
                known.SetPixel(0, 0, new Color(1, 1, 0, 1));
                known.Apply();
                float extent = 4f * _cam.orthographicSize * Mathf.Max(1f, _cam.aspect);
                Vector3 centre = _cam.transform.position;
                var window = new Vector4(centre.x - extent / 2f, centre.y - extent / 2f, extent, 1f / extent);
                foreach (FoamProbeSlot slot in slots)
                {
                    slot.Working.SetTexture(FoamShaderIds.BufferTex, known);
                    slot.Working.SetVector(FoamShaderIds.BufferWorld, window);
                    slot.Working.SetFloat(FoamLaceId, 0f);
                }
                SetFoamProbeStrength(slots, 0f);
                byte[] knownOff = Capture();
                SetFoamProbeStrength(slots, FoamProbeStrength);
                byte[] knownOn = Capture();
                byte[] knownRepeat = Capture();
                int control = RecordFoamPair("known input off/on", knownOff, knownOn);
                int knownFloor = RecordFoamPair("known on repeat", knownOn, knownRepeat);
                int bypass = RecordFoamPair("off ignores known input", off, knownOff);
                SavePlate(label + "-known-on.png", knownOn);
                SavePlate(label + "-known-off.png", knownOff);
                RestoreFoamProbeSlots(slots);
                int restored = RecordFoamPair("restored original draw state", baseline, Capture());

                // Flush even when a control fails. Identical real arms are a RESULT, not proof of
                // a missing sheet; only a responding known-input arm validates that inference.
                _rows.Add(livePixels > 0 ? "RESULT: production buffer affects drawn water in this frame."
                    : "RESULT: no production-buffer contribution measured; consult controls, texture and packing.");
                WriteMeasurements(label);
                Assert.AreEqual(frame, Time.frameCount, "A shutter crossed a frame boundary");
                Assert.AreEqual(0, baselineFloor + onFloor + offFloor + knownFloor,
                    "Same-frame repeats changed; do not attribute the live pair to foam");
                Assert.Greater(control, 0, "Known foam did not change water: the draw-channel probe is invalid");
                Assert.AreEqual(0, bypass, "Strength zero must ignore both real and known foam inputs");
                Assert.AreEqual(0, restored, "Restoring the property blocks did not restore the original picture");
                Assert.Greater(_foamEligibleFrames, 0, "Subject never offered an injection while driving");
            }
            finally
            {
                RestoreFoamProbeSlots(slots);
                if (known != null) Object.DestroyImmediate(known);
            }
        }
    }
}
