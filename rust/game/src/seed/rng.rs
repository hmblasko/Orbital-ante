//! Deterministic random numbers. Written by hand (SplitMix64 + xoshiro256**)
//! so results are identical on every platform and never change when a
//! dependency updates. Changing this file changes every seed's world, so the
//! golden-value test below must be updated on purpose, never casually.

/// SplitMix64 step: turns any 64-bit state into a well-mixed 64-bit value.
pub fn splitmix64(state: &mut u64) -> u64 {
    *state = state.wrapping_add(0x9E37_79B9_7F4A_7C15);
    let mut z = *state;
    z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
    z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
    z ^ (z >> 31)
}

/// FNV-1a hash of a system name, so "procgen" and "loot" get different streams.
pub fn hash_name(name: &str) -> u64 {
    name.bytes().fold(0xCBF2_9CE4_8422_2325, |h, b| (h ^ b as u64).wrapping_mul(0x0100_0000_01B3))
}

/// A random stream for one system. Cheap to create; derive a fresh one
/// instead of sharing one between systems.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SeededRng {
    s: [u64; 4],
}

impl SeededRng {
    pub fn from_u64(seed: u64) -> Self {
        let mut state = seed;
        let s = [
            splitmix64(&mut state),
            splitmix64(&mut state),
            splitmix64(&mut state),
            splitmix64(&mut state),
        ];
        Self { s }
    }

    /// The stream for `system` in the run with master seed `run_seed`.
    pub fn derive(run_seed: u64, system: &str) -> Self {
        let mut state = run_seed ^ hash_name(system);
        Self::from_u64(splitmix64(&mut state))
    }

    /// A numbered sub-stream, for example one per ante or per body.
    pub fn derive_indexed(run_seed: u64, system: &str, index: u64) -> Self {
        let mut state = run_seed ^ hash_name(system) ^ index.wrapping_mul(0xD6E8_FEB8_6659_FD93);
        Self::from_u64(splitmix64(&mut state))
    }

    pub fn next_u64(&mut self) -> u64 {
        let result = self.s[1].wrapping_mul(5).rotate_left(7).wrapping_mul(9);
        let t = self.s[1] << 17;
        self.s[2] ^= self.s[0];
        self.s[3] ^= self.s[1];
        self.s[1] ^= self.s[2];
        self.s[0] ^= self.s[3];
        self.s[2] ^= t;
        self.s[3] = self.s[3].rotate_left(45);
        result
    }

    /// Uniform in [0, 1).
    pub fn next_f32(&mut self) -> f32 {
        (self.next_u64() >> 40) as f32 / (1u64 << 24) as f32
    }

    /// Uniform in [0, 1), double precision.
    pub fn next_f64(&mut self) -> f64 {
        (self.next_u64() >> 11) as f64 / (1u64 << 53) as f64
    }

    /// Uniform integer in [0, bound) without modulo bias. `bound` must be > 0.
    pub fn below(&mut self, bound: u64) -> u64 {
        assert!(bound > 0, "SeededRng::below needs a positive bound");
        let zone = u64::MAX - u64::MAX % bound;
        loop {
            let x = self.next_u64();
            if x < zone {
                return x % bound;
            }
        }
    }

    /// Uniform integer in [low, high] inclusive.
    pub fn range_i64(&mut self, low: i64, high: i64) -> i64 {
        assert!(low <= high, "SeededRng::range_i64 needs low <= high");
        let span = (high - low) as u64 + 1;
        low + self.below(span) as i64
    }

    pub fn range_f32(&mut self, low: f32, high: f32) -> f32 {
        low + (high - low) * self.next_f32()
    }

    /// True with probability `p` (0..=1).
    pub fn chance(&mut self, p: f64) -> bool {
        self.next_f64() < p
    }

    pub fn pick<'a, T>(&mut self, items: &'a [T]) -> Option<&'a T> {
        if items.is_empty() {
            None
        } else {
            Some(&items[self.below(items.len() as u64) as usize])
        }
    }

    /// Index chosen with probability proportional to its weight.
    /// Returns None if the weights are empty or all zero.
    pub fn weighted_index(&mut self, weights: &[f64]) -> Option<usize> {
        let total: f64 = weights.iter().filter(|w| **w > 0.0).sum();
        if total <= 0.0 {
            return None;
        }
        let mut roll = self.next_f64() * total;
        for (i, w) in weights.iter().enumerate() {
            if *w <= 0.0 {
                continue;
            }
            if roll < *w {
                return Some(i);
            }
            roll -= w;
        }
        weights.iter().rposition(|w| *w > 0.0)
    }

    /// Fisher-Yates shuffle.
    pub fn shuffle<T>(&mut self, items: &mut [T]) {
        for i in (1..items.len()).rev() {
            let j = self.below(i as u64 + 1) as usize;
            items.swap(i, j);
        }
    }
}

/// Determinism tests (CLAUDE.md section 14): same seed, same results.
#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn same_seed_same_stream() {
        let mut a = SeededRng::derive(42, "procgen");
        let mut b = SeededRng::derive(42, "procgen");
        for _ in 0..1000 {
            assert_eq!(a.next_u64(), b.next_u64());
        }
    }

    #[test]
    fn systems_get_different_streams() {
        let mut a = SeededRng::derive(42, "procgen");
        let mut b = SeededRng::derive(42, "loot");
        let same = (0..100).filter(|_| a.next_u64() == b.next_u64()).count();
        assert_eq!(same, 0);
    }

    #[test]
    fn indexed_streams_differ() {
        let mut a = SeededRng::derive_indexed(42, "ante", 1);
        let mut b = SeededRng::derive_indexed(42, "ante", 2);
        assert_ne!(a.next_u64(), b.next_u64());
    }

    /// Pinned output. If this fails, every existing seed now builds a
    /// different world: only update it on purpose and say so in the report.
    #[test]
    fn golden_values() {
        let mut rng = SeededRng::derive(12345, "procgen");
        let got: Vec<u64> = (0..3).map(|_| rng.next_u64()).collect();
        assert_eq!(got, GOLDEN);
    }
    const GOLDEN: [u64; 3] = [2672281725075705521, 10481948829387592649, 1309160218682536240];

    /// Reference outputs of xoshiro256** for state [1, 2, 3, 4].
    #[test]
    fn matches_reference_xoshiro() {
        let mut rng = SeededRng { s: [1, 2, 3, 4] };
        let got: Vec<u64> = (0..4).map(|_| rng.next_u64()).collect();
        assert_eq!(got, [11520, 0, 1509978240, 1215971899390074240]);
    }

    #[test]
    fn ranges_stay_in_bounds() {
        let mut rng = SeededRng::from_u64(7);
        for _ in 0..10_000 {
            let f = rng.next_f32();
            assert!((0.0..1.0).contains(&f));
            let i = rng.range_i64(-3, 3);
            assert!((-3..=3).contains(&i));
        }
    }

    #[test]
    fn below_is_roughly_uniform() {
        let mut rng = SeededRng::from_u64(99);
        let mut counts = [0u32; 6];
        for _ in 0..60_000 {
            counts[rng.below(6) as usize] += 1;
        }
        for c in counts {
            assert!((9_000..11_000).contains(&c), "{counts:?}");
        }
    }

    #[test]
    fn weighted_index_respects_weights() {
        let mut rng = SeededRng::from_u64(5);
        let weights = [55.0, 25.0, 13.0, 6.0, 1.0];
        let mut counts = [0u32; 5];
        for _ in 0..100_000 {
            counts[rng.weighted_index(&weights).unwrap()] += 1;
        }
        // Within 1 percentage point of the stated odds.
        for (c, w) in counts.iter().zip(weights) {
            let pct = *c as f64 / 1000.0;
            assert!((pct - w).abs() < 1.0, "{counts:?}");
        }
        assert_eq!(rng.weighted_index(&[0.0, 0.0]), None);
    }

    #[test]
    fn shuffle_is_deterministic_permutation() {
        let mut a: Vec<u32> = (0..20).collect();
        let mut b = a.clone();
        SeededRng::from_u64(3).shuffle(&mut a);
        SeededRng::from_u64(3).shuffle(&mut b);
        assert_eq!(a, b);
        let mut sorted = a.clone();
        sorted.sort();
        assert_eq!(sorted, (0..20).collect::<Vec<_>>());
    }
}
