"""Draw the Observatory workshop's brass fittings and engraved controls."""
from pathlib import Path
from math import cos, hypot, sin, tau
from random import Random

from PIL import Image, ImageDraw

ART = Path(__file__).resolve().parents[2] / "Assets/Lumon/HubWorld/UI/Art"


def profile(size, inset, shoulder):
    w, h = size
    a, b = inset, shoulder
    return [(a + b, a), (w - a - b, a), (w - a, a + b), (w - a, h - a - b),
            (w - a - b, h - a), (a + b, h - a), (a, h - a - b), (a, a + b)]


def material(canvas, outline, lit, shade, seed, weave=False):
    w, h = canvas.size
    rng = Random(seed)
    texture = Image.new("RGB", (w, h))
    channels = [(lo, hi - lo) for hi, lo in zip(lit, shade)]
    pixels = texture.load()
    for y in range(h):
        for x in range(w):
            light = max(0.08, 1 - hypot((x - w * .28) / w, (y - h * .08) / h) * .89)
            noise = rng.gauss(0, 1.6)
            if weave and (x + y) % 5 == 0:
                noise += 1.4
            pixels[x, y] = tuple(int(max(0, min(255, lo + delta * light + noise)))
                                 for lo, delta in channels)
    mask = Image.new("L", canvas.size)
    ImageDraw.Draw(mask).polygon(outline, fill=255)
    canvas.paste(texture, (0, 0), mask)


def star(draw, x, y, r, color):
    points = [(x + sin(i * tau / 8) * (r if i % 2 == 0 else r * .24),
               y - cos(i * tau / 8) * (r if i % 2 == 0 else r * .24)) for i in range(8)]
    draw.polygon(points, fill=color)


def panel(name, size):
    image = Image.new("RGBA", size)
    w, h = size
    draw = ImageDraw.Draw(image)
    draw.polygon(profile(size, 4, 47), fill="#110e19")
    material(image, profile(size, 7, 45), (111, 72, 63), (42, 24, 33), 31, weave=True)
    draw.polygon(profile(size, 10, 42), outline="#a16a4b", width=2)
    draw.line([(52, 10), (w - 52, 10)], fill="#e7b77a", width=3)
    draw.polygon(profile(size, 24, 29), outline="#251a25", width=4)
    draw.polygon(profile(size, 27, 27), outline="#bc875a", width=2)
    material(image, profile(size, 30, 26), (53, 78, 84), (25, 39, 56), 77, weave=True)
    draw.line([(56, 31), (w - 56, 31)], fill="#19232e", width=4)
    draw.line([(56, h - 31), (w - 56, h - 31)], fill="#e4b77e", width=2)
    for x in (37, w - 37):
        for y in (35, h - 35):
            draw.ellipse((x - 14, y - 13, x + 14, y + 15), fill="#211826")
            draw.ellipse((x - 12, y - 12, x + 12, y + 12), fill="#bf8755", outline="#ffda98", width=2)
            draw.ellipse((x - 8, y - 8, x + 8, y + 8), fill="#704638")
            star(draw, x, y, 7, "#f5d79a")
    for y in (18, h - 18):
        x = w // 2
        draw.line((x - 71, y, x - 11, y), fill="#b7855c", width=2)
        draw.line((x + 11, y, x + 71, y), fill="#b7855c", width=2)
        star(draw, x, y, 8, "#ffda98")
    image.save(ART / name)


def key(name, lit, shade, rim, face_highlight):
    size = (280, 96)
    image = Image.new("RGBA", size)
    draw = ImageDraw.Draw(image)
    draw.polygon(profile(size, 3, 18), fill="#100d16")
    draw.polygon(profile((280, 91), 4, 17), fill="#4c302f")
    draw.polygon(profile((280, 86), 4, 16), fill=rim)
    material(image, profile((280, 86), 8, 13), lit, shade, 51, weave=True)
    draw.line((21, 8, 259, 8), fill=face_highlight, width=2)
    draw.line((21, 76, 259, 76), fill=shade, width=2)
    image.save(ART / f"workshop-key-{name}.png")


def insignia(name, paths):
    glyph = Image.new("RGBA", (96, 96))
    draw = ImageDraw.Draw(glyph)
    for points in paths:
        draw.line([(x, y + 3) for x, y in points], fill="#281e29", width=6, joint="curve")
        draw.line(points, fill="#f5d7a4", width=4, joint="curve")
    glyph.save(ART / f"workshop-icon-{name}.png")


panel("workshop-frame.png", (1266, 880))
panel("workshop-reveal-frame.png", (694, 635))
key("iron", (75, 100, 107), (43, 57, 72), "#ad8963", "#bca783")
key("brass", (137, 96, 61), (76, 49, 52), "#f3ca86", "#ffe5ad")
key("ember", (159, 80, 59), (95, 39, 47), "#f1bd7b", "#ffdf9c")
key("well", (45, 64, 73), (27, 38, 52), "#88715a", "#7d827d")

insignia("team", [[(19, 36), (48, 19), (77, 36), (73, 56), (48, 80), (23, 56), (19, 36)],
                  [(32, 48), (43, 48)], [(53, 48), (64, 48)], [(48, 56), (48, 67)]])
insignia("fusion", [[(48, 68), (13, 44), (33, 23), (63, 48), (48, 68)],
                    [(48, 68), (83, 44), (63, 23), (33, 48), (48, 68)]])
insignia("improve", [[(20, 65), (48, 37), (76, 65)], [(28, 40), (48, 20), (68, 40)],
                     [(37, 75), (59, 75)]])
egg = [(48, 19), (59, 24), (71, 42), (75, 54), (73, 67), (65, 76),
       (48, 80), (31, 76), (23, 67), (21, 54), (25, 42), (37, 24), (48, 19)]
insignia("eggs", [egg, [(24, 55), (40, 50), (48, 58), (59, 50), (73, 55)]])
insignia("coin", [[(48, 19), (66, 26), (77, 48), (66, 70), (48, 77), (26, 67), (19, 48), (26, 29), (48, 19)],
                   [(48, 34), (61, 48), (48, 62), (35, 48), (48, 34)]])
insignia("aurora", [[(48, 10), (57, 36), (82, 48), (57, 57), (48, 86), (38, 57),
                    (14, 48), (38, 36), (48, 10)], [(48, 34), (48, 62)]])
