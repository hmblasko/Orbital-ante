"""The alien creatures' own sounds. The Screamer (the cicada) first: its sound is its weapon, so the warning and the
scream must be learnable (always the same shape) and heard over gunfire, without being a sound nobody can bear.
Then the Ambusher (the bobbit worm): a wet hiss as it rears, the scrape of its jaws flying wide where it takes aim,
the snap of the strike, and the ground breaking where it comes up.

Every creature's warning (Ralph loop pass 2, 27 Nov 2026) is its own, so a warning heard from behind says which
animal it is, and comes in two sounds:

- the wind-up (`creature.<kind>.raise`, `.curl`, `.rear`, `.gape`, `.mark`, `.windup`, `.coil`), played as the
  creature starts to warn, while it still follows the player;
- the aim (`creature.<kind>.aim`), one hard mark (a clack, a snick, a clunk, a click, a thoom, a crack, a scrape, a
  pop) at the very front of its file, played by the game at the moment the creature takes aim
  (`Creatures/Windups.Change`). After the mark a step aside is a miss.

The game plays the mark on the creature's own clock, so it is on the lock whatever a dial, Focus or a hitstop does,
and a warning called off before it takes aim never marks. Each pair is cut from one designed warning (`_halves`), so
played on time the two are that warning, and both halves keep its balance: the mark stands well over its wind-up (rule
test `EveryAim_MarksTheMomentItPlays_AndStandsWellOverItsWindup`). A wind-up lets go of its layers just after the lock
instead of stopping dead, so a lock that comes late still finds it sounding.

Warnings are dry, never musical (critique loop round 1), and none may sound like the ante clock's wood (game-designer
review, pass 2): no mark or wind-up rings within two semitones of the clock's tick (979 and 1629 Hz), and no wind-up
rings near its warning's knocks (640 Hz) (checked by `tools/audio/qa.py`). The Deck's own speakers play almost nothing
under 200 Hz, so every pair is measured above it: the mark at least 5 dB over its wind-up there, the wind-up's loudest
100 ms at least -35 dB (the Drifting maw's thoom is mostly low, with an upper ring for the speakers)."""
from __future__ import annotations

import zlib

import numpy as np

from oa_audio import dsp
from oa_audio.dsp import SR, secs

from .common import _tp_safe, finish, finish_loop, sfx

CREATURE = dict(bus="CombatImpacts", spatial=1.0, minDist=5, maxDist=220, priority=50, vacuum="air", maxVoices=2)

# A warning's two halves are allowed to play again almost at once: two creatures of a kind may warn a moment apart,
# and each must be heard (the Unity review, pass 2). Two voices an event still stops a pile-up.
WARN_GAP_MS = 60

# A cicada sings by buckling a ribbed drum: every rib is a click, and its hollow abdomen rings to them. A real one
# rings near 4 kHz; an animal the size of a person rings much lower.
RING = (1150.0, 1720.0, 2380.0, 3300.0)
# Its hard double click where it takes aim rings the drum without the 1720 Hz ring, which sits by the clock's tick.
LOCK_RING = (1150.0, 2050.0, 2900.0, 3800.0)


def _drum(clicks: np.ndarray, rng, air: float = 0.0, ring=RING) -> np.ndarray:
    """A train of clicks through the abdomen's resonances, with a little breath of noise."""
    n = len(clicks)
    x = dsp.resonators(clicks, ring, (0.006, 0.005, 0.004, 0.003), (1.0, 0.8, 0.5, 0.3))
    body = dsp.bandpass(clicks, 180, 600) * 0.5
    hiss = dsp.bandpass(dsp.white(n, rng), 2500, 7000) * air
    return x + body + hiss


def _clicks(rate: np.ndarray, rng, jitter: float = 0.06) -> np.ndarray:
    """One sample at each click, the clicks coming at `rate` a second, not quite evenly."""
    phase = np.cumsum(rate * (1.0 + jitter * dsp.smooth_noise(len(rate), 40.0, rng))) / SR
    edge = np.diff(np.floor(phase), prepend=0.0) > 0
    return edge.astype(float) * rng.uniform(0.7, 1.0, len(rate))


# ------------------------------------------------------------------ a warning in two halves

# The mark's front sits this far into its own file, so the file never starts on a step.
AIM_PRE_ROLL = 0.004


def _design_rng(kind: str) -> np.random.Generator:
    """One random stream for both halves of a warning, so the two are cut from the very same sound."""
    return np.random.default_rng(zlib.crc32(f"creature.{kind}.warning".encode()))


def _until(t: np.ndarray, at: float, release: float) -> np.ndarray:
    """1 until `at`, then down to 0 over `release` seconds: a layer that lets go where the mark comes, instead of
    stopping dead (a late lock still finds the wind-up sounding)."""
    return np.clip(1.0 - (t - at) / max(1e-3, release), 0.0, 1.0)


def _level(pre: np.ndarray, post: np.ndarray, peak_db: float, hp: float) -> float:
    """The gain that sets the whole warning, wind-up and aim together as designed, to its peak level, so the two
    halves keep their balance however each is played."""
    whole = dsp.highpass(pre + post, hp) if hp else pre + post
    safe = _tp_safe(dsp.normalize_peak(whole, peak_db), peak_db)
    return float(np.max(np.abs(safe)) / (np.max(np.abs(whole)) + 1e-12))


def _halves(pre: np.ndarray, post: np.ndarray, lock: float, keep: float, peak_db: float, hp: float, fade_out: float, half: str):
    """One half of a designed warning: "windup" is everything before the mark, to `keep` seconds into the file;
    "aim" is the mark and everything after it, starting `AIM_PRE_ROLL` before the mark."""
    g = _level(pre, post, peak_db, hp)
    if half == "windup":
        x = (dsp.highpass(pre, hp) if hp else pre) * g
        x = x[: min(len(x), secs(keep))]
    else:
        x = (dsp.highpass(post, hp) if hp else post) * g
        x = x[max(0, secs(lock) - secs(AIM_PRE_ROLL)):]
        # A mark that rings out before its file ends stops there (a voice is held for the file's whole length).
        loud = np.nonzero(np.abs(x) > np.max(np.abs(x)) * 1e-3)[0]
        if len(loud):
            x = x[: min(len(x), loud[-1] + secs(0.01))]
    return dsp.fade(x, 0.00035, fade_out)


def _knock(at: np.ndarray | list, n: int, freqs, decays, gains, rng, grit: float = 0.25, grit_hp: float = 2500.0) -> np.ndarray:
    """Hard shell struck on hard shell: impulses at the given seconds through a few short resonances, each with a
    tiny burst of bright noise on its front so it cuts through gunfire."""
    hits = np.zeros(n)
    for when, gain in at:
        k = secs(when)
        if 0 <= k < n:
            hits[k] += gain
    body = _ring(hits, freqs, decays, gains)
    edge = np.zeros(n)
    for when, gain in at:
        k = secs(when)
        m = secs(0.004)
        if 0 <= k < n - m:
            edge[k:k + m] += dsp.highpass(dsp.white(m, rng), grit_hp) * np.exp(-np.arange(m) / SR / 0.0009) * gain
    return body + edge * grit


def _ring(x: np.ndarray, freqs, decays, gains) -> np.ndarray:
    """Resonances rung by single-sample impulses, scaled so a unit impulse rings each one at about its gain (a bare
    resonator answers an impulse a few thousand times quieter than a noise layer beside it)."""
    return dsp.resonators(x, freqs, decays, [g * d * SR for g, d in zip(gains, decays)])


def _crack(n: int, at: list, rng, low: float = 1500.0, high: float = 5000.0, tau: float = 0.0011) -> np.ndarray:
    """A hard broadband crack, each one under 5 ms: no ring to give it a pitch (so it is never wood, never a tick)."""
    x = np.zeros(n)
    m = secs(0.005)
    tt = np.arange(m) / SR
    for when, gain in at:
        k = secs(when)
        if 0 <= k < n - m:
            x[k:k + m] += dsp.bandpass(dsp.white(m, rng), low, high) * np.exp(-tt / tau) * gain
    return x


def _scrapes(n: int, rng, at: list, low: float, high: float) -> np.ndarray:
    """Shell dragged over shell: short rough bursts of noise, each its own length and loudness."""
    x = np.zeros(n)
    for when, gain in at:
        k = secs(when)
        m = secs(rng.uniform(0.012, 0.032))
        if k >= n or m < 8:
            continue
        tt = np.arange(m) / SR
        env = np.clip(tt / 0.0015, 0, 1) * np.exp(-tt / (m / SR * 0.4)) * (1.0 + 0.7 * dsp.smooth_noise(m, 250.0, rng))
        dsp.place(x, dsp.bandpass(dsp.white(m, rng), low, high) * env, k, gain)
    return x


def _bubbles(n: int, rng, times, low: float, high: float, size: float = 1.0) -> np.ndarray:
    """Bubbles, each a short ringing sine that rises a little as it goes (a bubble's ring climbs as it nears the
    surface). Smaller bubbles ring higher and shorter."""
    x = np.zeros(n)
    for when in times:
        f0 = rng.uniform(low, high)
        dur = 0.012 + 0.03 * (low / f0) * size
        m = secs(dur * 3.0)
        k = secs(when)
        if k >= n:
            continue
        tt = np.arange(m) / SR
        f = f0 * (1.0 + 0.35 * np.clip(tt / (dur * 2.0), 0, 1))
        b = np.sin(dsp.TAU * np.cumsum(f) / SR) * np.exp(-tt / dur) * rng.uniform(0.4, 1.0)
        b[: secs(0.0015)] *= np.linspace(0, 1, secs(0.0015))
        dsp.place(x, b, k)
    return x


def _sweep(n: int, rng, start: float, peak: float, end: float, lo: float, hi: float, q: float = 1.6, noise: str = "white") -> np.ndarray:
    """Air or water pushed aside: noise through a band that moves from lo to hi, swelling from start to its peak and
    gone by end (seconds)."""
    t = np.arange(n) / SR
    src = dsp.white(n, rng) if noise == "white" else dsp.pink(n, rng)
    span = np.clip((t - start) / max(1e-3, end - start), 0, 1)
    band = dsp.svf(src, lo + (hi - lo) * span, q, "bp")
    rise = np.clip((t - start) / max(1e-3, peak - start), 0, 1) ** 2
    fall = np.clip((end - t) / max(1e-3, end - peak), 0, 1)
    return band * np.where(t < peak, rise, fall) * (t >= start) * (t <= end)


def _blade(n: int, rng, low: float, high: float) -> np.ndarray:
    """Hard chitin drawn over hard chitin: a short ringing scrape."""
    t = np.arange(n) / SR
    scrape = dsp.bandpass(dsp.white(n, rng), low, high) * np.exp(-t / 0.035)
    ring = dsp.resonators(scrape, (2100.0, 3350.0, 4700.0), (0.030, 0.022, 0.015), (1.0, 0.6, 0.35))
    return scrape * 0.6 + ring * 0.5


# ------------------------------------------------------------------ the Screamer (the cicada)

SCREAMER_LOCK = 0.60


def _screamer():
    """The Screamer's warning, 1 s: single dry clicks of its drums that quicken into a rattle, rising, with a hard
    double click at 0.6 s where it takes aim. The end of the climb is the moment the scream starts."""
    rng = _design_rng("screamer")
    length, lock = 1.0, SCREAMER_LOCK
    n = secs(length)
    t = np.arange(n) / SR
    ramp = np.clip(t / length, 0, 1)
    rate = 9.0 + 150.0 * ramp ** 2.2
    # (Loud enough to be heard from its first clicks: the mark still stands about 10 dB over the rattle's loudest.)
    pre = _drum(_clicks(rate, rng), rng) * (0.35 + 0.65 * ramp) * 2.2
    pre *= np.minimum((length - t) / 0.01, 1.0)
    clicks = np.zeros(n)
    for at in (lock, lock + 0.035):
        clicks[secs(at)] = 1.6
    post = _drum(clicks, rng, ring=LOCK_RING) * 1.2
    edge = _crack(n, [(lock, 0.5), (lock + 0.035, 0.4)], rng, 2500.0, 7000.0, 0.0008)
    post += edge
    return pre, post


@sfx("creature.screamer.windup", "creature", variants=1, pitchVar=0.2, volumeDb=-5, cooldownMs=WARN_GAP_MS, **CREATURE)
def screamer_windup(rng, v):
    """The Screamer's wind-up, 1 s: dry clicks of its drums quickening into a rattle as it lifts its abdomen and fans
    its wings, rising until the scream starts. Its aim is a separate mark."""
    pre, post = _screamer()
    return _halves(pre, post, SCREAMER_LOCK, 1.0, -4, 120, 0.01, "windup")


@sfx("creature.screamer.aim", "creature", variants=1, pitchVar=0.2, volumeDb=-5, cooldownMs=WARN_GAP_MS, **CREATURE)
def screamer_aim(rng, v):
    """It takes aim: a hard double click of both drums as its wings and abdomen jerk to full height."""
    pre, post = _screamer()
    return _halves(pre, post, SCREAMER_LOCK, 0.0, -4, 120, 0.02, "aim")


@sfx("creature.screamer.scream", "creature", variants=1, volumeDb=-3, loop=True, **CREATURE)
def screamer_scream(rng, v):
    """The Screamer's scream, a loop for as long as it lasts: both drums buckling about 190 times a second, not
    quite together, ringing its hollow abdomen; it throbs five times a second, which is the beat it hurts on."""
    length = 3.0 + 0.5 + 0.25
    n = secs(length)
    t = np.arange(n) / SR
    one = _clicks(np.full(n, 188.0), rng, 0.03)
    two = _clicks(np.full(n, 193.0), rng, 0.03)
    x = _drum(one + two * 0.9, rng, air=0.05)
    throb = 0.72 + 0.28 * np.sin(dsp.TAU * 5.0 * t) ** 2
    x = dsp.saturate(x * throb, 1.6, 0.6)
    # Not the piercing top of a real cicada: this has to be borne for two and a half seconds at a time.
    x = dsp.lowpass(x, 5200)
    return finish_loop(x, -6, 0.5, hp=140)


# ------------------------------------------------------------------ the Ambusher (the bobbit worm)

AMBUSHER = dict(bus="CombatImpacts", spatial=1.0, minDist=3, maxDist=90, priority=55, vacuum="air", maxVoices=2)
AMBUSHER_LOCK = 0.495


def _ambusher():
    """The Ambusher's warning, 0.9 s: a wet hiss that tightens as it rears, ticks of its jaws half closing, and at
    0.5 s (where it takes aim) the scrape of both jaws flying wide."""
    rng = _design_rng("ambusher")
    length, lock = 0.9, AMBUSHER_LOCK
    n = secs(length)
    t = np.arange(n) / SR
    ramp = np.clip(t / length, 0, 1)
    hiss = dsp.svf(dsp.pink(n, rng), 500.0 + 2600.0 * ramp ** 1.5, 2.5, "bp") * (0.25 + 0.75 * ramp) * (1.0 + 0.3 * dsp.smooth_noise(n, 30.0, rng))
    ticks = np.zeros(n)
    for at in (0.06, 0.17, 0.26, 0.33, 0.39, 0.44):
        ticks[secs(at)] = rng.uniform(0.6, 1.0)
    # (Its ticks ring at 1250 and 2250 Hz, clear of the clock's tick.)
    click = dsp.resonators(ticks, (1250.0, 2250.0), (0.008, 0.005), (1.0, 0.6))
    pre = hiss * 0.5 + click * 0.9
    pre *= np.minimum((length - t) / 0.015, 1.0)
    post = np.zeros(n)
    dsp.place(post, _blade(secs(0.22), rng, 1800, 7000), secs(lock), 2.0)
    return pre, post


@sfx("creature.ambusher.coil", "creature", variants=1, pitchVar=0.15, volumeDb=-5, cooldownMs=WARN_GAP_MS, **AMBUSHER)
def ambusher_coil(rng, v):
    """The Ambusher's wind-up, 0.9 s: a wet hiss that tightens as it rears, and ticks of its jaws half closing."""
    pre, post = _ambusher()
    return _halves(pre, post, AMBUSHER_LOCK, 0.9, -4, 150, 0.015, "windup")


@sfx("creature.ambusher.aim", "creature", variants=1, pitchVar=0.15, volumeDb=-5, cooldownMs=WARN_GAP_MS, **AMBUSHER)
def ambusher_aim(rng, v):
    """It takes aim: the ringing scrape of both jaws flying wide. After the scrape, stepping aside is a miss."""
    pre, post = _ambusher()
    return _halves(pre, post, AMBUSHER_LOCK, 0.0, -4, 150, 0.03, "aim")


@sfx("creature.ambusher.strike", "creature", variants=2, pitchVar=0.2, volumeDb=-3, cooldownMs=150, **AMBUSHER)
def ambusher_strike(rng, v):
    """The strike: its length thrown out through the air in under a fifth of a second, and the jaws snapping shut."""
    n = secs(0.42)
    t = np.arange(n) / SR
    rush = dsp.svf(dsp.white(n, rng), 700.0 + 3800.0 * np.clip(t / 0.16, 0, 1), 1.8, "bp") * np.exp(-((t - 0.10) / 0.06) ** 2)
    x = rush * 0.7
    snap = _blade(secs(0.16), rng, 1200, 6000)
    thud = dsp.sine(140.0 * np.exp(-np.arange(secs(0.12)) / SR / 0.05) + 60.0, secs(0.12)) * dsp.env_exp(secs(0.12), 0.035)
    dsp.place(x, snap, secs(0.17), 1.6)
    dsp.place(x, thud, secs(0.17), 0.9)
    return finish(x, -3, 0.03, hp=70)


@sfx("creature.ambusher.rise", "creature", variants=2, pitchVar=0.25, volumeDb=-6, cooldownMs=400, **AMBUSHER)
def ambusher_rise(rng, v):
    """It comes up: the ground heaving, then breaking, and grit falling back. The heave comes first on purpose, so
    there is time to look down."""
    n = secs(1.1)
    t = np.arange(n) / SR
    heave = dsp.lowpass(dsp.brown(n, rng), 160) * np.clip(t / 0.4, 0, 1) ** 2 * np.exp(-np.clip(t - 0.5, 0, None) / 0.2)
    crack = dsp.bandpass(dsp.white(n, rng), 300, 2800) * np.exp(-np.clip(t - 0.42, 0, None) / 0.07) * (t > 0.42)
    grains = (rng.random(n) < (220.0 / SR) * np.clip((t - 0.45) / 0.1, 0, 1) * np.exp(-np.clip(t - 0.55, 0, None) / 0.25)) * rng.uniform(0.3, 1.0, n)
    grit = dsp.resonators(grains, (900.0, 1900.0, 3100.0), (0.004, 0.003, 0.002), (1.0, 0.7, 0.4))
    # The ground straining before it gives: small crackles under the heave, so the heave (the tell) is heard on the
    # Deck's own speakers too, which play almost nothing under 200 Hz (Ralph loop pass 2).
    pops = (rng.random(n) < (70.0 / SR) * np.clip(t / 0.4, 0, 1) ** 1.5 * (t < 0.46)) * rng.uniform(0.2, 0.8, n)
    strain = _ring(pops, (650.0, 1300.0, 2200.0), (0.006, 0.004, 0.003), (1.0, 0.6, 0.3))
    x = heave * 1.8 + crack * 0.8 + grit * 0.6 + strain * 0.9
    return finish(x, -5, 0.08, hp=40)


# ------------------------------------------------------------------ the Blind stalker (the olm)

STALKER = dict(bus="CombatImpacts", spatial=1.0, minDist=3, maxDist=70, priority=55, vacuum="air", maxVoices=2)
STALKER_LOCK = 0.45


def _wet_click(rng, n, low=900.0, high=2400.0):
    """One soft click from a wet throat: two short resonances and a breath of noise."""
    hit = np.zeros(n)
    hit[0] = 1.0
    hit[min(n - 1, secs(0.011))] = rng.uniform(0.4, 0.7)
    body = dsp.resonators(hit, (low, high), (0.010, 0.006), (1.0, 0.55))
    return body + dsp.bandpass(dsp.white(n, rng), 1500, 5000) * dsp.env_exp(n, 0.004) * 0.25


@sfx("creature.stalker.click", "creature", variants=3, pitchVar=0.12, volumeDb=-13, cooldownMs=250, **STALKER)
def stalker_click(rng, v):
    """Each time it takes a new place from what it hears: a quiet double click. It means: where you are now is known."""
    n = secs(0.12)
    x = _wet_click(rng, n, 1000.0 + 150.0 * v, 2500.0 + 200.0 * v)
    return finish(x, -8, 0.01, hp=300)


@sfx("creature.stalker.hear", "creature", variants=2, pitchVar=0.15, volumeDb=-8, cooldownMs=500, **STALKER)
def stalker_hear(rng, v):
    """It has heard something, 0.7 s: a slow string of clicks that quickens as its head comes round, under a thin
    wet breath as its gills stand out."""
    length = 0.7
    n = secs(length)
    t = np.arange(n) / SR
    x = np.zeros(n)
    at, gap = 0.03, 0.15
    while at < length - 0.08:
        dsp.place(x, _wet_click(rng, secs(0.1), 900.0 + 500.0 * at, 2300.0 + 600.0 * at), secs(at), rng.uniform(0.6, 1.0))
        at += gap
        gap *= 0.78
    breath = dsp.svf(dsp.pink(n, rng), 1400.0 + 900.0 * t / length, 2.0, "bp") * np.sin(np.pi * np.clip(t / length, 0, 1)) ** 2
    x = x * 0.9 + breath * 0.22
    return finish(x, -6, 0.03, hp=250)


def _stalker():
    """The Blind stalker's warning, 0.9 s: a long wet breath drawn in through its opening mouth, rising, and at 0.45 s
    (where it takes aim) the pop of the jaw dropping wide and the gills snapping out."""
    rng = _design_rng("stalker")
    length, lock = 0.9, STALKER_LOCK
    n = secs(length)
    t = np.arange(n) / SR
    ramp = np.clip(t / length, 0, 1)
    breath = dsp.svf(dsp.pink(n, rng), 350.0 + 1900.0 * ramp ** 1.3, 2.2, "bp") * (0.2 + 0.8 * ramp) * (1.0 + 0.35 * dsp.smooth_noise(n, 22.0, rng))
    gurgle = dsp.lowpass(dsp.brown(n, rng), 220) * (0.5 + 0.5 * np.sin(2 * np.pi * 17.0 * t)) * ramp
    pre = breath * 0.55 + gurgle * 0.8
    pre *= np.minimum((length - t) / 0.015, 1.0)
    pop_n = secs(0.16)
    pop = dsp.sine(520.0 * np.exp(-np.arange(pop_n) / SR / 0.02) + 150.0, pop_n) * dsp.env_exp(pop_n, 0.03) + _wet_click(rng, pop_n, 700.0, 1900.0) * 0.8
    post = np.zeros(n)
    dsp.place(post, pop, secs(lock), 1.5)
    return pre, post


@sfx("creature.stalker.gape", "creature", variants=1, pitchVar=0.12, volumeDb=-5, cooldownMs=WARN_GAP_MS, **STALKER)
def stalker_gape(rng, v):
    """The Blind stalker's wind-up, 0.9 s: a long wet breath drawn in through its opening mouth, rising."""
    pre, post = _stalker()
    return _halves(pre, post, STALKER_LOCK, 0.9, -4, 90, 0.02, "windup")


@sfx("creature.stalker.aim", "creature", variants=1, pitchVar=0.12, volumeDb=-5, cooldownMs=WARN_GAP_MS, **STALKER)
def stalker_aim(rng, v):
    """It takes aim: the pop of its jaw dropping wide and its gills snapping out. After the pop, stepping aside is a miss."""
    pre, post = _stalker()
    return _halves(pre, post, STALKER_LOCK, 0.0, -4, 90, 0.03, "aim")


@sfx("creature.stalker.lunge", "creature", variants=2, pitchVar=0.2, volumeDb=-3, cooldownMs=150, **STALKER)
def stalker_lunge(rng, v):
    """The lunge: its whole wet length thrown across the ground in a fifth of a second, and the jaws clapping shut."""
    n = secs(0.45)
    t = np.arange(n) / SR
    rush = dsp.svf(dsp.white(n, rng), 500.0 + 2600.0 * np.clip(t / 0.15, 0, 1), 1.6, "bp") * np.exp(-((t - 0.08) / 0.055) ** 2)
    slap = dsp.bandpass(dsp.white(n, rng), 250, 1800) * np.exp(-np.clip(t - 0.03, 0, None) / 0.05) * (t > 0.03) * 0.5
    x = rush * 0.6 + slap
    clap_n = secs(0.14)
    # The thump under the clap is kept small (Ralph loop pass 2): it was nine tenths of the sound's weight, all of it
    # below what the Deck's speakers play.
    clap = dsp.bandpass(dsp.white(clap_n, rng), 600, 4200) * dsp.env_exp(clap_n, 0.018) + dsp.sine(170.0 * np.exp(-np.arange(clap_n) / SR / 0.04) + 70.0, clap_n) * dsp.env_exp(clap_n, 0.04) * 0.3
    dsp.place(x, clap, secs(0.155), 1.6)
    return finish(x, -3, 0.04, hp=70)


# ------------------------------------------------------------------ warnings for the rest (Ralph loop pass 2)
# Each lock time is the brain's lock share of the warning's default length (see the C# in Creatures/Windups.cs):
# the Strider 0.25 x 1.6 s, the Sprinter 0.25 x 1.05 s, the Roller 0.6 x 0.9 s, the Cave hunter 0.65 x 0.75 s,
# the Drifting maw 0.6 x 1.4 s, the Pack biter 0.6 x 0.9 s. The game plays the aim at the lock itself, so these
# only shape each designed warning.

WALKER = dict(bus="CombatImpacts", spatial=1.0, minDist=4, maxDist=110, priority=55, vacuum="air", maxVoices=2)
SWIMMER = dict(bus="CombatImpacts", spatial=1.0, minDist=4, maxDist=90, priority=55, vacuum="air", maxVoices=2)

STRIDER_LOCK = 0.40
SPRINTER_LOCK = 0.2625
ROLLER_LOCK = 0.54
HUNTER_LOCK = 0.4875
MAW_LOCK = 0.84
BITER_LOCK = 0.54


def _strider():
    """The Strider's warning, 1 s: its claw arm creaking up joint by joint, heavy shell grinding on shell, and at
    0.4 s (where it takes aim) a hard double clack as the claw sets open. Then the swing: a heavy whoosh that ends
    where the claw comes down."""
    rng = _design_rng("strider")
    length, lock = 1.0, STRIDER_LOCK
    n = secs(length)
    t = np.arange(n) / SR
    up = np.clip(t / lock, 0, 1)
    let = _until(t, lock, 0.22)
    # The creak: a joint slipping and catching, quicker as the arm comes up, through the hollow of a big shell.
    rate = 16.0 + 34.0 * up ** 1.4
    slips = _clicks(rate, rng, 0.3) * let
    creak = _ring(slips, (300.0, 470.0, 1250.0, 2150.0), (0.022, 0.016, 0.010, 0.006), (1.0, 0.75, 0.5, 0.28))
    grind = dsp.bandpass(dsp.pink(n, rng), 260, 1500) * (0.15 + 0.85 * up) * let * (1.0 + 0.5 * dsp.smooth_noise(n, 18.0, rng))
    pre = creak * (0.35 + 0.65 * up) * 0.45 + grind * 1.5
    # The lock: claw set open, a hard double clack (rung clear of the clock's tick).
    clack = _knock([(lock, 1.0), (lock + 0.034, 0.7)], n, (450.0, 780.0, 1420.0, 2350.0), (0.040, 0.026, 0.014, 0.008),
                   (1.0, 0.85, 0.55, 0.3), rng, grit=0.5)
    post = clack * 1.25
    # The swing: a heavy arm through the air, rising and gone where the claw lands (0.62 of the strike, 0.99 s).
    post += _sweep(n, rng, 0.50, 0.93, 0.99, 260.0, 1250.0, 1.3) * 2.5
    post *= np.minimum((length - t) / 0.01, 1.0)
    return pre, post


@sfx("creature.strider.raise", "creature", variants=1, pitchVar=0.15, volumeDb=-5, cooldownMs=WARN_GAP_MS, **WALKER)
def strider_raise(rng, v):
    """The Strider's wind-up: its claw arm creaking up joint by joint, heavy shell grinding on shell."""
    pre, post = _strider()
    return _halves(pre, post, STRIDER_LOCK, STRIDER_LOCK + 0.22, -4, 110, 0.02, "windup")


@sfx("creature.strider.aim", "creature", variants=1, pitchVar=0.15, volumeDb=-5, cooldownMs=WARN_GAP_MS, **WALKER)
def strider_aim(rng, v):
    """It takes aim: a hard double clack as the claw sets open, then the heavy whoosh of the swing, gone where the claw
    comes down. After the clack, stepping aside is a miss."""
    pre, post = _strider()
    return _halves(pre, post, STRIDER_LOCK, 0.0, -4, 110, 0.01, "aim")


def _sprinter():
    """The Sprinter's warning, 0.68 s: a dry, fast chitter as it rubs its jaws together (a camel spider's sound),
    rising, and at 0.26 s (where it takes aim) the sharp snick of its jaws cocking; then a hiss that ends as it
    lunges."""
    rng = _design_rng("sprinter")
    length, lock = 0.68, SPRINTER_LOCK
    n = secs(length)
    t = np.arange(n) / SR
    up = np.clip(t / lock, 0, 1)
    let = _until(t, lock, 0.12)
    rate = 48.0 + 46.0 * up
    ticks = _clicks(rate, rng, 0.25) * let
    chitter = _ring(ticks, (2500.0, 3900.0, 5200.0), (0.0025, 0.0018, 0.0012), (1.0, 0.7, 0.45))
    rasp = dsp.bandpass(dsp.white(n, rng), 1800, 6000) * dsp.lowpass(np.abs(ticks), 200) * 40.0
    pre = (chitter + rasp * 0.5) * (0.4 + 0.6 * up) * 0.6
    snick = _knock([(lock, 1.0), (lock + 0.011, 0.55)], n, (3100.0, 4500.0, 6000.0), (0.008, 0.005, 0.003), (1.0, 0.6, 0.35), rng,
                   grit=0.7, grit_hp=3500)
    post = snick * 1.4
    post += _sweep(n, rng, 0.30, 0.62, 0.66, 1500.0, 4200.0, 2.2) * 2.0
    post *= np.minimum((length - t) / 0.01, 1.0)
    return pre, post


@sfx("creature.sprinter.raise", "creature", variants=1, pitchVar=0.15, volumeDb=-5, cooldownMs=WARN_GAP_MS, **WALKER)
def sprinter_raise(rng, v):
    """The Sprinter's wind-up: a dry, fast chitter as it rubs its jaws together (a camel spider's sound), rising."""
    pre, post = _sprinter()
    return _halves(pre, post, SPRINTER_LOCK, SPRINTER_LOCK + 0.12, -4, 300, 0.02, "windup")


@sfx("creature.sprinter.aim", "creature", variants=1, pitchVar=0.15, volumeDb=-5, cooldownMs=WARN_GAP_MS, **WALKER)
def sprinter_aim(rng, v):
    """It takes aim: the sharp snick of its jaws cocking, then a hiss that ends as it lunges. After the snick,
    stepping aside is a miss."""
    pre, post = _sprinter()
    return _halves(pre, post, SPRINTER_LOCK, 0.0, -4, 300, 0.01, "aim")


def _roller():
    """The Roller's warning, 0.9 s: its plates sliding over one another as it curls, rough scrapes coming quicker and
    quicker, and at 0.54 s (where it takes aim) the heavy clunk of the ball snapping shut. Then a low grinding roll as
    it sets off. After the clunk it cannot turn."""
    rng = _design_rng("roller")
    length, lock = 0.9, ROLLER_LOCK
    n = secs(length)
    t = np.arange(n) / SR
    # Plates dragged over plates: uneven, rough and unpitched (never an even run of knocks, which is the clock's).
    plates = []
    at, gap = 0.03, 0.09
    while at < lock - 0.025:
        plates.append((at, 0.45 + 0.55 * at / lock))
        if rng.random() < 0.4:
            plates.append((at + rng.uniform(0.012, 0.026), 0.25 + 0.35 * at / lock))
        at += gap * rng.uniform(0.6, 1.4)
        gap *= 0.82
    pre = _scrapes(n, rng, plates, 1000.0, 3000.0) * 4.5
    # Under the scrapes, a dry rustle of the whole shell shifting.
    pre += dsp.bandpass(dsp.pink(n, rng), 400, 1800) * np.clip(t / lock, 0, 1) * _until(t, lock, 0.08) * 0.25
    # The clunk: every plate home at once, big and hollow, with a thump under it (rung clear of the clock's tick).
    clunk = _knock([(lock, 1.0), (lock + 0.018, 0.45)], n, (205.0, 420.0, 800.0, 1350.0, 2600.0), (0.070, 0.045, 0.026, 0.013, 0.007),
                   (1.0, 0.9, 0.7, 0.45, 0.25), rng, grit=0.45)
    k = secs(0.14)
    thump = dsp.sine(150.0 * np.exp(-np.arange(k) / SR / 0.05) + 70.0, k) * dsp.env_exp(k, 0.05)
    dsp.place(clunk, thump, secs(lock), 3.0)
    post = clunk * 1.35
    # Setting off: grit crunching under a rolling shell.
    roll = np.clip((t - lock - 0.06) / 0.25, 0, 1)
    grains = (rng.random(n) < (160.0 / SR) * roll) * rng.uniform(0.3, 1.0, n)
    crunch = _ring(grains, (850.0, 1750.0, 2900.0), (0.004, 0.003, 0.002), (1.0, 0.7, 0.4))
    rumble = dsp.bandpass(dsp.brown(n, rng), 90, 520) * roll
    post += crunch * 0.5 + rumble * 1.6
    post *= np.minimum((length - t) / 0.04, 1.0)
    return pre, post


@sfx("creature.roller.curl", "creature", variants=1, pitchVar=0.15, volumeDb=-5, cooldownMs=WARN_GAP_MS, **WALKER)
def roller_curl(rng, v):
    """The Roller's wind-up: its plates sliding over one another as it curls, rough scrapes coming quicker and quicker."""
    pre, post = _roller()
    return _halves(pre, post, ROLLER_LOCK, ROLLER_LOCK + 0.08, -4, 70, 0.02, "windup")


@sfx("creature.roller.aim", "creature", variants=1, pitchVar=0.15, volumeDb=-5, cooldownMs=WARN_GAP_MS, **WALKER)
def roller_aim(rng, v):
    """It takes aim: the heavy clunk of the ball snapping shut, then a low grinding roll as it sets off. After the
    clunk it cannot turn: a step aside is a miss."""
    pre, post = _roller()
    return _halves(pre, post, ROLLER_LOCK, 0.0, -4, 70, 0.04, "aim")


def _hunter():
    """The Cave hunter's warning, 0.95 s: the skitter of its many legs as the front of its body lifts, under a dry
    hiss that tightens, and at 0.49 s (where it takes aim) the double click of its fangs flying wide. At 0.75 s the
    whip of its head coming down, gone where it lands."""
    rng = _design_rng("hunter")
    length, lock = 0.95, HUNTER_LOCK
    n = secs(length)
    t = np.arange(n) / SR
    up = np.clip(t / lock, 0, 1)
    legs = _clicks(np.full(n, 110.0) + 40.0 * up, rng, 0.45) * _until(t, lock, 0.2)
    skitter = _ring(legs, (3300.0, 4800.0, 6200.0), (0.0016, 0.0011, 0.0008), (1.0, 0.6, 0.35))
    tight = np.clip(t / 0.75, 0, 1)
    hiss = dsp.svf(dsp.white(n, rng), 900.0 + 2600.0 * tight ** 1.3, 3.0, "bp") * tight * (t < 0.76)
    # The hiss goes on tightening once it has taken aim, so its part after the lock goes with the aim.
    hold = _until(t, lock, 0.06)
    pre = skitter * (0.25 + 0.55 * up) + hiss * 0.8 * hold
    fangs = _knock([(lock, 1.0), (lock + 0.024, 0.85)], n, (2000.0, 3300.0, 4800.0), (0.013, 0.008, 0.005), (1.0, 0.65, 0.4), rng, grit=0.6)
    dsp.place(fangs, _blade(secs(0.09), rng, 2000, 6500), secs(lock + 0.004), 2.0)
    post = fangs * 1.4 + hiss * 0.8 * (1.0 - hold)
    post += _sweep(n, rng, 0.74, 0.90, 0.95, 700.0, 3800.0, 1.7) * 2.5
    post *= np.minimum((length - t) / 0.01, 1.0)
    return pre, post


@sfx("creature.hunter.rear", "creature", variants=1, pitchVar=0.15, volumeDb=-5, cooldownMs=WARN_GAP_MS, **WALKER)
def hunter_rear(rng, v):
    """The Cave hunter's wind-up: the skitter of its many legs as the front of its body lifts, under a dry hiss that
    tightens."""
    pre, post = _hunter()
    return _halves(pre, post, HUNTER_LOCK, HUNTER_LOCK + 0.2, -4, 220, 0.02, "windup")


@sfx("creature.hunter.aim", "creature", variants=1, pitchVar=0.15, volumeDb=-5, cooldownMs=WARN_GAP_MS, **WALKER)
def hunter_aim(rng, v):
    """It takes aim: the double click of its fangs flying wide, the hiss still tightening, then the whip of its head
    coming down, gone where it lands. After the click, stepping aside is a miss."""
    pre, post = _hunter()
    return _halves(pre, post, HUNTER_LOCK, 0.0, -4, 220, 0.01, "aim")


def _maw():
    """The Drifting maw's warning, 1.4 s, heard through water: the slow creak of its jaw as its mouth opens wider and
    wider, a few bubbles getting away, and at 0.84 s (where it takes aim) a deep hollow thoom as the jaw drops fully
    open and water starts to move."""
    rng = _design_rng("maw")
    length, lock = 1.4, MAW_LOCK
    n = secs(length)
    t = np.arange(n) / SR
    up = np.clip(t / lock, 0, 1)
    let = _until(t, lock, 0.2)
    slips = _clicks(9.0 + 16.0 * up, rng, 0.35) * let
    creak = _ring(slips, (230.0, 470.0, 820.0, 1300.0), (0.032, 0.022, 0.013, 0.008), (1.0, 0.8, 0.5, 0.3))
    groan = dsp.svf(dsp.pink(n, rng), 300.0 + 350.0 * up, 4.0, "bp") * (0.3 + 0.7 * up) * let
    pre = creak * (0.4 + 0.6 * up) * 0.4 + groan * 2.0
    pre += _bubbles(n, rng, sorted(rng.uniform(0.12, lock - 0.05, 7)), 380.0, 1100.0, 1.2) * 1.5
    # The thoom: the jaw falling open, felt and heard (its upper ring is for small speakers).
    k = secs(0.45)
    tt = np.arange(k) / SR
    thoom = (np.sin(dsp.TAU * np.cumsum(92.0 * np.exp(-tt / 0.3) + 64.0) / SR) * np.exp(-tt / 0.16)
             + 0.6 * np.sin(dsp.TAU * np.cumsum(276.0 * np.exp(-tt / 0.3) + 190.0) / SR) * np.exp(-tt / 0.09)
             + 0.35 * np.sin(dsp.TAU * 610.0 * tt) * np.exp(-tt / 0.04))
    thoom[: secs(0.003)] *= np.linspace(0, 1, secs(0.003))
    post = np.zeros(n)
    dsp.place(post, thoom, secs(lock), 4.0)
    post += _knock([(lock, 1.0)], n, (310.0, 760.0, 1300.0), (0.05, 0.03, 0.016), (1.0, 0.7, 0.4), rng, grit=0.15, grit_hp=1200) * 0.7
    post += _bubbles(n, rng, sorted(lock + rng.exponential(0.07, 14)), 320.0, 1500.0) * 1.5
    # The water beginning to move: the draw-in follows on its own sound.
    post += _sweep(n, rng, lock + 0.05, length - 0.05, length, 260.0, 700.0, 1.2, "pink") * 6.0
    pre, post = dsp.lowpass(pre, 3000), dsp.lowpass(post, 3000)
    post *= np.minimum((length - t) / 0.03, 1.0)
    return pre, post


@sfx("creature.maw.gape", "creature", variants=1, pitchVar=0.1, volumeDb=-4, cooldownMs=WARN_GAP_MS, **SWIMMER)
def maw_gape(rng, v):
    """The Drifting maw's wind-up, heard through water: the slow creak of its jaw as its mouth opens wider and wider,
    and a few bubbles getting away."""
    pre, post = _maw()
    return _halves(pre, post, MAW_LOCK, MAW_LOCK + 0.2, -4, 60, 0.03, "windup")


@sfx("creature.maw.aim", "creature", variants=1, pitchVar=0.1, volumeDb=-4, cooldownMs=WARN_GAP_MS, **SWIMMER)
def maw_aim(rng, v):
    """It takes aim: a deep hollow thoom as its jaw drops fully open, and the water starting to move. After the thoom
    it hardly turns: swimming sideways is the way out."""
    pre, post = _maw()
    return _halves(pre, post, MAW_LOCK, 0.0, -4, 60, 0.03, "aim")


@sfx("creature.maw.inhale", "creature", variants=1, pitchVar=0.05, volumeDb=-4, cooldownMs=400, **SWIMMER)
def maw_inhale(rng, v):
    """It draws in, 2.6 s: a roaring rush of water into its mouth that swells and climbs, so its rising tells how
    long it has left, and stops dead at the end."""
    length = 2.6
    n = secs(length)
    t = np.arange(n) / SR
    climb = np.clip(t / length, 0, 1)
    src = dsp.pink(n, rng) * 0.7 + dsp.white(n, rng) * 0.3
    rush = dsp.svf(src, 240.0 + 1100.0 * climb ** 1.2, 1.3, "bp")
    churn = 1.0 + 0.45 * dsp.smooth_noise(n, 7.0, rng) + 0.2 * np.sin(dsp.TAU * 3.1 * t)
    swell = np.clip(t / 0.35, 0, 1) ** 1.5 * (0.7 + 0.3 * climb)
    body = dsp.bandpass(dsp.brown(n, rng), 70, 300) * swell
    x = rush * churn * swell + body * 0.8
    x += _bubbles(n, rng, sorted(rng.uniform(0.2, length - 0.2, 30)), 300.0, 1300.0) * 0.25
    x = dsp.lowpass(x, 3400)
    x *= np.minimum((length - t) / 0.08, 1.0)
    return finish(x, -5, 0.08, hp=50)


@sfx("creature.maw.choke", "creature", variants=2, pitchVar=0.15, volumeDb=-4, cooldownMs=300, **SWIMMER)
def maw_choke(rng, v):
    """It chokes (a shot down its open throat): two hard gags, and the water it drew in coughed back out as a
    burst of bubbles."""
    length = 0.75
    n = secs(length)
    x = np.zeros(n)
    for when, gain in ((0.0, 1.0), (0.17 + 0.03 * v, 0.8)):
        k = secs(0.16)
        tt = np.arange(k) / SR
        gag = np.sin(dsp.TAU * np.cumsum(150.0 * np.exp(-tt / 0.05) + 85.0) / SR) * np.exp(-tt / 0.06)
        hk = dsp.bandpass(dsp.white(k, rng), 300, 1600) * np.exp(-tt / 0.03)
        dsp.place(x, gag * 0.9 + hk * 0.8, secs(when + 0.005), gain)
    x += _bubbles(n, rng, sorted(0.2 + rng.exponential(0.12, 26)), 300.0, 1700.0) * 0.6
    x = dsp.lowpass(x, 3000)
    return finish(x, -4, 0.05, hp=60)


def _biter():
    """A Pack biter's warning, 0.9 s, heard through water: its teeth chattering as its jaws flare, a swish as it rolls
    its silver flank to the light, and at 0.54 s (where it takes aim) the hard crack of its jaws setting and its fins
    sweeping back; then a held breath before the dart."""
    rng = _design_rng("biter")
    length, lock = 0.9, BITER_LOCK
    n = secs(length)
    t = np.arange(n) / SR
    up = np.clip(t / lock, 0, 1)
    teeth = _clicks(26.0 + 18.0 * up, rng, 0.2) * _until(t, lock, 0.1)
    chatter = _ring(teeth, (2050.0, 3300.0, 4700.0), (0.004, 0.0028, 0.0018), (1.0, 0.6, 0.35))
    pre = chatter * (0.35 + 0.55 * up) * 0.55
    pre += _sweep(n, rng, 0.10, 0.28, 0.46, 420.0, 1500.0, 1.4) * 0.6
    # The jaws setting: a broadband crack with no ring to give it a pitch (a ringing tock sounded like the clock),
    # teeth meeting a moment after the jaw.
    post = _crack(n, [(lock, 1.0), (lock + 0.0016, 0.8), (lock + 0.0042, 0.6), (lock + 0.009, 0.45)], rng, tau=0.0019) * 10.0
    post += _sweep(n, rng, lock + 0.01, lock + 0.06, lock + 0.17, 1300.0, 600.0, 1.5) * 1.0
    # Then a held breath before the dart: a thin hiss rising to the end.
    post += _sweep(n, rng, lock + 0.08, length - 0.02, length, 1800.0, 3200.0, 2.5) * 0.5
    pre, post = dsp.lowpass(pre, 5500), dsp.lowpass(post, 5500)
    post *= np.minimum((length - t) / 0.02, 1.0)
    return pre, post


@sfx("creature.biter.mark", "creature", variants=1, pitchVar=0.15, volumeDb=-6, cooldownMs=WARN_GAP_MS, **SWIMMER)
def biter_mark(rng, v):
    """A Pack biter's wind-up, heard through water: its teeth chattering as its jaws flare, and a swish as it rolls its
    silver flank to the light."""
    pre, post = _biter()
    return _halves(pre, post, BITER_LOCK, BITER_LOCK + 0.1, -4, 200, 0.02, "windup")


@sfx("creature.biter.aim", "creature", variants=1, pitchVar=0.15, volumeDb=-6, cooldownMs=WARN_GAP_MS, **SWIMMER)
def biter_aim(rng, v):
    """It takes aim: the hard crack of its jaws setting and its fins sweeping back, then a held breath before the dart.
    After the crack, turning back is the dodge."""
    pre, post = _biter()
    return _halves(pre, post, BITER_LOCK, 0.0, -4, 200, 0.02, "aim")


@sfx("creature.biter.dart", "creature", variants=2, pitchVar=0.2, volumeDb=-4, cooldownMs=150, **SWIMMER)
def biter_dart(rng, v):
    """The dart: a fish the size of a man ripping through the water past you, and its jaws snapping shut as it
    goes by."""
    length = 0.36
    n = secs(length)
    x = _sweep(n, rng, 0.0, 0.07, 0.24, 2600.0, 550.0, 1.4) * 3.0
    snap = _knock([(0.15 + 0.02 * v, 1.0), (0.162 + 0.02 * v, 0.5)], n, (1350.0, 2550.0, 4000.0), (0.012, 0.007, 0.004), (1.0, 0.6, 0.35), rng,
                  grit=0.6)
    x += snap * 1.3
    x = dsp.lowpass(x, 4800)
    return finish(x, -3, 0.03, hp=150)
