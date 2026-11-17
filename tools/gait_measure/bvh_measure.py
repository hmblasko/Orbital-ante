"""Measure CMU mocap BVH clips: timings and angles used to tune the procedural enemy animation.

Usage: python3 -I bvh_measure.py <file.bvh> ...  Prints a JSON summary per clip.
Everything is normalised to leg length (hip joint to ankle), so it can be carried to the game's skeleton.
"""
import json, math, sys
import numpy as np


def parse(path):
    with open(path) as f:
        tokens = f.read().split()
    i = 0
    joints = []  # (name, parent, offset, channels)
    stack = []
    while tokens[i] != "MOTION":
        t = tokens[i]
        if t in ("ROOT", "JOINT"):
            name = tokens[i + 1]
            joints.append([name, stack[-1] if stack else -1, None, []])
            i += 2
        elif t == "End":
            joints.append(["End_" + joints[stack[-1]][0], stack[-1], None, []])
            i += 2
        elif t == "{":
            stack.append(len(joints) - 1)
            i += 1
        elif t == "}":
            stack.pop()
            i += 1
        elif t == "OFFSET":
            joints[stack[-1]][2] = np.array([float(x) for x in tokens[i + 1:i + 4]])
            i += 4
        elif t == "CHANNELS":
            n = int(tokens[i + 1])
            joints[stack[-1]][3] = tokens[i + 2:i + 2 + n]
            i += 2 + n
        else:
            i += 1
    frames = int(tokens[i + 2])
    ft = float(tokens[i + 5])
    data = np.array([float(x) for x in tokens[i + 6:]]).reshape(frames, -1)
    return joints, ft, data


def rot(axis, deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    if axis == "X": return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
    if axis == "Y": return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


def fk(joints, data, step=1):
    names = [j[0] for j in joints]
    out = []
    for f in range(0, len(data), step):
        row = data[f]
        k = 0
        pos = [None] * len(joints)
        rotm = [None] * len(joints)
        for ji, (name, parent, offset, ch) in enumerate(joints):
            local_p = offset.copy()
            r = np.eye(3)
            for c in ch:
                v = row[k]; k += 1
                if c.endswith("position"):
                    local_p["XYZ".index(c[0])] = v if parent == -1 else local_p["XYZ".index(c[0])]
                else:
                    r = r @ rot(c[0], v)
            if parent == -1:
                pos[ji] = local_p
                rotm[ji] = r
            else:
                pos[ji] = pos[parent] + rotm[parent] @ local_p
                rotm[ji] = rotm[parent] @ r
        out.append(np.array(pos))
    return names, np.array(out)


def ang(a, b):
    a = a / (np.linalg.norm(a) + 1e-9); b = b / (np.linalg.norm(b) + 1e-9)
    return math.degrees(math.acos(max(-1, min(1, float(a @ b)))))


def summary(path, step=4):
    joints, ft, data = parse(path)
    names, P = fk(joints, data, step)
    dt = ft * step
    J = {n: i for i, n in enumerate(names)}
    g = lambda n: P[:, J[n]]
    hips, neck, head = g("Hips"), g("Neck") if "Neck" in J else g("Neck1"), g("Head")
    lul, lleg, lfoot = g("LeftUpLeg"), g("LeftLeg"), g("LeftFoot")
    rul, rleg, rfoot = g("RightUpLeg"), g("RightLeg"), g("RightFoot")
    lhand, rhand = g("LeftHand"), g("RightHand")
    lsh, rsh = g("LeftArm"), g("RightArm")
    leg = float(np.median(np.linalg.norm(lul - lleg, axis=1) + np.linalg.norm(lleg - lfoot, axis=1)))
    up = np.array([0, 1, 0])
    # Ground: the lowest foot over the clip.
    ground = float(min(lfoot[:, 1].min(), rfoot[:, 1].min()))
    # Facing, per frame: the line between the hip joints gives "right"; the toes say which way is forward.
    side = rul - lul; side[:, 1] = 0
    side /= np.linalg.norm(side, axis=1, keepdims=True) + 1e-9
    fwd = np.cross(side, up)
    toe = g("LeftToeBase") - lfoot + g("RightToeBase") - rfoot; toe[:, 1] = 0
    if np.mean(np.sum(fwd * toe, axis=1)) < 0: fwd = -fwd
    right = side
    trunk = neck - hips
    pitch = np.degrees(np.arctan2(np.sum(trunk * fwd, axis=1), trunk @ up))      # + leans forward
    roll = np.degrees(np.arctan2(np.sum(trunk * right, axis=1), trunk @ up))     # + leans to the right
    knee_l = np.array([180 - ang(lul[i] - lleg[i], lfoot[i] - lleg[i]) for i in range(len(P))])
    knee_r = np.array([180 - ang(rul[i] - rleg[i], rfoot[i] - rleg[i]) for i in range(len(P))])
    hip_h = (hips[:, 1] - ground) / leg
    foot_l = (lfoot[:, 1] - ground) / leg
    foot_r = (rfoot[:, 1] - ground) / leg
    hand_low = np.minimum(lhand[:, 1], rhand[:, 1])
    hand_h = (hand_low - ground) / leg
    arm_out_l = np.array([ang(lhand[i] - lsh[i], -up) for i in range(len(P))])
    arm_out_r = np.array([ang(rhand[i] - rsh[i], -up) for i in range(len(P))])
    speed = np.linalg.norm(np.diff(hips[:, [0, 2]], axis=0), axis=1) / dt / leg
    res = {
        "clip": path.split("/")[-1], "seconds": round(len(data) * ft, 2), "legLengthUnits": round(leg, 3),
        "hipHeight": stats(hip_h), "trunkPitch": stats(pitch), "trunkRoll": stats(roll),
        "kneeL": stats(knee_l), "kneeR": stats(knee_r), "footLiftL": stats(foot_l), "footLiftR": stats(foot_r),
        "lowestHand": stats(hand_h), "armFromDownL": stats(arm_out_l), "armFromDownR": stats(arm_out_r),
        "speedLegsPerSec": stats(speed),
    }
    # Periodicity of the hips' bob (step or hop period).
    res["bobPeriodS"] = period(hip_h, dt)
    res["series"] = {"t": [round(i * dt, 3) for i in range(len(P))], "hip": r3(hip_h), "pitch": r3(pitch), "roll": r3(roll),
                     "hand": r3(hand_h), "kneeL": r3(knee_l), "kneeR": r3(knee_r), "footL": r3(foot_l), "footR": r3(foot_r),
                     "armL": r3(arm_out_l), "armR": r3(arm_out_r)}
    return res


def r3(a): return [round(float(x), 3) for x in a]


def stats(a):
    a = np.asarray(a)
    return {"min": round(float(a.min()), 3), "p10": round(float(np.percentile(a, 10)), 3), "median": round(float(np.median(a)), 3),
            "p90": round(float(np.percentile(a, 90)), 3), "max": round(float(a.max()), 3)}


def period(x, dt):
    x = np.asarray(x) - np.mean(x)
    if len(x) < 8: return None
    ac = np.correlate(x, x, "full")[len(x) - 1:]
    ac /= ac[0] + 1e-9
    lo = int(0.25 / dt)
    hi = min(len(ac) - 1, int(2.5 / dt))
    if hi <= lo: return None
    # The first peak after the first dip.
    k = None
    for j in range(2, hi):
        if ac[j] > ac[j - 1] and ac[j] >= ac[j + 1] and ac[j] > 0.1 and any(ac[1:j] < ac[j] - 0.1):
            k = j; break
    if k is None: return None
    return {"seconds": round(k * dt, 3), "strength": round(float(ac[k]), 3)}


if __name__ == "__main__":
    full = "--series" in sys.argv
    for p in [a for a in sys.argv[1:] if not a.startswith("--")]:
        s = summary(p)
        if not full: s.pop("series")
        print(json.dumps(s))
