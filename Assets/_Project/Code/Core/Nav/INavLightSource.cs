using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>What light does this mark show, and when in its period?</b> — asked of whatever component
    /// happens to be sitting on a nav mark's root, so the thing that DRAWS a buoy light never has to
    /// know what a buoy is.
    ///
    /// <para><b>Why this is a seam in Core (rule 4).</b> The data is a <c>NavBuoyDef</c>, which is a
    /// Boats type; the light is a <c>SceneLight</c> quad, which is Art; and the Art assembly does not
    /// reference Boats and must not start. So the question is declared here, in the module both sides
    /// already speak — exactly the shape <see cref="IVesselWay"/> took for the fleet's own lamps, and
    /// for the same reason.</para>
    ///
    /// <para><b>A read, not an event.</b> Everything here is fixed for the life of a placed mark: a
    /// buoy does not change her character, and her phase is a property of her chart id. It is read
    /// once when the light installs. What VARIES — whether the lamp is burning this instant — is not
    /// on this interface at all, because it is not a fact anybody stores: it is
    /// <see cref="NavLightCharacter.IsOn"/> of the master clock (rule 5).</para>
    /// </summary>
    public interface INavLightSource
    {
        /// <summary>
        /// The character she shows, already parsed. A default (unlit) character means an unlit
        /// mark — a mooring buoy, a spar — and the correct response to it is NO LIGHT AT ALL, not a
        /// light that never comes on: an unlit mark must cost nothing.
        /// </summary>
        NavLightCharacter Character { get; }

        /// <summary>
        /// Where in her period she sits, in seconds — her own offset, so two marks of one character
        /// do not wink together.
        ///
        /// <para><b>Seconds rather than a seed, deliberately.</b> How a mark came by her phase is her
        /// own business: a placed mark is given one by <see cref="NavLightPhasePlan"/>, which shares
        /// the period out among everything wearing that character in her region, while a mark dropped
        /// by hand falls back to a hash of her id. The lamp does not care which, and it must not —
        /// putting a SEED here would have committed every mark to the hash, and the hash is the half
        /// that measured badly (two cans 0.021 s apart on a four-second period).</para>
        /// </summary>
        float PhaseSeconds { get; }

        /// <summary>
        /// How far above the waterline the lantern sits, in metres. The nav-buoy sheets pivot ON the
        /// waterline, so this is measured up from the mark's own transform and a tall channel pillar
        /// carries her light higher than a little harbour can does.
        /// </summary>
        float LanternHeightMetres { get; }

        /// <summary>
        /// The transform the lantern rides.
        ///
        /// <para><b>⚠️ It is the BOBBED visual, not the mark's root, and the difference is the whole
        /// reason this is on the interface rather than assumed.</b> A buoy's light goes up and down
        /// with the buoy — that is most of what a lit mark looks like in a seaway — and the bob lives
        /// on a child transform precisely because the wave field is sampled at the root, which must
        /// therefore stay still. Hang the light on the root and it burns at a fixed height while the
        /// can it is bolted to heaves half a metre underneath it.</para>
        ///
        /// <para>Null is allowed and means "use my own transform": a mark with no separate visual is
        /// simply a mark that does not bob.</para>
        /// </summary>
        Transform LanternMount { get; }
    }
}
