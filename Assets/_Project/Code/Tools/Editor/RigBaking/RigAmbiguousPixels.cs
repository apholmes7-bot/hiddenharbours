using System;
using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>The pixels the rig's own renderer cannot answer, and the proof that they are those pixels.</b>
    ///
    /// <para><b>What they are.</b> The rigs make a zero-thickness surface visible from both sides by
    /// pushing every face TWICE — <c>q</c>, then its exact reverse (<c>doryIsoRig.js:241</c>). The two
    /// carry identical vertices and an identical <c>db</c>, so they are exactly coplanar and their
    /// interpolated depth is the same real number; they carry OPPOSITE normals, so they shade several
    /// ramp steps apart (the dory's blade: ramp index 5 against index 0 — light wood against
    /// near-black). Which one you see is therefore decided by the last bit of a barycentric sum, and
    /// by nothing else.</para>
    ///
    /// <para><b>Why they must be excluded rather than tolerated — MEASURED, not argued.</b> Dory oar,
    /// port, 8 headings × 8 stroke phases, 25,646 inked pixels, 1,519 of them covered by both members
    /// of a pair. The rig's own choice among the two agrees with:</para>
    /// <list type="bullet">
    ///   <item><c>deff &lt; zbuf</c>, first wins (what the CPU oracle does): <b>50.6%</b></item>
    ///   <item><c>deff &lt;= zbuf</c>, last wins (what the facet shader does): <b>50.4%</b></item>
    ///   <item>the rig's own <c>Float32Array</c> z-buffer quantisation, replayed exactly: <b>44.6%</b></item>
    ///   <item>"the front-facing normal wins": <b>51.2%</b> — "the lit face wins": <b>51.2%</b> —
    ///   "the dark face wins": <b>48.8%</b></item>
    /// </list>
    /// <para>Six rules, every one a coin flip. <b>No renderer reproduces the rig at these pixels, and
    /// neither would the rig re-run with any other arithmetic.</b> A golden master that compares them
    /// is not measuring the bake; it is measuring which way a tie fell.</para>
    ///
    /// <para><b>Why this is not "widening the floor".</b> A tolerance says <i>some pixels may differ
    /// and I will not look at which</i>. This does the opposite. It identifies each excluded pixel
    /// STRUCTURALLY — the winning face has an exact-reverse twin (bit-identical vertices, reversed
    /// order: <see cref="RigMeshExtractor.FindReverseDuplicatePartners"/>) and that twin also covers
    /// the pixel and would paint a different colour — and it then still ASSERTS something about every
    /// one of them: that the rig painted one of the two colours our own render computed there
    /// (<see cref="Result.ReferenceMatchedNoCandidate"/>). A blade in the wrong place, feathered to
    /// the wrong angle, or shaded off the wrong normal fails that check, because its two candidate
    /// colours are then not the rig's colour. MEASURED: 0 exceptions in 1,519 for the committed bake,
    /// against 57 for a naive shortest-arc pose.</para>
    ///
    /// <para><b>Why the test is structural and not a depth tolerance.</b> In the rig the pair is
    /// exactly coplanar in f64. In the MESH it is float32, the quad is no longer exactly planar, and
    /// the two fan triangulations' interpolated depths drift apart by up to ~2e-8 — which is above any
    /// tolerance tight enough to be meaningful and below any that would be safe. The twin relationship
    /// is exact and needs no threshold. <see cref="Result.WorstTwinDepthGap"/> reports the drift so
    /// the coplanarity claim stays measured rather than assumed.</para>
    ///
    /// <para>⚠️ On the GPU this same ambiguity is z-fighting, and a shimmering blade is a real risk
    /// the day an oar turns. The mesh keeps both faces because dropping one is measurably FURTHER from
    /// the rig (worst cluster 49 → 60) and because the deterministic alternative — shading from
    /// whichever side faces the camera, i.e. <c>SV_IsFrontFace</c> in the facet pass — is a change to
    /// art-pipeline's shader, not a bake decision.</para>
    /// </summary>
    public static class RigAmbiguousPixels
    {
        public sealed class Result
        {
            /// <summary>True where the oracle has no answer. Feed to
            /// <see cref="RigMeshReferenceRasterizer.Compare(byte[],byte[],int,int,bool[])"/>.</summary>
            public bool[] Mask;

            /// <summary>How many pixels are ambiguous.</summary>
            public int Count;

            /// <summary>
            /// Ambiguous pixels where the reference painted a colour NEITHER twin computed.
            /// <b>Must be zero.</b> This is what keeps the exclusion honest: a real assertion about
            /// every excluded pixel, which fails the moment the geometry or the shading moves.
            /// </summary>
            public int ReferenceMatchedNoCandidate;

            /// <summary>Largest <c>|deff|</c> difference seen between two twins at one pixel — the
            /// measured cost of float32 breaking the quad's planarity. Reported so "exactly coplanar"
            /// stays a measurement.</summary>
            public double WorstTwinDepthGap;

            /// <summary>The first few offenders, as (x, y, referenceColour), for a failure message.</summary>
            public readonly List<(int x, int y, int reference)> Offenders =
                new List<(int, int, int)>();
        }

        /// <summary>
        /// Derives the mask from a paint trace whose <see cref="RigPaintTrace.Probe"/> covered the
        /// whole cell, checking every ambiguous pixel against <paramref name="reference"/>.
        /// </summary>
        /// <param name="partners">From
        /// <see cref="RigMeshExtractor.FindReverseDuplicatePartners"/>, indexed by face.</param>
        public static Result From(RigPaintTrace trace, byte[] reference, int width, int height,
                                  int[] partners)
        {
            if (trace == null) throw new ArgumentNullException(nameof(trace));
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            if (partners == null) throw new ArgumentNullException(nameof(partners));
            if (trace.Winner == null)
                throw new ArgumentException(
                    "The trace carries no Winner array — it was not produced by a paint. Pass the " +
                    "trace the render filled in, not a fresh one.", nameof(trace));
            if (reference.Length != width * height * 4)
                throw new ArgumentException(
                    $"{width}×{height} does not describe a {reference.Length}-byte RGBA cell.");

            var byPixel = new Dictionary<int, List<RigPaintTrace.Sample>>();
            foreach (RigPaintTrace.Sample s in trace.Samples)
            {
                if (!byPixel.TryGetValue(s.Pixel, out List<RigPaintTrace.Sample> l))
                    byPixel[s.Pixel] = l = new List<RigPaintTrace.Sample>();
                l.Add(s);
            }

            var result = new Result { Mask = new bool[width * height] };

            foreach (KeyValuePair<int, List<RigPaintTrace.Sample>> kv in byPixel)
            {
                int px = kv.Key;
                int winnerTri = trace.Winner[px];
                if (winnerTri < 0) continue;

                RigPaintTrace.Sample winner = default;
                bool haveWinner = false;
                foreach (RigPaintTrace.Sample s in kv.Value)
                    if (s.Tri == winnerTri) { winner = s; haveWinner = true; break; }
                if (!haveWinner || winner.Face < 0 || winner.Face >= partners.Length) continue;

                int twinFace = partners[winner.Face];
                if (twinFace < 0) continue;

                // The twin is coplanar by construction, so if it covered this pixel at all the choice
                // between them was arbitrary. Any of its fan triangles will do.
                bool twinDisagrees = false;
                foreach (RigPaintTrace.Sample s in kv.Value)
                {
                    if (s.Face != twinFace) continue;
                    double gap = Math.Abs(s.DepthEff - winner.DepthEff);
                    if (gap > result.WorstTwinDepthGap) result.WorstTwinDepthGap = gap;
                    if (s.PackedColor != winner.PackedColor) twinDisagrees = true;
                }
                if (!twinDisagrees) continue;

                result.Mask[px] = true;
                result.Count++;

                int refColour = (reference[px * 4] << 16) | (reference[px * 4 + 1] << 8)
                                | reference[px * 4 + 2];
                bool refOpaque = reference[px * 4 + 3] > 0;
                bool refIsACandidate = false;
                if (refOpaque)
                    foreach (RigPaintTrace.Sample s in kv.Value)
                        if ((s.Face == winner.Face || s.Face == twinFace) && s.PackedColor == refColour)
                        { refIsACandidate = true; break; }

                if (refIsACandidate) continue;

                result.ReferenceMatchedNoCandidate++;
                if (result.Offenders.Count < 8)
                    result.Offenders.Add((px % width, px / width, refColour));
            }

            return result;
        }
    }
}
