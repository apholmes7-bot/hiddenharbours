using System;
using HiddenHarbours.Art;
using UnityEngine;

namespace HiddenHarbours.SpikeDeckCharacterMesh
{
    /// <summary>
    /// ⚠️ SPIKE (deck-character-mesh, draft ADR 0024) — NOT shipping content. The baked "mesh
    /// flipbook" for one character build: everything <see cref="IsoFacetHullSetup"/> needs to draw
    /// the character through the EXISTING facet pipeline, plus one Mesh PER ANIMATION FRAME.
    ///
    /// <para><b>Why a flipbook and not one mesh.</b> The boat rigs build a static face list once and
    /// apply heading/rock as transforms afterwards — which is why a hull is ONE mesh (ADR 0022's
    /// load-bearing fact). The character rig does NOT have that shape: <c>facesOf(pose(anim,u,…))</c>
    /// bakes the pose into the vertex positions per frame. So the spike snapshots one mesh per
    /// (anim, frame) — the pose stays discrete exactly as the baked sheets play it — while the
    /// HEADING stays a live transform, continuous. Continuous rotation is the whole point of the
    /// spike; continuous pose interpolation is explicitly NOT attempted (it would need a skinned
    /// mesh or morph targets, i.e. new art-source work — see the draft ADR).</para>
    ///
    /// <para>Produced by the spike baker menu (Hidden Harbours → Spike → Deck Character Mesh) from
    /// <c>docs/art/rigs/characterIsoRig.js</c>, refreshed in place like <c>HullMeshDef</c>. All
    /// facts here are MEASURED or extracted from the rig, never transcribed by hand — including
    /// <see cref="AzimuthCounterClockwise"/>, which comes from <c>CharacterRigAzimuthProbe</c>
    /// (the repo has shipped five mirrored boats off declared conventions).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "DeckCharacterMeshSpike",
                     menuName = "Hidden Harbours/Spike/Deck Character Mesh Def (SPIKE)")]
    public sealed class DeckCharacterMeshSpikeDef : ScriptableObject
    {
        [Serializable]
        public struct Ramp
        {
            public Color32[] Colors;
            public int Offset;
        }

        /// <summary>One animation's worth of pose meshes, in the rig's own frame order.</summary>
        [Serializable]
        public struct PoseClip
        {
            [Tooltip("The rig ANIMS key this clip snapshots (e.g. 'idle', 'hold').")]
            public string Anim;
            [Tooltip("Playback rate, from the rig's own ANIMS.ms (fps = 1000/ms).")]
            public float FramesPerSecond;
            [Tooltip("One baked mesh per frame, pose baked into the vertices; heading stays live.")]
            public Mesh[] Frames;
        }

        [Tooltip("Stable id (append-only).")]
        public string Id = "spike.deck_character_mesh";

        [Tooltip("Which rig + build this was baked from (provenance, for the log).")]
        public string SourceRigPath = "docs/art/rigs/characterIsoRig.js";
        public string Build = "fisher";

        [Header("Render facts (extracted from the rig — the IsoFacetHullSetup payload)")]
        public int CellW = 64;
        public int CellH = 88;
        public Vector2 PivotPx = new Vector2(32f, 80f);
        public int PxPerMetre = 32;
        public float ElevationDeg = 40f;
        public Vector3 LightN;
        public float Gain;
        public float Bias;
        public Color32 Keyline;
        [Tooltip("4×4 ordered-dither thresholds, (v+0.5)/16, row-major [x*4+y].")]
        public float[] Bayer16 = Array.Empty<float>();
        public Ramp[] Ramps = Array.Empty<Ramp>();

        [Header("Pose facts (MEASURED, never declared)")]
        [Tooltip("True when the rig's azimuth turns counter-clockwise — measured by " +
                 "CharacterRigAzimuthProbe at bake time, exactly like the hull bakes.")]
        public bool AzimuthCounterClockwise;

        [Header("The flipbook")]
        public PoseClip[] Clips = Array.Empty<PoseClip>();

        public bool IsUsable()
        {
            if (Ramps == null || Ramps.Length == 0) return false;
            if (Bayer16 == null || Bayer16.Length != 16) return false;
            if (Clips == null || Clips.Length == 0) return false;
            foreach (var clip in Clips)
            {
                if (clip.Frames == null || clip.Frames.Length == 0) return false;
                foreach (var m in clip.Frames)
                    if (m == null) return false;
            }
            return true;
        }

        public bool TryGetClip(string anim, out PoseClip clip)
        {
            if (Clips != null)
                foreach (var c in Clips)
                    if (string.Equals(c.Anim, anim, StringComparison.Ordinal)) { clip = c; return true; }
            clip = default;
            return false;
        }

        /// <summary>The Art-side payload for ONE pose frame — everything except the mesh is shared
        /// across the whole flipbook, which is what makes the per-frame renderer cheap to set up.</summary>
        public IsoFacetHullSetup BuildSetup(Mesh poseMesh)
        {
            var ramps = new Color32[Ramps.Length][];
            var offsets = new int[Ramps.Length];
            for (int i = 0; i < Ramps.Length; i++)
            {
                ramps[i] = Ramps[i].Colors;
                offsets[i] = Ramps[i].Offset;
            }
            return new IsoFacetHullSetup
            {
                Mesh = poseMesh,
                Ramps = ramps,
                RampOffsets = offsets,
                LightN = LightN,
                Gain = Gain,
                Bias = Bias,
                Bayer16 = Bayer16,
                Keyline = Keyline,
                PivotPx = PivotPx,
                PxPerMetre = PxPerMetre,
                CellW = CellW,
                CellH = CellH,
                ElevationDeg = ElevationDeg,
            };
        }
    }
}
