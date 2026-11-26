using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>How a speaker reads the day's weather aloud</b>, as DATA (ADR 0003): the hours he looks at the
    /// sky, the wind speeds that separate a light air from a fresh breeze and a breeze from too much for
    /// a dory, the compass words he uses, and the short lines he picks from. One asset per speaker
    /// (<c>weather_read.snake_case</c>, e.g. <c>weather_read.basil_samson</c>), referenced from his
    /// <see cref="DialogueDef.WeatherRead"/>.
    ///
    /// <para><b>The words are always true.</b> Nothing here is a forecast of its own: <see cref="WeatherRead"/>
    /// asks Core's deterministic wind law for this day's wind at <see cref="ReadHour"/> and at
    /// <see cref="OutlookHour"/> and only chooses words for what it is told. Nothing is saved; the line is
    /// recomputed from <c>(worldSeed, day)</c> every time he is asked (CLAUDE.md rule 5).</para>
    ///
    /// <para><b>Lines.</b> A <i>now</i> line names where the wind is from and how hard (write
    /// <c>{from}</c> where the compass word goes), and an <i>outlook</i> line says what the rest of the day
    /// does and what that means for a man in a rowing dory. He says one of each, joined with a space. Which
    /// line of a pool he picks comes from <c>(worldSeed, day)</c>, so he does not repeat himself every day
    /// and never changes his mind within one.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Weather Read", fileName = "WeatherRead")]
    public class WeatherReadDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (weather_read.snake_case, e.g. weather_read.basil_samson).")]
        public string Id = "weather_read.example";

        [Header("When he looks at the sky (hours of the day, 0..24)")]
        [Tooltip("The hour his read of the wind is for: the morning, when a man decides whether to go out.")]
        [Range(0f, 24f)] public float ReadHour = 7f;

        [Tooltip("The hour he judges whether the wind builds or eases: later the same day.")]
        [Range(0f, 24f)] public float OutlookHour = 15f;

        [Tooltip("Each look is the wind averaged over this many game hours centred on its hour, so one " +
                 "passing gust does not decide his read.")]
        [Min(0f)] public float LookWindowHours = 2f;

        [Tooltip("How many samples make up one look (spread evenly across the window).")]
        [Range(1, 16)] public int LookSamples = 5;

        [Header("Wind speed bands (metres per second, ascending)")]
        [Tooltip("Below this the wind is a light air (barely a breath).")]
        [Min(0f)] public float ModerateFrom = 2f;

        [Tooltip("From this the wind is a fresh breeze: workable, but a dory feels it.")]
        [Min(0f)] public float FreshFrom = 6f;

        [Tooltip("From this the wind is too much for a man in a small rowing dory.")]
        [Min(0f)] public float TooMuchFrom = 8f;

        [Tooltip("From this it is a gale.")]
        [Min(0f)] public float GaleFrom = 11f;

        [Tooltip("The outlook wind must differ from the morning's by at least this much (m/s) to count as " +
                 "building or easing; anything less and he says it will hold.")]
        [Min(0f)] public float ChangeAtLeast = 1.5f;

        [Header("Compass words, the eight points clockwise from north")]
        [Tooltip("The word for where the wind comes FROM, in order: N, NE, E, SE, S, SW, W, NW.")]
        public string[] CompassWords =
            { "north", "nor'east", "east", "sou'east", "south", "sou'west", "west", "nor'west" };

        [Header("Now lines: where it is from and how hard ({from} = the compass word)")]
        [TextArea] public string[] LightLines = Array.Empty<string>();
        [TextArea] public string[] ModerateLines = Array.Empty<string>();
        [TextArea] public string[] FreshLines = Array.Empty<string>();
        [TextArea] public string[] TooMuchLines = Array.Empty<string>();
        [TextArea] public string[] GaleLines = Array.Empty<string>();

        [Header("Outlook lines: the rest of the day, and what it means for a dory")]
        [Tooltip("Fair now and it holds about the same.")]
        [TextArea] public string[] HoldingLines = Array.Empty<string>();
        [Tooltip("Fair now; it builds later, but stays fit for a dory.")]
        [TextArea] public string[] BuildingLines = Array.Empty<string>();
        [Tooltip("Fair now, and it eases later.")]
        [TextArea] public string[] EasingLines = Array.Empty<string>();
        [Tooltip("Fair now, but it comes up to too much for a dory later: go early, be home early.")]
        [TextArea] public string[] HomeEarlyLines = Array.Empty<string>();
        [Tooltip("Too much for a dory now, and it does not ease enough today.")]
        [TextArea] public string[] StayInLines = Array.Empty<string>();
        [Tooltip("Too much for a dory now; it eases later, but not enough to trust the day.")]
        [TextArea] public string[] StayInEasingLines = Array.Empty<string>();

        /// <summary>The pool of now lines for a band. Never null.</summary>
        public string[] NowLines(WindBand band)
        {
            string[] pool;
            switch (band)
            {
                case WindBand.Light: pool = LightLines; break;
                case WindBand.Moderate: pool = ModerateLines; break;
                case WindBand.Fresh: pool = FreshLines; break;
                case WindBand.TooMuch: pool = TooMuchLines; break;
                default: pool = GaleLines; break;
            }
            return pool ?? Array.Empty<string>();
        }

        /// <summary>The pool of outlook lines for an outlook. Never null.</summary>
        public string[] OutlookLines(WeatherOutlook outlook)
        {
            string[] pool;
            switch (outlook)
            {
                case WeatherOutlook.Holding: pool = HoldingLines; break;
                case WeatherOutlook.Building: pool = BuildingLines; break;
                case WeatherOutlook.Easing: pool = EasingLines; break;
                case WeatherOutlook.HomeEarly: pool = HomeEarlyLines; break;
                case WeatherOutlook.StayIn: pool = StayInLines; break;
                default: pool = StayInEasingLines; break;
            }
            return pool ?? Array.Empty<string>();
        }

        /// <summary>The band a wind speed falls in, by this asset's edges.</summary>
        public WindBand BandOf(float metresPerSecond)
        {
            if (metresPerSecond >= GaleFrom) return WindBand.Gale;
            if (metresPerSecond >= TooMuchFrom) return WindBand.TooMuch;
            if (metresPerSecond >= FreshFrom) return WindBand.Fresh;
            if (metresPerSecond >= ModerateFrom) return WindBand.Moderate;
            return WindBand.Light;
        }

        /// <summary>True when the band edges climb in order (a content check, and the guard the read relies on).</summary>
        public bool BandsAscend =>
            ModerateFrom > 0f && FreshFrom > ModerateFrom && TooMuchFrom > FreshFrom && GaleFrom > TooMuchFrom;
    }

    /// <summary>How hard the wind blows, in the words a dory man would sort it into.</summary>
    public enum WindBand
    {
        Light = 0,
        Moderate,
        Fresh,
        TooMuch,
        Gale,
    }

    /// <summary>What the rest of the day does, judged for a small rowing dory.</summary>
    public enum WeatherOutlook
    {
        Holding = 0,
        Building,
        Easing,
        HomeEarly,
        StayIn,
        StayInEasing,
    }
}
