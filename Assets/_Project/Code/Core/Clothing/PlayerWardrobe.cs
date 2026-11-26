using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One player's clothes</b> (store clothing, pass 1): what he owns, what he wears in each slot, and
    /// the effects of what he wears, summed. One instance per player, never a static, so two to four co-op
    /// players each keep their own (<see cref="IClothingService.For"/>).
    ///
    /// <para><b>Owning and wearing are separate.</b> Only an owned garment can be worn; a garment is owned
    /// once (no duplicates). Trying a garment on is not here at all: the try-on page holds its own tries,
    /// so nothing it does can change this until he buys.</para>
    ///
    /// <para><b>Reads are allocation-free.</b> The summed effects live in two small arrays rebuilt only
    /// when what he wears changes; the needs and dirt seams read them on their own ticks.</para>
    ///
    /// <para><b>Session-only for now.</b> Nothing here is saved: the save holds no row for garments owned
    /// or worn (docs/design/store-clothing.md, "Save"). A save step is the owner's call.</para>
    /// </summary>
    public sealed class PlayerWardrobe
    {
        private static readonly int NeedCount = MaxValue(typeof(NeedKind)) + 1;
        private static readonly int DirtCount = MaxValue(typeof(DirtActivity)) + 1;

        private readonly List<ClothingDef> _owned = new List<ClothingDef>();
        private readonly ClothingDef[] _worn = new ClothingDef[ClothingSlots.Count];
        private readonly float[] _relief = new float[NeedCount];
        private readonly float[] _dirt = new float[DirtCount];

        public PlayerWardrobe(string playerKey)
        {
            PlayerKey = playerKey ?? "";
            Rebuild();
        }

        /// <summary>Whose clothes these are (<see cref="ClothingPlayers.Local"/> for the local player).</summary>
        public string PlayerKey { get; }

        /// <summary>Goes up by one on every change to what he owns or wears. A reader may cache on it.</summary>
        public int Revision { get; private set; }

        /// <summary>Every garment he owns, in the order he got them.</summary>
        public IReadOnlyList<ClothingDef> Owned => _owned;

        public bool Owns(ClothingDef def) => def != null && _owned.Contains(def);

        public bool Owns(string clothingId)
        {
            if (string.IsNullOrEmpty(clothingId)) return false;
            for (int i = 0; i < _owned.Count; i++)
                if (string.Equals(_owned[i].Id, clothingId, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>The garment he wears in <paramref name="slot"/>, or null.</summary>
        public ClothingDef WornIn(ClothingSlot slot) => ClothingSlots.IsValid(slot) ? _worn[(int)slot] : null;

        public bool IsWearing(ClothingDef def) =>
            def != null && ClothingSlots.IsValid(def.Slot) && ReferenceEquals(_worn[(int)def.Slot], def);

        /// <summary>He now owns <paramref name="def"/> (bought or given). False, changing nothing, for a
        /// null or unslotted garment or one he already owns.</summary>
        public bool Grant(ClothingDef def)
        {
            if (def == null || !ClothingSlots.IsValid(def.Slot) || Owns(def) || Owns(def.Id)) return false;
            _owned.Add(def);
            Changed();
            return true;
        }

        /// <summary>He puts on <paramref name="def"/>, taking off whatever was in its slot. False, changing
        /// nothing, for a garment he does not own or is already wearing.</summary>
        public bool Wear(ClothingDef def)
        {
            if (!Owns(def) || IsWearing(def)) return false;
            _worn[(int)def.Slot] = def;
            Changed();
            return true;
        }

        /// <summary>He takes off whatever is in <paramref name="slot"/>. False when it is already empty.</summary>
        public bool TakeOff(ClothingSlot slot)
        {
            if (!ClothingSlots.IsValid(slot) || _worn[(int)slot] == null) return false;
            _worn[(int)slot] = null;
            Changed();
            return true;
        }

        /// <summary>What is left of <paramref name="need"/>'s penalty with what he wears: the product of his
        /// garments' reliefs on it, exactly 1 with none.</summary>
        public float NeedRelief(NeedKind need)
        {
            int i = (int)need;
            return i >= 0 && i < _relief.Length ? _relief[i] : 1f;
        }

        /// <summary>What is left of the dirt <paramref name="activity"/> adds with what he wears: the product
        /// of his garments' dirt scales on it, exactly 1 with none.</summary>
        public float DirtScale(DirtActivity activity)
        {
            int i = (int)activity;
            return i >= 0 && i < _dirt.Length ? _dirt[i] : 1f;
        }

        private void Changed()
        {
            Revision++;
            Rebuild();
            EventBus.Publish(new PlayerClothingChanged(PlayerKey));
        }

        private void Rebuild()
        {
            for (int i = 0; i < _relief.Length; i++) _relief[i] = 1f;
            for (int i = 0; i < _dirt.Length; i++) _dirt[i] = 1f;
            for (int s = 0; s < _worn.Length; s++)
            {
                ClothingEffect[] effects = _worn[s] != null ? _worn[s].Effects : null;
                if (effects == null) continue;
                for (int e = 0; e < effects.Length; e++)
                {
                    ClothingEffect fx = effects[e];
                    switch (fx.Kind)
                    {
                        case ClothingEffectKind.NeedRelief:
                            if ((int)fx.Need >= 0 && (int)fx.Need < _relief.Length) _relief[(int)fx.Need] *= fx.SafeScale;
                            break;
                        case ClothingEffectKind.DirtScale:
                            if ((int)fx.Activity >= 0 && (int)fx.Activity < _dirt.Length) _dirt[(int)fx.Activity] *= fx.SafeScale;
                            break;
                    }
                }
            }
        }

        private static int MaxValue(Type enumType)
        {
            int max = 0;
            foreach (object v in Enum.GetValues(enumType))
            {
                int n = Convert.ToInt32(v);
                if (n > max) max = n;
            }
            return max;
        }
    }

    /// <summary>What one player owns or wears changed. Read it back through
    /// <see cref="IClothingService.For"/>.</summary>
    public readonly struct PlayerClothingChanged
    {
        /// <summary>Whose clothes changed.</summary>
        public readonly string PlayerKey;

        public PlayerClothingChanged(string playerKey) { PlayerKey = playerKey ?? ""; }
    }
}
