"""Build a native Linux x86-64 Steam Deck playtest player with a local Unity 6 editor."""
import os
from pathlib import Path
import subprocess
import sys
import tempfile


def editor_path(project):
    setting = os.environ.get("UNITY_EDITOR")
    if setting:
        editor = Path(setting).expanduser()
        if editor.is_file() and os.access(editor, os.X_OK):
            return editor.resolve()
        raise ValueError(f"UNITY_EDITOR does not point to a Unity executable: {editor}")

    version_file = project / "ProjectSettings/ProjectVersion.txt"
    version = next((line.partition(":")[2].strip() for line in version_file.read_text().splitlines()
                    if line.startswith("m_EditorVersion:")), None)
    if not version:
        raise ValueError(f"Missing m_EditorVersion in {version_file}.")
    editions = [
        Path.home() / "Unity/Hub/Editor" / version / "Editor/Unity",
        Path("/Applications/Unity/Hub/Editor") / version / "Unity.app/Contents/MacOS/Unity",
        Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "Unity/Hub/Editor" / version / "Editor/Unity.exe",
    ]
    for editor in editions:
        if editor.is_file() and os.access(editor, os.X_OK):
            return editor
    raise ValueError(f"Unity {version} was not found. Set UNITY_EDITOR to the full path of the editor executable.")


def main():
    root = Path(__file__).resolve().parents[1]
    project = root / "unity"
    editor = editor_path(project)
    builds = root / "builds"
    builds.mkdir(exist_ok=True)
    run = Path(tempfile.mkdtemp(prefix="deck-", dir=builds))
    player = run / "Player"
    player.mkdir()
    executable = player / "OrbitalAnte.x86_64"
    env = dict(os.environ, OA_DECK_EXECUTABLE=str(executable))

    # A change to the input backend during setup takes effect on the next editor launch.
    for phase, method in (("setup", "SetUpFromCommandLine"), ("build", "BuildDeckPlaytest")):
        log = run / f"{phase}.log"
        print(f"Running Unity {phase}. Log: {log}", flush=True)
        result = subprocess.run([str(editor), "-batchmode", "-quit", "-projectPath", str(project),
                                 "-buildTarget", "Linux64", "-logFile", str(log),
                                 "-executeMethod", "OrbitalAnte.EditorTools.ProjectSetup." + method], env=env)
        if result.returncode:
            print(f"Unity {phase} failed (exit {result.returncode}). See {log}", file=sys.stderr)
            return 1

    for artifact in (executable, player / "UnityPlayer.so"):
        if not artifact.is_file() or artifact.stat().st_size == 0:
            raise ValueError(f"Unity reported success but {artifact} is missing or empty. See {run / 'build.log'}")
    data = player / "OrbitalAnte_Data"
    if not data.is_dir() or not any(data.iterdir()):
        raise ValueError(f"Unity reported success but the player data is missing. See {run / 'build.log'}")
    print(f"Copy the entire player folder to the Deck: {player}")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError) as error:
        print(error, file=sys.stderr)
        sys.exit(1)
