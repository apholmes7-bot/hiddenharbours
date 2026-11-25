"""The St Peters store's planned back-shelf goods, one picture each (store sections, 2026-11-25).

Every good the store plans to sell gets its own picture, drawn here at the store's pixel scale (32 px a
metre) in the shelf goods' palette, pivoted at its bottom centre so it stands on whatever shelf, hook or
floor spot its slot names. They are ready to bind: a slot, or the household goods' item Defs, points at
them when the good is stocked (docs/design/st-peters-store-sections.md).

    python make_store_items.py <out_dir>
"""
import os
import sys

from PIL import Image

P = {
    ".": None,
    "D": (58, 40, 30),      # outline, dark wood
    "w": (150, 104, 62),    # wood
    "W": (188, 140, 88),    # wood light
    "s": (176, 182, 188),   # steel
    "S": (220, 224, 228),   # steel light
    "k": (90, 94, 100),     # steel dark
    "r": (176, 64, 48),     # red paint
    "R": (214, 98, 70),     # red light
    "b": (70, 104, 150),    # blue tin
    "B": (110, 146, 190),   # blue light
    "y": (226, 196, 96),    # straw, wick flame
    "Y": (246, 232, 150),   # pale yellow
    "c": (238, 232, 214),   # candle wax, label
    "C": (206, 198, 176),   # wax shade
    "o": (120, 92, 52),     # oakum, tarred hemp
    "O": (162, 128, 78),    # oakum light
    "n": (196, 120, 84),    # copper
    "N": (232, 160, 112),   # copper light
    "g": (92, 128, 74),     # green label
    "p": (238, 214, 220),   # soap pink
    "q": (206, 176, 186),   # soap shade
    "h": (196, 170, 120),   # sacking
    "H": (164, 138, 92),    # sacking shade
    "a": (232, 208, 120),   # lamp oil, amber
    "A": (196, 160, 70),    # oil shade
}

ITEMS = {
    # gear.shovel: a clam shovel, short handle, square blade.
    "StoreItem_Shovel": [
        "..DWD..",
        "..DwD..",
        "..DwD..",
        "..DwD..",
        "..DwD..",
        "..DwD..",
        "..DkD..",
        ".DsSsD.",
        ".DsSsD.",
        ".DssSD.",
        ".DsssD.",
        "..DDD..",
    ],
    # gear.bucket: a galvanised bucket with its wire handle.
    "StoreItem_Bucket": [
        "..DDDDDD..",
        ".D......D.",
        "DDDDDDDDDD",
        "DSssssssSD",
        "DSsksssskD",
        ".DSssssD..",
        ".DsssssD..",
        ".DSsksSD..",
        "..DDDDD...",
    ],
    # gear.shellfish_rake: long handle, toothed head.
    "StoreItem_Rake": [
        "...DWD...",
        "...DwD...",
        "...DwD...",
        "...DwD...",
        "...DwD...",
        "...DwD...",
        "DDDDDDDDD",
        "DsSsSsSsD",
        "Dk.k.k.kD",
        ".k.k.k.k.",
    ],
    # gear.shellfish_basket: a woven wire basket.
    "StoreItem_Basket": [
        ".DDDDDDDD.",
        "DsDsDsDsDs",
        "DkSkSkSkSD",
        "DsDsDsDsDD",
        "DkSkSkSkSD",
        ".DsDsDsDD.",
        "..DDDDDD..",
    ],
    # gear.mussel_sack: a mesh sack tied at the neck.
    "StoreItem_MusselSack": [
        "...DD...",
        "...hD...",
        "..DHhD..",
        ".DhHhHD.",
        "DhHhHhHD",
        "DHhHhHhD",
        "DhHhHhHD",
        ".DDDDDD.",
    ],
    # gear.oyster_tongs: two long wooden shafts, steel heads.
    "StoreItem_Tongs": [
        "DD...DD",
        "Dw...wD",
        ".Dw.wD.",
        ".Dw.wD.",
        "..DwD..",
        "..DkD..",
        ".DkDkD.",
        ".Dk.kD.",
        "DsD.DsD",
        "DS...SD",
    ],
    # mend: a patch plank, a short board standing on end.
    "StoreItem_Plank": [
        "DDDD",
        "DWwD",
        "DwWD",
        "DWwD",
        "DwwD",
        "DWwD",
        "DwWD",
        "DWwD",
        "DwwD",
        "DDDD",
    ],
    # mend: copper nails, a little paper box with nails spilling.
    "StoreItem_Nails": [
        "DDDDDDD.",
        "DcccccD.",
        "DcnNncD.",
        "DcccccD.",
        "DDDDDDD.",
        ".n.N..n.",
    ],
    # mend: oakum, a coil of tarred hemp.
    "StoreItem_Oakum": [
        "..DDDD..",
        ".DoOoOD.",
        "DoODDOoD",
        "DOoD.DOD",
        "DoODDOoD",
        ".DOoOoD.",
        "..DDDD..",
    ],
    # mend: linseed paint, a red tin with a lid rim.
    "StoreItem_Paint": [
        ".DDDDD.",
        "DkkkkkD",
        "DRrrrRD",
        "DrcccrD",
        "DrcgcrD",
        "DRrrrRD",
        ".DDDDD.",
    ],
    # supply.soap_bar: a wrapped bar.
    "StoreItem_Soap": [
        ".DDDDDD.",
        "DpppppqD",
        "DpcccpqD",
        "DpppppqD",
        ".DDDDDD.",
    ],
    # household: a box of matches, the striker on its side.
    "StoreItem_Matches": [
        "DDDDDDD",
        "DrRrRrD",
        "DcYcYcD",
        "DrrrrrD",
        "DkkkkkD",
        "DDDDDDD",
    ],
    # household: a bundle of three candles, tied.
    "StoreItem_Candles": [
        ".y.y.y.",
        ".D.D.D.",
        "DcDcDcD",
        "DcDCDcD",
        "DhhhhhD",
        "DcDCDcD",
        "DcDcDcD",
        "DCDCDCD",
        "DDDDDDD",
    ],
    # household: a tin of lamp oil, spout and handle.
    "StoreItem_LampOil": [
        "....DD..",
        "...DkD..",
        ".DDDDDD.",
        "DbbbbbBD",
        "DbaaaabD",
        "DbaAaabD",
        "DbbbbbBD",
        "DDDDDDDD",
    ],
}


def draw(pattern):
    w, h = len(pattern[0]), len(pattern)
    assert all(len(r) == w for r in pattern), pattern
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for y, row in enumerate(pattern):
        for x, key in enumerate(row):
            c = P[key]
            if c is not None:
                im.putpixel((x, y), c + (255,))
    return im


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for name, pattern in ITEMS.items():
        im = draw(pattern)
        im.save(os.path.join(out, name + ".png"))
        print("%s %dx%d pivot (0.5, 0)" % (name, im.width, im.height))


if __name__ == "__main__":
    main()
