using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using UnityEngine;

namespace HiddenHarbours.CoopSpike
{
    /// <summary>Opt-in observation of real built peers; never changes the simulation or camera.</summary>
    public sealed class CoopProcessProof : MonoBehaviour
    {
        public CoopDevStart Boot;
        private readonly List<FishSchool> _schools = new();

        [Serializable]
        private sealed class Sample
        {
            public string role, region, fishHash, remoteHull;
            public int seed, boats, remoteViews, schools;
            public double sampleSeconds, clockSeconds;
            public bool clockAccepted, memoryOnly, localInFrame, remoteInFrame;
        }

        [Serializable]
        private sealed class Placement
        {
            public Vector3 gameplayFoot, drawnFoot, deckStand, remoteFoot, remoteHull, localHull, localVisual;
            public bool meshEnabled, meshPresent, meshDrawing, riderCarried;
            public bool remoteMeshDrawing, remoteOldSheetVisible;
            public Vector3 remoteDeckStand;
            public string meshReason, riderName, drawnState, activeClip, waterState;
            public float pictureDatum, tideRise;
            public string[] shaders;
        }

        private IEnumerator Start()
        {
            string directory = System.Environment.GetEnvironmentVariable("HH_COOP_PROOF_DIR");
            Directory.CreateDirectory(directory);
            string role = Boot.Manager.IsHost ? "host" : "client";
            string record = Path.Combine(directory, role + "-samples.jsonl");
            if (!int.TryParse(System.Environment.GetEnvironmentVariable("HH_COOP_PROOF_SECONDS"), out int seconds)) seconds = 90;
            seconds = Math.Clamp(seconds, 30, 300);
            float end = Time.realtimeSinceStartup + seconds;
            float nextCapture = 0;
            bool captured = false;
            while (Time.realtimeSinceStartup < end)
            {
                if (Boot.Manager == null || !Boot.Manager.IsListening || Boot.Manager.SpawnManager == null) break;
                var sample = new Sample { role = role, region = GameServices.CurrentRegionId,
                    memoryOnly = FindAnyObjectByType<SaveService>() == null,
                    clockSeconds = GameServices.Clock.TotalSeconds };
                CoopBoatReplica authority = null;
                foreach (var obj in Boot.Manager.SpawnManager.SpawnedObjects.Values)
                {
                    var replica = obj.GetComponent<CoopBoatReplica>();
                    if (replica == null) continue;
                    sample.boats++;
                    if (replica.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId) authority = replica;
                    if (replica.IsOwner) sample.localInFrame = InFrame(replica.transform.position);
                    else
                    {
                        var view = replica.GetComponent<CoopReplicaView>();
                        if (view != null && !string.IsNullOrEmpty(view.DrawnHullId))
                        {
                            sample.remoteViews++;
                            sample.remoteHull = view.DrawnHullId;
                            sample.remoteInFrame = InFrame(replica.transform.position);
                        }
                    }
                }
                if (authority != null && authority.Sea.Value.Ready)
                {
                    sample.seed = authority.Sea.Value.Seed;
                    // Compare a shared, whole game second; no clock or model is mutated for proof.
                    sample.sampleSeconds = Math.Floor(authority.Sea.Value.Seconds);
                    sample.clockAccepted = Boot.Manager.IsHost || authority.ClockAccepted;
                    _schools.Clear();
                    if (GameServices.FishSchools is IFishSchoolView schools)
                        schools.SchoolsInView(new Rect(-180, -180, 360, 360), sample.sampleSeconds, _schools);
                    sample.schools = _schools.Count;
                    sample.fishHash = Digest(_schools);
                }
                File.AppendAllText(record, JsonUtility.ToJson(sample) + "\n");
                if (!captured && Time.realtimeSinceStartup >= nextCapture
                    && sample.boats == 2 && sample.remoteViews == 1 && sample.clockAccepted
                    && sample.localInFrame && sample.remoteInFrame && sample.schools > 0)
                {
                    yield return new WaitForEndOfFrame();
                    captured = Capture(Path.Combine(directory, role + ".png"));
                    nextCapture = Time.realtimeSinceStartup + 5;
                    if (captured)
                    {
                        RecordPlacement(Path.Combine(directory, role + "-placement.json"));
                        Debug.Log("[Coop] Two-process proof screenshot saved for " + role);
                    }
                }
                yield return new WaitForSecondsRealtime(0.2f);
            }
            if (Boot.Manager != null && Boot.Manager.IsListening) Boot.Manager.Shutdown();
            yield return null;
            Debug.Log("[Coop] Diagnostic run complete; closing player.");
            Application.Quit(captured ? 0 : 3);
        }

        private void RecordPlacement(string path)
        {
            var rider = FindFirstObjectByType<DeckRiderVisual>();
            var placement = new Placement();
            if (rider != null)
            {
                placement.gameplayFoot = rider.transform.position;
                placement.deckStand = rider.DeckStandRigLocal;
                var presenter = rider.GetComponent<DeckRiderMeshPresenter>();
                placement.riderName = rider.gameObject.name;
                var clip = rider.GetComponent<CharacterClipPlayer>();
                placement.activeClip = clip != null ? clip.Clip.ToString() : "no clip player";
                var walker = rider.GetComponent<PlayerWalkController>();
                placement.waterState = walker != null ? walker.WaterState.ToString() : "no walker";
                placement.meshEnabled = GameServices.Config != null && GameServices.Config.MeshCharacter;
                placement.meshPresent = presenter != null;
                placement.meshDrawing = presenter != null && presenter.DrawsInsteadOfSprite;
                placement.meshReason = presenter != null ? presenter.NotDrawingReason : "no presenter";
                placement.drawnState = presenter != null ? presenter.DrawnStateKey : null;
                placement.riderCarried = rider.IsCarried;
                var boat = FindFirstObjectByType<HiddenHarbours.Boats.BoatController>();
                var tide = boat != null ? boat.GetComponent<HiddenHarbours.Boats.HullTideRide>() : null;
                if (boat != null)
                {
                    placement.localHull = boat.transform.position;
                    var hull = HiddenHarbours.Boats.BoatHullPresenterHost.Resolve(boat.gameObject);
                    placement.localVisual = hull != null && hull.Visual != null ? hull.Visual.position : boat.transform.position;
                }
                placement.pictureDatum = tide != null ? tide.BakedWaterlineElevation : 0f;
                placement.tideRise = tide != null ? tide.ScreenRiseNow() : 0f;
                placement.drawnFoot = presenter != null && presenter.DrawsInsteadOfSprite && presenter.PresentationRoot != null
                    ? presenter.PresentationRoot.position
                    : rider.RiderTransform != null ? rider.RiderTransform.position : rider.transform.position;
            }
            foreach (var view in FindObjectsByType<CoopReplicaView>(FindObjectsSortMode.None))
            {
                placement.remoteFoot = view.Skipper.position;
                placement.remoteHull = view.transform.position;
                placement.remoteMeshDrawing = view.Figure != null && view.Figure.DrawsInsteadOfSprite;
                placement.remoteDeckStand = view.FigureStandRigMetres;
                var sprite = view.Skipper.GetComponent<SpriteRenderer>();
                placement.remoteOldSheetVisible = sprite != null && sprite.enabled
                    && !sprite.forceRenderingOff && sprite.sprite != null;
            }
            string[] names = { "HiddenHarbours/DeckOccludedSprite", "Hidden/HiddenHarbours/FoamBufferAdvect",
                "HiddenHarbours/SilhouetteMask" };
            placement.shaders = Array.ConvertAll(names, name =>
            {
                var shader = Shader.Find(name);
                return name + ": " + (shader == null ? "MISSING" : shader.isSupported ? "SUPPORTED" : "UNSUPPORTED");
            });
            File.WriteAllText(path, JsonUtility.ToJson(placement, true));
        }

        private static bool Capture(string path)
        {
            Texture2D texture = null;
            try
            {
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null || texture.width == 0 || texture.height == 0) return false;
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Coop] Proof capture unavailable: " + error.Message);
                return false;
            }
            finally { if (texture != null) Destroy(texture); }
        }

        private static bool InFrame(Vector3 position)
        {
            var camera = Camera.main;
            if (camera == null) return false;
            var p = camera.WorldToViewportPoint(position);
            return p.z > 0 && p.x > 0 && p.x < 1 && p.y > 0 && p.y < 1;
        }

        private static string Digest(List<FishSchool> schools)
        {
            using var bytes = new MemoryStream();
            using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
            {
                writer.Write(schools.Count);
                foreach (var fish in schools)
                {
                    writer.Write(fish.Centre.x); writer.Write(fish.Centre.y);
                    writer.Write(fish.RadiusMetres); writer.Write(fish.DepthMetres); writer.Write(fish.MarkCount);
                    writer.Write(fish.StartSeconds); writer.Write(fish.EndSeconds);
                    writer.Write(fish.SpeciesIds.Count);
                    foreach (string id in fish.SpeciesIds) writer.Write(id);
                }
            }
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", string.Empty);
        }
    }
}
