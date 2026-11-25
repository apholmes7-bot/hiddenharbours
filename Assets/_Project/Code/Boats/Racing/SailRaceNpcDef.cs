using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    [CreateAssetMenu(menuName = "Hidden Harbours/Sailing/Race NPC", fileName = "SailRaceNpc")]
    public sealed class SailRaceNpcDef : ScriptableObject
    {
        public string Id = "racenpc.unnamed";
        // Same module; the existing hull owns BOTH the polar and the true-wind no-go threshold.
        public BoatHullDef Hull;
        public Vector2 StartPosition;
        public float InitialBearingDeg;
        public float FixedStepSeconds;
        public float MaximumReplaySeconds;
        public int HeadingSamples;
        public float MinimumSpeedFraction;
        public float MaximumSpeedFraction;
        public float GateClearanceMetres;
        public float RoundingLaneFraction;
        public float RoundingStepDeg;
        public float GateOvershootDeg;
        public SailRaceSkipperRules ClubSkipper;

        public SailRaceNpc.Skipper BuildSkipper(string racerId, SailRaceCourse course, int worldSeed, double gunTime,
            IRaceWindSource wind) => Build(racerId, course, worldSeed, gunTime).CreateSkipper(wind, ClubSkipper);

        public SailRaceNpc Build(string racerId, SailRaceCourse course, int worldSeed, double gunTime)
        {
            RaceGeometry.RequireId(Id, "racenpc.");
            if (Hull == null || !Hull.HasSailPlan) throw new ArgumentException("NPC needs a sailing hull with its existing polar.");
            return new SailRaceNpc(racerId, course, worldSeed, gunTime, Hull.SailPolar, Hull.NoGoTrueWindDeg,
                StartPosition, InitialBearingDeg, FixedStepSeconds, MaximumReplaySeconds, HeadingSamples,
                MinimumSpeedFraction, MaximumSpeedFraction, GateClearanceMetres, RoundingLaneFraction,
                RoundingStepDeg, GateOvershootDeg);
        }
    }
}
