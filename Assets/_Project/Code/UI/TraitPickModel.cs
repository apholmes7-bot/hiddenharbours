using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// <b>The creator's "pick your traits" choice</b> (owner ruling 2026-11-14): exactly
    /// <see cref="Required"/> named traits from whatever the catalog offers. Pure state over trait ids, so
    /// it never knows which traits exist or how many; the creator feeds it <see cref="ITraitService.All"/>
    /// and <see cref="GameServices.TraitPicksPerCharacter"/>.
    ///
    /// <para><b>The rules.</b> A pick toggles; a full hand refuses a new pick until one is let go; the
    /// creator may only continue when the hand is exactly full. A catalog smaller than the allowance asks
    /// only for what it has, so a short catalog never strands the player. The result is written only
    /// through <see cref="ITraitService.SetPlayerTraits"/> (<see cref="WriteTo"/>).</para>
    /// </summary>
    public sealed class TraitPickModel
    {
        private readonly List<string> _offered = new List<string>();
        private readonly List<string> _picked = new List<string>();
        private readonly List<string> _opening = new List<string>();

        /// <param name="offered">Every trait id the catalog offers, in display order. Null, empty and
        /// repeated ids are skipped.</param>
        /// <param name="picksPerCharacter">The allowance (<see cref="GameServices.TraitPicksPerCharacter"/>).</param>
        /// <param name="held">Ids the character already holds (a reopened creator). Ids not on offer,
        /// repeats and any beyond the allowance are dropped.</param>
        public TraitPickModel(IEnumerable<string> offered, int picksPerCharacter, IEnumerable<string> held = null)
        {
            if (offered != null)
                foreach (string id in offered)
                    if (!string.IsNullOrEmpty(id) && !_offered.Contains(id)) _offered.Add(id);
            Required = Math.Max(0, Math.Min(picksPerCharacter, _offered.Count));
            if (held != null)
                foreach (string id in held)
                    if (_opening.Count < Required && _offered.Contains(id) && !_opening.Contains(id)) _opening.Add(id);
            _picked.AddRange(_opening);
        }

        /// <summary>The ids on offer, in display order.</summary>
        public IReadOnlyList<string> Offered => _offered;

        /// <summary>The ids picked so far, in pick order.</summary>
        public IReadOnlyList<string> Picked => _picked;

        /// <summary>How many picks continuing needs: the allowance, or the whole catalog if smaller.</summary>
        public int Required { get; }

        /// <summary>Picks still owed before the creator can continue.</summary>
        public int Remaining => Required - _picked.Count;

        /// <summary>True when exactly <see cref="Required"/> traits are picked.</summary>
        public bool IsComplete => _picked.Count == Required;

        public bool IsPicked(string id) => id != null && _picked.Contains(id);

        /// <summary>Why the creator cannot continue yet, in plain words; null when it can.</summary>
        public string Why => IsComplete ? null : CharacterCreatorStrings.TraitsOwed(Remaining, Required);

        /// <summary>Pick <paramref name="id"/>, or let it go if already picked. False, with
        /// <paramref name="refusal"/>, for an id not on offer or a new pick into a full hand.</summary>
        public bool Toggle(string id, out string refusal)
        {
            refusal = null;
            if (id == null || !_offered.Contains(id))
            {
                refusal = CharacterCreatorStrings.UnknownTrait;
                return false;
            }
            if (_picked.Remove(id)) return true;
            if (_picked.Count >= Required)
            {
                refusal = CharacterCreatorStrings.TraitsFull(Required);
                return false;
            }
            _picked.Add(id);
            return true;
        }

        /// <summary>Back to the picks the creator opened with.</summary>
        public void Reset()
        {
            _picked.Clear();
            _picked.AddRange(_opening);
        }

        /// <summary>Keep the picks as the local player's traits. Refuses (writing nothing) while picks are
        /// owed, or when the service refuses.</summary>
        public bool WriteTo(ITraitService traits, out string refusal)
        {
            refusal = Why;
            if (refusal != null) return false;
            if (traits == null)
            {
                refusal = CharacterCreatorStrings.NoTraitService;
                return false;
            }
            return traits.SetPlayerTraits(_picked, out refusal);
        }
    }
}
