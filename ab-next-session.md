# Next session

**Updated:** 2026-09-12 · **main** @ cffcb58

## Where things stand

StarterModel is a road deterioration model that will be run for a real network, and is
also the model Lonrix will hand to every new client to clone from. Its project structure
is now sound — one name throughout, framework referenced from `refs\`, builds clean with
no warnings. Its C# is still the logic of the model it was cloned from, and does not yet
match the new `domain_model_setup.xlsx`, which is the specification it has to be refactored
towards.

## Start here

Refactor the Initialiser so the parameters it sets match the ones the setup file declares.
It is first in the order you chose and everything downstream reads what it writes — but
wait for your own sub-model document before touching the deterioration maths. Open
`Objects\Initialiser.cs`.

## Also live

- The sub-model spec for rutting, roughness and cracking is yours to write — you planned
  to start it on 13 September, and it replaces the deterioration code that is in there now.
- Twelve `Rehab_CS_*` / `Rehab_AC_*` treatments are declared in the setup file and produced
  by nothing. Deliberately left until the treatments trigger stage.
- `lkp_unit-rates` is missing from the lookups file. Also deferred to the trigger stage.

## Detail lives in

- [StarterModel.md](../JCassDomainModelAssistant-main/model-knowledge/StarterModel.md) — the
  decisions made, the refactor order, and why the setup-vs-C# mismatches are expected
