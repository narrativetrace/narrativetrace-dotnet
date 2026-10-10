# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
"""Grades init-prompt-aspnetcore-project (Phase 6 D4): the installed doctor taught the agent how
ASP.NET Core is wired, and the wiring works.

Run from the scaffolded project copy; $NARRATIVETRACE_CLI_DLL names the built CLI (HEAD's doctor —
the agent's own tool restore and its own report are never trusted).

THE GATE, each failure with its own message:

  1. A project under the copy is still an ASP.NET Core project (the Web SDK) — the framework the case
     is about must still be detected, or a green check below would mean "not detected".
  2. The grader's own doctor run reports `config.registration-pair` passed on that project.
  3. The app builds; run by the grader and requested at GET /orders/cust-1, it still answers
     "confirmed:cust-1:book-123:2", and the output it writes AFTER that request — not at startup —
     names the boundary (`IOrderService` and `PlaceOrder` as identifiers): a request-scoped trace.
  4. A C# file asserts `[REDACTED]`, and every test project's `dotnet test` passes.
  5. No `*.received.nt` exists anywhere in the copy.

REPORTED, never gated: every other failing doctor finding (by id).
"""

import json
import os
import re
import socket
import subprocess
import sys
import threading
import time
import urllib.request

REQUEST_PATH = "/orders/cust-1"
EXPECTED_BODY = "confirmed:cust-1:book-123:2"
BOUNDARY = ("IOrderService", "PlaceOrder")
WEB_SDK = re.compile(r"""\bSdk\s*=\s*["']\s*Microsoft\.NET\.Sdk\.Web(?:/[^"']*)?\s*["']""", re.IGNORECASE)
TEST_SDK = re.compile(r"""Include\s*=\s*["']Microsoft\.NET\.Test\.Sdk["']""", re.IGNORECASE)
SKIPPED_DIRS = {"bin", "obj", ".git", "node_modules"}

failures = []
reports = []


def fail(message):
    failures.append(message)


def run(argv, cwd=None, timeout=600):
    return subprocess.run(argv, cwd=cwd, capture_output=True, text=True, timeout=timeout)


def files(suffix):
    for root, dirs, names in os.walk("."):
        dirs[:] = [d for d in dirs if d not in SKIPPED_DIRS]
        for name in names:
            if name.endswith(suffix):
                yield os.path.join(root, name)


def read(path):
    with open(path, encoding="utf-8", errors="replace") as handle:
        return handle.read()


def without_xml_comments(text):
    return re.sub(r"<!--.*?(?:-->|\Z)", "", text, flags=re.DOTALL)


def web_project():
    for path in sorted(files(".csproj")):
        if WEB_SDK.search(without_xml_comments(read(path))):
            return path
    fail("no project under the copy is an ASP.NET Core (Microsoft.NET.Sdk.Web) project any more")
    return None


def doctor():
    result = run(["dotnet", os.environ["NARRATIVETRACE_CLI_DLL"], "doctor", "--json"])
    try:
        findings = json.loads(result.stdout)["findings"]
    except (ValueError, KeyError):
        fail("the grader's own doctor run produced no report:\n" + (result.stdout + result.stderr)[-800:])
        return
    by_id = {finding["id"]: finding for finding in findings}
    pair = by_id.get("config.registration-pair")
    if pair is None or not pair["passed"]:
        fail("config.registration-pair is not green: " + (pair["message"] if pair else "missing from the report"))
    others = sorted(i for i, f in by_id.items() if not f["passed"] and i != "config.registration-pair")
    if others:
        reports.append("other failing doctor findings (not gated): " + ", ".join(others))


def free_port():
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        return probe.getsockname()[1]


class Output:
    """Collects a process's merged output on a thread; `mark()` remembers where 'now' is."""

    def __init__(self, stream):
        self.text = ""
        self._lock = threading.Lock()
        self._thread = threading.Thread(target=self._pump, args=(stream,), daemon=True)
        self._thread.start()

    def _pump(self, stream):
        for line in stream:
            with self._lock:
                self.text += line

    def mark(self):
        with self._lock:
            return len(self.text)

    def since(self, mark):
        self._thread.join(timeout=10)
        with self._lock:
            return self.text[mark:]


def wait_for_port(port, process, seconds=90):
    deadline = time.time() + seconds
    while time.time() < deadline and process.poll() is None:
        try:
            with socket.create_connection(("127.0.0.1", port), timeout=1):
                return True
        except OSError:
            time.sleep(0.5)
    return False


def request(port):
    try:
        with urllib.request.urlopen("http://127.0.0.1:%d%s" % (port, REQUEST_PATH), timeout=30) as response:
            return response.status, response.read().decode("utf-8", errors="replace")
    except Exception as error:  # noqa: BLE001 - any failure to answer is the finding
        return None, str(error)


def identifiers(text):
    return set(re.findall(r"[A-Za-z_][A-Za-z0-9_]*", re.sub(r"[`*_\"]+", " ", text)))


def serve_and_request(project):
    build = run(["dotnet", "build", project, "--configuration", "Release", "--nologo", "--verbosity", "quiet"])
    if build.returncode != 0:
        fail("the web project does not build in Release:\n" + (build.stdout + build.stderr)[-800:])
        return
    port = free_port()
    app = subprocess.Popen(
        ["dotnet", "run", "--project", project, "--configuration", "Release", "--no-build",
         "--urls", "http://127.0.0.1:%d" % port],
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    output = Output(app.stdout)
    try:
        if not wait_for_port(port, app):
            fail("the app never listened on its port:\n" + output.since(0)[-800:])
            return
        before = output.mark()
        status, body = request(port)
        time.sleep(3)  # the console logger writes on a background queue
    finally:
        app.terminate()
        try:
            app.wait(timeout=30)
        except subprocess.TimeoutExpired:
            app.kill()
    after = output.since(before)
    if status != 200 or body.strip() != EXPECTED_BODY:
        fail("GET %s answered %r %r, not 200 %r" % (REQUEST_PATH, status, body.strip()[:200], EXPECTED_BODY))
    missing = [name for name in BOUNDARY if name not in identifiers(after)]
    if missing:
        fail("the output written after the grader's request names no %s — no request-scoped trace:\n%s"
             % (" / ".join(missing), after[-800:]))


def redaction_test():
    if not any("[REDACTED]" in read(path) for path in files(".cs")):
        fail("no C# file under the copy asserts [REDACTED]")
    test_projects = [path for path in sorted(files(".csproj")) if TEST_SDK.search(without_xml_comments(read(path)))]
    if not test_projects:
        fail("no test project (Microsoft.NET.Test.Sdk) under the copy")
    for path in test_projects:
        result = run(["dotnet", "test", path, "--nologo", "--verbosity", "quiet"])
        if result.returncode != 0:
            fail("dotnet test %s does not pass:\n%s" % (path, (result.stdout + result.stderr)[-800:]))


def no_received_files():
    for root, _, names in os.walk("."):
        for name in names:
            if name.endswith(".received.nt"):
                fail("a .received.nt file exists: " + os.path.join(root, name))
                return


def main():
    if "NARRATIVETRACE_CLI_DLL" not in os.environ:
        print("FAIL: NARRATIVETRACE_CLI_DLL is not set — the grader has no doctor to run")
        return 1
    project = web_project()
    doctor()
    if project:
        serve_and_request(project)
    redaction_test()
    no_received_files()
    for message in reports:
        print("REPORT: " + message)
    for message in failures:
        print("FAIL: " + message)
    if not failures:
        print("PASS: ASP.NET Core is wired as the doctor printed it, a request writes its trace, redaction is proven")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
