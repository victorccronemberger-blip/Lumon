"""Render Hub UI materials from the approved painted parchment."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

base = Path(__file__).resolve().parents[2] / "Assets/Lumon/HubWorld"
art = base / "UI/Art"

# Grain is sampled from the same painted parchment and cloth already used in the Hub.
paper = Image.open(art / "paper.png").convert("RGB")
grain = paper.crop((380, 295, 1070, 675)).convert("L").resize((640, 232), Image.Resampling.LANCZOS)
for name, light, dark in [
    ("carved-ticket", (251, 225, 177), (146, 99, 75)),
    ("carved-button", (161, 74, 50), (67, 25, 35)),
    ("carved-recess", (232, 199, 151), (110, 73, 72)),
]:
    size = (640, 232)
    mask = Image.new("L", size)
    draw = ImageDraw.Draw(mask)
    # Rounded shoulders are beveled, with a separate cast shadow under the object.
    outline = [(18,8),(619,8),(632,20),(632,210),(619,223),(18,223),(6,210),(6,20)]
    draw.polygon(outline, fill=255)
    face = Image.new("RGBA", size)
    surf = face.load()
    tone = grain.load()
    for y in range(size[1]):
        for x in range(size[0]):
            step = 1 if y <= 13 else 0.76 - 0.21 * y / size[1]
            if y >= 208:
                step = 0.21
            sample = (tone[x, y] - 193) * 0.29
            color = tuple(round(max(0,min(255, d + (l-d)*step + sample))) for l,d in zip(light,dark))
            surf[x,y] = (*color, mask.getpixel((x,y)))
    face.save(art / f"{name}.png")

# A fine engraved rule, with the compass lozenge used by the Observatory.
rule = Image.new("RGBA", (520, 28))
draw = ImageDraw.Draw(rule)
for y, color in [(14, (111, 67, 55, 179)), (16, (255, 239, 189, 196))]:
    draw.line((9,y,247,y),fill=color,width=2)
    draw.line((273,y,510,y),fill=color,width=2)
draw.polygon([(260,5),(268,14),(260,23),(252,14)],fill=(119,65,52,225))
draw.polygon([(260,9),(264,14),(260,19),(256,14)],fill=(231,183,115,255))
rule.save(art / "engraved-rule.png")

# A pressed coloured well behind each Lumon portrait, inset into the cardstock.
for name, light, shade in (
    ("ember", (251, 212, 157), (156, 85, 52)),
    ("water", (218, 231, 211), (89, 129, 124)),
    ("leaf", (231, 226, 176), (119, 132, 70)),
):
    canvas = Image.new("RGBA", (216, 216))
    shadow = Image.new("RGBA", canvas.size)
    ImageDraw.Draw(shadow).ellipse((25, 30, 191, 196), fill=(79,46,42,113))
    canvas.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(7)))
    draw = ImageDraw.Draw(canvas)
    draw.ellipse((25, 24, 191, 190), fill=(113,76,56,193))
    draw.ellipse((29, 27, 187, 186), fill=(*light,255))
    draw.ellipse((35, 33, 181, 179), fill=(*shade,224))
    draw.ellipse((38, 38, 178, 178), fill=(*light,255))
    glow = Image.new("RGBA", canvas.size)
    ImageDraw.Draw(glow).ellipse((52, 46, 135, 131), fill=(255,239,201,172))
    canvas.alpha_composite(glow.filter(ImageFilter.GaussianBlur(22)))
    canvas.save(art / f"portrait-well-{name}.png")
