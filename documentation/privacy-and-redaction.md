# Privacy and Redaction

**English** | [Español](es/privacidad-y-ocultacion.md) | [Português](pt-BR/privacidade-e-ocultacao.md) | [简体中文](zh-CN/隐私与脱敏.md)

NarrativeTrace runs inside your process and writes files your team will
share — CI artifacts, local trace output, whatever your logging pipeline
forwards. This page states exactly what is and isn't redacted, verified
against this runtime's code (not assumed, and not inherited from another
runtime), so you can decide whether it's safe for your data before you
wire it in.

## Redaction, surface by surface

Every shipped integration in this runtime renders parameter and return values
through the same engine (`ValueRenderer`, `NarrativeInterceptor`), which
always resolves to `RedactionPolicy.Default` — none of them expose a
configuration knob to turn it off:

| Surface | Can disable built-in redaction? | Why |
|---|---|---|
| `NarrativeTraceProxy.Create<T>` (raw `DispatchProxy` capture) | No | Renders every argument/return value with no `RenderOptions` argument at all — always `RedactionPolicy.Default`. |
| DI auto-wrap (`AddNarrativeTracing`) | No | Wraps with `NarrativeTraceProxy.Create` internally; `NarrativeTracingDiOptions` has no redaction field. |
| ASP.NET Core middleware | No | `NarrativeTraceOptions` has no redaction field; traces come from whatever proxy path produced them. |
| xUnit `NarrativeFixture` | No | No `RedactionPolicy`/`RenderOptions` parameter anywhere in the type. |
| NUnit `NarrativeTestBase` | No | Same shape as the xUnit fixture. |
| `[Narrated]` / `[OnError]` template placeholders (`NarrationResolver`) | No | Fully static class, hardcoded to `RedactionPolicy.Default` — see the narrower gap below. |
| Canonical JSON / structural JSON projection | N/A — nothing to turn off | Consumes already-rendered (already-redacted) strings; the structural projection additionally elides every value unconditionally. |
| Structural `.nt` artifact | N/A — no values exist | `StructuralTraceRenderer` emits names, hierarchy and outcome kind only, never a value. |
| `dotnet-narrativetrace clarity-scan` | N/A — never reads values | Reflection-only over a `MetadataLoadContext`: it never constructs an instance or invokes anything, so there is no value to redact. |
| A custom `ValueRenderer.Render(value, options)` call in **your own code** | Yes | The only escape hatch in the codebase: pass `new RenderOptions(Redaction: RedactionPolicy.Disabled)` yourself. `[NotTraced]` still redacts even then. |

That last row is the one honest exception, and it is deliberate rather than
an oversight: `RedactionPolicy.Disabled` exists as a library primitive
(`RedactionPolicy.Disabled = new([], valueShapesEnabled: false)`), but no
shipped integration wires it up. Reaching it means writing your own call to
`ValueRenderer.Render`/`RenderStructured` with an explicit `RenderOptions` —
a deliberate, reviewable act in your own source, never a flag or environment
variable.

## What the deny-list catches, and what outranks it

`RedactionPolicy.Default` matches 26 case-insensitive name patterns, not the
shorter "password, cvv, ssn, token, secret, authorization" set you might
guess from a quick skim of the FAQ:

```
password, passwd, secret, token, apikey, api_key, cvv, ssn, authorization,
credential, privatekey, private_key, cardnumber, card_number, jwt, cookie,
setcookie, set_cookie, sessionid, session_id, accountnumber, account_number,
routingnumber, routing_number, pan, iban
```

Matching is substring-based and deliberately biased toward over-redaction
(a pattern of `token` also catches `apiTokenValue`) — except `pan` and
`iban`, which would otherwise redact ordinary business fields
(`companyName`, `planId`, `spanCount`, `japaneseAddress`) as substrings, so
those two are matched on identifier-token boundaries instead.

Independent of any field name, a second axis recognizes three **value
shapes** and redacts them regardless of what the field is called: a JWT
(three base64url segments starting `eyJ`), a Luhn-valid 13–19 digit payment
card number, and an HTTP `Set-Cookie`-shaped string. This is why a field
named `data` or `note` still gets redacted when it happens to hold something
that looks like a card number or a bearer token.

`[NotTraced]` on a parameter, property, or field always wins, independent of
the deny-list:

- On a property/field, the value's getter is **never invoked at all** — the
  redaction check runs before reflection touches the member, not after.
- On a proxy parameter, the argument was already evaluated by the caller
  (unavoidable for a real method call), but NarrativeTrace itself never
  renders it — the marker is substituted before `ValueRenderer` ever sees it.
- It outranks a curated `ToString()` and a `[NarrativeSummary]` method on the
  *containing* member: redaction is checked before either is reached.
- Dictionary **keys** go through the same guarded path as values — a
  sensitively-named key is redacted the same way a sensitively-named
  property would be, never rendered via a bare `ToString()`.

The narrower, already-known gap: template placeholder resolution
(`[Narrated]`/`[OnError]`) is a fully static code path and always uses
`RedactionPolicy.Default`. If application code ever threads a *custom*
`RedactionPolicy` into `ValueRenderer` directly (the escape-hatch row
above, run in reverse — tightening rather than disabling), that custom
policy is honored everywhere `ValueRenderer` is called by hand, but **not**
inside `[Narrated]`/`[OnError]` templates, which keep using the default
deny-list regardless. No shipped integration threads a custom policy at
all today, so this only matters if you build directly against
`NarrativeTrace.Core`'s rendering API yourself.

## Bounds and escaping

Every rendered value is capped and sanitized, regardless of redaction:

| Control | Default |
|---|---|
| Max string length | 200 characters, then `"...(truncated)"`-style suffix |
| Max collection items | 5, then a `(N total)` summary |
| Max object keys | 5 |
| Max nesting depth | 4 |
| Cycle detection | Reference-identity, independent of depth |
| Control-character / log-injection escaping | Control characters and unpaired surrogates escaped as `\uXXXX` (quotes/backslashes are the serializer's job, not the renderer's) |

## Guarantees

- **Redaction is unconditional in every shipped integration.** `[NotTraced]`
  and the 26-pattern deny-list (plus the three value shapes) apply to every
  output path this runtime ships — proxy capture, DI auto-wrap, ASP.NET Core
  middleware, xUnit/NUnit test output, template placeholders, and every
  export format. No flag, environment variable, or MSBuild property turns
  them off.
- **The structural artifacts hold no runtime values at all.** Both the
  `.nt` text artifact and the opt-in `.structural.json` entry array strip
  every parameter value, return value, and exception message — a property
  test seeds hostile content into every value field of a captured entry and
  fails if any of it survives the projection.
- **A value's own `ToString()` is never trusted once its type has public
  state.** A record or class that exposes at least one public property or
  field is always rendered by reflecting over those members through the
  same redaction-checked path — a deny-listed or `[NotTraced]` member is
  substituted with the marker *before* anything reads its value, and the
  type's own `ToString()` is never invoked to produce the output, at any
  nesting depth. A hand-written `ToString()` that interpolates a
  deny-listed field directly (`return "Account[password=" + password +
  "]";`) cannot bypass this: the reflective walk renders the object
  instead, and that hand-written text is never reached. The only opt-in
  past the walk is `[NarrativeSummary]` on a member you named yourself —
  see the non-guarantee below. A type with genuinely no public members at
  all (nothing to walk) is the sole case where its own `ToString()` is
  used, and even then the result is sanitized and length-capped like every
  other string.
- **Rendering cannot fail your application.** Capture and rendering are
  exception-isolated at the three points that reach caller-written code: a
  throwing custom `ToString()`, a throwing `[NarrativeSummary]` member, and
  a throwing property/field getter reached during reflective introspection
  (including one named in a template). Each degrades *that one part* to a
  typed `<error: TypeName>` placeholder — the caught exception's own type
  name, e.g. `<error: InvalidOperationException>`, never its `.Message`
  (a message can carry the very value the render was protecting) — without
  aborting the call, the collection, or the trace. A throwing
  `[NarrativeSummary]` degrades the same way rather than falling back to
  the type's fields: the summary was curated precisely so the fields
  wouldn't be shown raw. This is a `try`/`catch` guard, not a stack-depth
  guard — a `StackOverflowException` from a recursing `ToString()` is an
  unmanaged CLR fault no managed handler can catch, so it is never reached
  through this path. What actually stops a recursing type is the
  reflective walk's `MaxDepth` cap and its reference-identity cycle guard,
  which is also why a type's own `ToString()` is only ever entered for a
  leaf value with no public members left to walk, never for the composite
  doing the recursing. A hostile enumerator or dictionary is a separate,
  still-guarded hazard outside those three named extension points, and
  degrades to a bare `<error>` rather than the typed form.
- **Resource use is bounded.** Length, collection size, object width, and
  nesting depth are all capped, with reference-identity cycle detection
  independent of depth.
- **Output cannot be forged.** Control characters and unpaired surrogates
  are escaped before a rendered value reaches a file, so a hostile value
  cannot inject a fake log line or corrupt Markdown/JSON/diagram syntax.
- **Test artifacts land somewhere ephemeral, not source control.** The xUnit
  and NUnit test integrations write on by default to
  `TestResults/narrativetrace/` under the test project — the same location
  `dotnet test`/Visual Studio/Rider already treat as disposable output, and
  which this repository's own `.gitignore` excludes. Set
  `NARRATIVETRACE_OUTPUT=false` to stop writing entirely, or
  `NARRATIVETRACE_OUTPUT_DIR` to redirect it; see
  [What to Commit](what-to-commit.md) for what's inside and whether any of
  it belongs in your own repository.

## Non-guarantees

- **No shipped integration lets you turn redaction off.** The only escape
  hatch is application code that constructs its own `ValueRenderer.Render`
  call with `RedactionPolicy.Disabled` — a deliberate act in your own
  source, never a configuration state. `[NotTraced]` still redacts even
  under `Disabled`.
- **Template placeholders don't honor a custom `RedactionPolicy`.**
  `[Narrated]`/`[OnError]` resolution always uses the default deny-list,
  even if you've threaded a custom policy into `ValueRenderer` elsewhere.
- **Detection is name- and shape-based, not statistical.** There is no
  entropy or "looks random" heuristic — a secret sitting in an innocuously
  named field with an unrecognized shape is not caught. This is a backstop,
  not a guarantee that every secret is found.
- **A `[NarrativeSummary]` member is trusted code, once you've named it.**
  This is the one deliberate exception to the guarantee above: annotating a
  member opts that type out of the reflective walk entirely, and the
  renderer shows only what the summary returns — unlike a plain
  `ToString()` (never trusted for a type with public state), a
  `[NarrativeSummary]` member *is* trusted, precisely because naming it was
  a deliberate act in your own source. If the summary itself echoes a
  field back, that is the summary you wrote choosing to expose it, the
  same trust model as a hand-written `ToString()` you'd read in a
  debugger — not a gap the renderer introduced. If it throws instead, the
  value degrades to the typed `<error: TypeName>` placeholder, never a
  leak of whatever it would have shown. Annotate the *member* with
  `[NotTraced]` instead if a type's own summary can't be trusted with a
  field.
- **No production baseline-comparison loop reads the structural artifact
  back yet.** The `.nt` file is deterministic and value-free by
  construction, but this runtime doesn't ship anything that diffs it against a
  previous run (see [What to Commit](what-to-commit.md)).

## What this page does not cover

- The full attribute contract (`[Narrated]`, `[OnError]`, `[NotTraced]`,
  `[NarrativeSummary]`, and the purity expectations on what they invoke) —
  see the [Annotations Guide](guides/annotations.md).
- Programmatic configuration of tracing levels and output — see the
  [Configuration Guide](guides/configuration.md).
- What happens when NarrativeTrace's own proxy stacks with another
  library's proxy or interceptor — see the [FAQ](../README.md#faq) in the
  root README.
