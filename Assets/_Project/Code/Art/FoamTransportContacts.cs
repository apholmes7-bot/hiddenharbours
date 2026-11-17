using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    internal static class FoamTransportContacts
    {
        // The explicit stride is part of the approved CPU budget. No strings or object references per record.
        [StructLayout(LayoutKind.Sequential, Size = 64)]
        internal readonly struct Shape
        {
            internal readonly Vector2 Center, Axis, HalfSize;
            internal readonly float MinLevel, MaxLevel;
            internal readonly int Circle;
            // A HULL's waterline, as her picture draws it (zero = a plain box or circle). Her picture squashes
            // north-south metres by ForeshortenY, and her waterline narrows over the forward BowTaper of
            // her length to StemHalf of her half-beam at the stem.
            internal readonly float ForeshortenY, BowTaper, StemHalf;
            internal readonly ulong Key;
            internal Shape(Vector2 center, Vector2 axis, Vector2 half, float min, float max, bool circle, ulong key)
                : this(center, axis, half, min, max, circle, 0f, 0f, 0f, key) { }
            private Shape(Vector2 center, Vector2 axis, Vector2 half, float min, float max, bool circle,
                          float foreshortenY, float bowTaper, float stemHalf, ulong key)
            {
                Center=center; Axis=axis; HalfSize=half; MinLevel=min; MaxLevel=max; Circle=circle?1:0;
                ForeshortenY=foreshortenY; BowTaper=bowTaper; StemHalf=stemHalf; Key=key;
            }

            /// <summary>A hull's waterline: <paramref name="center"/> where her picture's centre lies,
            /// <paramref name="bow"/> her bow in PLAN (unit), her plan half-length and half-beam, how her
            /// picture foreshortens north-south (0..1], and her bow's taper (share of her length from the
            /// stem, and half-width at the stem in half-beams, both 0..1).</summary>
            internal static Shape Hull(Vector2 center, Vector2 bow, float halfLength, float halfBeam,
                                       float foreshortenY, float bowTaper, float stemHalf, ulong key) =>
                new Shape(center, bow, new Vector2(halfLength, halfBeam), -float.MaxValue, float.MaxValue, false,
                          foreshortenY, bowTaper, stemHalf, key);

            internal bool IsHull => ForeshortenY > 0;
            internal bool Valid => FoamTransport.Finite(Center) && FoamTransport.Finite(Axis) &&
                FoamTransport.Finite(HalfSize) && HalfSize.x > 0 && HalfSize.y > 0 &&
                FoamTransport.Finite(MinLevel) && FoamTransport.Finite(MaxLevel) && MinLevel <= MaxLevel &&
                Mathf.Abs(Axis.sqrMagnitude-1) <= 1e-4f &&
                FoamTransport.Finite(ForeshortenY) && ForeshortenY >= 0 && ForeshortenY <= 1 &&
                FoamTransport.Finite(BowTaper) && BowTaper >= 0 && BowTaper <= 1 &&
                FoamTransport.Finite(StemHalf) && StemHalf >= 0 && StemHalf <= 1;
            internal float Radius => Circle != 0 ? HalfSize.x : HalfSize.magnitude;
            internal Vector2 AabbHalf => Circle != 0 ? Vector2.one*HalfSize.x : new Vector2(
                Mathf.Abs(Axis.x)*HalfSize.x+Mathf.Abs(Axis.y)*HalfSize.y,
                (Mathf.Abs(Axis.y)*HalfSize.x+Mathf.Abs(Axis.x)*HalfSize.y)*(IsHull?ForeshortenY:1f));
            internal bool Wet(float seaLevel) => seaLevel >= MinLevel && seaLevel <= MaxLevel;

            /// <summary>Half her waterline's width <paramref name="along"/> metres toward the bow from her
            /// centre (a hull only): her half-beam aft of the taper, narrowing straight to the stem.</summary>
            internal float HalfWidthAt(float along)
            {
                float taperFrom=HalfSize.x-2*HalfSize.x*BowTaper;
                if(along<=taperFrom || BowTaper<=0) return HalfSize.y;
                float t=Mathf.Clamp01((along-taperFrom)/(HalfSize.x-taperFrom));
                return HalfSize.y*(1-t*(1-StemHalf));
            }

            /// <summary>Whether a DRAWN point lies on her waterline (a hull only): un-squash it to plan
            /// metres, then test it against her tapered outline.</summary>
            internal bool HullContains(Vector2 drawn)
            {
                Vector2 d=drawn-Center; d.y/=ForeshortenY;
                float along=Vector2.Dot(d,Axis), across=Vector2.Dot(d,new Vector2(-Axis.y,Axis.x));
                return Math.Abs(along)<=HalfSize.x && Math.Abs(across)<=HalfWidthAt(along);
            }

            internal bool TouchesCell(Vector2 center, float cellHalf)
            {
                Vector2 d = center-Center;
                if (Circle != 0)
                {
                    float x=Math.Max(0,Math.Abs(d.x)-cellHalf), y=Math.Max(0,Math.Abs(d.y)-cellHalf);
                    return x*x+y*y <= HalfSize.x*HalfSize.x;
                }
                Vector2 h=AabbHalf;
                if (Math.Abs(d.x)>h.x+cellHalf || Math.Abs(d.y)>h.y+cellHalf) return false;
                // A hull is solid only where a cell's CENTRE lies on her waterline, never a cell she merely
                // grazes: foam held in a solid cell stands still, so any solid cell outside her picture
                // shows as a hard-edged slab of foam (the owner, 2026-11-16: "the bow foam looks
                // irregular its sqaure"). Under her picture a solid cell's foam is hidden by her hull.
                if (IsHull) return HullContains(center);
                float pad=cellHalf*(Math.Abs(Axis.x)+Math.Abs(Axis.y));
                return Math.Abs(Vector2.Dot(d,Axis))<=HalfSize.x+pad &&
                       Math.Abs(Vector2.Dot(d,new Vector2(-Axis.y,Axis.x)))<=HalfSize.y+pad;
            }
        }

        private static readonly List<FoamTransportContact> Live = new List<FoamTransportContact>();
        internal static void Register(FoamTransportContact contact)
        { if (contact != null && !Live.Contains(contact)) Live.Add(contact); }
        internal static void Unregister(FoamTransportContact contact) => Live.Remove(contact);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Live.Clear();

        internal static ulong StableKey(string id)
        {
            unchecked
            {
                ulong key=14695981039346656037UL;
                for (int i=0;i<id.Length;i++) key=(key^id[i])*1099511628211UL;
                return key;
            }
        }

        // Geometry breaks key/hash collisions, so registration order cannot change a velocity.
        internal static int Compare(in Shape a, in Shape b)
        {
            int c=a.Key.CompareTo(b.Key); if(c!=0) return c;
            c=a.Center.x.CompareTo(b.Center.x); if(c!=0) return c;
            c=a.Center.y.CompareTo(b.Center.y); if(c!=0) return c;
            return a.Radius.CompareTo(b.Radius);
        }

        internal static bool Capture(Shape[] into, Rect window, float level, out int count, out FoamTransport.Fault fault)
        {
            count=0; fault=FoamTransport.Fault.None;
            for(int i=0;i<Live.Count;i++)
            {
                if(Live[i]==null) continue;
                if(!Live[i].TrySnapshot(out Shape shape)) { fault=FoamTransport.Fault.Contacts; return false; }
                if(!shape.Wet(level) || !Relevant(shape,window)) continue;
                if(count==into.Length) { count++; fault=FoamTransport.Fault.Capacity; return false; }
                into[count++]=shape;
            }
            var hulls=HullPresences.Active;
            HullFoamSettings settings=GameServices.HullFoam;
            for(int i=0;i<hulls.Count;i++)
            {
                HullFootprint hull=hulls[i].Footprint;
                var shape=HullShape(hull,hull.Center,hulls[i].PictureForeshortenY,in settings);
                if(!shape.Valid) { fault=FoamTransport.Fault.Contacts; return false; }
                if(!Relevant(shape,window)) continue;
                if(count==into.Length) { count++; fault=FoamTransport.Fault.Capacity; return false; }
                into[count++]=shape;
            }
            // A bounded insertion sort uses the fixed snapshot only; no comparer/delegate allocation.
            for(int i=1;i<count;i++)
            {
                Shape value=into[i]; int j=i-1;
                while(j>=0 && Compare(value,into[j])<0) { into[j+1]=into[j]; j--; }
                into[j+1]=value;
            }
            return true;
        }

        /// <summary>
        /// A hull's foam contact: her waterline as her PICTURE draws it, centred at
        /// <paramref name="center"/>. Her plan outline is a box; her picture squashes it north-south by her
        /// art's foreshortening, and her waterline comes to a point at the stem. A plan box stands past her
        /// drawn stem whenever she heads up or down the screen and square across her bow at any heading,
        /// and the foam the field holds still inside it showed as a pale square slab ahead of her. A
        /// foreshortening outside (0, 1] is read as plan view.
        /// </summary>
        internal static Shape HullShape(in HullFootprint hull, Vector2 center, float foreshortenY,
                                        in HullFoamSettings settings)
        {
            HullFoamSettings d=HullFoamSettings.Default;
            float squash=FoamTransport.Finite(foreshortenY) && foreshortenY>0 && foreshortenY<=1 ? foreshortenY : 1f;
            float taper=Mathf.Clamp01(settings.ContactBowTaper>0 ? settings.ContactBowTaper : d.ContactBowTaper);
            float stem=Mathf.Clamp01(settings.ContactStemHalfBeams>0 ? settings.ContactStemHalfBeams : d.ContactStemHalfBeams);
            return Shape.Hull(center,hull.BowDirection,hull.HalfLength,hull.HalfBeam,squash,taper,stem,0);
        }

        private static bool Relevant(in Shape shape, Rect window)
        {
            // Include the entire downstream collection support and velocity halo, not just visible solids.
            float reach=4*Math.Max(FoamTransport.FlowCellMeters,shape.Radius)+FoamTransport.FlowCellMeters;
            return shape.Center.x+reach>=window.xMin && shape.Center.x-reach<=window.xMax &&
                   shape.Center.y+reach>=window.yMin && shape.Center.y-reach<=window.yMax;
        }
    }
}
