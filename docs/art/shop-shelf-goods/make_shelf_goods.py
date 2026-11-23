"""Draws the St Peters general store's shelf goods and empty-shelf covers.

Source for the PNGs under Assets/_Project/Art/Sprites/Buildings/Shops/ShelfGoods/. Run it again after
changing a pattern here; it overwrites those PNGs and prints, for each sprite, the slot offset (metres
from the store's ground point) that lines it up with the baked room picture, so the shelf-slot asset can
be checked against it. It does not touch the .meta files (their pivots are the PIVOT values below).

The room picture is ShopLevel_generalStore_ground.png at facing d4: 412 px per facing, 347 px high,
32 px per metre, pivot at (206, 133) px from its bottom-left. Room rects below are in that facing's
pixels, measured from the top-left.

    python -I make_shelf_goods.py <path to Assets/_Project/Art/Sprites/Buildings/Shops/ShelfGoods>
"""
import os
import sys

from PIL import Image

PPU = 32
ROOM_H = 347
ROOM_PIVOT = (206, 133)  # px from the room picture's bottom-left

# The room's own palette (sampled from the picture) plus a few colours for the new goods.
P = {
    ".": None,
    "a": (198, 92, 69),     # shelf back, red
    "b": (154, 52, 40),     # shelf shadow
    "e": (129, 42, 32),
    "x": (98, 30, 23),      # shelf lip
    "E": (71, 22, 18),
    "d": (208, 210, 202),   # white shelf rule
    "D": (84, 56, 35),      # wood outline (bread, packets, box)
    "k": (209, 184, 126),   # crust / card light
    "m": (187, 159, 98),    # crust mid
    "q": (147, 120, 45),    # crust dark
    "l": (237, 224, 190),   # score line / paper
    "B": (95, 130, 165),    # blue label
    "s": (184, 184, 177),   # tin top / fish silver
    "v": (204, 204, 197),   # tin rim
    "z": (115, 125, 130),   # tin foot / fish back
    "O": (196, 96, 40),     # beans label
    "I": (150, 60, 25),     # beans label band
    "K": (60, 66, 72),      # chest outline
    "W": (226, 228, 224),   # chest rim
    "N": (78, 100, 118),    # chest inside, empty
    "F": (104, 132, 150),   # frost speckle
    "G": (60, 80, 96),      # chest inside shadow
    "w": (208, 212, 206),   # chest body
    "h": (190, 194, 190),   # chest body shade
    "u": (170, 175, 170),   # chest foot shade
    "j": (70, 100, 135),    # blue band edge
    "T": (70, 110, 160),    # bag tie
    "C": (232, 240, 244),   # bag
    "H": (250, 252, 252),   # bag highlight
    "S": (180, 200, 214),   # bag shade
}

LOAF = [
    ".DDDDDDD.",
    "DkkklkkkD",
    "DkmlkmlmD",
    "DmmmmmmmD",
    "DqmmmmmqD",
    "DqqqqqqqD",
    ".DDDDDDD.",
]

OATCAKES = [
    "DDDDDD",
    "DllllD",
    "DBBBBD",
    "DkmkmD",
    "DmkmkD",
    "DDDDDD",
]

TIN_TALL = [
    ".sss.",
    "svvvs",
    "OOOOO",
    "OIIIO",
    "OOOOO",
    ".zzz.",
]

TIN_SHORT = [
    ".sss.",
    "svvvs",
    "OIIIO",
    "OOOOO",
    ".zzz.",
]

ICE_BAG = [
    "..T..",
    ".CCC.",
    "CCHCC",
    "CCCCC",
    "CSSSC",
    ".SSS.",
]

BAIT_BOX = [
    ".DDDDDDDDDD.",
    "DkkkkkkkkkkD",
    "DqsssszssszD",
    "DssszqssszqD",
    "DqzsssqzsssD",
    "DqqqqqqqqqqD",
    "DkkkkkkkkkkD",
    "DmmmmmmmmmmD",
    ".DDDDDDDDDD.",
]

# The gondola: room rect x 151..212, rows 214..257 (its top edge and the four goods bands under the
# white rules). Left of TOP_EDGE_FROM_X the top two rows stay clear: the stove stands there.
GONDOLA_X0, GONDOLA_Y0, GONDOLA_W, GONDOLA_H = 151, 214, 62, 44
TOP_EDGE_ROWS = {214: "D", 215: "x"}
TOP_EDGE_FROM_X = 180
RULES = {233, 242, 243, 251, 252}
LIP_ROWS = {234}            # dark lip under a rule, as the room draws it
SHADOW_ROWS = {216, 235, 244, 253}

ICE_X0, ICE_Y0, ICE_W, ICE_H = 241, 243, 26, 20
BAIT_X0, BAIT_Y0 = 275, 220

# Pivot of each sprite, px from its bottom-left: the slot offset is where this point lands.
PIVOT = {
    "ShelfGondolaEmpty": (31, 17),
    "ShelfGondolaGoods": (31, 17),
    "ShelfIceChestEmpty": (13, 11),
    "ShelfIceBags": (13, 11),
    "ShelfBaitBox": (6, 0),
}
ROOM_RECT = {
    "ShelfGondolaEmpty": (GONDOLA_X0, GONDOLA_Y0, GONDOLA_W, GONDOLA_H),
    "ShelfGondolaGoods": (GONDOLA_X0, GONDOLA_Y0, GONDOLA_W, GONDOLA_H),
    "ShelfIceChestEmpty": (ICE_X0, ICE_Y0, ICE_W, ICE_H),
    "ShelfIceBags": (ICE_X0, ICE_Y0, ICE_W, ICE_H),
    "ShelfBaitBox": (BAIT_X0, BAIT_Y0, len(BAIT_BOX[0]), len(BAIT_BOX)),
}


def blank(w, h):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put(im, x, y, key):
    c = P[key]
    if c is None:
        return
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c + (255,))


def stamp(im, pattern, x0, y0):
    for dy, row in enumerate(pattern):
        for dx, key in enumerate(row):
            put(im, x0 + dx, y0 + dy, key)


def gondola_empty():
    im = blank(GONDOLA_W, GONDOLA_H)
    for ry in range(GONDOLA_Y0, GONDOLA_Y0 + GONDOLA_H):
        y = ry - GONDOLA_Y0
        for x in range(GONDOLA_W):
            if ry in TOP_EDGE_ROWS:
                key = TOP_EDGE_ROWS[ry] if GONDOLA_X0 + x >= TOP_EDGE_FROM_X else "."
            elif ry in RULES:
                key = "d"
            elif ry in LIP_ROWS:
                key = "E" if x % 4 == 1 else "x"
            elif ry in SHADOW_ROWS:
                key = "b"
            else:
                key = "e" if x == 0 else "a"
            put(im, x, y, key)
    return im


def gondola_goods():
    im = blank(GONDOLA_W, GONDOLA_H)
    oy = -GONDOLA_Y0
    # Top shelf: two rows of loaves, the front row set half a loaf over.
    for i in range(6):
        stamp(im, LOAF, 1 + i * 10, 217 + oy)
    for i in range(5):
        stamp(im, LOAF, 6 + i * 10, 224 + oy)
    # Second shelf: oatcake packets.
    for i in range(8):
        stamp(im, OATCAKES, 2 + i * 7, 235 + oy)
    # Third and bottom shelves: tins of beans.
    for i in range(10):
        stamp(im, TIN_TALL, 1 + i * 6, 245 + oy)
    for i in range(10):
        stamp(im, TIN_SHORT, 3 + i * 6, 253 + oy)
    return im


def ice_chest_empty():
    im = blank(ICE_W, ICE_H)
    for y in range(ICE_H):
        for x in range(ICE_W):
            edge = x == 0 or x == ICE_W - 1
            if y == 0 or y == ICE_H - 1:
                key = "." if edge else "K"
            elif edge:
                key = "K"
            elif y == 1 or y == 9:
                key = "W"
            elif 2 <= y <= 8:
                if x == 1 or x == ICE_W - 2:
                    key = "W"
                elif y == 2:
                    key = "G"
                else:
                    key = "F" if (x * 7 + y * 3) % 11 == 0 else "N"
            elif y == 10:
                key = "h"
            elif y in (12, 13):
                key = "B"
            elif y == 14:
                key = "j"
            elif y == 18:
                key = "u"
            else:
                key = "w"
            put(im, x, y, key)
    return im


def ice_bags():
    im = blank(ICE_W, ICE_H)
    for i in range(4):
        stamp(im, ICE_BAG, 3 + i * 5, 3)
    return im


def bait_box():
    im = blank(len(BAIT_BOX[0]), len(BAIT_BOX))
    stamp(im, BAIT_BOX, 0, 0)
    return im


def offset_of(name):
    x0, y0, w, h = ROOM_RECT[name]
    px, py = PIVOT[name]
    bottom_from_bottom = ROOM_H - (y0 + h)
    return ((x0 + px - ROOM_PIVOT[0]) / PPU, (bottom_from_bottom + py - ROOM_PIVOT[1]) / PPU)


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    made = {
        "ShelfGondolaEmpty": gondola_empty(),
        "ShelfGondolaGoods": gondola_goods(),
        "ShelfIceChestEmpty": ice_chest_empty(),
        "ShelfIceBags": ice_bags(),
        "ShelfBaitBox": bait_box(),
    }
    for name, im in made.items():
        im.save(os.path.join(out, name + ".png"))
        w, h = im.size
        px, py = PIVOT[name]
        ox, oy = offset_of(name)
        print("%s %dx%d pivot (%.6f, %.6f) offset (%.5f, %.5f)" % (name, w, h, px / w, py / h, ox, oy))


if __name__ == "__main__":
    main()
