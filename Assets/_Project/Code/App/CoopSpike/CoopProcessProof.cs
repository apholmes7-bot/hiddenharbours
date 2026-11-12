using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using HiddenHarbours.Core;
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

        private IEnumerator Start()
        {
            string directory = System.Environment.GetEnvironmentVariable("HH_COOP_PROOF_DIR");
            Directory.CreateDirectory(directory);
            string role = Boot.Manager.IsHost ? "host" : "client";
            string record = Path.Combine(directory, role + "-samples.jsonl");
            if (!int.TryParse(System.Environment.GetEnvironmentVariable("HH_COOP_PROOF_SECONDS"), out int seconds)) seconds = 90;
            seconds = Math.Clamp(seconds, 30, 300);
            float end = Time.realtimeSinceStartup + seconds;
            bool captured = false;
            while (Time.realtimeSinceStartup < end)
            {
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
                if (!captured && sample.boats == 2 && sample.remoteViews == 1 && sample.clockAccepted
                    && sample.localInFrame && sample.remoteInFrame && sample.schools > 0)
                {
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, role + ".png"));
                    captured = true;
                    Debug.Log("[Coop] Two-process proof screenshot requested for " + role);
                }
                yield return new WaitForSecondsRealtime(0.2f);
            }
            Boot.Manager.Shutdown();
            yield return null;
            Debug.Log("[Coop] Diagnostic run complete; closing player.");
            Application.Quit(captured ? 0 : 3);
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
