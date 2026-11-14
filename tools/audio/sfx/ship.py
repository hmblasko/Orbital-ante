"""Spacecraft: engines, cockpit, seat and structure."""
from __future__ import annotations

import numpy as np

from oa_audio import dsp
from oa_audio.dsp import SR, secs

from .common import finish, finish_loop, metal_hit, sfx

SHIP = dict(bus="Engines", priority=50, vacuum="self")


def _rumble(n: int, rng, lo: float, hi: float) -> np.ndarray:
    """Stereo combustion roar: brown noise with a restless band-pass, different in each ear."""
    out = np.zeros((n, 2))
    for c in range(2):
        x = dsp.brown(n, rng)
        wander = 1 + 0.25 * dsp.smooth_noise(n, 3.0, rng)
        x = dsp.svf(x, (lo + hi) * 0.5 * wander, 0.6, "lp", 2)
        out[:, c] = dsp.highpass(x, lo * 0.5)
    return out / (np.max(np.abs(out)) + 1e-9)


@sfx("ship.engine.loop", "ship", loop=True, spatial=0.0, volumeDb=-6, **SHIP,
     note="Main engine burning, heard from the cockpit: felt through the seat more than heard. The game raises pitch and volume with thrust.")
def engine_loop(rng, v):
    n = secs(11.0)
    t = np.arange(n) / SR
    roar = _rumble(n, rng, 45, 420)
    crackle = dsp.stereo(dsp.bandpass(dsp.white(n, rng), 600, 2600)) * 0.06 * (1 + 0.5 * dsp.smooth_noise(n, 25, rng))[:, None]
    # turbopump: a faint whine with its harmonics, slightly unsteady
    drift = 1 + 0.004 * dsp.smooth_noise(n, 0.8, rng)
    pump = (dsp.sine(196 * drift, n) * 0.5 + dsp.sine(392 * drift, n) * 0.25 + dsp.saw(98 * drift, n) * 0.2) * 0.07
    hull = dsp.sine(41, n) * 0.25 * (1 + 0.2 * np.sin(dsp.TAU * 0.5 * t))
    x = roar * 0.6 + crackle * 3.0 + dsp.stereo(pump * 2.0 + hull * 0.3) + dsp.highpass(_rumble(n, rng, 300, 1800), 250) * 0.6
    x = dsp.saturate(x, 1.6)
    return finish_loop(x, -4, 1.0, hp=25)


@sfx("ship.engine.ignite", "ship", variants=2, spatial=0.0, volumeDb=-3, pitchVar=0.3, **SHIP)
def engine_ignite(rng, v):
    """Ignition: valves clunk, a cough of flame, then the roar arrives and settles."""
    n = secs(2.6)
    t = np.arange(n) / SR
    x = np.zeros((n, 2))
    dsp.place(x, metal_hit(rng, 0.2, [520, 930, 1700, 2600], hardness=0.8), 0, 0.5)
    dsp.place(x, metal_hit(rng, 0.2, [610, 1180, 2100], hardness=0.8), secs(0.16), 0.4)
    hiss = dsp.stereo(dsp.highpass(dsp.white(n, rng), 3000)) * (np.clip((t - 0.1) / 0.2, 0, 1) * np.exp(-np.maximum(t - 0.4, 0) / 0.1))[:, None] * 0.12
    boom_t = np.maximum(t - 0.42, 0)
    boom = dsp.sine(38 + 70 * np.exp(-boom_t / 0.08), n) * np.exp(-boom_t / 0.5) * (t > 0.42) * 0.9
    roar = _rumble(n, rng, 50, 900)
    swell = np.clip((t - 0.42) / 0.12, 0, 1) * (0.55 + 0.45 * np.exp(-np.maximum(t - 0.6, 0) / 0.6))
    cutoff = 300 + 5000 * np.exp(-np.maximum(t - 0.42, 0) / 0.7)
    roar = dsp.svf(roar, cutoff, 0.7, "lp") * swell[:, None]
    x += hiss + dsp.stereo(boom) * 0.5 + roar + dsp.highpass(roar, 300) * 1.5
    x = dsp.saturate(x, 2.0)
    return finish(dsp.fade(x, 0, 0.9), -2, 0.0, hp=25, limit=True)


@sfx("ship.rcs", "ship", variants=3, spatial=0.0, volumeDb=-13, pitchVar=1.0, volVarDb=1.5, maxVoices=4, **SHIP)
def rcs(rng, v):
    """Attitude thruster puff: a valve tick and a short burst of cold gas."""
    n = secs(0.32)
    t = np.arange(n) / SR
    gas = dsp.bandpass(dsp.white(n, rng), 900, 7500) * np.minimum(t / 0.004, 1) * np.exp(-np.maximum(t - 0.09, 0) / 0.04)
    gas *= 1 + 0.3 * dsp.smooth_noise(n, 90, rng)
    low = dsp.lowpass(dsp.white(n, rng), 300) * np.exp(-t / 0.05) * 0.8
    x = gas * 0.6 + low
    dsp.place(x, metal_hit(rng, 0.05, [2900, 4600, 7100], hardness=1.2), 0, 0.2)
    return finish(dsp.pan(x, rng.uniform(-0.5, 0.5)), -5, 0.03)


@sfx("ship.cabin.loop", "ship", loop=True, spatial=0.0, volumeDb=-22, bus="Engines", priority=200, vacuum="suit",
     note="Cabin room tone: air handling, a coolant pump and electronics. The sound of being safe indoors.")
def cabin_loop(rng, v):
    n = secs(12.0)
    t = np.arange(n) / SR
    air = np.stack([dsp.bandpass(dsp.pink(n, rng), 180, 2400), dsp.bandpass(dsp.pink(n, rng), 180, 2400)], axis=1) * 0.5
    fan = dsp.sine(118, n) * 0.12 + dsp.sine(236.3, n) * 0.05 + dsp.sine(354.5, n) * 0.025
    pump = dsp.sine(59, n) * 0.1 * (0.8 + 0.2 * np.sin(dsp.TAU * 0.25 * t))
    elec = dsp.sine(7810, n) * 0.004 + dsp.sine(3140, n) * 0.004
    x = air + dsp.stereo(fan + pump + elec)
    return finish_loop(x, -10, 1.0, hp=35)


@sfx("ship.switch", "ship", variants=3, spatial=0.0, volumeDb=-12, pitchVar=1.0, bus="Engines", priority=120,
     vacuum="suit")
def switch(rng, v):
    """Cockpit toggle: a chunky two-stage click, the kind that feels good to flip."""
    n = secs(0.16)
    x = np.zeros(n)
    dsp.place(x, metal_hit(rng, 0.05, [2100, 3300, 5200], hardness=1.0), 0, 0.5)
    dsp.place(x, metal_hit(rng, 0.1, [1400, 2500, 4100, 780], hardness=1.3), secs(0.022 + 0.004 * v), 1.0)
    t = np.arange(n) / SR
    x += dsp.sine(180, n) * np.exp(-np.maximum(t - 0.022, 0) / 0.012) * (t > 0.022) * 0.25
    return finish(x, -5, 0.02)


@sfx("ship.confirm", "ship", spatial=0.0, volumeDb=-14, bus="Engines", priority=100, vacuum="suit")
def confirm(rng, v):
    """Flight computer acknowledgement (autopilot or velocity match engaged): two soft tones, A then D."""
    n = secs(0.5)
    x = np.zeros(n)
    for f, at in [(880.0, 0.0), (1174.66, 0.11)]:
        m = n - secs(at)
        tt = np.arange(m) / SR
        tone = (dsp.sine(f, m) + 0.2 * dsp.sine(f * 3, m)) * np.minimum(tt / 0.004, 1) * np.exp(-tt / 0.09)
        dsp.place(x, tone, secs(at), 0.7)
    return finish(dsp.lowpass(x, 5000), -6, 0.05)


@sfx("ship.seatbelt", "ship", spatial=0.0, volumeDb=-9, pitchVar=0.4, bus="Engines", priority=80, vacuum="suit")
def seatbelt(rng, v):
    """Buckling in: webbing pulled, a hydraulic sigh as the harness tightens, and a firm latch."""
    n = secs(1.3)
    t = np.arange(n) / SR
    web = dsp.bandpass(dsp.white(n, rng), 1500, 6000) * np.exp(-((t - 0.15) / 0.09) ** 2) * 0.12
    web *= 0.5 + 0.5 * np.sin(dsp.TAU * 70 * t)
    hyd_env = np.clip((t - 0.3) / 0.08, 0, 1) * np.clip((0.95 - t) / 0.2, 0, 1)
    hyd = dsp.svf(dsp.white(n, rng), 1800 - 900 * np.clip((t - 0.3) / 0.6, 0, 1), 2.0, "bp") * hyd_env * 0.3
    servo = dsp.saw(140 + 60 * np.clip((t - 0.3) / 0.6, 0, 1), n) * hyd_env * 0.05
    x = web + hyd + dsp.lowpass(servo, 1200)
    dsp.place(x, metal_hit(rng, 0.18, [1250, 1900, 2850, 4100, 640], hardness=1.4), secs(0.97), 0.9)
    dsp.place(x, metal_hit(rng, 0.08, [3100, 4800, 6900], hardness=1.0), secs(0.985), 0.4)
    return finish(x, -4, 0.05)


@sfx("ship.touchdown", "ship", variants=2, spatial=0.0, volumeDb=-5, pitchVar=0.6, bus="Engines", priority=40,
     vacuum="self")
def touchdown(rng, v):
    """Landing legs meeting the ground: a weighty thud, struts compressing, the hull creaking as it settles."""
    n = secs(1.4)
    t = np.arange(n) / SR
    thud = dsp.sine(44 + 60 * np.exp(-t / 0.03), n) * np.exp(-t / 0.16) * 1.2
    crunch = dsp.lowpass(dsp.white(n, rng), 900) * np.exp(-t / 0.06) * 0.9
    strut = dsp.svf(dsp.white(n, rng), 700 + 500 * np.exp(-t / 0.2), 3.0, "bp") * np.exp(-((t - 0.16) / 0.1) ** 2) * 0.25
    x = dsp.saturate(thud * 0.4 + crunch + strut * 2.0 + dsp.bandpass(dsp.white(n, rng), 250, 1400) * np.exp(-t / 0.05) * 0.8, 1.8)
    for at in [0.42, 0.61 + 0.05 * v, 0.9]:
        creak_n = secs(0.18)
        tt = np.arange(creak_n) / SR
        fc = rng.uniform(260, 520)
        creak = dsp.saw(fc * (1 + 0.15 * np.sin(dsp.TAU * 9 * tt)), creak_n) * np.hanning(creak_n)
        creak = dsp.bandpass(creak, 300, 2400) * 0.08
        dsp.place(x, creak, secs(at), 1.0)
    return finish(x, -3, 0.2)


# ----------------------------------------------------------------------------- manoeuvring thrusters
#
# One loop per direction the ship can push itself. Each is placed where that thruster sits relative to the
# pilot's seat and has its own tone, so the direction can be told with eyes shut and on small speakers:
# side thrusters by which ear, nose thrusters by a close bright bark in front, belly thrusters by a low drum
# under the floor, roof thrusters by a thin hiss overhead. The main engine (ship.engine.loop) is "forward".

THRUSTER = dict(bus="Engines", priority=60, vacuum="self", spatial=0.0, loop=True)


def _jet(n: int, rng, lo: float, hi: float, tone: float, tone_q: float, flutter: float, tone_gain: float = 1.0) -> np.ndarray:
    """One thruster heard through the hull (mono): gas rushing through a nozzle, the nozzle's own note, and a
    restless flutter so it never sits still."""
    gas = dsp.bandpass(dsp.white(n, rng), lo, hi)
    gas *= 1 + 0.35 * dsp.smooth_noise(n, flutter, rng) + 0.15 * dsp.smooth_noise(n, flutter * 6, rng)
    wander = 1 + 0.03 * dsp.smooth_noise(n, 1.5, rng)
    note = dsp.svf(dsp.white(n, rng), tone * wander, tone_q, "bp", 2)
    note2 = dsp.svf(dsp.white(n, rng), tone * 2.31 * wander, tone_q * 0.7, "bp", 2)
    x = gas / (np.max(np.abs(gas)) + 1e-9) + tone_gain * (note / (np.max(np.abs(note)) + 1e-9) * 0.7 + note2 / (np.max(np.abs(note2)) + 1e-9) * 0.3)
    return x / (np.max(np.abs(x)) + 1e-9)


def _side(rng, sign: float) -> np.ndarray:
    n = secs(7.0)
    near = _jet(n, rng, 500, 5200, 470, 5.0, 11)
    # The far ear hears it late, duller and quieter, through the cabin: that is what makes it a place, not a pan pot.
    far = np.roll(dsp.lowpass(near, 1100), secs(0.0007)) * 0.3
    frame = dsp.lowpass(dsp.brown(n, rng), 180) * 0.5
    frame = frame / (np.max(np.abs(frame)) + 1e-9) * 0.35
    left, right = (near + frame, far + frame) if sign < 0 else (far + frame, near + frame)
    return finish_loop(dsp.saturate(np.stack([left, right], axis=1), 1.3), -5, 1.0, hp=60)


@sfx("ship.thruster.left.loop", "ship", volumeDb=-4, **THRUSTER,
     note="Thrusters on the LEFT side of the hull firing (the ship is being pushed to the right). Heard in the left ear.")
def thruster_left(rng, v):
    return _side(rng, -1)


@sfx("ship.thruster.right.loop", "ship", volumeDb=-4, **THRUSTER,
     note="Thrusters on the RIGHT side of the hull firing (the ship is being pushed to the left). Heard in the right ear.")
def thruster_right(rng, v):
    return _side(rng, +1)


@sfx("ship.thruster.nose.loop", "ship", volumeDb=-5, **THRUSTER,
     note="Nose thrusters firing forwards to brake or back away: close, bright and dead ahead.")
def thruster_nose(rng, v):
    n = secs(7.0)
    jet = _jet(n, rng, 900, 7000, 760, 7.0, 16, tone_gain=1.3)
    # two nozzles either side of the windscreen, nearly together
    x = np.stack([jet, np.roll(jet, secs(0.00025)) * 0.92 + dsp.bandpass(dsp.white(n, rng), 2000, 6000) * 0.05], axis=1)
    return finish_loop(dsp.saturate(x, 1.5), -5, 1.0, hp=180)


@sfx("ship.thruster.belly.loop", "ship", volumeDb=-4, **THRUSTER,
     note="Belly thrusters firing downwards to lift the ship: a low drumming under the floor, felt in the seat.")
def thruster_belly(rng, v):
    n = secs(7.0)
    t = np.arange(n) / SR
    drum = _rumble(n, rng, 70, 520)
    # floor plates buzzing: enough around 300-700 Hz that it survives small speakers
    plate = np.stack([_jet(n, rng, 240, 900, 330, 6.0, 7), _jet(n, rng, 240, 900, 352, 6.0, 7)], axis=1)
    throb = (1 + 0.18 * np.sin(dsp.TAU * 6.5 * t + 0.6 * dsp.smooth_noise(n, 2, rng)))[:, None]
    return finish_loop(dsp.saturate((drum * 0.8 + plate * 0.7) * throb, 1.6), -5, 1.0, hp=40)


@sfx("ship.thruster.roof.loop", "ship", volumeDb=-5, **THRUSTER,
     note="Roof thrusters firing upwards to push the ship down: a thin, airy hiss overhead with a metallic edge.")
def thruster_roof(rng, v):
    n = secs(7.0)
    out = np.zeros((n, 2))
    for c in range(2):
        jet = _jet(n, rng, 1800, 9000, 2350, 9.0, 21, tone_gain=0.8)
        # the roof panel a hand's width above the helmet answers straight back
        comb = jet + np.roll(jet, secs(0.0011 + 0.0002 * c)) * 0.55
        out[:, c] = comb
    return finish_loop(out, -6, 1.0, hp=700)


@sfx("ship.thruster.turn.loop", "ship", volumeDb=-6, **THRUSTER,
     note="Attitude jets chattering while the ship is being turned: quick cold-gas puffs, alternating sides.")
def thruster_turn(rng, v):
    n = secs(6.0)
    out = np.zeros((n, 2))
    at, side = 0.0, 0
    while at < 6.0 - 0.2:
        m = secs(rng.uniform(0.05, 0.09))
        t = np.arange(m) / SR
        puff = dsp.bandpass(dsp.white(m, rng), 1200, 8000) * np.minimum(t / 0.003, 1) * np.exp(-np.maximum(t - 0.02, 0) / 0.018)
        tick = metal_hit(rng, 0.03, [3100, 4900, 7300], hardness=1.1) * 0.25
        dsp.place(out, dsp.pan(puff, -0.55 if side == 0 else 0.55), secs(at), rng.uniform(0.7, 1.0))
        dsp.place(out, dsp.pan(tick, -0.55 if side == 0 else 0.55), secs(at), 1.0)
        side ^= 1
        at += rng.uniform(0.075, 0.13)
    # the first and last 0.2 s are left empty, so the loop wraps on silence
    return finish(out, -7, 0.01, hp=300)


@sfx("ship.thruster.start", "ship", variants=3, spatial=0.0, volumeDb=-8, pitchVar=1.2, volVarDb=1.5, maxVoices=4,
     cooldownMs=60, bus="Engines", priority=70, vacuum="self")
def thruster_start(rng, v):
    """A thruster opening: the valve's clack and the first cough of gas. The game pans it to the side that fired."""
    n = secs(0.22)
    t = np.arange(n) / SR
    x = dsp.bandpass(dsp.white(n, rng), 700, 6500) * np.minimum(t / 0.002, 1) * np.exp(-t / 0.035) * 0.7
    x += dsp.lowpass(dsp.white(n, rng), 320) * np.exp(-t / 0.03) * 0.6
    dsp.place(x, metal_hit(rng, 0.06, [1900, 3300, 5200], hardness=1.2), 0, 0.5)
    return finish(x, -5, 0.03)


@sfx("ship.thruster.stop", "ship", variants=3, spatial=0.0, volumeDb=-12, pitchVar=1.2, volVarDb=1.5, maxVoices=4,
     cooldownMs=60, bus="Engines", priority=90, vacuum="self")
def thruster_stop(rng, v):
    """A thruster closing: the valve seats and the last gas sighs out of the line."""
    n = secs(0.3)
    t = np.arange(n) / SR
    x = dsp.bandpass(dsp.white(n, rng), 1500, 7000) * np.exp(-t / 0.07) * 0.5 * (1 - np.exp(-t / 0.004))
    dsp.place(x, metal_hit(rng, 0.05, [2400, 4100, 6600], hardness=0.9), 0, 0.45)
    return finish(x, -6, 0.05)
