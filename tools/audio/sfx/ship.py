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


@sfx("ship.engine.loop", "ship", loop=True, spatial=0.0, volumeDb=-9, **SHIP,
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
