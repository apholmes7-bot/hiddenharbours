using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// A door in the scene that can be knocked on (<see cref="KnockDoorDef"/>). It asks the keeper who lives
    /// behind it whether they are indoors and raises the matching inner-voice cue over Core; it opens nothing
    /// and decides nothing else. With no keeper wired it treats the house as empty.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KnockDoor : MonoBehaviour, IInteractable
    {
        [Tooltip("The door's words and reach.")]
        [SerializeField] private KnockDoorDef _def;

        [Tooltip("Who lives behind the door. Written by the region's layer refresh.")]
        [SerializeField] private KeeperRound _household;

        public KnockDoorDef Def => _def;
        public KeeperRound Household => _household;

        public string Id => _def != null ? _def.Id : string.Empty;
        public string VerbLabel => _def != null ? _def.VerbLabel : string.Empty;
        public Vector2 WorldPosition => transform.position;
        public float ReachMeters => _def != null ? _def.ReachMetres : 0f;
        public int Priority => InteractPriority.Fixture;
        public InteractContext Contexts => InteractContext.OnFoot;
        public bool RequiresFacing => false;
        public bool IsAvailable => _def != null;

        /// <summary>The cue a knock raises now.</summary>
        public string CueNow => _def == null ? string.Empty : _def.CueFor(_household != null && _household.IsIndoors);

        public void Interact(in InteractActor actor) => Knock();

        /// <summary>Knock once: raise the cue for whoever is (or is not) home.</summary>
        public void Knock()
        {
            if (_household != null) _household.Stand();
            string cue = CueNow;
            if (!string.IsNullOrEmpty(cue)) EventBus.Publish(new InnerVoiceCue(cue));
        }

        public void Configure(KnockDoorDef def, KeeperRound household)
        {
            _def = def;
            _household = household;
        }

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);
    }
}
