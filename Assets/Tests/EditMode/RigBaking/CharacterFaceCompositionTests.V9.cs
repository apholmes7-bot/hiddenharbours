using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using Debug = UnityEngine.Debug;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>RIG 9'S FACE.</b> Rig 9 draws the face itself, as face groups in slots
    /// (<c>FACE_SLOTS</c>: eyes, brows, mouth), and a v9 def binds each slot's REST group; the
    /// other groups are what the rig draws when a clip blinks, looks or talks. Ruling 8 (2026-09-24)
    /// has the engine play blink and gaze from the rig, so the whole face has to be declared and fit
    /// a v9 skin, not only the part bound today.
    /// </summary>
    public partial class CharacterFaceCompositionTests
    {
        IRigScriptHost _v9Host;

        IRigScriptHost V9Host
        {
            get
            {
                if (_v9Host == null)
                {
                    _v9Host = RigScriptHostFactory.Create();
                    CharacterSkinExtractor.Load9(_v9Host);
                }
                return _v9Host;
            }
        }

        [OneTimeTearDown]
        public void DisposeV9()
        {
            _v9Host?.Dispose();
            _v9Host = null;
        }

        /// <summary>
        /// Every preset's whole face, every group of every slot, paints only materials the contract
        /// declares (the reader refuses any other), and the materials of the whole figure with it fit
        /// <see cref="CharacterSkinDef.MaxMaterials"/> for v9. The whole face holds at least what the
        /// rest face paints, or the rest face would not be part of it.
        /// </summary>
        [Test]
        public void V9_EveryPresetsWholeFaceIsDeclaredAndFitsTheV9MaterialLimit()
        {
            IRigScriptHost host = V9Host;
            int limit = CharacterSkinDef.MaxMaterials(ToneRule.V9);
            var counts = new StringBuilder();
            var over = new List<string>();

            foreach (string preset in CharacterSkinExtractor.Presets9(host))
            {
                int whole = CharacterSkinExtractor.ReadMaterials9(host, preset,
                    CharacterSkinExtractor.AllFacesJs9(preset)).Count;
                int rest = CharacterSkinExtractor.ReadMaterials9(host, preset,
                    CharacterSkinExtractor.DefaultFaceMeshJs9(host, preset)).Count;
                counts.Append($" {preset} {rest}/{whole}");

                Assert.GreaterOrEqual(whole, rest,
                    $"{preset}: the whole face paints {whole} materials and the rest face alone {rest}.");
                if (whole > limit) over.Add($"{preset} ({whole})");
            }

            Debug.Log($"[CharacterFaceCompositionTests] v9 materials, rest face / whole face, limit {limit}:{counts}");
            Assert.IsEmpty(over, $"Presets whose whole face takes the figure over the {limit} materials a v9 " +
                                 "skin carries, so blink and gaze could not be bound: " + string.Join(", ", over));
        }

        /// <summary>
        /// Since character PR 2a a v9 def binds EVERY face group of rig 9's <c>GROUP_ORDER</c> and
        /// draws one per slot per frame. So on every preset the rig's own bind mesh must hold faces for
        /// every one of those groups — a group with none is a state no clip, blink or glance could
        /// ever show — and the face it rests on must be one group per slot, each slot's first
        /// (<see cref="CharacterSkinExtractor.DefaultFaceGroups9"/> refuses any other). The group
        /// count is the rig's, never a number written here.
        /// </summary>
        [Test]
        public void V9_EveryPresetDrawsEveryFaceGroupAndRestsOnTheRigsRestFace()
        {
            IRigScriptHost host = V9Host;
            string g = CharacterSkinExtractor.V9GlobalName;
            int slots = (int)host.EvaluateNumber($"Object.keys({g}.FACE_SLOTS).length");
            int declared = (int)host.EvaluateNumber($"{g}.GROUP_ORDER.length");
            Assert.Greater(slots, 0, "Rig 9 declares no face slot.");
            string[] order = CharacterSkinExtractor.FaceGroupOrder9(host);
            Assert.AreEqual(declared, order.Length,
                $"Rig 9's GROUP_ORDER holds {declared} groups and the bake reads {order.Length}.");
            var counts = new StringBuilder();

            foreach (string preset in CharacterSkinExtractor.Presets9(host))
            {
                string[] rest = CharacterSkinExtractor.DefaultFaceGroups9(host, preset);
                Assert.AreEqual(slots, rest.Length,
                    $"{preset}: the rig has {slots} face slots and the preset rests on {rest.Length} groups.");
                CollectionAssert.IsSubsetOf(rest, order,
                    $"{preset}: the rest face [{string.Join(", ", rest)}] is not among the bound groups.");
                int least = int.MaxValue; string leastAt = "";
                foreach (string group in order)
                {
                    int drawn = (int)host.EvaluateNumber(
                        $"{g}.bindMesh('{preset}').filter(function(f){{return f.group==='{group}';}}).length");
                    Assert.Greater(drawn, 0,
                        $"{preset}: the face group '{group}' draws no face, so the def could never show it.");
                    if (drawn < least) { least = drawn; leastAt = group; }
                }
                counts.Append($" {preset} (fewest {least}, {leastAt})");
            }
            Debug.Log($"[CharacterFaceCompositionTests] v9: every preset binds all {order.Length} face groups:{counts}");
        }
    }
}
