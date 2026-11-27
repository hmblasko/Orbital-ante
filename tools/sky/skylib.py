"""Offline prototype of a realistic sky for Orbital Ante previews. Not game code."""
import numpy as np

F = np.float32


def _hash(ix, iy, iz, seed):
    h = (ix.astype(np.uint32) * np.uint32(0x8DA6B343)) ^ (iy.astype(np.uint32) * np.uint32(0xD8163841)) \
        ^ (iz.astype(np.uint32) * np.uint32(0xCB1AB31F)) ^ np.uint32(seed * 0x9E3779B1 & 0xFFFFFFFF)
    h ^= h >> np.uint32(15)
    h *= np.uint32(0x2C1B3C6D)
    h ^= h >> np.uint32(12)
    h *= np.uint32(0x297A2D39)
    h ^= h >> np.uint32(15)
    return (h & np.uint32(0xFFFFFF)).astype(F) / F(8388607.5) - F(1.0)


def vnoise(x, y, z, seed):
    x0 = np.floor(x); y0 = np.floor(y); z0 = np.floor(z)
    fx = (x - x0).astype(F); fy = (y - y0).astype(F); fz = (z - z0).astype(F)
    ix = x0.astype(np.int64); iy = y0.astype(np.int64); iz = z0.astype(np.int64)
    ux = fx * fx * fx * (fx * (fx * 6 - 15) + 10)
    uy = fy * fy * fy * (fy * (fy * 6 - 15) + 10)
    uz = fz * fz * fz * (fz * (fz * 6 - 15) + 10)
    out = 0
    for dz in (0, 1):
        wz = uz if dz else 1 - uz
        for dy in (0, 1):
            wy = uy if dy else 1 - uy
            a = _hash(ix, iy + dy, iz + dz, seed)
            b = _hash(ix + 1, iy + dy, iz + dz, seed)
            out = out + (a + (b - a) * ux) * wy * wz
    return out


def fbm(x, y, z, seed, octaves=5, gain=0.5, lac=2.03):
    amp = 1.0; tot = 0.0; out = 0
    for o in range(octaves):
        out = out + amp * vnoise(x, y, z, seed + o * 17)
        tot += amp
        amp *= gain
        x = x * lac + 11.3; y = y * lac + 5.7; z = z * lac + 3.1
    return out / tot


def ridged(x, y, z, seed, octaves=5, gain=0.5, lac=2.03):
    amp = 1.0; tot = 0.0; out = 0
    for o in range(octaves):
        out = out + amp * (1 - np.abs(vnoise(x, y, z, seed + o * 17)))
        tot += amp
        amp *= gain
        x = x * lac + 11.3; y = y * lac + 5.7; z = z * lac + 3.1
    return out / tot


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def dirs_equirect(W, H):
    """Galactic directions; l runs left to right from +180 to -180 (as sky maps are drawn), b top +90."""
    l = (0.5 - (np.arange(W, dtype=F) + 0.5) / W) * 2 * np.pi
    b = (0.5 - (np.arange(H, dtype=F) + 0.5) / H) * np.pi
    L, B = np.meshgrid(l, b)
    return np.cos(B) * np.cos(L), np.cos(B) * np.sin(L), np.sin(B), L, B


def lb_to_dir(l_deg, b_deg):
    l = np.radians(l_deg); b = np.radians(b_deg)
    return np.array([np.cos(b) * np.cos(l), np.cos(b) * np.sin(l), np.sin(b)])


def dir_to_uv(dx, dy, dz, W, H):
    l = np.arctan2(dy, dx); b = np.arcsin(np.clip(dz, -1, 1))
    u = (0.5 - l / (2 * np.pi)) * W - 0.5
    v = (0.5 - b / np.pi) * H - 0.5
    return u, v


# equatorial J2000 -> galactic rotation
EQ2GAL = np.array([[-0.0548755604, -0.8734370902, -0.4838350155],
                   [0.4941094279, -0.4448296300, 0.7469822445],
                   [-0.8676661490, -0.1980763734, 0.4559837762]])


def bv_to_rgb(bv):
    """Rough star colour from B-V via temperature, linear RGB, normalised to green."""
    bv = np.clip(bv, -0.4, 2.0)
    t = 4600 * (1 / (0.92 * bv + 1.7) + 1 / (0.92 * bv + 0.62))
    # fit of blackbody chromaticity
    x = t / 100.0
    r = np.where(x <= 66, 255, 329.698727446 * np.power(np.maximum(x - 60, 1e-3), -0.1332047592))
    g = np.where(x <= 66, 99.4708025861 * np.log(x) - 161.1195681661,
                 288.1221695283 * np.power(np.maximum(x - 60, 1e-3), -0.0755148492))
    b = np.where(x >= 66, 255, np.where(x <= 19, 0, 138.5177312231 * np.log(np.maximum(x - 10, 1e-3)) - 305.0447927307))
    rgb = np.clip(np.stack([r, g, b], -1) / 255.0, 0, 1) ** 2.2
    rgb /= rgb.mean(-1, keepdims=True)
    return rgb.astype(F)
