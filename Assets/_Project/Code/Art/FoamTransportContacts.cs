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
            internal readonly ulong Key;
            internal Shape(Vector2 center, Vector2 axis, Vector2 half, float min, float max, bool circle, ulong key)
            { Center=center; Axis=axis; HalfSize=half; MinLevel=min; MaxLevel=max; Circle=circle?1:0; Key=key; }
            internal bool Valid => FoamTransport.Finite(Center) && FoamTransport.Finite(Axis) &&
                FoamTransport.Finite(HalfSize) && HalfSize.x > 0 && HalfSize.y > 0 &&
                FoamTransport.Finite(MinLevel) && FoamTransport.Finite(MaxLevel) && MinLevel <= MaxLevel &&
                Mathf.Abs(Axis.sqrMagnitude-1) <= 1e-4f;
            internal float Radius => Circle != 0 ? HalfSize.x : HalfSize.magnitude;
            internal Vector2 AabbHalf => Circle != 0 ? Vector2.one*HalfSize.x : new Vector2(
                Mathf.Abs(Axis.x)*HalfSize.x+Mathf.Abs(Axis.y)*HalfSize.y,
                Mathf.Abs(Axis.y)*HalfSize.x+Mathf.Abs(Axis.x)*HalfSize.y);
            internal bool Wet(float seaLevel) => seaLevel >= MinLevel && seaLevel <= MaxLevel;
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
            for(int i=0;i<hulls.Count;i++)
            {
                HullFootprint hull=hulls[i].Footprint;
                var shape=new Shape(hull.Center,hull.BowDirection,new Vector2(hull.HalfLength,hull.HalfBeam),
                    -float.MaxValue,float.MaxValue,false,0);
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

        private static bool Relevant(in Shape shape, Rect window)
        {
            // Include the entire downstream collection support and velocity halo, not just visible solids.
            float reach=4*Math.Max(FoamTransport.FlowCellMeters,shape.Radius)+FoamTransport.FlowCellMeters;
            return shape.Center.x+reach>=window.xMin && shape.Center.x-reach<=window.xMax &&
                   shape.Center.y+reach>=window.yMin && shape.Center.y-reach<=window.yMax;
        }
    }
}
