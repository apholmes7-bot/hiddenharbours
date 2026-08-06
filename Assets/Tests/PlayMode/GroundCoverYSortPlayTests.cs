using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>Walking through the retuned meadow still sorts PER TUFT.</b>
    ///
    /// <para>The 2026-08-05 ground-cover retune took the grass grid from 2.2 m to 1.0 m, which is a
    /// five-figure population of SpriteRenderers. The standing escalation if that ever costs too much
    /// is merging static tufts into chunks — and the condition on it is NON-NEGOTIABLE: the player must
    /// still pass in front of the blades below them and behind the blades above them, ONE TUFT AT A
    /// TIME. A merged chunk sorts as a single object, so the player would pop in front of or behind a
    /// whole patch at once. This is the tripwire for that, and for anything else that coarsens the
    /// ground cover's sorting.</para>
    ///
    /// <para>PlayMode on purpose: <see cref="YSortSprite"/> only exhibits the static/mover split in
    /// play (in the editor everything re-sorts continuously so the Scene view stays WYSIWYG), so the
    /// interleaving asserted here does not exist to be measured in EditMode.</para>
    ///
    /// <para>⚠ The spacing below is the meadow's grain MIRRORED, not read: <c>StPetersGrass</c> lives
    /// in an editor-only assembly and this test asmdef ships to every platform. The number that must
    /// stay true — that one grid step resolves to a different sorting order — is asserted where the
    /// real constant is visible, in <c>StPetersGroundCoverBudgetTests</c>.</para>
    ///
    /// <para><b>✅ The row used to be placed inside a 9.5 m window ON PURPOSE — that constraint is
    /// gone.</b> <see cref="YSortSprite"/> clamped to orders 2…40 around a base of 10, so it resolved
    /// world Y only from −7.5 m to +2.0 m; past either end every sprite saturated on one order and
    /// tied, which on a 520 m region meant most of the island's decor — and the player standing in it
    /// — interleaved arbitrarily rather than by position. ADR 0032 re-based the band to span
    /// ±<c>SortingBands.DecorHalfExtentMetres</c> m with its floor held at 2, so nothing below it had
    /// to be re-derived. The row below still sits near the origin, which is now simply a convenient
    /// place rather than the only workable one — and <c>SortingBandsTests</c> plus
    /// <c>StPetersGroundCoverBudgetTests.TheSortingBandsReach_…</c> now ASSERT the reach instead of
    /// reporting it.</para>
    /// </summary>
    public class GroundCoverYSortPlayTests
    {
        /// <summary>Mirrors <c>StPetersGrass.GrassStep</c>. See the class remarks for why it is copied.</summary>
        const float GrassStepM = 0.85f;

        /// <summary>Where the row starts. It once had to sit inside a 9.5 m resolving window; since
        /// ADR 0032 re-based the band the whole island resolves, so this is just a convenient spot near
        /// the origin — see the class remarks.</summary>
        const float RowBaseY = -5f;

        readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.Destroy(go);
            _spawned.Clear();
        }

        GameObject Tuft(float y)
        {
            var go = new GameObject($"tuft@{y:F2}");
            go.SetActive(false);                      // built inactive so enable order is ours
            go.transform.position = new Vector3(0f, y, 0f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<YSortSprite>();           // static, exactly as the planter builds it
            go.SetActive(true);
            _spawned.Add(go);
            return go;
        }

        GameObject Walker(float y)
        {
            var go = new GameObject("player");
            go.SetActive(false);
            go.transform.position = new Vector3(0f, y, 0f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<YSortSprite>().Dynamic = true;
            go.SetActive(true);
            _spawned.Add(go);
            return go;
        }

        /// <summary>
        /// 🔴 The claim itself: walk a player up a row of tufts spaced at the meadow's own grain, and
        /// every tuft changes side at the moment the player passes IT — never a group at once.
        /// </summary>
        [UnityTest]
        public IEnumerator TheWalkerInterleavesWithEveryTuftSeparately()
        {
            const int rows = 8;
            var tufts = new List<GameObject>();
            for (int i = 0; i < rows; i++) tufts.Add(Tuft(RowBaseY + i * GrassStepM));

            var player = Walker(RowBaseY - GrassStepM);
            var playerSr = player.GetComponent<SpriteRenderer>();

            int splits = 0, lastInFront = -1;
            for (float y = RowBaseY - GrassStepM;
                 y <= RowBaseY + rows * GrassStepM;
                 y += GrassStepM * 0.1f)
            {
                player.transform.position = new Vector3(0f, y, 0f);
                yield return null;                    // LateUpdate re-sorts the mover

                int inFrontOfPlayer = 0;
                for (int i = 0; i < rows; i++)
                {
                    var sr = tufts[i].GetComponent<SpriteRenderer>();
                    bool tuftIsNearer = tufts[i].transform.position.y < y;
                    bool tuftDrawsInFront = sr.sortingOrder > playerSr.sortingOrder;

                    // A tuft on the near side of the player must draw in front of them and one on the
                    // far side behind, decided PER TUFT — the one thing a merged chunk cannot honour.
                    // Equal orders (the player level with a row) are legitimately either way, so they
                    // are skipped rather than asserted.
                    if (sr.sortingOrder != playerSr.sortingOrder)
                        Assert.AreEqual(tuftIsNearer, tuftDrawsInFront,
                            $"tuft {i} at y={tufts[i].transform.position.y:F2} draws " +
                            $"{(tuftDrawsInFront ? "in front of" : "behind")} a player at y={y:F2}. " +
                            "The ground cover must interleave with the walker one tuft at a time — if " +
                            "this fails after a chunk-merge, the merge is the bug, not this test.");

                    if (tuftDrawsInFront) inFrontOfPlayer++;
                }

                if (inFrontOfPlayer != lastInFront) { splits++; lastInFront = inFrontOfPlayer; }
            }

            // Crossing the row must move the split through every row rather than jumping it in blocks.
            Assert.GreaterOrEqual(splits, rows,
                $"the front/back split only changed {splits} times crossing {rows} rows of grass. The " +
                "player is passing whole groups of tufts at once, which is what a merged chunk looks " +
                "like from the player's side.");
        }
    }
}
