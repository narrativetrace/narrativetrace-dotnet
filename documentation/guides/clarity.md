# NarrativeTrace .NET — Clarity Guide

**English** | [Español](es/guia-de-claridad.md) | [Português](pt-BR/guia-de-clareza.md) | [简体中文](zh-CN/清晰度指南.md)

If the trace *is* the code, then trace quality is code quality. The
`NarrativeTrace.Clarity` package analyzes your method, class, and parameter
names and scores how well they communicate intent.

## Two ways to score

### From a captured trace (`ClarityAnalyzer`)

Analyze the names that actually appeared in a run:

```csharp
using NarrativeTrace.Clarity;

var result = ClarityAnalyzer.Analyze(context.CaptureTrace());
Console.WriteLine($"Overall clarity: {result.Overall:F2}");
```

### From an assembly, without running anything (`ClarityScanner`)

Reflection-only — inspects public methods of the given types without
executing them (safe for CI):

```csharp
IReadOnlyDictionary<string, ClarityResult> results =
    ClarityScanner.Scan([typeof(OrderService), typeof(PaymentService)]);

foreach (var (typeName, result) in results)
{
    Console.WriteLine($"{typeName}: {result.Overall:F2}");
}
```

Results are keyed by `Type.Name`. This is also what the
[`dotnet-narrativetrace` CLI](#build-enforcement) uses.

## What gets scored

`ClarityResult` carries one overall score plus five weighted components
(all `0.0`–`1.0`):

| Component | Weight | What it measures |
|---|---|---|
| Method (`Method`) | 30% | Verb quality and token specificity of method names. |
| Parameter (`Parameter`) | 25% | Domain specificity vs. generic/meaningless parameter tokens. |
| Class (`Class`) | 20% | Role-suffix quality and prefix specificity of class names. |
| Structural (`Structural`) | 15% | Parameter-count and call-depth penalties. |
| Cohesion (`Cohesion`) | 10% | Whether methods align with the class's role suffix. |

```csharp
public sealed record ClarityResult(
    double Overall, double Method, double Class,
    double Parameter, double Structural, double Cohesion,
    IReadOnlyList<ClarityIssue> Issues);
```

### The scoring intuition

- **Method names** — the first token is treated as a verb. *Domain* verbs
  (`calculate`, `validate`, `reserve`) score highest; *boolean* prefixes
  (`is`, `has`, `can`) score high; *standard* verbs (`create`, `find`)
  are middling; *generic* verbs (`get`, `process`, `handle`, `execute`)
  score lowest. Extra tokens add specificity (`reserveInventory` beats
  `reserve`).
- **Class names** — a domain prefix plus a functional suffix scores well
  (`OrderService`); a bare suffix (`Service`) or a generic `Manager`
  scores poorly.
- **Parameter names** — domain-specific (`customerId`, `checkInDate`)
  beats typed-generic (`id`, `count`) beats vague (`data`, `info`) beats
  meaningless (`x`, `tmp`).
- **Structural** — methods with many parameters or deep call chains are
  penalized.
- **Cohesion** — methods are checked against the verbs expected for the
  class's role suffix (a `Repository` is expected to `find`/`save`/`delete`).

## Issues

`ClarityResult.Issues` lists specific naming problems as
`ClarityIssue(Severity, Description, Suggestion)`. The analyzer currently
flags **generic-verb method names** as `high`-severity issues — for
example a method whose first token is `get`/`process`/`handle`:

```
severity:    high
description: Generic verb 'process' in DataProcessor.process
suggestion:  Use a domain-specific verb
```

Severity is a lowercase string token (`high`). `ClarityReportRenderer`
maps severities to icons for Markdown output.

## Your own vocabulary, from the glossary you already have

The built-in dictionaries know general software English. They do not know that
`Fold` is a verb in your domain, that `Tranche` is a precise noun, or that `Fx`
is your team's accepted shorthand for *foreign exchange* — and a name they do
not know scores as unknown, not as domain-specific.

You teach them with the vocabulary file your repository already carries: the
committed `glossary.json` (ADR-012). There is no second dictionary file to keep
in sync.

| Glossary entry | Kind | What clarity learns |
|---|---|---|
| `settle trade` | `verb-phrase` | `settle` is a domain verb; `trade` is a domain noun |
| `credit tranche` | `noun-phrase` | `credit` and `tranche` are domain nouns |
| `fx` | `word` | `fx` is a domain noun |

Multi-word terms teach one token at a time, because identifiers are scored one
token at a time. Every bounded context contributes: an identifier carries no
namespace, so context scoping cannot apply at scoring time.

### Accepted shorthand is declared, not inferred

Shorthand your team accepts lives in its own root-level section, which raises
the file to `schemaVersion: 2`:

```json
{
  "schemaVersion": 2,
  "contexts": { "trading": { "packages": ["Acme.Trading"] } },
  "abbreviations": { "fx": "foreign exchange", "calc": "calculate" },
  "terms": []
}
```

A listed token is never asked to be spelled out again, and the expansion is
what the teaching notes spell it out *with* — `noun 'fx' (foreign exchange)`.

Only that section accepts shorthand. A token that merely appears inside a
committed term (`calc` in `calc total`) is taught as a domain noun and stays an
abbreviation, because nobody decided it was shorthand. Accepting one is a
decision someone makes and reviews, not a side effect of a harvest.

Two file-format rules follow from that:

- The section is **human-owned** — a harvest never writes it, and a merge
  carries it through untouched, like `definition` and `translations`.
- The `2` stamp appears **only when the section has entries**, so a repository
  that never uses the feature keeps writing the byte-identical schema-1 file it
  wrote before. Readers accept the section at any version from 1 up.

### What the glossary cannot do

The built-in dictionaries keep their authority. A project can teach the scorers
a word they do not know; it cannot overrule a word they do.

- **Generic verbs stay generic.** Committing `process` or `handle` does not
  promote them, and the same holds for boolean prefixes (`is`, `has`).
- **Meaningless placeholders stay meaningless.** `temp`, `foo` and friends are
  not rescued by being written down.
- **Deprecated synonyms are never vocabulary.** An alias exists to be flagged;
  promoting it would silence the `non-canonical-term` issue it is declared for.
- **`stale` terms are not vocabulary.** Marking a term stale says the word left
  the domain.

Only the *committed* file counts. Nothing a run harvests feeds back into that
same run's scores — a self-expanding vocabulary would make scores
non-deterministic and self-certifying. The commit is the human approval.

### Where it applies

The xUnit fixture, the NUnit suite report and `dotnet-narrativetrace clarity-scan`
all find the glossary by the same upward search the harvest uses
(`GlossarySettings.ResolveFile`): `NARRATIVETRACE_GLOSSARY` names an explicit
path, `off` switches the feature off entirely, and otherwise the nearest
`glossary.json` above the working directory wins.

Reading is unconditional wherever a glossary exists — it changes nothing on
disk. A repository with no `glossary.json` scores exactly as it did before this
feature existed, and a glossary that cannot be read degrades to the built-in
dictionaries with a console note rather than failing the suite.

```csharp
var vocabulary = GlossaryVocabulary.FromFile(
    GlossarySettings.ResolveFile(
        Environment.GetEnvironmentVariable, Directory.GetCurrentDirectory()));

var result = ClarityAnalyzer.Analyze(tree, propertyNames, vocabulary);
```

## Report output

`ClarityReportRenderer.Render` produces a Markdown scores table across
scenarios:

```csharp
var report = ClarityReportRenderer.Render(
[
    new ScenarioClarity("Order placement", orderResult),
    new ScenarioClarity("Legacy processing", legacyResult),
]);
```

## Build enforcement

Clarity becomes a *gate*, not a suggestion, via the
`dotnet-narrativetrace` CLI (or the `NarrativeTrace.MSBuild` package that
wraps it).

```bash
# 1. Scan a built assembly into clarity-results.json:
dotnet-narrativetrace clarity-scan --assembly bin/Release/net10.0/MyApp.dll

# 2. Fail the build below a threshold or above a HIGH-issue budget:
dotnet-narrativetrace clarity-check --results clarity-results.json \
    --min-score 0.80 --max-high-issues 0
```

`clarity-check` exit codes: `0` pass (or `--warn-only`), `1` gate failure,
`2` usage error / malformed results. A missing results file is **skipped**
(exit 0), not failed — so a project with no scan yet won't break CI.

For MSBuild wiring (`ClarityScan` / `ClarityCheck` targets and the
`ClarityMinScore` / `ClarityMaxHighIssues` / `ClarityWarnOnly`
properties), see the [Configuration Guide](configuration.md#5-msbuild).

### JSON contract

`clarity-results.json` is the contract between the scan and the gate — an
array of scenario objects:

```json
[
  {
    "scenario": "OrderService",
    "overall": 0.85,
    "method": 0.90,
    "class": 0.95,
    "parameter": 0.80,
    "structural": 1.00,
    "cohesion": 0.70,
    "issues": [
      {
        "severity": "high",
        "description": "Generic verb 'process' in DataProcessor.process",
        "suggestion": "Use a domain-specific verb"
      }
    ]
  }
]
```

The gate counts HIGH-severity issues case-insensitively, so both `high`
(as the scanner emits) and `HIGH` are honored.

## NLP components

The clarity module uses hand-coded NLP with no external dependencies:

| Component | Purpose |
|---|---|
| `IdentifierTokenizer` | Splits `camelCase` / `snake_case` into tokens. |
| `VerbDictionary` | Categorizes verbs (`Domain`, `Standard`, `Boolean`, `Generic`, `Unknown`). |
| `RoleSuffixDictionary` | Classifies class suffixes (design-pattern, functional, generic). |
| `GenericTokenDetector` | Ranks token specificity (meaningless → domain-specific). |
| `AbbreviationDictionary` | Scores abbreviations by tier (universal, well-known, ambiguous). |
| `MorphologyAnalyzer` | Detects part of speech via suffixes (`-tion`, `-ize`, `-able`). |
| `CollocationDictionary` | Recognizes common multi-token domain phrases. |
| `CohesionScorer` | Checks method-verb alignment with the class role. |
| `MethodNameScorer` / `ClassNameScorer` / `ParameterNameScorer` / `StructuralScorer` | The five component scorers. |
| `DomainVocabulary` | The project's own words, read from the committed glossary; extends every dictionary above without overriding it. |

## See also

- [Installation Guide](installation.md) — the `dotnet-narrativetrace` tool
- [Configuration Guide](configuration.md) — the MSBuild clarity gate
- [Annotations Guide](annotations.md) — clean names first, attributes second
