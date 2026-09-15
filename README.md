# StarterModel

The Juno Cassandra **starter domain model** — the project a client's road deterioration model begins
from. Clone it, rename it, and change it to fit your network.

It builds clean with no warnings, so a *build* failure after you start editing is attributable to the
change you just made. `jcass-dm check` passes every rule it can apply locally.

**It has not yet been run against a real project.** Everything in it has been checked by reading the
code against the setup spreadsheets, which cannot catch a model that computes the wrong number. The
first thing to do with your copy is run it and watch the early periods.

---

## Read this before you run it for your own network

**The fitted coefficients that ship with this model are not yours, and they are not a default.** They
were fitted to one road network, by regression, and they encode that network's materials, climate,
traffic and construction history. Run them unchanged against a different network and the model will
complete, produce plausible-looking condition curves, and be wrong — with nothing reporting it. That
is the single most expensive mistake available in this repository.

They live in **ten CSV files in your project's `supporting\` folder**, uploaded on the web app's
Files page — not in this repository, and not in `lookups.xlsx`:

```
logistic_crack_onset_ac.csv    lognormal_crack_severity_ac.csv   lognormal_crack_below_ac.csv
logistic_crack_onset_cs.csv    lognormal_crack_severity_cs.csv   lognormal_crack_below_cs.csv
lognormal_rut_ac.csv           lognormal_iri_ac.csv
lognormal_rut_cs.csv           lognormal_iri_cs.csv
```

Each is a `term` / `estimate` table as an R fit writes it. The eight lognormal files each carry a
`(Sigma)` row — the residual standard deviation, which is what makes two segments of the same age and
traffic deteriorate differently. **Sigma is as network-specific as any slope**, and one carried over
from someone else's fit sets the spread of your entire forecast; the model refuses a lognormal file
without a positive one. The two cracking-onset files are logistic fits and have no sigma — onset
randomness is each segment's own uniform draw — so do not add one. The two below-threshold cracking
files additionally carry `(ZeroShare)`.

**What to do about it.** Refitting these is a statistical exercise on your own condition survey
history, not a modelling one, and it is not something to guess at. **Lonrix can carry out the refit
for you, or set your team up to do it** — talk to support@lonrix.com before your first real run. If a
refit introduces a covariate this model does not know how to evaluate, the model stops at setup and
names the file and the term, rather than ignoring it silently.

The model reads only four covariates: log of capped surface age, log of surveyed average daily
traffic, central deflection, and heavy vehicle percentage. A term absent from a fit contributes
nothing, which is how the same code serves fits of different shapes.

**The same warning applies, less sharply, to the judgement numbers in `lookups.xlsx`** — the
deterioration age cap, the cracking onset threshold, the chipseal rut growth rate, the treatment
trigger thresholds, the as-new values a rehabilitation imposes. Those are engineering judgement
rather than fitted values, they are all editable without touching C#, and the ones shipped here are
starting points. Every one of them carries a comment in the spreadsheet saying what it is for.

---

## How the model is shaped

**Five sub-models, fitted separately for two surface class groups** — `ac` (asphalt) and `cs`
(chipseal). Your `surf_class_group` lookup set maps each of your surface classes onto one of the two.

| Sub-model | Family | What it gives |
|---|---|---|
| Cracking onset | logistic | whether cracking has started |
| Cracking severity | lognormal | how much cracking, given it has started |
| Cracking below onset | lognormal | the sub-threshold distribution, for segments that have not started |
| Rutting | lognormal | rut depth in mm |
| Roughness | lognormal | IRI |

**They are level models, not increment models.** Each period recomputes condition from the segment's
age and its own persistent random draws — none of them is last period's value plus a rate. Nothing
accumulates, so nothing can drift. The one deliberate exception is described under *the two clocks*
below.

**Every segment keeps its own character for the life of its surfacing.** At year zero each level
model is inverted against the surveyed condition to recover the deviate that reproduces it, and that
deviate is then carried forward as a model parameter. This is what makes a segment that was worse
than its peers stay worse. The deviate is clamped to a configurable number of standard deviations, so
one extreme survey reading cannot lock a segment into fast deterioration for the whole run; a clamped
segment is recorded as clamped, because it does *not* reproduce its surveyed value.

**The inversion and the forward model must mirror each other term for term.** If you add anything to
the forward model outside the lognormal core, the inversion has to undo it, or year zero stops
matching the survey. This is the most common way to break the model subtly.

**Surveyed traffic and modelled traffic are different properties and are not interchangeable.**
Average daily traffic grows every period; the fitted models read the *surveyed* value, frozen at the
measurement, because that is what they were fitted against. Feeding a grown covariate into a fitted
relationship is extrapolation the fit never sanctioned. Only terms that are not part of any fit read
the modelled value — currently just the heavy-traffic adjustment on chipseal rut growth.

**Each segment draws from its own random stream**, derived from the run seed, the element index and
the period. Drawing from one shared generator would make every segment depend on how many draws the
segments before it happened to take, so adding a draw anywhere would change the character of the
whole network downstream of it. Change the run seed and the whole network changes, which is what a
seed is for.

## The two clocks

**Surface age** is the clock every condition model runs on. A resurfacing returns it to zero.

**Accumulated chipseal rut growth** is a second clock and behaves differently on purpose. The model
assumes a chipseal follows the shape of what it is laid on and *inherits* the rut rather than
renewing it, so this one survives a resurfacing and only a rehabilitation returns it to zero. It
starts at zero at year zero — not at the value implied by the surface age, because the chipseal rut
model carries no age term and already reproduces today's survey, so seeding it from the age would
count the same growth twice.

It **holds millimetres already earned, not a count of years**, and that is load-bearing rather than
cosmetic. The growth rate behind it is scaled by the segment's heavy vehicle count, so storing a year
count and pricing it at today's rate would re-price everything the segment had ever accumulated each
time its traffic moved. Adding each period's millimetres as they are earned confines a traffic change
to the year it happens in, and makes the forecast independent of how wide you set the traffic band.

**It advances after the traffic growth, in both the no-treatment path and the treatment path.** The
two must stay in step, or a treated segment accumulates on different traffic from an untreated one,
with nothing reporting it.

## Treatments and resets

Seventeen treatments, named once in `TreatmentNames.cs` and referred to by that constant everywhere
else. Three surfacing families — chipseal, asphalt and OGPA — plus repairs and rehabilitation.

Every treatment needs **five** things, and four of them fail silently if you forget:

1. a constant in `TreatmentNames.cs`;
2. a row on the bundle's `treatments` sheet;
3. a trigger that can produce it;
4. a case arm in the resetter, saying what it does to the segment;
5. rates and surfacing properties in `lookups.xlsx`.

`jcass-dm check` catches the mismatches between 1, 2 and 4. Nothing catches a missing trigger, and a
treatment that never triggers looks exactly like one that is never the best option.

**Three kinds of reset, and what separates them is the clock.** A *rehabilitation* starts the pavement
again: it imposes as-new condition values, redraws the persistent deviates, and zeroes the rut
accumulator. A *resurfacing* returns the surface age to zero and leaves the pavement state alone. A
*pre-repair* — a treatment that repairs defects without laying new surface — must not move the surface
age at all; it earns a credit against the persistent deviates instead, which decays with time and is
guarded so that a repair plus an overlay cannot out-perform a rebuild.

Because the fitted models have never seen a reconstructed pavement, a rehabilitation is the one place
condition values are imposed rather than modelled, and the segment carries a permanent offset
afterwards so that the next period does not simply pull it back.

## Where every number lives

| Kind of number | Where it goes | Why |
|---|---|---|
| Fitted coefficients and sigmas | `supporting\*.csv` in your project | Regenerated as a whole set by a refit; never edited one at a time |
| Tunable judgement values — thresholds, caps, rates, as-new values | `lookups.xlsx` at project level | A modeller changes these without a rebuild |
| Unit rates | the `lkp_unit_rates` sheet of `lookups.xlsx` | One place for every treatment's cost |
| Structure — which models exist, what resets what | C# | Changing it is a code change, and should look like one |

**Nothing engineering-related belongs hard-coded in C#.** If you find yourself typing a rate, a
threshold or a trigger age into a `.cs` file, it belongs in `lookups.xlsx` instead.

## Things that look like details and are not

- **A treatment priced at zero in the rates sheet is free, and quietly wins every budget it competes
  for.** Nothing guards this — check your rates before reading any forecast.
- **The treatment trigger thresholds shipped here were calibrated against a different distress index
  and do not transfer.** Recalibrate them against your own network before using the output.
- **A budget category named in C# but absent from your `budgets.xlsx` ends the run mid-way**, and the
  error names nothing useful. The web app's Check Setup page compares them; `jcass-dm check` cannot.
- **The four names must match exactly**: the `.csproj` filename, the assembly name (inherited from it,
  so `<AssemblyName>` stays unset), the entry class, and `main_dll` / `main_class` on the bundle's
  `meta` sheet. Nothing warns you when they drift — the model runs normally until it fails with
  *"class not found in the specified .dll"*.
- **A model parameter's declared decimal places round the stored value every period.** A parameter
  that accumulates a small quantity and is declared with zero decimals stays at zero forever.

## What is here

| | |
|---|---|
| `StarterModel.csproj` | The project. References the framework from `refs\*.dll` — never by project reference |
| `StarterModel.sln` | Open this in Visual Studio |
| `Objects\StarterModel.cs` | The entry class, `StarterModel : DomainModelBase` |
| `Objects\RoadSegment.cs` | The element class — one road segment. Rename it freely; it is not part of the four-name rule |
| `Objects\DeteriorationModels.cs` | The five sub-models, the year-zero inversions and the pre-repair guards |
| `Objects\DeteriorationCoefficients.cs` | Loads the ten fitted coefficient files at setup |
| `Objects\Constants.cs` | Everything read from `lookups.xlsx`, validated once at setup |
| `Objects\Incrementer.cs` / `Objects\Resetter.cs` | One period with no treatment, and one with |
| `Objects\TreatmentsTrigger.cs` | Which treatments a segment is a candidate for |
| `domain_model_setup.xlsx` | The bundle: `meta`, `input_headers`, `parameters`, `treatments`, `network_functions` |
| `refs\` | Framework reference assemblies and their XML documentation. Nothing here is yours, and nothing here is ever uploaded |

## Renaming it

Do not do it by hand — four strings have to stay identical. From your Assistant folder:

```powershell
.\tools\jcass-dm.exe rename YourModelName --project ..\YourModelFolder --namespace
```

## Checking your work

```powershell
.\tools\jcass-dm.exe check --lookups <path to your inputs\lookups.xlsx>
```

This is a **local subset**. It reads your project file, your C# and the bundle, and it reads the C# as
text rather than compiling it. A green result means "nothing locally visible is wrong", not "this will
run". The web app's **Check Setup** page is authoritative: it sees your input CSV, your budget columns
and your configuration, none of which are visible from here.

---

Questions about refitting the deterioration models, or about anything the framework does that the
documentation does not cover: **support@lonrix.com**.
