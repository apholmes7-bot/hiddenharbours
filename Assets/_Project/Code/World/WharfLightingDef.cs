using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>HOW ONE REGION'S WORKING WHARF IS LIT AT NIGHT.</b> One asset per wharf (<c>lighting.snake_case</c>, e.g.
    /// <c>lighting.nmc_wharf</c>): where the wall's lantern posts stand, which existing lamp poles carry a light, and the
    /// harbour lights' marker glow. The region's builder step places the lamps from these records, so a lamp moves by
    /// editing this asset and planning the step again, never by hand in the scene.
    ///
    /// <para>Plain data, no behaviour. The lamps themselves are the game's ordinary scene lights: they come on with the
    /// dark (dusk and night) and stay off by day through the light's own darkness gate.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Wharf Lighting", fileName = "WharfLighting")]
    public class WharfLightingDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (lighting.snake_case).")]
        public string Id = "lighting.example";
        public string DisplayName = "";

        [Header("Lantern posts along the wall")]
        [Tooltip("Where each lantern post stands, in world plan metres, on the deck just back from the wall's lip. Each " +
                 "throws a warm pool on the deck that spills over the lip onto the wall face and the water below. Empty " +
                 "where the wharf's own layout places its lanterns (St Peters' pier: StPetersWharf.LampPostSites); the " +
                 "strength and reach below still apply to them.")]
        public Vector2[] LanternSites = new Vector2[0];
        [Tooltip("The lanterns throw lamp shadows (the owner's 11-25 ruling: lights cast shadows).")]
        public bool LanternsCastShadows = true;
        [Tooltip("The lanterns' strength, as a multiple of the shared hung-lantern preset (1 = exactly St Peters'). Set " +
                 "here, never in the shared preset, so this wharf can read at night without relighting every doorstep " +
                 "lantern on the coast.")]
        [Min(0f)] public float LanternIntensityScale = 1f;
        [Tooltip("How far each lantern's warm pool reaches on the ground, metres (the shared preset's is 3.6).")]
        [Min(0f)] public float LanternReachMetres = 3.6f;

        [Header("Lamp poles already standing on the wharf")]
        [Tooltip("The key scene rows (prop ids) of the wharf's lamp poles that carry a light. Each is lit the way the fuel " +
                 "stop's poles are: a floodlight head at the pole's drawn luminaire.")]
        public string[] LitPoleIds = new string[0];
        [Tooltip("The poles throw lamp shadows.")]
        public bool PolesCastShadows = true;
        [Tooltip("The poles' floodlight strength, as a multiple of the shared floodlight preset (1 = the fuel stop's).")]
        [Min(0f)] public float PoleIntensityScale = 1f;
        [Tooltip("How far each pole's pool reaches on the ground, metres (the shared floodlight preset's is 7).")]
        [Min(0f)] public float PoleReachMetres = 7f;

        [Header("Harbour lights (the breakwater heads' green and red)")]
        [Tooltip("The marker glow's radius in metres, drawn on the lens. A navigation light reads as a small bright point " +
                 "from a boat; it does not flood the breakwater.")]
        public float MarkerBloomMetres = 1f;
        [Tooltip("The marker glow's strength at full dark.")]
        public float MarkerIntensity = 1.6f;
        [Tooltip("0 a hard disc, 1 all falloff.")]
        [Range(0f, 1f)] public float MarkerEdgeSoftness = 0.6f;
        [Tooltip("The west head's light: a navigation green, a little toward blue as real ones read.")]
        public Color WestGreen = new Color(0.3f, 1f, 0.5f, 1f);
        [Tooltip("The east head's light.")]
        public Color EastRed = new Color(1f, 0.22f, 0.15f, 1f);

        [Header("Channel marks beside the wharf (St Peters: the entrance's inner pair)")]
        [Tooltip("The chart ids of the lit channel marks that stand by this wharf and wear the harbour-marker look below " +
                 "instead of the coast-wide nav preset (e.g. channel.stpeters_entrance.s3). Empty = none.")]
        public string[] HarbourMarkIds = new string[0];
        [Tooltip("The lantern look those marks wear (a nav light look def: small glass core, soft halo, lens colours, a " +
                 "faint streak on the water). Typed loosely so this record does not depend on the art module; the " +
                 "builder step refuses anything that is not a nav light look.")]
        public Object HarbourMarkerLook;

        [Header("Lantern shadows")]
        [Tooltip("Wharf lanterns (by scene name, e.g. lanternPost_0) that throw no lamp shadows although the rest do. For a " +
                 "lantern so close to a floating mark that the mark's silhouette would be thrown across open water.")]
        public string[] LanternsWithoutShadows = new string[0];

        /// <summary>Does the named lantern throw lamp shadows?</summary>
        public bool LanternCastsShadows(string lanternName) =>
            LanternsCastShadows && System.Array.IndexOf(LanternsWithoutShadows ?? new string[0], lanternName) < 0;

        [TextArea] public string Why = "";
    }
}
