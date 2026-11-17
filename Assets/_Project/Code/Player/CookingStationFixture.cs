using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>A placed kitchen station</b>: a prep counter, a stove or a microwave in a galley, a house or a
    /// shop (kitchens and microwaves slice). It registers with the one interact verb, so the interact popup
    /// and the action chooser offer it like any other fixture (its words come from its
    /// <see cref="CookingStationDef.VerbLabel"/>), and a press asks <see cref="GameServices.Cooking"/> to
    /// start. What it cooks, how long it takes and what it refuses are the station Def's and the recipe
    /// Defs' answers, never this component's (rule 2).
    ///
    /// <para><b>What the player hears.</b> Every answer is a line in the player's own bubble
    /// (<see cref="AmbientSpeechRequested"/>, the inner-voice kind the "too tired" line uses): started,
    /// done, nothing to cook, busy, or "that can't go in the microwave". No HUD text, no bar.</para>
    ///
    /// <para><b>Finishing.</b> While something is on this station, <see cref="Update"/> compares the clock
    /// with the ready time (one comparison, no allocation) and, when it is done, the food goes into the supply
    /// locker. A station whose scene unloads mid-cook finishes when it is next enabled.</para>
    ///
    /// <para>Placeholder look: the prefab carries no sprite yet; the art lane adds one. Not placed in any
    /// scene by this slice.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CookingStationFixture : MonoBehaviour, IInteractable
    {
        [Tooltip("The station kind: what it runs, how fast, what it refuses, and what it says.")]
        [SerializeField] private CookingStationDef _def;

        [Tooltip("Stable id, unique among live interactables (fixture.<place>.<station>, e.g. " +
                 "fixture.leblanc_house.stove). It also keys this station's cook job, so two stoves cook " +
                 "separately.")]
        [SerializeField] private string _id = "fixture.kitchen.station";

        [Tooltip("Require the player to face the station. Off: standing in reach is enough.")]
        [SerializeField] private bool _requiresFacing;

        private bool _watching;

        /// <summary>The station kind this fixture runs.</summary>
        public CookingStationDef Def => _def;

        /// <inheritdoc/>
        public string Id => _id;

        /// <inheritdoc/>
        public string VerbLabel => _def != null ? _def.VerbLabel : null;

        /// <inheritdoc/>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float ReachMeters => _def != null ? _def.ReachMeters : 0f;

        /// <inheritdoc/>
        public int Priority => InteractPriority.Fixture;

        /// <summary>Ashore in a kitchen, or aboard in a galley.</summary>
        public InteractContext Contexts => InteractContext.OnFoot | InteractContext.OnDeck;

        /// <inheritdoc/>
        public bool RequiresFacing => _requiresFacing;

        /// <summary>Available whenever it has station data. An empty locker still answers out loud, which is
        /// the cozy refusal (see <see cref="IInteractable.IsAvailable"/>).</summary>
        public bool IsAvailable => _def != null;

        /// <inheritdoc/>
        public void Interact(in InteractActor actor) => Use();

        /// <summary>Wire this station from a builder.</summary>
        public void Configure(CookingStationDef def, string id)
        {
            _def = def;
            if (!string.IsNullOrEmpty(id)) _id = id;
        }

        private void OnEnable()
        {
            Interactables.Register(this);
            _watching = GameServices.Cooking.IsCooking(_id, out _);
        }

        private void OnDisable() => Interactables.Unregister(this);

        /// <summary>Start cooking, or say why not. Public so tests and a later driver call it without input.</summary>
        public CookRefusal Use()
        {
            RecipeRunner runner = GameServices.Cooking;
            CookRefusal refusal = runner.TryStart(_def, _id);
            if (refusal == CookRefusal.None && runner.IsCooking(_id, out CookJob job))
            {
                _watching = true;
                Say(_def.StartedLine, job.Recipe.DisplayName);
            }
            else if (_def != null) Say(_def.LineFor(refusal), null);
            return refusal;
        }

        private void Update()
        {
            if (!_watching) return;
            RecipeRunner runner = GameServices.Cooking;
            if (runner.TryFinish(_id, out CookJob done))
            {
                _watching = false;
                if (_def != null) Say(_def.ReadyLine, done.Recipe.DisplayName);
            }
            else if (!runner.IsCooking(_id, out _)) _watching = false;
        }

        private static void Say(string line, string recipeName)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (recipeName != null && line.Contains("{0}")) line = string.Format(line, recipeName);
            EventBus.Publish(new AmbientSpeechRequested(
                AmbientSpeechId.Next(), GameServices.PlayerTransform,
                new Vector3(0f, EffortGate.Settings.BubbleHeightMetres, 0f),
                speakerId: null, speakerName: null, line: line, voiceId: null, kind: AmbientSpeechKind.InnerVoice));
        }
    }
}
