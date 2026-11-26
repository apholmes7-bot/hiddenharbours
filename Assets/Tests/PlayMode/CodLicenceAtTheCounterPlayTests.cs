#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE COD LICENCE IS BOUGHT OVER LEBLANC'S COUNTER</b>, in the committed St Peters scene.
    ///
    /// <para>The player walks up to Marguerite at the store counter, one press starts her conversation,
    /// her browse row opens her wares book, and the cod licence is a row on its Gear shelf. Buying it
    /// with enough coins owns it once and takes exactly its data price from the purse.</para>
    ///
    /// <para><b>Where the row lives.</b> Not in her talk bubble: the bubble holds her three rows and the
    /// way out (the four the bubble kit draws), and a licence is never a bubble row. It is a row in
    /// the BOOK the browse row opens. The book pages its shelf, so adding a licence never crowds the
    /// bubble or clips a row.</para>
    ///
    /// <para>The plates (the open book on the cod row, before and after the purchase) need a GPU; on a
    /// null device the case skips before loading anything.</para>
    /// </summary>
    public class CodLicenceAtTheCounterPlayTests
    {
        const string Seller = "seller.leblancs";
        const string CodId = "license.cod";
        const string CodLicencePath = "Assets/_Project/Data/Resources/Catalog/Licenses/CodLicense.asset";
        const float Standoff = 2.6f, Arrived = 1.5f, Overshoot = 0.6f;
        const float OpeningHour = 10f;   // inside her 07:00-13:00 shift behind the counter

        WharfNightStage stage;
        PlayerWalkController player;
        HeldWalkIntents held;
        readonly List<(Canvas canvas, RenderMode mode, Camera cam)> _restored = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var r in _restored)
                if (r.canvas != null) { r.canvas.renderMode = r.mode; r.canvas.worldCamera = r.cam; }
            _restored.Clear();
            held?.Release();
            if (player != null) player.ConfigureWalkInput(null);
            Time.timeScale = 1f;
            if (stage != null) yield return stage.TearDown();
            stage = null; player = null; held = null;
        }

        [UnityTest]
        public IEnumerator WalkUpToMarguerite_BrowseHerBook_BuyTheCodLicence_OwnedOnceAndDebitedItsPrice()
        {
            WharfNightStage.RequireAGraphicsDevice();
            Assert.That(System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable), Is.Not.Null.And.Not.Empty,
                "the run must save under its own path, never the shared save");

            var cod = AssetDatabase.LoadAssetAtPath<LicenseDef>(CodLicencePath);
            Assert.That(cod, Is.Not.Null, "the shipped cod licence");

            stage = new WharfNightStage("StPeters", "CodLicenceAtTheCounter");
            yield return stage.Load();
            if (ShellFlow.WorldInputBlocked) ShellFlow.Reset();
            InteractionGate.Reset();
            yield return stage.SetNight(OpeningHour);   // freezes the clock, so her routine holds her post
            for (int i = 0; i < 10; i++) yield return null;

            player = Object.FindAnyObjectByType<PlayerWalkController>();
            Assert.That(player, Is.Not.Null, "St Peters has a player on foot");
            Interactable her = Object.FindObjectsByType<Interactable>(FindObjectsInactive.Exclude)
                .SingleOrDefault(i => i.gameObject.name == "MargueriteLeBlanc");
            Assert.That(her, Is.Not.Null, "Marguerite carries a live Interactable");
            GameObject counter = GameObject.Find("GeneralStoreCounter");
            Assert.That(counter, Is.Not.Null, "the store counter is in the scene");
            float fromCounter = Vector2.Distance(her.transform.position, counter.transform.position);
            TestContext.WriteLine($"Marguerite at ({her.transform.position.x:0.##}, {her.transform.position.y:0.##}), " +
                                  $"{fromCounter:0.##} m from the counter at {GameServices.Clock.HourOfDay:0.00} h");

            var interactor = Object.FindAnyObjectByType<WorldInteractor>();
            var bubble = Object.FindAnyObjectByType<DialoguePresenter>();
            var book = Object.FindAnyObjectByType<CatalogBookPresenter>();
            Assert.That(interactor != null && bubble != null && book != null, Is.True,
                "St Peters carries the interactor, the bubble and the wares book");

            // ---- walk up: a real foot walk from a clear standoff ------------------------------------------
            var body = player.GetComponent<Rigidbody2D>();
            var foot = player.GetComponent<CircleCollider2D>();
            Vector2 at = her.transform.position;
            Vector2 toward = ClearApproach(at, new[] { her.transform, counter.transform }, foot.offset, foot.radius + 0.05f);
            body.position = at - toward * Standoff; body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms(); yield return null;
            Time.timeScale = 4f;
            held = new HeldWalkIntents(); player.ConfigureWalkInput(held);
            int steps = 0, still = 0;
            Vector2 last = body.position;
            while (Vector2.Distance(body.position, at) > Arrived && steps++ < 900 && still < 30)
            {
                held.Walk(toward); yield return new WaitForFixedUpdate();
                still = (body.position - last).sqrMagnitude < 1e-6f ? still + 1 : 0;
                last = body.position;
            }
            held.Release(); Time.timeScale = 1f;
            for (int i = 0; i < 3; i++) yield return null;
            float d = Vector2.Distance(player.transform.position, at);
            TestContext.WriteLine($"walked {steps} steps toward ({toward.x:0}, {toward.y:0}); {d:0.###} m from her");
            Assert.That(interactor.Nearest, Is.SameAs(her),
                $"the interactor offers Marguerite once the walk arrives ({d:0.###} m); it offers " +
                $"'{(interactor.Nearest as Component)?.gameObject.name}'");

            // ---- talk: her rows, and the browse row -------------------------------------------------------
            Assert.That(interactor.BeginInteract(), Is.True, "the press is spent on her");
            yield return null;
            for (int press = 0; press < 12 && !bubble.IsChoosing; press++) { interactor.BeginInteract(); yield return null; }
            Assert.That(bubble.IsChoosing, Is.True, "her lines reach the rows");
            string[] labels = bubble.Options.Select(o => o.Label).ToArray();
            TestContext.WriteLine($"her bubble shows {labels.Length} rows: {string.Join(" | ", labels)}");
            Assert.That(bubble.Options.Any(o => o.Label != null && o.Label.ToLowerInvariant().Contains("licen")), Is.False,
                "a licence is never a bubble row; it is in her book");
            int browse = bubble.Options.ToList().FindIndex(o => o.CatalogSellerId == Seller);
            Assert.That(browse, Is.GreaterThanOrEqualTo(0), "a row browses her book");
            for (int guard = 0; guard < 16 && bubble.SelectedOption != browse; guard++)
            {
                bubble.MoveSelection(-1f); bubble.MoveSelection(0f);
            }
            interactor.BeginInteract();
            yield return null; yield return null;
            Assert.That(CatalogBookPresenter.IsOpen, Is.True, "her wares book opens");
            Assert.That(book.SellerId, Is.EqualTo(Seller));
            Assert.That(bubble.IsAwaitingCatalog, Is.True, "the conversation holds under the book");

            // ---- the cod licence is a row on her Gear shelf -----------------------------------------------
            CatalogBook state = book.Book;
            TestContext.WriteLine($"book opens on {state.Section} with {state.Shelf.Count} rows over " +
                                  $"{state.PageCount} page(s) of {state.LinesPerLeaf}: " +
                                  string.Join(", ", state.Shelf.Select(r => r.Id)));
            Assert.That(state.Section, Is.EqualTo(cod.Catalog.Section), "the book opens on the licence's shelf");
            Assert.That(state.Shelf.Count(r => r.Id == CodId), Is.EqualTo(1), "the cod licence is on her shelf, once");
            for (int guard = 0; guard < 64 && state.Current.Id != CodId; guard++)
            {
                state.Move(-1f); state.Move(0f);
            }
            Assert.That(state.Current.Id, Is.EqualTo(CodId), "the cursor reaches the cod licence");
            Assert.That(state.Current.Quote.Price, Is.EqualTo(cod.Price), "the book quotes the licence's own data price");

            // ---- enough coins, and the purchase ------------------------------------------------------------
            IWallet wallet = GameServices.Wallet;
            Assert.That(wallet, Is.Not.Null, "the purse is published");
            Assert.That(GameServices.Licenses.IsLicensed(CodId), Is.False, "a fresh run does not hold the cod licence yet");
            if (wallet.Money < cod.Price + 7) wallet.Add(cod.Price + 7 - wallet.Money);
            Rebuild(book);
            int before = wallet.Money;

            yield return Plate(book, "cod-licence-book-before.png");

            Assert.That(book.Confirm(), Is.True, "the cod licence buys over her counter");
            Assert.That(wallet.Money, Is.EqualTo(before - cod.Price), $"debited exactly its data price ({cod.Price})");
            Assert.That(GameServices.Licenses.IsLicensed(CodId), Is.True, "owned");
            var owned = GameServices.Save?.Current?.OwnedLicenses;
            Assert.That(owned, Is.Not.Null);
            Assert.That(owned.Count(id => id == CodId), Is.EqualTo(1), "recorded once in the save");
            TestContext.WriteLine($"bought {CodId} for {cod.Price}: purse {before} -> {wallet.Money}");

            Assert.That(book.Confirm(), Is.False, "a second confirm on an owned licence does not charge again");
            Assert.That(wallet.Money, Is.EqualTo(before - cod.Price));

            yield return Plate(book, "cod-licence-book-after.png");
        }

        /// <summary>Re-read the shelf so the purse the book shows is the purse the test just set.</summary>
        static void Rebuild(CatalogBookPresenter book)
        {
            typeof(CatalogBookPresenter).GetMethod("Rebuild", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(book, new object[] { null });
        }

        /// <summary>
        /// A plate of the open book with Marguerite and her bubble above it. The overlay canvases do not draw
        /// into a camera's target, so for the picture only they are put on the plate camera, then put back.
        /// </summary>
        IEnumerator Plate(CatalogBookPresenter book, string name)
        {
            CatalogBook state = book.Book;
            string current = state.Current.Id;
            typeof(CatalogBookPresenter).GetMethod("Draw", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(book, null);
            if (stage.Camera == null)
            {
                Interactable her = Object.FindObjectsByType<Interactable>(FindObjectsInactive.Exclude)
                    .First(i => i.gameObject.name == "MargueriteLeBlanc");
                yield return stage.FrameOn(her.transform.position);
                // Only her bubble and her book go on the plate camera; other overlays (the title, the HUD)
                // stay where they draw in play. On top of every world sorting layer, in their own order.
                Transform bubbleRoot = Object.FindAnyObjectByType<DialoguePresenter>().transform;
                int topLayer = SortingLayer.layers[SortingLayer.layers.Length - 1].id;
                foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    if (!c.transform.IsChildOf(bubbleRoot) && !c.transform.IsChildOf(book.transform)) continue;
                    _restored.Add((c, c.renderMode, c.worldCamera));
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = stage.Camera;
                    c.planeDistance = 1f;
                    c.sortingLayerID = topLayer;
                    c.sortingOrder = 30000 + c.sortingOrder;
                }
                for (int i = 0; i < 3; i++) yield return null;
            }
            Assert.That(book.Book.Current.Id, Is.EqualTo(current), "the cursor is still on the row being photographed");
            stage.SavePlate(name, stage.Capture());
        }

        Vector2 ClearApproach(Vector2 at, Transform[] ignore, Vector2 footOffset, float radius)
        {
            foreach (Vector2 toward in new[] { Vector2.down, Vector2.up, Vector2.right, Vector2.left })
            {
                bool clear = true;
                for (float s = Arrived - Overshoot; s <= Standoff && clear; s += 0.1f)
                    clear = !Physics2D.OverlapCircleAll(at - toward * s + footOffset, radius)
                        .Any(c => !c.isTrigger && !c.transform.IsChildOf(player.transform)
                                  && !ignore.Any(t => c.transform.IsChildOf(t)));
                if (clear) return toward;
            }
            Assert.Fail($"no side of ({at.x:0.##}, {at.y:0.##}) has a clear {Standoff} m approach");
            return Vector2.zero;
        }
    }
}
#endif
