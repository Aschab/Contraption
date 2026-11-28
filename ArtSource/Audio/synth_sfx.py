# Synthesises every non-ambience sound effect (python3, stdlib only): python3 ArtSource/Audio/synth_sfx.py
# One-shots get VARIANTS files (<name>_<i>.wav) that the game picks between at random; loops get one seamless
# <name>.wav. Output: Assets/_Project/Resources/Audio/Sfx (48 kHz, 16-bit mono). Deterministic per name/variant.
import math, os, random, struct, wave, zlib

SR = 48000
VARIANTS = 3
OUT = os.path.join(os.path.dirname(__file__), "../../Assets/_Project/Resources/Audio/Sfx")
TAU = 2 * math.pi

# ---------------------------------------------------------------- building blocks (lists of floats)
def N(sec): return max(1, int(sec * SR))
def silence(sec): return [0.0] * N(sec)

def mix(dst, src, at=0.0, gain=1.0):
    o = N(at) if at > 0 else 0
    if len(dst) < o + len(src): dst.extend([0.0] * (o + len(src) - len(dst)))
    for i, v in enumerate(src): dst[o + i] += v * gain
    return dst

def noise(sec, rng): return [rng.uniform(-1, 1) for _ in range(N(sec))]

def env(x, attack=0.002, decay=0.1, hold=0.0):
    a, h = N(attack), N(hold)
    out = []
    for i, v in enumerate(x):
        if i < a: g = i / a
        elif i < a + h: g = 1.0
        else: g = math.exp(-(i - a - h) / (decay * SR))
        out.append(v * g)
    return out

def shape(x, points):
    # Piecewise-linear gain over the sound's length: points = [(t0, g0), (t1, g1), ...] with t in 0..1.
    n = len(x); out = []
    for i, v in enumerate(x):
        t = i / n
        for k in range(len(points) - 1):
            (t0, g0), (t1, g1) = points[k], points[k + 1]
            if t0 <= t <= t1:
                g = g0 + (g1 - g0) * ((t - t0) / (t1 - t0) if t1 > t0 else 0); break
        else: g = points[-1][1]
        out.append(v * g)
    return out

def lowpass(x, fc):
    a = math.exp(-TAU * fc / SR); y = 0.0; out = []
    for v in x: y = (1 - a) * v + a * y; out.append(y)
    return out

def highpass(x, fc):
    lp = lowpass(x, fc)
    return [v - l for v, l in zip(x, lp)]

def bandpass(x, fc, q=2.0):
    # RBJ biquad; fc may be a function of t in 0..1 (sweeps).
    out = []; x1 = x2 = y1 = y2 = 0.0; n = len(x)
    for i, v in enumerate(x):
        f = fc(i / n) if callable(fc) else fc
        w = TAU * min(f, SR * 0.45) / SR; al = math.sin(w) / (2 * q); c = math.cos(w)
        b0, b2, a0, a1, a2 = al, -al, 1 + al, -2 * c, 1 - al
        y = (b0 * v + b2 * x2 - a1 * y1 - a2 * y2) / a0
        x2, x1, y2, y1 = x1, v, y1, y
        out.append(y)
    return out

def tone(sec, f0, f1=None, kind="sine", vib=0.0, vib_rate=6.0):
    # Oscillator with an exponential pitch glide f0 -> f1.
    n = N(sec); f1 = f0 if f1 is None else f1; ph = 0.0; out = []
    for i in range(n):
        t = i / n
        f = f0 * (f1 / f0) ** t * (1 + vib * math.sin(TAU * vib_rate * i / SR))
        ph += TAU * f / SR
        if kind == "sine": s = math.sin(ph)
        elif kind == "saw": s = sum(math.sin(ph * k) / k for k in range(1, 7)) * 0.6
        elif kind == "square": s = sum(math.sin(ph * k) / k for k in (1, 3, 5, 7)) * 0.8
        else: s = math.sin(ph) + 0.3 * math.sin(2 * ph)
        out.append(s)
    return out

def modal(sec, modes, rng=None):
    # Struck object: decaying sinusoids (freq, decay seconds, amplitude).
    n = N(sec); out = [0.0] * n
    for f, d, a in modes:
        ph = rng.uniform(0, TAU) if rng else 0.0
        for i in range(n): out[i] += a * math.sin(ph + TAU * f * i / SR) * math.exp(-i / (d * SR))
    return out

def click(rng, f=3000, body=None, decay=0.004, ring=0.015):
    x = env(highpass(noise(0.03, rng), f * 0.5), 0.0005, decay)
    if body: mix(x, modal(0.12, [(body, ring, 0.6), (body * 2.71, ring * 0.6, 0.3)], rng))
    return x

def bubble(rng, f0, dur, rise=None):
    rise = rise if rise is not None else rng.uniform(1.5, 4.0)
    n = N(dur); ph = 0.0; out = []
    for i in range(n):
        t = i / SR; ph += TAU * f0 * (1 + rise * t / dur * 0.5) / SR
        out.append(math.sin(ph) * math.exp(-t / (dur * 0.35)) * min(1.0, i / 40))
    return out

def bubbles(rng, sec, count, fmin, fmax, dmin=0.02, dmax=0.07, gain=0.5):
    x = silence(sec)
    for _ in range(count):
        f = rng.uniform(fmin, fmax); d = rng.uniform(dmin, dmax)
        mix(x, bubble(rng, f, d), rng.uniform(0, max(0.0, sec - d)), gain * rng.uniform(0.4, 1.0))
    return x[:N(sec)]

def whoosh(rng, sec, f0, f1, q=1.2, points=((0, 0), (0.5, 1), (1, 0))):
    return shape(bandpass(noise(sec, rng), lambda t: f0 * (f1 / f0) ** t, q), list(points))

def bell(rng, f, sec=0.8, decay=0.35):
    return modal(sec, [(f, decay, 1.0), (f * 2.76, decay * 0.5, 0.45), (f * 5.4, decay * 0.25, 0.25), (f * 0.5, decay * 1.2, 0.2)], rng)

def glass(rng, f, sec=0.4):
    return modal(sec, [(f, 0.12, 1.0), (f * 2.32, 0.08, 0.6), (f * 3.8, 0.05, 0.4)], rng)

def clunk(rng, f=180, sec=0.3, decay=0.08):
    x = modal(sec, [(f, decay, 1.0), (f * 2.33, decay * 0.6, 0.5), (f * 5.1, decay * 0.3, 0.3)], rng)
    return mix(x, env(lowpass(noise(0.05, rng), 1500), 0.001, 0.012), 0, 0.8)

def thud(rng, f=80, sec=0.25):
    x = env(tone(sec, f * 1.6, f), 0.002, 0.06)
    return mix(x, env(lowpass(noise(0.08, rng), 600), 0.001, 0.03), 0, 0.7)

def crackle(rng, sec, count, f=3000):
    x = silence(sec)
    for _ in range(count): mix(x, click(rng, f * rng.uniform(0.7, 1.5), decay=0.0015), rng.uniform(0, sec - 0.03), rng.uniform(0.2, 1.0))
    return x[:N(sec)]

def scratch(rng, sec, strokes, f=3200):
    x = bandpass(noise(sec, rng), f, 1.5); n = len(x); r = rng.uniform(0, TAU)
    return [v * max(0.0, math.sin(r + TAU * strokes * i / n * sec)) ** 2 for i, v in enumerate(x)]

def seq(parts):
    out = []
    for at, sound, gain in parts: mix(out, sound, at, gain)
    return out

def J(rng, v, amount=0.06): return v * rng.uniform(1 - amount, 1 + amount)

# ---------------------------------------------------------------- one-shots: name -> (fn(rng) -> samples, peak)
def ratchet(rng, count, base, step, gap=0.028):
    return seq([(i * J(rng, gap, 0.15), click(rng, base + step * i, body=(base + step * i) * 0.4, ring=0.01), 0.6) for i in range(count)])

ONE = {
    # controls
    "ctl_button": (lambda r: seq([(0, click(r, J(r, 2200), body=J(r, 320), ring=0.03), 1), (0, thud(r, J(r, 110), 0.12), 0.6),
                                  (J(r, 0.11), click(r, J(r, 3200), body=J(r, 520), ring=0.015), 0.5)]), 0.8),
    "ctl_lever_up": (lambda r: seq([(0, ratchet(r, 3, J(r, 1800), 400), 1), (0.09, clunk(r, J(r, 230)), 1)]), 0.85),
    "ctl_lever_down": (lambda r: seq([(0, ratchet(r, 3, J(r, 2400), -400), 1), (0.09, clunk(r, J(r, 150), decay=0.1), 1)]), 0.85),
    "ctl_dial_click": (lambda r: seq([(0, click(r, J(r, 2600), body=J(r, 1800), ring=0.025), 1)]), 0.7),
    "ctl_dial_wrap": (lambda r: seq([(0, click(r, J(r, 2600), body=J(r, 1800), ring=0.025), 0.8), (0.05, clunk(r, J(r, 210), 0.35, 0.12), 1),
                                     (0.05, env(tone(0.25, J(r, 420), 300, vib=0.04, vib_rate=30), 0.003, 0.08), 0.25)]), 0.85),
    "ctl_knob_click": (lambda r: seq([(0, click(r, J(r, 1500), body=J(r, 900), ring=0.02), 1), (0, env(lowpass(noise(0.05, r), 1200), 0.005, 0.02), 0.3)]), 0.65),
    "ctl_slider_step": (lambda r: seq([(0, env(bandpass(noise(0.08, r), J(r, 1500), 2), 0.01, 0.03), 0.7), (0.05, click(r, J(r, 2400), body=J(r, 1100)), 0.8)]), 0.65),
    "ctl_joystick_push": (lambda r: seq([(0, click(r, J(r, 1900), body=J(r, 600)), 0.8), (0.01, env(tone(0.16, J(r, 320), 190, vib=0.05, vib_rate=28), 0.003, 0.05), 0.5)]), 0.7),
    "ctl_joystick_center": (lambda r: seq([(0, env(tone(0.14, J(r, 260), 380, vib=0.05, vib_rate=30), 0.003, 0.04), 0.5), (0.05, click(r, J(r, 2600), body=J(r, 700)), 0.9)]), 0.7),
    "ctl_blocked": (lambda r: seq([(0, thud(r, J(r, 85), 0.2), 1), (0.01, env(tone(0.12, J(r, 110), kind="square"), 0.003, 0.05), 0.2)]), 0.6),
    # test run
    "test_plunger": (lambda r: seq([(0, whoosh(r, 0.3, 3200, 700, 1.5, ((0, 0), (0.1, 1), (1, 0))), 0.5), (0.05, thud(r, J(r, 70), 0.3), 1), (0.05, clunk(r, J(r, 300)), 0.5)]), 0.9),
    "test_lever": (lambda r: seq([(0, ratchet(r, 5, J(r, 1400), 250, 0.035), 1), (0.18, clunk(r, J(r, 130), 0.45, 0.14), 1), (0.3, click(r, 2600, body=900), 0.4)]), 0.9),
    "machine_start": (lambda r: seq([(0, shape(tone(1.0, J(r, 45), J(r, 120), kind="saw"), [(0, 0), (0.3, 0.8), (0.8, 0.6), (1, 0)]), 0.5),
                                    (0, crackle(r, 1.0, 14, 1800), 0.4), (0, clunk(r, J(r, 120)), 0.6)]), 0.75),
    "vial_fill": (lambda r: seq([(0, bubbles(r, 0.25, 8, 500, 1100, 0.02, 0.05), 1), (0.25, glass(r, J(r, 2800), 0.3), 0.35)]), 0.6),
    "test_skip": (lambda r: whoosh(r, 0.14, J(r, 700), 4000, 1.4), 0.5),
    # result
    "result_match": (lambda r: seq([(0, bell(r, [523.3, 587.3, 659.3][r.randrange(3)]), 0.8), (0.13, bell(r, 784.0 * r.choice([1, 1.0595])), 1)]), 0.7),
    "result_mismatch": (lambda r: seq([(0, env(lowpass(tone(0.35, J(r, 330, 0.03), kind="square"), 1400), 0.01, 0.12), 0.8),
                                      (0.16, env(lowpass(tone(0.45, J(r, 311, 0.03), kind="square"), 1100), 0.01, 0.18), 0.8)]), 0.55),
    "result_new": (lambda r: bell(r, J(r, 880, 0.03), 0.7, 0.3), 0.55),
    "seal_stamp": (lambda r: seq([(0, thud(r, J(r, 75), 0.3), 1), (0.01, env(lowpass(noise(0.15, r), 900), 0.005, 0.05), 0.6), (0.0, click(r, 1500), 0.3)]), 0.85),
    "machine_fixed": (lambda r: seq([(i * 0.12, bell(r, f, 1.6, 0.7), 0.7) for i, f in enumerate(r.choice([[523.3, 659.3, 784.0, 1046.5], [392.0, 523.3, 659.3, 784.0], [523.3, 784.0, 659.3, 1046.5]]))]
                                    + [(0.4, shape(highpass(noise(1.4, r), 6000), [(0, 0), (0.3, 1), (1, 0)]), 0.15), (0, shape(tone(2.0, 130.8, kind="soft"), [(0, 0), (0.2, 1), (1, 0)]), 0.2)]), 0.85),
    # displays
    "disp_light_on": (lambda r: seq([(0, click(r, J(r, 4000)), 0.4), (0, env(tone(0.12, J(r, 220), 440), 0.005, 0.05), 0.5)]), 0.45),
    "disp_light_off": (lambda r: seq([(0, click(r, J(r, 3000)), 0.4), (0, env(tone(0.12, J(r, 440), 180), 0.005, 0.04), 0.4)]), 0.4),
    "disp_bar_up": (lambda r: env(tone(0.07, J(r, 620), 950, kind="soft"), 0.003, 0.03), 0.4),
    "disp_bar_down": (lambda r: env(tone(0.07, J(r, 950), 620, kind="soft"), 0.003, 0.03), 0.4),
    "disp_gauge_move": (lambda r: seq([(0, env(bandpass(noise(0.15, r), J(r, 2200), 3), 0.01, 0.05), 0.5)] + [(i * 0.035, click(r, J(r, 3500), decay=0.002), 0.5) for i in range(3)]), 0.4),
    "disp_glyph_change": (lambda r: seq([(0, env(tone(0.05, J(r, 1250), kind="square"), 0.002, 0.03), 0.35), (0.06, env(tone(0.05, J(r, 1870), kind="square"), 0.002, 0.03), 0.35)]), 0.35),
    "disp_script_type": (lambda r: seq([(0, click(r, J(r, 3800), body=J(r, 650), ring=0.012), 1)]), 0.4),
    # navigation
    "focus_move": (lambda r: seq([(0, env(tone(0.03, J(r, 1900)), 0.001, 0.008), 0.6), (0, click(r, 5000, decay=0.001), 0.2)]), 0.22),
    "magnify_in": (lambda r: whoosh(r, 0.2, J(r, 400), 1800, 1.5), 0.3),
    "magnify_out": (lambda r: whoosh(r, 0.2, J(r, 1800), 400, 1.5), 0.25),
    "machine_turn": (lambda r: seq([(0, shape(lowpass(noise(0.9, r), 220), [(0, 0), (0.2, 1), (0.8, 1), (1, 0)]), 1), (0, crackle(r, 0.9, 12, 1500), 0.5),
                                   (0.1, shape(tone(0.7, J(r, 85), 95, vib=0.03, vib_rate=5), [(0, 0), (0.3, 1), (1, 0)]), 0.3)]), 0.75),
    "machine_turn_end": (lambda r: seq([(0, clunk(r, J(r, 100), 0.4, 0.13), 1), (0, thud(r, J(r, 60)), 0.8)]), 0.85),
    # rear view
    "cable_start": (lambda r: seq([(0, shape(tone(0.28, J(r, 200), 1200, kind="saw"), [(0, 0), (0.6, 1), (1, 0)]), 0.4), (0, crackle(r, 0.28, 6, 5000), 0.4)]), 0.5),
    "cable_node": (lambda r: seq([(0, env(tone(0.1, J(r, 900), kind="square"), 0.002, 0.03), 0.4), (0, crackle(r, 0.08, 3, 5000), 0.4)]), 0.45),
    "cable_end": (lambda r: seq([(0, shape(tone(0.7, f), [(0, 0), (0.25, 1), (1, 0)]), 0.3) for f in (220 * J(r, 1, 0.01), 330, 440)] + [(0.15, crackle(r, 0.3, 6, 6000), 0.3)]), 0.55),
    # sample tubes
    "tube_lift": (lambda r: seq([(0, glass(r, J(r, 2400)), 0.8), (0.02, whoosh(r, 0.15, 900, 2500, 2), 0.3)]), 0.55),
    "tube_drop": (lambda r: seq([(0, glass(r, J(r, 1700)), 0.7), (0, thud(r, J(r, 160), 0.12), 0.3)]), 0.5),
    "tube_clink": (lambda r: seq([(0, glass(r, J(r, 3000)), 0.8), (J(r, 0.07), glass(r, J(r, 3400)), 0.5)]), 0.5),
    "port_switch": (lambda r: seq([(0, click(r, J(r, 2000), body=J(r, 500)), 1), (0.05, click(r, J(r, 2600), body=J(r, 700)), 0.7)]), 0.55),
    # books
    "book_lift": (lambda r: seq([(0, whoosh(r, 0.3, J(r, 500), 1600, 0.9), 0.6), (0.02, env(bandpass(noise(0.2, r), 3500, 1.2), 0.02, 0.06), 0.3)]), 0.5),
    "book_down": (lambda r: seq([(0, thud(r, J(r, 110), 0.25), 0.8), (0, env(bandpass(noise(0.2, r), 2500, 1), 0.005, 0.05), 0.4)]), 0.6),
    "page_turn": (lambda r: seq([(0, whoosh(r, J(r, 0.32), J(r, 1800), 5000, 0.9, ((0, 0), (0.25, 1), (0.7, 0.6), (1, 0))), 0.8), (0.05, crackle(r, 0.25, 6, 2500), 0.25)]), 0.5),
    "book_end": (lambda r: thud(r, J(r, 120), 0.2), 0.45),
    "log_write": (lambda r: shape(scratch(r, J(r, 0.5), J(r, 9)), [(0, 0), (0.08, 1), (0.85, 1), (1, 0)]), 0.35),
    "log_select": (lambda r: click(r, J(r, 2200), body=J(r, 480), ring=0.02), 0.4),
    "log_delete": (lambda r: seq([(0, scratch(r, 0.35, J(r, 16), 2600), 0.8), (0.33, env(bandpass(noise(0.12, r), 3000, 1), 0.002, 0.03), 0.6)]), 0.4),
    # undo / reset
    "undo": (lambda r: seq([(0, whoosh(r, 0.25, J(r, 2500), 600, 1.3, ((0, 0), (0.85, 1), (1, 0))), 0.6), (0.18, env(tone(0.1, 700, 450), 0.002, 0.04), 0.3)]), 0.45),
    "reset": (lambda r: seq([(0, whoosh(r, 0.55, J(r, 3000), 300, 1.1, ((0, 0), (0.7, 1), (1, 0))), 0.7)] + [(0.05 + i * 0.06, click(r, J(r, 2400), body=J(r, 600)), 0.5) for i in range(6)]), 0.65),
    # scene
    "intro_whoosh": (lambda r: whoosh(r, 2.6, J(r, 250), 1400, 0.8, ((0, 0), (0.4, 1), (0.8, 0.8), (1, 0))), 0.5),
    "intro_land": (lambda r: seq([(0, thud(r, J(r, 70), 0.4), 0.7), (0.05, bell(r, J(r, 392, 0.02), 1.0, 0.4), 0.35)]), 0.6),
    "level_start": (lambda r: seq([(i * 0.16, bell(r, f, 1.0, 0.45), 0.6) for i, f in enumerate(r.choice([[392.0, 523.3, 659.3], [440.0, 587.3, 659.3], [392.0, 587.3, 784.0]]))]), 0.6),
}

# ---------------------------------------------------------------- loops: name -> (fn(rng, sec) -> samples, seconds, peak)
def hum(rng, sec):
    x = [0.0] * N(sec)
    for k, a in ((1, 1.0), (2, 0.5), (3, 0.35), (5, 0.15)):
        for i in range(len(x)): x[i] += a * math.sin(TAU * 55 * k * i / SR)
    return mix(x, crackle(rng, sec, int(sec * 6), 5000), 0, 0.5)

LOOPS = {
    "cable_travel": (lambda r, s: hum(r, s), 2.0, 0.35),
}

# ---------------------------------------------------------------- output
def normalize(x, peak):
    m = max(1e-9, max(abs(v) for v in x))
    x = [v * peak / m for v in x]
    f = min(len(x) // 4, N(0.004))
    for i in range(f): x[-1 - i] *= i / f   # click-free tail
    return x

def seamless(x, fade):
    # Crossfade the extra tail into the head so the loop has no seam.
    f = N(fade); body = x[:len(x) - f]
    for i in range(f):
        k = i / f
        body[i] = body[i] * math.sqrt(k) + x[len(x) - f + i] * math.sqrt(1 - k)
    return body

def write(path, x):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, v)) * 32767)) for v in x))

# Sounds imported from asset packs (Assets/ThirdParty/*/Resources/Audio) win; don't generate duplicates of them.
def provided():
    import glob, re
    root = os.path.join(os.path.dirname(__file__), "../../Assets/ThirdParty")
    return {re.sub(r"_\d+$", "", os.path.basename(f)[:-4]) for f in glob.glob(root + "/*/Resources/Audio/*/*.*") if not f.endswith(".meta")}

def main():
    skip = provided()
    for name, (fn, peak) in ONE.items():
        if name in skip: continue
        for v in range(VARIANTS):
            rng = random.Random(zlib.crc32(f"{name}/{v}".encode()))
            x = fn(rng)
            write(os.path.join(OUT, f"{name}_{v}.wav"), normalize(x, peak))
    for name, (fn, sec, peak) in LOOPS.items():
        if name in skip: continue
        rng = random.Random(zlib.crc32(name.encode()))
        x = fn(rng, sec + 0.5)
        m = max(abs(v) for v in x)
        write(os.path.join(OUT, f"{name}.wav"), [v * peak / m for v in seamless(x, 0.5)])
    print("skipped (provided by packs):", len(skip))

if __name__ == "__main__":
    main()
