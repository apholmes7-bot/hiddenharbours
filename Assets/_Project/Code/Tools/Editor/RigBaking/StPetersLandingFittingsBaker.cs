using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>Dedicated landing recipes. Shared rigs, sheets and importers are read only.</summary>
    public static class StPetersLandingFittingsBaker
    {
        public const string RecipePath = "docs/art/rigs/st-peters-landing-fittings-kit/recipes.json";
        public const string Folder = "Assets/_Project/Art/Sprites/StPeters/LandingFittings";
        public const string ContractPath = Folder + "/stpLandingFittings.contract.json";
        public const string HeightContractPath = Folder + "/stpLandingFittings.height-contract.json";
        public static readonly SetPieceSheetSlicer.Kit Sheets = new SetPieceSheetSlicer.Kit(
            Folder, RecipePath, ContractPath, "hidden-harbours/st-peters-landing-fittings-contract@1");
        public sealed class RenderedPiece
        {
            public string Key, Id, OptionsJson, GameplayJson;
            public int Width, Height, PivotX, PivotY;
            public Vector3 Min, Max;
            public byte[][] Rgba = new byte[8][], HeightRgba;
        }
        [Serializable] public sealed class MetricBounds { public float[] x, y, z; }
        [Serializable] public sealed class HeightMap
        {
            public string key, path;
            public float minimumLocalZ, maximumLocalZ;
        }
        [Serializable] public sealed class HeightContract
        {
            public string schema = "hidden-harbours/st-peters-landing-fittings-height@1";
            public string recipeSha256Lf;
            public HeightMap[] maps;
        }
        static string Q(string value) => "'" + value.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
        static string Abs(string path) => StPetersSetPieceKit.Abs(path);
        public static string Sha(byte[] bytes) => StPetersSetPieceKit.Sha256Hex(bytes);
        static int Number(IRigScriptHost h, string expression) => checked((int)h.EvaluateNumber(expression));

        /// <summary>Independent host rendering, also used for comparison with the real Node fixture.</summary>
        public static List<RenderedPiece> RenderAll()
        {
            using IRigScriptHost h = RigScriptHostFactory.Create();
            h.Execute("globalThis.__lp = " + File.ReadAllText(Abs(RecipePath)) + ";");
            int count = Number(h, "__lp.pieces.length");
            if (count != 3) throw new InvalidOperationException("The landing kit must contain three recipes.");
            var installed = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                string source = h.EvaluateString($"__lp.pieces[{i}].source");
                string expected = h.EvaluateString($"__lp.pieces[{i}].sourceSha256");
                if (StPetersSetPieceKit.Sha256Lf(source) != expected)
                    throw new InvalidOperationException("Landing recipe source hash mismatch: " + source);
                if (installed.Add(source)) h.Execute(File.ReadAllText(Abs(source)));
            }
            h.Execute(@"globalThis.__lpBounds = function(g,key,o) {
              if(g.bounds) return g.bounds(key,o);
              var p=g.PROPS[key],s=Object.assign({variant:0,len:p.run ? 0.4 : 0,loaded:false,tarp:false},p.def,o),faces=[];
              p.build(faces,s);var b={x:[Infinity,-Infinity],y:[Infinity,-Infinity],z:[Infinity,-Infinity]};
              for(var f of faces)for(var v of f.v)for(var j=0;j<3;j++){var a=b[['x','y','z'][j]];a[0]=Math.min(a[0],v[j]);a[1]=Math.max(a[1],v[j]);}
              return b;
            };");
            var result = new List<RenderedPiece>();
            for (int i = 0; i < count; i++)
            {
                h.Execute($"globalThis.__p=__lp.pieces[{i}];globalThis.__g=globalThis[__p.global];globalThis.__b=__lpBounds(__g,__p.key,__p.options);");
                var b = JsonUtility.FromJson<MetricBounds>(h.EvaluateString("JSON.stringify(__b)"));
                var p = new RenderedPiece
                {
                    Key = h.EvaluateString("__p.defKey"), Id = h.EvaluateString("__p.id"),
                    OptionsJson = h.EvaluateString("JSON.stringify(__p.options)"),
                    Width = Number(h, "__g.W"), Height = Number(h, "__g.H"),
                    PivotX = Number(h, "__g.pivot.x"), PivotY = Number(h, "__g.pivot.y"),
                    Min = new Vector3(b.x[0], b.y[0], b.z[0]), Max = new Vector3(b.x[1], b.y[1], b.z[1]),
                    GameplayJson = h.EvaluateString("JSON.stringify(Object.assign({id:__p.id},__g.contract(__p.key,__p.options)))")
                };
                bool hasHeight = h.EvaluateBool("typeof __g.frame==='function'");
                if (hasHeight) p.HeightRgba = new byte[8][];
                for (int k = 0; k < 8; k++)
                {
                    int dir = SetPieceDef.RigDirForFrame(k);
                    p.Rgba[k] = h.EvaluateBytes($"__g.render(__p.key,{dir},__p.options)");
                    if (p.Rgba[k].Length != p.Width * p.Height * 4) throw new InvalidOperationException("Wrong native buffer.");
                    if (hasHeight)
                    {
                        h.Execute($@"globalThis.__f=__g.frame(__p.key,{dir},__p.options);globalThis.__hz=new Uint8ClampedArray(__f.rgba.length);
                          for(var n=0;n<__f.localZ.length;n++)if(__f.rgba[n*4+3]){{var z=Math.max(0,Math.min(255,Math.round(255*(__f.localZ[n]-__b.z[0])/(__b.z[1]-__b.z[0]))));__hz[n*4]=z;__hz[n*4+1]=z;__hz[n*4+2]=z;__hz[n*4+3]=255;}}");
                        p.HeightRgba[k] = h.EvaluateBytes("__hz");
                    }
                }
                result.Add(p);
            }
            return result;
        }

        public static SetPieceSheetSlicer.Contract BakeImportBuild()
        {
            var rendered = RenderAll();
            var contract = new SetPieceSheetSlicer.Contract
            {
                schema = Sheets.ContractSchema, importSizeCap = 2048, facings = 8, kitVersion = "1.0.0",
                todaySha256Lf = StPetersSetPieceKit.Sha256Lf(RecipePath), skyJson = "{}", bakedLit = 0,
                outline = false, pixelsPerMetre = 32, elevationDeg = 40
            };
            Directory.CreateDirectory(Abs(Folder));
            var heightMaps = new List<HeightMap>();
            foreach (var r in rendered)
            {
                if (!IsoPropSheetBaker.MeasureCell(r.Rgba,r.Width,r.Height,r.PivotX,r.PivotY,
                    out int cw,out int ch,out int px,out int py,out int left,out int top,out _))
                    throw new InvalidOperationException("Empty landing recipe: " + r.Key);
                if (cw * 8 > 2048 || ch > 2048) throw new InvalidOperationException("Landing sheet exceeds import cap.");
                var p = new SetPieceSheetSlicer.Piece
                {
                    key=r.Key,id=r.Id,sheet=SetPieceSheetSlicer.SheetName(r.Key),cells=8,headings=0,
                    cellW=cw,cellH=ch,cols=8,rows=1,sheetW=cw*8,sheetH=ch,optionsJson=r.OptionsJson
                };
                int x0=r.PivotX+left,y0=r.PivotY+top;
                var pixels=new Color32[p.sheetW*p.sheetH];
                Color32[] heights=r.HeightRgba==null?null:new Color32[pixels.Length];
                for(int k=0;k<8;k++)
                {
                    BuildingRigBaker.BlitCropped(r.Rgba[k],r.Width,r.Height,x0,y0,cw,ch,pixels,p.sheetW,p.sheetH,k,0);
                    if(heights!=null)BuildingRigBaker.BlitCropped(r.HeightRgba[k],r.Width,r.Height,x0,y0,cw,ch,heights,p.sheetW,p.sheetH,k,0);
                    int opaque=0;for(int n=3;n<r.Rgba[k].Length;n+=4)if(r.Rgba[k][n]>0)opaque++;
                    p.frames.Add(new SetPieceSheetSlicer.Frame {cell=k,rigDir=SetPieceDef.RigDirForFrame(k),heading=-1,
                        cropX=x0,cropY=y0,pivotX=px,pivotY=py,nativeW=r.Width,nativeH=r.Height,nativePivotX=r.PivotX,nativePivotY=r.PivotY,opaque=opaque});
                }
                WritePng(SetPieceSheetSlicer.SheetPath(Sheets,r.Key),pixels,p.sheetW,p.sheetH);
                if(heights!=null)
                {
                    string path=Folder+"/"+p.sheet+"_height.png";WritePng(path,heights,p.sheetW,p.sheetH);
                    heightMaps.Add(new HeightMap {key=r.Key,path=path,minimumLocalZ=r.Min.z,maximumLocalZ=r.Max.z});
                }
                contract.pieces.Add(p);
            }
            File.WriteAllText(Abs(ContractPath),JsonUtility.ToJson(contract,true)+"\n");
            File.WriteAllText(Abs(HeightContractPath),JsonUtility.ToJson(new HeightContract {recipeSha256Lf=contract.todaySha256Lf,maps=heightMaps.ToArray()},true)+"\n");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(var p in contract.pieces) Configure(SetPieceSheetSlicer.SheetPath(Sheets,p.key),true);
            foreach(var p in heightMaps)Configure(p.path,false);
            var problems=new List<string>();int sliced=SetPieceSheetSlicer.SliceAll(Sheets,problems);
            if(sliced!=3||problems.Count>0)throw new InvalidOperationException(string.Join("\n",problems));
            foreach(var r in rendered)BuildDef(r,contract.pieces.Single(p=>p.key==r.Key));
            AssetDatabase.SaveAssets();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[StPetersLandingFittings] baked three recipes, 24 facings; shared sheets unchanged.");
            return contract;
        }
        /// <summary>Bake dedicated art and Def assets only; no scene or shared sheet is written.</summary>
        public static void BakeFromCommandLine()
        {
            int code=1;
            try
            {
                string[] args=System.Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-landingFittingsResult");
                if(at<0||at+1>=args.Length)throw new InvalidOperationException("Landing fittings result path required.");
                BakeImportBuild();File.WriteAllText(args[at+1],"LANDING_FITTINGS_BAKE_SUCCESS\n");code=0;
            }
            catch(Exception ex){Debug.LogException(ex);}
            EditorApplication.Exit(code);
        }
        static void WritePng(string path,Color32[] pixels,int w,int h)
        {
            var texture=new Texture2D(w,h,TextureFormat.RGBA32,false,false);
            try {texture.SetPixels32(pixels);texture.Apply(false,false);File.WriteAllBytes(Abs(path),texture.EncodeToPNG());}
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        static void Configure(string path,bool sprite)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
            var t=(TextureImporter)AssetImporter.GetAtPath(path);
            t.textureType=sprite?TextureImporterType.Sprite:TextureImporterType.Default;
            if(sprite){t.spriteImportMode=SpriteImportMode.Multiple;t.spritePixelsPerUnit=32;}
            t.filterMode=FilterMode.Point;t.mipmapEnabled=false;t.npotScale=TextureImporterNPOTScale.None;
            t.textureCompression=TextureImporterCompression.Uncompressed;t.isReadable=true;t.sRGBTexture=sprite;t.maxTextureSize=2048;
            var platform=t.GetDefaultPlatformTextureSettings();platform.format=TextureImporterFormat.RGBA32;platform.maxTextureSize=2048;t.SetPlatformTextureSettings(platform);
            t.SaveAndReimport();
        }
        static void BuildDef(RenderedPiece r,SetPieceSheetSlicer.Piece sheet)
        {
            string path=SetPieceDefBuilder.DefPath(r.Key);
            var d=AssetDatabase.LoadAssetAtPath<SetPieceDef>(path);
            if(d==null){d=ScriptableObject.CreateInstance<SetPieceDef>();AssetDatabase.CreateAsset(d,path);}
            var sprites=AssetDatabase.LoadAllAssetsAtPath(SetPieceSheetSlicer.SheetPath(Sheets,r.Key)).OfType<Sprite>().ToDictionary(s=>s.name);
            d.Id=r.Id;d.DisplayName=r.Key;d.RigKey=r.Key;d.SceneId="keyscene.stp_landing";d.BakedOptionsJson=r.OptionsJson;
            d.BoundsMin=r.Min;d.BoundsMax=r.Max;d.PixelsPerMetre=32;d.ElevationDeg=40;d.CellSize=new Vector2Int(sheet.cellW,sheet.cellH);
            d.Layer="raised";bool hanger=r.Key.StartsWith("landingTyre",StringComparison.Ordinal);
            d.Walk=hanger?"behind":"on";d.Mount=hanger?"wall":"ground";d.GameplayJson=r.GameplayJson;
            d.Frames=sheet.frames.Select(f=>new SetPieceFrame {Sprite=sprites[SetPieceSheetSlicer.SpriteName(r.Key,f.cell)],
                RigDir=f.rigDir,BearingDeg=(360-f.rigDir*45)%360,ShowFaces="recipe facing",CameraSees="metric rig",
                NativeSize=new Vector2Int(f.nativeW,f.nativeH),NativePivot=new Vector2Int(f.nativePivotX,f.nativePivotY),
                Crop=new Vector2Int(f.cropX,f.cropY),Pivot=new Vector2Int(f.pivotX,f.pivotY)}).ToArray();
            if(hanger)d.Anchors=new[] {new SetPieceAnchor {Name="hanger",Position=Vector3.zero}};
            EditorUtility.SetDirty(d);
        }
    }
}
