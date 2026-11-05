# Picks clip ranges from the per-frame metrics written by bvh_to_fbx.py.
#   python analyze_clips.py <out_dir>   -> <out_dir>/clip_plan.json
#
# Frames are absolute take frames (the FBX takes start at frame 2, 30 fps), which is what
# Unity's ModelImporterClipAnimation.firstFrame/lastFrame expect.
import json, math, os, sys

OUT = sys.argv[1] if len(sys.argv) > 1 else r"C:/LudusForge/Tools/survivor/out"
MET = os.path.join(OUT, "anim_metrics")
FPS = 30.0

def load(clip):
    d = json.load(open(os.path.join(MET, clip + ".json")))
    return d["first_frame"], d["frames"]

def ang(a, b):
    return (b - a + 180.0) % 360.0 - 180.0

def contacts(foot):
    """Heel strikes: the frames where the foot settles onto the ground."""
    lo = min(foot)
    out, down = [], False
    for i in range(1, len(foot) - 1):
        near = foot[i] < lo + 0.025
        if near and not down and foot[i] <= foot[i - 1] and foot[i] <= foot[i + 1]:
            out.append(i)
            down = True
        elif not near:
            down = False
    return out

def loop(clip, cycles=1):
    """Best single gait cycle (left strike to left strike): straightest and steadiest."""
    f0, fr = load(clip)
    lf = [min(r[4], r[6]) for r in fr]
    strikes = contacts(lf)
    speeds = []
    best = None
    for a, b in zip(strikes, strikes[cycles:]):
        if b - a < 12:
            continue
        dx, dy = fr[b][0] - fr[a][0], fr[b][1] - fr[a][1]
        dist = math.hypot(dx, dy)
        speed = dist / ((b - a) / FPS)
        turn = abs(ang(fr[a][3], fr[b][3]))
        wobble = max(abs(ang(fr[a][3], fr[i][3])) for i in range(a, b + 1))
        speeds.append(speed)
        score = turn * 2 + wobble
        if best is None or score < best[0]:
            best = (score, a, b, speed, turn)
    if best is None:
        return {"clip": clip, "error": "no cycle found", "strikes": strikes}
    _, a, b, speed, turn = best
    return {"clip": clip, "first": f0 + a, "last": f0 + b, "loop": True, "speed": round(speed, 3),
            "turn_deg": round(turn, 1), "cycle_s": round((b - a) / FPS, 3)}

def idle(clip, length_s=None):
    """A stretch whose ends match best (hips height and feet), for a seamless idle."""
    f0, fr = load(clip)
    n = len(fr)
    best = None
    lo = int(2.5 * FPS)
    for a in range(0, n - lo):
        for b in range(a + lo, n):
            ra, rb = fr[a], fr[b]
            d = abs(ra[2] - rb[2]) * 4 + abs(ra[4] - rb[4]) + abs(ra[5] - rb[5]) + abs(ang(ra[3], rb[3])) / 90.0
            if len(ra) > 8:
                d += sum(abs(ra[i] - rb[i]) for i in range(10, 14)) * 0.5   # hands
            d -= (b - a) / n * 0.02                                         # prefer longer
            if best is None or d < best[0]:
                best = (d, a, b)
    _, a, b = best
    return {"clip": clip, "first": f0 + a, "last": f0 + b, "loop": True, "speed": 0.0,
            "length_s": round((b - a) / FPS, 2), "match": round(best[0], 4)}

def jump(clip):
    f0, fr = load(clip)
    feet = [min(r[4], r[5], r[6], r[7]) for r in fr]
    ground = sorted(feet)[len(feet) // 10]
    air = [i for i, h in enumerate(feet) if h > ground + 0.06]
    take, land = air[0], air[-1] + 1
    apex = max(range(take, land), key=lambda i: fr[i][2])
    return {"clip": clip,
            "segments": {
                "JumpStart": [f0 + max(0, take - 9), f0 + take],
                "AirRise":   [f0 + take, f0 + apex],
                "AirFall":   [f0 + apex, f0 + land],
                "Land":      [f0 + land, f0 + min(len(fr) - 1, land + 14)]}}

def kneel(clip):
    """Getting down onto the knees, and a held stretch while down."""
    f0, fr = load(clip)
    hz = [r[2] for r in fr]
    stand, low = max(hz[:30]), min(hz)
    down = [i for i, h in enumerate(hz) if h < low + 0.04]
    start = max(i for i in range(0, down[0]) if hz[i] > stand - 0.03)
    hold_a = down[0]
    hold_b = down[0]
    while hold_b + 1 < len(hz) and hz[hold_b + 1] < low + 0.05:
        hold_b += 1
    return {"clip": clip, "hips_stand": round(stand, 3), "hips_low": round(low, 3),
            "segments": {"PrayEnter": [f0 + start, f0 + hold_a],
                         "Pray": [f0 + hold_a, f0 + max(hold_a + 20, hold_b)]}}

def slashes(clip, n=3):
    """Sword swings: the fastest right-hand moves, each from wind-up to recovery."""
    f0, fr = load(clip)
    sp = [0.0] + [math.dist(fr[i][8:11], fr[i - 1][8:11]) * FPS for i in range(1, len(fr))]
    peaks = sorted(range(len(sp)), key=lambda i: -sp[i])
    picked = []
    for p in peaks:
        if all(abs(p - q) > 20 for q in picked):
            picked.append(p)
        if len(picked) == n:
            break
    out = []
    for p in sorted(picked):
        a = p
        while a > 0 and sp[a - 1] > 0.8:
            a -= 1
        b = p
        while b + 1 < len(sp) and sp[b + 1] > 0.8:
            b += 1
        out.append({"peak": f0 + p, "first": f0 + max(0, a - 4), "last": f0 + min(len(fr) - 1, b + 6),
                    "peak_speed": round(sp[p], 2), "hand_z_at_peak": fr[p][10]})
    # A guard: the longest still stretch with the hand raised in front.
    best, run = None, 0
    for i in range(1, len(fr)):
        still = sp[i] < 0.35 and fr[i][10] > fr[i][2] + 0.25
        run = run + 1 if still else 0
        if still and (best is None or run > best[1]):
            best = (i, run)
    guard = {"first": f0 + best[0] - best[1] + 1, "last": f0 + best[0]} if best else None
    return {"clip": clip, "slashes": out, "guard": guard}

plan = {
    "walk_careful": loop("77_28"), "walk": loop("35_01"), "walk_silent": loop("77_31"),
    "jog": loop("35_17"), "run": loop("09_02"), "run_careful": loop("77_10"),
    "creep": loop("77_14"), "crawl": loop("111_03"),
    "idle_flashlight": idle("77_05"), "idle_standing": idle("77_02"),
    "jump": jump("16_01"), "kneel": kneel("23_03"), "sword": slashes("02_07"),
}
json.dump(plan, open(os.path.join(OUT, "clip_plan.json"), "w"), indent=1)
for k, v in plan.items():
    print(k, json.dumps(v))
