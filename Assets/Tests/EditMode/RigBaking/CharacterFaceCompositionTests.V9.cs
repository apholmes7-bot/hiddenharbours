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
        /// The face a v9 def binds is, on every preset, one group per slot, each the slot's rest group
        /// and the group the preset rests on (<see cref="CharacterSkinExtractor.DefaultFaceGroups9"/>
        /// refuses any other), and the rig draws every one of those groups on that preset's figure,
        /// so the bound face is never missing a feature.
        /// </summary>
        [Test]
        public void V9_TheBoundFaceIsTheRigsRestFaceOnEveryPreset()
        {
            IRigScriptHost host = V9Host;
            string g = CharacterSkinExtractor.V9GlobalName;
            int slots = (int)host.EvaluateNumber($"Object.keys({g}.FACE_SLOTS).length");
            Assert.Greater(slots, 0, "Rig 9 declares no face slot.");

            foreach (string preset in CharacterSkinExtractor.Presets9(host))
            {
                string[] groups = CharacterSkinExtractor.DefaultFaceGroups9(host, preset);
                Assert.AreEqual(slots, groups.Length,
                    $"{preset}: the rig has {slots} face slots and the def binds {groups.Length} groups.");
                foreach (string group in groups)
                {
                    int drawn = (int)host.EvaluateNumber(
                        $"{g}.bindMesh('{preset}').filter(function(f){{return f.group==='{group}';}}).length");
                    Assert.Greater(drawn, 0,
                        $"{preset}: the rest group '{group}' draws no face, so the bound face lacks it.");
                }
            }
        }
    }
}
