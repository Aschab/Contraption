import os, re, subprocess, sys, math
sys.path.insert(0, os.path.dirname(__file__))
from mapping import MAP, MUSIC
from check import real

PROJ = "/Volumes/Arturonchis/superserious/Contraption/Assets/ThirdParty"
PACK = {"U:": "UniversalSoundFX", "M:": "UltimateGameMusic"}
LOOPS = {"cable_travel": ("Sfx", -30), "lamp_buzz": ("Ambience", -42), "machine_idle": ("Ambience", -34), "workshop_ambience": ("Ambience", -27)}
PEAK = {"ctl_blocked": .6, "test_plunger": .85, "test_lever": .85, "machine_start": .6, "vial_fill": .5, "test_skip": .45,
        "result_match": .7, "result_mismatch": .55, "result_new": .55, "seal_stamp": .8, "machine_fixed": .85,
        "focus_move": .22, "magnify_in": .3, "magnify_out": .3, "machine_turn": .7, "machine_turn_end": .8,
        "cable_start": .5, "cable_node": .45, "cable_end": .55, "port_switch": .55, "book_lift": .45, "book_down": .6,
        "page_turn": .5, "book_end": .45, "log_write": .4, "log_select": .4, "log_delete": .4, "undo": .45, "reset": .6,
        "intro_whoosh": .5, "intro_land": .6, "level_start": .6}
CAP = {"ctl_lever_up": .7, "ctl_lever_down": .7, "ctl_blocked": .5, "test_lever": .8, "disp_light_on": .5, "disp_light_off": .5,
       "magnify_in": .4, "magnify_out": .4, "cable_node": .4, "machine_turn": 1.0, "log_delete": .8, "level_start": 5.0,
       "disp_gauge_move": .45, "tube_clink": .6, "ctl_dial_wrap": .45}

def peak_of(name):
    if name in PEAK: return PEAK[name]
    if name.startswith("ctl_"): return .75
    if name.startswith("disp_"): return .4
    if name.startswith("tube_"): return .5
    return .6

def run(args): subprocess.run(args, check=True, capture_output=True)

def level(f, kind):
    out = subprocess.run(["ffmpeg", "-hide_banner", "-i", f, "-af", "volumedetect", "-f", "null", "-"], capture_output=True, text=True).stderr
    return float(re.search(kind + r"_volume: (-?[\d.]+) dB", out).group(1))

manifest = []
for name, srcs in MAP.items():
    loop = name in LOOPS
    pack = PACK[srcs[0][:2]]
    dst_dir = os.path.join(PROJ, pack, "Resources/Audio", LOOPS[name][0] if loop else "Sfx")
    os.makedirs(dst_dir, exist_ok=True)
    for i, s in enumerate(srcs):
        src = real(s)
        dst = os.path.join(dst_dir, f"{name}.wav" if loop else f"{name}_{i}.wav")
        tmp = dst[:-4] + "_tmp.wav"
        if loop:
            ch = "2" if name == "workshop_ambience" else "1"
            run(["ffmpeg", "-y", "-i", src, "-ac", ch, "-ar", "48000", "-sample_fmt", "s16", tmp])
            gain = LOOPS[name][1] - level(tmp, "mean")
        else:
            af = "silenceremove=start_periods=1:start_threshold=-50dB:start_silence=0.004"
            if name in CAP:
                c = CAP[name]; af += f",atrim=0:{c},afade=t=out:st={c * 0.75:.3f}:d={c * 0.25:.3f}"
            run(["ffmpeg", "-y", "-i", src, "-af", af, "-ac", "1", "-ar", "48000", "-sample_fmt", "s16", tmp])
            gain = 20 * math.log10(peak_of(name)) - level(tmp, "max")
        run(["ffmpeg", "-y", "-i", tmp, "-af", f"volume={gain:.2f}dB", "-sample_fmt", "s16", dst])
        os.remove(tmp)
        manifest.append((pack, os.path.relpath(dst, os.path.join(PROJ, pack)), s[2:]))
    print(f"{name:20s} x{len(srcs)}")

mdir = os.path.join(PROJ, "UltimateGameMusic/Resources/Audio/Music")
os.makedirs(mdir, exist_ok=True)
for s in MUSIC:
    slug = re.sub(r"[^a-z0-9]+", "_", os.path.basename(s)[:-4].lower()).strip("_")
    dst = os.path.join(mdir, slug + ".ogg")
    gain = min(-20 - level(real(s), "mean"), -1 - level(real(s), "max"))   # plain gain keeps the loop seamless
    run(["ffmpeg", "-y", "-i", real(s), "-af", f"volume={gain:.2f}dB", "-ar", "44100", "-ac", "2", "-c:a", "vorbis", "-strict", "-2", "-q:a", "8", dst])
    manifest.append(("UltimateGameMusic", os.path.relpath(dst, os.path.join(PROJ, "UltimateGameMusic")), s[2:]))
    print("music", slug)

for pack in PACK.values():
    rows = [m for m in manifest if m[0] == pack]
    with open(os.path.join(PROJ, pack, "SOURCES.md"), "w") as f:
        f.write(f"# {pack}: files used by Contraption\n\nCopied from PurePackages and processed on import: leading silence removed, "
                "some capped with a fade, levelled, mono 48 kHz (music: OGG Vorbis, levelled). Renamed to the game's sound "
                "names; `<name>_<i>` files are random variations.\n\n| File | Source |\n|---|---|\n")
        for _, rel, src in rows: f.write(f"| `{rel}` | `{src}` |\n")
