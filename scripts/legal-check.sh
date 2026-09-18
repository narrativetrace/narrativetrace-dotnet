#!/usr/bin/env bash
# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
#
# Verifies the legal:* marked regions in README.md and its root translations
# are well-formed, and — when the sibling repository holding the canonical
# legal text (legal.properties' legal.canonicalRepo) is checked out next to
# this one — that the regions meant to be identical across runtimes, and
# LICENSE, still match the canonical copies.
#
# Region set: {plain-words, trademark}. There is no legal:exclusion region —
# an HTML comment terminates a GFM table, so marker-wrapping a licence-table
# row (or the inline exclusion clause) breaks rendering; the exclusion
# sentence already lives verbatim inside legal:plain-words, unmarked, and
# stays that way (settled in the canonical Java repo).
#
# (a) Marker well-formedness: every legal:* marker in README.md, LEAME.md,
#     LEIAME.md and 自述文件.md has exactly one begin/end pair, in order.
#     Runs unconditionally — no sibling required.
# (b) Canonical comparison, only when $CANONICAL_REPO exists:
#       - LICENSE: the canonical repo's LICENSE with its "Licensed Work:"
#         line's work description swapped for legal.licensedWork must
#         byte-match this repo's LICENSE exactly (placeholders included).
#       - LICENSE-APACHE: must exist at the repo root and byte-match the
#         canonical repo's LICENSE-APACHE exactly — no placeholder swap, the
#         Apache License 2.0 text carries none and is identical across runtimes.
#       - each legal:* region: extracted from the matching-name canonical
#         file (README.md <-> README.md, LEAME.md <-> LEAME.md, ...) and
#         compared to the local region with whitespace collapsed.
#         legal:plain-words is canonical shared text and expected to match
#         exactly; legal:trademark wraps each repo's own paraphrase of the
#         same underlying trademark fact, so a content difference there is
#         normal, not drift — it is still reported (strict mode escalates
#         it) so a human can confirm the difference is wording, not
#         substance.
# (c) A mismatch always prints a diff. Default mode WARNs and exits 0 (same
#     convention as translation-check: a checkout without the sibling stays
#     green). Set LEGAL_CHECK_STRICT=1 to fail instead, for a deliberate local
#     or CI verification run (see NUKE's LegalCheck target, which runs this in
#     default warn mode as part of Verify — strict is not wired into the
#     everyday gate or into publish-public.sh, because the expected
#     legal:trademark wording difference described above would then fail
#     every run). Canonical repo absent -> (b) is skipped with a note; (a)
#     still runs and can still fail in strict mode.
set -euo pipefail
cd "$(dirname "$0")/.."

PROPS="legal.properties"
[ -f "$PROPS" ] || { echo "ERROR: $PROPS not found at repo root."; exit 1; }

prop() {
    sed -n "s/^$1=\\(.*\\)\$/\\1/p" "$PROPS" | head -1
}

PROPS_CANONICAL_REPO="$(prop legal.canonicalRepo)"
LICENSED_WORK="$(prop legal.licensedWork)"
[ -n "$PROPS_CANONICAL_REPO" ] || { echo "ERROR: legal.canonicalRepo not set in $PROPS."; exit 1; }
[ -n "$LICENSED_WORK" ] || { echo "ERROR: legal.licensedWork not set in $PROPS."; exit 1; }

# Canonical repo resolution order: LEGAL_CANONICAL_REPO env (host or container override) ->
# legal.canonicalRepo in legal.properties (the host-checkout relative path) ->
# /workspace-java (the documented read-only mount inside the dev container, see
# scripts/dev-container.sh). The dev container mounts the Java repo at
# /workspace-java, not at the properties' "../narrative-trace-java" (that path
# does not exist inside the container), so without this fallback chain the
# canonical-copy comparison in (b) below silently never ran in the container.
CANONICAL_REPO=""
CANONICAL_REPO_SOURCE=""
for candidate in "LEGAL_CANONICAL_REPO:${LEGAL_CANONICAL_REPO:-}" "legal.properties:${PROPS_CANONICAL_REPO}" "container mount:/workspace-java"; do
    src="${candidate%%:*}"
    path="${candidate#*:}"
    if [ -n "$path" ] && [ -d "$path" ]; then
        CANONICAL_REPO="$path"
        CANONICAL_REPO_SOURCE="$src"
        break
    fi
done
if [ -n "$CANONICAL_REPO" ]; then
    echo ">> Canonical repo resolved via $CANONICAL_REPO_SOURCE: $CANONICAL_REPO"
else
    echo "SKIPPED: canonical repo not found (tried LEGAL_CANONICAL_REPO, legal.properties' $PROPS_CANONICAL_REPO, /workspace-java) — canonical-copy comparison did not run."
    if [ "${NARRATIVETRACE_REQUIRE_LEGAL:-0}" = "1" ]; then
        echo "legal-check: FAILED — NARRATIVETRACE_REQUIRE_LEGAL=1 and no canonical repo resolved."
        echo "  Set LEGAL_CANONICAL_REPO=/path, or clone it to $PROPS_CANONICAL_REPO (legal.canonicalRepo in legal.properties), and re-run."
        exit 1
    fi
fi

STRICT="${LEGAL_CHECK_STRICT:-0}"
FAILED=0

warn_or_fail() {
    if [ "$STRICT" = "1" ]; then
        echo "ERROR: $1"
        FAILED=1
    else
        echo "WARN: $1"
    fi
}

MARKERS="plain-words trademark"
# Family rule (2026-09-04, harmonized with the Swift runtime): only plain-words
# is canonical-diffed. legal:trademark is checked for marker shape only — the
# core assertion is shared, but the surrounding licence-count phrasing is
# legitimately repo-local, so byte-diffing it against java would fail on
# wording that is correct here.
CANONICAL_DIFF_MARKERS="plain-words"
FILES="README.md LEAME.md LEIAME.md 自述文件.md"

# Line numbers of a marker's begin/end comments in $1, "<begin> <end>" (0 = absent).
marker_lines() {
    awk -v m="$2" '
        $0 ~ ("<!-- legal:" m ":begin -->") { b = NR }
        $0 ~ ("<!-- legal:" m ":end -->")   { e = NR }
        END { print b + 0, e + 0 }
    ' "$1"
}

# Verifies $1's legal:$2 marker is exactly one well-ordered begin/end pair.
check_marker_shape() {
    local file="$1" marker="$2" begins ends b e
    begins="$(grep -c -- "<!-- legal:${marker}:begin -->" "$file")"
    ends="$(grep -c -- "<!-- legal:${marker}:end -->" "$file")"
    if [ "$begins" -ne 1 ] || [ "$ends" -ne 1 ]; then
        warn_or_fail "$file: legal:$marker marker malformed (begin=$begins, end=$ends; expected exactly one pair)"
        return 1
    fi
    read -r b e <<EOF
$(marker_lines "$file" "$marker")
EOF
    if [ "$b" -eq 0 ] || [ "$e" -eq 0 ] || [ "$b" -ge "$e" ]; then
        warn_or_fail "$file: legal:$marker begin/end missing or out of order"
        return 1
    fi
    return 0
}

# Region content of $1's legal:$2 marker, marker comment lines excluded.
extract_region() {
    sed -n "/<!-- legal:$2:begin -->/,/<!-- legal:$2:end -->/p" "$1" | sed '1d;$d'
}

# Collapses all whitespace runs to a single space and trims the ends.
normalize_ws() {
    tr '\n\t' '  ' | tr -s ' ' | sed -e 's/^ *//' -e 's/ *$//'
}

echo ">> Marker well-formedness ..."
for file in $FILES; do
    if [ ! -f "$file" ]; then
        warn_or_fail "expected translated file missing: $file"
        continue
    fi
    for marker in $MARKERS; do
        check_marker_shape "$file" "$marker" || true
    done
done
echo ">>   done."

if [ -n "$CANONICAL_REPO" ]; then
    echo ">> Comparing against canonical copies at $CANONICAL_REPO ..."

    CANONICAL_LICENSE="$CANONICAL_REPO/LICENSE"
    if [ -f "$CANONICAL_LICENSE" ]; then
        EXPECTED_LICENSE="$(mktemp)"
        trap 'rm -f "$EXPECTED_LICENSE"' EXIT
        # The one line the two repos' LICENSE files are allowed to differ on:
        # the Licensed Work's description. Everything else — placeholders
        # included — must come through byte-for-byte.
        sed -E "s/^(Licensed Work:[[:space:]]*).*( version \\{\\{VERSION\\}\\}\\..*)\$/\\1${LICENSED_WORK}\\2/" \
            "$CANONICAL_LICENSE" > "$EXPECTED_LICENSE"
        if [ -f LICENSE ]; then
            if ! diff -q "$EXPECTED_LICENSE" LICENSE >/dev/null 2>&1; then
                echo "--- LICENSE differs from the canonical-derived expectation ---"
                diff "$EXPECTED_LICENSE" LICENSE || true
                warn_or_fail "LICENSE has drifted from the canonical repo's LICENSE"
            fi
        else
            warn_or_fail "no local LICENSE to compare against the canonical repo"
        fi
    else
        warn_or_fail "canonical repo has no LICENSE at $CANONICAL_LICENSE"
    fi

    CANONICAL_LICENSE_APACHE="$CANONICAL_REPO/LICENSE-APACHE"
    if [ -f "$CANONICAL_LICENSE_APACHE" ]; then
        if [ -f LICENSE-APACHE ]; then
            if ! diff -q "$CANONICAL_LICENSE_APACHE" LICENSE-APACHE >/dev/null 2>&1; then
                echo "--- LICENSE-APACHE differs from the canonical copy ---"
                diff "$CANONICAL_LICENSE_APACHE" LICENSE-APACHE || true
                warn_or_fail "LICENSE-APACHE has drifted from the canonical repo's LICENSE-APACHE"
            fi
        else
            warn_or_fail "no local LICENSE-APACHE to compare against the canonical repo"
        fi
    else
        warn_or_fail "canonical repo has no LICENSE-APACHE at $CANONICAL_LICENSE_APACHE"
    fi

    for file in $FILES; do
        [ -f "$file" ] || continue
        canonical_file="$CANONICAL_REPO/$file"
        if [ ! -f "$canonical_file" ]; then
            warn_or_fail "no matching canonical file for $file at $canonical_file"
            continue
        fi
        for marker in $CANONICAL_DIFF_MARKERS; do
            if ! check_marker_shape "$canonical_file" "$marker" >/dev/null 2>&1; then
                warn_or_fail "$canonical_file: legal:$marker missing/malformed in canonical copy — cannot compare $file against it"
                continue
            fi
            local_norm="$(extract_region "$file" "$marker" | normalize_ws)"
            canonical_norm="$(extract_region "$canonical_file" "$marker" | normalize_ws)"
            if [ "$local_norm" != "$canonical_norm" ]; then
                echo "--- legal:$marker region differs (whitespace-normalized): $file vs $canonical_file ---"
                diff <(printf '%s\n' "$canonical_norm") <(printf '%s\n' "$local_norm") || true
                warn_or_fail "$file: legal:$marker region text differs from canonical $canonical_file"
            fi
        done
    done
    echo ">>   done."
fi
# else: resolution above already printed SKIPPED (and exited under
# NARRATIVETRACE_REQUIRE_LEGAL=1) — release rule 2: a graceful skip must be
# able to prove the tool has ever run.

if [ "$FAILED" -eq 1 ]; then
    echo "legal-check: FAILED (LEGAL_CHECK_STRICT=1)"
    exit 1
fi
RESOLVED_SUFFIX=""
[ -n "$CANONICAL_REPO" ] && RESOLVED_SUFFIX=" — canonical repo: $CANONICAL_REPO (via $CANONICAL_REPO_SOURCE)"
echo "legal-check: OK$( [ "$STRICT" != "1" ] && echo ' (warn mode — set LEGAL_CHECK_STRICT=1 to enforce)')$RESOLVED_SUFFIX"
