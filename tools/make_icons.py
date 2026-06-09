"""Convert chosen ComfyUI icons (glowing glyph on pure black) into
RGBA PNGs with luminance-as-alpha, then write to the mod source
Assets/Textures and the deployed Steam Mods folder.

Run with ComfyUI's embedded python (has Pillow):
  E:\\ComfUI\\python_embeded\\python.exe TheTurned\\tools\\make_icons.py
"""
import os
from PIL import Image

SRC_DIR = r"E:\ComfUI\ComfyUI\output"
DST_DIRS = [
    r"E:\DEV\PhoenixPoint\TheTurned\Assets\Textures",
    r"D:\Steam\steamapps\common\Phoenix Point\Mods\TheTurned\Assets\Textures",
]
SIZE = 256

# final mod filename -> chosen ComfyUI output (best version per perk)
SOURCES = {
    "Arthron_NaturalArmour": "Arthron_NaturalArmour_v3_00001_.png",
    "Arthron_AcidGlands":    "Arthron_AcidGlands_v2_00001_.png",
    "Arthron_ChitinPlating": "Arthron_ChitinPlating_v3_00001_.png",
    "Arthron_CrushingClaw":  "Arthron_CrushingClaw_v4_00001_.png",
    "Arthron_HardenedHide":  "Arthron_HardenedHide_v2_00001_.png",
    "Arthron_ApexCarapace":  "Arthron_ApexCarapace_v2_00001_.png",
    "Arthron_Spec":          "Arthron_Spec_v2_00001_.png",
}


def luminance_alpha(img: Image.Image) -> Image.Image:
    """Keep RGB, set alpha = perceived luminance so the pure-black
    background fades to fully transparent and the glyph/glow stays."""
    rgb = img.convert("RGB")
    alpha = rgb.convert("L")
    out = rgb.convert("RGBA")
    out.putalpha(alpha)
    return out


def main() -> None:
    for d in DST_DIRS:
        os.makedirs(d, exist_ok=True)
    for name, src_file in SOURCES.items():
        src = os.path.join(SRC_DIR, src_file)
        if not os.path.isfile(src):
            print(f"MISSING: {src}")
            continue
        rgba = luminance_alpha(Image.open(src))
        rgba = rgba.resize((SIZE, SIZE), Image.LANCZOS)
        for d in DST_DIRS:
            out_path = os.path.join(d, f"{name}.png")
            rgba.save(out_path, "PNG")
            print(f"WROTE {out_path}  ({rgba.size}, {rgba.mode})")


if __name__ == "__main__":
    main()
