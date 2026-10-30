//! Command-line options, passed after `--` when launching Godot, for example
//! `godot --path . -- --smoke --seed=7K3M-Q9TD --out=/tmp/run`.

#[derive(Debug, Clone, Default, PartialEq)]
pub struct LaunchArgs {
    /// Seed code to use instead of a random one.
    pub seed: Option<String>,
    /// Run the autoplay smoke test and quit.
    pub smoke: bool,
    /// Game seconds to run the smoke test for (default from data).
    pub duration_s: Option<f32>,
    /// Bot profile for the smoke test.
    pub bot: Option<String>,
    /// Replay this recorded input file and check the result matches.
    pub replay: Option<String>,
    /// Record the player's input to this file (normal play).
    pub record: Option<String>,
    /// Capture screenshots from fixed viewpoints and quit.
    pub screenshots: bool,
    /// Folder for test results, replays and screenshots.
    pub out: Option<String>,
}

impl LaunchArgs {
    pub fn parse<S: AsRef<str>>(args: &[S]) -> Result<Self, String> {
        let mut out = LaunchArgs::default();
        for raw in args {
            let raw = raw.as_ref();
            let (key, value) = match raw.split_once('=') {
                Some((k, v)) => (k, Some(v.to_string())),
                None => (raw, None),
            };
            let need = |v: Option<String>| v.ok_or(format!("{key} needs a value, like {key}=..."));
            match key {
                "--seed" => out.seed = Some(need(value)?),
                "--smoke" => out.smoke = true,
                "--duration" => {
                    out.duration_s = Some(
                        need(value)?
                            .parse()
                            .map_err(|_| "--duration must be a number of seconds".to_string())?,
                    )
                }
                "--bot" => out.bot = Some(need(value)?),
                "--replay" => out.replay = Some(need(value)?),
                "--record" => out.record = Some(need(value)?),
                "--screenshots" => out.screenshots = true,
                "--out" => out.out = Some(need(value)?),
                other => return Err(format!("unknown option {other}")),
            }
        }
        Ok(out)
    }

    pub fn is_test_run(&self) -> bool {
        self.smoke || self.replay.is_some() || self.screenshots
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_smoke_options() {
        let a = LaunchArgs::parse(&[
            "--smoke",
            "--seed=AAAA-BBBB",
            "--duration=60",
            "--out=/tmp/x",
        ])
        .unwrap();
        assert!(a.smoke && a.is_test_run());
        assert_eq!(a.seed.as_deref(), Some("AAAA-BBBB"));
        assert_eq!(a.duration_s, Some(60.0));
    }

    #[test]
    fn empty_is_normal_play() {
        assert!(!LaunchArgs::parse::<&str>(&[]).unwrap().is_test_run());
    }

    #[test]
    fn rejects_unknown_and_missing_values() {
        assert!(LaunchArgs::parse(&["--bogus"]).is_err());
        assert!(LaunchArgs::parse(&["--seed"]).is_err());
        assert!(LaunchArgs::parse(&["--duration=abc"]).is_err());
    }
}
