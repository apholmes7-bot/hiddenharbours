#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using HiddenHarbours.World;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.App.Editor
{
    public static partial class NineMileCreekWharfLandSide
    {
        static float WaterCoordinate(string yaml, string axis) => float.Parse(
            Regex.Match(yaml, @"(?:\{|, )" + axis + @": ([^,}]+)").Groups[1].Value, CultureInfo.InvariantCulture);
        static Vector2 WaterLocalPosition(Doc transform) => new Vector2(
            WaterCoordinate(transform.Field("m_LocalPosition"), "x"), WaterCoordinate(transform.Field("m_LocalPosition"), "y"));

        static LayerPatch OpenWaterGangwayHeads(SceneYaml scene)
        {
            Doc wharf=scene.RootNamed(NineMileCreekWharf.RootName);
            var mine=scene.SubtreeOf(wharf.FileId);
            Doc walls=scene.Docs.Single(d=>d.ClassId==1&&d.Field("m_Name")==NineMileCreekWalls.RootName&&mine.Contains(d.FileId));
            Doc colliderGo=scene.ChildNamed(scene.TransformOf(walls),PropertyBoundary.CollidersChildName);
            var patch=new LayerPatch(WaterStep,"NineMileCreekWharf/QuayWalls",walls.FileId);
            foreach(Doc collider in scene.ComponentsOf(colliderGo).Where(d=>d.ClassId==60))
            {
                string block=Regex.Match(collider.Text,@"(?ms)^  m_Points:.*?(?=^  m_UseDelaunayMesh:)").Value;
                var rectangles=new List<Rect>();
                foreach(Match group in Regex.Matches(block,@"(?m)^    - - [^\n]*(?:\n      - [^\n]*)*"))
                {
                    var points=Regex.Matches(group.Value,@"\{x: [^}]+\}").Cast<Match>().Select(m=>new Vector2(WaterCoordinate(m.Value,"x"),WaterCoordinate(m.Value,"y"))).ToArray();
                    if(points.Length!=4)throw new Refusal("H5 gangway opening: the inherited quay path is not a quadrilateral.");
                    var rect=Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
                    if(points.Any(p=>(Mathf.Abs(p.x-rect.xMin)>.001f&&Mathf.Abs(p.x-rect.xMax)>.001f)||(Mathf.Abs(p.y-rect.yMin)>.001f&&Mathf.Abs(p.y-rect.yMax)>.001f)))
                        throw new Refusal("H5 gangway opening: an inherited quay path is not axis-aligned.");
                    rectangles.Add(rect);
                }
                if(rectangles.Count==0)throw new Refusal("H5 gangway opening: the inherited quay has no paths.");
                var parts=WA(WM(NineMileCreekWharfWaterSide.ReadToday(),"parts"));
                bool changed=false;
                foreach(var part in parts.Where(p=>WM(p,"gangway")!=null&&WN(WM(WM(p,"gangway"),"step"))==0))
                {
                    var gang=WM(part,"gangway");Vector2 head=WP(WM(gang,"hinge"));float half=WN(WM(gang,"width"))*.5f;
                    var opening=Rect.MinMaxRect(head.x-half,head.y-1,head.x+half,head.y+1);
                    var next=new List<Rect>();
                    foreach(var rect in rectangles)
                    {
                        if(!rect.Overlaps(opening)){next.Add(rect);continue;}
                        changed=true;
                        float x0=Mathf.Max(rect.xMin,opening.xMin),x1=Mathf.Min(rect.xMax,opening.xMax),y0=Mathf.Max(rect.yMin,opening.yMin),y1=Mathf.Min(rect.yMax,opening.yMax);
                        void Keep(float left,float bottom,float right,float top){if(right-left>.001f&&top-bottom>.001f)next.Add(Rect.MinMaxRect(left,bottom,right,top));}
                        Keep(rect.xMin,rect.yMin,x0,rect.yMax);Keep(x1,rect.yMin,rect.xMax,rect.yMax);
                        Keep(x0,rect.yMin,x1,y0);Keep(x0,y1,x1,rect.yMax);
                    }
                    rectangles=next;
                }
                if(!changed)continue;
                var paths=rectangles.Select(r=>new[]{new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMax),new Vector2(r.xMin,r.yMax)}).ToList();
                string polygon=PolygonColliderYaml(collider.FileId,collider.FieldRef("m_GameObject"),paths);
                string replacement=Regex.Match(polygon,@"(?ms)^  m_Points:.*?(?=^  m_UseDelaunayMesh:)").Value;
                patch.Edit(collider,Regex.Replace(collider.Text,@"(?ms)^  m_Points:.*?(?=^  m_UseDelaunayMesh:)",_=>replacement),"QuayWalls / the four native gangway mouths; existing yard openings retained");
            }
            return patch.Seal(scene);
        }

        static string SetWaterPrefabPosition(string text,long target,string axis,float value)
        {
            var rx=new Regex(@"(?m)(    - target: \{fileID: "+target+@", guid: [a-f0-9]{32}, type: 3\}\n      propertyPath: m_LocalPosition\."+axis+@"\n      value: )[^\n]*");
            if(rx.Matches(text).Count!=1)throw new Refusal("H5 fuel pedestal has no unique prefab position override: "+axis);
            return rx.Replace(text,m=>m.Groups[1].Value+F(value));
        }
        static LayerPatch PlaceWaterFuel(SceneYaml scene)
        {
            Doc creek=scene.RootNamed(NineMileCreekStation.RootName);var creekIds=scene.SubtreeOf(creek.FileId);
            Doc root=scene.Docs.Single(d=>d.ClassId==1&&d.Field("m_Name")==NineMileCreekStation.WharfRootName&&creekIds.Contains(d.FileId));
            var mine=scene.SubtreeOf(root.FileId);var patch=new LayerPatch(WaterStep,"CreekFuel/WharfPumps",root.FileId);
            if(WaterLocalPosition(scene.TransformOf(root)).sqrMagnitude>1e-6f||WaterLocalPosition(scene.TransformOf(creek)).sqrMagnitude>1e-6f)
                throw new Refusal("H5 fuel placement requires the inherited zero-origin service roots.");
            var parts=WA(WM(NineMileCreekWharfWaterSide.ReadToday(),"parts"));
            var apron=parts.Single(p=>WS(WM(p,"key"))=="west_apron");var polygon=WA(WM(apron,"walkPolygon")).Select(WP).ToArray();
            var brow=parts.Single(p=>WS(WM(p,"harbour"))=="west"&&WM(p,"gangway")!=null&&WN(WM(WM(p,"gangway"),"step"))==0);
            Vector2 hinge=WP(WM(WM(brow,"gangway"),"hinge"));float spacing=NineMileCreekStation.WharfPumpSpacingMetres;
            // Two existing pump reaches between offers; both stand inland on the delivered working apron.
            Vector2 first=new Vector2(hinge.x+spacing,polygon.Max(p=>p.y)-spacing);
            foreach(string grade in new[]{"gas","diesel"})
            {
                Doc pump=scene.Docs.Single(d=>mine.Contains(d.FileId)&&d.IsScript("HiddenHarbours.Economy.FuelPump")&&d.Field("_grade")==grade);
                Doc hoseGo=scene.Require(pump.FieldRef("m_GameObject"),"H5 fuel hose");Doc hose=scene.TransformOf(hoseGo);
                Doc pedestal=scene.Require(hose.FieldRef("m_Father"),"H5 fuel pedestal transform");
                if(!pedestal.Stripped)throw new Refusal("H5 fuel pedestal is no longer the inherited prefab instance.");
                Doc instance=scene.Require(pedestal.FieldRef("m_PrefabInstance"),"H5 fuel pedestal prefab");
                Vector2 target=first+Vector2.right*(grade=="gas"?0:spacing),local=WaterLocalPosition(hose);
                string text=SetWaterPrefabPosition(instance.Text,pedestal.FieldRef("m_CorrespondingSourceObject"),"x",target.x-local.x);
                text=SetWaterPrefabPosition(text,pedestal.FieldRef("m_CorrespondingSourceObject"),"y",target.y-local.y);
                patch.Edit(instance,text,pump.Field("_id")+" / existing pedestal, on the working apron");
            }
            patch.Edit(root,SetField(root.Text,"m_IsActive","1"),"WharfPumps / both existing offers live; ids, prices and wallet provider retained");
            return patch.Seal(scene);
        }
    }
}
#endif
