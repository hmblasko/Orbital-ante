"""Places: wind, caves, machinery, radio. All stereo loops, built to sit under the music or carry a scene alone."""
from __future__ import annotations

import numpy as np

from oa_audio import dsp
from oa_audio.dsp import SR, secs

from .common import finish, finish_loop, loopable, ping, sfx

AMB = dict(bus="Environment", spatial=0.0, loop=True, priority=220)


@sfx("amb.wind.thin", "ambience", volumeDb=-14, vacuum="air", **AMB,
     note="Thin-air wind (Mars): a high, dry hiss with slow gusts and sand ticking against the visor.")
def wind_thin(rng, v):
    n = secs(22.0)
    out = np.zeros((n, 2))
    for c in range(2):
        gust = 0.55 + 0.45 * dsp.smooth_noise(n, 0.25, rng)
        w = dsp.svf(dsp.pink(n, rng), 900 + 1400 * gust, 1.4, "bp")
        whistle = dsp.svf(dsp.white(n, rng), 2400 + 900 * dsp.smooth_noise(n, 0.4, rng), 9.0, "bp") * 0.06 * gust ** 2
        out[:, c] = (w * gust + whistle)
    sand = (rng.uniform(0, 1, n) > 0.9993).astype(float) * rng.uniform(0.2, 1, n)
    sand = dsp.bandpass(sand, 3000, 9000) * 0.5
    out += dsp.pan(sand, 0.2)
    return finish_loop(out, -8, 2.0, hp=120)


@sfx("amb.wind.thick", "ambience", volumeDb=-12, vacuum="air", **AMB,
     note="Thick-atmosphere wind: a low, heavy roar that leans on the suit, with a moan in it.")
def wind_thick(rng, v):
    n = secs(22.0)
    out = np.zeros((n, 2))
    for c in range(2):
        gust = 0.5 + 0.5 * dsp.smooth_noise(n, 0.18, rng)
        w = dsp.svf(dsp.brown(n, rng), 260 + 500 * gust, 0.9, "lp", 2)
        moan = dsp.svf(dsp.pink(n, rng), 310 + 140 * dsp.smooth_noise(n, 0.15, rng), 14.0, "bp") * 0.5 * gust
        out[:, c] = w * (0.5 + 0.5 * gust) + moan
    return finish_loop(out, -7, 2.0, hp=40)


@sfx("amb.cave", "ambience", volumeDb=-14, vacuum="air", **AMB,
     note="Cave: a deep held breath of air, with a drip now and then answering from far inside.")
def cave(rng, v):
    n = secs(24.0)
    t = np.arange(n) / SR
    air = np.stack([dsp.svf(dsp.brown(n, rng), 160, 0.8, "lp", 2), dsp.svf(dsp.brown(n, rng), 160, 0.8, "lp", 2)], axis=1)
    tone = dsp.stereo(dsp.svf(dsp.pink(n, rng), 97, 30.0, "bp") + dsp.svf(dsp.pink(n, rng), 146, 30.0, "bp") * 0.6) * 0.5
    x = air + tone * (0.7 + 0.3 * np.sin(dsp.TAU * 0.07 * t))[:, None]
    drips = np.zeros((n, 2))
    for at in [2.1, 5.6, 6.0, 9.8, 13.2, 17.4, 17.9]:
        m = secs(0.08)
        tt = np.arange(m) / SR
        f = rng.uniform(1300, 2400)
        drip = np.sin(dsp.TAU * f * (1 + 2.5 * tt) * tt) * np.exp(-tt / 0.012)
        dsp.place(drips, dsp.pan(drip, rng.uniform(-0.8, 0.8)), secs(at), rng.uniform(0.02, 0.05))
    drips = dsp.reverb(dsp.with_tail(drips, 0)[:n], 3.5, 0.9, 0.6, 0.04, 1.6, seed=21)
    return finish_loop(x / (np.max(np.abs(x)) + 1e-9) * 0.5 + drips, -9, 2.0, hp=30)


@sfx("amb.facility", "ambience", volumeDb=-15, vacuum="air", **AMB,
     note="Abandoned facility: mains hum, a failing ventilation fan, a relay that still clicks to nobody.")
def facility(rng, v):
    n = secs(18.0)
    t = np.arange(n) / SR
    hum = dsp.sine(50, n) * 0.3 + dsp.sine(100, n) * 0.22 + dsp.sine(150, n) * 0.1 + dsp.sine(250.2, n) * 0.04
    wobble = 1 + 0.02 * np.sin(dsp.TAU * 0.6 * t) + 0.01 * dsp.smooth_noise(n, 2, rng)
    fan = dsp.saw(86 * wobble, n) * 0.06
    fan = dsp.lowpass(fan, 900) * (0.8 + 0.2 * np.sin(dsp.TAU * 86 / 7 * t))
    air = np.stack([dsp.bandpass(dsp.pink(n, rng), 250, 3200), dsp.bandpass(dsp.pink(n, rng), 250, 3200)], axis=1) * 0.35
    x = air + dsp.stereo(hum * 0.5 + fan)
    for at in [3.3, 3.42, 8.9]:
        m = secs(0.05)
        relay = ping(rng, 0.05, [1700, 3100, 5200], [0.008, 0.005, 0.003], [1, 0.6, 0.3])
        dsp.place(x, dsp.pan(relay, -0.4), secs(at), 0.05)
    buzz = dsp.square(100, n, 0.1) * (dsp.smooth_noise(n, 3, rng) > 0.75) * 0.015
    x += dsp.pan(dsp.bandpass(buzz, 1500, 6000), 0.5)
    return finish_loop(x, -10, 1.5, hp=35)


@sfx("amb.radio.static", "ambience", volumeDb=-24, vacuum="suit", bus="Dialogue", spatial=0.0, loop=True, priority=200,
     note="Radio static in the headset: hiss, crackle, and now and then the ghost of a carrier tone sweeping past.")
def radio_static(rng, v):
    n = secs(14.0)
    t = np.arange(n) / SR
    hiss = dsp.bandpass(dsp.white(n, rng), 500, 3400) * (0.7 + 0.3 * dsp.smooth_noise(n, 1.5, rng))
    crackle = (rng.uniform(0, 1, n) > 0.9992).astype(float) * rng.uniform(-1, 1, n)
    crackle = dsp.bandpass(crackle, 800, 3000) * 4
    f = 700 + 900 * (0.5 + 0.5 * dsp.smooth_noise(n, 0.12, rng))
    presence = np.clip(dsp.smooth_noise(n, 0.2, rng) - 0.3, 0, 1) * 0.25
    carrier = dsp.sine(f, n) * presence
    x = dsp.bandpass(hiss * 0.5 + crackle + carrier, 400, 3600, 3)
    x = dsp.saturate(x, 1.5)
    return dsp.stereo(finish_loop(x, -10, 1.0))


@sfx("amb.space.suit", "ambience", volumeDb=-17, vacuum="suit", **AMB,
     note="What vacuum sounds like from inside a suit: almost nothing. Life-support fan, coolant ticking, your own blood. This is the bed for the game's deliberate silences.")
def space_suit(rng, v):
    n = secs(18.0)
    t = np.arange(n) / SR
    fan = dsp.sine(143, n) * 0.1 + dsp.sine(286.4, n) * 0.04 + dsp.bandpass(dsp.pink(n, rng), 400, 1800) * 0.25
    blood = dsp.lowpass(dsp.brown(n, rng), 70) * 0.5 * (0.6 + 0.4 * np.sin(dsp.TAU * 1.05 * t) ** 8)
    x = dsp.stereo(fan + blood)
    for at in [1.7, 4.4, 4.9, 9.3]:
        m = secs(0.03)
        tick = ping(rng, 0.03, [rng.uniform(2500, 5000)], [0.004], [1.0], noise=0.1)
        dsp.place(x, dsp.pan(tick, rng.uniform(-0.6, 0.6)), secs(at), 0.03)
    return finish_loop(x, -12, 1.5, hp=25)
