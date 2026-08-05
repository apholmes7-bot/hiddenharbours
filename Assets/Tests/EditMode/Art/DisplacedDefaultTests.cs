using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards for the 2026-08-05 RETIREMENT FLIP — the owner's *"look at our 2d ocean sprite and see
    /// what's missing in the 3d ocean, so that we can retire the 2d"*.
    ///
    /// <para><b>What the flip is, and what it deliberately is not.</b> The displaced sea becomes the
    /// water the player sees, decided by ONE piece of data
    /// (<c>GameConfig.DisplacedWater.DefaultOn</c>) rather than by a serialized bool in each built
    /// scene — so reverting the whole game is one field and no builder re-run. The flat face is
    /// <b>not</b> removed, and cannot simply be: it is still the edit-mode sea (ADR 0014 — the owner
    /// paints coasts against water he can see, and nothing in the displaced path runs outside Play),
    /// and it is still the only water on any camera the off-screen feature skips. Physical removal is
    /// a later decision after the owner's verdict.</para>
    ///
    /// <para><b>⚠️ The trap this suite mostly exists for.</b> <c>DisplacedWaterSettings</c> is a
    /// STRUCT that <c>GameConfig.asset</c> serializes field by field. A new field absent from that
    /// YAML deserializes to its C# default — <c>false</c> for a bool — and NOT to
    /// <c>DisplacedWaterSettings.Default</c>. So shipping <c>DefaultOn = true</c> in code while the
    /// asset says nothing would leave the flip silently off in every built scene, looking exactly
    /// like a change that did not work. The asset must carry the key, and
    /// <see cref="TheConfigAsset_ActuallySerializesTheFlip_TheSilentRevertTrap"/> is the guard.</para>
    ///
    /// <para>All headless — config values, component behaviour with no renderer needed, and one YAML
    /// read. Nothing here says the displaced sea LOOKS right; that is the owner's GPU.</para>
    /// </summary>
    public class DisplacedDefaultTests
    {
        private const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";
        private const string SurfacePath = "Assets/_Project/Code/Art/DisplacedWaterSurface.cs";

        private static string Read(string repoRelative)
        {
            string path = Path.Combine(Application.dataPath, "..", repoRelative);
            Assert.IsTrue(File.Exists(path), $"missing: {repoRelative}");
            return File.ReadAllText(path);
        }

        // ==== the flip itself =====================================================================

        [Test]
        public void TheShippedDefault_IsTheDisplacedSea()
        {
            Assert.IsTrue(DisplacedWaterSettings.Default.DefaultOn,
                "the retirement flip's whole point: a new game starts on the displaced sea. If this " +
                "is false the change is a no-op the owner cannot see.");
        }

        [Test]
        public void TheConfigAsset_ActuallySerializesTheFlip_TheSilentRevertTrap()
        {
            // ⚠️ THE defect this suite exists for, and it is invisible to every value-based check:
            // GameConfig.asset already serializes the DisplacedWater block field by field, so a field
            // it does NOT list deserializes to the C# default (false) rather than to
            // DisplacedWaterSettings.Default. The code would read true, the game would read false,
            // and the flip would look like it simply did not work.
            string yaml = Read(ConfigAssetPath);
            var block = Regex.Match(yaml, @"^  DisplacedWater:\r?\n((?:    .*\r?\n)+)", RegexOptions.Multiline);
            Assert.IsTrue(block.Success, "GameConfig.asset does not serialize a DisplacedWater block");
            var m = Regex.Match(block.Groups[1].Value, @"^\s*DefaultOn:\s*(\d+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success,
                "GameConfig.asset's DisplacedWater block does not list DefaultOn. Unity will " +
                "deserialize it as FALSE — the flip silently reverts in every built scene while the " +
                "code still says true. Add 'DefaultOn: 1' to the asset.");
            Assert.AreEqual("1", m.Groups[1].Value,
                "the shipped config turns the flip OFF — intentional only if the owner ruled the " +
                "flat sea back; otherwise the asset and DisplacedWaterSettings.Default disagree.");
        }

        [Test]
        public void TheSurface_TakesItsStartingSideFromTheConfig_NotFromTheScene()
        {
            var go = new GameObject("Sea", typeof(SpriteRenderer));
            GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                var surface = go.AddComponent<DisplacedWaterSurface>();
                surface.Configure(Vector2.zero, new Vector2(64f, 48f), null, config);

                config.DisplacedWater.DefaultOn = true;
                Assert.IsTrue(surface.DefaultOn, "a wired config decides, and it says displaced");

                config.DisplacedWater.DefaultOn = false;
                Assert.IsFalse(surface.DefaultOn,
                    "…and it decides in BOTH directions — the owner's revert must be one field, not " +
                    "a builder re-run over every region (the standing 'scene-wired is not " +
                    "builder-wired' defect).");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void WithNoConfigWired_TheSurfaceKeepsTheSceneValue_SoFixturesAreUnchanged()
        {
            // EditMode fixtures and hand-built test scenes wire no config. They must behave exactly as
            // they did before the flip, or this change quietly rewrites the baseline of every other
            // suite that stands up a sea.
            var go = new GameObject("Sea", typeof(SpriteRenderer));
            try
            {
                var surface = go.AddComponent<DisplacedWaterSurface>();
                Assert.IsFalse(surface.DefaultOn,
                    "with no config, the serialized fallback (flat) still rules — an unwired fixture " +
                    "must not silently switch seas.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ==== what the flip must NOT do ===========================================================

        [Test]
        public void EditMode_StaysOnTheFlatFace_BecauseThatIsWhereCoastsArePainted()
        {
            // ADR 0014: the owner designs coasts SEEING this water in the scene view, and the
            // displaced path cannot draw there at all — its off-screen pass needs a live camera and
            // render graph. So every entry point is guarded on isPlaying, and the parity table's first
            // row is "edit mode: flat only, permanently". A flip that hid the flat sprite at edit time
            // would leave the owner painting a coast against a blank plane.
            string src = Read(SurfacePath);
            // Matched on the DECLARATION, not the name: `SetDisplaced` is also CALLED from Update,
            // and a window opened at the call site would happen to contain the right guard for the
            // wrong reason.
            string[] declarations =
            {
                "private void OnEnable()",
                "private void Update()",
                "public void SetDisplaced(bool displaced)",
            };
            foreach (string declaration in declarations)
            {
                int at = src.IndexOf(declaration, System.StringComparison.Ordinal);
                Assert.Greater(at, 0, $"'{declaration}' not found in DisplacedWaterSurface");
                // the guard must fall inside this method — i.e. before the next member's opening
                int guard = src.IndexOf("if (!Application.isPlaying) return;", at,
                                        System.StringComparison.Ordinal);
                int end = src.IndexOf("\n\n        ", at, System.StringComparison.Ordinal);
                if (end < 0) end = src.Length;
                Assert.IsTrue(guard > at && guard < end,
                    $"'{declaration}' must return early outside Play — edit mode shows the FLAT sea, " +
                    "and the displaced path has no way to draw there (its off-screen pass needs a " +
                    "live camera and render graph).");
            }
        }

        [Test]
        public void TurningItOffRestoresTheFlatFaceExactly_TheRevertContract()
        {
            // The flip is reversible by construction: OFF re-enables the flat renderer, hides the
            // children and empties the registry, so the feature records nothing and the pre-ADR-0023
            // path runs untouched. Asserted on the source because exercising it needs Play mode; the
            // three statements together ARE the contract.
            string src = Read(SurfacePath);
            int at = src.IndexOf("private void Deactivate()", System.StringComparison.Ordinal);
            Assert.Greater(at, 0, "Deactivate not found");
            string body = src.Substring(at, System.Math.Min(900, src.Length - at));
            StringAssert.Contains("_flatRenderer.enabled = true", body,
                "OFF must bring the flat sprite back, or the sea disappears instead of reverting");
            StringAssert.Contains("DisplacedWaterRegistry.Unregister(this)", body,
                "OFF must empty the registry, or the off-screen water pass keeps being recorded");
            StringAssert.Contains("DisplacedSea.Clear(this)", body,
                "OFF must clear the Core seam, or the fleet keeps riding a sea nothing is drawing");
        }

        [Test]
        public void TheFlatFaceSurvives_NothingInThisChangeDeletesIt()
        {
            // The brief is explicit that physical removal is a FOLLOW-UP after the owner's verdict,
            // never bundled with the flip — and that any plan starting by deleting WaterSurface.cs is
            // wrong, because that component is the sim→material BRIDGE (it bakes _HeightTex and pushes
            // every uniform), not merely a look. The displaced chunks read their uniforms by COPYING
            // the flat renderer's property block, so deleting the flat renderer would take the
            // displaced sea with it.
            Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath,
                              "_Project/Code/Art/WaterSurface.cs")),
                "WaterSurface is the uniform bridge the displaced surface copies FROM — it survives " +
                "the retirement of the flat sprite FACE, and the two are not the same thing.");

            string src = Read(SurfacePath);
            StringAssert.Contains("_flatRenderer.GetPropertyBlock(_mpb);", src,
                "the ONE-SEA rule: displaced chunks take their sim uniforms by copying the flat " +
                "renderer's block. The flat RENDERER is load-bearing even when its face is hidden.");
        }
    }
}
