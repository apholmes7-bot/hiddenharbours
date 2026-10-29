#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public partial class WharfBuildingPass2PlatePlayTests
    {
        sealed class P1Board
        {
            public string Id; public float X0, X1, Top, Bottom, BoardTide, Tide;
            public int Day, Minute;
            public P1Board(string id, float x0, float x1, float top, float bottom, int day, int minute, float boardTide, float tide)
            { Id = id; X0 = x0; X1 = x1; Top = top; Bottom = bottom; Day = day; Minute = minute; BoardTide = boardTide; Tide = tide; }
        }
        static readonly P1Board[] P1Boards =
        {
            new P1Board("landing.wide_approach.1431",190.555f,215.445f,9.649f,-4.351f,29,871,2.18f,1.999786f),
            new P1Board("cannery.wide_landing.0600",168.555f,193.445f,19.596f,5.596f,39,360,-.85f,-.842258f),
            new P1Board("ne_light.wide_station_0600",195.055f,219.945f,81.026f,67.026f,39,360,-.85f,-.842258f),
            new P1Board("crossing.head_open",-90,-46,12.046f,-12.704f,52,1064,-.1f,-.140773f),
            new P1Board("ginnys_plot.plot_noon",58,118,72.229f,22.229f,44,720,.63f,.755822f),
            new P1Board("alder_fall.noon",91,121,70.053f,50.053f,44,720,.63f,.755822f),
            new P1Board("gap_bridge.bridge_noon",142,171,-28.642f,-45.642f,44,720,.63f,.755822f),
            new P1Board("whelps_lookout.lookout_noon",154,199,-26.893f,-50.893f,44,720,.63f,.755822f),
        };
        [Serializable] sealed class P1Numbers
        {
            public string board, phase, hour, file;
            public int dayIndex, minute, plants, authored, chunks, missing, dormant;
            public float boardTide, actualTide, tintR, tintG, tintB;
            public long draws, batches, loadedTextureBytes, fieldMapBytes;
            public double cpuP95Ms;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator StPeters_PlantField_Before_AtTheEightSummerFrames() => P1Capture(false);
        [UnityTest, Timeout(600000)]
        public IEnumerator StPeters_PlantField_After_AtTheEightSummerFrames() => P1Capture(true);

        IEnumerator P1Capture(bool after)
        {
            RequireAGraphicsDevice();
            string phase = after ? "after" : "before";
            string folder = Path.Combine(Application.temporaryCachePath, "stp-p1-" + phase);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "_floor.txt"), DateTime.UtcNow.ToString("O"));
            TestContext.WriteLine("P1_FOLDER: " + folder);
            yield return LoadRegion(StPeters); EnsureTheFollow(); HoldTheMood();
            var fields = Object.FindObjectsByType<PlantField>(FindObjectsSortMode.None);
            Assert.AreEqual(after ? 1 : 0, fields.Length, "the selected scene version does not match this capture");
            var field = after ? fields[0] : null;
            if (after) Assert.AreEqual(9139, field.AuthoredPlantCount);
            Behaviour pixel = _cam.GetComponents<Behaviour>().FirstOrDefault(b => b.GetType().Name == "PixelPerfectCamera");
            bool pixelEnabled = pixel != null && pixel.enabled, followEnabled = _follow.enabled;
            try
            {
                _follow.enabled = false; if (pixel != null) pixel.enabled = false;
                foreach (var board in P1Boards)
                {
                    var clock = GameServices.Clock; Assert.IsNotNull(clock); Assert.IsNotNull(GameServices.Config);
                    double time = (board.Day + board.Minute / 1440.0) * GameServices.Config.SecondsPerDay;
                    Time.timeScale = 1; clock.SeekTo(time); clock.TimeScale = 0;
                    Assert.AreEqual(Season.HighSummer, clock.Season);
                    Assert.AreEqual(board.Tide, GameServices.Environment.WaterLevelAt(time), .005f);
                    Color previous = Shader.GetGlobalColor("_DayNightTint"); int stable = 0; float waited = 0;
                    for (int i = 0; i < 900; i++)
                    {
                        yield return null; waited += Time.deltaTime;
                        Color current = Shader.GetGlobalColor("_DayNightTint");
                        float delta = Mathf.Abs(current.r-previous.r)+Mathf.Abs(current.g-previous.g)+Mathf.Abs(current.b-previous.b);
                        stable = delta < .0001f ? stable + 1 : 0; previous = current;
                        if (waited >= ClearRunSeconds && stable >= 4) break;
                        Assert.Less(i,899,"the hourly light did not settle");
                    }
                    if (after)
                    {
                        field.RefreshAppearance(); Assert.AreEqual(1,field.CurrentSeason);
                        Assert.AreEqual(0,field.MissingCells,"every authored state needs a bound map");
                    }
                    _cam.targetTexture = null;
                    if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); }
                    _w = Mathf.RoundToInt((board.X1-board.X0)*32); _h = Mathf.RoundToInt((board.Top-board.Bottom)*32);
                    _rt = new RenderTexture(_w,_h,24,RenderTextureFormat.ARGBHalf); _rt.Create(); _cam.targetTexture = _rt;
                    _cam.orthographic = true; _cam.aspect = (float)_w/_h; _cam.orthographicSize = _h/64f;
                    _cam.transform.position = new Vector3((board.X0+board.X1)/2,(board.Top+board.Bottom)/2,_cam.transform.position.z);
                    for (int i=0;i<30;i++) yield return null;
                    yield return ShadersReady();
                    Assert.Less(Vector2.Distance(_cam.transform.position,new Vector2((board.X0+board.X1)/2,(board.Top+board.Bottom)/2)),.0001f);
                    var cpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread");
                    var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count");
                    var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render,"Batches Count");
                    var times = new List<double>(); long draw=0,batch=0;
                    try
                    {
                        for (int i=0;i<120;i++)
                        {
                            yield return null;
                            if (cpu.Valid && cpu.LastValue>0) times.Add(cpu.LastValue/1000000.0);
                            if (draws.Valid) draw=Math.Max(draw,draws.LastValue);
                            if (batches.Valid) batch=Math.Max(batch,batches.LastValue);
                        }
                    }
                    finally { cpu.Dispose(); draws.Dispose(); batches.Dispose(); }
                    times.Sort(); Time.timeScale=0; yield return null;
                    string file=board.Id+".png"; File.WriteAllBytes(Path.Combine(folder,file),Capture());
                    var textures=Resources.FindObjectsOfTypeAll<Texture>().Where(t=>t!=null).Distinct().ToArray();
                    var maps=after ? field.Species.SelectMany(s=>s.Pages).SelectMany(p=>new[]{p.Albedo,p.Normal,p.Light,p.Detail}).Where(t=>t!=null).Distinct().ToArray() : Array.Empty<Texture2D>();
                    var tint=Shader.GetGlobalColor("_DayNightTint");
                    var row=new P1Numbers { board=board.Id,phase=phase,file=file,dayIndex=board.Day,minute=board.Minute,
                        hour=(board.Minute/60).ToString("00")+":"+(board.Minute%60).ToString("00"),boardTide=board.BoardTide,
                        actualTide=GameServices.Environment.WaterLevelAt(time),tintR=tint.r,tintG=tint.g,tintB=tint.b,
                        draws=draw,batches=batch,cpuP95Ms=times.Count>0?times[Math.Min(times.Count-1,(int)(times.Count*.95))]:-1,
                        loadedTextureBytes=textures.Sum(t=>GpuBytes(t)),fieldMapBytes=maps.Sum(t=>GpuBytes(t)),
                        plants=after?field.PlantCount:0,authored=after?field.AuthoredPlantCount:0,chunks=after?field.ChunkCount:0,
                        missing=after?field.MissingCells:0,dormant=after?field.DormantCells:0 };
                    File.WriteAllText(Path.Combine(folder,board.Id+".json"),JsonUtility.ToJson(row,true));
                    TestContext.WriteLine("P1 "+JsonUtility.ToJson(row));
                }
            }
            finally { _follow.enabled=followEnabled; if(pixel!=null)pixel.enabled=pixelEnabled; Time.timeScale=1; }
        }
    }
}
#endif
