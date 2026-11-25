"""LeBlanc's store front: the shop sign and the three open-hours displays (store front, 2026-11-25).

Drawn at the village's pixel scale (32 px a metre), point filtered, each pivoted at its bottom centre so it
stands on the ground (or, for the board and its lamps, is fixed to the store's face) where the store front data puts it (Data/Resources/StoreFronts/StPetersGeneralStoreFront).

    python make_store_front.py <out_dir>

StoreFront_Sign          the name board on the face above the awning: "LeBlanc's", hidden while inside
StoreFront_SignNight     the board as its lamps light it after sunset, drawn over the night like a lit pane
StoreFront_LampLeft      a gooseneck lamp over the board's left end, lit at night; hidden while inside
StoreFront_LampRight     its mirror over the right end
StoreFront_PotatoCrate   a crate of potatoes on a low stand, out while the store is open
StoreFront_BuoyRack      a rack of painted buoys and a coil of rope, out while the store is open
StoreFront_BucketStand   galvanised buckets, a broom and a mop, out while the store is open
"""
import os
import sys

from PIL import Image

P = {
    "D": (52, 36, 28),      # outline, dark wood
    "w": (128, 86, 52),     # post wood
    "W": (166, 118, 72),    # post wood light
    "v": (98, 64, 40),      # post wood shade
    "c": (236, 226, 198),   # board paint, cream
    "C": (212, 198, 164),   # board paint shade
    "r": (160, 52, 40),     # store red (the awning's)
    "R": (198, 82, 62),     # store red light
    "i": (60, 34, 28),      # lettering ink
    "k": (112, 98, 70),     # crate slat shade
    "K": (176, 146, 96),    # crate slat
    "L": (206, 178, 122),   # crate slat light
    "p": (176, 136, 86),    # potato
    "P": (204, 166, 110),   # potato light
    "o": (136, 100, 60),    # potato shade
    "y": (232, 196, 70),    # buoy yellow
    "Y": (250, 228, 130),   # buoy yellow light
    "b": (70, 110, 160),    # buoy blue
    "B": (110, 150, 196),   # buoy blue light
    "x": (220, 222, 216),   # buoy white
    "h": (188, 164, 112),   # rope
    "H": (150, 126, 80),    # rope shade
    "s": (168, 174, 178),   # galvanised steel
    "S": (212, 216, 218),   # steel light
    "z": (110, 116, 122),   # steel dark
    "g": (196, 170, 96),    # broom straw
    "G": (160, 132, 64),    # broom straw shade
    "m": (226, 222, 210),   # mop head
    "M": (190, 186, 172),   # mop shade
    "q": (52, 84, 64),      # board paint, island green
    "Q": (72, 108, 84),     # board paint, green light
    "j": (40, 42, 46),      # lamp iron
    "z": (96, 100, 106),    # lamp wall plate
    "n": (46, 82, 60),      # lamp shade, green enamel
    "N": (84, 124, 92),     # lamp shade light
    "u": (240, 228, 188),   # bulb glass
}

def canvas(w, h):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put(img, x, y, key):
    if 0 <= x < img.width and 0 <= y < img.height and key in P:
        img.putpixel((x, y), P[key] + (255,))


def rect(img, x0, y0, x1, y1, key):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put(img, x, y, key)


def text_width(text, font, gap):
    return sum(len(font[ch][0]) for ch in text) + gap * (len(text) - 1)


def write(img, text, font, x, y, key, gap=1):
    for ch in text:
        glyph = font[ch]
        for gy, row in enumerate(glyph):
            for gx, cell in enumerate(row):
                if cell == "#":
                    put(img, x + gx, y + gy, key)
        x += len(glyph[0]) + gap


# Hand-painted letters for the name board, 7 rows tall (lower case on the bottom five), a little uneven on
# purpose: the kick on the L, the curl on the l and the comma of the apostrophe are a sign painter's, not a type.
HAND = {
    "L": ["##...", ".#...", ".#...", ".#...", ".#...", ".#..#", "#####"],
    "e": ["....", "....", ".##.", "#..#", "####", "#...", ".###"],
    "B": ["####.", ".#..#", ".#..#", ".###.", ".#..#", ".#..#", "####."],
    "l": ["##.", ".#.", ".#.", ".#.", ".#.", ".#.", ".##"],
    "a": ["....", "....", ".##.", "...#", ".###", "#..#", ".###"],
    "n": ["....", "....", "#.#.", "##.#", "#..#", "#..#", "#..#"],
    "c": ["....", "....", ".###", "#...", "#...", "#...", ".###"],
    "'": [".#", ".#", "#.", "..", "..", "..", ".."],
    "s": ["....", "....", ".###", "#...", ".##.", "...#", "###."],
}


def sign():
    # The name board painted on the store's face above the awning (owner 2026-11-25: on the store itself, not a
    # big post sign; modest, readable, lit at night by shaded lamps on arms). A plain board in the island's
    # green with cream hand lettering and a painted diamond at each end, framed in weathered wood. It fits the
    # band of siding between the awning and the upper windows; the two lamps hang over its ends.
    w, h = 60, 13
    img = canvas(w, h)
    rect(img, 0, 0, w - 1, h - 1, "D")
    rect(img, 1, 1, w - 2, h - 2, "w")
    rect(img, 1, 1, w - 2, 1, "W")
    rect(img, 1, h - 2, w - 2, h - 2, "v")
    rect(img, 2, 2, w - 3, h - 3, "q")
    rect(img, 2, 2, w - 3, 2, "Q")
    name = "LeBlanc's"
    tw = text_width(name, HAND, 1)
    write(img, name, HAND, (w - tw) // 2, 3, "c")
    for cx in (5, w - 6):
        put(img, cx, 4, "C"); put(img, cx - 1, 5, "C"); put(img, cx, 5, "c"); put(img, cx + 1, 5, "C"); put(img, cx, 6, "C")
    return img


def sign_night():
    # The board as its two lamps light it after sunset, drawn over the night's darkness the way a lit window
    # pane is: each lamp throws a warm pool down from its bulb (just over the board's ends), fading toward the
    # middle, over a dim warm floor so the whole name still reads.
    day = sign()
    w, h = day.size
    img = canvas(w, h)
    bulbs = ((5.5, -1.0), (w - 5.5, -1.0))
    for y in range(h):
        for x in range(w):
            r, g, b, a = day.getpixel((x, y))
            if a == 0:
                continue
            lit = 0.22
            for bx, by in bulbs:
                d2 = ((x + 0.5 - bx) / 1.0) ** 2 + ((y + 0.5 - by) * 1.6) ** 2
                lit += 0.95 / (1.0 + d2 / 90.0)
            lit = min(lit, 1.1)
            warm = (1.0, 0.86, 0.62)
            img.putpixel((x, y), tuple(min(255, int(round(c * lit * k))) for c, k in zip((r, g, b), warm)) + (255,))
    return img

# A gooseneck lamp: a plate on the wall, an iron arm curving up and over, a green enamel shade hung from its
# end with the bulb under it. Drawn for the lamp on the board's left (its plate outside the board, its shade
# over the board's end); the right one is its mirror.
LAMP = [
    "...jjjjj......",
    "..j.....j.....",
    ".j.......j....",
    ".j........j...",
    "jjj.......j...",
    "jzj.......j...",
    "jzj......jnj..",
    "jzj.....jnNnj.",
    "jjj....jnNNnnj",
    ".......jjjjjjj",
    ".........uuu..",
]


def lamp(mirror):
    w, h = len(LAMP[0]), len(LAMP)
    img = canvas(w, h)
    for y, row in enumerate(LAMP):
        for x, key in enumerate(row):
            if key != ".":
                put(img, (w - 1 - x) if mirror else x, y, key)
    return img

def potato_crate():
    w, h = 34, 24
    img = canvas(w, h)
    # Low stand legs.
    for lx in (3, 29):
        rect(img, lx, 18, lx + 1, h - 1, "v")
        put(img, lx, h - 1, "D"); put(img, lx + 1, h - 1, "D")
    # The crate, three slats on its face.
    rect(img, 1, 9, 32, 19, "D")
    for i, y in enumerate((10, 13, 16)):
        rect(img, 2, y, 31, y + 1, "K")
        rect(img, 2, y, 31, y, "L")
        rect(img, 2, y + 2, 31, y + 2, "k")
    rect(img, 2, 19, 31, 19, "k")
    # A heap of potatoes over the rim.
    spuds = [(4, 7), (8, 6), (12, 5), (16, 4), (20, 5), (24, 6), (28, 7), (7, 3), (13, 2), (19, 2), (25, 3), (10, 0), (17, 0)]
    for sx, sy in spuds:
        rect(img, sx, sy + 1, sx + 4, sy + 3, "p")
        rect(img, sx + 1, sy, sx + 3, sy, "p")
        rect(img, sx + 1, sy + 4, sx + 3, sy + 4, "o")
        put(img, sx + 1, sy + 1, "P"); put(img, sx + 2, sy + 1, "P")
        put(img, sx + 3, sy + 3, "o")
    return img


def buoy(img, x, y, top, top_light):
    # A lobster buoy, 6 wide and 11 tall, with a spindle up through it.
    rect(img, x + 2, y - 3, x + 3, y, "w")
    rect(img, x, y + 1, x + 5, y + 4, top)
    rect(img, x + 1, y, x + 4, y, top)
    rect(img, x, y + 5, x + 5, y + 7, "x")
    rect(img, x + 1, y + 8, x + 4, y + 9, top)
    put(img, x + 1, y + 1, top_light); put(img, x + 1, y + 2, top_light)
    rect(img, x + 5, y + 1, x + 5, y + 9, "z")


def buoy_rack():
    w, h = 32, 34
    img = canvas(w, h)
    # The rack: two uprights and two rails.
    for ux in (2, 28):
        rect(img, ux, 4, ux + 1, h - 1, "w")
        rect(img, ux + 1, 4, ux + 1, h - 1, "v")
    rect(img, 1, 4, 30, 5, "W")
    rect(img, 1, 5, 30, 5, "v")
    rect(img, 1, 19, 30, 20, "W")
    rect(img, 1, 20, 30, 20, "v")
    # Three buoys hung from the top rail.
    buoy(img, 5, 9, "r", "R")
    buoy(img, 13, 9, "y", "Y")
    buoy(img, 21, 9, "b", "B")
    # A coil of rope on the bottom rail.
    for cx in (9, 22):
        cy = 22
        rect(img, cx - 5, cy, cx + 5, cy + 7, "h")
        for ring in (1, 3):
            rect(img, cx - 5 + ring, cy + ring, cx + 5 - ring, cy + ring, "H")
            rect(img, cx - 5 + ring, cy + 7 - ring, cx + 5 - ring, cy + 7 - ring, "H")
            rect(img, cx - 5 + ring, cy + ring, cx - 5 + ring, cy + 7 - ring, "H")
            rect(img, cx + 5 - ring, cy + ring, cx + 5 - ring, cy + 7 - ring, "H")
        rect(img, cx - 4, cy + 7, cx + 4, cy + 7, "H")
        for qx, qy in ((cx - 5, cy), (cx + 5, cy), (cx - 5, cy + 7), (cx + 5, cy + 7)):
            img.putpixel((qx, qy), (0, 0, 0, 0))
    return img


def bucket(img, x, y, w, h):
    rect(img, x, y, x + w - 1, y + h - 1, "s")
    rect(img, x, y, x + w - 1, y, "S")
    rect(img, x + w - 2, y + 1, x + w - 1, y + h - 1, "z")
    rect(img, x + 1, y + h - 1, x + w - 2, y + h - 1, "z")
    rect(img, x + 1, y + 1, x + 1, y + h - 2, "S")


def bucket_stand():
    w, h = 28, 38
    img = canvas(w, h)
    # A broom on the left and a mop on the right, stood head up behind the buckets.
    rect(img, 2, 0, 7, 8, "g")
    rect(img, 2, 7, 7, 8, "G")
    rect(img, 3, 0, 6, 0, "G")
    for i in range(22):
        put(img, 5, 9 + i, "w")
    rect(img, 20, 0, 26, 7, "m")
    rect(img, 21, 6, 26, 7, "M")
    put(img, 20, 3, "M"); put(img, 24, 2, "M")
    for i in range(22):
        put(img, 23, 8 + i, "W")
    # Three galvanised buckets: two on the ground, one stacked on them.
    bucket(img, 3, 28, 10, 10)
    bucket(img, 14, 28, 10, 10)
    bucket(img, 8, 19, 11, 10)
    # Wire bails.
    for bx, by, bw in ((3, 28, 10), (14, 28, 10), (8, 19, 11)):
        for i in range(bw):
            put(img, bx + i, by - 2 if 1 < i < bw - 2 else by - 1, "z")
    return img


def main(out):
    os.makedirs(out, exist_ok=True)
    for name, img in (("StoreFront_Sign", sign()), ("StoreFront_SignNight", sign_night()), ("StoreFront_LampLeft", lamp(False)), ("StoreFront_LampRight", lamp(True)),
                      ("StoreFront_PotatoCrate", potato_crate()),
                      ("StoreFront_BuoyRack", buoy_rack()), ("StoreFront_BucketStand", bucket_stand())):
        img.save(os.path.join(out, name + ".png"))
        print(name, img.size)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else ".")
