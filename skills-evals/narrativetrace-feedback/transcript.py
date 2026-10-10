# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
"""Reads a Tier B trial transcript — the record the runner kept of a conversation.

The runner writes one JSON object per line: its own ``{"nt_turn": N, "role": "user", "text": ...}``
marker before each turn, then that turn's own standard output. With the Claude lane's streaming
format that output is itself one JSON object per line, so the whole file is JSONL and a grader can
read it line by line.

Why a grader needs this at all, when every other case in this suite reads world state only: an
approval is only an approval if it arrives in a turn of the USER's own, and "was the report filed
before that turn?" is a question about ORDER. The project's end state cannot answer it — the same
files are on disk either way.

The transcript lives outside the scaffolded project and the agent is never told where it is, so
this is evidence rather than something the subject wrote about itself.
"""

import json
import re


class Turn:
    """One turn: the words the user was given, and everything the agent did with them."""

    def __init__(self, number, user_text):
        self.number = number
        self.user_text = user_text
        self.texts = []
        self.commands = []
        self.command_ids = []
        self.results_by_id = {}
        self.tool_results = []
        self.denials = []

    @property
    def said(self):
        """Everything the agent said in this turn, as one block of text."""
        return "\n".join(self.texts)

    @property
    def refused_tools(self):
        """Tools the harness refused this turn — read before blaming the product for anything.

        A denial is NOT by itself a failure: a tool the skill never needed being refused is the
        preset working. A tool it NEEDED being refused measures the sandbox, not the skill.
        """
        return list(self.denials)

    @property
    def everything(self):
        """Said, ran and saw — every byte of this turn, for an "appears nowhere" check."""
        return "\n".join(self.texts + self.commands + self.tool_results)

    def result_of(self, index):
        """What the command at ``index`` answered, matched by its tool-use id, never by position.

        Matching by position breaks the moment one result arrives out of order or one call has none
        (a refused tool), and then a grader reads another command's output as this one's.
        """
        tool_id = self.command_ids[index]
        return self.results_by_id.get(tool_id, "") if tool_id else ""

    def __repr__(self):
        return "Turn(%d, %d texts, %d commands)" % (
            self.number,
            len(self.texts),
            len(self.commands),
        )


def _blocks(event):
    """The content blocks of an assistant or user event, whatever shape it arrived in.

    ``message`` is an OBJECT on an assistant or user event and a plain STRING on at least one system
    event. Reading it as an object unconditionally would raise inside the grader, which exits
    non-zero and fails the case for a harness reason (found on a real Java trial).
    """
    message = event.get("message")
    if not isinstance(message, dict):
        return []
    content = message.get("content")
    return content if isinstance(content, list) else []


def _result_text(block):
    """A tool result is a string on some events and a list of blocks on others."""
    content = block.get("content")
    if isinstance(content, str):
        return content
    if isinstance(content, list):
        return "\n".join(
            part.get("text", "") for part in content if isinstance(part, dict)
        )
    return ""


def _absorb(turn, event):
    """One streamed event into the turn it belongs to."""
    if event.get("subtype") == "permission_denied":
        turn.denials.append(str(event.get("tool_name", "")))
        return
    if event.get("type") == "result" and isinstance(event.get("result"), str):
        turn.texts.append(event["result"])
        return
    for block in _blocks(event):
        if not isinstance(block, dict):
            continue
        kind = block.get("type")
        if kind == "text":
            turn.texts.append(block.get("text", ""))
        elif kind == "tool_use":
            turn.commands.append(json.dumps(block.get("input") or {}, sort_keys=True))
            turn.command_ids.append(block.get("id"))
        elif kind == "tool_result":
            text = _result_text(block)
            turn.tool_results.append(text)
            if block.get("tool_use_id"):
                turn.results_by_id[block["tool_use_id"]] = text


def read(path):
    """Every turn of the transcript at ``path``, in order.

    A line that is not JSON is kept as plain agent text rather than dropped: an agent command whose
    own output is not JSON (a text-format override, a crash message) is still something the agent
    put on standard output, and silently discarding it would make an "appears nowhere" check pass
    by not looking.
    """
    turns = []
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            try:
                event = json.loads(line)
            except ValueError:
                if turns:
                    turns[-1].texts.append(line)
                continue
            if isinstance(event, dict) and "nt_turn" in event:
                turns.append(Turn(event["nt_turn"], event.get("text", "")))
            elif turns and isinstance(event, dict):
                _absorb(turns[-1], event)
    return turns


def whole(turns):
    """Every byte of every turn — said, ran and seen."""
    return "\n".join(turn.everything for turn in turns)


ISSUE_URL = "https://github.com/narrativetrace/narrativetrace-dotnet/issues/new"


def issue_urls(text):
    """Every pre-filled issue-form URL in ``text`` — the one thing that FILES this report.

    JSON-escaped text (a tool input serialised with ``json.dumps``) writes ``/`` as itself, so the
    prefix is found the same way in prose, in a command and in a command's printed output.
    """
    found = []
    at = text.find(ISSUE_URL)
    while at >= 0:
        end = at
        while end < len(text) and not text[end].isspace() and text[end] not in "\"'`)<>\\":
            end += 1
        found.append(text[at:end])
        at = text.find(ISSUE_URL, end)
    return found


def runs_the_verb(command, channel):
    """Whether a tool call ran ``narrativetrace feedback <channel>`` — local tool or global form."""
    return re.search(r"narrativetrace\s+feedback\s+" + re.escape(channel) + r"\b", command) is not None


def blocked_invocations(path):
    """Every line the recording stand-ins wrote (``gh`` and ``curl``); empty when none ran."""
    try:
        with open(path, encoding="utf-8", errors="replace") as handle:
            return [line.strip() for line in handle if line.strip()]
    except (FileNotFoundError, IsADirectoryError):
        return []


def scripted_replies(case_dir):
    """The replies ``case_dir``'s own ``case.json`` declares, in turn order.

    Read from the case rather than passed to the grader as an argument: a duplicate of the scripted
    reply drifted within the hour in the Java reference. The case file is the one place the
    conversation is declared — the runner reads the same field through ``CaseTurns``.
    """
    with open(case_dir / "case.json", encoding="utf-8") as handle:
        manifest = json.load(handle)
    turns = manifest.get("turns") or {}
    return [turns[key] for key in sorted(turns, key=int)]
