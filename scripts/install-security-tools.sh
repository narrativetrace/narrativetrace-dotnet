#!/usr/bin/env bash
# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
# install-security-tools.sh — provisions the security-scanning binaries the
# security-tooling entry points need (SecretsScan/gitleaks, Semgrep,
# OsvScan) so a CI job can run the real scan instead of the local warn-and-
# pass fallback those NUKE targets use when a tool is simply absent from
# PATH. THIN-CI: pinned versions live here, not in CI YAML — the private CI
# and .github/workflows/ci.yml only invoke this script with a tool name.
#
# CI-only. Not meant for the local/offline dev container: it wants root (or
# an image where pip/apt work unprompted) and fails loudly on a download
# error rather than degrading gracefully — the opposite of what a developer
# machine should do when a tool is missing.
#
# Usage: scripts/install-security-tools.sh <gitleaks|osv-scanner|semgrep> [...]
set -euo pipefail

GITLEAKS_VERSION="8.30.1"
OSV_SCANNER_VERSION="2.5.1"
SEMGREP_VERSION="1.176.0"
BIN_DIR="${SECURITY_TOOLS_BIN_DIR:-/usr/local/bin}"

arch() { uname -m; }

install_gitleaks() {
    local asset
    case "$(arch)" in
        x86_64) asset="gitleaks_${GITLEAKS_VERSION}_linux_x64.tar.gz" ;;
        aarch64) asset="gitleaks_${GITLEAKS_VERSION}_linux_arm64.tar.gz" ;;
        *) echo "install-security-tools: unsupported arch $(arch) for gitleaks" >&2; exit 1 ;;
    esac
    curl -sSfL "https://github.com/gitleaks/gitleaks/releases/download/v${GITLEAKS_VERSION}/${asset}" \
        | tar -xz -C "$BIN_DIR" gitleaks
    chmod +x "$BIN_DIR/gitleaks"
    gitleaks version
}

install_osv_scanner() {
    local asset
    case "$(arch)" in
        x86_64) asset="osv-scanner_linux_amd64" ;;
        aarch64) asset="osv-scanner_linux_arm64" ;;
        *) echo "install-security-tools: unsupported arch $(arch) for osv-scanner" >&2; exit 1 ;;
    esac
    curl -sSfL -o "$BIN_DIR/osv-scanner" \
        "https://github.com/google/osv-scanner/releases/download/v${OSV_SCANNER_VERSION}/${asset}"
    chmod +x "$BIN_DIR/osv-scanner"
    osv-scanner --version
}

install_semgrep() {
    if ! python3 -m pip --version >/dev/null 2>&1; then
        if command -v apt-get >/dev/null 2>&1; then
            apt-get update -qq && apt-get install -y -qq --no-install-recommends python3-pip >/dev/null
        else
            curl -sSfL https://bootstrap.pypa.io/get-pip.py -o /tmp/get-pip.py
            python3 /tmp/get-pip.py --quiet
        fi
    fi
    python3 -m pip install --break-system-packages --quiet "semgrep==${SEMGREP_VERSION}" \
        || python3 -m pip install --quiet "semgrep==${SEMGREP_VERSION}"
    semgrep --version
}

if [ "$#" -eq 0 ]; then
    echo "usage: install-security-tools.sh <gitleaks|osv-scanner|semgrep> [...]" >&2
    exit 2
fi

for tool in "$@"; do
    case "$tool" in
        gitleaks) install_gitleaks ;;
        osv-scanner) install_osv_scanner ;;
        semgrep) install_semgrep ;;
        *) echo "install-security-tools: unknown tool '$tool'" >&2; exit 2 ;;
    esac
done
