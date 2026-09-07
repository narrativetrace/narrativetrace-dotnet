#!/usr/bin/env pwsh
# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
# NarrativeTrace demo launcher for Windows — a thin wrapper, not a rewrite.
#
#   ./demo.ps1                          interactive example picker
#   ./demo.ps1 -Example ecommerce       non-interactive
#   ./demo.ps1 -Example ecommerce -Classic   traditional timestamped logs
#   ./demo.ps1 -Example ecommerce -Lang es   the same run re-rendered via the glossary
#   ./demo.ps1 -List                    list available examples
#
# What it does: build the example quietly, run it, and print its output — the
# scenario headers, the live → ← !! lines, and every rendering section; with
# -Lang, the same run re-rendered through the example's committed glossary,
# scenario by scenario, with the glossary-gaps footer. What it does not do
# (yet): the colorized, paced walk with per-scenario wiring notes, which lives
# in the awk programs under examples/demo and is what demo.sh gives you on a
# Unix shell (Git Bash and WSL both run demo.sh unchanged). This script was
# written without a Windows box to test on; keep it simple and report problems
# rather than extending it.
[CmdletBinding()]
param(
    [ValidateSet('ecommerce', 'clarity', 'minecraft', 'library')]
    [string] $Example,
    [switch] $Classic,
    [switch] $NoPause,
    [switch] $List,
    [string] $Lang
)
$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

$examples = @('ecommerce', 'clarity', 'minecraft', 'library')
if ($List) { $examples | ForEach-Object { Write-Output $_ }; exit 0 }

if (-not $Example) {
    Write-Host 'Which example? (the trace narrates as the code runs)'
    for ($i = 0; $i -lt $examples.Count; $i++) { Write-Host ("  {0}) {1}" -f ($i + 1), $examples[$i]) }
    $choice = Read-Host '#?'
    $index = 0
    if (-not [int]::TryParse($choice, [ref] $index) -or $index -lt 1 -or $index -gt $examples.Count) {
        Write-Error "not an example number: $choice"
        exit 2
    }
    $Example = $examples[$index - 1]
}

$projects = @{
    ecommerce = 'examples/NarrativeTrace.Examples.ECommerce'
    clarity   = 'examples/NarrativeTrace.Examples.Clarity'
    minecraft = 'examples/NarrativeTrace.Examples.Minecraft'
    library   = 'examples/NarrativeTrace.Examples.Library'
}
$project = $projects[$Example]
$glossaryFile = Join-Path $project 'glossary.json'

# Supported locales are the example glossary's own, intersected with the
# scaffolding bundles the library ships (es, zh-CN): a locale with neither
# would render English scaffolding and an all-gaps footer, which is not a demo.
function Get-SupportedLanguages {
    $supported = @('en')
    if (Test-Path $glossaryFile) {
        $text = Get-Content -Raw -Path $glossaryFile
        foreach ($candidate in @('es', 'zh-CN')) {
            if ($text.Contains("`"$candidate`":")) { $supported += $candidate }
        }
    }
    return $supported
}

if (-not $Lang) { $Lang = 'en' }
$supported = Get-SupportedLanguages
if ($supported -notcontains $Lang) {
    Write-Error "language '$Lang' is not in $Example's glossary (supported: $($supported -join ' '))"
    exit 2
}
if ($Lang -ne 'en' -and $Classic) {
    Write-Error '-Classic replays raw log output; it has no translated variant. Drop one of the switches.'
    exit 2
}

Write-Host "Building $Example (quiet, one-time)..."
$buildLog = dotnet build $project -c Release --nologo -v q 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed for ${Example}:"
    $buildLog | ForEach-Object { Write-Host $_ }
    exit 1
}

# -Lang: the run renders its own translated scenario files in-process
# (DemoTraces sends each scenario's trace through the committed glossary, the
# same rendering the live TranslationSubscriber uses); this script then walks
# them. Values, return values and exception messages are byte-identical to the
# English run — only glossary-covered identifiers and templates change.
if ($Lang -ne 'en') {
    $work = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
    $traces = Join-Path $work "traces-$Lang"
    try {
        $env:NARRATIVETRACE_DEMO_TRANSLATION_DIR = $traces
        $env:NARRATIVETRACE_DEMO_LOCALE = $Lang
        $env:NARRATIVETRACE_GLOSSARY_PATH = (Resolve-Path $glossaryFile).Path
        Write-Host "Running $Example (rendered in '$Lang' as it goes)..."
        $runLog = dotnet run --no-build -c Release --project $project 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "${Example} failed:"
            $runLog | ForEach-Object { Write-Host $_ }
            exit 1
        }

        $files = @()
        if (Test-Path $traces) { $files = Get-ChildItem -Path $traces -Filter '*.md' | Sort-Object Name }
        if ($files.Count -eq 0) {
            Write-Error 'no translated traces were produced'
            exit 1
        }

        Write-Host ''
        Write-Host "The same run, re-rendered in '$Lang' from $glossaryFile."
        Write-Host 'Translated identifiers keep the original in parentheses so the canonical log stays greppable;'
        Write-Host 'the glossary-gaps footer lists the phrases the glossary does not cover yet — that list IS the curation queue.'
        foreach ($file in $files) {
            $lines = Get-Content -Path $file.FullName
            Write-Host ''
            Write-Host $lines[0]
            if ($lines.Count -gt 2) { $lines[2..($lines.Count - 1)] | ForEach-Object { Write-Host $_ } }
        }
        Write-Host ''
        Write-Host "Tip: ./demo.ps1 -Example $Example compares this with the English run."
        exit 0
    }
    finally {
        $env:NARRATIVETRACE_DEMO_TRANSLATION_DIR = $null
        $env:NARRATIVETRACE_DEMO_LOCALE = $null
        $env:NARRATIVETRACE_GLOSSARY_PATH = $null
        if (Test-Path $work) { Remove-Item -Recurse -Force $work }
    }
}

$arguments = @()
if ($Classic) {
    Write-Host 'Classic log format: same run, the traditional line every log tool shows — date, level, [thread], [traceId], [logger].'
    Write-Host ''
    $arguments += '--classic'
}

& dotnet run --no-build -c Release --project $project -- @arguments
exit $LASTEXITCODE
