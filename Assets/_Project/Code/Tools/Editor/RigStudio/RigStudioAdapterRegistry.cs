using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace HiddenHarbours.Tools.RigStudio
{
    /// <summary>
    /// Finds every <see cref="IRigStudioAdapter"/> in the loaded editor assemblies via
    /// <see cref="TypeCache"/> — which is the whole growth mechanism: a kit import that ships an
    /// adapter class (public, concrete, parameterless constructor) appears as a studio tab with
    /// ZERO changes to the studio core. There is no registration call to forget and no list here
    /// to keep in step with the imports.
    /// </summary>
    public static class RigStudioAdapterRegistry
    {
        /// <summary>One discovered adapter — constructed, or the reason it could not be.</summary>
        public sealed class Entry
        {
            public Type Type;
            public IRigStudioAdapter Adapter;

            /// <summary>Why construction failed, verbatim — shown on the tab rather than swallowed,
            /// so a broken adapter reads as "this kit's adapter is broken", never as "this kit
            /// doesn't exist".</summary>
            public string Error;
        }

        /// <summary>
        /// Construct every discoverable adapter. A type that fails to construct still gets an entry
        /// (with <see cref="Entry.Error"/> set); the caller owns disposal of the ones that succeeded.
        /// </summary>
        public static List<Entry> CreateAll()
        {
            var entries = new List<Entry>();

            foreach (Type type in TypeCache.GetTypesDerivedFrom<IRigStudioAdapter>()
                                           .OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition) continue;

                var entry = new Entry { Type = type };
                try
                {
                    if (type.GetConstructor(Type.EmptyTypes) == null)
                        throw new InvalidOperationException(
                            "no public parameterless constructor — the registry constructs adapters " +
                            "itself, so an adapter must not need arguments.");

                    entry.Adapter = (IRigStudioAdapter)Activator.CreateInstance(type);
                }
                catch (Exception e)
                {
                    entry.Error = (e.InnerException ?? e).Message;
                }
                entries.Add(entry);
            }

            return entries;
        }
    }
}
