using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Where a plant sorts against a HULL (the owner, 2026-11-16: "it seem that sprites appear above the
    /// boat when they should be below/behind"). A hull's picture sorts below the whole decor band
    /// (<c>BoatVisualDef.SortingOrder</c>), and the plant field Y-sorts every plant inside that band, so
    /// any plant at all drew over a boat run up on the foreshore. The rule:
    /// <list type="bullet">
    /// <item><description>growth that LIES on the ground (ground cover, intertidal weed) is always under a
    /// hull;</description></item>
    /// <item><description>a plant that STANDS sorts against her waterline pivot: south of it (nearer the
    /// camera) it draws over her, as before; behind it and inside her picture it draws under her.</description></item>
    /// </list>
    /// Pure functions of the plant, the field's dials and the hulls' footprints, so a test can state the
    /// rule without a scene.
    /// </summary>
    public static class PlantHullSorting
    {
        /// <summary>Whether this species lies on the ground rather than standing: no taller than
        /// <paramref name="groundCoverMaxHeightM"/> (and measured at all), or growing in one of the intertidal
        /// <paramref name="lyingHabitats"/> (weed that lies flat on the rocks when the tide leaves it).</summary>
        public static bool LiesFlat(PlantSpeciesDef species, float groundCoverMaxHeightM, string[] lyingHabitats)
        {
            if (species == null) return false;
            // A species with no measured height (0) is not taken for ground cover: it stands, as before.
            if (species.StandingHeightM > 0f && species.StandingHeightM <= groundCoverMaxHeightM) return true;
            if (lyingHabitats == null || string.IsNullOrEmpty(species.Habitat)) return false;
            for (int i = 0; i < lyingHabitats.Length; i++)
                if (string.Equals(lyingHabitats[i], species.Habitat, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// Whether a standing plant rooted at <paramref name="root"/> (its ground point, world metres) is
        /// BEHIND one of <paramref name="hulls"/>: north of her waterline pivot, and close enough that her
        /// picture can cover it — inside her drawn outline across the screen, and no further up-screen
        /// than her drawn outline plus <paramref name="pictureRiseM"/> (what stands on her deck: cabin,
        /// mast). Beyond that the two never overlap and the order cannot matter.
        /// </summary>
        public static bool BehindAHull(Vector2 root, IReadOnlyList<IHullPresence> hulls, float pictureRiseM)
        {
            if (hulls == null) return false;
            for (int i = 0; i < hulls.Count; i++)
            {
                var presence = hulls[i];
                if (presence == null) continue;
                HullFootprint hull = presence.Footprint;
                float squash = presence.PictureForeshortenY;
                if (!(squash > 0f) || squash > 1f) squash = 1f;
                Vector2 bow = hull.BowDirection;
                float halfX = Mathf.Abs(bow.x) * hull.HalfLength + Mathf.Abs(bow.y) * hull.HalfBeam;
                float halfY = (Mathf.Abs(bow.y) * hull.HalfLength + Mathf.Abs(bow.x) * hull.HalfBeam) * squash;
                Vector2 d = root - hull.Center;
                if (d.y <= 0f || Mathf.Abs(d.x) > halfX) continue;
                if (d.y <= halfY + Mathf.Max(0f, pictureRiseM)) return true;
            }
            return false;
        }

        /// <summary>
        /// The order a plant draws at. <paramref name="submerged"/> wins (the sea reads over it);
        /// growth that lies flat, or stands behind a hull, goes <see cref="SortingBands.UnderHulls"/>;
        /// everything else Y-sorts in the decor band by its ground Y, as it always has.
        /// </summary>
        public static int OrderFor(float groundY, bool submerged, int submergedOrder, bool liesFlat, bool behindAHull)
        {
            if (submerged) return submergedOrder;
            if (liesFlat || behindAHull) return SortingBands.UnderHulls;
            return YSortSprite.OrderFor(groundY, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                                        SortingBands.DecorFloor, SortingBands.DecorCeiling);
        }
    }
}
