using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.EditMode.RigBaking
{
    /// <summary>
    /// <b>The rail — the level the sea can rise to — is derived from each hull's own geometry, and
    /// it must land above her deck line.</b>
    ///
    /// <para>The interior mask's second half (ADR 0023) is that the sea has a LEVEL: a face lying
    /// wholly above <see cref="RigMeshInteriorClassifier.DeriveRailHeight"/> is out of reach
    /// whichever way it faces. That is what retired the over-the-lip family of defects — level rays
    /// entering over a gunwale, overhanging washboards wetted via their own underside, elevated bow
    /// decks — without naming any of them (owner, 2026-07-26: <i>"fixing one surface at a time feels
    /// tedious, there must be a better solution, like defining the exterior walls of a boat"</i>).</para>
    ///
    /// <para><b>Why the deck line is the right floor to assert against.</b> A rail at or below the
    /// deck would put the deck itself out of the sea's reach — harmless-looking, since the mask only
    /// ever removes wettable faces — but it would also mean the derivation had lost the hull side
    /// and locked onto something inboard. The deck height is hand-measured off the rigs' own
    /// <c>DECK</c> constants and shares no code with the derivation, so this is two independent
    /// numbers agreeing, not a tautology.</para>
    ///
    /// <para>Sabotage: raise <see cref="RigMeshInteriorClassifier.RailBeamFraction"/> toward 1.0
    /// until only the widest vertex at each station counts, and the rail collapses onto the
    /// waterline on the hulls with tumblehome.</para>
    /// </summary>
    public sealed class HullRailHeightTests
    {
        [Test]
        public void EveryHullsRail_SitsAboveHerDeckLine()
        {
            var failures = new List<string>();
            var table = new StringBuilder("hull / rail / deck (m above keel)\n");

            foreach (var hull in HullMeshFleet.Hulls)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                Assert.NotNull(def, $"{hull.Key}: no HullMeshDef at {hull.MeshAssetPath}");

                using var host = RigScriptHostFactory.Create();
                RigMeshData data = RigMeshExtractor.ExtractFrom(host, hull.ScriptPath, hull.GlobalName);
                float rail = RigMeshInteriorClassifier.DeriveRailHeight(data);
                float deck = def.WatertightDeckHeightMeters;

                table.AppendLine($"  {hull.Key,-16} {rail,7:0.000} {deck,7:0.00}");

                if (rail >= float.MaxValue)
                    failures.Add($"{hull.Key}: no rail could be derived (degenerate geometry)");
                else if (rail < deck)
                    failures.Add($"{hull.Key}: rail {rail:0.###} is BELOW her deck line {deck:0.##} — " +
                                 "the derivation has lost the hull side, and her deck is now out of " +
                                 "the sea's reach");
            }

            Debug.Log("[rail] " + table);
            Assert.IsEmpty(failures,
                "The sea's level must clear the deck on every hull. " + string.Join("; ", failures));
        }
    }
}
