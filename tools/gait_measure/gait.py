"""Gait curves from CMU BVH or from motion_dump JSON, on the same definitions, for comparing walks.
python3 -I gait.py file.bvh|file.json [...]  -> prints a table of features (averaged over strides)."""
import sys, json, math
import numpy as np
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bvh_measure as bm

N = 20

def from_bvh(path):
    joints, ft, data = bm.parse(path)
    names, P = bm.fk(joints, data, 1)
    J = {n: i for i, n in enumerate(names)}
    g = lambda n: P[:, J[n]].astype(float)
    d = dict(hipL=g("LeftUpLeg"), hipR=g("RightUpLeg"), kneeL=g("LeftLeg"), kneeR=g("RightLeg"), ankL=g("LeftFoot"), ankR=g("RightFoot"),
             toeL=g("LeftToeBase"), toeR=g("RightToeBase"), shL=g("LeftArm"), shR=g("RightArm"), elL=g("LeftForeArm"), elR=g("RightForeArm"),
             wrL=g("LeftHand"), wrR=g("RightHand"), neck=g("Neck") if "Neck" in J else g("Neck1"), head=g("Head"), spine=g("Spine") if "Spine" in J else g("Hips"))
    return d, ft

def from_json(path):
    c = json.load(open(path))
    fr = c["frames"]
    def o(n): return np.array([[f["parts"][n][3], f["parts"][n][7], f["parts"][n][11]] for f in fr])
    def ax(n, col): return np.array([[f["parts"][n][col], f["parts"][n][4 + col], f["parts"][n][8 + col]] for f in fr])
    d = dict(hipL=o("LeftThigh"), hipR=o("RightThigh"), kneeL=o("LeftShin"), kneeR=o("RightShin"), ankL=o("LeftFoot"), ankR=o("RightFoot"),
             shL=o("LeftUpperArm"), shR=o("RightUpperArm"), elL=o("LeftForearm"), elR=o("RightForearm"), wrL=o("LeftHand"), wrR=o("RightHand"),
             neck=o("Head"), spine=o("Torso"))
    d["toeL"] = d["ankL"] + ax("LeftFoot", 2) * 0.14 - ax("LeftFoot", 1) * 0.06
    d["toeR"] = d["ankR"] + ax("RightFoot", 2) * 0.14 - ax("RightFoot", 1) * 0.06
    d["head"] = d["neck"] + ax("Head", 1) * 0.15 + ax("Head", 2) * 0.0
    d["headfwd"] = ax("Head", 2)
    return d, c["seconds_per_frame"]

def analyse(d, dt, label):
    up = np.array([0, 1.0, 0])
    leg = float(np.median(np.linalg.norm(d["hipL"] - d["kneeL"], axis=1) + np.linalg.norm(d["kneeL"] - d["ankL"], axis=1)))
    pel = (d["hipL"] + d["hipR"]) / 2
    # heading from smoothed travel
    k = max(1, int(0.5 / dt))
    v = np.gradient(pel, dt, axis=0)
    vs = np.array([v[max(0, i - k):i + k + 1].mean(axis=0) for i in range(len(v))])
    hv = vs.copy(); hv[:, 1] = 0
    speed = np.linalg.norm(hv, axis=1)
    fwd = hv / (speed[:, None] + 1e-9)
    right = np.cross(up, fwd)
    if np.mean(np.sum(right * (d["hipR"] - d["hipL"]), axis=1)) < 0: right = -right
    # contacts: left heel strike = ankle height minimum crossing / foot speed lowest, use ankle-height local minima after swing
    ground = min(d["ankL"][:, 1].min(), d["ankR"][:, 1].min())
    aL = d["ankL"][:, 1] - ground
    vL = np.linalg.norm(np.gradient(d["ankL"], dt, axis=0)[:, [0, 2]], axis=1)
    # heel strike: ankle forward speed drops below 25% of body speed after being above 150%
    strikes = []
    fast = False
    for i in range(len(vL)):
        if vL[i] > 1.5 * speed[i]: fast = True
        elif fast and vL[i] < 0.3 * speed[i]:
            strikes.append(i); fast = False
    cycles = [(a, b) for a, b in zip(strikes, strikes[1:]) if 0.3 < (b - a) * dt < 2.5]
    # skip first/last cycles (start/stop)
    if len(cycles) > 4: cycles = cycles[1:-1]
    if not cycles: print(label, "no cycles"); return None
    def sag(vec):   # angle from straight down in the sagittal plane, + forward
        return np.degrees(np.arctan2(np.sum(vec * fwd, axis=1), -vec @ up))
    def flex(a, b, c):
        u = a - b; w = c - b
        cs = np.sum(u * w, axis=1) / (np.linalg.norm(u, axis=1) * np.linalg.norm(w, axis=1) + 1e-9)
        return 180 - np.degrees(np.arccos(np.clip(cs, -1, 1)))
    hipline = d["hipR"] - d["hipL"]
    shline = d["shR"] - d["shL"]
    def yaw(line): return np.degrees(np.arctan2(np.sum(line * fwd, axis=1), np.sum(line * right, axis=1)))
    def roll(line): return np.degrees(np.arctan2(line @ up, np.sum(line * right, axis=1)))
    trunk = d["neck"] - pel
    f = {
        "bob": (pel[:, 1] - ground) / leg,
        "sway": np.sum((pel - np.cumsum(np.vstack([pel[:1] * 0, hv[:-1] * dt]), axis=0) - pel[0]) * right, axis=1) / leg,
        "pelvisYaw": yaw(hipline), "pelvisRoll": roll(hipline), "shoulderYawRel": yaw(shline) - yaw(hipline), "shoulderRoll": roll(shline),
        "trunkPitch": np.degrees(np.arctan2(np.sum(trunk * fwd, axis=1), trunk @ up)),
        "hipFlexL": sag(d["kneeL"] - d["hipL"]), "kneeL": flex(d["hipL"], d["kneeL"], d["ankL"]),
        "ankleH": aL / leg, "footPitchL": np.degrees(np.arctan2((d["toeL"] - d["ankL"]) @ up, np.sum((d["toeL"] - d["ankL"]) * fwd, axis=1))),
        "shFlexL": sag(d["elL"] - d["shL"]), "elbowL": flex(d["shL"], d["elL"], d["wrL"]), "shFlexR": sag(d["elR"] - d["shR"]),
        "headH": (d["neck"][:, 1] - ground) / leg,
    }
    # sway: lateral offset of pelvis relative to its own average line (detrend per cycle)
    res = {}
    for name, x in f.items():
        curves = []
        for a, b in cycles:
            seg = x[a:b + 1]
            if name == "sway":
                seg = seg - np.linspace(seg[0], seg[-1], len(seg))
            t = np.linspace(0, 1, len(seg))
            curves.append(np.interp(np.linspace(0, 1, N, endpoint=False), t, seg))
        c = np.mean(curves, axis=0)
        res[name] = c
    cad = 1.0 / np.mean([(b - a) * dt for a, b in cycles])
    spd = float(np.mean([speed[a:b].mean() for a, b in cycles])) / leg
    return dict(label=label, leg=leg, cadence=cad, speed=spd, cycles=len(cycles), curves=res)

def show(rs):
    rs = [r for r in rs if r]
    print("%-14s" % "", "".join("%22s" % r["label"][:21] for r in rs))
    print("%-14s" % "strides/s", "".join("%22.2f" % r["cadence"] for r in rs))
    print("%-14s" % "legs/s", "".join("%22.2f" % r["speed"] for r in rs))
    print("%-14s" % "stride/leg", "".join("%22.2f" % (r["speed"] / r["cadence"]) for r in rs))
    for name in rs[0]["curves"]:
        row = []
        for r in rs:
            c = r["curves"][name]
            row.append("%7.2f..%-7.2f m%5.2f" % (c.min(), c.max(), c.mean()) if abs(c).max() < 5 else "%7.1f..%-7.1f m%5.1f" % (c.min(), c.max(), c.mean()))
        print("%-14s" % name, "".join("%22s" % s for s in row))

def curves(r, names):
    for n in names:
        print(r["label"][:10], "%-10s" % n, " ".join("%6.1f" % v if abs(v) > 2 else "%6.3f" % v for v in r["curves"][n]))

if __name__ == "__main__":
    rs = []
    for p in sys.argv[1:]:
        if p.startswith("--"): continue
        d, dt = from_bvh(p) if p.endswith(".bvh") else from_json(p)
        rs.append(analyse(d, dt, p.split("/")[-1]))
    show(rs)
    if "--curves" in sys.argv:
        for r in rs:
            if r: curves(r, ["kneeL", "hipFlexL", "shFlexL", "elbowL", "bob", "pelvisYaw", "shoulderYawRel", "pelvisRoll", "footPitchL", "ankleH"])
