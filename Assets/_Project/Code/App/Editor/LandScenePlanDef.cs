using System;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    [Serializable]
    public sealed class LandDoor
    {
        public string Id;
        public RegionDef Target;
        public string TargetArrival;
        public string OwnArrival;
        public Vector2 Centre;
        public Vector2 Size;
        public Vector2 Arrival;
        public Vector2 Disembark;
        public bool OnFootOnly;
        /// <summary>Placed and wired, with its band off: nothing crosses here, and the closed edge just beyond
        /// the band stops whoever walks on. A door waits shut until the far region's half is placed, and both
        /// open together (Rice Point's Route 91 door waits on NR1b).</summary>
        public bool Shut;
    }

    /// <summary>Authoring data in ground metres. No region's dimensions live in the template.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Land Scene Plan")]
    public sealed class LandScenePlanDef : ScriptableObject
    {
        public string Id;
        public RegionDef Region;
        public PaintedHeightMap Ground;
        public TextAsset Source;
        public Texture2D[] SurfacePaint;
        public LandDoor[] Doors;
        public Vector2 PlayerStart;
        public Vector2 BoatStart;
        public float BoatHeading;
        public BoatHullDef DevHull;
        public FishSpeciesDef[] DevFish;
        public Color Backdrop;
        public float EdgeThickness;
        public float MaxShoreGradient;
        public float PaintFloor;
        public float[] HeightBands;

        public Vector2 WorldSize => Region.WorldSizeMeters;
        // ADR 0042: height fields, travel bands and independent sitings use unsquashed ground metres.
        // The package's older art-projection note applies only inside a baked piece, not this frame.
        public static Vector2 Project(Vector2 metres) => metres;
        public string ScenePath => "Assets/_Project/Scenes/" + Region.SceneName + ".unity";

        public void Validate()
        {
            if (Region == null || Ground == null || Source == null || DevHull == null)
                throw new InvalidOperationException("Land plan needs region, ground, source and dev hull.");
            if (Region.WorldCenter != Vector2.zero || Region.WorldSizeMeters.x <= 0 || Region.WorldSizeMeters.y <= 0)
                throw new InvalidOperationException("Land regions build around zero with positive extents.");
            if (Ground.WorldCenter != Vector2.zero || Vector2.Distance(Ground.WorldSize, WorldSize) > 0.001f)
                throw new InvalidOperationException("Ground and region frame disagree.");
            var texels = Region.SeabedTexels;
            foreach (var texture in new[] { Ground.HeightTexture, Ground.StillLevelTexture })
                if (texture == null || texture.width != texels.x || texture.height != texels.y)
                    throw new InvalidOperationException("Ground raster and region resolution disagree.");
            if (SurfacePaint == null || SurfacePaint.Length != TerrainSplatBrush.TextureCount || Array.Exists(SurfacePaint, p => p == null))
                throw new InvalidOperationException("Land plan needs every surface paint channel.");
            if (Array.Exists(SurfacePaint, p => p.width != texels.x || p.height != texels.y))
                throw new InvalidOperationException("Paint and ground resolutions disagree.");
            if (HeightBands == null || HeightBands.Length != 5 || EdgeThickness <= 0)
                throw new InvalidOperationException("Land plan needs height bands and closed edges.");
            var keys = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var door in Doors ?? Array.Empty<LandDoor>())
                if (string.IsNullOrEmpty(door.Id) || string.IsNullOrEmpty(door.TargetArrival) ||
                    string.IsNullOrEmpty(door.OwnArrival) || !keys.Add(door.OwnArrival) ||
                    door.Size.x <= 0 || door.Size.y <= 0)
                    throw new InvalidOperationException("Invalid land door or duplicate arrival.");
        }
    }
}
