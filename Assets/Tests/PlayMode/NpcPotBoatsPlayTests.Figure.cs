using System.Collections;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    public partial class NpcPotBoatsPlayTests
    {
        void EnableSkipperMeshes()
        {
            Assert.IsNotNull(CharacterFigurePresentation.Service);
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.MeshCast = true;
            config.MeshCastAshore = true;
            _spawned.Add(config);
            GameServices.Config = config;
        }

        CharacterFigurePresenter DeckFigure => _hull.GetComponentInChildren<CharacterFigurePresenter>(true);

        static void AssertMesh(CharacterFigurePresenter figure, string state, bool ashore)
        {
            Assert.IsNotNull(figure);
            Assert.IsTrue(figure.DrawsInsteadOfSprite, figure.NotDrawingReason);
            Assert.IsTrue(figure.GetComponent<SpriteRenderer>().enabled);
            Assert.IsTrue(figure.GetComponent<SpriteRenderer>().forceRenderingOff);
            Assert.AreEqual(state, ashore ? figure.AshoreFigure.DrawnStateKey : figure.DrawnStateKey);
        }

        static void AssertSprite(CharacterFigurePresenter figure)
        {
            Assert.IsFalse(figure.DrawsInsteadOfSprite);
            var sprite = figure.GetComponent<SpriteRenderer>();
            Assert.IsTrue(sprite.enabled);
            Assert.IsFalse(sprite.forceRenderingOff);
            Assert.IsNotNull(sprite.sprite, "a fallback must have a real sprite to draw");
        }

        [UnityTest]
        public IEnumerator Landing_IsAMeshOnBothLadderLegsAndTheWharf_ThenReturnsAboard()
        {
            EnableSkipperMeshes();
            Assert.IsTrue(_boats.TryBind(_def, _hull));
            yield return At(_today.ReturnSeconds + 5.0);
            var plan = _boats.LandingOf(_def.Id);
            var walk = _boats.LandingWalkOf(_def.Id);
            var ashore = walk.FigureCharacter.GetComponent<CharacterFigurePresenter>();
            var aboard = DeckFigure;
            Assert.IsTrue(plan.HasLanding);
            Assert.AreSame(_hull.Owner.Skipper.Skin, walk.FigureCharacter.Visual.Skin);
            AssertMesh(aboard, CharacterSkinStateMap.Idle, false);

            double up = plan.StartSeconds + 2 * _def.DeckWalkSeconds + _def.CrateLiftSeconds;
            double down = plan.EndSeconds - _def.DeckWalkSeconds - _def.LadderSeconds;
            foreach (double start in new[] { up, down })
            {
                foreach (double progress in new[] { 0.2, 0.5, 0.8 })
                {
                    double t = start + _def.LadderSeconds * progress;
                    yield return At(t);
                    var moment = plan.MomentAt(t);
                    Assert.IsTrue(moment.OnLadder);
                    Assert.IsTrue(walk.IsAshore);
                    AssertMesh(ashore, CharacterClipStateMap.LadderDown, true);
                    Assert.IsFalse(aboard.DrawsInsteadOfSprite, "only the ashore figure draws");
                    Assert.IsFalse(aboard.GetComponent<SpriteRenderer>().enabled);
                    Assert.IsTrue(walk.TryGetFigureClip(out var clip, out float routeProgress));
                    Assert.AreEqual(CharacterClip.LadderDown, clip);
                    Assert.That(routeProgress, Is.EqualTo(moment.Step01).Within(0.0001f));
                    Assert.IsTrue(walk.FigureCharacter.Visual.Skin.TryGetClip(CharacterClipStateMap.LadderDown, out var skinClip));
                    int expectedFrame = Mathf.Clamp(Mathf.FloorToInt(moment.Step01 * skinClip.FrameCount), 0, skinClip.FrameCount - 1);
                    Assert.AreEqual(expectedFrame, ashore.AshoreFigure.RequestedFrame);
                    Vector2 foot = _hull.transform.TransformPoint(_def.StepAshoreLocalMeters);
                    Vector2 expected = moment.Step == NpcLandingStep.Carrying
                        ? Vector2.Lerp(foot, _def.LandingRoute[0], moment.Step01)
                        : Vector2.Lerp(_def.LandingRoute[0], foot, moment.Step01);
                    Assert.That((walk.Position - expected).magnitude, Is.LessThan(0.01f));
                }
            }

            yield return At(up + _def.LadderSeconds + 1.0);
            AssertMesh(ashore, CharacterSkinStateMap.Walk, true);
            Assert.That(walk.FigureCharacter.Speed, Is.EqualTo(_def.LandingWalkSpeed).Within(0.001f));
            Assert.IsTrue(walk.InHands.enabled);
            Assert.AreSame(_def.LandingCrateSprite, walk.InHands.sprite);
            Assert.AreEqual(1, walk.InHands.GetComponents<SpriteRenderer>().Length);
            Assert.IsNull(walk.InHands.GetComponent<MeshRenderer>());
            Assert.IsFalse(_boats.DeckOf(_def.Id).InHand.enabled);

            yield return At(plan.SoldAtSeconds - _def.SaleSeconds * 0.5);
            AssertMesh(ashore, CharacterSkinStateMap.Idle, true);
            Assert.AreEqual(0f, walk.FigureCharacter.Speed);
            Assert.IsFalse(walk.InHands.enabled);

            yield return At(plan.EndSeconds + 1.0);
            Assert.IsFalse(walk.IsAshore);
            Assert.IsFalse(ashore.DrawsInsteadOfSprite);
            AssertMesh(aboard, CharacterSkinStateMap.Idle, false);
        }

        [UnityTest]
        public IEnumerator Deck_WalksWithItsRouteVelocity_AndIdlesAtTheRailAndHelm()
        {
            EnableSkipperMeshes();
            Assert.IsTrue(_boats.TryBind(_def, _hull));
            double moving = _today.GaffStart[0] + _def.GaffSeconds * 0.25;
            yield return At(moving);
            var deck = _boats.DeckOf(_def.Id);
            var figure = DeckFigure;
            var character = figure.Stand.FigureCharacter;
            Vector2 delta = deck.RailDeckMeters - deck.HelmDeckMeters;
            float expectedSpeed = delta.magnitude / (_def.GaffSeconds * 0.5f);
            AssertMesh(figure, CharacterSkinStateMap.Walk, false);
            Assert.That(character.Speed, Is.EqualTo(expectedSpeed).Within(0.001f));
            float heading = _today.PoseAt(moving).HeadingDegrees + NpcPotDay.Bearing(Vector2.zero, delta, 0f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(character.HeadingDegrees, heading)), Is.LessThan(0.01f));
            Assert.That((deck.StandDeckMeters - (deck.HelmDeckMeters + deck.RailDeckMeters) * 0.5f).magnitude, Is.LessThan(0.01f));

            yield return At(_today.HaulStart[0] + 1.0);
            AssertMesh(figure, CharacterSkinStateMap.Idle, false);
            Assert.AreEqual(0f, character.Speed);

            yield return At(_today.DepartSeconds + 30.0);
            AssertMesh(figure, CharacterSkinStateMap.Idle, false);
            Assert.AreEqual(0f, character.Speed, "hull travel is not a footstep");
            yield return At(moving);
            AssertMesh(figure, CharacterSkinStateMap.Walk, false);
            Assert.That(character.Speed, Is.EqualTo(expectedSpeed).Within(0.001f), "seeking back needs no previous frame");
        }

        [UnityTest]
        public IEnumerator Skipper_MissingSkinRestoresSprites_AshoreAndAboard()
        {
            EnableSkipperMeshes();
            Assert.IsTrue(_boats.TryBind(_def, _hull));
            yield return At(_today.HaulStart[0] + 1.0);
            var deck = DeckFigure;
            AssertMesh(deck, CharacterSkinStateMap.Idle, false);
            var visual = Object.Instantiate(_hull.Owner.Skipper);
            _spawned.Add(visual);
            visual.Skin = null;
            deck.Stand.FigureCharacter.Configure(visual);
            yield return At(_today.HaulStart[0] + 1.0);
            AssertSprite(deck);

            var plan = _boats.LandingOf(_def.Id);
            var walk = _boats.LandingWalkOf(_def.Id);
            double onWharf = plan.StartSeconds + 2 * _def.DeckWalkSeconds + _def.CrateLiftSeconds + _def.LadderSeconds + 1.0;
            yield return At(onWharf);
            var ashore = walk.FigureCharacter.GetComponent<CharacterFigurePresenter>();
            AssertMesh(ashore, CharacterSkinStateMap.Walk, true);
            walk.FigureCharacter.Configure(visual);
            yield return At(onWharf);
            AssertSprite(ashore);
            Assert.IsFalse(deck.DrawsInsteadOfSprite);
        }

        [UnityTest]
        public IEnumerator Skipper_MissingWalkClipKeepsTheWalkingSprite_WithoutAnIdleMesh()
        {
            EnableSkipperMeshes();
            Assert.IsTrue(_boats.TryBind(_def, _hull));
            var visual = Object.Instantiate(_hull.Owner.Skipper);
            var skin = Object.Instantiate(visual.Skin);
            _spawned.Add(visual);
            _spawned.Add(skin);
            skin.Clips = skin.Clips.Where(clip => clip.StateKey != CharacterSkinStateMap.Walk).ToArray();
            Assert.IsTrue(skin.IsUsable());
            Assert.IsTrue(skin.TryGetClip(CharacterSkinStateMap.Idle, out _), "idle is present but must not replace walking");
            visual.Skin = skin;
            DeckFigure.Stand.FigureCharacter.Configure(visual);
            yield return At(_today.GaffStart[0] + _def.GaffSeconds * 0.25);
            AssertSprite(DeckFigure);
            Assert.AreEqual(CharacterGait.Walk, DeckFigure.Stand.FigureCharacter.Gait);
            var plan = _boats.LandingOf(_def.Id);
            var walk = _boats.LandingWalkOf(_def.Id);
            walk.FigureCharacter.Configure(visual);
            yield return At(plan.StartSeconds + 2 * _def.DeckWalkSeconds + _def.CrateLiftSeconds + _def.LadderSeconds + 1.0);
            AssertSprite(walk.FigureCharacter.GetComponent<CharacterFigurePresenter>());
            Assert.AreEqual(CharacterGait.Walk, walk.FigureCharacter.Gait);
        }

        [UnityTest]
        public IEnumerator Skipper_MissingIdleAndLadderClipsRestoreSprites_ThenRecover()
        {
            EnableSkipperMeshes();
            Assert.IsTrue(_boats.TryBind(_def, _hull));
            var visual = Object.Instantiate(_hull.Owner.Skipper);
            var skin = Object.Instantiate(visual.Skin);
            _spawned.Add(visual);
            _spawned.Add(skin);
            skin.Clips = skin.Clips.Where(clip => clip.StateKey != CharacterSkinStateMap.Idle &&
                                                 clip.StateKey != CharacterClipStateMap.LadderDown).ToArray();
            Assert.IsTrue(skin.IsUsable(), "remove clips without poisoning the rest of the skin");
            visual.Skin = skin;
            DeckFigure.Stand.FigureCharacter.Configure(visual);
            yield return At(_today.HaulStart[0] + 1.0);
            AssertSprite(DeckFigure);
            var walk = _boats.LandingWalkOf(_def.Id);
            walk.FigureCharacter.Configure(visual);
            var plan = _boats.LandingOf(_def.Id);
            double ladder = plan.StartSeconds + 2 * _def.DeckWalkSeconds + _def.CrateLiftSeconds + _def.LadderSeconds * 0.5;
            yield return At(ladder);
            var ashore = walk.FigureCharacter.GetComponent<CharacterFigurePresenter>();
            AssertSprite(ashore);
            walk.FigureCharacter.Configure(_hull.Owner.Skipper);
            yield return At(ladder);
            AssertMesh(ashore, CharacterClipStateMap.LadderDown, true);
        }
    }
}
