using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>The room-extraction rules every room bake shares</b> (ADR 0041, 0048 and 0060): the room's own
    /// material table deduplicated by ramp content, the depth shift that draws a room in front of the
    /// shell around it, and the interior ramp cap.
    ///
    /// <para>Factored out of <see cref="BoatInteriorGeometryExtractor"/> and
    /// <see cref="RigMeshAssetBaker.AppendMeshInteriorIfConverted"/> when the dwelling bake became the
    /// second caller (ADR 0060 section 2: "the shared extraction is factored out, not copied"). The
    /// boat path runs through these unchanged, so a converted hull bakes the same bytes it did.</para>
    /// </summary>
    public static class RigRoomExtraction
    {
        /// <summary>
        /// The room's OWN material table, a separate index space from the shell's (the shader's
        /// <c>_RampMetaInterior</c>). Materials are deduplicated by RAMP CONTENT, not by name: measured on
        /// the boats, cab=wains, daylight=glass, flame=screen and iron=panel are byte-identical, so four
        /// name pairs collapse to four slots with no colour lost.
        /// </summary>
        public sealed class MaterialTable
        {
            readonly Dictionary<string, int> _byRamp = new Dictionary<string, int>(StringComparer.Ordinal);
            readonly Dictionary<string, int> _byName = new Dictionary<string, int>(StringComparer.Ordinal);

            /// <summary>The deduplicated materials, in first-use order.</summary>
            public readonly List<RigMaterial> Materials = new List<RigMaterial>();

            /// <summary>True when <paramref name="name"/> already resolved to a slot.</summary>
            public bool TryGetIndex(string name, out int index) => _byName.TryGetValue(name, out index);

            /// <summary>
            /// The slot for material <paramref name="name"/> with ramp <paramref name="rampHex"/>: an
            /// existing slot when a material with the same ramp was seen before, a new one otherwise.
            /// </summary>
            public int IndexFor(string name, string[] rampHex)
            {
                if (_byName.TryGetValue(name, out int known)) return known;
                if (rampHex == null || rampHex.Length == 0)
                    throw new InvalidOperationException($"material '{name}' has an empty ramp.");

                string key = string.Join(",", rampHex);
                if (!_byRamp.TryGetValue(key, out int index))
                {
                    index = Materials.Count;
                    _byRamp[key] = index;
                    Materials.Add(new RigMaterial
                    {
                        Name = name,
                        RampHex = rampHex,
                        Ramp = rampHex.Select(ParseHex).ToArray(),
                        Off = 0,
                    });
                }
                _byName[name] = index;
                return index;
            }
        }

        /// <summary>
        /// Diagonal of the axis-aligned bounds of <paramref name="faces"/>, rig metres: the room's depth shift.
        ///
        /// <para>UV0.z is the rig's per-face <c>db</c>, which the vertex stage subtracts from clip depth.
        /// A shell's near walls stand between the camera and a room's floor in a three-quarter view;
        /// measured on the boats, a revealed room survived at 20.3% without the shift and 97.6% with
        /// it. The diagonal is the smallest shift guaranteed to clear the shell's own geometry at any
        /// heading. 0 for no faces.</para>
        /// </summary>
        public static double BoundingDiameter(IEnumerable<RigFace> faces)
        {
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            foreach (RigFace f in faces)
                foreach (Vector3d v in f.V)
                {
                    if (v.X < minX) minX = v.X; if (v.X > maxX) maxX = v.X;
                    if (v.Y < minY) minY = v.Y; if (v.Y > maxY) maxY = v.Y;
                    if (v.Z < minZ) minZ = v.Z; if (v.Z > maxZ) maxZ = v.Z;
                }
            if (minX > maxX) return 0;
            double dx = maxX - minX, dy = maxY - minY, dz = maxZ - minZ;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// The interior ramp cap, as a refusal: null when <paramref name="rampCount"/> fits the facet
        /// shader's interior table, otherwise the reason it does not. The owner of the room is named so
        /// the bake log says which one.
        /// </summary>
        public static string InteriorRampProblem(string owner, int rampCount)
        {
            if (rampCount <= HullMeshDef.InteriorRampSlots) return null;
            return $"{owner}'s room paints {rampCount} ramps and the facet shader's _RampMetaInterior " +
                   $"holds {HullMeshDef.InteriorRampSlots}. Do NOT spend the shell's own " +
                   $"{HullMeshDef.HullRampSlots} to fix this; that cap is a fleet law. Merge ramps in the " +
                   "extraction, or take a widening upstream with a measured cost.";
        }

        /// <summary>Throws when <see cref="InteriorRampProblem"/> finds one.</summary>
        public static void RequireInteriorRampsFit(string owner, int rampCount)
        {
            string problem = InteriorRampProblem(owner, rampCount);
            if (problem != null) throw new InvalidOperationException(problem);
        }

        /// <summary>A rig ramp colour, <c>#rrggbb</c>, opaque.</summary>
        public static Color32 ParseHex(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex[0] != '#' || hex.Length < 7)
                throw new FormatException($"interior ramp colour '{hex}' is not #rrggbb.");
            return new Color32(
                Convert.ToByte(hex.Substring(1, 2), 16),
                Convert.ToByte(hex.Substring(3, 2), 16),
                Convert.ToByte(hex.Substring(5, 2), 16), 255);
        }
    }
}
