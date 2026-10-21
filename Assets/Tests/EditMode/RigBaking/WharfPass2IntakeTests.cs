using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    public class WharfPass2IntakeTests
    {
        const string Kit = "docs/art/rigs/wharf-rig-kit-v2/";
        static string Root => RigCatalog.RepoRoot;
        static string Read(string file) => File.ReadAllText(Path.Combine(Root, Kit + file));
        static string Quote(string value) => "\"" + value.Replace("\\", "\\\\")
            .Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n")
            .Replace("\t", "\\t") + "\"";
        static string Sha(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        [Test]
        public void DeliveredKitMatchesPinnedManifestAndLfBytes()
        {
            Assert.That(Sha(File.ReadAllBytes(Path.Combine(Root, Kit + "SHA256SUMS.txt"))),
                Is.EqualTo("bcc115b9db3d8438ed2c63c48832976adfba4f110824c98cbadcf74a9f3732d5"));
            var lines = Read("SHA256SUMS.txt").Split('\n').Where(l => l.Length > 0).ToArray();
            Assert.That(lines.Length, Is.EqualTo(77));
            foreach (string line in lines)
            {
                string file = line.Substring(66);
                byte[] bytes = File.ReadAllBytes(Path.Combine(Root, Kit + file));
                Assert.That(Sha(bytes), Is.EqualTo(line.Substring(0, 64)), file);
                if (!file.EndsWith(".png", StringComparison.Ordinal))
                    Assert.That(Encoding.UTF8.GetString(bytes), Does.Not.Contain("\r"), file);
            }
            Assert.That(Directory.GetFiles(Path.Combine(Root, Kit), "*", SearchOption.AllDirectories).Length,
                Is.EqualTo(79), "Do not insert generated data into the immutable delivery.");
        }

        [Test]
        public void CatalogInstallsExactlyTheReadmeChainWithoutViewerLibraries()
        {
            var expected = new[] { "wharfRig2.geo.js", "wharfRig2.fam.js", "wharfRig2.verbs.js",
                "wharfRig2.js", "wharfRig2.kit.js" };
            var sources = new List<string>();
            var visited = new HashSet<string>();
            void Walk(string key)
            {
                if (!visited.Add(key)) return;
                var entry = RigCatalog.Get(key);
                foreach (string prerequisite in entry.Prerequisites) Walk(prerequisite);
                sources.Add(entry.ScriptPath);
            }
            Walk("wharfRig2Kit");
            CollectionAssert.AreEqual(expected.Select(f => Kit + f).ToArray(), sources);
            using var host = new V8RigScriptHost();
            RigCatalog.InstallModule(host, RigCatalog.Get("wharfRig2Kit"));
            Assert.That(host.EvaluateNumber("Object.keys(WharfGeo2.FAMILIES).length"), Is.EqualTo(10));
            Assert.That(host.EvaluateNumber("Object.keys(WharfRig2.PRESETS).length"), Is.EqualTo(25));
            Assert.That(host.EvaluateBool("typeof WeatherSky === 'undefined' && typeof CharacterIso9 === 'undefined'"), Is.True);
        }

        [Test]
        public void AllSidecarsMatchLiveModelsAndPinTheCharacterTheyName()
        {
            using var host = new V8RigScriptHost();
            RigCatalog.InstallModule(host, RigCatalog.Get("wharfRig2Kit"));
            host.Execute(Read("checks/wharfChecks.js"));
            var files = Directory.GetFiles(Path.Combine(Root, Kit + "gameplay"), "*.json");
            Assert.That(files.Length, Is.EqualTo(29));
            string characterHash = Sha(File.ReadAllBytes(Path.Combine(Root, Kit + "lib/characterIsoRig9.js")));
            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                host.Execute("var gp = JSON.parse(" + Quote(text) + ");");
                Assert.That(host.EvaluateString("gp.schema"), Is.EqualTo("hidden-harbours/wharf-gameplay@2"), file);
                Assert.That(host.EvaluateBool("WharfChecks2.keys().includes(gp.key)"), Is.True, file);
                Assert.That(host.EvaluateString("gp.CHAR.rig"), Is.EqualTo("characterIsoRig9.js"), file);
                Assert.That(host.EvaluateString("gp.CHAR.rigSha"), Is.EqualTo(characterHash), file);
                Assert.That(host.EvaluateString("WharfChecks2.text(WharfChecks2.sidecar(gp.key))"), Is.EqualTo(text), file);
            }
        }

        [Test]
        public void RasterAndHarbourConventionsFollowMeasuredPixelCoordinates()
        {
            using var host = new V8RigScriptHost();
            RigCatalog.InstallModule(host, RigCatalog.Get("wharfRig2Kit"));
            // Least-squares fit from the raster's visible model coordinates to screen coordinates.
            // This measures the raster output, not a label or copied camera formula. Remove the
            // known 40-degree foreshortening only when computing the +X-axis azimuth.
            host.Execute(@"
                function axisAngle(dir) {
                  var fr=WharfRig2.frame('tallPier',{dir:dir,tide:0.9});
                  var a=Array.from({length:4},()=>[0,0,0,0,0,0]),n=0;
                  for(var i=0;i<fr.w*fr.h;i++) if(fr.gb.a[i]) {
                    var v=[fr.gb.X[i],fr.gb.Y[i],fr.gb.Z[i],1],x=i%fr.w,y=Math.floor(i/fr.w);
                    for(var r=0;r<4;r++){for(var c=0;c<4;c++)a[r][c]+=v[r]*v[c];a[r][4]+=v[r]*x;a[r][5]+=v[r]*y;}n++;
                  }
                  if(n<1000)throw Error('blank raster');
                  for(var c=0;c<4;c++) {
                    var best=c;for(var r=c+1;r<4;r++)if(Math.abs(a[r][c])>Math.abs(a[best][c]))best=r;
                    var swap=a[c];a[c]=a[best];a[best]=swap;
                    var d=a[c][c];if(Math.abs(d)<1e-8)throw Error('singular raster');
                    for(var j=c;j<6;j++)a[c][j]/=d;
                    for(var r=0;r<4;r++)if(r!==c){var m=a[r][c];for(var j=c;j<6;j++)a[r][j]-=m*a[c][j];}
                  }
                  return Math.atan2(a[0][5]/Math.sin(40*Math.PI/180),a[0][4])*180/Math.PI;
                }");
            Assert.That(host.EvaluateNumber("axisAngle(0)"), Is.EqualTo(0).Within(0.1));
            Assert.That(host.EvaluateNumber("axisAngle(1)"), Is.EqualTo(-45).Within(0.1));
            Assert.That(RigCatalog.Get("wharfRig2").DeclaredConvention, Is.EqualTo(AzimuthConvention.CounterClockwise));
            Assert.That(RigCatalog.Get("wharfRig2Kit").DeclaredConvention, Is.EqualTo(AzimuthConvention.CounterClockwise));
        }

        [TestCase("tallPier", "lit")]
        [TestCase("tallPier", "unlit")]
        [TestCase("tallPier", "normal")]
        [TestCase("tallPier", "light")]
        [TestCase("tallPier", "height")]
        [TestCase("tallPier", "snow")]
        [TestCase("tallPier", "detail")]
        [TestCase("tallPier", "layer")]
        [TestCase("timberFloat", "unlit")]
        [TestCase("timberFloat", "normal")]
        [TestCase("timberFloat", "height")]
        [TestCase("timberFloat", "snow")]
        public void SuppliedEngineMapMatchesV8Pixels(string key, string channel)
        {
            using var host = new V8RigScriptHost();
            RigCatalog.InstallModule(host, RigCatalog.Get("wharfRig2Kit"));
            host.Execute(EngineMapsJs);
            host.Execute("var rendered = engineSheet(" + Quote(key) + "," + Quote(channel) + ");");
            byte[] actual = host.EvaluateBytes("rendered.rgba");
            string suffix = key == "timberFloat" ? "_sheet" : "";
            byte[] png = File.ReadAllBytes(Path.Combine(Root, Kit + "maps/" + key + "_d1_" + channel + suffix + ".png"));
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Assert.That(texture.LoadImage(png), Is.True);
                Assert.That(texture.width, Is.EqualTo(host.EvaluateNumber("rendered.w")));
                Assert.That(texture.height, Is.EqualTo(host.EvaluateNumber("rendered.h")));
                var expected = texture.GetPixels32();
                int mismatches = 0, covered = 0;
                for (int y = 0; y < texture.height; y++)
                    for (int x = 0; x < texture.width; x++)
                    {
                        int i = (y * texture.width + x) * 4;
                        var e = expected[(texture.height - 1 - y) * texture.width + x];
                        if (actual[i + 3] != 0) covered++;
                        if (actual[i] != e.r || actual[i + 1] != e.g || actual[i + 2] != e.b || actual[i + 3] != e.a) mismatches++;
                    }
                Assert.That(covered, Is.GreaterThan(1000), "A blank comparison is not evidence.");
                Assert.That(mismatches, Is.Zero, key + "/" + channel);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        // Engine encoding is different from view()'s diagnostic height/snow/layer colours.
        const string EngineMapsJs = @"
            function engineMap(fr,ch) {
              if(!['height','snow','layer'].includes(ch))return WharfRig2.view(fr,ch);
              var out=new Uint8ClampedArray(fr.w*fr.h*4),g=fr.gb;
              for(var i=0;i<fr.w*fr.h;i++)if(g.a[i]) {
                if(ch==='height'){var mm=Math.round((g.Z[i]+10)*1000);out[4*i]=(mm>>8)&255;out[4*i+1]=mm&255;}
                if(ch==='snow')out[4*i]=out[4*i+1]=out[4*i+2]=fr.snow[i];
                if(ch==='layer'){out[4*i]=g.lay[i]?255:0;out[4*i+1]=g.g[i]*40;}
                out[4*i+3]=255;
              }return out;
            }
            function engineSheet(key,ch) {
              var moving=key==='timberFloat',o={dir:1,tide:0.9,wind:{w:moving?0.6:0.2,gust:0.4,dir:1}};
              var f0=WharfRig2.frame(key,o),cols=moving?4:1,w=f0.w*cols,h=f0.h*cols,rgba=new Uint8ClampedArray(w*h*4);
              for(var f=0;f<(moving?16:1);f++) {
                var fr=WharfRig2.frame(key,Object.assign({},o,{frame:f})),p=engineMap(fr,ch);
                for(var y=0;y<fr.h;y++)rgba.set(p.subarray(y*fr.w*4,(y+1)*fr.w*4),((Math.floor(f/cols)*fr.h+y)*w+(f%cols)*fr.w)*4);
              }return {w:w,h:h,rgba:rgba};
            }";
    }
}
