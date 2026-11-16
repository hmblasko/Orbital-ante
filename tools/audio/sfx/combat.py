"""Impacts, gore and hit confirmation."""
from __future__ import annotations

import numpy as np

from oa_audio import dsp
from oa_audio.dsp import SR, secs
from oa_audio.seq import hz, midi

from .common import finish, metal_hit, sfx

IMPACT = dict(bus="CombatImpacts", spatial=1.0, minDist=2, maxDist=60, priority=110, vacuum="air")


@sfx("impact.metal", "combat", variants=3, pitchVar=1.2, volVarDb=2, volumeDb=-6, maxVoices=4, **IMPACT)
def impact_metal(rng, v):
    """Bullet on metal: a hard spank and a short clashing ring."""
    sets = [[1180, 1870, 2950, 4100, 6300], [960, 2210, 3380, 5150, 7200], [1420, 2050, 3620, 4480, 8100]]
    x = metal_hit(rng, 0.45, sets[v % 3], decays=[0.09, 0.06, 0.05, 0.03, 0.02], hardness=2.0)
    n = len(x)
    t = np.arange(n) / SR
    x += dsp.bandpass(dsp.white(n, rng), 1500, 9000) * np.exp(-t / 0.008) * 0.5
    x += dsp.sine(210, n) * np.exp(-t / 0.02) * 0.3
    return finish(dsp.saturate(x, 1.8), -3, 0.08)


@sfx("impact.ricochet", "combat", variants=3, pitchVar=1.5, volVarDb=2, volumeDb=-8, maxVoices=3, **IMPACT)
def ricochet(rng, v):
    """Ricochet: the bullet whining away, pitch falling as it tumbles."""
    n = secs(0.75)
    t = np.arange(n) / SR
    f0 = rng.uniform(2600, 3900)
    f = f0 * np.exp(-t / rng.uniform(0.45, 0.8)) * (1 + 0.025 * np.sin(dsp.TAU * rng.uniform(22, 38) * t))
    whine = dsp.sine(f, n) * 0.6 + dsp.sine(f * 2.37, n) * 0.2
    whine *= np.minimum(t / 0.004, 1) * np.exp(-t / 0.2)
    flutter = dsp.bandpass(dsp.white(n, rng), 2000, 8000) * np.exp(-t / 0.12) * 0.15 * (0.5 + 0.5 * np.sin(dsp.TAU * 55 * t))
    spank = metal_hit(rng, 0.2, [1500, 2700, 4400], hardness=2.0)
    x = whine + flutter
    dsp.place(x, spank, 0, 0.5)
    return finish(x, -4, 0.2)


@sfx("impact.dirt", "combat", variants=3, pitchVar=1.5, volVarDb=2, volumeDb=-9, maxVoices=4, **IMPACT)
def impact_dirt(rng, v):
    """Bullet into regolith or soil: a dull thup and a spit of grit."""
    n = secs(0.3)
    t = np.arange(n) / SR
    thup = dsp.lowpass(dsp.white(n, rng), 420) * np.exp(-t / 0.022) * 2.0
    grit = dsp.bandpass(dsp.white(n, rng), 1800, 7000) * np.exp(-t / 0.05) * (rng.uniform(0, 1, n) > 0.6) * 0.5
    grit = dsp.lowpass(grit, 6000)
    return finish(thup + grit, -5, 0.05)


@sfx("impact.flesh", "combat", variants=3, pitchVar=1.5, volVarDb=1.5, volumeDb=-7, maxVoices=4, **IMPACT)
def impact_flesh(rng, v):
    """Bullet into a body: a wet, padded smack."""
    n = secs(0.22)
    t = np.arange(n) / SR
    smack = dsp.bandpass(dsp.white(n, rng), 250, 1800) * np.exp(-t / 0.014) * 1.6
    body = dsp.sine(110 * (1 + 0.8 * np.exp(-t / 0.01)), n) * np.exp(-t / 0.03) * 0.45
    wet = dsp.svf(dsp.white(n, rng), 900 + 2500 * np.exp(-t / 0.03), 3.0, "bp") * np.exp(-t / 0.045) * 0.4
    return finish(dsp.saturate(smack + body + wet, 1.6), -4, 0.04)


@sfx("impact.armor", "combat", variants=2, pitchVar=1.0, volVarDb=1.5, volumeDb=-6, maxVoices=3, **IMPACT)
def impact_armor(rng, v):
    """Bullet on a hard suit shell: a plate knock with a cracked, ceramic edge."""
    x = metal_hit(rng, 0.3, [620, 1480, 2330, 3900], decays=[0.03, 0.025, 0.015, 0.01], hardness=1.6)
    n = len(x)
    t = np.arange(n) / SR
    x += dsp.sine(170, n) * np.exp(-t / 0.03) * 0.5
    x += dsp.highpass(dsp.white(n, rng), 5000) * np.exp(-t / 0.004) * 0.3
    return finish(dsp.saturate(x, 1.8), -4, 0.05)


# ----------------------------------------------------------------------------- gore

GORE = dict(bus="CombatImpacts", spatial=0.6, minDist=3, maxDist=50, priority=60, vacuum="air")


def _pop(rng, size: float, wet: float = 1.0, ring: float = 0.0) -> np.ndarray:
    """A limb coming off: a cork-like pop (pressure release), a tearing rip, and a wet slap.
    size 0 (hand) .. 1 (torso) lowers and lengthens everything."""
    n = secs(0.25 + 0.5 * size)
    t = np.arange(n) / SR
    f0 = 900 - 620 * size
    pop_f = f0 * (0.35 + 0.65 * np.exp(-t / (0.012 + 0.03 * size)))
    pop = dsp.sine(pop_f, n) * np.exp(-t / (0.03 + 0.07 * size))
    air = dsp.bandpass(dsp.white(n, rng), f0 * 0.8, f0 * 5) * np.exp(-t / 0.008) * 0.8
    # rip: crackly band of noise, gated into grains
    grains = (rng.uniform(0, 1, n) > 0.55).astype(float)
    grains = dsp.lowpass(grains, 900)
    rip = dsp.bandpass(dsp.white(n, rng), 500, 3500 - 1500 * size) * grains
    rip *= np.exp(-((t - 0.035 - 0.03 * size) / (0.03 + 0.05 * size)) ** 2) * 0.9
    squelch = dsp.svf(dsp.white(n, rng), 500 + 1800 * np.exp(-t / (0.05 + 0.08 * size)), 4.0, "bp")
    squelch *= np.exp(-t / (0.06 + 0.12 * size)) * 0.5 * wet
    thud = dsp.sine(70 + 60 * (1 - size), n) * np.exp(-t / (0.04 + 0.1 * size)) * (0.4 + 0.9 * size)
    x = pop * 0.9 + air + rip * 1.5 + squelch * 1.5 + thud * 0.45
    if ring > 0:
        x += dsp.sine(1960, n) * np.exp(-t / 0.12) * ring + dsp.sine(2940, n) * np.exp(-t / 0.07) * ring * 0.5
    return dsp.saturate(x, 2.0)


@sfx("gore.pop.small", "combat", variants=3, pitchVar=1.5, volVarDb=1.5, volumeDb=-6, maxVoices=3, **GORE)
def pop_small(rng, v):
    """Hand or foot shot off."""
    return finish(_pop(rng, 0.05, 0.7), -4, 0.04)


@sfx("gore.pop.medium", "combat", variants=3, pitchVar=1.5, volVarDb=1.5, volumeDb=-5, maxVoices=3, **GORE)
def pop_medium(rng, v):
    """Forearm or shin shot off."""
    return finish(_pop(rng, 0.3, 0.9), -3, 0.05)


@sfx("gore.pop.big", "combat", variants=3, pitchVar=1.2, volVarDb=1.5, volumeDb=-4, maxVoices=3, **GORE)
def pop_big(rng, v):
    """Upper arm or thigh shot off."""
    return finish(_pop(rng, 0.55, 1.0), -2, 0.06)


@sfx("gore.pop.torso", "combat", variants=2, pitchVar=1.0, volVarDb=1.0, volumeDb=-2, maxVoices=2, **GORE)
def pop_torso(rng, v):
    """Torso destroyed: the big wet one."""
    return finish(_pop(rng, 1.0, 1.3), -1, 0.1, limit=True)


@sfx("gore.pop.head", "combat", variants=2, pitchVar=1.0, volVarDb=1.0, volumeDb=-3, maxVoices=2, **GORE)
def pop_head(rng, v):
    """Head destroyed: a sharp pop with a bright ring so a kill is unmistakable."""
    return finish(_pop(rng, 0.2, 0.8, ring=0.18), -2, 0.08)


@sfx("gore.splat", "combat", variants=3, pitchVar=2.5, volVarDb=3, volumeDb=-12, maxVoices=3, cooldownMs=50,
     bus="CombatImpacts", spatial=1.0, minDist=2, maxDist=30, priority=160, vacuum="air")
def splat(rng, v):
    """A chunk landing."""
    n = secs(0.2)
    t = np.arange(n) / SR
    x = dsp.svf(dsp.white(n, rng), 300 + 1500 * np.exp(-t / 0.02), 2.5, "bp") * np.exp(-t / 0.04)
    x += dsp.lowpass(dsp.white(n, rng), 260) * np.exp(-t / 0.025) * 1.2
    return finish(x, -6, 0.04)


# ----------------------------------------------------------------------------- hit confirmation (never filtered)

UI_HIT = dict(bus="UI", spatial=0.0, priority=10, vacuum="ui")


@sfx("hit.marker", "combat", variants=1, pitchVar=0.5, volumeDb=-7, cooldownMs=35, maxVoices=3, **UI_HIT)
def hit_marker(rng, v):
    """Hit confirm: a dry, bright tick with a click on its front, short enough to follow automatic fire and
    pitched (around 3 kHz) above where the guns and the music are busiest, so it is heard through both."""
    n = secs(0.07)
    t = np.arange(n) / SR
    x = dsp.pad_to(metal_hit(rng, 0.07, [2940, 4410, 1470], decays=[0.012, 0.008, 0.014], gains=[1.0, 0.55, 0.3], hardness=0.7), n)
    x = x + dsp.highpass(dsp.white(n, rng), 3500) * np.exp(-t / 0.0012) * 0.5
    return finish(x, -4, 0.01)


@sfx("hit.headshot", "combat", variants=1, pitchVar=0.0, volumeDb=-8, cooldownMs=60, maxVoices=2, **UI_HIT)
def hit_headshot(rng, v):
    """Headshot ping: a bright bell on the home note (D), so it sits in tune with the music."""
    n = secs(0.6)
    t = np.arange(n) / SR
    f = hz(midi("D7"))
    x = dsp.fm(f, n, 3.5, 2.0 * np.exp(-t / 0.04)) * np.exp(-t / 0.13)
    x += dsp.sine(f * 1.5, n) * np.exp(-t / 0.07) * 0.35
    x += dsp.highpass(dsp.white(n, rng), 6000) * np.exp(-t / 0.003) * 0.3
    return finish(x, -4, 0.1)


@sfx("hit.kill", "combat", variants=1, pitchVar=0.0, volumeDb=-9, cooldownMs=80, maxVoices=2, **UI_HIT)
def hit_kill(rng, v):
    """Kill confirm: two quick notes rising a fourth (A to D), with a soft thump underneath."""
    n = secs(0.5)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for k, (note, at) in enumerate([("A5", 0.0), ("D6", 0.07)]):
        m = n - secs(at)
        tt = np.arange(m) / SR
        s = dsp.fm(hz(midi(note)), m, 2.0, 1.5 * np.exp(-tt / 0.05)) * np.exp(-tt / 0.1)
        dsp.place(x, s, secs(at), 0.8)
    x += dsp.sine(98, n) * np.exp(-t / 0.05) * 0.5
    return finish(x, -4, 0.1)


# ----------------------------------------------------------------------------- slow motion

@sfx("fx.slowmo.in", "combat", spatial=0.0, volumeDb=-9, bus="PlayerEquipment", priority=40, vacuum="suit", cooldownMs=400)
def slowmo_in(rng, v):
    """Time slowing down: the room's air seems to be drawn out of it, a tone sinking as it goes, and one heavy
    heartbeat arriving at the bottom."""
    n = secs(1.1)
    t = np.arange(n) / SR
    fall = 900 * np.exp(-t / 0.22) + 110
    tone = dsp.sine(fall, n) * np.exp(-t / 0.5) * 0.5 + dsp.sine(fall * 1.5, n) * np.exp(-t / 0.3) * 0.2
    air = dsp.svf(dsp.white(n, rng), 6000 * np.exp(-t / 0.2) + 350, 1.2, "lp", 2) * np.exp(-t / 0.35) * 0.35
    beat = (dsp.sine(62 + 50 * np.exp(-np.maximum(t - 0.55, 0) / 0.03), n) + dsp.bandpass(dsp.white(n, rng), 300, 900) * 0.25) \
        * np.exp(-np.maximum(t - 0.55, 0) / 0.11) * (t > 0.55) * 0.8
    return finish(tone + air + beat, -4, 0.2)


@sfx("fx.slowmo.out", "combat", spatial=0.0, volumeDb=-11, bus="PlayerEquipment", priority=60, vacuum="suit", cooldownMs=400)
def slowmo_out(rng, v):
    """Time snapping back: the same breath let go, rising fast and stopping short."""
    n = secs(0.5)
    t = np.arange(n) / SR
    rise = 140 * np.exp(t / 0.19)
    tone = dsp.sine(rise, n) * np.minimum(t / 0.25, 1) ** 2 * 0.4
    air = dsp.svf(dsp.white(n, rng), 400 + 9000 * (t / 0.5) ** 2, 1.2, "lp", 2) * np.minimum(t / 0.3, 1) ** 2 * 0.4
    x = (tone + air) * np.exp(-np.maximum(t - 0.4, 0) / 0.02)
    return finish(x, -5, 0.04)
