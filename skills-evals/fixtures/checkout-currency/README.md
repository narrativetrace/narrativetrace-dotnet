# checkout-currency

The `narrativetrace-debug` case's fixture (Phase 7 D6, Java milestone 2's cross-port item 6): a
billing checkout that charges the card in its own currency, NarrativeTrace already installed
(Proxy and Testing.Xunit from the local eval feed, the CLI as a local tool). Never copied into a
trial — the harness skips a fixture's root README.

- `CheckoutService.Checkout` issues the invoice in euro, reads the card's currency from
  `ICustomerDirectory`, converts with `ICurrencyConverter`, authorizes the converted amount and
  confirms. `RateTableConverter.Convert` — "keeping the amount exact until the last step", says its
  doc comment — rounds to whole units BEFORE moving to minor units: 45.99 EUR at 0.93 is charged
  4300 rappen instead of 4277. The defect is visible only as a value at one boundary:
  `ICurrencyConverter.Convert(euroCents: 4599, currency: "CHF") → 4300 #1.3`, its child
  `IExchangeRates.RateFor` returning the right `0.93`. The structural trace is identical before
  and after the fix.
- Every test passes as shipped: `RateTableConverterTests` only converts amounts that land on whole
  units, and `CheckoutFlowTests` drives a whole-euro order on a euro card.
- A red run writes the `.md` narrative and the diagram but keeps the last green `.nt`, so the
  failing reproduction's shape is read from its `.md` (Java milestone 2's cross-port item 3).
- Near misses the grader fails: a fix downstream (rounding the amount again in `CheckoutService` or
  the gateway — the converter still answers 4300); routing around the span (computing the charge
  without the converter — the structural delta moves).

Case: `narrativetrace-debug/debug-value-divergence`.
