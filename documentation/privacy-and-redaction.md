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
through the same engine (`ValueRenderer`, `NarrativeInterceptor`). Most
resolve to `RedactionPolicy.Default` with no way to change it; the proxy path
and its two auto-wrap entry points accept a different policy
*(since 0.1.4)*:

| Surface | Can plug in a custom `RedactionPolicy`? | Why |
|---|---|---|
| `NarrativeTraceProxy.Create<T>` / `.Create` (raw `DispatchProxy` capture) | **Yes**, via `new ProxyOptions(Redaction: ...)` *(since 0.1.4)* | Threads the policy into every `ValueRenderer.Render` call this interceptor makes (parameters, nested object walks, return values) and into the top-level parameter-name decision alike — see [Configuration Guide §6](guides/configuration.md#6-redaction). Given explicitly, it *replaces* the default decision rather than widening it, so `RedactionPolicy.Disabled` here really disables name-based redaction end to end. |
| DI auto-wrap (`AddNarrativeTracing`) | **Yes**, via `NarrativeTracingDiOptions.Redaction` *(since 0.1.4)* | Threaded into the `ProxyOptions` each wrapped service is constructed with. Falls back to a `RedactionPolicy` registered by `AddNarrativeTrace` (below) in the same container when this call's own `Redaction` is left unset. |
| ASP.NET Core integration (`AddNarrativeTrace`) | **Yes**, via `NarrativeTraceOptions.Redaction` *(since 0.1.4)* | Registers the policy as a `RedactionPolicy` singleton in the same service collection, so `AddNarrativeTracing`'s auto-wrap — a separate package with no reference to this one — can resolve it as a fallback for every service it wraps in the same app. The middleware itself renders no values; this is what lets one policy, configured once, reach every proxy invoked over the course of a request. |
| xUnit `NarrativeFixture` | No | No `RedactionPolicy`/`RenderOptions` parameter anywhere in the type. |
| NUnit `NarrativeTestBase` | No | Same shape as the xUnit fixture. |
| `[Narrated]` / `[OnError]` template placeholders (`NarrationResolver`) | **Yes**, via the same proxy's `ProxyOptions.Redaction` *(since 0.1.4)* | Threaded through from `NarrativeInterceptor` into every `NarrationResolver.Resolve` call, both call sites (entry narration and exception-time error context). Any proxy with `Redaction` left unset still resolves through `RedactionPolicy.Default`. |
| Canonical JSON / structural JSON projection | N/A — nothing to turn off | Consumes already-rendered (already-redacted) strings; the structural projection additionally elides every value unconditionally. |
| Structural `.nt` artifact | N/A — no values exist | `StructuralTraceRenderer` emits names, hierarchy and outcome kind only, never a value. |
| `dotnet-narrativetrace clarity-scan` | N/A — never reads values | Reflection-only over a `MetadataLoadContext`: it never constructs an instance or invokes anything, so there is no value to redact. |
| A custom `ValueRenderer.Render(value, options)` call in **your own code** | Yes | Pass `new RenderOptions(Redaction: ...)` yourself. `[NotTraced]` still redacts even then. |
| Every surface above, additively | Yes, but only *widening* | `NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS` (comma-separated field-name patterns) is unioned into `RedactionPolicy.Default` itself at process start, so it reaches every surface in this table that still says "No" too — including the ones with no per-call hook. It can only add patterns, never remove or replace, and — being read into a `static readonly` field — must be set before anything in the process first touches `RedactionPolicy`. |

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

`[NotTraced]` has no METHOD-level meaning — matching the JVM edition, whose
`@NotTraced` has no `METHOD` target either — so it always names a parameter,
property, or record component, never a whole call. Applying it to a method
compiles, but `NarrativeTraceProxy.Create`/`.Create<T>` reject it at
proxy-creation time with an `InvalidOperationException` naming the attribute,
the offending method, and the fix *(since 0.1.4)*, rather than
leaving the misuse to a confusing failure the first time that method runs.

Template placeholder resolution (`[Narrated]`/`[OnError]`) now honors the
proxy's own effective policy too *(since 0.1.4)*: a
`[Narrated("issued {token}")]` template resolved on a proxy constructed
with `new ProxyOptions(Redaction: ...)` checks that policy — the name axis
on a bare `{token}` placeholder and on a `{obj.Property}` path alike — the
same "replace, not widen" contract `ProxyOptions.Redaction` documents.
`[NotTraced]` still wins regardless of policy. Leave `Redaction` unset and
templates fall back to `RedactionPolicy.Default`, exactly as before.

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

- **Redaction is unconditional by default in every shipped integration**,
  and stays that way unless application code deliberately opts a proxy out
  in its own source: `[NotTraced]` and the 26-pattern deny-list (plus the
  three value shapes) apply to every output path this runtime ships —
  proxy capture, DI auto-wrap, ASP.NET Core middleware, xUnit/NUnit test
  output, template placeholders, and every export format — unless that
  specific proxy was constructed with `new ProxyOptions(Redaction: ...)`.
  No flag, environment variable, or MSBuild property turns redaction off;
  only that one explicit, reviewable constructor argument can, and
  `[NotTraced]` still redacts even then.
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
  typed `<error: TypeName>` placeholder *(since 0.1.4)* — the
  caught exception's own type
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
- **A trace's name and a run's name carry no data.** Both are a
  deterministic three-word phrase derived from a random id (see
  [Configuration Guide §7](guides/configuration.md#7-logging-bridge-microsoftextensionslogging)
  *(since 0.1.4)*) — never from anything captured — so neither
  can leak a runtime value, and both stay out of the structural `.nt`
  artifact for the same reason everything else in it does.

## Non-guarantees

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
  value degrades to the typed `<error: TypeName>` placeholder *(since 0.1.4)*, never a
  leak of whatever it would have shown. Annotate the *member* with
  `[NotTraced]` instead if a type's own summary can't be trusted with a
  field.
- **No redaction of test *names*.** A test's display name is
  developer-authored text, and a `[Theory]`/data-driven test's name can
  interpolate its arguments into it. That name reaches the artifact
  *filename* (`equipment_can_be_found-002-find_tent.md` — the slugged
  label is what tells two invocations apart on disk), the run's
  `manifest.json`, and the heading of the value-carrying artifacts. No
  deny-list is consulted for any of them: a name is an identifier here,
  not a captured value. Keep secrets out of display-name templates — the
  value-free `.nt` header is the one place this is handled for you, by not
  using the display name at all (see
  [Structural Trace Format](structural-trace-format.md)).
- **Adopting an inbound `traceparent` trusts the caller** *(since 0.1.4)*. `NarrativeTraceMiddleware` and `NarrativeTraceConfig`'s
  seeding path both take the trace id and parent span id from a
  `traceparent` header or value at face value — there is no signature, no
  allow-list of trusted upstreams, and nothing here confirms the caller
  actually owns the trace it names. Neither field is confidential (a trace
  or span id carries no data of its own), so this is a trace-identity
  concern, not a leak: a hostile or misconfigured caller can make your
  service's spans appear under a trace id and parent it did not choose, at
  worst confusing correlation across services, never exposing a value this
  page redacts. `Traceparent.Parse` degrades a malformed or forbidden
  header to "start a fresh trace" rather than throwing, so a stranger's bad
  header cannot fail the request either way — see the [Configuration
  Guide, §1](guides/configuration.md#traceparent-seeding)
  for the seeding path and [§4](guides/configuration.md#4-aspnet-core) for
  the middleware.

## What this page does not cover

- The full attribute contract (`[Narrated]`, `[OnError]`, `[NotTraced]`,
  `[NarrativeSummary]`, and the purity expectations on what they invoke) —
  see the [Annotations Guide](guides/annotations.md).
- Programmatic configuration of tracing levels and output — see the
  [Configuration Guide](guides/configuration.md).
- What happens when NarrativeTrace's own proxy stacks with another
  library's proxy or interceptor — see the [FAQ](../README.md#faq) in the
  root README.
