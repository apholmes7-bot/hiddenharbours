using System;
using System.Globalization;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Reads one facing's <b>G-buffer</b> out of a building rig that lights through
    /// <c>CoastalPass.light</c> (the returned <c>HouseIso</c>, drop 14): the frame the rig's own
    /// <c>render()</c> relights, plus the glow its own relight draws at night with every light in the
    /// house on. <see cref="BuildingLightChannels.Pack"/> turns it into the L2 sheets.
    ///
    /// <para><b>Nothing here draws.</b> The helper calls the rig's published API —
    /// <c>frame(dir, opts)</c>, <c>lights(dir, opts)</c> and <c>relight(fr, sky, o)</c> — and copies
    /// typed arrays out. The rig source runs unmodified (ADR 0021 §5).</para>
    ///
    /// <para><b>The glow is the rig's, checked against a copy of its law.</b> For every texel whose
    /// light is on, the helper recomputes <c>CoastalPass.light.relight</c>'s emitter branch — the WARM
    /// or FIRE ramp, step <c>clamp(1 + round(level·2.6) + sb + (sb &gt; 0), 0, 5)</c> — and compares
    /// it with what the rig drew. One texel off and the bake refuses: the emitter sheet would no longer
    /// be the rig's glow, and nothing downstream could tell.</para>
    ///
    /// <para><b>"Every light on" is the rig's own full occupancy</b>
    /// (<c>occupancy: {rooms: {kitchen: 1, parlour: 1, upper: 1, hall: 1}, lantern: 1}</c>) at
    /// <c>NIGHT_SKY</c>, where <c>lampNeed</c> is 1: each window at its own <c>vary(id)</c>, the
    /// lantern and the door at 1. That is the LEVEL L2 bakes; which rooms are lit at which hour is
    /// occupancy, and the SOURCE bits keep the room for it.</para>
    /// </summary>
    public static class BuildingLightFrame
    {
        /// <summary>The global the helper installs.</summary>
        public const string HelperGlobal = "__hhBuildingLightFrame";

        /// <summary>The camera elevation the lit sprite path assumes
        /// (<see cref="HiddenHarbours.Art.SpriteLightMath.CameraElevationDeg"/>). A frame at any other
        /// elevation is refused: its view normals would light from the wrong place.</summary>
        public static double CameraElevationDeg => HiddenHarbours.Art.SpriteLightMath.CameraElevationDeg;

        // The helper. Plain ES2020, no host objects. SOURCE codes are SpriteLightMath.EmitSource*.
        const string HelperJs = @"
globalThis.__hhBuildingLightFrame = (function () {
  'use strict';
  const OCC = { rooms: { kitchen: 1, parlour: 1, upper: 1, hall: 1 }, lantern: 1 };
  const SOURCE = { kitchen: 1, parlour: 2, upper: 3, hall: 4, porch: 5 };
  let cur = null;
  const bytes = (a) => new Uint8Array(a.buffer, a.byteOffset, a.byteLength);
  function take(R, dir, opts) {
    const CPL = globalThis.CoastalPass && globalThis.CoastalPass.light;
    if (!CPL) throw new Error('CoastalPass.light is not loaded, so the rig has no light frame to read.');
    for (const k of ['frame', 'lights', 'relight'])
      if (typeof R[k] !== 'function') throw new Error('the rig publishes no ' + k + '(); it has no light frame to read.');
    const fr = R.frame(dir, opts);
    if (!fr || !fr.gb) throw new Error('frame() returned no G-buffer.');
    const gb = fr.gb, W = fr.W, H = fr.H, N = W * H, C = fr.C, EM = fr.mdl.EM, sky = CPL.NIGHT_SKY;
    const L = R.lights(dir, Object.assign({}, opts, { occupancy: OCC, sky: sky }));
    const level = {}, source = {};
    for (const l of L.lamps) { level[l.id] = l.level; source[l.id] = SOURCE[l.room] || 0; }
    const night = R.relight(fr, sky, { occupancy: OCC, lights: [] });
    const fog = sky.fog || 0;
    const glow = new Uint8Array(N * 4), src = new Uint8Array(N);
    let lit = 0, miss = 0, firstMiss = null;
    for (let i = 0; i < N; i++) {
      if (!gb.a[i]) continue;
      const ek = gb.em[i]; if (!ek) continue;
      const name = EM[ek], el = Math.min(1, Math.max(0, +level[name] || 0));
      if (!(el > .04)) continue;
      const f = fr.mdl.faces[gb.fid[i]], P = f.fire ? CPL.FIRE : CPL.WARM, sb = gb.sb[i];
      const c = P[Math.min(5, Math.max(0, 1 + Math.round(el * 2.6) + sb + (sb > 0 ? 1 : 0)))];
      if (fog > .01) throw new Error('NIGHT_SKY carries fog; the copy of the emitter law does not model it.');
      const o = i * 4;
      if (night[o] !== c[0] || night[o + 1] !== c[1] || night[o + 2] !== c[2] || night[o + 3] !== 255) {
        if (!miss++) firstMiss = { x: i % W, y: (i / W) | 0, name, drew: [night[o], night[o + 1], night[o + 2], night[o + 3]], law: c };
      }
      glow[o] = night[o]; glow[o + 1] = night[o + 1]; glow[o + 2] = night[o + 2]; glow[o + 3] = 255;
      src[i] = source[name] || 0; lit++;
    }
    cur = { a: gb.a, nx: gb.nx, ny: gb.ny, nz: gb.nz, d: gb.d, glow, src };
    return JSON.stringify({ W, H, ct: C.ct, st: C.st, se: C.se, ce: C.ce, S: C.S, need: L.state.need, lit, miss, firstMiss });
  }
  return {
    take,
    a: () => bytes(cur.a), nx: () => bytes(cur.nx), ny: () => bytes(cur.ny), nz: () => bytes(cur.nz),
    d: () => bytes(cur.d), glow: () => cur.glow, src: () => cur.src,
    clear: () => { cur = null; },
  };
})();";

        /// <summary>What the helper reports beside the arrays.</summary>
        [Serializable]
        public sealed class FrameInfo
        {
            public int W, H;
            public double ct, st, se, ce, S, need;
            public int lit, miss;
            public MissInfo firstMiss;
        }

        [Serializable]
        public sealed class MissInfo
        {
            public int x, y;
            public string name;
            public int[] drew, law;
        }

        /// <summary>Install the helper into a host that already carries the rig.</summary>
        public static void Install(IRigScriptHost host) => host.Execute(HelperJs);

        /// <summary>
        /// Read facing <paramref name="dirJs"/> of the build <paramref name="optsJs"/> from rig global
        /// <paramref name="rigGlobal"/>. <see cref="Install"/> first. Refuses a frame that is not the
        /// rig's native cell, not at the lit path's 40°, not at full night need, or whose glow is not
        /// the rig's own law.
        /// </summary>
        public static BuildingLightChannels.GBuffer Capture(IRigScriptHost host, string rigGlobal,
                                                            string dirJs, string optsJs,
                                                            int width, int height, string label)
        {
            string json = host.EvaluateString($"{HelperGlobal}.take({rigGlobal},{dirJs},{optsJs})");
            var info = JsonUtility.FromJson<FrameInfo>(json);
            if (info == null) throw new InvalidOperationException($"The light frame of '{label}' reported nothing.");

            if (info.W != width || info.H != height)
                throw new InvalidOperationException(
                    $"The light frame of '{label}' is {info.W}×{info.H}, but render() draws {width}×{height}. " +
                    "The channels must sit on the albedo's pixels one for one.");

            double e = CameraElevationDeg * Math.PI / 180.0;
            if (Math.Abs(info.se - Math.Sin(e)) > 1e-9 || Math.Abs(info.ce - Math.Cos(e)) > 1e-9)
                throw new InvalidOperationException(
                    $"The light frame of '{label}' was taken at elevation " +
                    $"{Math.Atan2(info.se, info.ce) * 180 / Math.PI:0.###}°, not the " +
                    $"{CameraElevationDeg}° the lit sprite path lights at. Its normals would light from " +
                    "the wrong place.");

            if (Math.Abs(info.need - 1.0) > 1e-9)
                throw new InvalidOperationException(
                    $"The rig reports a lamp need of {info.need} at NIGHT_SKY for '{label}'. L2 bakes the " +
                    "glow with every light full on, which is need 1.");

            if (info.miss > 0)
            {
                var m = info.firstMiss;
                throw new InvalidOperationException(
                    $"The rig's night glow on '{label}' is not the emitter law this bake copies: " +
                    $"{info.miss} of {info.lit} glowing texel(s) differ, the first at ({m?.x}, {m?.y}) on " +
                    $"'{m?.name}' — the rig drew ({Join(m?.drew)}), the law says ({Join(m?.law)}). " +
                    "CoastalPass.light.relight has changed; re-derive the copy before baking.");
            }

            int n = width * height;
            var gb = new BuildingLightChannels.GBuffer
            {
                Width = width,
                Height = height,
                Coverage = Read(host, "a", n),
                NormalX = ReadFloats(host, "nx", n),
                NormalY = ReadFloats(host, "ny", n),
                NormalZ = ReadFloats(host, "nz", n),
                Depth = ReadFloats(host, "d", n),
                Glow = Read(host, "glow", n * 4),
                Source = Read(host, "src", n),
                Camera = new BuildingLightChannels.Camera(info.ct, info.st, info.se, info.ce, info.S),
            };
            host.Execute($"{HelperGlobal}.clear()");
            return gb;
        }

        static byte[] Read(IRigScriptHost host, string field, int expected)
        {
            byte[] b = host.EvaluateBytes($"{HelperGlobal}.{field}()");
            if (b.Length != expected)
                throw new InvalidOperationException(
                    $"The light frame's '{field}' came back {b.Length} bytes, expected {expected}.");
            return b;
        }

        static float[] ReadFloats(IRigScriptHost host, string field, int expected)
        {
            byte[] b = Read(host, field, expected * 4);
            if (!BitConverter.IsLittleEndian)
                throw new PlatformNotSupportedException("The light frame is read as little-endian floats.");
            var f = new float[expected];
            Buffer.BlockCopy(b, 0, f, 0, b.Length);
            return f;
        }

        static string Join(int[] a) =>
            a == null ? "?" : string.Join(", ", Array.ConvertAll(a, v => v.ToString(CultureInfo.InvariantCulture)));
    }
}
