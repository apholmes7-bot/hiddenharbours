using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>An authored landing point. ScreenPoint is on the host's picture; SortY is its ground sort line.</summary>
    public interface IGullPerch
    {
        string Id { get; }
        Vector2 GroundPoint { get; }
        Vector2 ScreenPoint { get; }
        float SortY { get; }
    }

    /// <summary>Scene-lifetime offers and exclusive reservations. No update loop and no saved state.</summary>
    public static class GullPerches
    {
        static readonly List<IGullPerch> Offers = new List<IGullPerch>();
        static readonly Dictionary<IGullPerch, object> Claims = new Dictionary<IGullPerch, object>();

        public static void Register(IGullPerch perch)
        {
            if (perch == null) throw new ArgumentNullException(nameof(perch));
            foreach (var existing in Offers)
                if (ReferenceEquals(existing, perch)) return;
                else if (existing.Id == perch.Id) throw new InvalidOperationException("Duplicate gull perch: " + perch.Id);
            Offers.Add(perch);
            Offers.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
        }

        public static void Unregister(IGullPerch perch) { Claims.Remove(perch); Offers.Remove(perch); }
        public static bool IsClaimedBy(IGullPerch perch, object bird) =>
            perch != null && Claims.TryGetValue(perch, out object owner) && ReferenceEquals(owner, bird);

        public static bool TryClaim(Vector2 centre, Vector2 halfSize, object bird, out IGullPerch perch)
        {
            if (bird == null) throw new ArgumentNullException(nameof(bird));
            foreach (var offer in Offers)
            {
                Vector2 d = offer.GroundPoint - centre;
                if (Math.Abs(d.x) > halfSize.x || Math.Abs(d.y) > halfSize.y || Claims.ContainsKey(offer)) continue;
                Claims.Add(offer, bird);
                perch = offer;
                return true;
            }
            perch = null;
            return false;
        }

        public static void Release(IGullPerch perch, object bird)
        {
            if (IsClaimedBy(perch, bird)) Claims.Remove(perch);
        }
    }
}
