using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Art.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards that re-slicing an already-sliced sheet is a genuine no-op on its <c>.meta</c>.
    ///
    /// <para><b>The regression.</b> <see cref="SpriteSheetSlicer"/> documents itself as "idempotent by
    /// sprite NAME", but <c>BuildRects</c> minted <c>spriteID = GUID.Generate()</c> for every cell on
    /// every run. Since <c>RigBakeMenu.BakeLobsterBoat</c> slices the WHOLE manifest at the end of each
    /// bake, baking one boat rewrote the spriteID of every slice in every manifest sheet — ~20 unrelated
    /// <c>.meta</c> files showing as modified for no authored change.</para>
    ///
    /// <para><b>Why it was noise and not breakage</b> — worth recording, because the reflex is to assume a
    /// changed GUID orphans a reference. Nothing in this repo resolves a sprite by <c>spriteID</c>:
    /// BoatVisualDef, prefabs and the wake SpriteLibrary all serialize
    /// <c>{fileID: &lt;internalID&gt;, guid: &lt;TEXTURE guid&gt;}</c>, and the texture guid lives in the
    /// <c>.meta</c> header (untouched) while the internalID is held stable across a re-slice by the
    /// <c>nameFileIdTable</c> keyed on the slice NAME (also untouched, since the names are derived from
    /// the stem and index). That is why the observed churn moved <c>spriteID</c> while every
    /// <c>internalID</c> stayed put. The hazard was diff noise burying a real slice change — not a boat
    /// losing its skin.</para>
    ///
    /// <para><b>What actually would break refs</b> is a change to the RECT SET (count, order, or names),
    /// which re-keys the nameFileIdTable and mints new internalIDs. The per-kit slice tests
    /// (<see cref="PuntSheetSliceTests"/> and friends) guard that; this file guards the churn.</para>
    ///
    /// <para>Deliberately exercised on the SMALL manifest sheets only. The mechanism is shared by every
    /// entry, and the hull sheets run to 3648×3360 — re-importing those here would cost minutes of CI for
    /// no extra coverage.</para>
    /// </summary>
    public class SpriteSheetSlicerIdempotenceTests
    {
        // Small, fast-importing manifest sheets spanning all three pivot styles the slicer emits:
        // centre (finds/seaweed), bottom-centre (the tray), and a custom boat-origin pivot (the punt hull,
        // the smallest of the iso boat sheets). The custom-pivot case is the one that matters most — those
        // are the sheets a rig bake churns.
        private static readonly string[] SmallSheets =
        {
            "Assets/_Project/Art/Sprites/Shore/SeaweedWisp.png",
            "Assets/_Project/Art/Sprites/Shore/Finds/SeaGlass.png",
            "Assets/_Project/Art/Sprites/Gear/FishTray.png",
            "Assets/_Project/Art/Boats/PuntIso.png",
        };

        private static IEnumerable<string> Sheets() => SmallSheets;

        private static string MetaPath(string assetPath) => assetPath + ".meta";

        [Test]
        [TestCaseSource(nameof(Sheets))]
        public void ReSlicing_AnAlreadySlicedSheet_LeavesTheMetaByteIdentical(string assetPath)
        {
            Assert.IsTrue(File.Exists(assetPath), $"{assetPath}: not on disk");
            Assert.IsTrue(File.Exists(MetaPath(assetPath)), $"{assetPath}: .meta not committed");

            // The committed .meta IS the baseline. If a plain re-slice moves it, the slicer is not
            // idempotent and every rig bake dirties this file.
            string before = File.ReadAllText(MetaPath(assetPath));

            Assert.IsTrue(SpriteSheetSlicer.SliceSheet(assetPath), $"{assetPath}: slice failed");
            AssetDatabase.Refresh();

            string after = File.ReadAllText(MetaPath(assetPath));
            Assert.AreEqual(before, after,
                $"{Path.GetFileName(assetPath)}: re-slicing an unchanged sheet rewrote its .meta. " +
                "The slicer must re-use the spriteID of any slice name that already exists — see " +
                "SpriteSheetSlicer.BuildRects(existingIds).");
        }

        [Test]
        [TestCaseSource(nameof(Sheets))]
        public void SlicingTwiceInARow_IsStable(string assetPath)
        {
            // Belt-and-braces on the test above: that one trusts the committed .meta as the baseline, so a
            // repo whose metas were committed mid-churn could mask a regression. This one takes its own
            // baseline from a first slice, so it holds regardless of what is on disk.
            Assert.IsTrue(SpriteSheetSlicer.SliceSheet(assetPath), $"{assetPath}: first slice failed");
            AssetDatabase.Refresh();
            string first = File.ReadAllText(MetaPath(assetPath));

            Assert.IsTrue(SpriteSheetSlicer.SliceSheet(assetPath), $"{assetPath}: second slice failed");
            AssetDatabase.Refresh();
            string second = File.ReadAllText(MetaPath(assetPath));

            Assert.AreEqual(first, second, $"{Path.GetFileName(assetPath)}: two consecutive slices disagreed");
        }

        [Test]
        [TestCaseSource(nameof(Sheets))]
        public void ReSlicing_PreservesEverySlicesInternalId_SoReferencesSurvive(string assetPath)
        {
            // The identifier that references actually resolve by. Asserted directly (rather than inferred
            // from the .meta text matching) so the intent survives any future reformatting of the meta:
            // BoatVisualDef.Facings is a list of {fileID: <this>, guid: <texture>}.
            var before = LocalIdsByName(assetPath);
            CollectionAssert.IsNotEmpty(before, $"{assetPath}: no slices found — is it Multiple-mode?");

            Assert.IsTrue(SpriteSheetSlicer.SliceSheet(assetPath), $"{assetPath}: slice failed");
            AssetDatabase.Refresh();

            var after = LocalIdsByName(assetPath);
            CollectionAssert.AreEquivalent(before.Keys, after.Keys,
                $"{Path.GetFileName(assetPath)}: the slice NAME set changed — that re-keys the " +
                "nameFileIdTable and orphans every reference into this sheet");
            foreach (var kv in before)
            {
                Assert.AreEqual(kv.Value, after[kv.Key],
                    $"{kv.Key}: internalID changed across a re-slice — any BoatVisualDef, prefab or " +
                    "SpriteLibrary pointing at this slice just lost it");
            }
        }

        /// <summary>
        /// Slice name → its local file id (the <c>fileID</c> half of a serialized sprite reference).
        /// Multiple-mode sheets return null from <c>LoadAssetAtPath&lt;Sprite&gt;</c>, so LoadAllAssets is
        /// the rule here (see the repo's imported-art notes).
        /// </summary>
        private static Dictionary<string, long> LocalIdsByName(string assetPath)
        {
            var map = new Dictionary<string, long>();
            foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>())
            {
                Assert.IsTrue(
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out _, out long localId),
                    $"{sprite.name}: no GUID/localId — the sprite is not a persisted sub-asset");
                map[sprite.name] = localId;
            }
            return map;
        }
    }
}
