using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE FACET SIGN, ASKED OF THE WHOLE CAST — ALL TEN, BY NAME.</b>
    ///
    /// <para><see cref="CharacterMeshAssetBaker.MeasureFacetSign"/> decides which way a figure
    /// turns, and a bake that reads it wrong ships a character mirrored. Until 2026-09-18 CI only
    /// ever asked it about the PLAYER (<see cref="CharacterMeshBakeGuardTests"/>), and the first
    /// time the nine cast presets met it — the Phase C bake, PR #863 — it refused <c>girl</c>:
    /// 9 vs 35 px on idle frame 0, a 3.89x margin against a 4x bar, ONE PIXEL short. Every one of
    /// the ten had read the SAME sign; only her margin was short, because her rest pose is the
    /// cast's most mirror-symmetric East view. Nine characters had already been written when the
    /// tenth stopped the bake.</para>
    ///
    /// <para>So the question gets asked for the whole register, here, without an editor. <b>The bar
    /// is not what is under test.</b> It is fixed at 4x and it stays there — the owner's ruling of
    /// 09-18 is that a guard which cannot read a pose asks MORE POSES, it does not lower its bar
    /// for the one figure it was right to doubt. What is under test is that the adjudication can
    /// read every figure the bake will hand it, and that the ten agree with each other.</para>
    ///
    /// <para><b>The per-frame table is logged for every preset</b>, which makes a green run of this
    /// test the standing record of which frame each character's sign rests on and by how much — the
    /// thing that did not exist on 09-17, and whose absence is why the bake was the first to find
    /// out. Every preset is read even after one refuses: a bake stops at the first failure, but a
    /// guard that stopped there would hide the other nine on the run that matters.</para>
    ///
    /// <para>No asset is written and no editor is needed: <see cref="RigScriptHostFactory"/> runs on
    /// CI, and everything below is V8 and pixels.</para>
    /// </summary>
    public class CharacterCastFacetSignGuardTests
    {
        [Test]
        public void EveryCastPresetReadsItsFacetSign_AndTheTenAgree()
        {
            (string preset, string stem)[] order = CharacterSkinAssetBaker.CastBakeOrder();
            Assert.AreEqual(10, order.Length,
                "the cast register is no longer ten presets. This guard is worth exactly the list " +
                "it iterates, so a preset added to CharacterRigBakeMenu.Cast without landing here " +
                "would bake unasked: re-read the register, then move this number.");

            using var host = RigScriptHostFactory.Create();
            CharacterPoseMeshExtractor.Load(host);

            var refused = new List<string>();
            var direct = new List<string>();
            var summary = new StringBuilder();
            string playerDecidingFrame = null;

            foreach ((string preset, string stem) in order)
            {
                bool negates;
                string report;
                try
                {
                    negates = CharacterMeshAssetBaker.MeasureFacetSign(host, preset, out report);
                }
                catch (InvalidOperationException ex)
                {
                    refused.Add(preset);
                    Debug.Log($"[cast-facet-sign] {preset} ({stem}) REFUSED\n{ex.Message}");
                    summary.Append($"\n   {preset,-9} REFUSED - no frame read could tell");
                    continue;
                }

                Debug.Log($"[cast-facet-sign] {preset} ({stem}) => " +
                          (negates ? "NEGATED" : "direct") + "\n" + report);

                StringAssert.Contains("=> deciding frame:", report,
                    preset + "'s report does not name the frame its verdict rests on");
                StringAssert.Contains("=> frames read (", report,
                    preset + "'s report carries no per-frame table — the bake, this guard and any " +
                    "future reader of a CI log all lean on that table to see WHY the sign reads " +
                    "the way it does");

                if (!negates) direct.Add(preset);
                if (preset == CharacterRigBakeMenu.PlayerPreset)
                    playerDecidingFrame = DecidingFrame(report);

                summary.Append($"\n   {preset,-9} {(negates ? "NEGATED" : "DIRECT ")}  " +
                               $"{Silhouette(report),-14}  @ {Deciding(report)}");
            }

            Debug.Log("[cast-facet-sign] the ten — verdict, deciding margin, deciding frame:" + summary);

            CollectionAssert.IsEmpty(refused,
                "MeasureFacetSign cannot read the sign for: " + string.Join(", ", refused) + ".\n" +
                "The cast bake stops dead at the first of those, so this is a bake blocker. The fix " +
                "is NOT to lower the 4x bar — that spends the guard's whole point on the one figure " +
                "it was right to doubt. Add or change the FRAMES the sign is read from " +
                "(CharacterMeshAssetBaker.FacetSignFrames) so a pose with real asymmetry is in the " +
                "list, and press this test again. Each refusal's per-frame table is in the log above.");

            CollectionAssert.IsEmpty(direct,
                "these presets read their facet sign as DIRECT while the rest of the cast reads " +
                "NEGATED: " + string.Join(", ", direct) + ".\n" +
                "One rig, one turntable convention — every build in it must turn the same way. A " +
                "disagreement means either that preset's build is mirrored in the rig SOURCE (an " +
                "art-director question, docs/art/rigs/**) or the adjudication has started reading " +
                "noise. Do NOT flip an expectation to match it, and do not bake until it is " +
                "understood: the answer belongs in ADR 0044 §6.");

            Assert.AreEqual("idle 0", playerDecidingFrame,
                "the Player's facet sign no longer rests on idle frame 0. That frame is FIRST in " +
                "the fixed list on purpose: it is the reading every guard in this suite was written " +
                "against, and the 4x-margin assertion in " +
                "CharacterSkinBakeGuardTests.TheTurntableSignCarriesItsSabotageMargin_AndTheDefStoredIt " +
                "parses the deciding frame's counts out of the same report (fisher, 11.1x). If a " +
                "change to the frame list moved it, re-baseline those guards and ADR 0044 in the " +
                "same PR — do not let this drift silently.");
        }

        // ---- reading the report the way a human reads it ----------------------------------------

        /// <summary>The report line naming the frame the verdict rests on, without its marker.</summary>
        static string Deciding(string report) => LineAfter(report, "=> deciding frame:");

        /// <summary>Just the state and frame of that line — "idle 0" — without the tally after it.</summary>
        static string DecidingFrame(string report)
        {
            string line = Deciding(report);
            int comma = line.IndexOf(',');
            return (comma < 0 ? line : line[..comma]).Trim();
        }

        /// <summary>The deciding frame's two silhouette counts, "N vs M px". Read through the SAME
        /// marker CharacterSkinBakeGuardTests parses, on purpose: if the report shape ever moves,
        /// the summary table logged here goes blank in the same run that guard reds.</summary>
        static string Silhouette(string report) =>
            LineAfter(report, "SILHOUETTE (opaque-vs-transparent):");

        static string LineAfter(string report, string marker)
        {
            int i = report.IndexOf(marker, StringComparison.Ordinal);
            if (i < 0) return "(absent)";
            string tail = report[(i + marker.Length)..];
            int end = tail.IndexOf('\n');
            return (end < 0 ? tail : tail[..end]).Trim();
        }
    }
}
