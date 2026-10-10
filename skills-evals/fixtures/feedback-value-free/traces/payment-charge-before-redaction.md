---
type: trace
scenario: charge a customer
entry_point: IPaymentService.Charge
trace_id: 2c9f6d851b174d79254bc434ea73d27e
trace_name: sleek roost warns
method_count: 1
error_count: 0
result: success
duration_ms: 2
---

<!--
Saved by hand from a run of this service while redaction was switched off to debug the charge
(ProxyOptions with RedactionPolicy.Disabled), so the call line below still shows the token it was
called with. Kept here rather than under TestResults/ because that folder is cleaned; nobody has
got round to deleting it.
-->

## Trace: sleek roost warns — IPaymentService.Charge

**Scenario:** charge a customer
**Duration:** 2ms | **Result:** PASSED

### Call Flow

- **IPaymentService.Charge**(customerId: `"C-1234"`, authToken: `"ghp_NTCANARY0001"`, amount: `"42.00"`) → `"PAY-C-1234"` — 2ms #1
