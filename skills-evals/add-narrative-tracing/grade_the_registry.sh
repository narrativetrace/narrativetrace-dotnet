#!/usr/bin/env bash
# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
# World-state verifier for a REGISTRY case: what the registry left in the project, and what the
# installer then did with it. Shared by both registry cases; run with PROJECT_DIR set to the
# scaffolded fixture copy, from a case's graders/verify.sh, with $1 naming the registry that
# delivered the pages (claude-marketplace | npx-skills) and $2 the service boundary the fixture
# defines.
#
# The published prompt's own promises are NOT graded here — grade_the_prompt.sh is the whole of
# that, and it runs last. What is graded here is the three things only a registry case can be
# asked, in the order that fails cheapest first:
#
#   1. No vendor page was written THROUGH a symbolic link. `npx skills add` makes
#      .claude/skills/<name> a link to .agents/skills/<name>, so following it to write the vendor
#      flavour would stamp the open-standard page it points at while leaving the link in place.
#   2. The installer refused nothing, so no `--force` was needed — read out of a real
#      `init --dry-run --json` plan, never an exit code a dry run always leaves at zero.
#   3. The registry's own files are still the registry's: its lock file, and the pages it wrote
#      where our installer never looks.
#
# This port's one real divergence from Java's grader: the catalogue has no per-skill tool list, so
# BOTH page flavours render byte-identical and `allowed-tools` (Java's key for check 1) cannot tell
# them apart. The write-through hazard has a structural signature instead — a .claude/skills link
# that SURVIVES while the open-standard page it points at has been stamped with our provenance line
# (the safe installer DELETES the link and writes a real directory; milestone 2's replace-link).
#
# $NARRATIVETRACE_CLI_DLL is the built, zero-dependency CLI the runner points the grader at; absent,
# the agent's own local tool is used. Either reads the installer's PLAN, independent of the agent.
set -euo pipefail

: "${PROJECT_DIR:?PROJECT_DIR must point at the trial fixture copy}"
cd "$PROJECT_DIR"

registry="${1:?usage: grade_the_registry.sh <claude-marketplace|npx-skills> <ServiceName>}"
service="${2:-IOrderService}"
here="$(dirname "$0")"
PROVENANCE='<!-- installed by narrativetrace init from '

if [[ -n "${NARRATIVETRACE_CLI_DLL:-}" ]]; then
  cli=(dotnet "$NARRATIVETRACE_CLI_DLL")
else
  cli=(dotnet tool run dotnet-narrativetrace)
fi

# ------------------------------------------------------------------------------------------------
# 1. Nothing was written through a link.
# ------------------------------------------------------------------------------------------------
for entry in .claude/skills/*; do
  [ -e "$entry" ] || [ -L "$entry" ] || continue
  name="$(basename "$entry")"
  if [ -L "$entry" ]; then
    target="$(readlink "$entry")"
    case "$target" in
      *.agents/skills/*) : ;;
      *)
        echo "FAIL: $entry is a link to $target, not the registry's .agents/skills page —" >&2
        echo "something replaced a link the installer must refuse" >&2
        exit 1 ;;
    esac
    agents_page=".agents/skills/$name/SKILL.md"
    if [ -f "$agents_page" ] && grep -q "$PROVENANCE" "$agents_page"; then
      echo "FAIL: $entry is still a link, yet $agents_page carries our provenance line — the" >&2
      echo "open-standard page was stamped THROUGH the link instead of the link being replaced" >&2
      exit 1
    fi
    echo "no write-through: $entry is still the registry's link to $target"
  elif [ -d "$entry" ]; then
    if ! grep -q "$PROVENANCE" "$entry/SKILL.md" 2>/dev/null; then
      echo "FAIL: $entry is a real directory with no provenance line — the installer never wrote" >&2
      echo "it, so the wrong thing created a real page where the registry left a link" >&2
      exit 1
    fi
    echo "no write-through: $entry is a real directory the installer wrote (link safely replaced)"
  fi
done

# ------------------------------------------------------------------------------------------------
# 2. The installer refuses nothing on the tree the registry made, and needs no --force.
#    A dry run writes nothing and always exits 0, so the plan itself is what is read.
# ------------------------------------------------------------------------------------------------
plan="$("${cli[@]}" init --dry-run --json)"
printf '%s' "$plan" | python3 -c '
import json, sys
plan = json.load(sys.stdin)
actions = plan.get("actions", [])
refused = [a for a in actions if a.get("status") == "refused"]
if refused:
    print("the installer refuses actions on the registry-installed tree, so a reader would reach",
          "for --force:", refused, file=sys.stderr)
    sys.exit(1)
print("no refusals: the plan is", ", ".join(sorted({a["kind"] for a in actions})) or "empty")
'

# ------------------------------------------------------------------------------------------------
# 3. What this particular registry leaves behind, and what the installer may never touch of it.
# ------------------------------------------------------------------------------------------------
case "$registry" in
  npx-skills)
    test -f skills-lock.json || {
      echo "FAIL: the registry tool's own lock file is gone — the installer may remove only what" >&2
      echo "it wrote itself" >&2
      exit 1
    }
    python3 - <<'PY'
import json, pathlib, sys
lock = json.loads(pathlib.Path("skills-lock.json").read_text())
names = sorted(lock.get("skills", {}))
if not names:
    print("skills-lock.json names no skill at all: the registry installed nothing", file=sys.stderr)
    sys.exit(1)
missing = [n for n in names if not pathlib.Path(".agents/skills", n, "SKILL.md").is_file()]
if missing:
    print("the lock file names skills with no page left on disk:", missing, file=sys.stderr)
    sys.exit(1)
# The vendor side the tool symlinked has to be there too — as the link it made, or as the real
# directory the installer replaced it with. Gone means somebody removed what the registry left.
vendorless = [n for n in names if not pathlib.Path(".claude/skills", n).exists()
              and not pathlib.Path(".claude/skills", n).is_symlink()]
if vendorless:
    print("the registry pointed .claude/skills at these and now nothing is there:", vendorless,
          file=sys.stderr)
    sys.exit(1)
print("the registry's", len(names), "pages and its lock file are intact:", ", ".join(names))
PY
    # The third place some npx versions write real pages (agent/skills, reflowed by the tool).
    # Our installer never looks there, so it must never have stamped one.
    for page in agent/skills/*/SKILL.md; do
      [ -f "$page" ] || continue
      if grep -q "$PROVENANCE" "$page"; then
        echo "FAIL: $page carries our provenance line, but it is the registry tool's own copy in" >&2
        echo "a directory the installer does not own" >&2
        exit 1
      fi
    done
    ;;
  claude-marketplace)
    test ! -f skills-lock.json || {
      echo "FAIL: a user-scope plugin install writes no lock file into the project, so this tree" >&2
      echo "was not made by the registry this case names" >&2
      exit 1
    }
    # User scope: the plugin's pages live in the agent's own configuration, never in the project.
    # So every page that IS in the project has to be one the installer wrote and stamped.
    for page in .agents/skills/*/SKILL.md .claude/skills/*/SKILL.md; do
      [ -f "$page" ] || continue
      grep -q "$PROVENANCE" "$page" || {
        echo "FAIL: $page is in the project without the installer's provenance line, and a" >&2
        echo "user-scope plugin install puts nothing in the project — where did it come from?" >&2
        exit 1
      }
    done
    echo "user scope: the project carries no page the installer did not write"
    ;;
  *)
    echo "FAIL: unknown registry \"$registry\" — the graders know claude-marketplace and npx-skills" >&2
    exit 1
    ;;
esac

# ------------------------------------------------------------------------------------------------
# 4. Everything the published prompt itself promises, graded exactly as the other cases grade it.
# ------------------------------------------------------------------------------------------------
exec bash "$here/grade_the_prompt.sh" "$service"
