using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>Which renderer the interact highlight may copy</b>, as a pure rule the presenter asks and a test
    /// can pin.
    ///
    /// <para><b>The defect this closes (owner, 2026-11-19: the player never falls back to the old sprite
    /// sheets).</b> The highlight draws by copying a renderer's sprite into two overlays. It used to take
    /// the first <see cref="SpriteRenderer"/> under the registrant, whether or not that renderer was
    /// actually drawing. The belt pail is registered on the player's own object, so after a dig, with a
    /// clam in hand and the pail offered, the overlays copied the player's body sprite: a renderer the
    /// mesh figure keeps enabled but holds off with <see cref="Renderer.forceRenderingOff"/>. The result
    /// was a faint, standing copy of the old sprite sheet drawn over the mesh player.</para>
    ///
    /// <para><b>Two rules, both needed.</b></para>
    /// <list type="number">
    /// <item><b>Only a renderer that draws is copied</b> (<see cref="IsDrawn"/>). Hidden by
    /// <c>enabled</c>, by <c>forceRenderingOff</c>, by an inactive object, by a clear colour or by having
    /// no sprite: none of them is a picture on screen, so none of them may be lit. This holds for every
    /// fixture, not only the player.</item>
    /// <item><b>A registrant on the player's own object is never lit</b> (<see cref="IsOnPlayer"/>).
    /// It has no picture of its own (the pail rides his belt), so the only thing a highlight could find
    /// there is the player himself, and lighting the player says nothing about the pail.</item>
    /// </list>
    ///
    /// <para>No allocation: the caller owns the scratch list.</para>
    /// </summary>
    public static class InteractAffordanceTargets
    {
        /// <summary>True when <paramref name="renderer"/> puts its sprite on screen this frame.</summary>
        public static bool IsDrawn(SpriteRenderer renderer)
        {
            if (renderer == null) return false;
            if (!renderer.enabled || renderer.forceRenderingOff) return false;
            if (!renderer.gameObject.activeInHierarchy) return false;
            if (renderer.sprite == null) return false;
            return renderer.color.a > 0f;
        }

        /// <summary>True when the registrant sits on the player's own object.</summary>
        public static bool IsOnPlayer(Transform where, Transform player)
            => where != null && player != null && where == player;

        /// <summary>
        /// The renderer the highlight copies for a registrant at <paramref name="where"/>: the first one
        /// under it that is drawn, or null. Null for a registrant on the player's own object.
        /// <paramref name="scratch"/> is filled and cleared here, so nothing is allocated per call.
        /// </summary>
        public static SpriteRenderer Resolve(Transform where, Transform player, List<SpriteRenderer> scratch)
        {
            if (where == null || scratch == null) return null;
            if (IsOnPlayer(where, player)) return null;

            where.GetComponentsInChildren(false, scratch);
            SpriteRenderer found = null;
            for (int i = 0; i < scratch.Count; i++)
            {
                if (!IsDrawn(scratch[i])) continue;
                found = scratch[i];
                break;
            }
            scratch.Clear();
            return found;
        }
    }
}
