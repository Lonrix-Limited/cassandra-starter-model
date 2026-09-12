# StarterModel

The Juno Cassandra **starter domain model** — the project a client's model begins from.

It is the V2 default road network model, renamed and stripped of everything specific to the
machine it was developed on. It builds clean with no warnings, so a *build* failure after you
start editing is attributable to the change you just made.

**It does not run yet.** The C# is still the cloned model's logic, and is being refactored to
match `domain_model_setup.xlsx`, which is the specification. Until that lands, `jcass-dm check`
reports three known mismatches: 31 parameters the bundle declares that nothing writes, twelve
`Rehab_*` treatments nothing produces, and a lookup set named `unit_rate_set` that is missing
from the project's `lookups.xlsx` — the last of which stops a run at setup. These are expected,
and they are not faults in your copy.

## What is here

| | |
|---|---|
| `StarterModel.csproj` | The project. References the framework from `refs\*.dll` — never by project reference |
| `StarterModel.sln` | Open this in Visual Studio |
| `Objects\StarterModel.cs` | The entry class, `StarterModel : DomainModelBase` |
| `Objects\RoadSegment.cs` | The element class — one road segment. Rename it freely; it is not part of the four-name rule |
| `domain_model_setup.xlsx` | The bundle: `meta`, `input_headers`, `parameters`, `treatments`, `network_functions` |
| `refs\` | Framework reference assemblies and their XML documentation. Nothing here is yours, and nothing here is ever uploaded |

## Before you rename anything

Four strings have to be identical: the `.csproj` filename, the assembly name (inherited from it,
so `<AssemblyName>` stays unset), the entry class, and `main_dll` / `main_class` on the bundle's
`meta` sheet. Nothing warns you when they drift apart — a model can run normally and then fail on
F5 with *"class not found in the specified .dll"*.

Do not do it by hand. From the Assistant folder:

```powershell
.\tools\jcass-dm.exe rename NewModelName --project ..\StarterModel --namespace
```

## Unit rates

Unit rates are not in this project. They belong in the mandatory `lookups.xlsx` in the client's
`inputs\` folder, so that every treatment's rate is set in one place at project level.

**Which sheet is not yet settled.** The C# here asks for a lookup set named `unit_rate_set`; an
earlier version of this file named a `lkp_unit-rates` sheet instead, and the `lookups.xlsx`
shipped alongside this model has neither. It is decided at the treatments-trigger stage of the
refactor.
