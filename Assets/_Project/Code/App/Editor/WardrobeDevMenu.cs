using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Wear the next outfit, in play</b> — the owner's way to SEE the wardrobe before there is a
    /// wardrobe to walk to.
    ///
    /// <para>The fixture that opens the picker lands with the cabin's upstairs, so until then the
    /// recolour has no way into the game and would have to be taken on trust from a test. It should not
    /// be: the whole point of a palette swap is what it looks like. This cycles the shipped presets on
    /// the live player so the change can be judged by eye — which is also the fastest way to notice a
    /// preset whose colours fight the fisher's blond hair, something no assertion is going to
    /// tell anyone.</para>
    ///
    /// <para>A menu item rather than a dev key on purpose: the key ledger is exhausted A–Z, and this is
    /// a scaffold with a known end date, not an input the game is gaining.</para>
    /// </summary>
    public static class WardrobeDevMenu
    {
        [MenuItem("Hidden Harbours/Dev/Wear Next Outfit", priority = 153)]
        private static void WearNext()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Wardrobe] Enter play mode first — the recolour is applied to the live " +
                                 "player's renderer, so there is nothing to dress in edit mode.");
                return;
            }

            var wardrobe = Resources.Load<CharacterWardrobeDef>(CharacterWardrobeDef.ResourcesPath);
            if (wardrobe == null || wardrobe.Count == 0)
            {
                Debug.LogError($"[Wardrobe] No wardrobe at Resources/{CharacterWardrobeDef.ResourcesPath}, " +
                               "or it holds no outfits.");
                return;
            }

            string worn = OutfitLocker.WornOutfitId();
            int index = wardrobe.IndexOf(worn);
            var next = wardrobe.Outfits[(index + 1 + wardrobe.Count) % wardrobe.Count];

            if (!OutfitLocker.Wear(next.Id))
            {
                Debug.LogWarning($"[Wardrobe] Could not change into '{next.Id}' — no save service is " +
                                 "attached, so there is nowhere to record what the player is wearing.");
                return;
            }

            var swaps = new List<CharacterColourSwap>();
            wardrobe.TryBuildPalette(next.Id, swaps, out _);
            Debug.Log($"[Wardrobe] Now wearing {next.Label} ({next.Id}) — outfit '{next.Outfit}', " +
                      $"shirt '{next.Shirt}', {swaps.Count} colour(s) swapped.");
        }
    }
}
