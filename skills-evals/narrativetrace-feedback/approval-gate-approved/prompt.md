The doctor reports `trap.redaction-proof` on this project — "no test asserts [REDACTED]" — but the
project is configured correctly. `PaymentServiceRedactionTests` passes and does assert the marker:
it asserts it through the library's own public constant, `RedactionPolicy.Marker`, instead of
retyping the text of it. I think the check itself is wrong.

Please use the `narrativetrace-feedback` skill to report that to the maintainers.
