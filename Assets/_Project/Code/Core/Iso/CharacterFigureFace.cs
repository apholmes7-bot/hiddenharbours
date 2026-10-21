namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Which three face groups a figure shows on a frame</b> — the clip frame's own face, then the
    /// gaze, then the blink, in the rig's order (README §4 and §5).
    ///
    /// <list type="number">
    /// <item>The clip frame's face track names one group per slot (<see cref="CharacterSkinDef.SkinClip.Face"/>);
    /// a clip with none shows the rest face.</item>
    /// <item>A gaze (<c>eyes.left</c> / <c>eyes.right</c>) replaces the OPEN eyes only; half, shut
    /// and wide stay as the clip has them.</item>
    /// <item>A blink shows its eyes group over any eyes but the ones the def's
    /// <see cref="CharacterSkinDef.BlinkSkipGroups"/> name (sleep keeps its closed eyes), judged on
    /// the frame's OWN eyes.</item>
    /// </list>
    ///
    /// <para>Pure and allocation-free: a presenter asks every frame.</para>
    /// </summary>
    public static class CharacterFigureFace
    {
        /// <summary>
        /// The groups (1-based into <see cref="CharacterSkinDef.FaceGroups"/>) for
        /// <paramref name="frame"/> of <paramref name="clip"/>, with a gaze
        /// (<see cref="CharacterFigureLook.GazeOpen"/> for none) and a blink's eyes group
        /// (<see cref="CharacterSkinDef.NoFaceGroup"/> for none). All three come back
        /// <see cref="CharacterSkinDef.NoFaceGroup"/> for a def with no face.
        /// </summary>
        public static void Compose(CharacterSkinDef def, in CharacterSkinDef.SkinClip clip, int frame,
                                   int gaze, int blinkEyes, out int eyes, out int brows, out int mouth)
        {
            eyes = brows = mouth = CharacterSkinDef.NoFaceGroup;
            if (def == null || !def.HasFace) return;

            eyes = SlotOf(def, clip, frame, CharacterSkinDef.EyesSlot);
            brows = SlotOf(def, clip, frame, CharacterSkinDef.BrowsSlot);
            mouth = SlotOf(def, clip, frame, CharacterSkinDef.MouthSlot);

            int own = eyes;
            if (gaze != CharacterFigureLook.GazeOpen && own == def.LookEyes.x && own != CharacterSkinDef.NoFaceGroup)
            {
                int turned = gaze < 0 ? def.LookEyes.y : def.LookEyes.z;
                if (turned != CharacterSkinDef.NoFaceGroup) eyes = turned;
            }
            if (blinkEyes != CharacterSkinDef.NoFaceGroup && !Skips(def, own)) eyes = blinkEyes;
        }

        /// <summary>True when a blink leaves these eyes alone.</summary>
        public static bool Skips(CharacterSkinDef def, int eyes)
        {
            if (def?.BlinkSkipGroups == null) return false;
            foreach (int g in def.BlinkSkipGroups)
                if (g == eyes) return true;
            return false;
        }

        static int SlotOf(CharacterSkinDef def, in CharacterSkinDef.SkinClip clip, int frame, int slot)
        {
            int g = clip.FaceGroupOf(frame, slot);
            if (g != CharacterSkinDef.NoFaceGroup) return g;
            return def.RestFace != null && slot < def.RestFace.Length ? def.RestFace[slot] : CharacterSkinDef.NoFaceGroup;
        }
    }
}
