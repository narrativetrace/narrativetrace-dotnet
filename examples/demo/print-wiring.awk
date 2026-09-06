# Prints the wiring note for the scenario title passed as -v key, or nothing when the
# table has none. Loaded AFTER a wiring table (-f wiring-<locale>.awk -f print-wiring.awk):
# awk runs BEGIN blocks in file order, so the table is populated by the time this runs.
#
# The ecommerce async scenario captures two per-thread traces titled
# "Scenario 6: … (main thread)" / "(async thread)"; neither is a key of its own, so an
# exact miss retries with the trailing parenthetical stripped. Titles whose parenthetical
# IS part of the key (clarity's "(Excellent Naming)") hit exactly and never reach the
# fallback.
BEGIN {
  if (!(key in wiring)) sub(/ \([^)]*\)$/, "", key)
  if (key in wiring) print wiring[key]
}
