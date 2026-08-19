using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The thing that puts the player back where they went to sleep</b> (ADR 0037) — the mirror of
    /// <see cref="RestSaveResponder"/>, and the half that gives the manual save its meaning. Subscribes
    /// to <see cref="GameLoaded"/>, reads the anchor <see cref="RestLocker"/> kept, moves
    /// <see cref="GameServices.PlayerTransform"/> to it, and says so with <see cref="PlayerWokeAt"/>.
    ///
    /// <para><b>Why here and not in <see cref="SaveRestore"/>.</b> That class restores things it can
    /// name a Core INTERFACE for — the clock, the wallet, the licence service — and there is no
    /// <c>IPlayer</c>: what moves is a <c>Transform</c> on a relay, published by the App travel rig for
    /// exactly this kind of consumer. It is also the pattern <see cref="SaveRestore"/> itself documents
    /// for everything it does not push: "read LIVE off <see cref="ISaveService.Current"/> by their owning
    /// lanes ... <see cref="GameLoaded"/> is the single edge those lanes re-sync on". This is one of
    /// those lanes, written as a static installer so it needs no scene wiring and no component to go
    /// missing from a region builder's output — the same shape, and the same owner
    /// (<c>SaveService</c>), as the responder it mirrors.</para>
    ///
    /// <para><b>⭐ SAME-REGION ONLY, AND IT SAYS SO OUT LOUD.</b> An anchor in a region other than the one
    /// that booted is DECLINED, with a warning naming both — never applied. Waking in a different region
    /// than the start scene means driving the additive region load from the save, which is a travel-rig
    /// change and not a save-schema one; the anchor already carries everything that change would need,
    /// so it is a read-side extension and not a second migration. What must never happen in the meantime
    /// is the silent alternative: St Peters coordinates applied inside Nine Mile Creek would put the
    /// player in the sea, or in a cliff, with nothing in the log to say why. Today this costs nothing
    /// real — the game's one authored player bed is in the start region — which is precisely why it is
    /// the honest place to stop.</para>
    ///
    /// <para><b>The storey is not applied here, deliberately.</b> Core has no idea what a building is.
    /// This publishes <see cref="PlayerWokeAt"/> carrying the whole anchor and <c>BuildingInterior</c>
    /// (World, which owns the ADR 0036 level ladder) opens on the right floor. Rule 4, and the same seam
    /// <see cref="OutfitChanged"/> uses to re-skin a fisher Core cannot draw.</para>
    /// </summary>
    public static class RestWakeRestorer
    {
        /// <summary>Whether the restorer is currently listening. Idempotence is the point — see
        /// <see cref="Install"/>.</summary>
        public static bool Installed { get; private set; }

        /// <summary>Begin answering loads. Double-install is a no-op: two subscriptions would move the
        /// player twice and publish two wakes for one load, and a service that survives quit-to-title can
        /// plausibly be re-awoken.</summary>
        public static void Install()
        {
            if (Installed) return;
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
            Installed = true;
        }

        /// <summary>Stop answering. Safe when not installed (a teardown must never have to check).</summary>
        public static void Uninstall()
        {
            if (!Installed) return;
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
            Installed = false;
        }

        static void OnGameLoaded(GameLoaded _) => Wake();

        /// <summary>
        /// Put the player at their anchor if there is one to honour. Returns the anchor that was APPLIED,
        /// or <see cref="RestAnchor.None"/> when nothing was — which covers every ordinary reason as well
        /// as every unusual one:
        /// <list type="bullet">
        ///   <item>the player has never turned in (a new game, and every save older than v13),</item>
        ///   <item>the anchor belongs to another region — declined loudly, see the class remarks,</item>
        ///   <item>no player has been published (EditMode, a bare art scene, a region opened for review).</item>
        /// </list>
        ///
        /// <para>Public and returning what it did so a test can drive the whole contract as a call, the
        /// same courtesy <see cref="RestSaveResponder.Fulfil"/> extends.</para>
        /// </summary>
        public static RestAnchor Wake()
        {
            RestAnchor anchor = RestLocker.Anchor();
            if (!anchor.IsSet) return RestAnchor.None;      // never rested — the authored spawn stands

            string here = GameServices.CurrentRegionId;
            if (!anchor.IsIn(here))
            {
                // LOUD, and deliberately not an error: the save is fine, the feature is simply not built
                // out this far yet. Silence here is the one outcome that would be a bug — the player
                // would wake at the spawn with no way to tell that from "the anchor was never written".
                Debug.LogWarning(
                    $"[RestWakeRestorer] You went to sleep in '{anchor.Region}' but the game booted into " +
                    $"'{(string.IsNullOrEmpty(here) ? "(no region reported)" : here)}'. Waking at this " +
                    "region's own spawn instead — cross-region wake needs the travel rig to choose the " +
                    "region from the save, which it does not do yet (ADR 0037). The anchor is kept.");
                return RestAnchor.None;
            }

            // ⚠ The accessor has already laundered Unity's fake-null into a REAL null, so a plain
            // != null is right here and a ?. would NOT be — see GameServices.PlayerTransform.
            Transform player = GameServices.PlayerTransform;
            if (player == null) return RestAnchor.None;     // nobody to wake; carry on, never throw

            // Match RegionTravelCoordinator.ApplyArrival: the rig is placed by writing the transform.
            // Z is left exactly as the rig had it — the sort band reads Y (ADR 0032) and the anchor has
            // no business inventing a depth for a player it only ever knew in two dimensions.
            Vector3 at = player.position;
            player.position = new Vector3(anchor.Position.x, anchor.Position.y, at.z);

            // The move has landed; whoever owns the ROOM answers this. See the class remarks.
            EventBus.Publish(new PlayerWokeAt(anchor));
            return anchor;
        }
    }
}
