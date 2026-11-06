#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public class NmcShipyardPlayTests
    {
        WharfNightStage stage;
        PlayerWalkController player;
        HeldWalkIntents held;
        ISaveService previousSave;
        bool saveChanged;
        IEnumerator Load()
        {
            Assert.That(System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable), Is.Not.Null.And.Not.Empty);
            stage = new WharfNightStage("NineMileCreek", "NmcShipyard");
            yield return stage.Load(); yield return stage.SetNight(14f);
            if (ShellFlow.WorldInputBlocked) ShellFlow.Reset();
            InteractionGate.Reset();
            player = Object.FindFirstObjectByType<PlayerWalkController>();
            Assert.That(player, Is.Not.Null);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            held?.Release();
            if (player != null) player.ConfigureWalkInput(null);
            if (saveChanged) GameServices.Save = previousSave;
            saveChanged = false;
            if (stage != null) yield return stage.TearDown();
            stage = null; player = null; held = null;
        }
        static GameObject Yard() => GameObject.Find("NmcWharfBoatYard");
        void Plate(string name, string evidenceFolder = "Evidence~/nmc-h6/pr3n/plates")
        {
            stage.SavePlate(name, stage.Capture());
            string source = Path.Combine(Application.temporaryCachePath, "NmcShipyard", name);
            foreach (string relative in new[] { evidenceFolder, "artifacts/nmc-h6/pr3/plates" })
            {
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../" + relative));
                Directory.CreateDirectory(folder); File.Copy(source, Path.Combine(folder, name), true);
            }
        }
        IEnumerator PinHour(float hour)
        {
            // Load's earlier SetNight(14) already ran the shared helper's clock-republish guard.
            // Ruling 31: dusk is the game's settled tint at this hour, with no fixture lamp window.
            Time.timeScale = 1f;
            GameServices.Clock.TimeScale = 1f;
            GameServices.Clock.SeekTo((1.0 + hour / 24.0) * GameServices.Config.SecondsPerDay);
            Color tint = Shader.GetGlobalColor("_DayNightTint");
            int still = 0, frames = 0;
            while (frames < 900)
            {
                yield return null;
                frames++;
                Color now = Shader.GetGlobalColor("_DayNightTint");
                float move = Mathf.Abs(now.r - tint.r) + Mathf.Abs(now.g - tint.g) + Mathf.Abs(now.b - tint.b);
                still = move < 1e-4f ? still + 1 : 0;
                tint = now;
                if (still >= 4) break;
            }
            Assert.Less(frames, 900, $"the tint never settled at {hour:0.00}");
            Assert.That(GameServices.Clock.HourOfDay, Is.EqualTo(hour).Within(0.05));
            GameServices.Clock.TimeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;
            TestContext.WriteLine($"plate hour {hour:0.00}: clock {GameServices.Clock.HourOfDay:0.00}, " +
                $"tint ({tint.r:0.000}, {tint.g:0.000}, {tint.b:0.000}), settled in {frames} frames");
        }
        [UnityTest]
        public IEnumerator TheYardAndBothCounters_AtDayAndDusk()
        {
            WharfNightStage.RequireAGraphicsDevice(); yield return Load();
            var body = player.GetComponent<Rigidbody2D>();
            foreach (float hour in new[] { 14f, 19f })
            foreach (var view in new[] { ("shed", new Vector2(197.8f,123.9f)), ("quay", new Vector2(198f,89.5f)), ("yard", new Vector2(198f,111f)) })
            {
                yield return PinHour(hour);
                body.position = view.Item2; body.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms(); yield return null;
                yield return stage.FrameOn(view.Item2);
                Plate(view.Item1 + (hour == 14 ? "-day.png" : "-dusk.png"));
            }
        }
        [UnityTest]
        public IEnumerator BothPiersCarryARealWalk_FromTheQuayToTheirEnds()
        {
            yield return Load(); Time.timeScale = 4f;
            var body = player.GetComponent<Rigidbody2D>(); var foot = player.GetComponent<CircleCollider2D>();
            held = new HeldWalkIntents(); player.ConfigureWalkInput(held);
            var piers = Yard().GetComponentsInChildren<StandablePlatform>().Where(s => s.Id.Contains("pier_")).ToArray();
            Assert.That(piers.Length, Is.EqualTo(2));
            foreach (var pier in piers)
            {
                Rect rect = pier.Footprint;
                float radius = foot.radius + 0.05f, fromY = rect.yMax + 1.5f, toY = rect.yMin + radius;
                float? clearX = null;
                for (float x = rect.xMin + radius; x <= rect.xMax - radius && clearX == null; x += 0.1f)
                {
                    bool clear = true;
                    for (float y = toY; y <= fromY && clear; y += 0.1f)
                        clear = !Physics2D.OverlapCircleAll(new Vector2(x,y), radius).Any(c =>
                            !c.isTrigger && !c.transform.IsChildOf(player.transform));
                    if (clear) clearX = x;
                }
                if (!clearX.HasValue) yield return ReportPierBlockers(pier, radius, toY, fromY);
                Assert.That(clearX.HasValue, Is.True, pier.Id + " has a body-wide clear tread");
                body.position = new Vector2(clearX.Value,fromY) - foot.offset;
                body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms(); yield return null;
                int steps = 0;
                while ((body.position + foot.offset).y > toY + 0.15f && steps++ < 1800)
                {
                    held.Walk(Vector2.down); yield return new WaitForFixedUpdate();
                    Vector2 at = body.position + foot.offset;
                    Assert.That(player.WaterState, Is.EqualTo(OnFootWaterState.Dry), pier.Id + " stays dry");
                    if (at.y <= rect.yMax)
                        Assert.That(StandableSurfaces.StandingElevationNow(-99f,at), Is.EqualTo(pier.DeckElevation).Within(0.001));
                }
                held.Release(); yield return null;
                Assert.That((body.position + foot.offset).y, Is.LessThanOrEqualTo(toY + 0.15f), pier.Id + " was walked to its end");
                Assert.That(pier.DeckElevation, Is.GreaterThan(2.2f), "dry even at spring high");
                TestContext.WriteLine(pier.Id + ": real foot walk from " + fromY + " to " + toY + " in " + steps + " steps");
            }
        }
        IEnumerator ReportPierBlockers(StandablePlatform pier, float radius, float toY, float fromY)
        {
            Rect rect = pier.Footprint;
            var blockers = new Dictionary<Collider2D, HashSet<string>>();
            // Diagnostic pass over the same sample grid; the original clear-tread scan is unchanged.
            for (float x = rect.xMin + radius; x <= rect.xMax - radius; x += 0.1f)
            for (float y = toY; y <= fromY; y += 0.1f)
            foreach (var collider in Physics2D.OverlapCircleAll(new Vector2(x, y), radius))
            {
                if (collider.isTrigger || collider.transform.IsChildOf(player.transform)) continue;
                if (!blockers.TryGetValue(collider, out var bands))
                    blockers.Add(collider, bands = new HashSet<string>());
                bands.Add(y > rect.yMax ? "QUAY STRIP" : "PIER");
            }
            string FullPath(Transform t) => t.parent == null ? t.name : FullPath(t.parent) + "/" + t.name;
            TestContext.WriteLine($"pier scan {pier.Id}: rect ({rect.xMin:R}, {rect.yMin:R}).." +
                $"({rect.xMax:R}, {rect.yMax:R}), radius {radius:R}, y {toY:R}..{fromY:R}, step 0.1");
            foreach (var entry in blockers.OrderBy(p => FullPath(p.Key.transform)))
            {
                Collider2D c = entry.Key; Bounds b = c.bounds;
                TestContext.WriteLine($"PIER BLOCKER {FullPath(c.transform)}; component={c.GetType().FullName}; " +
                    $"isTrigger={c.isTrigger}; layer={c.gameObject.layer}({LayerMask.LayerToName(c.gameObject.layer)}); " +
                    $"world bounds min=({b.min.x:R}, {b.min.y:R}, {b.min.z:R}), " +
                    $"max=({b.max.x:R}, {b.max.y:R}, {b.max.z:R}); BAND={string.Join(", ", entry.Value.OrderBy(s => s))}");
            }
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    var material = stage.Track(new Material(shader));
                    foreach (var c in blockers.Keys)
                    {
                        var paths = new List<Vector2[]>();
                        if (c is PolygonCollider2D polygon)
                            for (int i = 0; i < polygon.pathCount; i++) paths.Add(polygon.GetPath(i));
                        else if (c is BoxCollider2D box)
                        {
                            Vector2 h = box.size * 0.5f;
                            paths.Add(new[] { new Vector2(-h.x,-h.y), new Vector2(h.x,-h.y),
                                new Vector2(h.x,h.y), new Vector2(-h.x,h.y) });
                        }
                        else if (c is EdgeCollider2D edge) paths.Add(edge.points);
                        foreach (var path in paths)
                        {
                            var go = stage.Track(new GameObject("Pier blocker outline"));
                            var line = go.AddComponent<LineRenderer>();
                            line.sharedMaterial = material; line.useWorldSpace = true;
                            line.loop = !(c is EdgeCollider2D); line.widthMultiplier = 0.06f;
                            line.startColor = line.endColor = Color.red; line.sortingOrder = 32760;
                            line.positionCount = path.Length;
                            for (int i = 0; i < path.Length; i++)
                                line.SetPosition(i, c.transform.TransformPoint(path[i] + c.offset));
                        }
                    }
                }
                yield return stage.FrameOn(rect.center);
                Plate(pier.Id.Contains("pier_w") ? "west-pier-blockers.png" : "east-pier-blockers.png",
                    "Evidence~/nmc-h6/pr3o/plates");
            }
        }
        [UnityTest]
        public IEnumerator TheYardSolidsBlockTheirInteriors_AndTheShedDoorIsOpen()
        {
            yield return Load(); Physics2D.SyncTransforms();
            var colliders = Yard().GetComponentsInChildren<PolygonCollider2D>();
            Assert.That(colliders.Length, Is.GreaterThanOrEqualTo(9));
            foreach (var collider in colliders)
            {
                Assert.That(collider.enabled && !collider.isTrigger, Is.True, collider.name);
                Vector2[] points = collider.GetPath(0);
                Vector2 centre = points.Aggregate(Vector2.zero,(a,b) => a+b) / points.Length;
                Assert.That(collider.OverlapPoint(collider.transform.TransformPoint(centre)), Is.True, collider.name);
            }
            Vector2 door = new Vector2(197.8f,123.9f);
            Assert.That(colliders.Any(c => c.OverlapPoint(door)), Is.False, "the published shed door remains an opening");
        }
        sealed class Wallet : IWallet
        {
            public int Money { get; private set; } = 1000000;
            public void Add(int n) => Money += n;
            public bool TrySpend(int n) { if(n<0 || n>Money)return false; Money-=n; return true; }
        }
        sealed class MemorySave : ISaveService
        {
            public SaveData Current { get; } = new SaveData();
            public bool GetFlag(string k) => false;
            public void SetFlag(string k,bool v) { }
            public void Save() { }
        }
        [UnityTest]
        public IEnumerator AllFiveCommittedOffersSell_AndTheRepairChargesItsPrice()
        {
            yield return Load();
            previousSave = GameServices.Save; var save = new MemorySave(); GameServices.Save = save; saveChanged = true;
            var wallet = new Wallet(); var boats = new List<string>(); int pots = 0, repairs = 0;
            Action<BoatPurchased> onBoat = e => boats.Add(e.BoatId);
            Action<PotPurchased> onPot = e => pots++;
            Action<BoatRepaired> onRepair = e => repairs++;
            EventBus.Subscribe(onBoat); EventBus.Subscribe(onPot); EventBus.Subscribe(onRepair);
            try
            {
                foreach (string root in new[] { "NmcWharfYardDoryTrade", "NmcWharfYardCatalogue", "NmcWharfYardOutboardTrade" })
                {
                    var seller = GameObject.Find(root).GetComponent<Shipwright>();
                    Assert.That(seller.Offer, Is.Not.Null, root);
                    int before = wallet.Money;
                    Assert.That(seller.TryBuy(seller.Offer,wallet), Is.True, root);
                    Assert.That(before-wallet.Money, Is.EqualTo(seller.Offer.Price));
                    if (seller.Offer.StartsDamaged)
                    {
                        before = wallet.Money;
                        Assert.That(seller.TryRepair(seller.Offer,wallet,save.Current), Is.True);
                        Assert.That(before-wallet.Money, Is.EqualTo(seller.Offer.RepairCost));
                        Assert.That(RepairLedger.IsRepaired(save.Current,seller.Offer.BoatId), Is.True);
                    }
                }
                var stock = GameObject.Find("NmcWharfYardCatalogue").GetComponents<PotShop>();
                CollectionAssert.AreEquivalent(new[] { "offer.lobster_pot", "offer.crab_pot" },stock.Select(s => s.Offer.Id));
                foreach (var shop in stock)
                {
                    int before = wallet.Money;
                    Assert.That(shop.TryBuy(shop.Offer,wallet,save.Current), Is.True);
                    Assert.That(before-wallet.Money, Is.EqualTo(shop.Offer.Price));
                }
                CollectionAssert.AreEquivalent(new[] { "boat.dory", "boat.punt", "boat.dory_outboard" },boats);
                Assert.That(pots, Is.EqualTo(2)); Assert.That(repairs, Is.EqualTo(1));
            }
            finally
            {
                EventBus.Unsubscribe(onBoat); EventBus.Unsubscribe(onPot); EventBus.Unsubscribe(onRepair);
            }
        }
    }
}
#endif
