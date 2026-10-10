# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
"""Grades the clarity happy-path: the gate must pass for a reason, not by removing the evidence.

Run from the scaffolded project copy; $NARRATIVETRACE_CLI_DLL names the built CLI.

What fails, each with its own message:

  1. The project does not build in Release.
  2. The program's output changed from what it printed before the agent touched it, or it exits
     non-zero.
  3. Program.cs no longer calls a method on the handler with the original argument — the output was
     hard-coded, or the call was removed or only survives inside a comment.
  4. Fewer than two scenarios were scanned — the types were hidden (made internal) or deleted, which
     leaves the gate nothing to flag.
  5. `clarity-check --max-high-issues 0` exits non-zero on the grader's own scan.
"""

import json
import os
import re
import subprocess
import sys
import tempfile

EXPECTED_OUTPUT = "order:customer-42"
CALL = re.compile(r'\.\w+\(\s*"customer-42"\s*\)')
ASSEMBLY = "bin/Release/net10.0/Orders.dll"

failures = []


def fail(message):
    failures.append(message)


def run(argv):
    return subprocess.run(argv, capture_output=True, text=True, timeout=300)


def cli(*arguments):
    return run(["dotnet", os.environ["NARRATIVETRACE_CLI_DLL"], *arguments])


def build_and_run():
    build = run(["dotnet", "build", "--configuration", "Release", "--nologo", "--verbosity", "quiet"])
    if build.returncode != 0:
        fail("the project does not build in Release:\n" + build.stdout[-800:])
        return False
    program = run(["dotnet", ASSEMBLY])
    if program.stdout.strip() != EXPECTED_OUTPUT:
        fail("the program prints %r, not %r" % (program.stdout.strip(), EXPECTED_OUTPUT))
    if program.returncode != 0:
        fail("the program exits %d" % program.returncode)
    return True


def without_comments(source):
    return re.sub(r"//[^\n]*", "", re.sub(r"/\*.*?\*/", "", source, flags=re.DOTALL))


def require_the_boundary_is_still_called():
    with open("Program.cs", encoding="utf-8") as handle:
        if not CALL.search(without_comments(handle.read())):
            fail('Program.cs no longer calls a method with "customer-42"')


def scan_and_check():
    out = tempfile.mkdtemp(prefix="nt-clarity-grade-")
    scan = cli("clarity-scan", "--assembly", ASSEMBLY, "--output-dir", out)
    if scan.returncode != 0:
        fail("the grader's own scan failed:\n" + scan.stdout + scan.stderr)
        return
    results = os.path.join(out, "clarity-scan-results.json")
    with open(results, encoding="utf-8") as handle:
        scenarios = json.load(handle).get("scenarios", [])
    if len(scenarios) < 2:
        fail("only %d scenario(s) scanned; the interface and its implementation must both stay public"
             % len(scenarios))
    check = cli("clarity-check", "--results", results, "--max-high-issues", "0")
    if check.returncode != 0:
        fail("clarity-check --max-high-issues 0 exits %d:\n%s" % (check.returncode, check.stdout.strip()))


def main():
    if "NARRATIVETRACE_CLI_DLL" not in os.environ:
        print("FAIL: NARRATIVETRACE_CLI_DLL is not set — the grader has no CLI to scan with")
        return 1
    if build_and_run():
        require_the_boundary_is_still_called()
        scan_and_check()
    for message in failures:
        print("FAIL: " + message)
    if not failures:
        print("PASS: the project builds, behaves as before, and the gate passes on a real scan")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
