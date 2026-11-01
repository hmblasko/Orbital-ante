//! Ship flight: the shared 6-axis flight model, ships, and their interiors
//! (CLAUDE.md section 5).

pub mod breakup;
pub mod cockpit;
pub mod damage;
pub mod dash;
pub mod meshes;
pub mod model;
pub mod rover;
pub mod ship;
pub mod takeoff;

pub use ship::Ship;
