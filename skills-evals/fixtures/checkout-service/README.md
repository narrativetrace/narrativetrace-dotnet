# checkout-service

The `narrativetrace-verify` cases' fixture (Phase 7 D6): a billing service whose checkout runs
through a third-party pipeline, NarrativeTrace already installed (Proxy and Testing.Xunit from the
local eval feed, the CLI as a local tool). Never copied into a trial — the harness skips a
fixture's root README, and this one names the trap.

- `Billing.CheckoutService.Checkout` delegates to `Contoso.Checkout.CheckoutPipeline.Run`, a
  COMPILED package (`skills-evals/fixture-packages/verify-fixture-dependency.bundle`, an encoded
  bundle `./build.sh SkillsEvalFeed` unpacks into a temp directory and packs into the feed — encoded
  because a trial agent can read this repository, and the first trial did read the plain source). The package documents
  `ICheckoutListener.OnCheckout` as "called once a checkout's payment has been confirmed" and
  `Run` as "takes the payment, then notifies every listener" — but it calls the listeners after
  `IPaymentGateway.Authorize` and BEFORE `IPaymentGateway.Confirm`. `Checkout.Compose` repeats the
  belief in a comment. The cause is outside the readable source on purpose (Java milestone 1's
  cross-port item 9: a careful model reads a readable cause and avoids it, and the trace then only
  confirms).
- The task asks for a receipt "once their payment is confirmed". The natural solution — a
  `ReceiptListener` registered beside `AuditListener` — passes every test while the structural
  trace of `CheckoutFlowTests.Customer_checks_out` shows `INotificationService.Send` before
  `IPaymentGateway.Confirm`. The fix sends the receipt after `pipeline.Run` returns.
- `LateFees.FeeFor` is a pure function with its own unit test — `verify-skip`'s subject.

Cases: `narrativetrace-verify/verify-unintended-interaction`, `narrativetrace-verify/verify-skip`.
