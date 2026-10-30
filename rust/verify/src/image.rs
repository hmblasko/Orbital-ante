//! Screenshot comparison (SSIM) and automatic image checks (section 19.3).

use std::fs::File;
use std::path::Path;

pub struct Rgb {
    pub width: usize,
    pub height: usize,
    /// 8-bit RGB, row-major.
    pub pixels: Vec<[u8; 3]>,
}

pub fn load_png(path: &Path) -> Result<Rgb, String> {
    let decoder = png::Decoder::new(std::io::BufReader::new(
        File::open(path).map_err(|e| e.to_string())?,
    ));
    let mut reader = decoder.read_info().map_err(|e| e.to_string())?;
    let mut buf = vec![0; reader.output_buffer_size().ok_or("image too large")?];
    let info = reader.next_frame(&mut buf).map_err(|e| e.to_string())?;
    if info.bit_depth != png::BitDepth::Eight {
        return Err("only 8-bit PNGs are supported".into());
    }
    let channels = match info.color_type {
        png::ColorType::Rgb => 3,
        png::ColorType::Rgba => 4,
        other => return Err(format!("unsupported PNG colour type {other:?}")),
    };
    let pixels = buf[..info.buffer_size()]
        .chunks_exact(channels)
        .map(|c| [c[0], c[1], c[2]])
        .collect();
    Ok(Rgb {
        width: info.width as usize,
        height: info.height as usize,
        pixels,
    })
}

fn luma(p: [u8; 3]) -> f64 {
    0.299 * p[0] as f64 + 0.587 * p[1] as f64 + 0.114 * p[2] as f64
}

/// Mean structural similarity over 8x8 blocks of luma. 1.0 = identical.
pub fn ssim(a: &Rgb, b: &Rgb) -> Result<f64, String> {
    if a.width != b.width || a.height != b.height {
        return Err(format!(
            "sizes differ: {}x{} vs {}x{}",
            a.width, a.height, b.width, b.height
        ));
    }
    const BLOCK: usize = 8;
    const C1: f64 = (0.01 * 255.0) * (0.01 * 255.0);
    const C2: f64 = (0.03 * 255.0) * (0.03 * 255.0);
    let mut total = 0.0;
    let mut blocks = 0usize;
    for by in (0..a.height.saturating_sub(BLOCK - 1)).step_by(BLOCK) {
        for bx in (0..a.width.saturating_sub(BLOCK - 1)).step_by(BLOCK) {
            let (mut sa, mut sb, mut saa, mut sbb, mut sab) = (0.0, 0.0, 0.0, 0.0, 0.0);
            for y in by..by + BLOCK {
                for x in bx..bx + BLOCK {
                    let i = y * a.width + x;
                    let (la, lb) = (luma(a.pixels[i]), luma(b.pixels[i]));
                    sa += la;
                    sb += lb;
                    saa += la * la;
                    sbb += lb * lb;
                    sab += la * lb;
                }
            }
            let n = (BLOCK * BLOCK) as f64;
            let (ma, mb) = (sa / n, sb / n);
            let va = saa / n - ma * ma;
            let vb = sbb / n - mb * mb;
            let cov = sab / n - ma * mb;
            total += ((2.0 * ma * mb + C1) * (2.0 * cov + C2))
                / ((ma * ma + mb * mb + C1) * (va + vb + C2));
            blocks += 1;
        }
    }
    if blocks == 0 {
        return Err("image smaller than one block".into());
    }
    Ok(total / blocks as f64)
}

/// Problems that need no baseline: missing-texture magenta, blank frames.
pub fn automatic_checks(img: &Rgb) -> Vec<String> {
    let mut problems = Vec::new();
    let n = img.pixels.len().max(1) as f64;
    let magenta = img
        .pixels
        .iter()
        .filter(|p| p[0] > 230 && p[1] < 30 && p[2] > 230)
        .count() as f64;
    if magenta / n > 0.005 {
        problems.push(format!(
            "{:.1}% magenta pixels (missing texture?)",
            100.0 * magenta / n
        ));
    }
    let lumas: Vec<f64> = img.pixels.iter().map(|p| luma(*p)).collect();
    let mean = lumas.iter().sum::<f64>() / n;
    let std = (lumas.iter().map(|l| (l - mean).powi(2)).sum::<f64>() / n).sqrt();
    if std < 2.0 && mean < 8.0 {
        problems.push("frame is fully black".into());
    } else if std < 2.0 && mean > 247.0 {
        problems.push("frame is fully white".into());
    } else if std < 1.0 {
        problems.push("frame is a single flat colour".into());
    }
    problems
}

#[cfg(test)]
mod tests {
    use super::*;

    fn flat(w: usize, h: usize, c: [u8; 3]) -> Rgb {
        Rgb {
            width: w,
            height: h,
            pixels: vec![c; w * h],
        }
    }

    fn gradient(w: usize, h: usize) -> Rgb {
        let pixels = (0..w * h).map(|i| {
            let v = ((i % w) * 255 / w) as u8;
            [v, v / 2, 255 - v]
        });
        Rgb {
            width: w,
            height: h,
            pixels: pixels.collect(),
        }
    }

    #[test]
    fn identical_images_score_one() {
        let g = gradient(64, 32);
        assert!((ssim(&g, &g).unwrap() - 1.0).abs() < 1e-9);
    }

    #[test]
    fn different_images_score_lower() {
        let g = gradient(64, 32);
        let mut changed = gradient(64, 32);
        for p in changed.pixels.iter_mut().take(64 * 16) {
            *p = [0, 0, 0];
        }
        assert!(ssim(&g, &changed).unwrap() < 0.98);
    }

    #[test]
    fn size_mismatch_is_an_error() {
        assert!(ssim(&gradient(64, 32), &gradient(32, 32)).is_err());
    }

    #[test]
    fn checks_catch_black_white_and_magenta() {
        assert!(automatic_checks(&flat(16, 16, [0, 0, 0]))[0].contains("black"));
        assert!(automatic_checks(&flat(16, 16, [255, 255, 255]))[0].contains("white"));
        assert!(
            automatic_checks(&flat(16, 16, [255, 0, 255]))
                .iter()
                .any(|p| p.contains("magenta"))
        );
        assert!(automatic_checks(&gradient(64, 32)).is_empty());
    }
}
