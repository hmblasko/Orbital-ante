"""Warnings that tell the player something is about to happen to them (critique loop round 1, 26 Nov 2026).

Behavioural rule for every sound here: a warning must never sound like a reward. Rewards are musical, bright and in
D on the theme's notes (rewards.py); these are short, dry, low or noisy, and out of the reward ladder, so the ear
learns two separate meanings ("I did well" and "watch out") and never confuses them. Each is one clear shape that is
always the same, so it is learnable (constitution rule 3) and never a surprise rule.
"""
from __future__ import annotations

import numpy as np

from oa_audio import dsp
from oa_audio.dsp import SR, secs

from .common import finish, sfx

SUIT = dict(bus="UI", spatial=0.0, vacuum="suit")
UI = dict(bus="UI", spatial=0.0, vacuum="ui")


@sfx("enemy.spotted", "alerts", volumeDb=-11, pitchVar=0.0, cooldownMs=3500, maxVoices=1, priority=20, **SUIT)
def spotted(rng, v):
    """The suit's threat sensor: you have just been seen. A low thrum under two quick rising pips, like a radar
    lock, about a third of a second. Played once when a quiet stretch ends (nobody had you in sight), panned
    toward whoever saw you, so the ear turns the head before the shot comes."""
    n = secs(0.42)
    t = np.arange(n) / SR
    thrum = dsp.sine(74 + 18 * np.exp(-t / 0.05), n) * np.exp(-t / 0.16) * 0.55
    thrum += dsp.bandpass(dsp.white(n, rng), 90, 300) * np.exp(-t / 0.08) * 0.25
    x = thrum
    for f, at in [(1180.0, 0.02), (1580.0, 0.13)]:
        m = secs(0.075)
        tt = np.arange(m) / SR
        tone = dsp.square(f, m, 0.25) * 0.35 + dsp.sine(f, m) * 0.65
        tone = dsp.lowpass(tone, 5200) * np.minimum(tt / 0.002, 1) * np.minimum((0.075 - tt) / 0.012, 1)
        dsp.place(x, tone, secs(at), 0.5)
    return finish(x, -5, 0.03)


@sfx("enemy.radio.call", "alerts", volumeDb=-14, pitchVar=0.4, cooldownMs=6000, maxVoices=1, priority=40, **SUIT)
def radio_call(rng, v):
    """Enemy radio picked up by the suit: a squelch break, a few garbled syllables (never words), and the squelch
    closing. Plays when a restless squad is sent to look for you, so a long quiet ends with a fair warning
    instead of a squad simply arriving."""
    L = 1.35
    n = secs(L)
    t = np.arange(n) / SR
    x = np.zeros(n)
    # Squelch open and close: short bursts of band-limited hiss.
    for at, dur in [(0.0, 0.07), (L - 0.11, 0.09)]:
        m = secs(dur)
        tt = np.arange(m) / SR
        burst = dsp.bandpass(dsp.white(m, rng), 900, 5200) * np.minimum(tt / 0.003, 1) * np.minimum((dur - tt) / 0.01, 1)
        dsp.place(x, burst, secs(at), 0.55)
    # Syllables: a voiced buzz with a moving formant, gated into five or six uneven bursts, then band-limited
    # like a cheap handset. Pitch and timing vary a little per play through pitchVar.
    f0 = 118 + 14 * np.sin(dsp.TAU * 1.7 * t)
    buzz = dsp.saw(f0, n)
    formant = dsp.bandpass(buzz, 500, 2600)
    gate = np.zeros(n)
    at = 0.12
    while at < L - 0.2:
        dur = rng.uniform(0.07, 0.17)
        m = secs(dur)
        g = np.hanning(m) ** 0.6
        dsp.place(gate, g, secs(at), rng.uniform(0.6, 1.0))
        at += dur + rng.uniform(0.02, 0.08)
    voice = formant * gate
    voice = dsp.saturate(voice * 2.2, 1.6)
    hiss = dsp.bandpass(dsp.white(n, rng), 1200, 4800) * 0.06 * (t > 0.06) * (t < L - 0.1)
    x += voice * 0.6 + hiss
    x = dsp.bandpass(x, 350, 3400)
    return finish(x, -6, 0.02)


@sfx("ante.clock.warn", "alerts", volumeDb=-13, pitchVar=0.0, cooldownMs=5000, maxVoices=1, priority=35, **UI)
def clock_warn(rng, v):
    """Half a minute left and the target not met: three quick, low woodblock knocks, a heads-up rather than an
    alarm. It is the same wooden family as the last-ten-seconds tick, so the ear files it under "the clock", and it
    is deliberately not two falling tones: that shape belongs to the suit (low air) and to overtime. Heard while the
    eyes are on the fight, where the red clock in the corner is not."""
    n = secs(0.5)
    x = np.zeros(n)
    for at, f, g in [(0.0, 640.0, 0.8), (0.11, 640.0, 0.7), (0.22, 560.0, 1.0)]:
        m = secs(0.2)
        tt = np.arange(m) / SR
        body = (dsp.sine(f, m) + 0.45 * dsp.sine(f * 1.66, m) + 0.2 * dsp.sine(f * 2.7, m)) * np.exp(-tt / 0.035)
        knock = dsp.bandpass(dsp.white(m, rng), 900, 4500) * np.exp(-tt / 0.0025) * 0.35
        dsp.place(x, body + knock, secs(at), g)
    return finish(x, -6, 0.04)


@sfx("ante.clock.tick", "alerts", volumeDb=-17, pitchVar=0.0, cooldownMs=600, maxVoices=1, priority=60, **UI)
def clock_tick(rng, v):
    """The last ten seconds of an ante short of its target: a dry woodblock tick once a second. The game raises
    its volume a little each second, never its speed, so it builds without nagging."""
    n = secs(0.12)
    t = np.arange(n) / SR
    body = (dsp.sine(980.0, n) + 0.5 * dsp.sine(1630.0, n)) * np.exp(-t / 0.018)
    knock = dsp.bandpass(dsp.white(n, rng), 1500, 6000) * np.exp(-t / 0.002) * 0.4
    return finish(body + knock, -6, 0.01)


@sfx("ante.overtime", "alerts", volumeDb=-9, pitchVar=0.0, cooldownMs=8000, maxVoices=1, priority=15, **UI)
def overtime(rng, v):
    """The clock has run out short of the target and the hunt is on: the clock's own wood, struck hard once, over a
    low drum that falls away, with a short dry rattle after it. It belongs to the clock family (the 30 s knocks and
    the last-ten ticks lead into it), and it is nothing like the suit's two-note air warning, so "time is up" and
    "air is low" can never be mistaken for each other."""
    n = secs(1.3)
    t = np.arange(n) / SR
    drum = dsp.sine(62 + 70 * np.exp(-t / 0.035), n) * np.exp(-t / 0.38) * 0.95
    skin = dsp.bandpass(dsp.white(n, rng), 120, 700) * np.exp(-t / 0.05) * 0.35
    wood = (dsp.sine(560.0, n) + 0.5 * dsp.sine(560.0 * 1.66, n) + 0.25 * dsp.sine(560.0 * 2.7, n)) * np.exp(-t / 0.06) * 0.6
    crack = dsp.bandpass(dsp.white(n, rng), 900, 5000) * np.exp(-t / 0.003) * 0.5
    # The drum's weight is below what the Deck's speakers play, so it has a second voice an octave and a fifth up.
    tom = dsp.sine(186 + 120 * np.exp(-t / 0.03), n) * np.exp(-t / 0.2) * 0.5
    x = drum * 0.6 + tom * 1.3 + skin + wood * 1.6 + crack
    # The rattle: five quick knocks slowing down, like a dropped stick settling.
    at = 0.34
    gap = 0.07
    for i in range(5):
        m = secs(0.09)
        tt = np.arange(m) / SR
        k = (dsp.sine(480.0, m) + 0.4 * dsp.sine(800.0, m)) * np.exp(-tt / 0.014)
        dsp.place(x, k, secs(at), 0.6 * (0.82 ** i))
        at += gap
        gap *= 1.28
    return finish(dsp.saturate(x, 1.3), -4, 0.08)


@sfx("player.armour.low", "alerts", volumeDb=-13, pitchVar=0.0, cooldownMs=350, maxVoices=1, priority=30, **SUIT)
def armour_low(rng, v):
    """Your own pulse, heard inside the helmet while the suit is on its last plates: two soft low thumps, the second
    a little quieter. The game plays one each beat and stops the moment the suit recovers, so getting behind cover
    is answered by the sound going away. Low and felt more than heard; it must sit under gunfire and music without
    masking either."""
    n = secs(0.46)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for at, g, f in [(0.0, 1.0, 66.0), (0.17, 0.72, 60.0)]:
        m = secs(0.26)
        tt = np.arange(m) / SR
        thump = dsp.sine(f + 26 * np.exp(-tt / 0.02), m) * np.exp(-tt / 0.07)
        # The Deck's own speakers give almost nothing under about 200 Hz, so the pulse also has a knock two and
        # four times up that small speakers can play; on headphones the low thump leads.
        knock = (dsp.sine(f * 4.0, m) * 0.5 + dsp.sine(f * 6.0, m) * 0.3) * np.exp(-tt / 0.03)
        body = dsp.bandpass(dsp.white(m, rng), 180, 520) * np.exp(-tt / 0.02) * 0.3
        dsp.place(x, (thump * 0.45 + knock * 1.5 + body) * np.minimum(tt / 0.004, 1), secs(at), g)
    x = dsp.lowpass(x, 900)
    return finish(x, -5, 0.04)
