using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// How a fishing line looks: the owner-tunable numbers the shared line (Player's <c>RodLineStrand</c>)
    /// draws with. Plain <see cref="SerializableAttribute"/> data, so a World activity Def can carry a
    /// villager's line look as authored data. Built by its owner on every render (the player's
    /// <c>RodFightPresenter</c> from its own inspector fields, a villager's presenter from the stand's
    /// look), so a value tuned live is in the very next frame and nothing is cached. No defaults live
    /// here: every number comes from the owner's data (rule 6). Moved from Player (ADR 0053 §11).
    /// </summary>
    [Serializable]
    public struct RodLineLook
    {
        public float MaxSagM;
        public Color LineColor, StrainColor, SlackColor;
        public float WhitenLateBias, ShudderStart01, ShudderAmpM, ShudderHz;
        public float RestTaut, SinkTaut, FightTautFloor;
        public float SlackOvershoot01, SlackSettleSeconds;
    }
}
