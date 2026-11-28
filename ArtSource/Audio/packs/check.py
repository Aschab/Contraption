import os, subprocess, sys, glob
sys.path.insert(0, os.path.dirname(__file__)); from mapping import MAP, MUSIC
ROOT = {"U:": "/Volumes/Arturonchis/superserious/PurePackages/Assets/Universal Sound FX/", "M:": "/Volumes/Arturonchis/superserious/PurePackages/Assets/Ultimate Game Music Collection/"}
def real(p):
    full = ROOT[p[:2]] + p[2:]
    if os.path.exists(full): return full
    # tolerate _mono/_stereo/subfolder differences
    base = os.path.basename(p[2:]).replace("_mono.wav","").replace("_stereo.wav","").replace(".wav","")
    hits = glob.glob(ROOT[p[:2]] + "**/" + base + "*.wav", recursive=True)
    hits = [h for h in hits if os.path.basename(h).replace("_mono","").replace("_stereo","")[:-4] == base]
    return hits[0] if hits else None
def dur(f): return float(subprocess.run(["ffprobe","-v","error","-show_entries","format=duration","-of","csv=p=0",f],capture_output=True,text=True).stdout or 0)
if __name__ == "__main__":
  bad = 0
  for k, srcs in list(MAP.items()) + [("music", MUSIC)]:
    out = []
    for s in srcs:
        r = real(s)
        if r is None: out.append("MISSING " + s); bad += 1
        else: out.append(f"{dur(r):.2f}s")
    print(f"{k:20s} " + "  ".join(out))
  print("missing:", bad)
