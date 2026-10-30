//! Deterministic 3D gradient noise and fractal sums, written by hand so a
//! seed gives the same terrain on every platform and Godot version.

/// Hash three lattice coordinates and a seed into 32 bits.
fn hash(x: i32, y: i32, z: i32, seed: u32) -> u32 {
    let mut h = seed ^ 0x9E37_79B9;
    for v in [x as u32, y as u32, z as u32] {
        h ^= v.wrapping_mul(0x85EB_CA6B);
        h = h.rotate_left(13).wrapping_mul(0xC2B2_AE35);
    }
    h ^= h >> 16;
    h.wrapping_mul(0x7FEB_352D) ^ (h >> 15)
}

/// One of 12 cube-edge gradient directions, dotted with the offset.
fn grad(h: u32, x: f32, y: f32, z: f32) -> f32 {
    match h % 12 {
        0 => x + y,
        1 => -x + y,
        2 => x - y,
        3 => -x - y,
        4 => x + z,
        5 => -x + z,
        6 => x - z,
        7 => -x - z,
        8 => y + z,
        9 => -y + z,
        10 => y - z,
        _ => -y - z,
    }
}

fn fade(t: f32) -> f32 {
    t * t * t * (t * (t * 6.0 - 15.0) + 10.0)
}

fn lerp(a: f32, b: f32, t: f32) -> f32 {
    a + (b - a) * t
}

/// Perlin-style gradient noise, roughly in -1..1.
pub fn gradient3(x: f32, y: f32, z: f32, seed: u32) -> f32 {
    let (xi, yi, zi) = (x.floor() as i32, y.floor() as i32, z.floor() as i32);
    let (xf, yf, zf) = (x - xi as f32, y - yi as f32, z - zi as f32);
    let (u, v, w) = (fade(xf), fade(yf), fade(zf));
    let c = |dx: i32, dy: i32, dz: i32| grad(hash(xi + dx, yi + dy, zi + dz, seed), xf - dx as f32, yf - dy as f32, zf - dz as f32);
    let x00 = lerp(c(0, 0, 0), c(1, 0, 0), u);
    let x10 = lerp(c(0, 1, 0), c(1, 1, 0), u);
    let x01 = lerp(c(0, 0, 1), c(1, 0, 1), u);
    let x11 = lerp(c(0, 1, 1), c(1, 1, 1), u);
    lerp(lerp(x00, x10, v), lerp(x01, x11, v), w)
}

/// Fractal sum of `octaves` layers, each twice the frequency and `gain` the
/// amplitude of the last. Normalised to roughly -1..1.
pub fn fbm3(p: [f32; 3], octaves: u32, gain: f32, seed: u32) -> f32 {
    let (mut sum, mut amp, mut freq, mut norm) = (0.0, 1.0, 1.0, 0.0);
    for i in 0..octaves {
        sum += amp * gradient3(p[0] * freq, p[1] * freq, p[2] * freq, seed.wrapping_add(i * 101));
        norm += amp;
        amp *= gain;
        freq *= 2.0;
    }
    if norm > 0.0 { sum / norm } else { 0.0 }
}

/// Ridged fractal: sharp crests, good for rocky hills.
pub fn ridged3(p: [f32; 3], octaves: u32, gain: f32, seed: u32) -> f32 {
    let (mut sum, mut amp, mut freq, mut norm) = (0.0, 1.0, 1.0, 0.0);
    for i in 0..octaves {
        let n = 1.0 - gradient3(p[0] * freq, p[1] * freq, p[2] * freq, seed.wrapping_add(i * 131)).abs();
        sum += amp * n * n;
        norm += amp;
        amp *= gain;
        freq *= 2.0;
    }
    if norm > 0.0 { sum / norm } else { 0.0 }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn determinism_noise_is_pure() {
        assert_eq!(gradient3(1.3, -2.7, 0.4, 7), gradient3(1.3, -2.7, 0.4, 7));
        assert_ne!(gradient3(1.3, -2.7, 0.4, 7), gradient3(1.3, -2.7, 0.4, 8));
    }

    #[test]
    fn noise_is_bounded_and_zero_on_lattice() {
        assert_eq!(gradient3(3.0, 4.0, -5.0, 1), 0.0);
        for i in 0..2000 {
            let t = i as f32 * 0.137;
            let n = fbm3([t, t * 0.7 - 3.0, -t * 1.3], 5, 0.5, 3);
            assert!((-1.01..=1.01).contains(&n), "{n}");
            let r = ridged3([t, t * 0.7 - 3.0, -t * 1.3], 4, 0.5, 3);
            assert!((0.0..=1.01).contains(&r), "{r}");
        }
    }
}
