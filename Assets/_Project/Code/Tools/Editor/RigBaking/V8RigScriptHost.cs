using System;
using System.IO;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript; // ITypedArray<byte> — the bulk ReadBytes path lives here
using Microsoft.ClearScript.V8;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <see cref="IRigScriptHost"/> over V8, via Microsoft ClearScript 7.5.1. Editor-only — see
    /// the licence note in Assets/_Project/Plugins/Editor/JsEngine/THIRD-PARTY.md before you are
    /// tempted to make this reachable from runtime code.
    /// </summary>
    public sealed class V8RigScriptHost : IRigScriptHost
    {
        public const string PluginFolder = "Assets/_Project/Plugins/Editor/JsEngine";

        /// <summary>
        /// ⚠️ Required, and its absence FAILS SILENTLY. Without ClearScript.V8.ICUData.dll present,
        /// Unity reports "Unable to resolve reference 'ClearScript.V8.ICUData'" and then skips the
        /// WHOLE assembly — so the test run reports total=0 and exits 0, which reads GREEN. This
        /// cost a debugging cycle in the spike behind ADR 0021. Asserted, never inferred.
        /// </summary>
        public const string IcuDataAssembly = "ClearScript.V8.ICUData.dll";

        public static readonly string[] RequiredFiles =
        {
            "ClearScript.Core.dll",
            "ClearScript.V8.dll",
            IcuDataAssembly,
        };

        readonly V8ScriptEngine _engine;

        public string EngineName { get; }

        public V8RigScriptHost()
        {
            // ClearScript probes for its native library next to the managed assembly. Under Unity
            // the managed DLL is loaded from a shadow-copy location, so point it at the real
            // plugin folder or the native load fails with a misleading "not found".
            HostSettings.AuxiliarySearchPath = AbsolutePluginFolder;

            // Plain constructor, exactly as the ADR 0021 spike proved. The rigs need no host
            // objects and no shims, so there is nothing to configure and nothing to expose.
            _engine = new V8ScriptEngine();
            EngineName = $"V8 (ClearScript {typeof(V8ScriptEngine).Assembly.GetName().Version})";
        }

        public static string AbsolutePluginFolder =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, PluginFolder)
                .Replace('\\', Path.DirectorySeparatorChar);

        public void Execute(string script) => _engine.Execute(script);

        public double EvaluateNumber(string expression) =>
            Convert.ToDouble(_engine.Evaluate(expression));

        public string EvaluateString(string expression) =>
            Convert.ToString(_engine.Evaluate(expression));

        public bool EvaluateBool(string expression) =>
            Convert.ToBoolean(_engine.Evaluate(expression));

        public byte[] EvaluateBytes(string expression)
        {
            object result = _engine.Evaluate(expression);

            // THE BULK PATH — see the warning on IRigScriptHost.EvaluateBytes. Do not replace this
            // with a loop that indexes the JS array; that is the one implementation mistake that
            // would undo the engine choice in ADR 0021.
            if (result is ITypedArray<byte> typed)
            {
                var buffer = new byte[typed.Length];
                ulong read = typed.ReadBytes(0, typed.Length, buffer, 0);
                if (read != typed.Length)
                    throw new InvalidOperationException(
                        $"Bulk readback was short: got {read} of {typed.Length} bytes from `{expression}`.");
                return buffer;
            }

            throw new InvalidOperationException(
                $"Expected a Uint8ClampedArray from `{expression}` but got " +
                $"{(result == null ? "null" : result.GetType().FullName)}. Every rig's render() " +
                "returns a Uint8ClampedArray; if this fired, the expression is wrong, not the rig.");
        }

        public void Dispose() => _engine?.Dispose();
    }
}
