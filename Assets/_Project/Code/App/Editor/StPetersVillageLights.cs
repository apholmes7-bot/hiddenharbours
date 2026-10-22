#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static class StPetersVillageLights
    {
        public const string RootName = "StPetersVillageLights";
        public const float HouseGlassHeight = 2.462f;
        public const float StoreGlassHeight = 2.7f;
        public const float PostOfficeGlassHeight = 2.75f;

        public static bool IsLit(string id) => id != "light.st_peters.fire_pit_square" &&
            id != "light.st_peters.battery_shed_light_sage_cottage";

        public static LightPresets.Kind Preset(string value)
        {
            if (!Enum.TryParse(value, false, out LightPresets.Kind kind) || !Enum.IsDefined(typeof(LightPresets.Kind), kind))
                throw new InvalidOperationException("Unknown village light preset: " + value);
            return kind;
        }

        public static float FittingHeight(string id, string piece) => piece == LampPosts.LanternPost
            ? LampPosts.LanternPostFittingHeightMetres
            : id == "light.st_peters.doorstep_lantern_general_store" ? StoreGlassHeight
            : id == "light.st_peters.doorstep_lantern_post_office" ? PostOfficeGlassHeight
            : HouseGlassHeight;

        public static Vector2 PostSpriteOffset => new Vector2(0,-.31f * IsoGround.GroundDepthScale);
        public static float BloomLift(string id,string piece) => FittingHeight(id,piece) * IsoGround.HeightScale;

        public static void Configure(GameObject go, LightPostDef def)
        {
            if (!IsLit(def.Id)) return;
            if (float.IsNaN(def.Reach) || float.IsInfinity(def.Reach) || def.Reach <= 0f)
                throw new InvalidOperationException("Invalid village light reach: " + def.Id);
            LightPresets.Kind kind = Preset(def.Preset);
            bool post = def.KitPiece == LampPosts.LanternPost;
            float width = post ? LampPosts.LanternPostFittingWidthMetres : 0f;
            var light = go.AddComponent<SceneLight>();
            LightPresets.ApplyFitting(light, kind, width);
            light.ReachMetres = def.Reach;
            light.CastsShadows = def.CastsShadow;
            light.LampHeightMeters = FittingHeight(def.Id, def.KitPiece);
            light.BloomLiftMetres = BloomLift(def.Id, def.KitPiece);
            var preset = go.AddComponent<PreconfiguredLight>();
            preset.Preset = kind;
            preset.FittingWidthMetres = width;
            preset.ReachOverrideMetres = def.Reach;
        }

        public static GameObject Place(IReadOnlyList<LightPostDef> defs)
        {
            var root = new GameObject(RootName);
            foreach (var def in defs)
            {
                var go = new GameObject(def.Id);
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(def.Position.x, def.Position.y, 0f);
                if (def.KitPiece == LampPosts.LanternPost)
                {
                    int facing = IsoPackSprites.FacingForHeading("wharfDecor", 180f);
                    Sprite sprite = IsoPackSprites.Facing("wharfDecor", LampPosts.LanternPost, facing);
                    if (sprite == null) throw new InvalidOperationException("Missing village lantern post sheet.");
                    var post = new GameObject("Post");
                    post.transform.SetParent(go.transform,false);
                    post.transform.localPosition = PostSpriteOffset;
                    post.AddComponent<SpriteRenderer>().sprite = sprite;
                    post.AddComponent<YSortSprite>();
                    post.AddComponent<SpriteShadow>().CastsGroundContact = false;
                }
                Configure(go, def);
            }
            return root;
        }
    }
}
#endif
