using System;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// The seam between the baker and whatever JavaScript engine actually runs the art director's
    /// rigs. V8/ClearScript is the implementation we ship (ADR 0021); the seam exists so the
    /// rejected alternative — Jint, a pure-C# interpreter, ~2.7 MB of managed DLLs with a clean
    /// attribution-only licence — can be swapped back in without re-running the spike.
    ///
    /// The measured trade, from ADR 0021, in the Unity 6000.5 Mono editor:
    ///
    ///   punt, one facing        Jint 902–1051 ms   ·   V8 4.5 ms
    ///   console skiff, 1 facing Jint 1523–1942 ms  ·   V8 5.1 ms
    ///   32 dir × 4 rock, big hull  Jint ~3–4 min (threaded)  ·   V8 ~2.4 s
    ///
    /// Jint parallelises ~3.9× across facings (one Engine per thread) and was genuinely adequate
    /// at 8 directions. It is 32 directions and live scrubbing that bought V8 its ~96 MB of native
    /// binaries. If that tax ever stops being worth it, implement this interface over Jint and
    /// change the one line in <see cref="RigScriptHostFactory"/>.
    /// </summary>
    public interface IRigScriptHost : IDisposable
    {
        /// <summary>Human-readable engine identity, e.g. "V8 (ClearScript 7.5.1)". Logged by the
        /// baker so a run's provenance is never inferred.</summary>
        string EngineName { get; }

        /// <summary>Runs rig source. The art director's files are executed UNMODIFIED — that is
        /// the contract of ADR 0021 §5. Any shim a rig needs belongs in host code, never in his
        /// file. (Measured: these rigs need no shims at all — no DOM, no ImageData, no console.)</summary>
        void Execute(string script);

        /// <summary>Evaluates an expression returning a JS number.</summary>
        double EvaluateNumber(string expression);

        /// <summary>Evaluates an expression returning a JS string.</summary>
        string EvaluateString(string expression);

        /// <summary>Evaluates an expression returning a JS boolean.</summary>
        bool EvaluateBool(string expression);

        /// <summary>
        /// Evaluates an expression returning a <c>Uint8ClampedArray</c> (what every rig's
        /// <c>render()</c> returns) and copies it into managed memory.
        ///
        /// ⚠️ IMPLEMENTATIONS MUST USE A BULK COPY. ⚠️ Measured in the spike: render+readback of a
        /// 210,816-byte buffer via ClearScript's bulk <c>ITypedArray&lt;byte&gt;.ReadBytes</c> took
        /// 7.61 ms against 7.94 ms for render alone — i.e. free, within noise. The naive
        /// per-element marshalling path (indexing the JS array from C# one byte at a time) would
        /// erase the entire engine advantage that ADR 0021 is built on.
        /// </summary>
        byte[] EvaluateBytes(string expression);
    }
}
