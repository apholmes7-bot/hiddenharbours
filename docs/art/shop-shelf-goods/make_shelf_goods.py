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
    "p": (190, 150, 98),    # paper bag, kraft
    "r": (150, 112, 66),    # paper bag shade
    "L": (214, 180, 128),   # paper bag fold
    "P": (205, 120, 120),   # sea worm
    "R": (70, 50, 35),      # worm box soil
    "Y": (236, 226, 220),   # squid strip
    "y": (214, 170, 172),   # squid strip edge
    "c": (226, 200, 150),   # shucked clam
    "o": (196, 150, 96),    # clam shade
    "g": (60, 62, 66),      # rod butt and tip
    "t": (128, 84, 46),     # rod blank, varnished
}

# The fishing section (2026-11-25): four baits on the third shelf, each two tubs or boxes, then the rods
# lying along the bottom shelf. Six rows tall, to fit band 3 (rows 245..250).
CAPELIN_TUB = [
    ".DDDDD.",
    "DsszssD",
    "DzssszD",
    "DkkkkkD",
    "DmmmmmD",
    ".DDDDD.",
]
WORM_BOX = [
    ".DDDDD.",
    "DRPRPRD",
    "DPRRPRD",
    "DkkkkkD",
    "DqqqqqD",
    ".DDDDD.",
]
SQUID_TRAY = [
    ".jjjjj.",
    "jYyYyYj",
    "jyYyYyj",
    "jYyYyYj",
    "jBBBBBj",
    ".jjjjj.",
]
CLAM_TUB = [
    ".DDDDD.",
    "DcocoyD",
    "DoccocD",
    "DlllllD",
    "DBBBBBD",
    ".DDDDD.",
]
BAND3_ROW = 245
BAITS_X = {"capelin": 1, "worm": 16, "squid": 31, "clam": 46}

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

# The paper shopping bag in his hand (buying at the shelf, slice 2): a kraft bag with a loaf showing.
PAPER_BAG = [
    "...kk.....",
    "..kmq.....",
    "DDkmqDDDDD",
    "DLLLLLLLLD",
    "DppppppprD",
    "DppppppprD",
    "DppppppprD",
    "DppppppprD",
    "DppppppprD",
    "DppppppprD",
    "DppppppprD",
    "DrrrrrrrrD",
    "DDDDDDDDDD",
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
    # The three foods, each on the gondola's canvas, pivoted at its own slot (each good is counted on its
    # own line, slice 2). Store sections (2026-11-25): all three share the FOOD section in the top two
    # bands (bread left and oatcakes right on the top shelf, beans on the second), so the lower two bands
    # are free for the fishing section. Each pivot is the middle of its own goods, where its card stands.
    "ShelfGondolaBread": (15, 34),
    "ShelfGondolaOatcakes": (47, 33),
    "ShelfGondolaBeans": (31, 20),
    # The fishing section: the four baits on band 3, each pivoted at the middle of its two tubs, and the
    # rods along band 4.
    "ShelfGondolaCapelin": (BAITS_X["capelin"] + 7, 10),
    "ShelfGondolaSeaWorm": (BAITS_X["worm"] + 7, 10),
    "ShelfGondolaSquidStrip": (BAITS_X["squid"] + 7, 10),
    "ShelfGondolaShuckedClam": (BAITS_X["clam"] + 7, 10),
    "ShelfGondolaRods": (31, 2),
    # Held: the bottom centre; the carry pose hangs it its own height below the hand.
    "ShoppingBagPaper": (5, 0),
}
ROOM_RECT = {
    "ShelfGondolaEmpty": (GONDOLA_X0, GONDOLA_Y0, GONDOLA_W, GONDOLA_H),
    "ShelfGondolaGoods": (GONDOLA_X0, GONDOLA_Y0, GONDOLA_W, GONDOLA_H),
    "ShelfIceChestEmpty": (ICE_X0, ICE_Y0, ICE_W, ICE_H),
    "ShelfIceBags": (ICE_X0, ICE_Y0, ICE_W, ICE_H),
    "ShelfBaitBox": (BAIT_X0, BAIT_Y0, len(BAIT_BOX[0]), len(BAIT_BOX)),
    "ShelfGondolaBread": (GONDOLA_X0, GONDOLA_Y0, GONDOLA_W, GONDOLA_H),
    "ShelfGondolaOatcakes": (GONDOLA_X0, GONDOLA_Y0, GONDOLA_W, GONDOLA_H),
    "ShelfGondolaBeans": (GONDOLA_X0, GONDOLA_Y0, GONDOLA_W, GONDOLA_H),
    "ShelfGondolaCapelin": (GONDOLA_X0, GONDOLA_Y0, GONDOLA_W, GONDOLA_H),
    "ShelfGondolaSeaWorm": (GONDOLA_X0, GONDOLA_Y0, GONDOLA_W, GONDOLA_H),
    "ShelfGondolaSquidStrip": (GONDOLA_X0, GONDOLA_Y0, GONDOLA_W, GONDOLA_H),
    "ShelfGondolaShuckedClam": (GONDOLA_X0, GONDOLA_Y0, GONDOLA_W, GONDOLA_H),
    "ShelfGondolaRods": (GONDOLA_X0, GONDOLA_Y0, GONDOLA_W, GONDOLA_H),
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


def gondola_bread(im=None):
    im = im or blank(GONDOLA_W, GONDOLA_H)
    oy = -GONDOLA_Y0
    # Top shelf, left half: two rows of loaves, the front row set half a loaf over.
    for i in range(3):
        stamp(im, LOAF, 1 + i * 10, 217 + oy)
    for i in range(2):
        stamp(im, LOAF, 6 + i * 10, 224 + oy)
    return im


def gondola_oatcakes(im=None):
    im = im or blank(GONDOLA_W, GONDOLA_H)
    oy = -GONDOLA_Y0
    # Top shelf, right half: oatcake packets stacked two high.
    for i in range(4):
        stamp(im, OATCAKES, 33 + i * 7, 219 + oy)
        stamp(im, OATCAKES, 33 + i * 7, 226 + oy)
    return im


def gondola_beans(im=None):
    im = im or blank(GONDOLA_W, GONDOLA_H)
    oy = -GONDOLA_Y0
    # Second shelf: a row of tins of beans.
    for i in range(10):
        stamp(im, TIN_TALL, 1 + i * 6, 236 + oy)
    return im


def gondola_bait(pattern, key):
    im = blank(GONDOLA_W, GONDOLA_H)
    x = BAITS_X[key]
    stamp(im, pattern, x, BAND3_ROW - GONDOLA_Y0)
    stamp(im, pattern, x + 7, BAND3_ROW - GONDOLA_Y0)
    return im


def gondola_rods():
    # Bottom shelf (rows 254..257): four rods lying along it, two to a row, end to end, the lower pair
    # set two pixels over: a dark butt, a cork grip, a reel seat, then the varnished blank to the tip.
    im = blank(GONDOLA_W, GONDOLA_H)
    for row, shift in ((255, 0), (257, 2)):
        y = row - GONDOLA_Y0
        for x0 in (2 + shift, 31 + shift):
            put(im, x0, y, "g")
            for x in range(x0 + 1, x0 + 6):
                put(im, x, y, "c")
            put(im, x0 + 6, y, "s")
            put(im, x0 + 7, y, "g")
            for x in range(x0 + 8, x0 + 27):
                put(im, x, y, "t")
            put(im, x0 + 26, y, "g")
    return im


def gondola_goods():
    # All three bands at once (the slice 1 sprite, kept for reference; slice 2 draws each band on its own).
    im = blank(GONDOLA_W, GONDOLA_H)
    gondola_bread(im)
    gondola_oatcakes(im)
    gondola_beans(im)
    return im


def paper_bag():
    im = blank(len(PAPER_BAG[0]), len(PAPER_BAG))
    stamp(im, PAPER_BAG, 0, 0)
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
        "ShelfGondolaBread": gondola_bread(),
        "ShelfGondolaOatcakes": gondola_oatcakes(),
        "ShelfGondolaBeans": gondola_beans(),
        "ShoppingBagPaper": paper_bag(),
        "ShelfGondolaCapelin": gondola_bait(CAPELIN_TUB, "capelin"),
        "ShelfGondolaSeaWorm": gondola_bait(WORM_BOX, "worm"),
        "ShelfGondolaSquidStrip": gondola_bait(SQUID_TRAY, "squid"),
        "ShelfGondolaShuckedClam": gondola_bait(CLAM_TUB, "clam"),
        "ShelfGondolaRods": gondola_rods(),
    }
    for name, im in made.items():
        im.save(os.path.join(out, name + ".png"))
        w, h = im.size
        px, py = PIVOT[name]
        if name in ROOM_RECT:
            ox, oy = offset_of(name)
            print("%s %dx%d pivot (%.6f, %.6f) offset (%.5f, %.5f)" % (name, w, h, px / w, py / h, ox, oy))
        else:
            print("%s %dx%d pivot (%.6f, %.6f) held" % (name, w, h, px / w, py / h))


if __name__ == "__main__":
    main()
