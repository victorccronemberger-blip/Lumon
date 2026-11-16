"""Crop the Higgsfield plates and export Unity textures with their alpha."""
from pathlib import Path

from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Art/HubWorld/source"
UI = ROOT / "Assets/Lumon/HubWorld/UI/Art"


def trim(image, threshold=10):
    image = image.convert("RGBA")
    alpha = image.getchannel("A")
    bounds = alpha.point(lambda value: 255 if value > threshold else 0).getbbox()
    if bounds is None:
        raise ValueError("The source does not contain a visible plate.")
    image.putalpha(alpha.point(lambda value: 0 if value < 5 else min(255, round(value * 255 / 252))))
    return image.crop(bounds)


def export_card(image, name):
    # Both variants share their geometry, including the margin for the focus glow.
    padding = 14
    size = (image.width + 2 * padding, image.height + 2 * padding)
    plate = Image.new("RGBA", size)
    plate.paste(image, (padding, padding))
    plate.save(UI / f"higgsfield-card-{name}.png")
    edge = plate.getchannel("A").filter(ImageFilter.MaxFilter(7))
    glow = Image.new("RGBA", size, "#ffba57")
    glow.putalpha(edge.filter(ImageFilter.GaussianBlur(7)))
    Image.alpha_composite(glow, plate).save(UI / f"higgsfield-card-{name}-focus.png")


with Image.open(SOURCE / "workshop-higgsfield-frame.png") as frame:
    frame = trim(frame)
    frame.save(UI / "higgsfield-workshop-frame.png")
    dim = Image.new("RGBA", frame.size, "#0a1220")
    dim.putalpha(frame.getchannel("A").point(lambda value: round(value * 0.77)))
    dim.save(UI / "higgsfield-workshop-dim.png")

with Image.open(SOURCE / "workshop-higgsfield-atlas.png") as atlas:
    cards = {
        "fire": (0, 20, 348, 488),
        "water": (348, 20, 681, 488),
        "leaf": (681, 20, 1024, 488),
    }
    for name, bounds in cards.items():
        export_card(trim(atlas.crop(bounds)), name)

    buttons = {
        "jade": (0, 488, 346, 688),
        "gold": (346, 488, 682, 688),
        "copper": (682, 488, 1024, 688),
    }
    for name, bounds in buttons.items():
        cutoff = 80 if name == "gold" else 10
        trim(atlas.crop(bounds), cutoff).save(UI / f"higgsfield-key-{name}.png")
