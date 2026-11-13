"""Weapon sounds. Each gun has its own recipe so no two are interchangeable; variants change the noise, the
echo pattern and the mechanical timing rather than just the pitch."""
from __future__ import annotations

import numpy as np

from oa_audio import dsp
from oa_audio.dsp import SR, secs

from .common import finish, finish_loop, gunshot, metal_hit, norm, outdoor_tail, sfx

GUN = dict(bus="Weapons", spatial=0.65, minDist=5, maxDist=140, priority=20, vacuum="self")

BOLT = [1850, 2720, 4300, 6150, 880]
SLIDE = [2400, 3650, 5200, 7400, 1250]
LIGHT = [3100, 4800, 6900, 9200]
HEAVY = [1250, 1900, 2850, 4100, 640]


@sfx("weapon.rifle.shot", "weapons", variants=4, pitchVar=0.35, volVarDb=1.0, volumeDb=-1, maxVoices=5, **GUN)
def rifle(rng, v):
    """AK-style rifle: sharp crack, a barking mid, and the clack of a heavy bolt carrier."""
    return gunshot(rng, length=1.1, crack=1.0, crack_tau=0.009, crack_hp=1700, blast_ms=2.0,
                   body=0.8, body_tau=0.05, body_band=(220, 2600), thump=0.38, thump_f=(175, 68), thump_tau=0.04,
                   bark=0.6, bark_f=(520, 880, 1420), bark_tau=0.03,
                   mech=[(38, 0.11, BOLT, 0.09), (71, 0.15, BOLT, 0.11)], drive=2.4,
                   tail=0.2, tail_len=1.0, tail_dark=2600, tail_spread=0.3)


@sfx("weapon.smg.shot", "weapons", variants=4, pitchVar=0.4, volVarDb=1.0, volumeDb=-3, maxVoices=6, **GUN)
def smg(rng, v):
    """Uzi-style SMG: short, dry and snappy, with a rattling open bolt so the cadence reads at speed."""
    return gunshot(rng, length=0.5, crack=1.0, crack_tau=0.006, crack_hp=2600, blast_ms=1.3,
                   body=0.5, body_tau=0.022, body_band=(400, 3600), thump=0.26, thump_f=(270, 130), thump_tau=0.022,
                   bark=0.7, bark_f=(980, 1650, 2500), bark_tau=0.014, bark_q=6,
                   mech=[(22, 0.2, LIGHT, 0.05), (44, 0.17, SLIDE, 0.05)], drive=2.0,
                   tail=0.07, tail_len=0.4, tail_dark=3800, tail_spread=0.1, tail_echoes=4, presence_db=3.0)


@sfx("weapon.handcannon.shot", "weapons", variants=3, pitchVar=0.3, volVarDb=0.8, volumeDb=-2, maxVoices=3, **GUN)
def handcannon(rng, v):
    """Desert Eagle-style handgun: a huge, slow boom with a hard slap-back and a late, heavy slide."""
    return gunshot(rng, length=1.7, crack=0.9, crack_tau=0.013, crack_hp=1300, blast_ms=3.6, blast=1.0,
                   body=0.95, body_tau=0.075, body_band=(130, 1700), thump=0.6, thump_f=(140, 46), thump_tau=0.085,
                   bark=0.8, bark_f=(330, 445, 690), bark_tau=0.07, bark_q=11,
                   mech=[(95, 0.13, HEAVY, 0.14), (150, 0.09, SLIDE, 0.1)], drive=3.0,
                   tail=0.2, tail_len=1.6, tail_dark=1900, tail_spread=0.45, slap=0.6, slap_time=0.13, low_db=2.0)


@sfx("weapon.sniper.shot", "weapons", variants=2, pitchVar=0.2, volVarDb=0.5, volumeDb=-2, maxVoices=2, **GUN)
def sniper(rng, v):
    """Sniper rifle: a hard, bright whip-crack with a lean report under it, then the valley answering back for
    two seconds. Where the hand cannon is all chest, this is all edge and distance."""
    return gunshot(rng, length=2.6, crack=1.5, crack_tau=0.005, crack_hp=3200, blast_ms=1.8, blast=1.0,
                   body=0.55, body_tau=0.05, body_band=(320, 3400), thump=0.3, thump_f=(200, 72), thump_tau=0.045,
                   bark=0.5, bark_f=(540, 1020, 1950), bark_tau=0.03,
                   mech=[], drive=2.8,
                   tail=0.26, tail_len=2.5, tail_dark=2700, tail_spread=0.85, tail_echoes=11, slap=0.7, slap_time=0.21,
                   presence_db=3.5, low_db=0.0)


@sfx("weapon.sniper.bolt", "weapons", variants=2, bus="Weapons", spatial=0.6, minDist=3, maxDist=40, priority=60,
     pitchVar=0.4, volumeDb=-8, vacuum="self")
def sniper_bolt(rng, v):
    """Working the sniper's bolt: up, back, forward, down."""
    n = secs(0.75)
    x = np.zeros(n)
    for at, g, fs in [(0.0, 0.5, LIGHT), (0.11, 0.9, BOLT), (0.36, 1.0, HEAVY), (0.47, 0.6, LIGHT)]:
        dsp.place(x, metal_hit(rng, 0.16, fs, hardness=1.3), secs(at * rng.uniform(0.95, 1.05)), g)
    slide = dsp.bandpass(dsp.white(n, rng), 1800, 6000) * 0.05
    env = np.zeros(n)
    env[secs(0.12):secs(0.34)] = np.hanning(secs(0.34) - secs(0.12))
    x += slide * env
    return finish(x, -4)


@sfx("weapon.suppressed.shot", "weapons", variants=3, pitchVar=0.5, volVarDb=1.0, volumeDb=-9, maxVoices=4,
     bus="Weapons", spatial=0.7, minDist=2, maxDist=35, priority=30, vacuum="self")
def suppressed(rng, v):
    """Suppressed PPK-style pistol: a soft cough of gas; the slide and the spring are the loudest things."""
    n = secs(0.5)
    t = np.arange(n) / SR
    gas = dsp.bandpass(dsp.white(n, rng), 500, 3200)
    gas *= np.minimum(t / 0.002, 1) * (np.exp(-t / 0.022) + 0.2 * np.exp(-t / 0.07))
    thump = dsp.sine(160 * (1 + 0.6 * np.exp(-t / 0.02)), n) * np.exp(-t / 0.025) * 0.2
    hiss = dsp.highpass(dsp.white(n, rng), 4500) * np.exp(-t / 0.05) * 0.12
    x = dsp.saturate(gas * 0.9 + thump + hiss, 1.5)
    dsp.place(x, metal_hit(rng, 0.09, SLIDE, hardness=1.4), secs(0.028 * rng.uniform(0.9, 1.1)), 0.42)
    dsp.place(x, metal_hit(rng, 0.1, SLIDE, hardness=1.0), secs(0.062 * rng.uniform(0.9, 1.1)), 0.5)
    spring = dsp.sine(2900 * (1 + 0.04 * np.sin(dsp.TAU * 90 * t)), n) * np.exp(-t / 0.05) * 0.035
    dsp.place(x, spring, secs(0.065), 1.0)
    x += norm(outdoor_tail(x, rng, 0.5, 3, 0.02, 0.08, 3000, 0.4)) * 0.04
    return finish(x, -3)


@sfx("weapon.laser_revolver.shot", "weapons", variants=3, pitchVar=0.6, volVarDb=0.8, volumeDb=-2, maxVoices=4, **GUN)
def laser_revolver(rng, v):
    """Laser cowboy revolver: hammer click, a hard zap that dives, and a ricochet-like twang trailing off."""
    n = secs(1.0)
    t = np.arange(n) / SR
    f = 240 + 4200 * np.exp(-t / 0.028) + 600 * np.exp(-t / 0.12)
    zap = dsp.fm(f, n, 2.01, 3.0 * np.exp(-t / 0.05)) * np.exp(-t / 0.055)
    zap += dsp.saw(f * 0.5, n) * np.exp(-t / 0.04) * 0.5
    air = dsp.svf(dsp.white(n, rng), f * 1.5, 3.0, "bp") * np.exp(-t / 0.05) * 0.8   # the beam tearing the air
    snap = dsp.highpass(dsp.white(n, rng), 3000) * np.exp(-t / 0.005) * 1.3
    thump = dsp.sine(110 * (1 + np.exp(-t / 0.02)), n) * np.exp(-t / 0.045) * 0.6
    # twang: a noise burst through a string-like comb whose pitch bends up, the western nod
    twang_src = dsp.bandpass(dsp.white(n, rng), 700, 6000) * np.exp(-t / 0.012)
    twang = dsp.comb(twang_src, 660 * rng.uniform(0.94, 1.06), 0.975, 0.35)
    twang = dsp.pitch_shift_resample(twang, 0)[:n] * np.exp(-t / 0.16)
    x = dsp.saturate(norm(zap + air) * 0.9 + snap + thump, 2.2) + norm(twang) * 0.16
    x += norm(outdoor_tail(x, rng, 1.0, 5, 0.03, 0.22, 3500, 0.5)) * 0.1
    out = np.zeros(n + secs(0.03))
    dsp.place(out, metal_hit(rng, 0.05, LIGHT, hardness=1.5), 0, 0.12)  # hammer falls just before the bolt of light
    dsp.place(out, x, secs(0.022), 1.0)
    return finish(out, -1, 0.15, limit=True)


@sfx("weapon.raygun.shot", "weapons", variants=3, pitchVar=0.7, volVarDb=0.8, volumeDb=-6, maxVoices=4, **GUN)
def raygun(rng, v):
    """Ray gun: a cartoon 'pyoo' that wobbles on the way down, with a solid thump so it still hits hard."""
    n = secs(0.9)
    t = np.arange(n) / SR
    # each take has its own wobble speed, sweep and bubbles, so three shots in a row are three different pyoos
    rate, fall, top = rng.uniform(26, 42), rng.uniform(0.07, 0.115), rng.uniform(1600, 2300)
    wob = 1 + 0.18 * np.sin(dsp.TAU * (rate - 0.5 * rate * t / 0.9) * t + rng.uniform(0, dsp.TAU)) * np.exp(-t / 0.3)
    f = (rng.uniform(380, 470) + top * np.exp(-t / fall)) * wob
    tone = dsp.square(f, n, 0.5 - 0.3 * np.exp(-t / 0.1)) * 0.6 + dsp.sine(f * 2, n) * 0.4
    tone = dsp.svf(tone, 1200 + 5000 * np.exp(-t / 0.12), 2.0, "lp") * np.exp(-t / 0.1)
    ring = tone * (0.6 + 0.4 * np.sin(dsp.TAU * 61 * t))
    thump = dsp.sine(95 * (1 + 1.5 * np.exp(-t / 0.015)), n) * np.exp(-t / 0.05) * 0.7
    snap = dsp.highpass(dsp.white(n, rng), 2500) * np.exp(-t / 0.005) * 0.8
    # bubbles: three little rising blips after the shot
    x = dsp.saturate(ring + thump + snap, 2.0)
    for k in range(3):
        m = secs(0.05)
        tt = np.arange(m) / SR
        blip = np.sin(dsp.TAU * (900 + 500 * k) * rng.uniform(0.8, 1.25) * (1 + 3 * tt) * tt) * np.hanning(m) * 0.08
        dsp.place(x, blip, secs(0.18 + 0.07 * k + rng.uniform(0, 0.05)), 1.0)
    x += norm(outdoor_tail(x, rng, 0.9, 4, 0.03, 0.2, 3200, 0.5)) * 0.08
    return finish(x, -1, 0.12, limit=True)


@sfx("weapon.laser_shotgun.shot", "weapons", variants=2, pitchVar=0.3, volVarDb=0.6, volumeDb=-2, maxVoices=3, **GUN)
def laser_shotgun(rng, v):
    """Double-barrel laser shotgun: two discharges a hair apart, a wall of sawtooth and a floor-shaking drop."""
    n = secs(1.7)
    t = np.arange(n) / SR
    out = np.zeros(n)
    for k, off in enumerate([0.0, 0.028 * rng.uniform(0.8, 1.2)]):
        m = n - secs(off)
        tt = np.arange(m) / SR
        f = 90 + 1500 * np.exp(-tt / 0.05) * (1.0 + 0.12 * k)
        beam = dsp.saw(f, m, rng.uniform()) + dsp.saw(f * 1.506, m, rng.uniform()) * 0.7 + dsp.saw(f * 2.01, m) * 0.4
        beam = dsp.svf(beam, 700 + 7000 * np.exp(-tt / 0.07), 1.2, "lp") * np.exp(-tt / 0.13)
        blast = dsp.bandpass(dsp.white(m, rng), 200, 5000) * np.exp(-tt / 0.03) * 1.4
        dsp.place(out, beam * 0.8 + blast, secs(off), 1.0)
    drop = dsp.sine(40 + 110 * np.exp(-t / 0.06), n) * np.exp(-t / 0.14) * 0.7
    crackle = dsp.highpass(dsp.white(n, rng), 3500) * (rng.uniform(0, 1, n) > 0.985) * np.exp(-t / 0.35) * 0.9
    x = dsp.saturate(norm(out) * 0.95, 3.0) + drop + dsp.lowpass(crackle, 9000) * 0.5
    x += norm(outdoor_tail(x, rng, 1.7, 8, 0.03, 0.45, 2000, 0.7, 0.5, 0.14)) * 0.2
    return finish(x, -1, 0.3, limit=True)


@sfx("weapon.gravity.grab", "weapons", variants=2, bus="Weapons", spatial=0.6, minDist=3, maxDist=40, priority=40,
     pitchVar=0.5, volumeDb=-4, vacuum="self")
def gravity_grab(rng, v):
    """Gravity gun catching something: coils spin up, the field snaps shut with a thunk."""
    n = secs(0.7)
    t = np.arange(n) / SR
    k = t / 0.7
    f = rng.uniform(62, 80) * 2 ** (k * rng.uniform(2.3, 2.9))
    hum = dsp.saw(f, n) * 0.4 + dsp.sine(f * 2.5, n) * 0.3 + dsp.saw(f * 6.02, n) * 0.22
    hum *= (0.6 + 0.4 * np.sin(dsp.TAU * (rng.uniform(14, 24) + rng.uniform(45, 75) * k) * t))
    hum = dsp.svf(hum, 600 + 3500 * k ** 2, 2.0, "bp") * np.minimum(k / 0.1, 1) * (1 - k) ** 0.3
    x = np.zeros(n + secs(0.3))
    dsp.place(x, hum, 0, 0.8)
    tt = np.arange(secs(0.3)) / SR
    thunk = dsp.sine(85 * (1 + np.exp(-tt / 0.012)), len(tt)) * np.exp(-tt / 0.06)
    dsp.place(x, thunk, secs(0.6), 1.0)
    dsp.place(x, metal_hit(rng, 0.2, HEAVY, hardness=0.8), secs(0.6), 0.25)
    return finish(dsp.saturate(x, 1.6), -3, 0.05)


@sfx("weapon.gravity.punt", "weapons", variants=2, bus="Weapons", spatial=0.6, minDist=4, maxDist=80, priority=25,
     pitchVar=0.4, volumeDb=-4, vacuum="self")
def gravity_punt(rng, v):
    """Gravity gun launching what it holds: an electromagnetic whoomp that sucks in, then kicks out."""
    n = secs(1.0)
    t = np.arange(n) / SR
    kick = dsp.sine(50 + 150 * np.exp(-t / 0.03), n) * np.exp(-t / 0.09) * 0.8
    whoosh = dsp.svf(dsp.white(n, rng), 300 + 4200 * np.exp(-t / 0.1), 3.0, "bp") * np.exp(-t / 0.14)
    zing_f = 1800 * np.exp(-t / 0.25) + 240
    zing = dsp.fm(zing_f, n, 1.41, 3 * np.exp(-t / 0.15)) * np.exp(-t / 0.2) * 0.35
    zing *= 0.6 + 0.4 * np.sin(dsp.TAU * 47 * t)
    x = dsp.saturate(kick + whoosh * 0.9 + zing, 2.4)
    out = np.zeros(n + secs(0.09))
    m = secs(0.09)
    suck = dsp.bandpass(dsp.white(m, rng), 500, 4000) * np.linspace(0, 1, m) ** 3 * 0.5
    dsp.place(out, suck, 0, 1.0)
    dsp.place(out, x, m, 1.0)
    out += norm(dsp.pad_to(outdoor_tail(out, rng, len(out) / SR, 5, 0.03, 0.25, 2200, 0.6), len(out))) * 0.1
    return finish(out, -1, 0.2, limit=True)


@sfx("weapon.gravity.hold", "weapons", bus="Weapons", spatial=0.5, minDist=2, maxDist=25, priority=90, loop=True,
     volumeDb=-14, vacuum="self")
def gravity_hold(rng, v):
    """Gravity gun holding an object: a steady magnetic hum with a slow throb."""
    n = secs(3.25 + 1.0)
    t = np.arange(n) / SR
    throb = 0.7 + 0.3 * np.sin(dsp.TAU * 2.0 * t)
    x = dsp.saw(92, n) * 0.4 + dsp.sine(184.6, n) * 0.4 + dsp.sine(277.5, n) * 0.2 * np.sin(dsp.TAU * 0.3333 * t)
    x = dsp.svf(x, 600 + 300 * np.sin(dsp.TAU * 2.0 * t), 2.0, "lp") * throb
    x += dsp.bandpass(dsp.white(n, rng), 2500, 6000) * 0.02 * throb
    return finish_loop(dsp.pad_to(x, n), -6, 0.5)


# ----------------------------------------------------------------------------- handling

HANDLING = dict(bus="Weapons", spatial=0.6, minDist=2, maxDist=25, priority=70, vacuum="self")


@sfx("weapon.mag_out", "weapons", variants=2, pitchVar=0.6, volumeDb=-9, **HANDLING)
def mag_out(rng, v):
    """Magazine release: catch click, then the magazine sliding free."""
    n = secs(0.35)
    x = np.zeros(n)
    dsp.place(x, metal_hit(rng, 0.08, LIGHT, hardness=1.4), 0, 0.8)
    t = np.arange(n) / SR
    slide = dsp.bandpass(dsp.white(n, rng), 900, 4500) * np.exp(-((t - 0.11) / 0.05) ** 2) * 0.12
    dsp.place(x, slide, 0, 1.0)
    dsp.place(x, metal_hit(rng, 0.12, [640, 1150, 1900, 3100], hardness=0.6), secs(0.17), 0.5)
    return finish(x, -5)


@sfx("weapon.mag_in", "weapons", variants=2, pitchVar=0.6, volumeDb=-7, **HANDLING)
def mag_in(rng, v):
    """Magazine seated: slide in, a solid latch, a pat on the base."""
    n = secs(0.4)
    x = np.zeros(n)
    t = np.arange(n) / SR
    slide = dsp.bandpass(dsp.white(n, rng), 1200, 5000) * np.exp(-((t - 0.05) / 0.035) ** 2) * 0.1
    dsp.place(x, slide, 0, 1.0)
    dsp.place(x, metal_hit(rng, 0.14, HEAVY, hardness=1.2), secs(0.1), 1.0)
    dsp.place(x, metal_hit(rng, 0.07, LIGHT, hardness=1.0), secs(0.115), 0.4)
    thud = dsp.sine(140, secs(0.08)) * np.exp(-np.arange(secs(0.08)) / SR / 0.02)
    dsp.place(x, thud, secs(0.1), 0.5)
    return finish(x, -4)


@sfx("weapon.bolt_rack", "weapons", variants=2, pitchVar=0.5, volumeDb=-6, **HANDLING)
def bolt_rack(rng, v):
    """Charging handle: pulled back against the spring, then slamming home."""
    n = secs(0.5)
    x = np.zeros(n)
    t = np.arange(n) / SR
    dsp.place(x, metal_hit(rng, 0.1, BOLT, hardness=1.2), 0, 0.7)
    scrape = dsp.bandpass(dsp.white(n, rng), 1500, 7000) * np.exp(-((t - 0.09) / 0.05) ** 2) * 0.14
    scrape *= 0.6 + 0.4 * np.sin(dsp.TAU * 180 * t)
    dsp.place(x, scrape, 0, 1.0)
    dsp.place(x, metal_hit(rng, 0.16, HEAVY, hardness=1.5), secs(0.2), 1.0)
    dsp.place(x, metal_hit(rng, 0.1, BOLT, hardness=1.2), secs(0.208), 0.6)
    return finish(x, -3)


@sfx("weapon.dry_fire", "weapons", variants=2, pitchVar=0.5, volumeDb=-10, **HANDLING)
def dry_fire(rng, v):
    """Empty chamber: a small, disappointing click."""
    n = secs(0.12)
    x = metal_hit(rng, 0.12, [3400, 5200, 7900, 1900], hardness=1.6)
    dsp.place(x, metal_hit(rng, 0.06, LIGHT), secs(0.012), 0.4)
    return finish(dsp.pad_to(x, n), -6)
