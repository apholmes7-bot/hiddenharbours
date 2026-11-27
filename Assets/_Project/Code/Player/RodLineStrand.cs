using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;   // RodLineMath

namespace HiddenHarbours.Player
{
    // RodLineLook moved to Core in its own file (Core/Fishing/RodLineLook.cs), so a World Def can carry it.

    /// <summary>
    /// <b>One fishing line</b>: its <see cref="LineRenderer"/>, its sample buffer, and the drawing of it
    /// from a rod tip to a far end for one published <see cref="FishingState"/> (sag, the slack pop,
    /// whitening, the strain shudder, the colour axis, and its order over the fisher). Shared by every
    /// presenter that draws a rod fight, so the player's line and a villager's line are one line.
    ///
    /// <para><b>Reads no clock.</b> Every second it needs is handed in by the caller (the slack pop's
    /// seconds, the shudder's phase seconds), so the same inputs draw the same line: the player passes
    /// its own clocks, a villager passes seconds on its game-time timeline. Allocation-free per frame;
    /// the renderer and buffer are made once in the constructor.</para>
    /// </summary>
    public sealed class RodLineStrand
    {
        private readonly LineRenderer _line;
        private readonly Vector2[] _buffer;

        /// <summary>Make the line as a child of <paramref name="parent"/>, hidden.</summary>
        public RodLineStrand(Transform parent, float width, int segments)
        {
            var go = new GameObject("FishingLine");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            lr.startWidth = width;
            lr.endWidth = width;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) lr.material = new Material(shader);   // null headless → skip (rope precedent)
            lr.positionCount = 0;   // ordered over the fisher as it draws (Render)
            lr.enabled = false;
            _line = lr;
            _buffer = new Vector2[Mathf.Max(2, segments)];
        }

        /// <summary>The renderer. For tests and plates.</summary>
        public LineRenderer Line => _line;

        /// <summary>
        /// Draw the line from <paramref name="tip"/> to <paramref name="far"/>.
        /// <paramref name="fightLoad01"/> is the fight's load read (the worse of bend and tension);
        /// <paramref name="slackEdgeSeconds"/> the seconds since the slack window opened;
        /// <paramref name="shudderSeconds"/> the seconds the shudder's phase is taken at. The order is
        /// written only when <paramref name="sorted"/> (the caller has a fisher to sort over).
        /// </summary>
        public void Render(Vector2 tip, Vector2 far, in FishingState s, in RodLineLook look,
                           in FishFeelSettings feel, float fightLoad01, float slackEdgeSeconds,
                           float shudderSeconds, bool sorted, int sortingOrder)
        {
            if (_line == null) return;

            float taut = RodPresenterMath.TautFor(s.Phase, s.SlackWindowOpen, s.RodBend01,
                                                  s.Tension01, look.RestTaut, look.SinkTaut, look.FightTautFloor);
            // HER WEIGHT ON THE LINE (GameConfig.FishFeel): a heavy fish keeps it tight, humming and a
            // touch brighter. Hooked fish only — a hand-gathered catch (Tending) is untouched.
            bool hooked = s.IsFightPhase && s.Phase != FishingPhase.Tending;
            if (hooked) taut = FishFeelMath.LineTaut01(taut, s.Size01, s.SlackWindowOpen, in feel);

            // The hit-bottom / gone-slack POP: the belly kicks past rest and settles (RodLineMath).
            float sag = look.MaxSagM;
            if (s.SlackWindowOpen)
                sag *= RodLineMath.SlackOvershoot(slackEdgeSeconds, look.SlackOvershoot01, look.SlackSettleSeconds);

            RodLineMath.SampleLine(tip, far, taut, sag, _buffer);

            // Strain: whiten late, shudder near the snap (one visual language with the haul rope).
            bool fight = s.IsFightPhase;
            float whiten = fight ? RodLineMath.Whiten01(s.Tension01, look.WhitenLateBias) : 0f;
            float shudder01 = fight ? RodLineMath.StrainShudder01(s.Tension01, look.ShudderStart01) : 0f;
            if (hooked)
            {
                whiten = FishFeelMath.LineWhiten01(whiten, s.Size01, in feel);
                shudder01 = FishFeelMath.LineShudder01(shudder01, s.Size01, fightLoad01,
                                                       s.SlackWindowOpen, in feel);
            }

            // THE COLOUR AXIS IS THE INSTRUCTION (owner ask 2026-07-24 — readable, but no UI): the line
            // falls dull the moment she gives (REEL) and climbs to bar-white as it loads (EASE OFF).
            Color c = s.SlackWindowOpen && fight
                ? Color.Lerp(look.LineColor, look.SlackColor, 1f)
                : Color.Lerp(look.LineColor, look.StrainColor, whiten);
            _line.startColor = c;
            _line.endColor = c;

            int n = _buffer.Length;
            Vector2 span = far - tip;
            float len = span.magnitude;
            // Never more than a sliver of the span: a hum on a short line, not a zigzag (RodLineMath).
            float shudderAmp = RodLineMath.ShudderAmplitude(look.ShudderAmpM * shudder01, len);
            Vector2 perp = len > 1e-4f ? new Vector2(-span.y, span.x) / len : Vector2.up;
            float phase = shudderSeconds * look.ShudderHz * (2f * Mathf.PI);

            if (!_line.enabled) _line.enabled = true;
            // ADR 0059: the line rides the fisher's own order, behind him (Core EffectSorting.RodLineRungs).
            // One int, written only when it changes.
            if (sorted && _line.sortingOrder != sortingOrder) _line.sortingOrder = sortingOrder;
            _line.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = _buffer[i];
                if (shudderAmp > 1e-5f)
                    p += perp * RodLineMath.ShudderOffset(i, n, phase, shudderAmp);
                _line.SetPosition(i, new Vector3(p.x, p.y, 0f));
            }
        }

        /// <summary>Take the line out of the picture (and its points with it).</summary>
        public void Hide()
        {
            if (_line == null) return;
            _line.enabled = false;
            _line.positionCount = 0;
        }
    }
}
