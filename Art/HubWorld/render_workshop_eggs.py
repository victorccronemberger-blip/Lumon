from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import math

base=Path(__file__).resolve().parents[2] / "Assets/Lumon/HubWorld"
art=base/"UI/Art"

# Warm stage light between the plaza and the parchment.
light=Image.new("RGBA",(360,204))
px=light.load()
for y in range(light.height):
    for x in range(light.width):
        X=2*x/(light.width-1)-1
        Y=2*y/(light.height-1)-1
        radius=min(1, math.hypot(X*.86, Y*.9))
        px[x,y]=(32,18,33,round(111+113*radius**1.9))
light.save(art/"workshop-vignette.png")

# Each shell has a readable colour and its own stamp. Both halves share its silhouette.
size=(144,158)
outline=[(72,7),(90,12),(107,29),(124,55),(133,81),(132,111),(122,133),
         (106,145),(82,152),(59,151),(36,143),(18,127),(10,102),(11,78),(19,52),(34,29),(51,13)]
cover=Image.new("L",size)
ImageDraw.Draw(cover).polygon(outline,fill=255)
seam=[(0,84),(26,82),(43,94),(57,76),(72,91),(85,73),(102,90),(120,79),(144,81)]
mask_top=Image.new("L",size)
ImageDraw.Draw(mask_top).polygon([(0,0),(144,0)]+list(reversed(seam)),fill=255)
from PIL import ImageChops

for index,(name,dark,mid,light) in enumerate([
        ("fire",(71,27,37),(172,71,46),(255,212,141)),
        ("water",(33,52,89),(67,124,151),(203,235,224)),
        ("leaf",(47,59,43),(117,138,68),(236,231,157))]):
    image=Image.new("RGBA",size)
    px=image.load()
    for y in range(size[1]):
        for x in range(size[0]):
            r=math.hypot((x-48)/110,(y-39)/103)
            t=min(1, max(0,(1-r)*1.13))
            tone=mid if t<.40 else light
            below=dark if t<.40 else mid
            k=t/.40 if t<.40 else (t-.40)/.60
            noise=2*math.sin(x*.62+y*.81)+1.5*math.sin(x*.19-y*.43)
            color=tuple(round(max(0,min(255,a+(b-a)*k+noise))) for a,b in zip(below,tone))
            px[x,y]=(*color,cover.getpixel((x,y)))
    draw=ImageDraw.Draw(image)
    etch=tuple(max(0,int(c*.52)) for c in mid)+(255,)
    bright=(*light,255)
    if index==0:
        icon=[(72,47),(85,70),(78,76),(87,90),(80,106),(65,110),(53,97),(55,85),(63,91),(62,77)]
    elif index==1:
        icon=[(72,49),(92,80),(94,92),(86,105),(73,110),(59,105),(52,92),(56,77)]
    else:
        icon=[(94,51),(96,74),(87,93),(72,103),(56,102),(49,92),(55,73),(75,60)]
    draw.polygon(icon,fill=etch)
    draw.line(icon+[icon[0]],fill=bright,width=2,joint="curve")
    draw.line([(70,100),(75,77)] if index==2 else [(63,95),(64,103)],fill=bright,width=2)
    image.save(art/f"egg-{name}.png")
    for half, mask in (("top",mask_top),("bottom",ImageChops.invert(mask_top))):
        part=image.copy()
        part.putalpha(ImageChops.multiply(cover,mask))
        part.save(art/f"egg-{name}-{half}.png")
