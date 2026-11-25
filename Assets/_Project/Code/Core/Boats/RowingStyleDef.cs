using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>How a rower pulls</b>: the TIMING of one oar stroke, laid over whatever angles the oar's own
    /// pose gives (it never moves an oar further than the rig does, it only decides WHEN the oar is
    /// where), and how hard each part of the stroke drives the hull.
    ///
    /// <para>One entity per file, stable id (<c>rowstyle.snake_case</c>). <c>rowstyle.steady</c> is the
    /// stroke the game has always rowed: its shares equal the stroke's own parts and its tempo is 1, so
    /// the oars and the rower move exactly as before; it adds only a subtle surge of the hull on the
    /// pull. <c>rowstyle.casual</c> is the unhurried pull with a rest between strokes, blades out and the
    /// boat gliding.</para>
    ///
    /// <para><b>Who rows which.</b> The player rows <see cref="GameConfig.PlayerRowingStyle"/>; a
    /// character the game rows for reads <see cref="CharacterVisualDef.RowingStyle"/>, else
    /// <see cref="GameConfig.NpcRowingStyle"/> (<see cref="RowingStyle.ChooseForNpc"/>).</para>
    ///
    /// <para>The stroke phase is the project's own (<see cref="IOarStroke"/>): 0 the catch, 0.5 the
    /// release. The five shares are fractions of ONE stroke's time spent on: the catch (phase 0 to
    /// 0.125), the pull (to 0.375), the release (to 0.625), the recovery (to 1, broken by the rest) and
    /// the rest (the oars held at <see cref="RestAtStrokePhase"/>, blades clear of the water). They are
    /// normalised by their sum.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Boats/Rowing Style", fileName = "RowingStyle")]
    public class RowingStyleDef : ScriptableObject
    {
        [Header("Identity")]
        public string Id = "rowstyle.new";

        [Header("Tempo")]
        [Tooltip("Stroke rate as a share of the boat's own stroke tempo (BoatController's stroke " +
                 "frames per second). 1 = the stroke the game always rowed; 0.5 = one stroke in twice " +
                 "the time.")]
        [Min(RowingStyle.MinTempoScale)] public float TempoScale = 1f;

        [Header("Shares of one stroke's time (normalised by their sum)")]
        [Tooltip("The blade going in: stroke phase 0 to 0.125.")]
        [Min(0f)] public float CatchShare = RowingStyle.CatchSpan;
        [Tooltip("The pull, blade down and sweeping aft: stroke phase 0.125 to 0.375.")]
        [Min(0f)] public float PullShare = RowingStyle.PullSpan;
        [Tooltip("The blade coming out: stroke phase 0.375 to 0.625.")]
        [Min(0f)] public float ReleaseShare = RowingStyle.ReleaseSpan;
        [Tooltip("The recovery, blade out and travelling forward: stroke phase 0.625 to 1, either side " +
                 "of the rest.")]
        [Min(0f)] public float RecoveryShare = RowingStyle.RecoverySpan;
        [Tooltip("The rest between strokes, oars held still at RestAtStrokePhase. 0 = no rest.")]
        [Min(0f)] public float RestShare = 0f;

        [Header("The rest")]
        [Tooltip("Where in the recovery the oars wait (stroke phase, 0.625..1). 0.75 is the top of the " +
                 "recovery: oars square across the boat, blades at their highest.")]
        [Range(RowingStyle.ReleaseEnd, 1f)] public float RestAtStrokePhase = RowingStyle.DefaultRestAt;
        [Tooltip("How softly the oars settle into the rest and leave it: 0 = at an even speed, 1 = " +
                 "easing fully to a stop and away again.")]
        [Range(0f, 1f)] public float RestEase = 0f;

        [Header("The body")]
        [Tooltip("Scales the rowing station's lean-back at the finish. 1 = the station's own layback.")]
        [Min(0f)] public float LaybackScale = 1f;

        [Header("The hull's surge")]
        [Tooltip("How much of the oar's drive is gathered into the pull rather than spread evenly over " +
                 "the stroke. 0 = a steady push (no surge); 1 = all of it on the pull, none in the glide. " +
                 "The average push over a stroke is unchanged by it.")]
        [Range(0f, 1f)] public float SurgeDepth = 0f;
        [Tooltip("The average push over a stroke, as a share of the hull's oar power. 1 = the hull's " +
                 "speed under oars as tuned; below 1 = a gentler rower, slower on average.")]
        [Min(0f)] public float AverageThrustScale = 1f;
    }
}
