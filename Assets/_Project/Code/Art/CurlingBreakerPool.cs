using System;
using HiddenHarbours.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The curl's fixed pool: <see cref="Capacity"/> slots, made once, never grown. A site that finds
    /// no free slot yields (content validation forbids more enabled sites per region than the pool
    /// holds). Each slot owns one sheet mesh and one mask mesh, written in place from fixed arrays.
    /// </summary>
    public sealed class CurlingBreakerPool : IDisposable
    {
        public const int Capacity = CurlingBreakerSettings.PilotCapacity;

        private readonly Slot[] _slots = new Slot[Capacity];

        public CurlingBreakerPool()
        {
            for (int i = 0; i < Capacity; i++) _slots[i] = new Slot();
        }

        public Slot this[int index] => _slots[index];

        /// <summary>Slots claimed by a site and holding a live pose.</summary>
        public int LiveCount
        {
            get
            {
                int live = 0;
                for (int i = 0; i < Capacity; i++)
                    if (_slots[i].Live) live++;
                return live;
            }
        }

        /// <summary>Slots claimed by a site, live or not.</summary>
        public int ClaimedCount
        {
            get
            {
                int claimed = 0;
                for (int i = 0; i < Capacity; i++)
                    if (_slots[i].Site != null) claimed++;
                return claimed;
            }
        }

        /// <summary>The slot <paramref name="site"/> holds, else a free one claimed for it, else null.</summary>
        public Slot Claim(CurlingBreakerSiteDef site)
        {
            if (site == null) return null;
            for (int i = 0; i < Capacity; i++)
                if (_slots[i].Site == site) return _slots[i];
            for (int i = 0; i < Capacity; i++)
            {
                if (_slots[i].Site != null) continue;
                _slots[i].Site = site;
                return _slots[i];
            }
            return null;
        }

        public void Release(CurlingBreakerSiteDef site)
        {
            if (site == null) return;
            for (int i = 0; i < Capacity; i++)
                if (_slots[i].Site == site) _slots[i].Free();
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < Capacity; i++) _slots[i].Free();
        }

        public void Dispose()
        {
            for (int i = 0; i < Capacity; i++) _slots[i].DestroyMeshes();
        }

        public sealed class Slot
        {
            public const int MaxSheetVertices = CurlingBreakerPose.MaxColumns * CurlingBreakerSampler.MaxRows;
            public const int MaxMaskVertices = CurlingBreakerPose.MaxColumns * 2;

            public readonly CurlingBreakerPose Pose = new CurlingBreakerPose();
            public readonly Vector3[] SheetVertices = new Vector3[MaxSheetVertices];
            public readonly Vector4[] SheetUvs = new Vector4[MaxSheetVertices];
            public readonly Vector3[] MaskVertices = new Vector3[MaxMaskVertices];
            public readonly Vector4[] MaskUvs = new Vector4[MaxMaskVertices];

            private readonly int[] _sheetTriangles =
                new int[(CurlingBreakerPose.MaxColumns - 1) * (CurlingBreakerSampler.MaxRows - 1) * 6];
            private readonly int[] _maskTriangles = new int[(CurlingBreakerPose.MaxColumns - 1) * 6];
            private int _columns;
            private int _rows;

            public CurlingBreakerSiteDef Site { get; internal set; }
            public Mesh Sheet { get; private set; }
            public Mesh Mask { get; private set; }
            public bool Live => Site != null && Pose.Live;

            /// <summary>Writes the filled arrays into the meshes. The topology (and the meshes
            /// themselves) are made only on first use and when the column/row tunables change; a
            /// steady frame rewrites vertices and uvs in place.</summary>
            public void Apply(int columns, int rows, Bounds sheetBounds, Bounds maskBounds)
            {
                if (Sheet == null) Sheet = MakeMesh("CurlingBreakerSheet");
                if (Mask == null) Mask = MakeMesh("CurlingBreakerMask");
                int sheetCount = columns * rows;
                int maskCount = columns * 2;
                if (columns != _columns || rows != _rows)
                {
                    int sheetIndices = BuildGrid(_sheetTriangles, columns, rows);
                    int maskIndices = BuildGrid(_maskTriangles, columns, 2);
                    Sheet.Clear();
                    Mask.Clear();
                    Sheet.SetVertices(SheetVertices, 0, sheetCount);
                    Sheet.SetUVs(0, SheetUvs, 0, sheetCount);
                    Sheet.SetTriangles(_sheetTriangles, 0, sheetIndices, 0, false);
                    Mask.SetVertices(MaskVertices, 0, maskCount);
                    Mask.SetUVs(0, MaskUvs, 0, maskCount);
                    Mask.SetTriangles(_maskTriangles, 0, maskIndices, 0, false);
                    _columns = columns;
                    _rows = rows;
                }
                else
                {
                    Sheet.SetVertices(SheetVertices, 0, sheetCount);
                    Sheet.SetUVs(0, SheetUvs, 0, sheetCount);
                    Mask.SetVertices(MaskVertices, 0, maskCount);
                    Mask.SetUVs(0, MaskUvs, 0, maskCount);
                }
                Sheet.bounds = sheetBounds;
                Mask.bounds = maskBounds;
            }

            internal void Free()
            {
                Site = null;
                Pose.Clear();
            }

            internal void DestroyMeshes()
            {
                Free();
                DestroyMesh(Sheet);
                DestroyMesh(Mask);
                Sheet = null;
                Mask = null;
                _columns = _rows = 0;
            }

            /// <summary>Column-major grid, <paramref name="rows"/> vertices per column. Wound so the
            /// side facing along <c>(−shoreward.y, shoreward.x) × shoreward</c> is the front face.</summary>
            private static int BuildGrid(int[] triangles, int columns, int rows)
            {
                int n = 0;
                for (int c = 0; c < columns - 1; c++)
                for (int r = 0; r < rows - 1; r++)
                {
                    int v00 = c * rows + r;
                    int v10 = v00 + rows;
                    int v01 = v00 + 1;
                    int v11 = v10 + 1;
                    triangles[n++] = v00;
                    triangles[n++] = v10;
                    triangles[n++] = v01;
                    triangles[n++] = v10;
                    triangles[n++] = v11;
                    triangles[n++] = v01;
                }
                return n;
            }

            private static Mesh MakeMesh(string name)
            {
                var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
                mesh.MarkDynamic();
                return mesh;
            }

            private static void DestroyMesh(Mesh mesh)
            {
                if (mesh == null) return;
                if (Application.isPlaying) Object.Destroy(mesh);
                else Object.DestroyImmediate(mesh);
            }
        }
    }
}
