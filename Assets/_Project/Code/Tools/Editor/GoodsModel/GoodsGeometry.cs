using System;

namespace HiddenHarbours.Tools.GoodsModel
{
    // Integer metric units make boundary fits and repeat runs exact. Only yaw rotation is allowed.
    [Serializable]
    public struct GoodsBox
    {
        public int LengthMm;
        public int WidthMm;
        public int HeightMm;

        public GoodsBox(int lengthMm, int widthMm, int heightMm)
        {
            LengthMm = lengthMm;
            WidthMm = widthMm;
            HeightMm = heightMm;
        }

        public bool IsValid => LengthMm > 0 && WidthMm > 0 && HeightMm > 0;
        public decimal VolumeMm3 => (decimal)LengthMm * WidthMm * HeightMm;

        public bool Fits(GoodsBox bounds, bool rotate = false)
        {
            return IsValid && bounds.IsValid && HeightMm <= bounds.HeightMm &&
                ((LengthMm <= bounds.LengthMm && WidthMm <= bounds.WidthMm) ||
                 (rotate && WidthMm <= bounds.LengthMm && LengthMm <= bounds.WidthMm));
        }
    }

    [Serializable]
    public struct GoodsSlot
    {
        public int Xmm;
        public int Ymm;
        public int Zmm;
        public GoodsBox Size;

        public bool IsInside(GoodsBox bounds)
        {
            return Size.IsValid && Xmm >= 0 && Ymm >= 0 && Zmm >= 0 &&
                (long)Xmm + Size.LengthMm <= bounds.LengthMm &&
                (long)Ymm + Size.WidthMm <= bounds.WidthMm &&
                (long)Zmm + Size.HeightMm <= bounds.HeightMm;
        }

        public bool Overlaps(GoodsSlot other)
        {
            return Xmm < (long)other.Xmm + other.Size.LengthMm && other.Xmm < (long)Xmm + Size.LengthMm &&
                Ymm < (long)other.Ymm + other.Size.WidthMm && other.Ymm < (long)Ymm + Size.WidthMm &&
                Zmm < (long)other.Zmm + other.Size.HeightMm && other.Zmm < (long)Zmm + Size.HeightMm;
        }
    }
}
