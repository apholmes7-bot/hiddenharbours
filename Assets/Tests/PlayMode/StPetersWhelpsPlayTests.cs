using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    // The Whelps in the loaded region: the tide covers and bares the rocks, and a player with no boat can walk
    // from the village to the light and to the cottage, and nowhere off the island.
    public partial class WharfBuildingPass2PlatePlayTests
    {
        const string WhelpsRoot = "StPetersWhelps";

        /// <summary>The boards' tides: the lookout's (the rocks are under), a falling tide that stands part way up
        /// them, and the low water the skerries board is drawn at.</summary>
        const float WhelpsCoveredTide = 0.63f, WhelpsHalfTide = -0.8f, WhelpsBaredTide = -1.4f;

        IEnumerator HoldTheTide(IEnvironmentService held, float level)
        {
            GameServices.Environment = new LandingBoardTide(held, level);
            float until = Time.realtimeSinceStartup + 20f;
            do { yield return null; }
            while (Time.realtimeSinceStartup < until &&
                   (Shader.GetGlobalVector("_HHSeaLevelWorld").w < .5f ||
                    Mathf.Abs(Shader.GetGlobalVector("_HHSeaLevelWorld").x - level) > .01f));
            Assert.AreEqual(level, Shader.GetGlobalVector("_HHSeaLevelWorld").x, .011f, "the sea never came to the level asked for");
        }

        /// <summary>The lowest and highest a picture's pixels stand, read the way its shader reads them.</summary>
        static (float low, float high) StandsBetween(SpriteRenderer picture)
        {
            var block = new MaterialPropertyBlock();
            picture.GetPropertyBlock(block);
            Vector4 decode = block.GetVector("_HHFaceHeightDecode");
            Assert.Greater(decode.w, .5f, picture.name + " carries no height to be cut by");
            var heights = block.GetTexture("_HHFaceHeight") as Texture2D;
            Assert.IsNotNull(heights, picture.name + " has no height sheet");
            Rect cell = picture.sprite.textureRect;
            Color[] px = heights.GetPixels((int)cell.x, (int)cell.y, (int)cell.width, (int)cell.height);
            Color[] ink = picture.sprite.texture.isReadable
                ? picture.sprite.texture.GetPixels((int)cell.x, (int)cell.y, (int)cell.width, (int)cell.height) : null;
            float low = float.MaxValue, high = float.MinValue;
            for (int i = 0; i < px.Length; i++)
            {
                if (ink != null ? ink[i].a <= 0f : px[i].r <= 0f) continue;
                float h = TidalFaceWaterline.DecodeHeight(px[i].r, new Vector3(decode.x, decode.y, decode.z));
                low = Mathf.Min(low, h); high = Mathf.Max(high, h);
            }
            return (low, high);
        }

        [UnityTest]
        public IEnumerator StPeters_TheWhelps_AreCoveredAndBaredByTheTide()
        {
            yield return LoadRegion(StPeters);
            GameObject root = GameObject.Find(WhelpsRoot);
            Assert.IsNotNull(root, "St Peters holds no " + WhelpsRoot + ".");
            var rocks = root.GetComponentsInChildren<TidalFaceWaterline>(true).Where(f => f.transform.parent == root.transform &&
                f.name.StartsWith("rock.", System.StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(7, rocks.Length, "the seven rocks of the Whelps");

            IEnvironmentService held = GameServices.Environment;
            try
            {
                int cutPartWay = 0;
                foreach (float tide in new[] { WhelpsCoveredTide, WhelpsHalfTide, WhelpsBaredTide })
                {
                    yield return HoldTheTide(held, tide);
                    Assert.IsTrue(TidalFaceWaterline.TryPublishedSeaLevel(out float sea), "no sea is published");
                    foreach (TidalFaceWaterline rock in rocks)
                    {
                        var under = rock.GetComponent<SpriteRenderer>();
                        var upper = rock.transform.GetChild(0).GetComponent<SpriteRenderer>();
                        Assert.IsTrue(rock.RidesTheTide && rock.IsConfigured, rock.name + " is not cut by the sea");
                        Assert.AreEqual(TidalFaceWaterline.ShaderName, under.sharedMaterial.shader.name, rock.name);
                        Assert.AreEqual(under.sortingOrder + 1, upper.sortingOrder, rock.name + ": the upper picture lies over the wet one");
                        (float low, float high) = StandsBetween(under);
                        (float upperLow, float upperHigh) = StandsBetween(upper);
                        Assert.Less(upperHigh, high, rock.name + ": the upper picture is cut above the water, by the wet band");
                        Assert.Less(upperLow, low, rock.name + ": and so is its foot");
                        if (Mathf.Approximately(tide, WhelpsCoveredTide))
                            Assert.Less(high, sea, $"{rock.name} shows at {tide} m, where the board has it under");
                        if (Mathf.Approximately(tide, WhelpsBaredTide))
                            Assert.Greater(low, sea, $"{rock.name} is still cut at {tide} m, where the board has it bare");
                        if (Mathf.Approximately(tide, WhelpsHalfTide) && low < sea && high > sea) cutPartWay++;
                    }
                }
                Assert.Greater(cutPartWay, 0, "on a half tide no rock stood part in and part out of the water");
            }
            finally
            {
                GameServices.Environment = held;
            }
        }

        /// <summary>
        /// The Harbour Stack stands out of the sea at every tide, the water cutting it along its own shape, and a
        /// hull cannot pass through its foot. The three ledges the Whelps sit on are under at the board's tide
        /// and stand part out at low water.
        /// </summary>
        [UnityTest]
        public IEnumerator StPeters_TheHarbourStack_StandsInEveryTide_AndTheLedgesAreCoveredAndBared()
        {
            yield return LoadRegion(StPeters);
            GameObject root = GameObject.Find(WhelpsRoot);
            Assert.IsNotNull(root, "St Peters holds no " + WhelpsRoot + ".");
            var forms = new List<Transform>();
            foreach (Transform child in root.transform)
                if (child.name.StartsWith("form.", System.StringComparison.Ordinal)) forms.Add(child);
            Assert.AreEqual(4, forms.Count, "the stack and the three ledges");
            Transform stack = forms.Single(f => f.name == "form.stp_harbour_stack");
            var foot = stack.GetComponent<PolygonCollider2D>();
            Assert.IsNotNull(foot, "the stack has no foot a hull would meet");
            Assert.IsFalse(foot.isTrigger, "the stack's foot is solid");
            Assert.IsTrue(foot.OverlapPoint(stack.position), "the foot lies about the stack's centre");
            Assert.AreEqual(3, forms.Count(f => f.GetComponent<Collider2D>() == null), "a ledge carries no collider: there a hull meets the ground's depth");
            Assert.AreEqual(2, stack.childCount, "the stack is its body and the plinth under the sea");

            // One stone, one shadow: of all the forms' pictures only the stack's body is left to the sun.
            var rules = Resources.Load<HiddenHarbours.Art.SunCasterRules>(nameof(HiddenHarbours.Art.SunCasterRules));
            Assert.IsNotNull(rules, "the sun caster's rules");
            var left = forms.SelectMany(f => f.GetComponentsInChildren<SpriteRenderer>(true)).Where(r => !rules.IsExcludedArt(r)).ToArray();
            Assert.AreEqual(1, left.Length, "pictures the sun may cast from: " + string.Join(", ", left.Select(r => r.transform.parent.name + "/" + r.name)));
            Assert.AreEqual("Body", left[0].name);
            Assert.AreEqual(stack, left[0].transform.parent);

            IEnvironmentService held = GameServices.Environment;
            try
            {
                foreach (float tide in new[] { WhelpsCoveredTide, WhelpsHalfTide, WhelpsBaredTide })
                {
                    yield return HoldTheTide(held, tide);
                    Assert.IsTrue(TidalFaceWaterline.TryPublishedSeaLevel(out float sea), "no sea is published");
                    foreach (Transform form in forms)
                    {
                        float formHigh = float.NegativeInfinity;
                        foreach (Transform piece in form)
                        {
                            string what = form.name + "/" + piece.name;
                            var upper = piece.GetComponent<SpriteRenderer>();
                            var under = piece.GetChild(0).GetComponent<SpriteRenderer>();
                            var face = under.GetComponent<TidalFaceWaterline>();
                            Assert.IsTrue(face.RidesTheTide && face.IsConfigured, what + " is not cut by the sea");
                            Assert.IsTrue(piece.GetComponent<TidalFaceWaterline>().RidesTheTide, what);
                            Assert.AreEqual(TidalFaceWaterline.ShaderName, under.sharedMaterial.shader.name, what);
                            Assert.AreEqual(under.sortingOrder + 1, upper.sortingOrder, what + ": the dry picture lies over the wet one");
                            (float low, float high) = StandsBetween(under);
                            (float upperLow, float _) = StandsBetween(upper);
                            Assert.Less(upperLow, low, what + ": the dry picture is cut above the water, by the wet band");
                            formHigh = Mathf.Max(formHigh, high);
                        }
                        if (form == stack)
                            Assert.Greater(formHigh, sea + 5f, "the stack does not stand high out of the water at " + tide);
                        else if (Mathf.Approximately(tide, WhelpsCoveredTide))
                            Assert.Less(formHigh, sea, $"{form.name} shows at {tide} m, where the board has the Whelps under");
                        else if (Mathf.Approximately(tide, WhelpsBaredTide))
                            Assert.Greater(formHigh, sea + 0.2f, $"{form.name} is not bared at {tide} m");
                    }
                }
            }
            finally
            {
                GameServices.Environment = held;
            }
        }

        /// <summary>The village end of the cliff walk (path.stp_cliff_walk's first point), where the walks start.</summary>
        static readonly Vector2 VillageEndOfTheCliffWalk = new Vector2(178f, -2.5f);

        /// <summary>The ground searched: wider than the island on every side, so reaching its edge is leaving.</summary>
        static readonly Rect WalkSearch = new Rect(-620f, -320f, 1060f, 640f);

        const float WalkCellMetres = 0.5f, WalkerRadiusMetres = 0.3f, AnchorReachMetres = 1f;

        /// <summary>
        /// Everywhere a walker of the player's size can get to from the village with no boat: over ground the
        /// tide leaves walkable, around every solid thing. Both keepers' doors are in it, at the board's tide and
        /// at low water, and at neither does it reach the edge of the sea.
        /// </summary>
        const string GapFootbridgeId = "bridge.stp_gap_footbridge";
        const string GapFootbridgeRoot = "StPetersFootbridges";
        const string RailNearName = "RailNear", RailFarName = "RailFar";
        const float BridgeApproachMetres = 1.2f;      // how far off each end the walk starts and ends, on the path
        const float BridgeArriveMetres = 0.15f;
        const int BridgeWalkFrames = 900;
        const int RailPushFrames = 120;

        // The footbridge over the gap stream, walked by the real player on the real ground: west to east, back
        // again, and a push at each rail from the middle of the deck.
        [UnityTest]
        public IEnumerator StPeters_TheGapFootbridge_IsWalkedAcrossBothWays_BetweenItsRails()
        {
            yield return LoadRegion(StPeters);
            StandablePlatform bridge = Object.FindObjectsByType<StandablePlatform>(FindObjectsInactive.Include)
                .FirstOrDefault(p => p.Id == GapFootbridgeId);
            Assert.That(bridge, Is.Not.Null, "the gap footbridge is in the loaded region as a deck to stand on");
            Rect stood = bridge.Footprint;
            Transform root = bridge.transform;
            while (root.parent != null && root.name != GapFootbridgeRoot) root = root.parent;
            SpriteRenderer near = root.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r => r.name == RailNearName);
            SpriteRenderer far = root.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r => r.name == RailFarName);
            Assert.That(near, Is.Not.Null, "the bridge has a rail on its near side");
            Assert.That(far, Is.Not.Null, "the bridge has a rail on its far side");
            Assert.That(far.sortingLayerID, Is.EqualTo(near.sortingLayerID));
            Assert.That(far.sortingOrder, Is.LessThan(near.sortingOrder), "the far rail is drawn behind the near one");
            // The planks lie between the two rails; the ground stood on reaches a little landward of them, because the
            // game reads a walker's ground at his body and draws his feet below it.
            Rect deck = Rect.MinMaxRect(stood.xMin, near.transform.position.y, stood.xMax, far.transform.position.y);
            Assert.That(deck.height, Is.GreaterThan(0.5f)); Assert.That(stood.yMax, Is.GreaterThanOrEqualTo(deck.yMax));

            var player = Object.FindAnyObjectByType<PlayerWalkController>(FindObjectsInactive.Include);
            Assert.That(player, Is.Not.Null, "the player is in the loaded game");
            player.gameObject.SetActive(true);
            player.enabled = true;
            var body = player.GetComponent<Rigidbody2D>();
            var foot = player.GetComponent<CircleCollider2D>();
            Assert.That(body, Is.Not.Null); Assert.That(foot, Is.Not.Null);
            var held = new HeldWalkIntents();
            player.ConfigureWalkInput(held);
            Vector2 Feet() => body.position + foot.offset;
            void Stand(Vector2 at)
            {
                held.Release();
                body.position = at - foot.offset;
                body.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
            }
            // The figure is drawn at the order of the picture it sorts by.
            YSortSprite sorter = player.GetComponentInChildren<YSortSprite>();
            Assert.That(sorter, Is.Not.Null, "the walker sorts by where he stands");
            Renderer sortedBy = sorter.GetComponent<Renderer>();
            int FigureOrder(out int layer)
            {
                layer = sortedBy.sortingLayerID;
                return sortedBy.sortingOrder;
            }
            string Sorting() => $"sorted by '{sorter.name}' at y {sorter.transform.position.y:0.00} pivot {sorter.SortPivotYOffset:0.00}, " +
                                $"body y {body.position.y:0.00}, order {sortedBy.sortingOrder}, rails {far.sortingOrder} to {near.sortingOrder}";

            float mid = deck.center.y;
            Vector2 west = new Vector2(deck.xMin - BridgeApproachMetres, mid);
            Vector2 east = new Vector2(deck.xMax + BridgeApproachMetres, mid);
            try
            {
                foreach ((Vector2 from, Vector2 to, string way) in new[] { (west, east, "west to east"), (east, west, "east to west") })
                {
                    Stand(from);
                    yield return new WaitForFixedUpdate();
                    int onDeck = 0, frames = 0;
                    float lowest = float.MaxValue, highest = float.MinValue;
                    while ((Feet() - to).magnitude > BridgeArriveMetres && frames++ < BridgeWalkFrames)
                    {
                        held.Walk((to - Feet()).normalized);
                        yield return new WaitForFixedUpdate();
                        Vector2 at = Feet();
                        if (at.x <= deck.xMin || at.x >= deck.xMax) continue;
                        onDeck++;
                        lowest = Mathf.Min(lowest, at.y); highest = Mathf.Max(highest, at.y);
                        Assert.That(bridge.TryGetDeckElevation(body.position, out float h), Is.True,
                            $"{way}: with his feet on the planks at {at} the game reads him as standing on the deck (body {body.position})");
                        Assert.That(h, Is.EqualTo(bridge.DeckElevation));
                        Assert.That(player.WaterState, Is.EqualTo(OnFootWaterState.Dry), $"{way}: he crosses dry at {at}");
                        int order = FigureOrder(out int layer);
                        Assert.That(layer, Is.EqualTo(near.sortingLayerID), $"{way}: he is drawn in the rails' layer");
                        Assert.That(order, Is.GreaterThan(far.sortingOrder), $"{way}: at {at} he is in front of the far rail; {Sorting()}");
                        Assert.That(order, Is.LessThan(near.sortingOrder), $"{way}: at {at} he is behind the near rail; {Sorting()}");
                    }
                    Assert.That((Feet() - to).magnitude, Is.LessThanOrEqualTo(BridgeArriveMetres),
                        $"{way}: he reaches the far bank; he stopped at {Feet()} after {frames} steps");
                    Assert.That(onDeck, Is.GreaterThan(0), $"{way}: the walk went over the deck");
                    Assert.That(lowest, Is.GreaterThan(deck.yMin)); Assert.That(highest, Is.LessThan(deck.yMax));
                }

                // From the middle of the deck neither rail lets him step off into the stream.
                foreach ((Vector2 push, string side) in new[] { (Vector2.down, "near"), (Vector2.up, "far") })
                {
                    Stand(deck.center);
                    yield return new WaitForFixedUpdate();
                    for (int i = 0; i < RailPushFrames; i++)
                    {
                        held.Walk(push);
                        yield return new WaitForFixedUpdate();
                    }
                    Vector2 at = Feet();
                    Assert.That(at.y, Is.InRange(deck.yMin, deck.yMax),
                        $"the {side} rail keeps his feet on the planks; he ended at {at}, planks {deck}");
                    Assert.That(bridge.TryGetDeckElevation(body.position, out _), Is.True,
                        $"pushed at the {side} rail he is still read as on the deck (body {body.position}, stood {stood})");
                }
            }
            finally
            {
                held.Release();
                player.ConfigureWalkInput(null);
            }
        }

        [UnityTest]
        public IEnumerator StPeters_WithNoBoat_BothKeepersCanBeWalkedTo_AndTheIslandCannotBeLeft()
        {
            yield return LoadRegion(StPeters);
            for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
            IEnvironmentService held = GameServices.Environment;
            var anchors = new[] { "anchor.stp_ne_light.tower_door", "anchor.stp_whelps.cottage_door" };
            try
            {
                foreach (float tide in new[] { WhelpsCoveredTide, WhelpsBaredTide })
                {
                    yield return HoldTheTide(held, tide);
                    yield return new WaitForFixedUpdate();
                    Assert.IsTrue(Reached(VillageEndOfTheCliffWalk, out HashSet<Vector2Int> walked, out bool leftTheSearch, out string how), how);
                    Debug.Log($"[KeySceneWalk] tide {tide}: {walked.Count} cells of {WalkCellMetres} m walked from the village. {how}");
                    Assert.IsFalse(leftTheSearch, $"at tide {tide} a walker with no boat gets off the island: {how}");
                    foreach (string id in anchors)
                    {
                        GameObject anchor = GameObject.Find(id);
                        Assert.IsNotNull(anchor, "no " + id);
                        Vector2 at = anchor.transform.position;
                        int reach = Mathf.CeilToInt(AnchorReachMetres / WalkCellMetres);
                        Vector2Int c = CellOf(at);
                        bool there = false;
                        for (int dy = -reach; dy <= reach && !there; dy++)
                        for (int dx = -reach; dx <= reach && !there; dx++)
                            there = walked.Contains(new Vector2Int(c.x + dx, c.y + dy));
                        Assert.IsTrue(there, $"at tide {tide} a walker with no boat cannot reach {id} at {at} from the village");
                    }
                }
            }
            finally
            {
                GameServices.Environment = held;
            }
        }

        static Vector2Int CellOf(Vector2 p) => new Vector2Int(
            Mathf.FloorToInt((p.x - WalkSearch.xMin) / WalkCellMetres), Mathf.FloorToInt((p.y - WalkSearch.yMin) / WalkCellMetres));

        static Vector2 CentreOf(Vector2Int c) => new Vector2(
            WalkSearch.xMin + (c.x + 0.5f) * WalkCellMetres, WalkSearch.yMin + (c.y + 0.5f) * WalkCellMetres);

        static bool Reached(Vector2 from, out HashSet<Vector2Int> walked, out bool leftTheSearch, out string how)
        {
            var walker = Object.FindAnyObjectByType<PlayerWalkController>(FindObjectsInactive.Include);
            int layer = walker != null ? walker.gameObject.layer : 0;
            float radius = WalkerRadiusMetres;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(layer));
            var hits = new Collider2D[8];
            bool Free(Vector2Int c)
            {
                Vector2 p = CentreOf(c);
                if (!TidalWalkability.IsWalkableNow(p)) return false;
                int n = Physics2D.OverlapCircle(p, radius, filter, hits);
                for (int i = 0; i < n; i++)
                    if (hits[i] != null && hits[i].attachedRigidbody == null) return false;   // solid and standing still
                return true;
            }

            int w = Mathf.CeilToInt(WalkSearch.width / WalkCellMetres), h = Mathf.CeilToInt(WalkSearch.height / WalkCellMetres);
            walked = new HashSet<Vector2Int>();
            leftTheSearch = false;
            Vector2Int start = CellOf(from);
            if (!Free(start))
            {
                how = $"the walk's start at {from} is not ground a walker can stand on";
                return false;
            }
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start); walked.Add(start);
            Vector2Int edge = default;
            var steps = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                Vector2Int c = queue.Dequeue();
                foreach (Vector2Int s in steps)
                {
                    Vector2Int n = c + s;
                    if (n.x < 0 || n.y < 0 || n.x >= w || n.y >= h) { leftTheSearch = true; edge = c; continue; }
                    if (walked.Contains(n) || !Free(n)) continue;
                    walked.Add(n); queue.Enqueue(n);
                }
            }
            int minX = walked.Min(c => c.x), maxX = walked.Max(c => c.x), minY = walked.Min(c => c.y), maxY = walked.Max(c => c.y);
            how = $"walked ground spans x {CentreOf(new Vector2Int(minX, 0)).x:0.0} to {CentreOf(new Vector2Int(maxX, 0)).x:0.0}, " +
                  $"y {CentreOf(new Vector2Int(0, minY)).y:0.0} to {CentreOf(new Vector2Int(0, maxY)).y:0.0}" +
                  (leftTheSearch ? $"; it runs off the searched ground at {CentreOf(edge)}" : "; the sea closes it on every side") +
                  $"; walker radius {radius} m on layer {LayerMask.LayerToName(layer)}";
            return true;
        }
    }
}
