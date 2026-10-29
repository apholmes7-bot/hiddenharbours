using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    static class VillageTestGeometry
    {
        public static Vector2[] House(StPetersVillage.Site site)
        {
            var shell = VillageBuildingCatalog.Find(site.Key);
            if (!shell.IsValid) throw new InvalidOperationException("Missing shell: " + site.Key);
            return Hull(site.Position,shell.Entry.footprintWidthMetres,shell.Entry.footprintLengthMetres,
                StPetersVillage.FacingFor(shell,site),shell.Entry.facings);
        }
        public static Vector2[] Shop(StPetersShops.Site site)
        {
            var shell = ShopCatalog.FindShell(site.Key);
            if (!shell.IsValid) throw new InvalidOperationException("Missing shop: " + site.Key);
            return Hull(site.Position,shell.FootprintMetres.x,shell.FootprintMetres.y,
                StPetersShops.FacingFor(shell,site),shell.Entry.facings);
        }
        static Vector2[] Hull(Vector2 centre,float width,float length,int facing,int facings)
        {
            var floor = new InteriorFootprint(centre,width,length,facing,facings,SpriteLightMath.GroundDepthScale);
            return new[] { new Vector2(-width*.5f,-length*.5f),new Vector2(width*.5f,-length*.5f),
                new Vector2(width*.5f,length*.5f),new Vector2(-width*.5f,length*.5f) }.Select(floor.ModelToWorld).ToArray();
        }
        public static float ToHull(Vector2 p,Vector2[] hull)
        {
            if (VillageGeometry.Contains(hull,p)) return 0;
            return Enumerable.Range(0,hull.Length).Min(i=>VillageGeometry.PointSegment(p,hull[i],hull[(i+1)%hull.Length]));
        }
        public static GroundPolygon[] FullRigs()
        {
            var data = KeySceneImport.Json.Parse(File.ReadAllText(StPetersVillageStations.DataPath),"station geometry");
            var sites = StPetersVillage.Sites.ToDictionary(s=>s.Key,s=>s.Position);
            foreach (var shop in StPetersShops.Sites) sites.Add(shop.Key,shop.Position);
            return data.Get("fullBounds").Items.Select(row=>
            {
                string[] words = row.Get("lot").Text.Split('_');
                string key = words[0]+string.Concat(words.Skip(1).Select(w=>char.ToUpperInvariant(w[0])+w.Substring(1)));
                Vector2 at = sites[key];
                var b = row.Get("local").Items.Select(v=>v.Float(key)).ToArray();
                return new GroundPolygon(VillageGeometry.Rectangle(new Vector4(b[0]+at.x,b[1]+at.y,b[2]+at.x,b[3]+at.y)));
            }).ToArray();
        }
        public static Vector2[] Trunks()
        {
            var scene = SceneYaml.Parse(File.ReadAllText(ScenePath));
            return scene.Docs.Where(d=>d.IsScript(typeof(TreeTrunkAnchor).FullName)).Select(d=>
            {
                var tr = scene.TransformOf(scene.Get(d.FieldRef("m_GameObject")));
                Vector2 at = ParseVec3(tr.Field("m_LocalPosition"));
                long parent = tr.FieldRef("m_Father");
                while (parent != 0)
                {
                    tr = scene.Get(parent);
                    if (tr.Field("m_LocalRotation") != "{x: 0, y: 0, z: 0, w: 1}" ||
                        ParseVec3(tr.Field("m_LocalScale")) != Vector3.one)
                        throw new InvalidOperationException("Tree parent has a non-identity frame.");
                    at += (Vector2)ParseVec3(tr.Field("m_LocalPosition")); parent = tr.FieldRef("m_Father");
                }
                return at;
            }).ToArray();
        }
    }
}
