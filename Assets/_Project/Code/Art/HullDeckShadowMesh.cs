using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>Configuration-time receiver and stackless triangle tree, all in the borrowed rig frame.</summary>
    public sealed class HullDeckShadowMesh : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Triangle { public Vector4 A, B, C, N; }
        [StructLayout(LayoutKind.Sequential)]
        public struct Node { public Vector4 Low, High; }
        public readonly Mesh Receiver;
        public readonly Triangle[] Triangles;
        public readonly Node[] Nodes;
        public readonly float DeckHeight, DeckLevel;
        public readonly int HouseTriangles;
        public readonly ComputeBuffer TriangleBuffer, NodeBuffer;

        public HullDeckShadowMesh(HullSunShadowState state)
        {
            DeckHeight=state.DeckHeight;
            var triangles=new List<Triangle>();
            Append(state.Hull,0,triangles);Append(state.ClosedLeaf,1,triangles);Append(state.OpenLeaf,2,triangles);
            DeckLevel=float.MaxValue;
            foreach(var t in triangles)
                if(IsPlane(t,DeckHeight))DeckLevel=Mathf.Min(DeckLevel,t.B.w);
            if(DeckHeight<=0||DeckLevel==float.MaxValue){Triangles=Array.Empty<Triangle>();Nodes=Array.Empty<Node>();return;}
            foreach(var t in triangles)if(t.B.w>DeckLevel&&Mathf.Max(t.A.z,Mathf.Max(t.B.z,t.C.z))>DeckHeight)HouseTriangles++;
            if(HouseTriangles==0){Triangles=Array.Empty<Triangle>();Nodes=Array.Empty<Node>();return;}
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var tags=new List<Vector4>();var indices=new List<int>();
            foreach(var t in triangles)
            {
                if(!IsPlane(t,DeckHeight)||t.B.w!=DeckLevel)continue;
                int start=vertices.Count;
                vertices.Add(t.A);vertices.Add(t.B);vertices.Add(t.C);
                for(int i=0;i<3;i++){normals.Add(t.N);tags.Add(new Vector4(t.B.w,t.C.w,t.N.w,0));indices.Add(start+i);}
            }
            Receiver=new Mesh{name="HullDeckReceiver",hideFlags=HideFlags.HideAndDontSave,indexFormat=IndexFormat.UInt32};
            Receiver.SetVertices(vertices);Receiver.SetNormals(normals);Receiver.SetUVs(0,tags);Receiver.SetTriangles(indices,0);Receiver.RecalculateBounds();
            var nodes=new List<Node>();Build(triangles,0,triangles.Count,nodes);
            Triangles=triangles.ToArray();Nodes=nodes.ToArray();
            TriangleBuffer=new ComputeBuffer(Triangles.Length,64);TriangleBuffer.SetData(Triangles);
            NodeBuffer=new ComputeBuffer(Nodes.Length,32);NodeBuffer.SetData(Nodes);
        }

        static bool IsPlane(Triangle t,float height) => t.N.z>0.9f&&Mathf.Abs(t.A.z-height)<1e-5f&&Mathf.Abs(t.B.z-height)<1e-5f&&Mathf.Abs(t.C.z-height)<1e-5f;
        static void Append(Mesh mesh,int part,List<Triangle> into)
        {
            if(mesh==null)return;
            var points=mesh.vertices;var normals=mesh.normals;var levels=new List<Vector4>();mesh.GetUVs(1,levels);
            var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3)
            {
                int a=indices[i],b=indices[i+1],c=indices[i+2];
                Vector4 tag=a<levels.Count?levels[a]:Vector4.zero;
                Vector3 n=a<normals.Length?normals[a]:Vector3.Cross(points[b]-points[a],points[c]-points[a]).normalized;
                into.Add(new Triangle{A=new Vector4(points[a].x,points[a].y,points[a].z,part),
                    B=new Vector4(points[b].x,points[b].y,points[b].z,tag.x),C=new Vector4(points[c].x,points[c].y,points[c].z,tag.y),N=new Vector4(n.x,n.y,n.z,tag.z)});
            }
        }
        static int Build(List<Triangle> tris,int start,int count,List<Node> nodes)
        {
            int at=nodes.Count;nodes.Add(default);
            var bounds=new Bounds(tris[start].A,Vector3.zero);
            for(int i=start;i<start+count;i++){bounds.Encapsulate((Vector3)tris[i].A);bounds.Encapsulate((Vector3)tris[i].B);bounds.Encapsulate((Vector3)tris[i].C);}
            if(count>1)
            {
                Vector3 size=bounds.size;int axis=size.x>=size.y&&size.x>=size.z?0:size.y>=size.z?1:2;
                tris.Sort(start,count,Comparer<Triangle>.Create((a,b)=>((a.A[axis]+a.B[axis]+a.C[axis])/3).CompareTo((b.A[axis]+b.B[axis]+b.C[axis])/3)));
                int half=count/2;Build(tris,start,half,nodes);Build(tris,start+half,count-half,nodes);
            }
            Vector3 low=bounds.min,high=bounds.max;
            nodes[at]=new Node{Low=new Vector4(low.x,low.y,low.z,nodes.Count),High=new Vector4(high.x,high.y,high.z,count==1?start:-1)};
            return at;
        }
        public void Dispose()
        {
            TriangleBuffer?.Dispose();NodeBuffer?.Dispose();
            if(Receiver!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(Receiver);else UnityEngine.Object.DestroyImmediate(Receiver);}
        }
    }
}
