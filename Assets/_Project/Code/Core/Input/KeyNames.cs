namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The one table of key and button names</b> every prompt writes its key through. A hint never
    /// spells a key itself: it names the key from here, so "E" is written one way, "Esc/B" one way, and
    /// a rebind is one line.
    ///
    /// <para><b>How a key is written.</b> The key, a colon, then the action in lower case: "E: talk".
    /// Where a hint serves both hands it names the key and the pad button with a slash: "Esc/B". Parts
    /// of one hint are set three spaces apart (<see cref="Gap"/>). Every name uses only letters the
    /// pixel face has.</para>
    ///
    /// <para>All members are constants, so a hint built from them is still one interned literal and
    /// costs nothing per frame (rule 7).</para>
    /// </summary>
    public static class KeyNames
    {
        // ---- keyboard and mouse ---------------------------------------------------------------
        public const string Interact = "E";
        public const string Back = "Esc";
        public const string Tab = "Tab";
        public const string TideTable = Tab;
        public const string Confirm = "Enter";
        public const string Space = "Space";
        public const string Backspace = "Backspace";
        public const string Randomise = "R";
        public const string Arrows = "Arrows";
        public const string UpDown = "Up/down";
        public const string LeftRight = "Left/right";
        public const string WalkUpDown = "W/S";
        public const string WalkLeftRight = "A/D";
        public const string LeftMouse = "left mouse";

        // ---- gamepad ----------------------------------------------------------------------------
        public const string PadConfirm = "A";
        public const string PadBack = "B";
        public const string PadX = "X";
        public const string PadY = "Y";
        public const string PadStart = "Start";
        public const string PadStick = "stick";
        public const string PadDpad = "d-pad";
        public const string PadShoulders = "shoulders";

        // ---- both hands -------------------------------------------------------------------------
        public const string BackEither = Back + "/" + PadBack;            // Esc/B
        public const string ConfirmEither = Confirm + "/" + PadConfirm;   // Enter/A
        public const string ArrowsEither = Arrows + "/" + PadStick;       // Arrows/stick

        // ---- how a hint is set ------------------------------------------------------------------
        /// <summary>Between a key and what it does.</summary>
        public const string Says = ": ";
        /// <summary>Between two parts of one hint.</summary>
        public const string Gap = "   ";
    }
}
