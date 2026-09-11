# The hostile corpus

Eight JSON fixtures describing input that a NarrativeTrace runtime does not
control: values a traced method returned, headers a stranger sent, templates an
author wrote, object graphs a third-party DTO produced, call-tree shapes a
hand-built or replayed `TraceTree` can carry, instruction-shaped text aimed at
whatever reads the narrative afterwards, names a test class or method hands to
the artifact writer, and the sensitive-field vocabulary and national-id value
shapes the redaction policy must recognize.

**This directory is the cross-runtime corpus.** Every NarrativeTrace runtime
copies these files verbatim — the way the conformance schemas are copied — so
the same case hits all five renderers. A newly-understood attack shape is added
*here*, once, and every runtime gains it on the next sync. Nothing in these
files is Java-specific: they are data, and the builder that turns a declarative
graph shape into a live object graph is the only per-runtime code.

Adding a case: append an object to the relevant array, give it a stable
kebab-case `id` and a `description` that says *what breaks* rather than what the
bytes are, and keep the file ASCII — every non-ASCII character is written as a
`\uXXXX` escape so the fixture reads the same in every editor and diff.

## The files

| File | Feeds | What it holds |
|---|---|---|
| `strings.json` | the value renderer, every output format | hostile scalar values: control characters, bidi and zero-width, combining sequences, unpaired surrogates, template lookalikes, JSON/Mermaid/Markdown/YAML metacharacters, values up to 1 MiB |
| `headers.json` | `Traceparent` and any other wire reader | W3C `traceparent` and `tracestate` values: wrong lengths, non-hex, all-zero ids, version `ff`, trailing garbage, embedded CRLF, oversize |
| `templates.json` | `TemplateParser` and `RedactedPaths` | `@Narrated`/`@OnError` templates: nesting, unterminated braces, paths into redacted members at every depth, 50-segment paths, unicode identifiers |
| `graphs.json` | the value renderer | declarative object-graph *shapes*: depth, width, cycles, self-reference, `Optional`-in-`Map`-in-record chains, throwing/blocking/recursive `toString`, `hashCode` that throws, huge collections, standalone `Map.Entry`, `AtomicReferenceArray` |
| `tree-shapes.json` | every renderer/exporter, over the `TraceNode` call tree itself | declarative call-tree *shapes*: a legitimate deep chain, and cyclic rings (`n: 1` is a self-holding node) — the shape `TraceNode.Children` itself can carry, not a value inside it, so kept separate from `graphs.json`, which is documented as feeding specifically the value renderer |
| `injection.json` | every output format, as an AI-consumer oracle | prompt-injection payloads arriving as captured values: override phrasings, role and turn markers, tool-call lookalikes, markdown-link exfiltration, fence and frontmatter terminators, Mermaid label terminators, homoglyph and zero-width variants |
| `names.json` | the artifact writer / output-directory resolver | names an artifact writer must survive: test class and method names that reach the filesystem — empty, whitespace, traversal, separators, control characters, unpaired surrogates, noncharacters, bidi overrides, and names at and past the 255-byte per-component limit |
| `redaction.json` | `RedactionPolicy` (both axes) | **MIRROR-OF** `narrative-trace-java`'s `narrativetrace-security-tests/src/test/resources/hostile-corpus/redaction.json` — the sensitive-field name vocabulary (es/pt/fr/zh, folded through NFD) and the national-id value shapes (RUT, CPF/CNPJ, DNI/NIE, NIR, Chinese resident id), each row a `name`+`canary` pair or a bare `value`, saying whether it must be `redacted` or stay `visible` — the false-positive half is what keeps the default switched on |

## Case shapes

Every case is an object with `id` and `description`. The remaining keys say how
to build the input.

**Literal value** — `value` holds it directly:

```json
{ "id": "lone-cr", "description": "a line break to YAML but not to a Java line reader", "value": "\r" }
```

**Generated value** — `repeat` builds a large one without a large fixture;
optional `prefix` and `suffix` bracket it. `prefix + unit x count + suffix`:

```json
{ "id": "long-1mib", "description": "one mebibyte in one value", "repeat": { "unit": "x", "count": 1048576 } }
```

**Declarative graph** (`graphs.json`) — either `layers`, a stack of wrappers
built outward around `payload` (index 0 is innermost, so
`["optional","map","record"]` is a record holding a map holding an `Optional`),
or `kind`, naming a shape a stack cannot express:

| `kind` | Built as |
|---|---|
| `repeatLayer` | `n` copies of `layer` stacked around `payload` |
| `width` | one `container` holding `n` elements, `payload` last |
| `cycle` | a ring of `n` holders; `n: 1` is a self-holding object |
| `selfInCollection` | a `container` that contains itself |
| `diamond` | one object reached twice by different paths — shared, not cyclic |
| `hostileMember` | an object whose `member` (`toString`, `hashCode`, `equals`, a getter, a record accessor, a field *name*) misbehaves |
| `manyFields` | an object with `n` fields |
| `emptyContainers` | every empty container, nested |
| `future` | a `Future` in the given `state` |
| `throwable` | an exception carrying `payload`, optionally with an `n`-deep cause chain |

`payload: "secret-record"` means the builder plants a record with a
`@NotTraced` component holding a **unique per-case sentinel token** at that
position. The redaction oracle then asserts the token appears in no byte of any
output, at any depth, in any format.

**Declarative call tree** (`tree-shapes.json`) — `kind` and `n` describe the
`TraceNode` forest itself, not a value inside it:

| `kind` | Built as |
|---|---|
| `chain` | a single linear call chain `n` nodes deep |
| `cycle` | a ring of `n` `TraceNode`s, each holding the next, the last holding the first; `n: 1` is a node that holds itself |

**Redaction case** (`redaction.json`) — a `name` case names a field and
carries the `canary` planted behind it (the payload is a one-entry map,
`{ name: canary }`, since a record component has to be a compile-time
identifier and these names are data); a `value` case is its own canary,
because the shape *is* the secret. `expect` is `"redacted"` when the canary
must appear in no byte of any output and `"visible"` when it must survive —
the false-positive half is the half that keeps the default switched on.

## The oracles these feed

Named here so every runtime implements the same ones. They are documented for readers
in `documentation/security-testing.md`.

1. **No uncaught exception** — a hostile input degrades, it does not propagate.
2. **Bounded time and size** — narration costs O(size); no input hangs, and no
   input produces unbounded output.
3. **Well-formedness** — JSON parses back and validates against
   `chapter-tree.schema.json`; Mermaid keeps balanced blocks and one line per
   statement; Markdown frontmatter parses as YAML.
4. **Redaction** — a sentinel behind `@NotTraced` reaches no output, at any
   depth, including through wrappers, `Map.Entry`, exception messages and any
   `toString` fallback path.
5. **Idempotence** — rendering the same input twice produces the same bytes.
6. **No thread or hook left behind** — no `narrative-trace-*` thread survives.
7. **AI-consumer containment** — an injection payload comes back as exactly one
   value when the output is parsed or lexed again; it never terminates the
   enclosing JSON string, Mermaid label, Markdown fence or frontmatter block.
8. **Sensitive-vocabulary containment** — a canary behind a redaction-case
   name or value shape reaches no byte of any output, at any nesting depth;
   a canary behind a false-positive name or value stays readable.
9. **Artifact-naming safety** — every corpus name writes every artifact
   without throwing, every artifact it produces stays inside the configured
   output directory, no path component it produces exceeds the filesystem's
   per-component byte limit, and resolving the same name twice always gives
   the same path.
10. **Redaction, through the real capture path** — the same `redaction.json`
    oracle as (8), replayed through the actual proxy/interceptor a caller
    invokes rather than a payload handed straight to the value renderer, and
    asserted on the captured `ParameterCapture` itself (not only on rendered
    text). A defect can live in the decision a capture path makes before any
    renderer runs — the name axis reaching field/property names but not a
    method *parameter* name was exactly such a defect, invisible to (8) for
    the library's entire life because every one of its cases starts one
    layer below where that decision is made. A `name` case's parameter name
    is corpus data (including accented, decomposed and CJK spellings), so
    it is synthesized into a real signature at runtime (`System.Reflection.
    Emit` in the .NET runtime, ASM in the java one) rather than written as a
    compile-time interface per case.
