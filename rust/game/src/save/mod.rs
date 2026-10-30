//! Versioned saves (CLAUDE.md section 14): atomic writes, the last 3
//! autosaves kept, migration of old versions, and fallback to an older save
//! if the newest one is damaged.
//!
//! Serves priority 2 (stability: no save loss) and pillar 3 (every run counts:
//! meta-progression must never be lost).

pub mod format;
pub mod singleton;
pub mod store;

pub use format::{CURRENT_VERSION, SaveFile};
pub use singleton::SaveData;
pub use store::{SaveSettings, SaveStore};
