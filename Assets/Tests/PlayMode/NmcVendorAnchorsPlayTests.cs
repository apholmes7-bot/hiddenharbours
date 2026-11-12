#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
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
    /// <summary>
    /// <b>NINE MILE CREEK'S VENDORS ARE TALKABLE.</b> In the committed scene, the player walks up to each of the
    /// three counters (the harbour office, the yard's catalogue, Hector at his barrel), the interactor offers that
    /// counter, one press starts the conversation, and its browse row opens that seller's wares book with the
    /// conversation holding under it. Nothing is bought: the wallet and the save are not touched.
    /// </summary>
    public class NmcVendorAnchorsPlayTests
    {
        /// <summary>Distances are the player's transform to the anchor, which is what the interactor measures;
        /// the walk may run up to <see cref="Overshoot"/> past <see cref="Arrived"/> before it settles.</summary>
        const float Standoff = 2.6f, Arrived = 1.5f, Overshoot = 0.6f;

        WharfNightStage stage;
        PlayerWalkController player;
        HeldWalkIntents held;

        IEnumerator Load()
        {
            Assert.That(System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable), Is.Not.Null.And.Not.Empty,
                "the run must save under its own path, never the shared save");
            stage = new WharfNightStage("NineMileCreek", "NmcVendorAnchors");
            yield return stage.Load();
            if (ShellFlow.WorldInputBlocked) ShellFlow.Reset();
            InteractionGate.Reset();
            player = Object.FindAnyObjectByType<PlayerWalkController>();
            Assert.That(player, Is.Not.Null, "the creek has a player on foot");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            held?.Release();
            if (player != null) player.ConfigureWalkInput(null);
            Time.timeScale = 1f;
            if (stage != null) yield return stage.TearDown();
            stage = null; player = null; held = null;
        }

        [UnityTest]
        public IEnumerator TheHarbourOffice_WalkUpAndTalk_OpensTheLicenceBook() =>
            WalkUpTalkAndBrowse(NineMileCreekWharfLandSide.OfficeVendorName, "seller.nmc_harbourmaster", "Harbourmaster", CatalogSection.Gear);

        [UnityTest]
        public IEnumerator TheYard_WalkUpAndTalk_OpensTheBoatBook() =>
            WalkUpTalkAndBrowse(NineMileCreekWharfYard.CatalogueName, "seller.nmc_yard", "Yard hand", CatalogSection.Boats);

        [UnityTest]
        public IEnumerator Hector_WalkUpAndTalk_OpensTheOutboardBook() =>
            WalkUpTalkAndBrowse("HectorBernard", "seller.hectors_barrel", "Hector Bernard", CatalogSection.Boats);

        IEnumerator WalkUpTalkAndBrowse(string anchorName, string sellerId, string speaker, CatalogSection section)
        {
            yield return Load();
            Interactable anchor = Object.FindObjectsByType<Interactable>(FindObjectsInactive.Exclude)
                .SingleOrDefault(i => i.gameObject.name == anchorName);
            Assert.That(anchor, Is.Not.Null, $"{anchorName} carries a live Interactable");
            var interactor = Object.FindAnyObjectByType<WorldInteractor>();
            Assert.That(interactor, Is.Not.Null, "the creek has a dialogue driver");
            var bubble = Object.FindAnyObjectByType<DialoguePresenter>();
            var book = Object.FindAnyObjectByType<CatalogBookPresenter>();
            Assert.That(bubble != null && book != null, Is.True, "the creek's DialogueUI carries the bubble and the wares book");

            // ---- walk up: a real foot walk from a clear standoff, facing the counter ---------------------------
            var body = player.GetComponent<Rigidbody2D>();
            var foot = player.GetComponent<CircleCollider2D>();
            Vector2 at = anchor.transform.position;
            Vector2 toward = ClearApproach(at, anchor.transform, foot.offset, foot.radius + 0.05f);
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
            TestContext.WriteLine($"{anchorName}: walked {steps} steps toward ({toward.x:0}, {toward.y:0}); " +
                                  $"{d:0.###} m from the counter, facing {player.CurrentFacing}");
            Assert.That(interactor.Nearest, Is.SameAs(anchor), $"the interactor offers {anchorName} once the walk arrives ({d:0.###} m)");

            // ---- interact: one press, the role speaks -------------------------------------------------------
            Assert.That(interactor.BeginInteract(), Is.True, "the press is spent on the counter");
            yield return null;
            Assert.That(bubble.IsShowing, Is.True, "the conversation is up");
            Assert.That(anchor.Speaker, Is.EqualTo(speaker), "the nameplate is the role");
            for (int press = 0; press < 12 && !bubble.IsChoosing; press++) { interactor.BeginInteract(); yield return null; }
            Assert.That(bubble.IsChoosing, Is.True, "the lines reach the rows");
            int browse = bubble.Options.ToList().FindIndex(o => o.CatalogSellerId == sellerId);
            Assert.That(browse, Is.GreaterThanOrEqualTo(0), $"a row browses {sellerId}");
            for (int guard = 0; guard < 16 && bubble.SelectedOption != browse; guard++)
            {
                bubble.MoveSelection(-1f); bubble.MoveSelection(0f);
            }
            Assert.That(bubble.SelectedOption, Is.EqualTo(browse), "the cursor reaches the browse row");

            // ---- the trade menu opens ------------------------------------------------------------------------
            interactor.BeginInteract();
            yield return null; yield return null;
            Assert.That(CatalogBookPresenter.IsOpen, Is.True, "the wares book opens");
            Assert.That(book.SellerId, Is.EqualTo(sellerId), "it is this counter's book");
            Assert.That(book.Book.Shelf.Count, Is.GreaterThan(0), "the book has wares on it");
            Assert.That(book.Book.Section, Is.EqualTo(section), "it opens on the counter's shelf");
            Assert.That(bubble.IsAwaitingCatalog, Is.True, "the conversation holds under the book");
            TestContext.WriteLine($"{anchorName}: {sellerId} book open on {book.Book.Section}, {book.Book.Shelf.Count} rows");
        }

        /// <summary>The first side (south, north, west, east) from which a body-wide walk to the counter is clear of
        /// walls for the whole standoff, overshoot included, so the walk tests reach, not a path finder. The check
        /// follows the feet: the foot circle sits at <paramref name="footOffset"/> from the transform, so walking
        /// south the feet lead and walking north they trail. Bumping the counter's own body ends the walk, and
        /// the reach assertion then judges where it ended.</summary>
        Vector2 ClearApproach(Vector2 at, Transform counter, Vector2 footOffset, float radius)
        {
            foreach (Vector2 toward in new[] { Vector2.up, Vector2.down, Vector2.right, Vector2.left })
            {
                bool clear = true;
                for (float s = Arrived - Overshoot; s <= Standoff && clear; s += 0.1f)
                    clear = !Physics2D.OverlapCircleAll(at - toward * s + footOffset, radius)
                        .Any(c => !c.isTrigger && !c.transform.IsChildOf(player.transform) && !c.transform.IsChildOf(counter));
                if (clear) return toward;
            }
            Assert.Fail($"no side of ({at.x:0.##}, {at.y:0.##}) has a clear {Standoff} m approach");
            return Vector2.zero;
        }
    }
}
#endif
