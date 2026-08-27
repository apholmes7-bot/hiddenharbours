using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>What kind of vehicle a drop is</b> — the discriminator that decides, eventually, whether
    /// she can leave the road (ADR 0035).
    ///
    /// <para>Deliberately small, and deliberately NOT a list of every machine: it names the
    /// capability the game branches on, not the model. A second pickup adds a row to
    /// <c>VehicleRigFleet</c> and no value here.</para>
    /// </summary>
    public enum VehicleKind
    {
        /// <summary>Roads and yards only. Water is a wall.</summary>
        RoadVehicle = 0,

        /// <summary>Drives ashore and swims. The water seam reads this.</summary>
        AmphibiousVehicle = 1,

        /// <summary>
        /// ⚠️ <b>TOWED. Not a vehicle that drives — a body that is dragged.</b> A semi-trailer has
        /// no engine, no steering axle and no driver's seat; she goes where her tractor's fifth
        /// wheel takes her.
        ///
        /// <para>She is in this enum rather than beside it because she arrives through the same
        /// door: an art kit with a gameplay sidecar, scanned by the same coverage law, baked by the
        /// same mesh path. What she must never do is reach a driving code path — see
        /// <see cref="VehicleKinds.IsDrivable"/>, which is the one question that separates her from
        /// the two above.</para>
        /// </summary>
        TowedBody = 2,
    }

    /// <summary>
    /// ⭐ <b>THE ONE PLACE a shipped <c>kind</c> token becomes a <see cref="VehicleKind"/>.</b>
    ///
    /// <para><b>Why a mapping exists at all, rather than the enum simply being named what the art
    /// says.</b> The art side and the repo disagree, and BOTH of the art side's own words disagree
    /// with each other: the Otter's gameplay sidecar declares <c>"kind": "amphibious_xtv"</c> while
    /// her rig's body record says <c>amphib_xtv</c>, and the ruled repo-side value is
    /// <c>amphibious_vehicle</c>. Three spellings of one idea.</para>
    ///
    /// <para><b>The ruling (coordinator, on #549, 2026-08-17):</b> the shipped tokens are accepted AS
    /// SHIPPED. <c>docs/art/rigs/**</c> is the art director's lane and a generated sidecar is never
    /// hand-edited — a token "fixed" by hand is a token that comes back wrong on the next
    /// regeneration, and worse, it breaks the sidecar's own hash pin. So the translation lives here,
    /// in code, in exactly one table.</para>
    ///
    /// <para>⚠️ <b>An unknown token is a REFUSAL, never a default.</b> Falling back to
    /// <see cref="VehicleKind.RoadVehicle"/> would drive an amphibian onto the road and drown a truck
    /// — silently, because both are plausible. <see cref="TryFromToken"/> returns false and the
    /// coverage law turns that into a failing test, which is how a new drop's unrecognised word
    /// reaches a human instead of a launch ramp.</para>
    /// </summary>
    public static class VehicleKinds
    {
        /// <summary>
        /// Every shipped spelling, and what it means. Keys are compared ordinal-insensitively so a
        /// drop that changes case does not become a new vehicle.
        ///
        /// <para>Add a row here — never a special case at a call site — when the art side ships a
        /// word this does not know.</para>
        /// </summary>
        static readonly IReadOnlyDictionary<string, VehicleKind> Tokens =
            new Dictionary<string, VehicleKind>(StringComparer.OrdinalIgnoreCase)
            {
                // The repo's own ruled names.
                ["road_vehicle"] = VehicleKind.RoadVehicle,
                ["amphibious_vehicle"] = VehicleKind.AmphibiousVehicle,

                // As SHIPPED by the art side, accepted rather than corrected.
                //   · the Dually's sidecar already says `road_vehicle`, so she needs no alias.
                //   · the Otter's SIDECAR says `amphibious_xtv`…
                ["amphibious_xtv"] = VehicleKind.AmphibiousVehicle,
                //   · …and her RIG's own body record says `amphib_xtv`. Both are real, both are on
                //     disk, and a reader that knew only one of them would work until it read the
                //     other file.
                ["amphib_xtv"] = VehicleKind.AmphibiousVehicle,

                // The repo's ruled name for a towed body, and the road-fleet drop's own PLURAL
                // spelling of it. The trailer kit ships ONE rig and ONE sidecar carrying FOUR
                // bodies (flatbed 28/53, reefer 28/53), and its `kind` is plural because the
                // document really does describe four — its `variant` is `trailers-x4`. Accepted
                // as shipped, like the Otter's two spellings above; the ENUM stays singular,
                // because a kind describes one registered body.
                ["towed_body"] = VehicleKind.TowedBody,
                ["towed_bodies"] = VehicleKind.TowedBody,
            };

        /// <summary>Every token this repo recognises — the coverage law enumerates it so a sidecar
        /// carrying an unknown word fails loudly rather than going unscanned.</summary>
        public static IEnumerable<string> KnownTokens => Tokens.Keys;

        /// <summary>
        /// Translate a shipped token. <b>False for anything unrecognised</b> — see the class doc for
        /// why there is no default.
        /// </summary>
        public static bool TryFromToken(string token, out VehicleKind kind)
        {
            if (!string.IsNullOrWhiteSpace(token))
                return Tokens.TryGetValue(token.Trim(), out kind);
            kind = default;
            return false;
        }

        /// <summary>True when this token names a vehicle at all — which is what tells a vehicle
        /// sidecar apart from a boat's (boat sidecars carry no top-level <c>kind</c>).</summary>
        public static bool IsVehicleToken(string token) => TryFromToken(token, out _);

        /// <summary>The repo-side canonical name, for messages and for anything that has to write
        /// the value back out. Never the shipped alias.</summary>
        public static string CanonicalToken(VehicleKind kind) => kind switch
        {
            VehicleKind.RoadVehicle => "road_vehicle",
            VehicleKind.AmphibiousVehicle => "amphibious_vehicle",
            VehicleKind.TowedBody => "towed_body",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unmapped VehicleKind"),
        };

        /// <summary>
        /// ⭐ <b>Can this kind be DRIVEN?</b> The one question that separates a towed body from the
        /// two machines, and the reason <see cref="VehicleKind.TowedBody"/> is worth a value of its
        /// own rather than a flag on a road vehicle.
        ///
        /// <para>⚠️ <b>A trailer has no engine, no steering axle and no seat</b> — measured, not
        /// assumed: <c>trailerIsoRig.js</c> resolves no <c>steer</c> axis at all, and the kit's own
        /// README says <i>"No steering — towed bodies"</i>. Every driving path (throttle, lock
        /// angles, mounting, fuel burn) has to ask this before it poses anything, because a trailer
        /// handed to a controller looks exactly like a truck with a broken steering rack: she
        /// drives, badly, and nothing fails.</para>
        ///
        /// <para>Written as an explicit switch rather than <c>kind != TowedBody</c> so a new kind
        /// added to the enum cannot silently inherit "drivable" — it stops compiling instead,
        /// which is where that decision belongs.</para>
        /// </summary>
        public static bool IsDrivable(VehicleKind kind) => kind switch
        {
            VehicleKind.RoadVehicle => true,
            VehicleKind.AmphibiousVehicle => true,
            VehicleKind.TowedBody => false,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unmapped VehicleKind"),
        };
    }
}
