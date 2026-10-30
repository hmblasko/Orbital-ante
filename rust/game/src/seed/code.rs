//! Seeds as short, shareable codes, such as "7K3M-Q9TD".
//!
//! Crockford base32: no I, L, O or U, so codes are hard to misread, and
//! lowercase or the look-alike letters are accepted when typed.

const ALPHABET: &[u8; 32] = b"0123456789ABCDEFGHJKMNPQRSTVWXYZ";
/// Seeds have 40 bits: 8 characters, about a trillion possible runs.
pub const SEED_BITS: u32 = 40;
const CODE_CHARS: usize = 8;

/// Keep only the bits a code can hold.
pub fn clamp_seed(seed: u64) -> u64 {
    seed & ((1u64 << SEED_BITS) - 1)
}

/// "XXXX-XXXX" code for a seed.
pub fn encode_seed(seed: u64) -> String {
    let seed = clamp_seed(seed);
    let mut out = String::with_capacity(CODE_CHARS + 1);
    for i in (0..CODE_CHARS).rev() {
        let digit = (seed >> (i * 5)) & 31;
        out.push(ALPHABET[digit as usize] as char);
        if i == CODE_CHARS / 2 {
            out.push('-');
        }
    }
    out
}

/// Parse a code typed by a player. Ignores dashes, spaces and case.
pub fn decode_seed(code: &str) -> Option<u64> {
    let mut value: u64 = 0;
    let mut count = 0;
    for c in code.chars() {
        if c == '-' || c.is_whitespace() {
            continue;
        }
        let c = match c.to_ascii_uppercase() {
            'O' => '0',
            'I' | 'L' => '1',
            other => other,
        };
        let digit = ALPHABET.iter().position(|a| *a as char == c)? as u64;
        value = (value << 5) | digit;
        count += 1;
    }
    (count == CODE_CHARS).then_some(value)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn round_trip() {
        for seed in [0, 1, 12345, (1u64 << 40) - 1, 0xDEAD_BEEF] {
            assert_eq!(decode_seed(&encode_seed(seed)), Some(clamp_seed(seed)));
        }
    }

    #[test]
    fn format_and_forgiving_input() {
        let code = encode_seed(0xDEAD_BEEF);
        assert_eq!(code.len(), 9);
        assert_eq!(&code[4..5], "-");
        let typed = code.to_lowercase().replace('-', " ");
        assert_eq!(decode_seed(&typed), Some(0xDEAD_BEEF));
        assert_eq!(decode_seed("o0o0-o0o1"), Some(1));
    }

    #[test]
    fn rejects_bad_codes() {
        assert_eq!(decode_seed("ABC"), None);
        assert_eq!(decode_seed("ABCD-EFGU"), None);
    }
}
