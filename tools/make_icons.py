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
# final mod filename -> chosen ComfyUI output (best version per perk)
SOURCES = {
    "Arthron_NaturalArmour": "Arthron_NaturalArmour_v3_00001_.png",
    "Arthron_AcidGlands":    "Arthron_AcidGlands_v2_00001_.png",
    "Arthron_ChitinPlating": "Arthron_ChitinPlating_v3_00001_.png",
    "Arthron_CrushingClaw":  "Arthron_CrushingClaw_v4_00001_.png",
    "Arthron_HardenedHide":  "Arthron_HardenedHide_v2_00001_.png",
    "Arthron_ApexCarapace":  "Arthron_ApexCarapace_v2_00001_.png",
    "Arthron_Spec":          "Arthron_Spec_v2_00001_.png",
    # Phase 3 — Carapace Gunner second spec row
    "ArthronGunner_Spec":              "ArthronGunner_Spec_v1_00001_.png",
    "ArthronGunner_SteadyAim":         "ArthronGunner_SteadyAim_v1_00001_.png",
    "ArthronGunner_SuppressPlates":    "ArthronGunner_SuppressPlates_v2_00001_.png",
    "ArthronGunner_LongBarrel":        "ArthronGunner_LongBarrel_v1_00001_.png",
    "ArthronGunner_ReturnFire":        "ArthronGunner_ReturnFire_v1_00001_.png",
    "ArthronGunner_Spotter":           "ArthronGunner_Spotter_v1_00001_.png",
    "ArthronGunner_OverwatchCarapace": "ArthronGunner_OverwatchCarapace_v1_00001_.png",
    # Phase 3 — arm-roll marker fallbacks
    "Arthron_ArmRight":                "Arthron_ArmRight_v3_00001_.png",
    "Arthron_ArmLeft":                 "Arthron_ArmLeft_v1_00001_.png",
}

# Phase 4 — row and cell icons (claw payloads, spray payloads, survival)
SOURCES_PHASE4 = {
    "ArthronClaw_Poison":      "ArthronClaw_Poison_v1_00001_.png",
    "ArthronClaw_Stun":        "ArthronClaw_Stun_v1_00001_.png",
    "ArthronClaw_Viral":       "ArthronClaw_Viral_v1_00001_.png",
    "ArthronSpray_Acid":       "ArthronSpray_Acid_v1_00001_.png",
    "ArthronSpray_Poison":     "ArthronSpray_Poison_v1_00001_.png",
    "ArthronSpray_Goo":        "ArthronSpray_Goo_v1_00001_.png",
    "ArthronSurvival_Panic":   "ArthronSurvival_Panic_v2_00001_.png",
    "ArthronSurvival_Poison":  "ArthronSurvival_Poison_v1_00001_.png",
    "ArthronSurvival_MC":      "ArthronSurvival_MC_v1_00001_.png",
    "ArthronSurvival_Daze":    "ArthronSurvival_Daze_v1_00001_.png",
    "ArthronSurvival_Fire":    "ArthronSurvival_Fire_v2_00001_.png",
    "ArthronSurvival_Regen":   "ArthronSurvival_Regen_v1_00001_.png",
}

# batches: (sources dict, output size). Run only the batch you are iterating
# on so earlier, already-committed icons are not rewritten.
BATCHES = {
    "phase1_3": (SOURCES, 256),
    "phase4":   (SOURCES_PHASE4, 128),  # loader expects 128x128
}
RUN = ["phase4"]


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
    for batch in RUN:
        sources, size = BATCHES[batch]
        for name, src_file in sources.items():
            src = os.path.join(SRC_DIR, src_file)
            if not os.path.isfile(src):
                print(f"MISSING: {src}")
                continue
            rgba = luminance_alpha(Image.open(src))
            rgba = rgba.resize((size, size), Image.LANCZOS)
            for d in DST_DIRS:
                out_path = os.path.join(d, f"{name}.png")
                rgba.save(out_path, "PNG")
                print(f"WROTE {out_path}  ({rgba.size}, {rgba.mode})")


if __name__ == "__main__":
    main()
