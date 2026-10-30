//! In-game half of the verification layer (CLAUDE.md section 19):
//! autoplay bots, automatic detectors, input replay recording and the
//! screenshot harness. The offline half (screenshot comparison, report
//! dashboard, bug filing) is the `oa_verify` tool in rust/verify.
//!
//! Serves priority 2 (stability) and every pillar indirectly: nothing is
//! "done" until it passes this layer.

pub mod bot;
pub mod detectors;
pub mod harness;
pub mod replay;
pub mod results;

pub use harness::{Harness, HarnessMode};
