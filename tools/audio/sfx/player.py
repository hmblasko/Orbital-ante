"""The player's body and suit."""
from __future__ import annotations

import numpy as np

from oa_audio import dsp
from oa_audio.dsp import SR, secs

from .common import finish, finish_loop, loopable, ping, sfx

SUIT = dict(bus="PlayerEquipment", spatial=0.0, vacuum="suit")


@sfx("player.step.regolith", "player", variants=5, pitchVar=1.2, volVarDb=2.0, volumeDb=-16, priority=180,
     maxVoices=2, bus="PlayerEquipment", spatial=0.0, vacuum="self")
def step_regolith(rng, v):
    """Boot on dusty ground: a soft crunch of grit with a padded heel."""
    n = secs(0.3)
    t = np.arange(n) / SR
    heel = dsp.lowpass(dsp.white(n, rng), 240) * np.exp(-t / 0.025) * 1.4
    grains = (rng.uniform(0, 1, n) > 0.8).astype(float) * rng.uniform(0.3, 1, n)
    crunch = dsp.bandpass(dsp.white(n, rng) * dsp.lowpass(grains, 2500), 900, 6500)
    crunch *= np.minimum(t / 0.01, 1) * np.exp(-np.maximum(t - 0.03, 0) / (0.05 + 0.015 * v)) * 3.0
    return finish(heel + crunch, -6, 0.04)


@sfx("player.step.metal", "player", variants=4, pitchVar=1.0, volVarDb=2.0, volumeDb=-15, priority=180,
     maxVoices=2, bus="PlayerEquipment", spatial=0.0, vacuum="self")
def step_metal(rng, v):
    """Boot on deck plating: a dull clank with the plate's hollow answer underneath."""
    n = secs(0.35)
    t = np.arange(n) / SR
    base = [212, 388, 655, 1130, 1720]
    plate = ping(rng, 0.35, [f * rng.uniform(0.94, 1.06) for f in base], [0.035, 0.04, 0.035, 0.025, 0.015], [0.5, 0.8, 1.0, 0.7, 0.5], noise=0.12)
    heel = dsp.lowpass(dsp.white(n, rng), 300) * np.exp(-t / 0.02) * 0.6
    rubber = dsp.bandpass(dsp.white(n, rng), 800, 3000) * np.exp(-t / 0.012) * 0.25
    return finish(plate * 0.7 + heel * 0.1 + rubber * 0.8, -6, 0.05, hp=120)


@sfx("player.land", "player", variants=2, pitchVar=0.8, volVarDb=1.0, volumeDb=-10, priority=120,
     bus="PlayerEquipment", spatial=0.0, vacuum="self")
def land(rng, v):
    """Landing from a jump: both boots, the suit's joints taking the load, kit rattling."""
    n = secs(0.5)
    t = np.arange(n) / SR
    thud = dsp.sine(62 + 50 * np.exp(-t / 0.02), n) * np.exp(-t / 0.08)
    dust = dsp.bandpass(dsp.white(n, rng), 700, 5000) * np.exp(-t / 0.07) * 0.4
    rattle = np.zeros(n)
    for _ in range(4):
        at = rng.uniform(0.02, 0.14)
        m = secs(0.04)
        tick = ping(rng, 0.04, [rng.uniform(1800, 4200)], [0.008], [1.0], noise=0.15)
        dsp.place(rattle, tick, secs(at), rng.uniform(0.3, 0.8))
    return finish(dsp.saturate(thud * 0.35 + dust + dsp.bandpass(dsp.white(n, rng), 200, 900) * np.exp(-t / 0.03) * 0.7, 1.5) + rattle * 0.15, -4, 0.06)


@sfx("player.jetpack.loop", "player", loop=True, volumeDb=-12, priority=60, **SUIT,
     note="Jetpack thrust heard through the suit: pressurised gas with a steady nozzle tone. The game scales volume and pitch with thrust.")
def jetpack_loop(rng, v):
    n = secs(7.0)
    hiss = np.stack([dsp.bandpass(dsp.white(n, rng), 700, 6000), dsp.bandpass(dsp.white(n, rng), 700, 6000)], axis=1)
    hiss *= (1 + 0.2 * dsp.smooth_noise(n, 30, rng))[:, None]
    low = dsp.stereo(dsp.lowpass(dsp.brown(n, rng), 220)) * 0.9
    tone = dsp.stereo(dsp.sine(310, n) * 0.05 + dsp.sine(623, n) * 0.03)
    x = dsp.saturate(hiss * 0.35 + low + tone, 1.4)
    return finish_loop(x, -5, 0.6)


@sfx("player.jetpack.burst", "player", variants=3, pitchVar=1.0, volVarDb=1.5, volumeDb=-11, priority=80,
     maxVoices=3, **SUIT)
def jetpack_burst(rng, v):
    """One tap of the jetpack: a valve opens, gas kicks, valve shuts."""
    n = secs(0.28)
    t = np.arange(n) / SR
    env = np.minimum(t / 0.006, 1) * np.exp(-np.maximum(t - 0.1, 0) / 0.035)
    gas = dsp.bandpass(dsp.white(n, rng), 800, 6500) * env
    kick = dsp.sine(80 * (1 + np.exp(-t / 0.01)), n) * np.exp(-t / 0.04) * 0.6
    return finish(dsp.pan(gas * 0.7 + kick, rng.uniform(-0.3, 0.3)), -5, 0.03)


@sfx("player.breath.loop", "player", loop=True, volumeDb=-24, priority=210, **SUIT,
     note="Breathing inside the helmet, one calm in-and-out cycle of four seconds, with the regulator's soft click. The game brings it forward in vacuum, in silence and when hurt.")
def breath_loop(rng, v):
    n = secs(4.0)
    t = np.arange(n) / SR
    x = np.zeros(n)
    # inhale 0.2..1.5 s (brighter, through the regulator); exhale 1.9..3.5 s (darker, longer)
    inh = np.exp(-((t - 0.85) / 0.42) ** 2)
    exh = np.exp(-((t - 2.65) / 0.55) ** 2)
    breath_in = dsp.svf(dsp.white(n, rng), 1900 + 500 * inh, 1.6, "bp") * inh * 0.6
    breath_out = dsp.svf(dsp.white(n, rng), 900 - 250 * exh, 1.2, "bp") * exh * 0.75
    helmet = dsp.resonators(breath_in + breath_out, [410, 1150, 2300], [0.004, 0.003, 0.002], [0.6, 0.4, 0.2]) * 2.0
    x += breath_in + breath_out + helmet
    click = ping(rng, 0.05, [2600, 4100], [0.006, 0.004], [1, 0.6], noise=0.1)
    dsp.place(x, click, secs(0.22), 0.06)
    dsp.place(x, click, secs(1.72), 0.04)
    air = dsp.bandpass(dsp.pink(n, rng), 300, 3000) * 0.03
    return finish(dsp.stereo(x + air), -8, 0.0)


@sfx("player.suit.alarm", "player", volumeDb=-12, priority=15, cooldownMs=900, **SUIT)
def suit_alarm(rng, v):
    """Suit warning (low oxygen or a breach): two urgent notes a tritone apart, impossible to mistake for music."""
    n = secs(0.6)
    x = np.zeros(n)
    for f, at in [(932.33, 0.0), (659.26, 0.2)]:
        m = secs(0.16)
        tt = np.arange(m) / SR
        tone = dsp.square(f, m, 0.3) * 0.5 + dsp.sine(f, m) * 0.5
        tone = dsp.lowpass(tone, 4500) * np.minimum(tt / 0.003, 1) * np.minimum((0.16 - tt) / 0.02, 1)
        dsp.place(x, tone, secs(at), 0.7)
    return finish(x, -6, 0.03)


@sfx("player.hurt", "player", variants=3, pitchVar=1.0, volVarDb=1.5, volumeDb=-8, priority=25, maxVoices=2,
     cooldownMs=90, **SUIT)
def hurt(rng, v):
    """Taking a hit: a blunt thud on the suit shell with a crack of stress."""
    n = secs(0.3)
    t = np.arange(n) / SR
    thud = dsp.sine(88 + 60 * np.exp(-t / 0.015), n) * np.exp(-t / 0.06) * 1.2
    shell = ping(rng, 0.3, [540 * rng.uniform(0.95, 1.05), 1260, 2150, 3300], [0.05, 0.035, 0.02, 0.012], [1, 0.8, 0.5, 0.3], noise=0.15)
    crack = dsp.highpass(dsp.white(n, rng), 3500) * np.exp(-t / 0.005) * 0.4
    return finish(dsp.saturate(thud * 0.15 + shell * 0.9 + crack + dsp.bandpass(dsp.white(n, rng), 300, 1500) * np.exp(-t / 0.02) * 0.6, 1.8), -4, 0.05)


@sfx("player.interact", "player", variants=2, pitchVar=0.8, volumeDb=-14, priority=150, **SUIT)
def interact(rng, v):
    """Using something in the world: a gloved press and a small confirming tick."""
    n = secs(0.14)
    t = np.arange(n) / SR
    press = dsp.lowpass(dsp.white(n, rng), 700) * np.exp(-t / 0.012) * 0.8
    x = press
    tick = ping(rng, 0.08, [1900 + 300 * v, 3200], [0.012, 0.008], [1, 0.5], noise=0.1)
    dsp.place(x, tick, secs(0.03), 0.6)
    return finish(x, -6, 0.02)
