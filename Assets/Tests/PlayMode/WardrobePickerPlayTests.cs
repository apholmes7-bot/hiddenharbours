using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.UI;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The wardrobe, opened and worn for real.</b> A real fixture publishing the real signal, the real
    /// picker answering it, the real <see cref="PlayerOutfitVisual"/> on a real renderer, and the real
    /// <see cref="OutfitLocker"/> writing a (faked) save — end to end, with nothing stubbed between the
    /// press and the pixels.
    ///
    /// <para><b>What this proves that EditMode cannot.</b> Four things. (1) The three modules are actually
    /// wired: World publishes, UI answers, Art draws and Core persists, with no module referencing
    /// another — a seam that compiles is not a seam that connects. (2) <c>Awake</c>/<c>OnEnable</c> run,
    /// so the picker's subscription and the fixture's <see cref="Interactables"/> registration are the
    /// real ones. (3) The preview reaches a live <c>SpriteRenderer</c>'s property block, which is the only
    /// place a colour-space or resolution mistake shows up. (4) The claims are raised and — the part that
    /// matters — RELEASED, on every path that closes the panel.</para>
    ///
    /// <para><b>⚠ Driven through the component API, never through keys.</b> A virtual key press is
    /// undeliverable to the new Input System's device state in a headless run, so a test that "pressed E"
    /// would be asserting on a press that never happened. <see cref="WardrobePicker.Open"/>,
    /// <see cref="WardrobePicker.MoveSelection"/>, <see cref="WardrobePicker.Confirm"/> and
    /// <see cref="WardrobePicker.CancelAndClose"/> are the same methods this component's own
    /// <c>Update</c> calls, so what is driven here is what a press does.</para>
    ///
    /// <para><b>⚠ The save service is FAKED.</b> The real <c>SaveService</c> bootstraps itself into play
    /// mode and writes the developer's actual save file; a confirm that reached it would overwrite the
    /// owner's game. <see cref="GameServices.Save"/> is pointed at a fake and put back in teardown.</para>
    ///
    /// <para><b>⚠ The picker under test is the INSTALLED one.</b> It self-installs at
    /// <c>AfterSceneLoad</c> onto a <c>DontDestroyOnLoad</c> host and destroys any rival, so standing up a
    /// second would be testing an object that is torn down underneath the assertions.
    /// <see cref="WardrobePicker.Instance"/> is that one, re-pointed at this test's camera for the
    /// duration.</para>
    ///
    /// <para>The scene is created here and unloaded in teardown — a scene left resident reds other files
    /// with failures that read like gameplay bugs.</para>
    /// </summary>
    public class WardrobePickerPlayTests
    {
        const string SceneName = "WardrobePicker_Test";
        const string FixtureId = "fixture.playtest.wardrobe";
        const float Reach = 1.2f;

        /// <summary>A save service that behaves as the real one does in the respect this feature cares
        /// about: a write lands and is counted, and nothing reaches the disk.</summary>
        sealed class FakeSaveService : ISaveService
        {
            public int Saves;
            public SaveData Current { get; } = new SaveData();
            public bool LoadedExistingSave { get; private set; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { Saves++; LoadedExistingSave = true; }
        }

        Scene _scene;
        FakeSaveService _save;
        WardrobePicker _picker;
        CharacterWardrobeDef _wardrobe;
        PlayerOutfitVisual _visual;
        InteriorWardrobe _fixture;
        GameObject _player;
        Camera _camera;
        readonly List<DevNotice> _notices = new List<DevNotice>();
        System.Action<DevNotice> _noticeSink;

        /// <summary>The shipped wardrobe's outfit ids, in the order it holds them. Read from the asset
        /// rather than retyped: the list is CONTENT (rule 2), and a test that hard-coded it would go red
        /// the day the owner added a ninth coat.</summary>
        List<string> Ids
        {
            get
            {
                var ids = new List<string>();
                foreach (var o in _wardrobe.Outfits)
                    if (o != null && !string.IsNullOrEmpty(o.Id)) ids.Add(o.Id);
                return ids;
            }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _scene = SceneManager.CreateScene(SceneName);
            SceneManager.SetActiveScene(_scene);

            _wardrobe = Resources.Load<CharacterWardrobeDef>(CharacterWardrobeDef.ResourcesPath);
            if (_wardrobe == null || _wardrobe.Count == 0)
                Assert.Ignore("No shipped CharacterWardrobe in Resources — nothing to pick from.");
            if (_wardrobe.Count < 6)
                Assert.Ignore($"The shipped wardrobe holds {_wardrobe.Count} outfits; these tests step to " +
                              "row 5 to prove the walk is real rather than a single toggle.");

            _save = new FakeSaveService();
            GameServices.Save = _save;

            // One listener, so a play-mode scene does not log "there are no audio listeners" on every
            // frame of every test in the suite.
            var rig = new GameObject("Rig", typeof(AudioListener));
            var camGo = new GameObject("Camera", typeof(Camera));
            _camera = camGo.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 5f;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            // The fisher: a real renderer with the real outfit visual on it, so the preview is observed
            // where the game observes it. NOT named "Player" — PlayerOutfitInstaller hunts that name and
            // would install a second visual behind the test's back.
            _player = new GameObject("TestFisher", typeof(SpriteRenderer));
            _player.transform.position = Vector3.zero;
            _visual = _player.AddComponent<PlayerOutfitVisual>();
            GameServices.PlayerTransform = _player.transform;

            // The wardrobe, within reach and registered — the picker finds it by id to place its panel.
            var fixtureGo = new GameObject("wardrobe");
            fixtureGo.transform.position = new Vector3(0.4f, 0f, 0f);
            _fixture = fixtureGo.AddComponent<InteriorWardrobe>();
            _fixture.Configure(FixtureId, Reach);

            _picker = WardrobePicker.Instance;
            Assert.NotNull(_picker, "the picker self-installs at AfterSceneLoad; if it did not, nothing " +
                                    "in the game answers a wardrobe");
            _picker.Configure(_wardrobe, _camera);

            // ⚠️ RE-ARM THE SUBSCRIPTION. The picker subscribes once, in OnEnable, at install — and
            // CabinUpstairsPlayTests calls EventBus.Clear<WardrobeRequested>() to isolate its own
            // assertion, which drops EVERY handler on that channel including this one. Whether that has
            // happened depends on test order, so this cycles OnDisable/OnEnable to put it back rather
            // than betting on running first. (CancelAndClose on the way down is a no-op while closed.)
            _picker.enabled = false;
            _picker.enabled = true;

            _notices.Clear();
            _noticeSink = _notices.Add;
            EventBus.Subscribe(_noticeSink);

            yield return null;   // let Awake/OnEnable/Start land before anything is driven
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_picker != null)
            {
                _picker.CancelAndClose();
                // Put the installed picker back on the shipped content for whoever runs next.
                _picker.Configure(null, null);
            }

            if (_noticeSink != null) { EventBus.Unsubscribe(_noticeSink); _noticeSink = null; }

            InteractionGate.Reset();
            MoveActionClaim.Reset();
            Interactables.Clear();
            GameServices.PlayerTransform = null;
            GameServices.Save = null;

            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }

        // =============================================================================
        //  opening
        // =============================================================================

        [UnityTest]
        public IEnumerator Working_the_fixture_opens_the_picker_and_claims_both_the_key_and_the_axis()
        {
            _fixture.Interact(new InteractActor(_player.transform.position, Vector2.right,
                                                InteractContext.OnFoot));
            yield return null;

            Assert.IsTrue(_picker.IsOpen, "the fixture's signal reached the picker — the whole seam");
            Assert.AreEqual(FixtureId, _picker.OpenFixtureId, "and it knows which wardrobe it is at");
            Assert.AreEqual(Ids.Count, _picker.OutfitCount, "listing everything the asset holds");

            Assert.IsTrue(MoveActionClaim.IsClaimed,
                          "the axis is claimed, or arrowing the list would walk the fisher out of the room");
            Assert.IsTrue(InteractionGate.IsBlocked,
                          "and the modal hold is up, or the confirm press would also board a boat and Esc " +
                          "would open the pause menu underneath");
        }

        [UnityTest]
        public IEnumerator It_opens_on_what_the_player_is_already_wearing()
        {
            var ids = Ids;
            OutfitLocker.Wear(_save.Current, ids[3]);

            _fixture.Open();
            yield return null;

            Assert.AreEqual(3, _picker.SelectedIndex, "the cursor starts on the coat you have on");
            Assert.AreEqual(ids[3], _picker.SelectedOutfitId);
            Assert.AreEqual(ids[3], _visual.AppliedOutfitId, "which is also what is on the fisher");
        }

        [UnityTest]
        public IEnumerator A_wardrobe_with_nothing_in_it_declines_and_says_so_rather_than_opening_empty()
        {
            var empty = ScriptableObject.CreateInstance<CharacterWardrobeDef>();
            empty.Outfits = new CharacterOutfitDef[0];
            _picker.Configure(empty, _camera);

            _fixture.Open();
            yield return null;

            Assert.IsFalse(_picker.IsOpen, "no panel over an empty box");
            Assert.IsFalse(MoveActionClaim.IsClaimed, "and nothing is claimed, so the player can walk off");
            Assert.IsFalse(InteractionGate.IsBlocked);
            Assert.AreEqual(1, _notices.Count, "the honest refusal is given — once, and by the half that " +
                                               "can actually see the contents");
            Assert.AreEqual(WardrobeStrings.NothingToWear, _notices[0].Text);

            Object.DestroyImmediate(empty);
        }

        [UnityTest]
        public IEnumerator It_refuses_to_open_over_another_modal()
        {
            InteractionGate.IsBlocked = true;   // a conversation mid-sentence

            _fixture.Open();
            yield return null;

            Assert.IsFalse(_picker.IsOpen, "one press cannot have two meanings");
            Assert.IsFalse(MoveActionClaim.IsClaimed);
        }

        // =============================================================================
        //  previewing — the fisher IS the preview
        // =============================================================================

        [UnityTest]
        public IEnumerator Moving_the_highlight_recolours_the_fisher_and_touches_nothing_else()
        {
            var ids = Ids;
            OutfitLocker.Wear(_save.Current, ids[0]);
            int savesBefore = _save.Saves;

            _fixture.Open();
            yield return null;

            _picker.MoveSelection(-1f);          // push
            _picker.MoveSelection(0f);           // release
            yield return null;

            Assert.AreEqual(1, _picker.SelectedIndex);
            Assert.AreEqual(ids[1], _visual.AppliedOutfitId,
                            "the live character is wearing the highlighted coat — there is no second " +
                            "preview surface to keep in step");
            Assert.Greater(_visual.AppliedSwapCount, 0, "…and real substitutions reached the renderer");

            Assert.AreEqual(ids[0], _save.Current.WornOutfitId,
                            "⭐ THE SAVE IS UNTOUCHED. A preview is what you are looking at, not what you own");
            Assert.AreEqual(savesBefore, _save.Saves, "and nothing was written to it");
        }

        [UnityTest]
        public IEnumerator The_preview_walks_the_whole_list_and_the_revert_target_never_moves()
        {
            var ids = Ids;
            OutfitLocker.Wear(_save.Current, ids[0]);

            _fixture.Open();
            yield return null;

            for (int i = 1; i < ids.Count; i++)
            {
                _picker.MoveSelection(-1f);
                _picker.MoveSelection(0f);
                yield return null;

                Assert.AreEqual(ids[i], _visual.AppliedOutfitId, $"row {i} is on the fisher");
                Assert.AreEqual(ids[0], _picker.RevertOutfitId, "and what a cancel would put back is fixed");
                Assert.AreEqual(ids[0], _save.Current.WornOutfitId, "and the save has still not moved");
            }
        }

        // =============================================================================
        //  cancelling
        // =============================================================================

        [UnityTest]
        public IEnumerator Cancelling_puts_back_exactly_what_was_saved_and_releases_everything()
        {
            var ids = Ids;
            OutfitLocker.Wear(_save.Current, ids[2]);
            int savesBefore = _save.Saves;

            _fixture.Open();
            yield return null;

            _picker.MoveSelection(-1f); _picker.MoveSelection(0f);
            _picker.MoveSelection(-1f); _picker.MoveSelection(0f);
            yield return null;
            Assert.AreEqual(ids[4], _visual.AppliedOutfitId, "…having genuinely wandered off first");

            _picker.CancelAndClose();
            yield return null;

            Assert.AreEqual(ids[2], _visual.AppliedOutfitId, "⭐ EXACTLY what was saved is back on the fisher");
            Assert.AreEqual(ids[2], _save.Current.WornOutfitId, "and the save never moved at all");
            Assert.AreEqual(savesBefore, _save.Saves);

            Assert.IsFalse(_picker.IsOpen);
            Assert.IsFalse(MoveActionClaim.IsClaimed, "the player can walk again");
            Assert.IsFalse(InteractionGate.IsBlocked, "and Esc goes back to the pause menu");
        }

        [UnityTest]
        public IEnumerator A_player_in_the_baked_look_who_cancels_goes_back_to_the_baked_look()
        {
            // The empty id is a real answer, not a missing one — and it is the one most likely to be lost
            // by a revert that fell back to "the first row" or to the wardrobe's default.
            Assert.AreEqual("", OutfitLocker.WornOutfitId(_save.Current));

            _fixture.Open();
            yield return null;

            _picker.MoveSelection(-1f); _picker.MoveSelection(0f);
            yield return null;
            Assert.AreNotEqual("", _visual.AppliedOutfitId, "…having previewed something first");

            _picker.CancelAndClose();
            yield return null;

            Assert.AreEqual("", _visual.AppliedOutfitId, "back to the outfit the sheets were baked in");
            Assert.AreEqual(0, _visual.AppliedSwapCount, "which is zero substitutions, not a substitution to it");
            Assert.AreEqual("", _save.Current.WornOutfitId);
        }

        [UnityTest]
        public IEnumerator The_fixture_going_away_underneath_the_panel_reverts_rather_than_leaking()
        {
            var ids = Ids;
            OutfitLocker.Wear(_save.Current, ids[1]);

            _fixture.Open();
            yield return null;

            _picker.MoveSelection(-1f); _picker.MoveSelection(0f);
            yield return null;

            // A region unloaded, or the storey the wardrobe is on was switched off.
            _fixture.gameObject.SetActive(false);
            yield return null;

            Assert.IsFalse(_picker.IsOpen, "a panel cannot outlive the wardrobe it is hanging off");
            Assert.AreEqual(ids[1], _visual.AppliedOutfitId, "and the preview went with it");
            Assert.IsFalse(MoveActionClaim.IsClaimed, "⭐ the player is not left unable to move");
            Assert.IsFalse(InteractionGate.IsBlocked);
        }

        [UnityTest]
        public IEnumerator A_player_carried_out_of_reach_by_something_other_than_walking_is_let_go()
        {
            var ids = Ids;
            OutfitLocker.Wear(_save.Current, ids[0]);

            _fixture.Open();
            yield return null;
            Assert.IsTrue(_picker.IsOpen);

            // With the axis claimed they cannot walk out — so this is the OTHER way it happens: a travel,
            // a rest, anything that moves the player without asking the picker.
            _player.transform.position = new Vector3(50f, 50f, 0f);
            yield return null;

            Assert.IsFalse(_picker.IsOpen, "the panel gives up rather than following them across the map");
            Assert.IsFalse(MoveActionClaim.IsClaimed, "⭐ and does not strand them with their own movement held");
            Assert.AreEqual(ids[0], _visual.AppliedOutfitId);
        }

        // =============================================================================
        //  confirming
        // =============================================================================

        [UnityTest]
        public IEnumerator Confirming_writes_the_choice_through_the_locker_and_closes()
        {
            var ids = Ids;
            OutfitLocker.Wear(_save.Current, ids[0]);
            int savesBefore = _save.Saves;

            _fixture.Open();
            yield return null;

            _picker.MoveSelection(-1f); _picker.MoveSelection(0f);
            _picker.MoveSelection(-1f); _picker.MoveSelection(0f);
            yield return null;

            Assert.IsTrue(_picker.Confirm());
            yield return null;

            Assert.AreEqual(ids[2], _save.Current.WornOutfitId, "⭐ the choice is in the save");
            Assert.AreEqual(savesBefore + 1, _save.Saves, "and it was actually written, not just set");
            Assert.AreEqual(ids[2], _visual.AppliedOutfitId, "the fisher keeps wearing it");

            Assert.IsFalse(_picker.IsOpen);
            Assert.IsFalse(MoveActionClaim.IsClaimed);
            Assert.IsFalse(InteractionGate.IsBlocked);
        }

        [UnityTest]
        public IEnumerator A_confirmed_outfit_survives_a_reload()
        {
            var ids = Ids;
            _fixture.Open();
            yield return null;

            _picker.MoveSelection(-1f); _picker.MoveSelection(0f);
            yield return null;
            _picker.Confirm();
            yield return null;

            string chosen = ids[1];
            Assert.AreEqual(chosen, _save.Current.WornOutfitId);

            // The reload: a fresh fisher, drawing herself from the save the way she does on load. This is
            // the path OnEnable takes — Apply(OutfitLocker.WornOutfitId()) — with nothing carried over
            // from the picker at all.
            Object.DestroyImmediate(_visual);
            var reloaded = _player.AddComponent<PlayerOutfitVisual>();
            yield return null;

            Assert.AreEqual(chosen, reloaded.AppliedOutfitId,
                            "⭐ she comes back wearing what was chosen, from the save alone");
            Assert.Greater(reloaded.AppliedSwapCount, 0);
            _visual = reloaded;
        }

        [UnityTest]
        public IEnumerator Re_confirming_what_is_already_worn_closes_without_a_pointless_write()
        {
            var ids = Ids;
            OutfitLocker.Wear(_save.Current, ids[5]);
            int savesBefore = _save.Saves;

            _fixture.Open();
            yield return null;
            Assert.AreEqual(ids[5], _picker.SelectedOutfitId, "the cursor opened on it");

            Assert.IsTrue(_picker.Confirm());
            yield return null;

            Assert.AreEqual(ids[5], _save.Current.WornOutfitId);
            Assert.AreEqual(savesBefore, _save.Saves,
                            "the locker declines a no-op, so agreeing with yourself does not touch the disk");
            Assert.IsFalse(_picker.IsOpen, "…but it is still a way out of the panel");
        }

        // =============================================================================
        //  the Esc key, which the pause menu also wants
        // =============================================================================

        [UnityTest]
        public IEnumerator The_wardrobe_owns_the_cancel_key_while_it_is_up_AND_on_the_frame_it_closes()
        {
            Assert.IsFalse(WardrobePicker.OwnsCancelKey, "nothing is up, so Esc belongs to the shell");

            _fixture.Open();
            yield return null;
            Assert.IsTrue(WardrobePicker.OwnsCancelKey, "the panel's Esc leaves the wardrobe");

            _picker.CancelAndClose();

            // ⭐ NO YIELD. This is the frame the press landed on, and it is the whole point: the shell
            // reads the same key in its own Update, in an order Unity does not define, so on this frame
            // the answer must still be "mine" even though the panel has already gone. Without it, whether
            // Esc also opened the pause menu would be a coin toss on component order.
            Assert.IsFalse(_picker.IsOpen, "…the panel really is closed");
            Assert.IsTrue(WardrobePicker.OwnsCancelKey,
                          "⭐ but the key is still spent — the shell must not open the pause menu behind it");

            yield return null;
            Assert.IsFalse(WardrobePicker.OwnsCancelKey, "and by the next frame Esc is the shell's again");
        }

        [UnityTest]
        public IEnumerator While_the_panel_is_up_the_world_interactor_refuses_the_confirm_press()
        {
            // The other half of the same press: the wardrobe's E must not also start a conversation with
            // whoever is standing next to the furniture. The gate is the mechanism; this pins that the
            // picker actually raises it for the whole time the panel is up.
            _fixture.Open();
            yield return null;

            Assert.IsTrue(InteractionGate.IsBlocked, "raised on open");

            _picker.MoveSelection(-1f); _picker.MoveSelection(0f);
            yield return null;
            Assert.IsTrue(InteractionGate.IsBlocked, "…and still up while the player browses");

            _picker.Confirm();
            yield return null;
            Assert.IsFalse(InteractionGate.IsBlocked, "released the moment the choice is made");
        }

        // =============================================================================
        //  the mouse
        // =============================================================================

        [UnityTest]
        public IEnumerator Pointing_at_a_row_previews_it_the_same_way_arrowing_to_it_does()
        {
            var ids = Ids;
            OutfitLocker.Wear(_save.Current, ids[0]);

            _fixture.Open();
            yield return null;

            Assert.IsTrue(_picker.SelectRow(4));
            yield return null;

            Assert.AreEqual(4, _picker.SelectedIndex);
            Assert.AreEqual(ids[4], _visual.AppliedOutfitId, "one preview path, whichever device asked");
            Assert.AreEqual(ids[0], _save.Current.WornOutfitId, "and the mouse cannot write a save either");

            Assert.IsFalse(_picker.SelectRow(4), "re-pointing at the row you are on is not a re-preview");
            Assert.IsFalse(_picker.SelectRow(ids.Count), "and a row that is not there is declined");
        }
    }
}
