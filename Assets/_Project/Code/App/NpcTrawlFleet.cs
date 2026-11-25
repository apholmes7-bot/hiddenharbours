using System;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.App
{
    /// <summary>
    /// Every NPC trawler in the game: her day (Fishing) beside the hull she is and the paint she wears (Boats).
    /// Lives in Resources so the presenter finds the fleet without a scene reference: adding a trawler is a new
    /// day Def and a line here, never a scene edit (rule 2, ADR 0003). It lives in App because the day and the
    /// hull are different modules' Defs and only the composition root may name both (rule 4).
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/NPC Trawl Fleet", fileName = "NpcTrawlFleet")]
    public sealed class NpcTrawlFleet : ScriptableObject
    {
        public const string ResourcesPath = "NpcTrawlFleet";

        [Serializable]
        public struct Trawler
        {
            [Tooltip("Her working day.")]
            public NpcTrawlDayDef Day;
            [Tooltip("The hull she is. Her Visual is what is drawn.")]
            public BoatHullDef Hull;
            [Tooltip("The paint on her hull. Null = the hull's own colours.")]
            public HullPaintSchemeDef Paint;
        }

        [Tooltip("Every NPC trawler. Entries with no day or no hull are skipped.")]
        public Trawler[] Trawlers = Array.Empty<Trawler>();
    }
}
