using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>NO FLAT OUTLINED ROCK IS LEFT ON EITHER COAST.</b> The old shore rocks were six flat sub-sprites of
    /// the shoreline sheet (<c>ShoreIsoSprites_reef/s/m/l/bs/bm/bl</c>): one red-brown, a dark keyline, no
    /// shadow side. Every rock that wore one now wears a shaded Rock Px cell named by its own
    /// <see cref="ShoreRockDef"/>, and four older unused rock PNGs were deleted.
    ///
    /// <para>Content: no scene, prefab or asset references a retired sprite; every rock under ShoreRocks in
    /// both played scenes is named by exactly one Def of its region and wears the cell (GUID, internal id,
    /// size) that Def names. Logic: <see cref="NineMileCreekShorePainter.SwapFor"/>, which keeps a rebuild of
    /// Nine Mile Creek on the new art.</para>
    /// </summary>
    public class ShadedShoreRocksTests
    {
        const string ShoreIsoSheetGuid = "339f953a88a78fb4fbe150e90ae059eb";

        /// <summary>The shoreline sheet's rock sub-sprites by internal id (its .meta), all retired as rocks.</summary>
        static readonly string[] RetiredShoreIsoRockIds =
        {
            "-1230941723",   // _reef
            "1819273866",    // _s
            "-1087745409",   // _m
            "1935516920",    // _l
            "-1898028754",   // _bs
            "28864283",      // _bm
            "521383701",     // _bl
        };

        /// <summary>The four older rock PNGs, deleted: Shore/RockCluster, RockMid, RockSmall, TidePoolRock.</summary>
        static readonly string[] DeletedRockPngGuids =
        {
            "8d33cacfdc40a9b48850f15097c6c3fb",
            "ba8dfd8ea74ca1840874dfd9be759df4",
            "e64c9ef497665b944832260a26e505f4",
            "85dc085b9458b3841acd46444fce661b",
        };

        static readonly (string Scene, string IdPrefix)[] Coasts =
        {
            ("Assets/_Project/Scenes/StPeters.unity", "rock.stp_"),
            ("Assets/_Project/Scenes/NineMileCreek.unity", NineMileCreekShorePainter.RockIdPrefix),
        };

        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // ============================================================================================
        //  content
        // ============================================================================================

        [Test]
        public void NoSceneOrPrefabOrAsset_ReferencesARetiredRockSprite()
        {
            string[] retired = RetiredShoreIsoRockIds.Select(id => $"fileID: {id}, guid: {ShoreIsoSheetGuid}").ToArray();
            var hits = new List<string>();
            foreach (string pattern in new[] { "*.unity", "*.prefab", "*.asset" })
                foreach (string path in Directory.GetFiles("Assets", pattern, SearchOption.AllDirectories))
                {
                    string text = File.ReadAllText(path);
                    foreach (string r in retired) if (text.Contains(r)) hits.Add(path + " wears " + r);
                    foreach (string g in DeletedRockPngGuids) if (text.Contains("guid: " + g)) hits.Add(path + " references deleted " + g);
                }
            CollectionAssert.IsEmpty(hits, "references to retired flat rock sprites");
        }

        [Test]
        public void TheDeletedRockPngs_AreGone()
        {
            foreach (string g in DeletedRockPngGuids)
                Assert.That(AssetDatabase.GUIDToAssetPath(g), Is.Null.Or.Empty, g + " is still an asset");
        }

        [Test]
        public void EveryShoreRock_InBothPlayedScenes_WearsTheCellItsDefNames()
        {
            List<ShoreRockDef> all = AssetDatabase.FindAssets("t:" + nameof(ShoreRockDef))
                .Select(g => AssetDatabase.LoadAssetAtPath<ShoreRockDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null).ToList();
            CollectionAssert.AllItemsAreUnique(all.Select(d => d.Id), "ShoreRockDef ids");
            var sprites = new StPetersLayerRefresh.EditorShoreRockSprites();

            foreach ((string scenePath, string prefix) in Coasts)
            {
                List<ShoreRockDef> defs = all.Where(d => d.Id.StartsWith(prefix, StringComparison.Ordinal)).ToList();
                var t = new LayerRefreshSceneText(File.ReadAllText(scenePath));
                List<long> roots = t.Order.Where(id => t.ClassOf(id) == 1 && !t.IsStripped(id) && t.Field(id, "m_Name") == "ShoreRocks").ToList();
                Assert.AreEqual(1, roots.Count, scenePath + ": one ShoreRocks");
                List<long> rocks = t.ChildGameObjects(roots[0]);
                Assert.AreEqual(defs.Count, rocks.Count, scenePath + ": one Def per rock");
                var used = new HashSet<string>(StringComparer.Ordinal);
                foreach (long go in rocks)
                {
                    string name = t.Field(go, "m_Name");
                    Vector2 at = t.Vec3(t.TransformOf(go), "m_LocalPosition");
                    string who = $"{scenePath}: {name} at ({at.x}, {at.y})";
                    ShoreRockDef def = NineMileCreekShorePainter.SwapFor(defs, name, at);
                    Assert.IsNotNull(def, who + ": no Def names it");
                    Assert.IsTrue(used.Add(def.Id), who + ": " + def.Id + " names two rocks");
                    Assert.IsTrue(sprites.TryRockPx(def, out StPetersLayerRefresh.SpriteRef want), who + ": the kit lacks " +
                                  StPetersLayerRefresh.RockPxSpriteName(def));
                    long sr = t.ComponentOfClass(go, 212);
                    Assert.AreEqual(want.Sprite.Guid, t.Guid(sr, "m_Sprite"), who + ": sprite guid (" + def.Id + ")");
                    Assert.AreEqual(want.Sprite.FileId, t.Ref(sr, "m_Sprite"), who + ": sprite id (" + def.Id + ")");
                    Vector2 size = t.Vec2(sr, "m_Size");
                    Assert.That(Vector2.Distance(want.Size, size), Is.LessThan(1e-4f), who + ": size (" + def.Id + ")");
                }
            }
        }

        // ============================================================================================
        //  the Nine Mile Creek painter's match
        // ============================================================================================

        ShoreRockDef Def(string id, string today, float x, float y)
        {
            var d = ScriptableObject.CreateInstance<ShoreRockDef>();
            _made.Add(d);
            d.Id = id;
            d.Today = today;
            d.At = new Vector2(x, y);
            return d;
        }

        [Test]
        public void SwapFor_ADefOfTheRocksNameWithinAHundredthOfAMetre_Applies()
        {
            ShoreRockDef d = Def("rock.nmc_bs_1", "Rock_bs", 43.98f, -56.28f);
            Assert.AreSame(d, NineMileCreekShorePainter.SwapFor(new[] { d }, "Rock_bs", new Vector2(43.97859f, -56.283783f)));
        }

        [Test]
        public void SwapFor_NoMatch_LeavesTheRockAsPainted()
        {
            ShoreRockDef d = Def("rock.nmc_bs_1", "Rock_bs", 43.98f, -56.28f);
            Assert.IsNull(NineMileCreekShorePainter.SwapFor(new[] { d }, "Rock_bm", new Vector2(43.98f, -56.28f)), "another name");
            Assert.IsNull(NineMileCreekShorePainter.SwapFor(new[] { d }, "Rock_bs", new Vector2(44.00f, -56.28f)), "two centimetres off");
            Assert.IsNull(NineMileCreekShorePainter.SwapFor(Array.Empty<ShoreRockDef>(), "Rock_bs", new Vector2(43.98f, -56.28f)), "no Defs");
            Assert.IsNull(NineMileCreekShorePainter.SwapFor(null, "Rock_bs", new Vector2(43.98f, -56.28f)), "null list");
        }

        [Test]
        public void SwapFor_TwoDefsNamingOneRock_Throws()
        {
            ShoreRockDef a = Def("rock.nmc_bs_1", "Rock_bs", 43.98f, -56.28f);
            ShoreRockDef b = Def("rock.nmc_bs_1b", "Rock_bs", 43.979f, -56.281f);
            Assert.Throws<InvalidOperationException>(() =>
                NineMileCreekShorePainter.SwapFor(new[] { a, b }, "Rock_bs", new Vector2(43.98f, -56.28f)));
        }
    }
}
