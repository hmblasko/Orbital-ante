"""Tests for tools/chatgpt/oa_handoff.py. Run: python3 -m unittest discover -s tools/chatgpt/tests"""
import contextlib
import io
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import oa_handoff as h  # noqa: E402

CODE = """namespace OrbitalAnte.Ship
{
    public sealed class HatchRules
    {
        public float OpenSeconds = 1.5f;

        public float Progress(float t)
        {
            return t / OpenSeconds;
        }
    }
}
"""


class HandoffTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.cwd = os.getcwd()
        os.chdir(self.root)
        run = lambda *a: subprocess.run(["git", *a], check=True, capture_output=True)
        run("init", "-q")
        run("config", "user.email", "t@example.com")
        run("config", "user.name", "t")
        (self.root / "unity/Assets/OrbitalAnte").mkdir(parents=True)
        (self.root / "unity/Assets/OrbitalAnte/HatchRules.cs").write_text(CODE)
        (self.root / "unity/Assets/OrbitalAnte/HatchRules.cs.meta").write_text("guid: 1\n")
        (self.root / "CLAUDE.md").write_text("rules\n")
        run("add", "-A")
        run("commit", "-qm", "init")

    def tearDown(self):
        os.chdir(self.cwd)
        self.tmp.cleanup()

    def pack(self, *args):
        with contextlib.redirect_stdout(io.StringIO()) as out:
            code = h.main(["pack", "--out", str(self.root / "handoff"), *args])
        self.assertEqual(code, 0)
        pid = out.getvalue().split()[1]
        return self.root / "handoff" / pid, pid

    def intake(self, pdir, reply, *args):
        f = self.root / "reply.md"
        f.write_text(reply)
        with contextlib.redirect_stdout(io.StringIO()) as out:
            code = h.main(["intake", str(pdir), str(f), *args])
        return code, out.getvalue()

    def envelope(self, pid, task, findings=None, **extra):
        env = {"packet": pid, "task": task, "summary": "ok", "findings": findings or [], "notChecked": []}
        env.update(extra)
        return "```json\n" + json.dumps(env, indent=2) + "\n```\n"

    def finding(self, **over):
        f = {"id": "GPT-001", "file": "unity/Assets/OrbitalAnte/HatchRules.cs", "line": 9, "category": "code",
             "severity": "P1", "kind": "suspected_defect", "title": "Divide by zero",
             "description": "OpenSeconds 0 divides by zero", "evidence": ["return t / OpenSeconds;"],
             "recommendedFixes": ["guard with Mathf.Max"], "acceptanceCriteria": ["test with 0"], "confidence": 0.7}
        f.update(over)
        return f

    # --- pack

    def test_review_packet_has_numbered_code_and_reply_format(self):
        pdir, pid = self.pack("--task", "review-code", "--title", "Hatch rules", "--ask", "find bugs",
                              "--file", "unity/Assets/OrbitalAnte/HatchRules.cs")
        text = (pdir / "packet.md").read_text()
        self.assertIn(pid, text)
        self.assertIn("    9|             return t / OpenSeconds;", text)
        self.assertIn('"packet": "' + pid, text)
        self.assertNotIn("```edits", text)
        meta = json.loads((pdir / "packet.json").read_text())
        self.assertEqual(meta["task"], "review-code")
        self.assertIn("unity/Assets/OrbitalAnte/HatchRules.cs", meta["fileHashes"])

    def test_edit_packet_shows_raw_file_and_edit_format(self):
        pdir, _ = self.pack("--task", "small-edit", "--title", "guard", "--ask", "guard zero",
                            "--allow-edit", "unity/Assets/OrbitalAnte/HatchRules.cs")
        text = (pdir / "packet.md").read_text()
        self.assertIn("            return t / OpenSeconds;", text)
        self.assertNotIn("    9|", text)
        self.assertIn("<<<<<<< SEARCH", text)

    def test_never_edit_list_refused_at_pack(self):
        for path in ("CLAUDE.md", "unity/Assets/OrbitalAnte/HatchRules.cs.meta"):
            with self.assertRaises(SystemExit):
                h.main(["pack", "--out", str(self.root / "handoff"), "--task", "small-edit", "--title", "x",
                        "--ask", "x", "--allow-edit", path])

    def test_edit_task_needs_allow_edit(self):
        with self.assertRaises(SystemExit):
            h.main(["pack", "--task", "small-edit", "--title", "x", "--ask", "x",
                    "--file", "unity/Assets/OrbitalAnte/HatchRules.cs"])

    def test_second_opinion_copies_pictures(self):
        img = self.root / "shot.jpg"
        img.write_bytes(b"\xff\xd8\xff")
        pdir, _ = self.pack("--task", "second-opinion", "--title", "lander", "--ask", "judge", "--image", str(img))
        self.assertTrue((pdir / "attach" / "01-shot.jpg").exists())
        self.assertIn("advisoryScores", (pdir / "packet.md").read_text())

    # --- intake: findings

    def test_valid_review_reply_accepted_and_marked_unverified(self):
        pdir, pid = self.pack("--task", "review-code", "--title", "t", "--ask", "a",
                              "--file", "unity/Assets/OrbitalAnte/HatchRules.cs")
        code, out = self.intake(pdir, "Sure!\n" + self.envelope(pid, "review-code", [self.finding()]))
        self.assertEqual(code, 0, out)
        self.assertIn("ACCEPTED", out)
        saved = json.loads((pdir / "findings.json").read_text())
        self.assertFalse(saved[0]["verified"])
        self.assertEqual(saved[0]["source"], "chatgpt")

    def test_verified_defect_is_downgraded(self):
        pdir, pid = self.pack("--task", "review-code", "--title", "t", "--ask", "a",
                              "--file", "unity/Assets/OrbitalAnte/HatchRules.cs")
        code, out = self.intake(pdir, self.envelope(pid, "review-code", [self.finding(kind="verified_defect")]))
        self.assertEqual(code, 0, out)
        self.assertEqual(json.loads((pdir / "findings.json").read_text())[0]["kind"], "suspected_defect")
        self.assertIn("downgraded", out)

    def test_wrong_packet_id_and_bad_fields_rejected(self):
        pdir, pid = self.pack("--task", "review-code", "--title", "t", "--ask", "a",
                              "--file", "unity/Assets/OrbitalAnte/HatchRules.cs")
        code, out = self.intake(pdir, self.envelope("H-other", "review-code",
                                                    [self.finding(severity="high", confidence=7)]))
        self.assertEqual(code, 1)
        self.assertIn("REJECTED", out)
        self.assertIn("not " + pid, out)
        self.assertIn("severity", out)
        self.assertIn("confidence", out)

    def test_unfenced_json_still_read(self):
        pdir, pid = self.pack("--task", "review-code", "--title", "t", "--ask", "a",
                              "--file", "unity/Assets/OrbitalAnte/HatchRules.cs")
        env = {"packet": pid, "task": "review-code", "summary": "s", "findings": [], "notChecked": []}
        code, out = self.intake(pdir, "Here you go: " + json.dumps(env))
        self.assertEqual(code, 0, out)
        self.assertIn("not in a ```json fence", out)

    def test_edits_refused_on_review_packet(self):
        pdir, pid = self.pack("--task", "review-code", "--title", "t", "--ask", "a",
                              "--file", "unity/Assets/OrbitalAnte/HatchRules.cs")
        reply = self.envelope(pid, "review-code") + ("```edits\nFILE: unity/Assets/OrbitalAnte/HatchRules.cs\n"
                                                     "<<<<<<< SEARCH\n            return t / OpenSeconds;\n=======\n"
                                                     "            return 0;\n>>>>>>> REPLACE\n```\n")
        code, out = self.intake(pdir, reply, "--apply")
        self.assertEqual(code, 1)
        self.assertEqual((self.root / "unity/Assets/OrbitalAnte/HatchRules.cs").read_text(), CODE)

    def test_advisory_scores_checked(self):
        img = self.root / "shot.jpg"
        img.write_bytes(b"x")
        pdir, pid = self.pack("--task", "second-opinion", "--title", "l", "--ask", "j", "--image", str(img))
        f = self.finding(file=None, target="01-shot.jpg", category="modeling", kind="aesthetic_judgment")
        code, out = self.intake(pdir, self.envelope(pid, "second-opinion", [f], advisoryScores={"modeling": 6}))
        self.assertEqual(code, 0, out)
        self.assertIn("never count toward the gate", out)
        code, out = self.intake(pdir, self.envelope(pid, "second-opinion", [f], advisoryScores={"vibes": 11}))
        self.assertEqual(code, 1)

    # --- intake: edits

    def edit_reply(self, pid, search, replace, file="unity/Assets/OrbitalAnte/HatchRules.cs"):
        return self.envelope(pid, "small-edit") + (
            f"```edits\nFILE: {file}\n<<<<<<< SEARCH\n{search}\n=======\n{replace}\n>>>>>>> REPLACE\n```\n")

    def edit_packet(self):
        return self.pack("--task", "small-edit", "--title", "g", "--ask", "g",
                         "--allow-edit", "unity/Assets/OrbitalAnte/HatchRules.cs")

    def test_edit_dry_run_then_apply(self):
        pdir, pid = self.edit_packet()
        reply = self.edit_reply(pid, "            return t / OpenSeconds;",
                                "            return OpenSeconds <= 0f ? 1f : t / OpenSeconds;")
        path = self.root / "unity/Assets/OrbitalAnte/HatchRules.cs"
        code, out = self.intake(pdir, reply)
        self.assertEqual(code, 0, out)
        self.assertEqual(path.read_text(), CODE)
        self.assertIn("--apply", out)
        code, out = self.intake(pdir, reply, "--apply")
        self.assertEqual(code, 0, out)
        self.assertIn("OpenSeconds <= 0f", path.read_text())
        self.assertIn("APPLIED", out)

    def test_edit_outside_allowed_files_rejected(self):
        pdir, pid = self.edit_packet()
        code, out = self.intake(pdir, self.edit_reply(pid, "rules", "new rules", file="CLAUDE.md"), "--apply")
        self.assertEqual(code, 1)
        self.assertEqual((self.root / "CLAUDE.md").read_text(), "rules\n")

    def test_edit_search_missing_or_ambiguous_rejected_and_nothing_written(self):
        pdir, pid = self.edit_packet()
        path = self.root / "unity/Assets/OrbitalAnte/HatchRules.cs"
        code, out = self.intake(pdir, self.edit_reply(pid, "return t * 2;", "x"), "--apply")
        self.assertEqual(code, 1)
        self.assertIn("not found", out)
        code, out = self.intake(pdir, self.edit_reply(pid, "    {", "x"), "--apply")
        self.assertEqual(code, 1)
        self.assertIn("appears", out)
        self.assertEqual(path.read_text(), CODE)

    def test_one_bad_edit_blocks_all(self):
        pdir, pid = self.edit_packet()
        reply = self.envelope(pid, "small-edit") + (
            "```edits\nFILE: unity/Assets/OrbitalAnte/HatchRules.cs\n<<<<<<< SEARCH\n"
            "        public float OpenSeconds = 1.5f;\n=======\n        public float OpenSeconds = 2f;\n"
            ">>>>>>> REPLACE\n\nFILE: unity/Assets/OrbitalAnte/HatchRules.cs\n<<<<<<< SEARCH\nnope\n=======\n"
            "x\n>>>>>>> REPLACE\n```\n")
        code, _ = self.intake(pdir, reply, "--apply")
        self.assertEqual(code, 1)
        self.assertEqual((self.root / "unity/Assets/OrbitalAnte/HatchRules.cs").read_text(), CODE)

    def test_trailing_space_tolerated(self):
        pdir, pid = self.edit_packet()
        code, out = self.intake(pdir, self.edit_reply(pid, "            return t / OpenSeconds;   ",
                                                      "            return 1f;"), "--apply")
        self.assertEqual(code, 0, out)
        self.assertIn("trailing spaces", out)

    def test_unified_diff_rejected_with_advice(self):
        pdir, pid = self.edit_packet()
        reply = self.envelope(pid, "small-edit") + "```diff\n--- a/x\n+++ b/x\n@@ -1 +1 @@\n-a\n+b\n```\n"
        code, out = self.intake(pdir, reply)
        self.assertEqual(code, 1)
        self.assertIn("edits block", out)

    def test_windows_line_endings_in_reply(self):
        pdir, pid = self.edit_packet()
        reply = self.edit_reply(pid, "            return t / OpenSeconds;", "            return 1f;").replace("\n", "\r\n")
        code, out = self.intake(pdir, reply, "--apply")
        self.assertEqual(code, 0, out)


if __name__ == "__main__":
    unittest.main()
