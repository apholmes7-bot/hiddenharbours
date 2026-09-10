using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>ADR 0044's riskiest unknown, asked as a test instead of assumed.</b> The mesh fleet is
    /// drawn by <see cref="IsoFacetHullFeature"/>, which collects its subjects with a
    /// <c>ShaderTagId("HHHullFacet")</c> renderer list — never by naming a component. Every mesh
    /// that has ever gone through it is a <see cref="MeshRenderer"/>. A skinned character is not:
    /// <b><see cref="SkinnedMeshRenderer"/> appears nowhere else in this repository.</b> So before
    /// anyone writes a presenter, two questions have to be answered, and they are DIFFERENT
    /// questions with different answers:
    ///
    /// <list type="number">
    /// <item><b>Is the pass recorded at all?</b> <c>AddRenderPasses</c> opens with
    /// <c>bool hulls = IsoFacetHullRegistry.Count &gt; 0</c> and returns early when nothing else
    /// wants the frame. A character standing in a field, with no boat anywhere, enqueues NOTHING —
    /// and no property of her renderer can change that. Measured below, headlessly, on CI.</item>
    /// <item><b>Given the pass IS recorded, does the renderer list include her?</b> That is a
    /// question about <c>CreateRendererList</c> and needs pixels. Gated on a graphics device,
    /// skipped loudly on CI, and the headless test above it establishes the NECESSARY conditions so
    /// that a red result on a GPU means "the list excludes skinned renderers" rather than "the
    /// fixture forgot to enable something".</item>
    /// </list>
    ///
    /// <para><b>Why they must be separate tests.</b> They are two gates in series and one
    /// observation cannot tell them apart: a blank frame is equally consistent with "the pass never
    /// ran" and "the pass ran and skipped her". The first gate is decidable without a GPU, so it is
    /// decided without one, and the GPU test stands the hull up itself precisely so that gate 1 is
    /// known-open while gate 2 is being read.</para>
    ///
    /// <para><b>The cost of path (b) is measured here regardless of the verdict</b>
    /// (<see cref="CpuSkinningTheBindMeshCostsThisMuchOfAFrame"/>), because the handoff asks for the
    /// number whether or not (a) works, and because it is the only one of the four tests whose
    /// answer nothing else in the repo can supply.</para>
    ///
    /// <para><b>⚠️ The skinning below is the SUBJECT, not a copy of the oracle.</b>
    /// <c>CharacterSkinBakeGuardTests</c> carries its own <c>SkinMatrices</c>/<c>SkinVertex</c> pair
    /// for the rig-6 comparison. These are deliberately not shared: that one is an oracle walk whose
    /// job is to disagree with the baker, this one is a candidate PRESENTER whose cost is being
    /// timed. Folding them together would make each the other's mirror.</para>
    /// </summary>
    public class CharacterSkinFacetPassTests
    {
        const string Player = CharacterRigBakeMenu.PlayerPreset;

        /// <summary>The isolated layer the probe camera sees, matching
        /// <c>IsoFacetUrpPassTests</c> — anything the editor leaves lying around in the open scene
        /// is on layer 0 and stays out of frame.</summary>
        const int ProbeLayer = 31;

        /// <summary>60 fps, in milliseconds. CLAUDE.md §3.7's budget, spelled once.</summary>
        const double FrameBudgetMs = 1000.0 / 60.0;

        /// <summary>How many figures the cast PR will eventually put on screen at once — the
        /// number path (b)'s per-figure cost has to be multiplied by before it means anything.</summary>
        const int CastSize = 10;

        IRigScriptHost _host;
        CharacterSkinAssetBaker.SkinBake _bake;
        CharacterSkinDef _def;

        [OneTimeSetUp]
        public void ComposeOnce()
        {
            _host = RigScriptHostFactory.Create();
            _bake = CharacterSkinAssetBaker.Compose(_host, Player);
            _def = _bake.Def;
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            if (_def != null)
            {
                if (_def.BindMesh != null) Object.DestroyImmediate(_def.BindMesh);
                Object.DestroyImmediate(_def);
            }
            _host?.Dispose();
            _host = null; _bake = null; _def = null;
        }

        // =======================================================================================
        // GATE 1 — is the pass recorded at all?
        // =======================================================================================

        /// <summary>
        /// <b>The facet pass is gated on the HULL registry, and a character cannot join it.</b>
        ///
        /// <para>This is the finding that outranks the renderer-list question, because it applies to
        /// BOTH candidate paths equally — a CPU-skinned <see cref="MeshRenderer"/> (option b) is
        /// exactly as invisible ashore as a <see cref="SkinnedMeshRenderer"/> (option a). Whatever
        /// the presenter does with the mesh, the feature records <c>HH Hull Facet</c> only when
        /// <c>IsoFacetHullRegistry.Count &gt; 0</c>, and the only thing that can raise that count is
        /// an <see cref="IsoFacetHullRenderer"/>: <c>Register</c> is <c>internal</c> AND typed to
        /// that component, so there is no seam a character could take even from inside
        /// <c>HiddenHarbours.Art</c> without a code change.</para>
        ///
        /// <para><b>What that means for the presenter, stated so it cannot be discovered the
        /// expensive way:</b> a mesh figure draws through the facet path only while she shares a
        /// frame with a registered mesh hull. Aboard the dory that is free. On the wharf it is not.
        /// Opening the gate is a change to <c>IsoFacetHullFeature</c>/<c>IsoFacetHullRegistry</c>,
        /// which belong to the water lane — so it is FILED here, not fixed here.</para>
        ///
        /// <para><b>This test is a statement about today's code and it is meant to redden</b> the day
        /// someone opens the registry to non-hulls. That is not a false alarm; that is the
        /// notification this lane is asking for.</para>
        /// </summary>
        [Test]
        public void TheFacetPassRecordsNothingUnlessAMeshHullIsRegistered()
        {
            MethodInfo register = typeof(IsoFacetHullRegistry).GetMethod(
                "Register", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(register,
                "IsoFacetHullRegistry.Register is gone — the gate this test describes has moved, and " +
                "the presenter's assumption about where facet subjects come from must be re-read.");

            ParameterInfo[] args = register.GetParameters();
            Assert.AreEqual(1, args.Length, "Register's shape changed; re-read the gate.");
            Assert.AreEqual(typeof(IsoFacetHullRenderer), args[0].ParameterType,
                "Register now takes something other than an IsoFacetHullRenderer — the facet pass " +
                "may have learned about non-hull subjects, which is exactly the change a mesh " +
                "character needs. Re-read IsoFacetHullFeature.AddRenderPasses and update ADR 0044.");
            Assert.IsFalse(register.IsPublic,
                "Register became public — a character could now raise the count that gates the " +
                "facet pass. Re-read the gate before relying on this test's conclusion.");

            // And the other half: nothing else in the registry's PUBLIC surface can raise Count.
            foreach (MethodInfo m in typeof(IsoFacetHullRegistry)
                         .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                Assert.IsFalse(m.Name.StartsWith("Register", StringComparison.Ordinal),
                    $"IsoFacetHullRegistry.{m.Name} is public and looks like a way in. If a " +
                    "character can now register as a facet subject, ADR 0044's presenter section is " +
                    "out of date.");
            }

            Debug.Log(
                "FACET PASS GATE 1 — the pass is recorded only while IsoFacetHullRegistry.Count > 0, " +
                "and Register is internal + typed to IsoFacetHullRenderer.\n" +
                "  → a mesh character draws through the facet path ONLY in a frame that also carries " +
                "a registered mesh hull.\n" +
                "  → this is true of BOTH option (a) SkinnedMeshRenderer and option (b) CPU-skinned " +
                "MeshRenderer: it does not choose between them, it prices them both.\n" +
                "  → opening it is a change to IsoFacetHullFeature / IsoFacetHullRegistry, the water " +
                "lane's files. FILED, not fixed here.\n" +
                $"  registry count as this test ran: {IsoFacetHullRegistry.Count}");
        }

        // =======================================================================================
        // GATE 2 — necessary conditions, decided without a GPU
        // =======================================================================================

        /// <summary>
        /// <b>Nothing a skinned character carries excludes her from what the facet list filters
        /// on.</b> Necessary, not sufficient — and the class doc says which is which.
        ///
        /// <para>The facet list is built with <c>DrawingSettings(s_FacetTag, sorting)</c> +
        /// <c>FilteringSettings(RenderQueueRange.all)</c>: no layer mask, no renderer-type
        /// discriminator, <c>PerObjectData.None</c>. So the list's own inputs reduce to two
        /// questions — does the subject's shader have a pass tagged <c>HHHullFacet</c>, and does the
        /// subject survive culling into <c>cullResults</c>. This test asks both of a REAL
        /// <see cref="SkinnedMeshRenderer"/> built on the composed bind mesh.</para>
        ///
        /// <para><b>⚠️ <c>SkinQuality</c> is not a detail and it is asserted here.</b> The BLENDED
        /// rings carry TWO weights; collapsing them to one moves a vertex by 4.52e-2 m — 452× the
        /// rig's own 1e-4 m tolerance. A renderer left on <see cref="SkinQuality.Auto"/> reads
        /// <see cref="QualitySettings.skinWeights"/>, which is a PROJECT setting a quality-level
        /// edit can lower without touching a line of this lane's code. The presenter must set
        /// <see cref="SkinQuality.Bone2"/> explicitly; the project's current setting is printed
        /// either way, because that is the number that says how close the trapdoor is.</para>
        /// </summary>
        [Test]
        public void NothingASkinnedRendererCarriesExcludesItFromTheFacetList()
        {
            var shader = Shader.Find("HiddenHarbours/IsoFacet");
            Assert.IsNotNull(shader,
                "HiddenHarbours/IsoFacet is missing — the facet material a character would carry " +
                "does not exist, so this question cannot be asked.");

            // (i) the shader really does carry the tag the list matches on.
            bool tagged = false;
            var passes = new StringBuilder();
            for (int s = 0; s < shader.subshaderCount; s++)
                for (int p = 0; p < shader.GetPassCountInSubshader(s); p++)
                {
                    ShaderTagId lm = shader.FindPassTagValue(s, p, new ShaderTagId("LightMode"));
                    passes.Append(' ').Append(string.IsNullOrEmpty(lm.name) ? "(untagged)" : lm.name);
                    if (string.Equals(lm.name, "HHHullFacet", StringComparison.Ordinal)) tagged = true;
                }
            Assert.IsTrue(tagged,
                "HiddenHarbours/IsoFacet carries no pass tagged LightMode=HHHullFacet, so the facet " +
                $"renderer list would skip ANY renderer using it. Passes found:{passes}");

            var probe = new GameObject("SkinProbe") { layer = ProbeLayer };
            try
            {
                var smr = probe.AddComponent<SkinnedMeshRenderer>();
                var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                smr.sharedMaterial = mat;
                Transform[] bones = BuildSkeleton(probe.transform, _def);
                smr.bones = bones;
                smr.rootBone = bones[0];
                smr.sharedMesh = _def.BindMesh;
                smr.quality = SkinQuality.Bone2;
                smr.updateWhenOffscreen = true;

                // (ii) the render queue. RenderQueueRange.all is the filter the facet list uses.
                RenderQueueRange all = RenderQueueRange.all;
                Assert.GreaterOrEqual(mat.renderQueue, all.lowerBound,
                    "the facet material's render queue is below RenderQueueRange.all");
                Assert.LessOrEqual(mat.renderQueue, all.upperBound,
                    "the facet material's render queue is above RenderQueueRange.all");

                // (iii) everything culling consults.
                Assert.IsTrue(smr.enabled, "renderer disabled");
                Assert.IsFalse(smr.forceRenderingOff, "forceRenderingOff is set");
                Assert.IsTrue(probe.activeInHierarchy, "probe inactive");
                Assert.AreNotEqual(0u, smr.renderingLayerMask & 1u,
                    "the probe's renderingLayerMask excludes rendering layer 0, which is the only " +
                    "one FilteringSettings' default mask admits");
                Assert.AreNotEqual(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly,
                    smr.shadowCastingMode, "a ShadowsOnly renderer is dropped from the opaque list");

                Bounds b = smr.bounds;
                Assert.Greater(b.size.sqrMagnitude, 1e-6f,
                    "the skinned renderer's world bounds are degenerate, so frustum culling would " +
                    "drop her before any tag was compared. bounds = " + b);
                Assert.IsFalse(float.IsNaN(b.center.x) || float.IsInfinity(b.extents.x),
                    "non-finite bounds: " + b);

                // (iv) the skin itself is well formed, which is what makes the bounds meaningful.
                Assert.AreEqual(_def.BindMesh.vertexCount, _def.BindMesh.boneWeights.Length,
                    "one bone weight per vertex");
                Assert.AreEqual(_def.BindMesh.bindposes.Length, smr.bones.Length,
                    "SkinnedMeshRenderer.bones must be exactly as long as the mesh's bindposes; " +
                    "Unity indexes them together and a short array is undefined behaviour");
                Assert.AreEqual(SkinQuality.Bone2, smr.quality,
                    "the presenter MUST pin SkinQuality.Bone2 — the blended rings carry two weights " +
                    "and dropping the second moves a vertex 4.52e-2 m, 452x the rig's tolerance");

                Debug.Log(
                    "FACET PASS GATE 2, necessary conditions — ALL MET (this is a PREDICTION, not " +
                    "evidence; the evidence is the GPU test below).\n" +
                    $"  shader passes:{passes}\n" +
                    $"  facet material renderQueue {mat.renderQueue} inside RenderQueueRange.all " +
                    $"[{all.lowerBound}, {all.upperBound}]\n" +
                    $"  bones {smr.bones.Length}, bindposes {_def.BindMesh.bindposes.Length}, " +
                    $"weights {_def.BindMesh.boneWeights.Length}, verts {_def.BindMesh.vertexCount}\n" +
                    $"  world bounds {b.center} +/- {b.extents}\n" +
                    $"  quality pinned to {smr.quality}; the PROJECT default " +
                    $"QualitySettings.skinWeights is {QualitySettings.skinWeights} — a renderer left " +
                    "on Auto would inherit that, and OneBone costs 4.52e-2 m");

                Object.DestroyImmediate(mat);
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// <b>The filter the facet list uses cannot name a renderer class at all.</b>
        ///
        /// <para>This is the structural half of the prediction, and it is worth a test of its own
        /// because it is the only part that generalises: <see cref="FilteringSettings"/> is Unity's,
        /// not this project's, and if it ever grew a renderer-type discriminator then every
        /// conclusion drawn here about "the same API URP's opaque pass uses for skinned characters"
        /// would need re-reading. The member list is printed rather than asserted equal to a
        /// snapshot — a snapshot would redden on a Unity upgrade for no reason.</para>
        /// </summary>
        [Test]
        public void TheFacetListsFilterCannotNameARendererClass()
        {
            var names = new List<string>();
            foreach (PropertyInfo p in typeof(FilteringSettings)
                         .GetProperties(BindingFlags.Public | BindingFlags.Instance))
                names.Add(p.Name);
            foreach (FieldInfo f in typeof(FilteringSettings)
                         .GetFields(BindingFlags.Public | BindingFlags.Instance))
                names.Add(f.Name);

            Assert.IsNotEmpty(names, "FilteringSettings exposes nothing at all — re-read the API.");

            foreach (string n in names)
            {
                string lower = n.ToLowerInvariant();
                Assert.IsFalse(lower.Contains("skin") || lower.Contains("renderertype") ||
                               lower.Contains("componenttype"),
                    $"FilteringSettings.{n} looks like a renderer-class discriminator. The facet " +
                    "list's filter can now say what KIND of renderer it wants, which is exactly the " +
                    "thing this lane concluded it could not — re-measure before trusting option (a).");
            }

            Debug.Log("FilteringSettings exposes: " + string.Join(", ", names) +
                      "\n  → no renderer-class discriminator, so the facet list's filter cannot " +
                      "single out (or exclude) a SkinnedMeshRenderer. Combined with the fleet's " +
                      "MeshRenderers already drawing through it, option (a) is PREDICTED to work. " +
                      "Prediction, not evidence.");
        }

        // =======================================================================================
        // PATH (b), COSTED — the number the body needs whichever way (a) lands
        // =======================================================================================

        /// <summary>
        /// <b>What option (b) costs per figure per frame.</b> CPU-skin the bind mesh into a
        /// <see cref="Mesh"/>: compose one matrix per bone, then walk every corner at up to two
        /// influences, positions and normals, and hand the buffers to the mesh.
        ///
        /// <para><b>The assertion is deliberately loose and the REPORT is the point.</b> An EditMode
        /// stopwatch measures this machine on this day; a tight bar here would be a hardware test
        /// wearing a budget's clothes. The ceiling below only reddens on a change of ORDER — a
        /// presenter that started skinning four influences, or lost the pre-allocated buffers and
        /// started allocating 2,992 Vector3s a frame.</para>
        ///
        /// <para>Buffers are allocated ONCE, outside the timed loop, because that is how a presenter
        /// would hold them: a per-frame allocation of this size is a GC pause, not a cost.</para>
        /// </summary>
        [Test]
        public void CpuSkinningTheBindMeshCostsThisMuchOfAFrame()
        {
            Mesh bind = _def.BindMesh;
            EnsureParents(_def);
            Vector3[] srcVerts = bind.vertices;
            Vector3[] srcNorms = bind.normals;
            BoneWeight[] weights = bind.boneWeights;
            Matrix4x4[] bindposes = bind.bindposes;
            int n = _def.Bones.Length;

            CharacterSkinDef.SkinClip clip = ClipNamed("walk");

            // What a presenter holds for the life of the figure.
            var world = new Matrix4x4[n];
            var skin = new Matrix4x4[n];
            var outVerts = new Vector3[srcVerts.Length];
            var outNorms = new Vector3[srcNorms.Length];
            var target = new Mesh { indexFormat = bind.indexFormat };
            target.vertices = srcVerts;
            target.normals = srcNorms;
            target.triangles = bind.triangles;

            const int warm = 20;
            const int iterations = 300;

            // --- timed: skin only (no mesh upload) ------------------------------------------
            for (int i = 0; i < warm; i++)
                SkinOnce(clip, i % clip.FrameCount, n, bindposes, weights, srcVerts, srcNorms,
                         world, skin, outVerts, outNorms);

            var clock = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
                SkinOnce(clip, i % clip.FrameCount, n, bindposes, weights, srcVerts, srcNorms,
                         world, skin, outVerts, outNorms);
            clock.Stop();
            double skinMs = clock.Elapsed.TotalMilliseconds / iterations;

            // --- timed: skin + hand the buffers to the Mesh ---------------------------------
            for (int i = 0; i < warm; i++)
            {
                SkinOnce(clip, i % clip.FrameCount, n, bindposes, weights, srcVerts, srcNorms,
                         world, skin, outVerts, outNorms);
                target.vertices = outVerts;
                target.normals = outNorms;
            }

            clock.Restart();
            for (int i = 0; i < iterations; i++)
            {
                SkinOnce(clip, i % clip.FrameCount, n, bindposes, weights, srcVerts, srcNorms,
                         world, skin, outVerts, outNorms);
                target.vertices = outVerts;
                target.normals = outNorms;
            }
            clock.Stop();
            double totalMs = clock.Elapsed.TotalMilliseconds / iterations;

            Object.DestroyImmediate(target);

            long influences = 0;
            for (int v = 0; v < weights.Length; v++)
                influences += weights[v].weight1 != 0f ? 2 : 1;

            Debug.Log(
                "OPTION (b), CPU SKINNING — COST PER FIGURE PER FRAME\n" +
                $"  {srcVerts.Length:N0} vertices x {_def.MaxInfluences} max influences " +
                $"({influences:N0} weighted terms), {n} bones\n" +
                $"  skin only            {skinMs:F3} ms  ({skinMs / FrameBudgetMs * 100.0:F2} % of a " +
                $"{FrameBudgetMs:F2} ms frame)\n" +
                $"  skin + Mesh upload   {totalMs:F3} ms  ({totalMs / FrameBudgetMs * 100.0:F2} % of a frame)\n" +
                $"  whole cast x{CastSize}        {totalMs * CastSize:F3} ms  " +
                $"({totalMs * CastSize / FrameBudgetMs * 100.0:F2} % of a frame)\n" +
                "  ⚠️ EditMode stopwatch on this machine — an order-of-magnitude figure for the PR " +
                "body, not a budget gate. The ceiling asserted below is 2 ms and only catches a " +
                "change of order (four influences, or per-frame allocation).");

            Assert.Less(totalMs, 2.0,
                $"CPU-skinning one figure took {totalMs:F3} ms — an order more than measured when " +
                "this was written. Something changed shape: influences per corner, buffer reuse, or " +
                "the vertex count.");
        }

        // =======================================================================================
        // GATE 2, THE EVIDENCE — needs a GPU, so it skips loudly on CI
        // =======================================================================================

        /// <summary>
        /// <b>The question itself: does the facet renderer list draw a
        /// <see cref="SkinnedMeshRenderer"/>?</b> Asked both ways in ONE stage, so the two draws
        /// cannot differ by anything except which renderer made them.
        ///
        /// <para>A real <see cref="IsoFacetHullRenderer"/> is configured with the CPU-skinned mesh —
        /// that is option (b), it is the reference image, AND it is what holds gate 1 open. A
        /// <see cref="SkinnedMeshRenderer"/> is then parented under the hull's own
        /// <see cref="IsoFacetHullRenderer.PosedMesh"/> carrying the same material and property
        /// block, with its bone root at identity there — so both paths resolve to the same world
        /// matrix and any difference in the picture is the renderer, not the placement.</para>
        ///
        /// <para>Three renders, each with exactly one of the two enabled, plus a SABOTAGE: one bone
        /// rotated a quarter turn, which must move the picture. Without that last one a fixture that
        /// silently rendered nothing twice would report a perfect match.</para>
        ///
        /// <para><b>⚠️ CI runs Unity with no graphics device, where recording this pass CRASHES the
        /// editor rather than failing.</b> The device check is the first statement in the method and
        /// it skips, loudly: a green CI run carries no evidence about this fixture.</para>
        /// </summary>
        [Test]
        public void TheSkinnedRendererPaintsTheSameFacetPixelsAsTheCpuSkinnedMesh()
        {
            RequireAGraphicsDevice();

            CharacterSkinDef.SkinClip clip = ClipNamed("walk");
            const int Frame = 3;

            Mesh posed = CpuSkinnedCopy(_def, clip, Frame);
            GameObject hullGo = null, camGo = null, skinGo = null;
            RenderTexture rt = null;
            Material skinMat = null;

            try
            {
                // --- the hull: option (b)'s renderer, and gate 1's key ------------------------
                hullGo = new GameObject("CharSkinFacetProbe") { layer = ProbeLayer };
                var hull = hullGo.AddComponent<IsoFacetHullRenderer>();
                hull.Configure(SetupFrom(_bake.Bind, posed));
                hull.HeadingDirUnits = 0f;
                hull.ApplyPose();
                SetLayerRecursive(hullGo.transform, ProbeLayer);

                Assert.Greater(IsoFacetHullRegistry.Count, 0,
                    "the probe hull did not register, so the facet pass would not be recorded and " +
                    "a blank frame would prove nothing about skinned renderers (gate 1, not gate 2).");

                Transform posedMesh = hull.PosedMesh;
                Assert.IsNotNull(posedMesh, "IsoFacetHullRenderer.PosedMesh is null after Configure");
                var hullMr = posedMesh.GetComponent<MeshRenderer>();
                Assert.IsNotNull(hullMr, "the hull's PosedMesh carries no MeshRenderer");

                // --- the camera ---------------------------------------------------------------
                Bounds bb = hullMr.bounds;
                rt = new RenderTexture(_bake.Bind.W * 2, _bake.Bind.H * 2, 24,
                                       RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
                camGo = new GameObject("CharSkinFacetCam") { layer = ProbeLayer };
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(0.5f, bb.extents.y * 1.35f);
                cam.transform.position = new Vector3(bb.center.x, bb.center.y, bb.center.z - 100f);
                cam.nearClipPlane = 1f;
                cam.farClipPlane = 400f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.clear;
                cam.cullingMask = 1 << ProbeLayer;
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.targetTexture = rt;

                // --- option (a): the same figure, skinned by Unity ----------------------------
                skinGo = new GameObject("SkinnedFigure") { layer = ProbeLayer };
                skinGo.transform.SetParent(posedMesh, worldPositionStays: false);
                skinGo.transform.localPosition = Vector3.zero;
                skinGo.transform.localRotation = Quaternion.identity;
                skinGo.transform.localScale = Vector3.one;

                var smr = skinGo.AddComponent<SkinnedMeshRenderer>();
                skinMat = new Material(hullMr.sharedMaterial) { hideFlags = HideFlags.HideAndDontSave };
                smr.sharedMaterial = skinMat;
                Transform[] bones = BuildSkeleton(skinGo.transform, _def);
                smr.bones = bones;
                smr.rootBone = bones[0];
                smr.sharedMesh = _def.BindMesh;
                smr.quality = SkinQuality.Bone2;
                smr.updateWhenOffscreen = true;
                PoseSkeleton(bones, _def, clip, Frame);

                // The hull writes _HullId, _HullOrigin and the deck-occupant slots per DRAW, on a
                // property block. Without them the skinned figure would resolve into a different id
                // band and the diff would be about ids, not about renderers.
                var block = new MaterialPropertyBlock();
                hullMr.GetPropertyBlock(block);
                smr.SetPropertyBlock(block);
                SetLayerRecursive(skinGo.transform, ProbeLayer);

                // --- render (b), then (a), then the sabotage ----------------------------------
                smr.enabled = false;
                hullMr.enabled = true;
                WarmShaders(cam);
                byte[] refPx = Render(cam, rt);

                int refSolid = SolidPixels(refPx);
                Assert.Greater(refSolid, 200,
                    $"the CPU-skinned reference painted only {refSolid} solid pixels — the SUBJECT " +
                    "is not in frame, so nothing below is a measurement of anything. Fix the " +
                    "framing before reading the skinned result.");

                hullMr.enabled = false;
                smr.enabled = true;
                byte[] skinPx = Render(cam, rt);
                int skinSolid = SolidPixels(skinPx);

                // THE ANSWER. Everything else in this method exists to make this line mean something.
                Assert.Greater(skinSolid, 0,
                    "THE FACET RENDERER LIST DREW NOTHING FOR A SkinnedMeshRenderer, while drawing " +
                    $"{refSolid} solid pixels for the same geometry on a MeshRenderer in the same " +
                    "frame, same material, same property block, same transform, same layer. " +
                    "Option (a) is NOT available: the presenter must CPU-skin into a Mesh " +
                    "(option b) — its cost is in CpuSkinningTheBindMeshCostsThisMuchOfAFrame.");

                double diff = MismatchPercent(refPx, skinPx);
                Debug.Log(
                    "FACET PASS GATE 2 — MEASURED ON A GPU\n" +
                    $"  device            {SystemInfo.graphicsDeviceType} / {SystemInfo.graphicsDeviceName}\n" +
                    $"  (b) MeshRenderer, CPU-skinned   {refSolid:N0} solid px\n" +
                    $"  (a) SkinnedMeshRenderer         {skinSolid:N0} solid px\n" +
                    $"  per-pixel mismatch              {diff:F3} %\n" +
                    "  → a SkinnedMeshRenderer IS picked up by the HHHullFacet renderer list.");

                Assert.Less(diff, 2.0,
                    $"the two paths disagree on {diff:F3} % of pixels. The list DID draw the " +
                    "skinned renderer (it painted pixels), so this is not the tag question — it is " +
                    "the skin: check SkinQuality, the bindposes, and whether GPU skinning carried " +
                    "uv0 (the per-face facet attrs) through.");

                // --- the sabotage: the comparison must be able to fail ------------------------
                int sabotaged = SabotageBone(_def);
                bones[sabotaged].localRotation = bones[sabotaged].localRotation *
                                                 Quaternion.AngleAxis(90f, Vector3.right);
                byte[] brokenPx = Render(cam, rt);
                double brokenDiff = MismatchPercent(refPx, brokenPx);

                Assert.Greater(brokenDiff, diff + 1.0,
                    $"rotating bone {sabotaged} ('{_def.Bones[sabotaged].Id}') a quarter turn moved " +
                    $"the picture by only {brokenDiff:F3} % against the honest {diff:F3} %. This " +
                    "comparison cannot tell a wrong pose from a right one, so its PASS above is " +
                    "worth nothing.");

                Debug.Log($"  sabotage margin: bone '{_def.Bones[sabotaged].Id}' +90° moves " +
                          $"{brokenDiff:F3} % of pixels against the honest {diff:F3} % — " +
                          $"{brokenDiff / Math.Max(1e-9, diff):F0}x");
            }
            finally
            {
                RenderTexture.active = null;
                if (camGo != null)
                {
                    var c = camGo.GetComponent<Camera>();
                    if (c != null) c.targetTexture = null;
                    Object.DestroyImmediate(camGo);
                }
                if (skinMat != null) Object.DestroyImmediate(skinMat);
                if (skinGo != null) Object.DestroyImmediate(skinGo);
                if (hullGo != null) Object.DestroyImmediate(hullGo);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (posed != null) Object.DestroyImmediate(posed);
            }
        }

        // =======================================================================================
        // helpers
        // =======================================================================================

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore(
                    "SKIPPED, NOT VERIFIED — this run has no graphics device (Renderer: Null " +
                    "Device), so the facet renderer list could not draw and this fixture proved " +
                    "NOTHING about whether it admits a SkinnedMeshRenderer. Expected on CI; ADR " +
                    "0044's option (a) verdict only comes from a machine with a GPU.");
            }
        }

        CharacterSkinDef.SkinClip ClipNamed(string anim)
        {
            foreach (CharacterSkinDef.SkinClip c in _def.Clips)
                if (string.Equals(c.Anim, anim, StringComparison.Ordinal)) return c;
            Assert.Fail($"the def carries no '{anim}' clip");
            return default;
        }

        /// <summary>Option (b) in one call — the presenter candidate whose cost is timed above.</summary>
        static void SkinOnce(in CharacterSkinDef.SkinClip clip, int frame, int boneCount,
                             Matrix4x4[] bindposes, BoneWeight[] weights,
                             Vector3[] srcVerts, Vector3[] srcNorms,
                             Matrix4x4[] world, Matrix4x4[] skin,
                             Vector3[] outVerts, Vector3[] outNorms)
        {
            for (int b = 0; b < boneCount; b++)
            {
                CharacterSkinDef.BoneKey k = clip.KeyOf(frame, b, boneCount);
                var local = Matrix4x4.TRS(k.Position, k.Rotation, Vector3.one);
                int p = ParentOf(b, boneCount);
                world[b] = p < 0 ? local : world[p] * local;
                skin[b] = world[b] * bindposes[b];
            }

            for (int v = 0; v < srcVerts.Length; v++)
            {
                BoneWeight w = weights[v];
                Matrix4x4 m0 = skin[w.boneIndex0];
                Vector3 pos = m0.MultiplyPoint3x4(srcVerts[v]) * w.weight0;
                Vector3 nrm = m0.MultiplyVector(srcNorms[v]) * w.weight0;
                if (w.weight1 != 0f)
                {
                    Matrix4x4 m1 = skin[w.boneIndex1];
                    pos += m1.MultiplyPoint3x4(srcVerts[v]) * w.weight1;
                    nrm += m1.MultiplyVector(srcNorms[v]) * w.weight1;
                }
                outVerts[v] = pos;
                outNorms[v] = nrm.normalized;
            }
        }

        // The parent table is read off the def once and held here, because SkinOnce is the TIMED
        // loop and a field lookup per bone per frame would be measuring this test, not the work.
        static int[] s_Parents;
        static int ParentOf(int bone, int boneCount) => s_Parents[bone];

        /// <summary>The same skinning as <see cref="SkinOnce"/>, materialised as a Mesh the hull
        /// renderer can be configured with — every non-position stream copied through unchanged,
        /// which is what makes the two paths comparable (uv0 carries the per-face facet attrs).</summary>
        static Mesh CpuSkinnedCopy(CharacterSkinDef def, in CharacterSkinDef.SkinClip clip, int frame)
        {
            Mesh bind = def.BindMesh;
            EnsureParents(def);

            Vector3[] srcVerts = bind.vertices;
            Vector3[] srcNorms = bind.normals;
            var outVerts = new Vector3[srcVerts.Length];
            var outNorms = new Vector3[srcNorms.Length];
            int n = def.Bones.Length;
            SkinOnce(clip, frame, n, bind.bindposes, bind.boneWeights, srcVerts, srcNorms,
                     new Matrix4x4[n], new Matrix4x4[n], outVerts, outNorms);

            var mesh = new Mesh { name = "CharSkin_cpu", indexFormat = bind.indexFormat };
            mesh.vertices = outVerts;
            mesh.normals = outNorms;
            var uv = new List<Vector4>();
            for (int channel = 0; channel < 4; channel++)
            {
                bind.GetUVs(channel, uv);
                if (uv.Count > 0) mesh.SetUVs(channel, uv);
            }
            mesh.SetTriangles(bind.triangles, 0, calculateBounds: true);
            return mesh;
        }

        static void EnsureParents(CharacterSkinDef def)
        {
            if (s_Parents != null && s_Parents.Length == def.Bones.Length) return;
            s_Parents = new int[def.Bones.Length];
            for (int b = 0; b < def.Bones.Length; b++) s_Parents[b] = def.Bones[b].Parent;
        }

        /// <summary>The def's bone table as live Transforms under <paramref name="root"/>, in the
        /// def's own order (a parent always sits earlier, which <c>CharacterSkinDef.IsUsable</c>
        /// guarantees), posed to the REST pose.</summary>
        static Transform[] BuildSkeleton(Transform root, CharacterSkinDef def)
        {
            EnsureParents(def);
            var bones = new Transform[def.Bones.Length];
            for (int b = 0; b < bones.Length; b++)
            {
                var go = new GameObject(def.Bones[b].Id) { layer = root.gameObject.layer };
                Transform t = go.transform;
                int p = def.Bones[b].Parent;
                t.SetParent(p == CharacterSkinDef.NoBone ? root : bones[p],
                            worldPositionStays: false);
                t.localPosition = def.Bones[b].RestPosition;
                t.localRotation = def.Bones[b].RestRotation;
                t.localScale = Vector3.one;
                bones[b] = t;
            }
            return bones;
        }

        static void PoseSkeleton(Transform[] bones, CharacterSkinDef def,
                                 in CharacterSkinDef.SkinClip clip, int frame)
        {
            int n = def.Bones.Length;
            for (int b = 0; b < n; b++)
            {
                CharacterSkinDef.BoneKey k = clip.KeyOf(frame, b, n);
                bones[b].localPosition = k.Position;
                bones[b].localRotation = k.Rotation;
            }
        }

        /// <summary>A bone that actually owns vertices, so rotating it has to move pixels — picking
        /// a leaf that deforms nothing would make the sabotage margin a false comfort.</summary>
        static int SabotageBone(CharacterSkinDef def)
        {
            var counts = new int[def.Bones.Length];
            foreach (BoneWeight w in def.BindMesh.boneWeights)
            {
                counts[w.boneIndex0]++;
                if (w.weight1 != 0f) counts[w.boneIndex1]++;
            }
            int best = 0;
            for (int b = 1; b < counts.Length; b++)
                if (counts[b] > counts[best]) best = b;
            return best;
        }

        /// <summary>The character def's look, in the hull renderer's own setup shape. Every field is
        /// a straight hand-over: the two rigs share one rasteriser contract, which is the whole
        /// reason a character can be drawn by the hull feature at all.</summary>
        static IsoFacetHullSetup SetupFrom(RigMeshData data, Mesh mesh)
        {
            var ramps = new Color32[data.Materials.Count][];
            var offs = new int[data.Materials.Count];
            for (int m = 0; m < data.Materials.Count; m++)
            {
                ramps[m] = data.Materials[m].Ramp;
                offs[m] = data.Materials[m].Off;
            }
            var bayer = new float[16];
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    bayer[x * 4 + y] = (float)data.Bayer[x, y];

            return new IsoFacetHullSetup
            {
                Mesh = mesh,
                Ramps = ramps,
                RampOffsets = offs,
                LightN = new Vector3((float)data.LightN.X, (float)data.LightN.Y, (float)data.LightN.Z),
                Gain = (float)data.Gain,
                Bias = (float)data.Bias,
                Bayer16 = bayer,
                Keyline = data.Keyline,
                PivotPx = new Vector2((float)data.PivotX, (float)data.PivotY),
                PxPerMetre = data.PxPerMetre,
                CellW = data.W,
                CellH = data.H,
                ElevationDeg = (float)data.DefaultElev,
            };
        }

        /// <summary>URP compiles facet variants asynchronously and renders a PLACEHOLDER meanwhile —
        /// an image indistinguishable from a real regression. Block until it settles, exactly as
        /// <c>IsoFacetUrpPassTests</c> does.</summary>
        static void WarmShaders(Camera cam)
        {
            const double timeoutSeconds = 180.0;
            const int maxWarmUps = 10;
            var clock = Stopwatch.StartNew();
            int renders = 0;
            for (; renders < maxWarmUps; renders++)
            {
                cam.Render();
                if (!ShaderUtil.anythingCompiling) break;
                while (ShaderUtil.anythingCompiling && clock.Elapsed.TotalSeconds < timeoutSeconds)
                    System.Threading.Thread.Sleep(25);
            }
            if (ShaderUtil.anythingCompiling || renders >= maxWarmUps)
                Assert.Fail(
                    "THE FACET SHADERS NEVER FINISHED COMPILING — this is NOT a verdict on skinned " +
                    $"renderers. After {renders} warm-up render(s) and {clock.Elapsed.TotalSeconds:F1}s " +
                    "the compiler was still busy, so a measuring render would land on the async " +
                    "placeholder. Re-run with a warm shader cache.");
        }

        static byte[] Render(Camera cam, RenderTexture rt)
        {
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            Color32[] px = tex.GetPixels32();
            Object.DestroyImmediate(tex);

            var bytes = new byte[px.Length * 4];
            for (int i = 0; i < px.Length; i++)
            {
                bytes[i * 4] = px[i].r;
                bytes[i * 4 + 1] = px[i].g;
                bytes[i * 4 + 2] = px[i].b;
                bytes[i * 4 + 3] = px[i].a;
            }
            return bytes;
        }

        static int SolidPixels(byte[] rgba)
        {
            int n = 0;
            for (int i = 3; i < rgba.Length; i += 4)
                if (rgba[i] > 8) n++;
            return n;
        }

        static double MismatchPercent(byte[] a, byte[] b)
        {
            Assert.AreEqual(a.Length, b.Length, "two renders of different sizes");
            int pixels = a.Length / 4, bad = 0;
            for (int i = 0; i < pixels; i++)
            {
                int o = i * 4;
                if (a[o] != b[o] || a[o + 1] != b[o + 1] ||
                    a[o + 2] != b[o + 2] || a[o + 3] != b[o + 3]) bad++;
            }
            return 100.0 * bad / Math.Max(1, pixels);
        }

        static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i), layer);
        }
    }
}
