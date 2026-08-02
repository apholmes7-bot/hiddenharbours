#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tools.RigStudio
{
    /// <summary>
    /// The CHARACTER kit's Rig Studio adapter — preset × anim × facing × frame × carry, every value
    /// read from the rig's own tables (<c>CAST</c>, <c>BUILDS</c>, <c>ANIMS</c>, <c>ANIM_MOUNT</c>,
    /// <c>CARRY_ORDER</c>, <c>CARRIES</c>) rather than restated. The character is the first kit whose
    /// parameter space has a genuine CROSS-AXIS law in two directions at once, and both are the
    /// adapter's business, not the studio's:
    ///
    /// <list type="bullet">
    ///   <item><b>Frame count is per anim</b> — <c>reel</c> has twelve, <c>strike</c> six. The axis
    ///   carries the longest and the preview refuses a frame the viewed anim does not have, naming
    ///   its real count. (The rock kit's per-species variant count, again.)</item>
    ///   <item><b>Carry is only legal on a FREE anim</b>, and only on the anims the rig's CARRIES
    ///   table rides. Asking for pails on a <c>reel</c> is not a stance the rig would draw — it
    ///   drops the carry and renders the rod. Showing that as "buckets" would be the silent
    ///   substitution this studio exists to refuse.</item>
    /// </list>
    ///
    /// <para><b>Preview = the bake's own render call.</b> Same <c>render(dir, opts)</c>, same opts
    /// composition (anim, frame, power, carry, build preset) that <c>CharacterRigBaker</c> writes into
    /// a sheet cell, so what the owner approves is bit-identical to what ships. That is the contract's
    /// hard requirement and the reason the opts are composed in one place.</para>
    ///
    /// <para><b>Bake = the kit's own menu chain.</b> Viewing a cast preset bakes the cast; viewing the
    /// player bakes the player. Output lands in the working tree exactly where a menu bake puts it —
    /// the studio is a front door, never a parallel writer.</para>
    /// </summary>
    public sealed class CharacterStudioAdapter : IRigStudioAdapter
    {
        public const string PresetAxis = "preset";
        public const string AnimAxis = "anim";
        public const string DirAxis = "dir";
        public const string FrameAxis = "frame";
        public const string CarryAxis = "carry";
        public const string PowerAxis = "power";

        /// <summary>What the carry axis reads when the figure's hands are empty.</summary>
        public const string NoCarry = "—";

        /// <summary>What the power axis reads on an anim with no power sub-range.</summary>
        public const string NoPower = "—";

        IRigScriptHost _host;
        RigGeometry _geo;
        RigStudioSchema _schema;

        string Global => RigCatalog.Get("character").GlobalName;

        IRigScriptHost Host()
        {
            if (_host != null) return _host;
            _host = RigScriptHostFactory.Create();
            // Install() pulls the head and eye rigs in first — the body renders without them, badly
            // and silently, so this is the one call that must never be replaced by a raw Execute.
            _geo = RigCatalog.Install(_host, RigCatalog.Get("character"));
            return _host;
        }

        // ---- schema ------------------------------------------------------------------------------

        public RigStudioSchema Describe()
        {
            if (_schema != null) return _schema;

            IRigScriptHost host = Host();
            string g = Global;

            string[] cast = Csv(host.EvaluateString($"{g}.CAST.join(',')"));
            string[] anims = Csv(host.EvaluateString($"Object.keys({g}.ANIMS).join(',')"));
            string[] carries = Csv(host.EvaluateString($"{g}.CARRY_ORDER.join(',')"));
            int longestAnim = (int)host.EvaluateNumber(
                $"Math.max.apply(null, Object.keys({g}.ANIMS).map(function(k)" +
                $"{{ return {g}.ANIMS[k].frames; }}))");

            var labels = cast.ToDictionary(
                p => p,
                p => host.EvaluateString($"String(({g}.BUILDS['{p}'] || {{}}).label || '{p}')"));

            _schema = new RigStudioSchema
            {
                KitName = "The cast",
                About = "One procedural person: eight facings, fourteen animations, four carry " +
                        "stances, and ten built cast members. Colour is a runtime ramp swap — what " +
                        "is baked here is SHAPE.",
                BakeNote = "Bake this bakes the viewed BUILD's whole sheet set — the player " +
                           "(fisher) gets every state plus its carry stances, a cast member gets " +
                           "idle and walk — then slices them. It does not bake the single cell on " +
                           "screen; there is no such sheet.",
                Axes = new[]
                {
                    new RigStudioAxis
                    {
                        Key = PresetAxis, Label = "Who", Kind = RigStudioAxisKind.Keys,
                        Keys = cast, Default = cast[0],
                        Note = "the rig's own cast, in its own order — 'fisher' is the player",
                    },
                    new RigStudioAxis
                    {
                        Key = AnimAxis, Label = "Anim", Kind = RigStudioAxisKind.Keys,
                        Keys = anims, Default = "idle",
                    },
                    new RigStudioAxis
                    {
                        Key = DirAxis, Label = "Facing", Kind = RigStudioAxisKind.IntRange,
                        Min = 0, Max = _geo.NativeDirs - 1, Default = 4,
                        Note = "row index — 0 is N, 4 is S (toward the camera). Which way the rows " +
                               "RUN is measured at bake time, never assumed",
                    },
                    new RigStudioAxis
                    {
                        Key = FrameAxis, Label = "Frame", Kind = RigStudioAxisKind.IntRange,
                        Min = 0, Max = longestAnim - 1, Default = 0,
                        Note = "the count is per anim (the rig's ANIMS table) — a frame the viewed " +
                               "anim does not have refuses with its real count",
                    },
                    new RigStudioAxis
                    {
                        Key = CarryAxis, Label = "Carrying", Kind = RigStudioAxisKind.Keys,
                        Keys = new[] { NoCarry }.Concat(carries).ToArray(), Default = NoCarry,
                        Note = "a stance changes the POSE, so each is its own sheet. Only free " +
                               "anims carry; a tool anim always wins",
                    },
                    new RigStudioAxis
                    {
                        Key = PowerAxis, Label = "Power", Kind = RigStudioAxisKind.Keys,
                        Keys = new[] { NoPower, "short", "long" }, Default = NoPower,
                        Note = "the cast's sub-range — 'cast' only. Its own sheet either way",
                    },
                },
            };
            return _schema;
        }

        // ---- preview -----------------------------------------------------------------------------

        public RigStudioPreview Preview(IReadOnlyDictionary<string, object> point)
        {
            RigStudioPoints.Validate(Describe(), point);

            IRigScriptHost host = Host();
            string g = Global;

            string preset = RigStudioPoints.KeyOf(point, PresetAxis);
            string anim = RigStudioPoints.KeyOf(point, AnimAxis);
            int dir = RigStudioPoints.IntOf(point, DirAxis);
            int frame = RigStudioPoints.IntOf(point, FrameAxis);
            string carry = RigStudioPoints.KeyOf(point, CarryAxis);
            string power = RigStudioPoints.KeyOf(point, PowerAxis);

            // ---- cross-axis law 1: frame count is per anim ----------------------------------------
            int frames = CharacterRigBaker.FramesOf(host, g, anim);
            if (frame >= frames)
                throw new ArgumentException(
                    $"'{anim}' is {frames} frames — frame {frame} does not exist. Wrapping it round " +
                    "would show a cell the sheet does not contain, which is exactly the silent " +
                    "substitution this studio refuses.");

            // ---- cross-axis law 2: only a free anim carries ---------------------------------------
            string mount = CharacterRigBaker.MountOf(host, g, anim);
            string stance = carry == NoCarry ? null : carry;
            if (stance != null)
            {
                if (mount != "free")
                    throw new ArgumentException(
                        $"'{anim}' drives the {mount} layer, so the rig ignores carry on it. It " +
                        $"would render the {mount} and this preview would be labelled '{stance}' " +
                        "while showing something else.");

                var allowed = AllowedCarries(host, g, anim);
                if (!allowed.Contains(stance))
                    throw new ArgumentException(
                        $"The rig's CARRIES table does not ride '{stance}' on '{anim}'. It allows: " +
                        $"{(allowed.Length == 0 ? "(none)" : string.Join(", ", allowed))}.");
            }

            string sub = power == NoPower ? null : power;
            if (sub != null && anim != "cast")
                throw new ArgumentException(
                    $"'{anim}' has no power sub-range — only 'cast' reads it. The rig would ignore " +
                    $"'{sub}' and this preview would claim a variant that does not exist.");

            var state = new CharacterState(anim, sub, stance);

            // THE one render path. Same call, same opts composition, as the sheet cell.
            string opts = $"{{anim:'{anim}',frame:{frame}" +
                          (sub != null ? $",power:'{sub}'" : "") +
                          (stance != null ? $",carry:'{stance}'" : "") +
                          $",build:{{preset:'{preset}'}}}}";
            byte[] rgba = host.EvaluateBytes($"{g}.render({dir},{opts})");

            int expected = _geo.Width * _geo.Height * 4;
            if (rgba.Length != expected)
                throw new InvalidOperationException(
                    $"render came back {rgba.Length} bytes, expected {expected} for " +
                    $"{_geo.Width}×{_geo.Height} RGBA — the cell does not match the rig's own " +
                    "geometry. Do not show it.");

            double heightM = host.EvaluateNumber($"{g}.metrics({{preset:'{preset}'}}).top");
            string label = host.EvaluateString($"String(({g}.BUILDS['{preset}'] || {{}}).label || '{preset}')");

            return new RigStudioPreview
            {
                Rgba = rgba,
                Width = _geo.Width,
                Height = _geo.Height,
                HasPivot = true,
                PivotX = _geo.PivotX,
                PivotY = _geo.PivotY,
                Caption = $"{label} ({preset}) — {heightM:0.00} m · {state.Key} " +
                          $"f{frame}/{frames} · facing row {dir}/{_geo.NativeDirs} · " +
                          $"cell {_geo.Width}×{_geo.Height}, pivot ({_geo.PivotX},{_geo.PivotY}) " +
                          $"= ground contact {_geo.Height - _geo.PivotY} px up · mount {mount}",
            };
        }

        static string[] AllowedCarries(IRigScriptHost host, string g, string anim) =>
            Csv(host.EvaluateString(
                $"{g}.CARRY_ORDER.filter(function (c) {{ var e = {g}.CARRIES[c]; " +
                $"return e && Array.isArray(e.anims) && e.anims.indexOf('{anim}') >= 0; }})" +
                ".join(',')"));

        // ---- bake --------------------------------------------------------------------------------

        public RigStudioBakeReport Bake(IReadOnlyDictionary<string, object> point)
        {
            RigStudioPoints.Validate(Describe(), point);
            string preset = RigStudioPoints.KeyOf(point, PresetAxis);

            // The kit's own menu chain — bake, force-reimport, slice — never a parallel writer. The
            // player and the cast are different recipes because they are different jobs: he needs
            // every state, they need the gaits.
            bool isPlayer = string.Equals(preset, CharacterRigBakeMenu.PlayerPreset,
                                          StringComparison.Ordinal);
            if (isPlayer) CharacterRigBakeMenu.BakePlayerSheets();
            else CharacterRigBakeMenu.BakeCastSheets();

            string folder = isPlayer
                ? CharacterRigBakeMenu.CharacterArtFolder
                : CharacterRigBakeMenu.FolderFor(preset);

            return new RigStudioBakeReport
            {
                Minted = new[] { folder },
                PlaceableNote = isPlayer
                    ? "The player's sheets are re-baked and sliced. Re-run Build Character Visual " +
                      "Defs and the persistent-core builder — a re-slice changes the sprite sub-asset " +
                      "ids, so anything holding a serialized reference to them goes stale."
                    : $"'{preset}' is baked and sliced under {folder}. An NPC wears it through its " +
                      "CharacterBuildDef; the world builders read that.",
                Summary = isPlayer
                    ? "Baked the player: every state the rig declares, plus its carry stances."
                    : $"Baked the cast ({CharacterRigBakeMenu.Cast.Length} presets × idle + walk). " +
                      $"'{preset}' is one of them — the cast bakes as a set, since nine bakes of one " +
                      "preset each would spin nine hosts.",
            };
        }

        static string[] Csv(string s) =>
            string.IsNullOrEmpty(s) ? Array.Empty<string>() : s.Split(',');

        public void Dispose()
        {
            _host?.Dispose();
            _host = null;
        }
    }
}
#endif
