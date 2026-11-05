# Paints a few days' stubble into the skin diffuse, inside the area the (removed) 3D beard
# covered: fine dark dots over a soft cool shadow, feathered at the edges. PIL only.
#   python make_stubble.py <out_dir>
import sys, json, shutil, os, random
from PIL import Image, ImageDraw, ImageFilter, ImageChops

out = sys.argv[1] if len(sys.argv) > 1 else r"C:/LudusForge/Tools/survivor/out"
tex = os.path.join(out, "unity", "Survivor.fbm", "middleage_lightskinned_male_diffuse.png")
orig = tex.replace(".png", "_orig.png")
if not os.path.exists(orig):
    shutil.copyfile(tex, orig)            # always paint from the untouched original
skin = Image.open(orig).convert("RGB")
W, H = skin.size

mask = Image.new("L", (W, H), 0)
d = ImageDraw.Draw(mask)
for face in json.load(open(os.path.join(out, "stubble_uv.json"))):
    us = [u for u, v in face]; vs = [v for u, v in face]
    if min(us) < 0.62 or min(vs) < 0.2:   # head island only (skip mouth-interior islands)
        continue
    d.polygon([(u * W, (1 - v) * H) for u, v in face], fill=255)
mask = mask.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(14))

random.seed(7)
noise = Image.effect_noise((W, H), 64)                          # gaussian around 128
dots = noise.point(lambda x: 255 if x > 146 else 0).filter(ImageFilter.GaussianBlur(0.9))
strength = dots.point(lambda x: int(255 * (0.26 + 0.6 * x / 255.0)))    # shadow + follicles
dark = ImageChops.multiply(mask, strength)                       # 0..255 darkness

channels = []
for ch, tone in zip(skin.split(), (0.62, 0.66, 0.70)):           # slightly cool, like real stubble
    mult = dark.point(lambda x, t=tone: int(255 - x * t))
    channels.append(ImageChops.multiply(ch, mult))
Image.merge("RGB", channels).save(tex)
cov = sum(1 for p in mask.resize((256, 256)).getdata() if p > 127) * (W * H) // (256 * 256)
print("stubble painted ->", tex, "approx coverage px:", cov)
