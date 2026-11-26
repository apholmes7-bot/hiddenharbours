using System;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>A cast member's own clothes over a preset's body</b>: one of the rig's cast builds with its
    /// garment, hat and colours changed, baked into a skin of its own under its own id
    /// (<c>charskin.basil</c>). The rig already draws every garment and hat named here; an outfit is a
    /// choice among the rig's own options, so it needs a bake and no art pass.
    ///
    /// <para><b>The rig's bytes are never touched.</b> The outfit is installed into a script host's
    /// copy of the rig's <c>BUILDS</c> table when the kit loads (<see cref="Install"/>), and only into
    /// that host. The rig's <c>CAST10</c> is left as it ships, so every guard that walks the rig's own
    /// presets walks exactly what it walked before, and the committed skins keep their pins.</para>
    ///
    /// <para><b>An outfit takes its base preset's face, body, proportions and gameplay sidecar.</b> It
    /// changes clothes, not the person: the base's sidecar (blink and look) is the one its bake is
    /// checked against (<see cref="SidecarPresetFor"/>).</para>
    ///
    /// <para><b>Every value is checked against the rig's own OPTIONS</b> before it is installed. The
    /// rig's <c>normBuild</c> answers an unknown value with the fisher's, so a typo here would bake the
    /// player's overalls under Basil's name without a sound.</para>
    /// </summary>
    public static class CharacterOutfits
    {
        /// <summary>One outfit: the key it bakes under, the preset whose body it wears, and the
        /// fields it changes (the rig's own field names and option values).</summary>
        public sealed class Outfit
        {
            public readonly CharacterRigKit Kit;
            public readonly string Key;
            public readonly string Stem;
            public readonly string BasePreset;
            public readonly string BaseStem;
            public readonly string Label;
            public readonly (string field, string value)[] Changes;

            public Outfit(CharacterRigKit kit, string key, string stem, string basePreset, string baseStem,
                          string label, params (string field, string value)[] changes)
            {
                Kit = kit; Key = key; Stem = stem; BasePreset = basePreset; BaseStem = baseStem;
                Label = label; Changes = changes;
            }
        }

        /// <summary>
        /// The outfits the game bakes. Owner, 2026-11-25: Basil wears outfit A, a cream knit jumper,
        /// charcoal trousers and a navy peaked cap, over the skipper's body. Hector keeps the skipper's
        /// oilskins. In the rig's terms a sweater's jumper is its <c>shirt</c> colour and its trousers
        /// are its <c>outfit</c> colour; the captain's cap is white unless <c>hatCol</c> says otherwise.
        /// </summary>
        public static readonly Outfit[] All =
        {
            new Outfit(CharacterRigKit.Rig10, "basil", "Basil", "skipper", "Skipper", "Basil",
                       ("garment", "sweater"), ("shirt", "cream"), ("outfit", "char"),
                       ("hat", "captain"), ("hatCol", "navy")),
        };

        /// <summary>The outfit baked under <paramref name="key"/> from <paramref name="kit"/>, or null.</summary>
        public static Outfit Find(CharacterRigKit kit, string key)
        {
            foreach (Outfit o in All)
                if (o.Kit == kit && string.Equals(o.Key, key, StringComparison.Ordinal)) return o;
            return null;
        }

        /// <summary>True when <paramref name="key"/> is an outfit of <paramref name="kit"/>.</summary>
        public static bool Has(CharacterRigKit kit, string key) => Find(kit, key) != null;

        /// <summary>The preset whose committed gameplay sidecar <paramref name="preset"/>'s bake is
        /// checked against: the base preset for an outfit, the preset itself otherwise.</summary>
        public static string SidecarPresetFor(CharacterRigKit kit, string preset) =>
            Find(kit, preset)?.BasePreset ?? preset;

        /// <summary>
        /// Install every outfit of <paramref name="kit"/> into <paramref name="host"/>'s copy of the
        /// rig's <c>BUILDS</c>. Idempotent. Refuses an outfit whose key is already one of the rig's own
        /// builds, whose base is not one of the rig's baked presets, or whose field or value the rig
        /// does not offer.
        /// </summary>
        public static void Install(IRigScriptHost host, CharacterRigKit kit)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (kit == null) throw new ArgumentNullException(nameof(kit));
            string g = kit.GlobalName;
            foreach (Outfit o in All)
            {
                if (o.Kit != kit) continue;
                if (host.EvaluateBool($"!!{g}.BUILDS[{Js(o.Key)}] && {g}.BUILDS[{Js(o.Key)}].__hhOutfit===true"))
                    continue;
                if (host.EvaluateBool($"!!{g}.BUILDS[{Js(o.Key)}]"))
                    throw new InvalidOperationException(
                        $"{kit}'s BUILDS already has '{o.Key}'. An outfit never shadows one of the rig's own builds.");
                if (!host.EvaluateBool($"{g}.{kit.PresetTable}.indexOf({Js(o.BasePreset)})>=0"))
                    throw new InvalidOperationException(
                        $"Outfit '{o.Key}' wears '{o.BasePreset}', which is not one of {kit}'s {kit.PresetTable}.");

                var js = new StringBuilder($"Object.assign({{}},{g}.BUILDS[{Js(o.BasePreset)}],{{label:{Js(o.Label)}");
                foreach ((string field, string value) in o.Changes)
                {
                    if (!host.EvaluateBool($"{g}.FIELDS.indexOf({Js(field)})>=0"))
                        throw new InvalidOperationException($"Outfit '{o.Key}' sets '{field}', which is not one of {kit}'s build fields.");
                    if (!host.EvaluateBool($"Array.isArray({g}.OPTIONS[{Js(field)}]) && {g}.OPTIONS[{Js(field)}].indexOf({Js(value)})>=0"))
                        throw new InvalidOperationException(
                            $"Outfit '{o.Key}' sets {field} = '{value}', which {kit} does not offer; its normBuild " +
                            "would quietly answer with the fisher's.");
                    js.Append(',').Append(field).Append(':').Append(Js(value));
                }
                js.Append("})");
                host.Execute($"(function(){{var b={js};Object.defineProperty(b,'__hhOutfit',{{value:true}});" +
                             $"{g}.BUILDS[{Js(o.Key)}]=b;}})();");

                // The rig answers the outfit's fields back as installed, or the outfit is refused.
                foreach ((string field, string value) in o.Changes)
                    if (host.EvaluateString($"String({g}.normBuild({Js(o.Key)})[{Js(field)}])") != value)
                        throw new InvalidOperationException(
                            $"{kit}'s normBuild('{o.Key}') does not answer {field} = '{value}'.");
            }
        }

        static string Js(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
    }
}
