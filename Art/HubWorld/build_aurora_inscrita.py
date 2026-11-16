"""Build Aurora Inscrita from Lumon's original chisel-cut display outlines."""
import math
import json
import unicodedata
from pathlib import Path

from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parents[2] / "Assets/Lumon/HubWorld/UI/Fonts/AuroraInscrita.ttf"

# The local design grid is 500 × 700. Rounded letters overshoot the cap line.
# Each entry names the stroke weight followed by the centerline coordinates.
SHAPES = {
    "A": [(69, (25, 0), (244, 706)), (92, (244, 706), (477, 0)), (57, (109, 235), (396, 235))],
    "B": [(87, (54, 0), (54, 700)), (70, (54, 700), (304, 700), (414, 632), (425, 531), (360, 430), (277, 392), (54, 392)),
          (80, (282, 392), (400, 345), (451, 260), (450, 138), (381, 43), (281, 0), (54, 0))],
    "C": [(80, (456, 582), (388, 671), (261, 706), (151, 667), (65, 566), (27, 424), (27, 265), (73, 124), (163, 34), (271, -6), (383, 32), (460, 112))],
    "D": [(87, (49, 0), (49, 700)), (81, (49, 700), (240, 700), (362, 652), (455, 518), (486, 351), (458, 174), (364, 47), (240, 0), (49, 0))],
    "E": [(87, (49, 0), (49, 700)), (65, (49, 700), (445, 700)), (58, (49, 360), (355, 360)), (72, (49, 0), (454, 0))],
    "F": [(87, (53, 0), (53, 700)), (65, (53, 700), (439, 700)), (58, (53, 362), (352, 362))],
    "G": [(80, (459, 586), (385, 672), (266, 706), (150, 669), (66, 567), (27, 424), (27, 264), (72, 126), (161, 37), (271, -6), (391, 26), (475, 96)), (78, (475, 96), (475, 303)), (60, (476, 304), (310, 304))],
    "H": [(85, (49, 0), (49, 700)), (85, (454, 0), (454, 700)), (61, (49, 352), (454, 352))],
    "I": [(92, (116, 0), (116, 700)), (56, (26, 700), (206, 700)), (65, (26, 0), (206, 0))],
    "J": [(82, (337, 700), (337, 151), (304, 46), (225, -10), (128, -2), (65, 46), (45, 118)), (57, (235, 700), (430, 700))],
    "K": [(85, (52, 0), (52, 700)), (66, (56, 266), (437, 700)), (89, (196, 432), (460, 0))],
    "L": [(85, (51, 700), (51, 0)), (74, (51, 0), (447, 0))],
    "M": [(69, (35, 0), (35, 700), (275, 133), (528, 700)), (83, (528, 700), (528, 0))],
    "N": [(71, (41, 0), (41, 700)), (88, (41, 700), (453, 0)), (70, (453, 0), (453, 700))],
    "O": [(78, (250, 705), (365, 675), (449, 569), (478, 449), (478, 245), (427, 108), (351, 28), (250, -7), (146, 24), (69, 111), (22, 245), (22, 442), (56, 568), (135, 674), (250, 705))],
    "P": [(86, (51, 0), (51, 700)), (71, (51, 700), (300, 700), (411, 638), (454, 532), (430, 414), (343, 350), (249, 333), (51, 333))],
    "Q": [(77, (250, 705), (365, 675), (449, 569), (478, 449), (478, 245), (427, 108), (351, 28), (250, -7), (146, 24), (69, 111), (22, 245), (22, 442), (56, 568), (135, 674), (250, 705)), (67, (288, 177), (496, -112))],
    "R": [(85, (51, 0), (51, 700)), (71, (51, 700), (291, 700), (411, 638), (446, 539), (415, 430), (313, 362), (51, 362)), (86, (266, 362), (467, 0))],
    "S": [(77, (448, 601), (368, 673), (259, 704), (137, 673), (63, 602), (45, 503), (100, 421), (382, 302), (452, 215), (439, 114), (362, 30), (241, -5), (117, 27), (42, 101))],
    "T": [(85, (247, 0), (247, 699)), (69, (17, 699), (478, 699))],
    "U": [(85, (43, 701), (43, 185), (78, 75), (157, 13), (249, -9), (349, 15), (430, 75), (460, 188), (460, 701))],
    "V": [(90, (22, 700), (252, 0)), (68, (252, 0), (478, 700))],
    "W": [(86, (19, 700), (169, 0)), (69, (169, 0), (339, 608)), (86, (339, 608), (514, 0)), (69, (514, 0), (648, 700))],
    "X": [(86, (37, 702), (461, 0)), (67, (36, 0), (461, 702))],
    "Y": [(88, (23, 700), (242, 365)), (69, (242, 365), (473, 700)), (83, (242, 365), (242, 0))],
    "Z": [(67, (32, 700), (465, 700)), (84, (465, 700), (30, 0)), (71, (30, 0), (476, 0))],
    "0": [(73, (242, 701), (355, 661), (436, 544), (458, 351), (434, 152), (351, 35), (242, -4), (134, 31), (54, 153), (31, 351), (54, 548), (134, 668), (242, 701))],
    "1": [(83, (247, 0), (247, 698)), (58, (247, 698), (87, 588)), (67, (122, 0), (374, 0))],
    "2": [(77, (43, 581), (98, 662), (229, 701), (359, 668), (430, 580), (427, 469), (47, 0), (449, 0))],
    "3": [(78, (49, 624), (151, 684), (286, 699), (390, 644), (419, 541), (385, 450), (311, 386), (218, 365)), (81, (218, 365), (322, 365), (410, 303), (441, 188), (411, 91), (309, 18), (178, 0), (53, 77))],
    "4": [(69, (343, 0), (343, 701), (31, 218), (450, 218))],
    "5": [(77, (417, 699), (105, 699), (64, 381), (237, 411), (360, 373), (432, 269), (420, 137), (345, 48), (224, 1), (113, 22), (42, 79))],
    "6": [(79, (402, 652), (305, 697), (182, 678), (86, 558), (40, 377), (43, 190), (90, 75), (211, 1), (330, 27), (421, 128), (431, 246), (362, 346), (246, 387), (123, 354), (44, 282))],
    "7": [(69, (38, 698), (453, 698)), (88, (453, 698), (157, 0))],
    "8": [(77, (234, 707), (122, 677), (65, 585), (69, 478), (151, 404), (359, 300), (435, 218), (425, 114), (343, 26), (235, -4), (118, 25), (43, 111), (47, 215), (128, 304), (357, 420), (414, 484), (415, 590), (348, 676), (234, 707))],
    "9": [(78, (424, 415), (333, 349), (214, 323), (98, 367), (35, 464), (49, 589), (139, 678), (249, 705), (369, 648), (430, 531), (427, 347), (375, 163), (274, 47), (153, 3), (53, 30))],
}

# Widths are based on the same lettering grid, including the narrow I and broad M/W.
WIDTHS = {"I": 232, "M": 568, "W": 677, "J": 440}

def polygon(pen, vertices, scale, x, y):
    points = [(round(a * scale + x), round(b * scale + y)) for a, b in vertices]
    area = sum(a * d - c * b for (a, b), (c, d) in zip(points, points[1:] + points[:1]))
    if area > 0:
        points.reverse()
    pen.moveTo(points[0])
    for point in points[1:]:
        pen.lineTo(point)
    pen.closePath()


def stroke(pen, radius, points, scale, x, y):
    for (ax, ay), (bx, by) in zip(points, points[1:]):
        dx, dy = bx - ax, by - ay
        length = math.hypot(dx, dy)
        ox, oy = -dy * radius / length, dx * radius / length
        polygon(pen, ((ax + ox, ay + oy), (bx + ox, by + oy), (bx - ox, by - oy), (ax - ox, ay - oy)), scale, x, y)
    for px, py in points[1:-1]:
        polygon(pen, [(px + radius * math.cos(i * math.pi / 4), py + radius * math.sin(i * math.pi / 4)) for i in range(8)], scale, x, y)


def glyph(character):
    pen = TTGlyphPen(None)
    base, *marks = unicodedata.normalize("NFD", character)
    cap = base.upper()
    scale = 0.76 if base.islower() else 1.0
    offset = 57
    width = WIDTHS.get(cap, 502)
    if cap in SHAPES:
        for weight, *points in SHAPES[cap]:
            stroke(pen, weight * 0.63, points, scale, offset, 0)
            for endpoint in (points[0], points[-1]):
                px, py = endpoint
                if py in (0, 700, 701) and (cap not in "COQS" or len(points) < 8):
                    sign = 1 if py < 1 else -1
                    polygon(pen, ((px-65, py-17*sign), (px+64, py-17*sign),
                                  (px+34, py+33*sign), (px-35, py+48*sign)), scale, offset, 0)
        for mark in marks:
            middle = width / 2
            top = 775
            strokes = {
                "\u0300": [(middle-82, top+90), (middle+24, top)],
                "\u0301": [(middle-24, top), (middle+83, top+91)],
                "\u0302": [(middle-88, top), (middle, top+83), (middle+89, top)],
                "\u0303": [(middle-126, top+10), (middle-75, top+60), (middle-15, top+59), (middle+48, top+16), (middle+125, top+54)],
                "\u0308": [(middle-71, top+35), (middle-69, top+36)],
                "\u0327": [(middle+16, -31), (middle-18, -112), (middle+56, -139), (middle+44, -190), (middle-36, -214)],
            }
            if mark in strokes:
                stroke(pen, 22, strokes[mark], scale, offset, 0)
                if mark == "\u0308":
                    stroke(pen, 22, [(middle+69, top+35), (middle+71, top+36)], scale, offset, 0)
    elif base == " ":
        return pen.glyph(), 235
    else:
        punctuation = {
            ".": [(38, (106, 12), (106, 50))], ",": [(31, (127, 48), (79, -65))],
            "'": [(25, (114, 664), (153, 774))], "’": [(27, (105, 644), (146, 766))],
            "–": [(28, (51, 274), (360, 274))],
            "-": [(28, (51, 274), (284, 274))], "·": [(36, (101, 273), (103, 302))],
        }
        for weight, *points in punctuation.get(base, []):
            stroke(pen, weight, points, 1, 0, 0)
        return pen.glyph(), {"-": 340, "–": 420}.get(base, 218)
    return pen.glyph(), round(width * scale + 2 * offset)


def main():
    characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ÀÁÂÃÄÇÉÊÍÓÔÕÖÚÜÑàáâãäçéêíóôõöúüñ.,'’·-–"
    builder = FontBuilder(1000, isTTF=True)
    cmap = {ord(c): f"uni{ord(c):04X}" for c in characters}
    empty = TTGlyphPen(None)
    glyphs, widths = {".notdef": empty.glyph()}, {".notdef": (360, 0)}
    for char in characters:
        name = cmap[ord(char)]
        outline, advance = glyph(char)
        glyphs[name] = outline
        widths[name] = advance, 0
    builder.setupGlyphOrder(list(glyphs))
    builder.setupCharacterMap(cmap)
    builder.setupGlyf(glyphs)
    builder.setupHorizontalMetrics({name: (width, getattr(glyphs[name], "xMin", 0)) for name, (width, _) in widths.items()})
    builder.setupHorizontalHeader(ascent=960, descent=-225)
    builder.setupNameTable({"familyName": "Aurora Inscrita", "styleName": "Regular", "fullName": "Aurora Inscrita Regular",
                            "psName": "AuroraInscrita-Regular", "uniqueFontIdentifier": "Lumon:AuroraInscrita:1.0",
                            "version": "Version 1.000", "copyright": "Aurora Inscrita for Lumon, 2026"})
    builder.setupOS2(sTypoAscender=960, sTypoDescender=-225, sTypoLineGap=0, usWinAscent=960, usWinDescent=225)
    builder.setupPost()
    builder.save(OUT)
    make_glyph_sheet(characters)


def make_glyph_sheet(characters, font_path=OUT, filename="aurora-glyphs", size=52, ink_stroke=0, lean=0):
    """Pack original font glyphs, preserving the baseline and actual ink bounds."""
    cell = 96
    font = ImageFont.truetype(str(font_path), size)
    pixels = Image.new("RGBA", (1024, 1024), (255, 255, 255, 0))
    glyphs = []
    for i, char in enumerate(characters):
        left, top, right, bottom = font.getbbox(char, anchor="ls", stroke_width=ink_stroke)
        x, y = (i % 10) * cell + 1, (i // 10) * cell + 1
        width, height = right - left, bottom - top
        if width and height:
            ink = Image.new("L", (width, height))
            ImageDraw.Draw(ink).text((-left, -top), char, font=font, fill=255, anchor="ls",
                                     stroke_width=ink_stroke)
            if lean:
                start_shift = -top * lean
                end_shift = -bottom * lean
                lean_left, lean_right = int(min(start_shift, end_shift)) - 1, int(max(start_shift, end_shift)) + 1
                width += lean_right - lean_left
                ink = ink.transform((width, height), Image.Transform.AFFINE,
                                    (1, lean, lean_left - start_shift, 0, 1, 0), resample=Image.Resampling.BICUBIC)
                left += lean_left
            if max(width, height) >= cell - 2 or y + height > pixels.height:
                raise ValueError(f"Glyph {char!r} out of atlas bounds")
            pixels.paste((255, 255, 255, 255), (x, y, x+width, y+height), ink)
        glyphs.append(dict(code=ord(char), x=x, y=y, width=width, height=height, left=left, top=top,
                           advance=font.getlength(char)))
    art = OUT.parent.parent / "Art"
    pixels.save(art / f"{filename}.png")
    (art / f"{filename}.json").write_text(json.dumps(dict(size=size, glyphs=glyphs), indent=2) + "\n",
                                           encoding="utf-8")


if __name__ == "__main__":
    main()
