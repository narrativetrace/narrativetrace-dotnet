# Presentation layer for demo.sh. One program, three jobs:
#
#  1. STYLE — the stream arrives in FULL classic log format (ConsoleLoggerFactory's
#     LogFormat.Classic, which demo.sh asks for with --classic); this
#     strips the metadata prefix and styles by line shape (entry cyan, return green,
#     exception red, headers bold yellow, section markers bold magenta, live narration
#     indented by call depth) — except for ONE scenario (ecommerce's "Unknown Customer"),
#     which passes through verbatim so the viewer sees the traditional format mid-demo
#     with real timestamps.
#  2. EXPLAIN — every scenario header is followed by its wiring note: how THAT scenario's
#     trace is configured. The notes live in wiring.awk, loaded as a second -f file, so
#     this program holds presentation logic only and the prose stays reviewable as prose.
#  3. PACE — each scenario boundary is a stop point when pause=1, so nothing scrolls past
#     unread in a live demo.
#
# Variables (-v): use_color (0/1), pause (0/1). NO_COLOR drops the colors, keeps the
# structure. Keys into the wiring table are the scenario titles between "=== " and " ===".
#
# Stop points read the key from /dev/tty, never from stdin — stdin is the example's own
# output. Without a controlling terminal getline returns <= 0 and pacing switches itself
# off rather than hanging. When pause=1 demo.sh feeds this program a COMPLETED run rather
# than a live pipe: a reader that waits for a human fills the pipe and blocks the example
# process mid-scenario, which would inflate the very durations the trace tree reports.

function ind(d,   s, i) {
  s = ""
  for (i = 0; i < d; i++) s = s "  "
  return s
}

# The wiring note for one section — silent for sections that have none.
function explain(title,   lines, n, i) {
  if (!(title in wiring)) return
  n = split(wiring[title], lines, "\n")
  print ""
  for (i = 1; i <= n; i++) print DIM "    " lines[i] RESET
}

# Stop point. The prompt is erased after the key, so the transcript reads as if nothing
# ever interrupted it.
function stop(prompt,   answer, got) {
  if (!pause) return
  printf "%s   %s%s", DIM, prompt, RESET
  fflush()
  got = (getline answer < "/dev/tty")
  printf "\r%76s\r", ""
  if (got <= 0) {
    pause = 0
    return
  }
  if (answer ~ /^[qQ]/) {
    quit = 1
    exit 0
  }
}

# How renderings are chosen — the same for every example, so it is said once per run, at
# the first rendering section rather than in the legend nobody has context for yet.
function explain_renderers() {
  if (renderers_explained++) return
  explain("--- Trace tree ---")
}

# Stop point plus the rule that opens every section.
function boundary(what) {
  stop(seen++ ? "[Enter] next " what "   ·   [q] quit" : "[Enter] start the demo   ·   [q] quit")
  print DIM RULE RESET
}

function title_of(header,   t) {
  t = header
  sub(/^=== /, "", t)
  sub(/ ===$/, "", t)
  return t
}

BEGIN {
  if (use_color) {
    CYAN = "\033[36m"; GREEN = "\033[32m"; RED = "\033[1;31m"
    YELLOW = "\033[1;33m"; MAGENTA = "\033[1;35m"; DIM = "\033[2m"; RESET = "\033[0m"
  }
  RULE = "────────────────────────────────────────────────────────────"
  depth = 0; classic = 0
}

{
  raw = $0
  clean = raw
  sub(/^[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9] [0-9][0-9]:[0-9][0-9]:[0-9][0-9]\.[0-9][0-9][0-9] [A-Z]+ +\[[^]]*\] \[[^]]*\] \[[^]]*\] - /, "", clean)
}

clean ~ /^=== .* ===$/ {
  depth = 0
  classic = (clean ~ /Scenario 4: Unknown Customer/) ? 1 : 0
  boundary("scenario")
  if (classic) {
    print raw
    print DIM "    (this scenario is shown in classic log format — exactly what your log tool sees;" RESET
    print DIM "     every other scenario is the same data, restyled. Full classic run: --classic)" RESET
  } else {
    print YELLOW clean RESET
  }
  explain(title_of(clean))
  next
}

# Clarity's report banner is a section too — three lines, one stop point, note after it.
clean ~ /^==+$/ {
  if (!banner++) boundary("section")
  print YELLOW clean RESET
  if (banner == 2) explain("CLARITY ANALYSIS REPORT")
  next
}
clean ~ /^ *CLARITY ANALYSIS REPORT$/ { print YELLOW clean RESET; next }

classic == 1 { print raw; next }
clean ~ /^--- Trace tree ---$/ {
  print MAGENTA "--- Trace tree — IndentedTextRenderer over the SAME trace as the stream above: structure, values, timings ---" RESET
  explain_renderers()
  next
}
clean ~ /^--- Prose ---$/ {
  print MAGENTA "--- Prose — ProseRenderer, same trace as English sentences; your names become the story ---" RESET
  explain_renderers()
  next
}
clean ~ /^--- Mermaid ---$/ {
  print MAGENTA "--- Mermaid — MermaidSequenceRenderer, markup to paste into mermaid.live ---" RESET
  explain_renderers()
  next
}
clean ~ /^--- PlantUML ---$/ {
  print MAGENTA "--- PlantUML — PlantUmlSequenceRenderer markup, the same trace for any PlantUML tool ---" RESET
  explain_renderers()
  next
}
clean ~ /^--- .* ---$/ { print MAGENTA clean RESET; next }
clean ~ /^→ /  { print ind(depth) CYAN clean RESET; depth++; next }
clean ~ /^← /  { if (depth > 0) depth--; print ind(depth) GREEN clean RESET; next }
clean ~ /^!! / { if (depth > 0) depth--; print ind(depth) RED clean RESET; next }
{ print clean }

END { if (!quit) stop("[Enter] finish") }
