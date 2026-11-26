# S02-W05-T01 — authority-side CardCut

`CardCut.Rotate` is the pure shared-core operation for an already shuffled deck and an index in `1..51`. It returns a separate array with `deck[k:] + deck[:k]`, validates the complete 52-card permutation, and does not change its input. The E-04 shuffle calls this operation after it determines a cut index; the cutter is only expected to submit the index, with no deck in the player-facing contract.

Smoke checks cover all 51 legal indices, exact order, original deck immutability, and malformed inputs. The approved E-04 golden vector still checks the final shuffled and cut deck. Cutter authorization, single submission, deadline handling and persistence belong to subsequent S02-W05 tasks; this task does not claim those behaviors.
