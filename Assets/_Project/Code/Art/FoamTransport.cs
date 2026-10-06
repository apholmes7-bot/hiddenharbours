using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>ADR 0027 F2: visual-only transfer. No clock, random state or simulation consumer.</summary>
    internal static class FoamTransport
    {
        internal const uint Codes = 65535;
        internal const float FlowCellMeters = 1f; // eight history cells; approved sampling contract
        internal const float MaxExtentMeters = 96f;
        internal const float MaxCourant = .25f;
        internal const int MaxSubsteps = 4;
        internal const int Matchings = 4;
        internal const int MaxContacts = 128;
        internal const string StrainName = "_FoamTransportStrain";
        internal const string CurlName = "_FoamTransportCurl";
        internal const string CollectionName = "_FoamTransportCollection";
        internal static readonly int StrainId = Shader.PropertyToID(StrainName);
        internal static readonly int CurlId = Shader.PropertyToID(CurlName);
        internal static readonly int CollectionId = Shader.PropertyToID(CollectionName);

        internal enum Route { Legacy, Uniform, Local, Hold, Unavailable }
        internal enum Fault { None, Format, Extent, Inputs, Map, Contacts, Capacity, Shader }

        internal readonly struct Step
        {
            internal readonly int Count;
            internal readonly float Seconds, DroppedSeconds;
            internal Step(int count, float seconds, float dropped)
            { Count = count; Seconds = seconds; DroppedSeconds = dropped; }
        }

        internal static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        internal static bool Finite(Vector2 x) => Finite(x.x) && Finite(x.y);
        internal static bool Finite(Vector3 x) => Finite(x.x) && Finite(x.y) && Finite(x.z);
        // Invalid nonzero controls must reach the fail-closed path, not silently turn the effect off.
        internal static bool Requested(Vector3 strengths) => !Finite(strengths) || strengths.x > 0 || strengths.y > 0 || strengths.z > 0;
        internal static Vector3 ReadStrengths(Material material) => material == null ? Vector3.zero : new Vector3(
            material.HasProperty(StrainId) ? material.GetFloat(StrainId) : 0,
            material.HasProperty(CurlId) ? material.GetFloat(CurlId) : 0,
            material.HasProperty(CollectionId) ? material.GetFloat(CollectionId) : 0);

        internal static Fault Availability(RenderTextureFormat format, float extent, bool auxiliaryFormats)
        {
            if (format != RenderTextureFormat.RG32 || !auxiliaryFormats) return Fault.Format;
            if (!Finite(extent) || extent > MaxExtentMeters || extent < FoamBuffer.MinExtentMeters) return Fault.Extent;
            return Fault.None;
        }

        internal static Route Classify(Vector3 strengths, bool available, bool valid, bool uniform, int occupied)
        {
            if (!Requested(strengths)) return Route.Legacy;
            if (!available) return Route.Unavailable;
            if (!valid || !Finite(strengths)) return Route.Hold;
            return uniform && occupied == 0 ? Route.Uniform : Route.Local;
        }

        internal static Vector2Int DriftCells(Route route, ref Vector2 remainder,
                                             Vector2 legacy, Vector2 uniform, float dt)
        {
            if (route == Route.Local || route == Route.Hold) return Vector2Int.zero;
            return FoamBuffer.AdvectCells(ref remainder, (route == Route.Uniform ? uniform : legacy) * dt);
        }

        internal static Step Substeps(float velocityMaxComponent, float dt)
        {
            if (!Finite(dt) || !Finite(velocityMaxComponent) || dt <= 0 || velocityMaxComponent <= 0)
                return default;
            double permitted = MaxCourant * FoamBuffer.CellSize / (double)velocityMaxComponent;
            int count = (int)Math.Min(MaxSubsteps, Math.Max(1, Math.Ceiling(dt / permitted)));
            float used = (float)Math.Min(dt, count * permitted);
            return new Step(count, used / count, Math.Max(0, dt - used));
        }

        internal static int Parity(int absoluteCell) => absoluteCell & 1;
        internal static int FlowResolution(float extent) => Mathf.CeilToInt(extent / FlowCellMeters) + 3;
        internal static long GpuBytes(int resolution, int flowResolution) =>
            9L * resolution * resolution + 8L * flowResolution * flowResolution;
        internal static long CpuBytes(int resolution, int flowResolution) =>
            9L * resolution * resolution + 8L * flowResolution * flowResolution + 64L * MaxContacts;

        /// <summary>Canonical A=left/bottom, B=right/top. Equal integer transfer at both endpoints.</summary>
        internal static void Exchange(uint ma, uint fa, uint mb, uint fb, float courant,
                                      out uint a, out uint af, out uint b, out uint bf)
        {
            a = ma; af = fa; b = mb; bf = fb;
            if (!Finite(courant) || courant == 0) return;
            bool reverse = courant < 0;
            uint donor = reverse ? mb : ma, receiver = reverse ? ma : mb;
            uint donorFresh = reverse ? fb : fa, receiverFresh = reverse ? fa : fb;
            uint q = Math.Min((uint)Math.Floor(Math.Min(Math.Abs(courant), MaxCourant) * donor), Codes - receiver);
            if (q == 0) return;
            uint total = receiver + q;
            uint fresh = (receiver * receiverFresh + q * donorFresh + total / 2) / total;
            uint retained = donor - q;
            if (reverse) { a = total; af = fresh; b = retained; bf = retained == 0 ? 0 : donorFresh; }
            else { a = retained; af = retained == 0 ? 0 : donorFresh; b = total; bf = fresh; }
        }

        internal static Vector2 WaveVelocity(in PackedWaveField waves, Vector2 position, float scale, Vector3 strengths)
        {
            Vector2 slope = Vector2.zero;
            float norm = 0;
            for (int i = 0; i < waves.Count; i++)
            {
                Vector4 train = waves.Train(i);
                float weight = train.w * train.z * scale;
                double phase = (double)train.z * scale * ((double)train.x * position.x + (double)train.y * position.y)
                             + waves.Phase(i);
                phase -= Math.Floor(phase / (2 * Math.PI)) * (2 * Math.PI);
                slope += new Vector2(train.x, train.y) * (weight * (float)Math.Cos(phase));
                norm += Math.Abs(weight);
            }
            slope /= Math.Max(1, norm);
            return Math.Max(0, strengths.x) * slope + Math.Max(0, strengths.y) * new Vector2(-slope.y, slope.x);
        }

        internal static Vector2 CollectionVelocity(Vector2 position, Vector2 drift, float strength,
                                                   FoamTransportContacts.Shape[] contacts, int count)
        {
            if (strength <= 0 || drift.sqrMagnitude == 0) return Vector2.zero;
            Vector2 e = drift.normalized, side = new Vector2(-e.y, e.x);
            float best = 0, winnerAcross = 0, winnerRadius = 1;
            int winner = -1;
            for (int i = 0; i < count; i++)
            {
                float r = Math.Max(FlowCellMeters, contacts[i].Radius);
                Vector2 d = position - contacts[i].Center;
                float down = Vector2.Dot(d, e), across = Vector2.Dot(d, side);
                float lee = Math.Max(0, 1 - Math.Abs(down - 2*r) / (2*r)) * Math.Max(0, 1 - Math.Abs(across) / (2*r));
                if (lee > best || (lee > 0 && lee == best && winner >= 0 &&
                                  FoamTransportContacts.Compare(contacts[i], contacts[winner]) < 0))
                { best = lee; winner = i; winnerAcross = across; winnerRadius = r; }
            }
            return -strength * best * (e + winnerAcross / (2*winnerRadius) * side) * .5f;
        }
    }
}
