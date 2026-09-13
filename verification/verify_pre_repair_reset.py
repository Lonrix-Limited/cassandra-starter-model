r"""
Numerical verification of the PRE-REPAIR RESET (specification section 9a).

WHY THIS SCRIPT EXISTS. The project does not compile as a whole yet - the objective value and
maintenance cost regions still name distresses that were deleted with the jFunction-era survey - so
the model cannot be run to check that its arithmetic does what the specification says. This script
re-implements the relevant parts of DeteriorationModels.cs in Python, against the SAME fitted
coefficient files the model reads, and checks the behaviour the specification states.

It is not a unit test of the C#. It is an independent implementation of the same equations, which is
the point: if the two disagree, one of them is wrong and the disagreement is visible.

HOW TO RUN IT

    python verification/verify_pre_repair_reset.py

It needs numpy and scipy, and it needs the client setup folder beside the model - by default
    ..\starter-model-setup\supporting\
Pass a different folder as the first argument.

Exit code 0 if every check passes, 1 otherwise.

WHAT IT DOES NOT TOUCH. It reads only the fitted coefficient CSVs, which are model parameters, never
the client's model_input_data.csv. The segments it runs are synthetic, built from values quoted in
the specification.
"""

import csv
import math
import os
import sys

from scipy.stats import norm

# ---------------------------------------------------------------------------------------------------
# Configuration - the same numbers the model reads from lookups.xlsx
# ---------------------------------------------------------------------------------------------------

AGE_OFFSET = 0.5            # deterioration.age_offset
AGE_CAP_YEARS = 30.0        # deterioration.age_cap_years
CRACK_ONSET_PCT = 1.0       # deterioration.crack_onset_pct
RUT_CS_GROWTH_PER_YEAR = 0.064   # deterioration.rut_cs_growth_mm_per_year
MULTIPLIER_CLAMP_SD = 2.0   # deterioration.multiplier_clamp_sd

FEEDBACK_CRACK_ON_RUT = 0.0
FEEDBACK_CRACK_ON_IRI = 0.0
FEEDBACK_RUT_ON_IRI = 0.0

REHAB_RESET = {"crack_pct": 0.0, "rut_mm": 2.0, "iri": 2.5}
REHAB_RESET_D0 = {"ac": 0.7225, "cs": 0.9090}
REHAB_OFFSET_RUT = {"ac": 0.0000, "cs": -0.6048}
REHAB_OFFSET_IRI = {"ac": -0.5738, "cs": -0.8268}
REHAB_OFFSET_CRACK_ONSET = {"ac": 0.0, "cs": 0.0}

# pre_repair, as shipped
PRE_REPAIR = {
    "max_extent_crack_pct": 20.0,
    "max_extent_rut_mm": 2.5,
    "max_extent_iri": 0.0,
    "retained_fraction": 0.4,
    "decay_tau_yrs": {"ac": 8.0, "cs": 4.0},
    "guard_factor": 1.25,
}

MIN_OBSERVABLE = 1e-6


# ---------------------------------------------------------------------------------------------------
# The fitted coefficients, read exactly as DeteriorationCoefficients.cs reads them
# ---------------------------------------------------------------------------------------------------

def canonicalise(term):
    term = term.strip()
    if term == "(Intercept)":
        return "intercept"
    if term == "(Sigma)":
        return "sigma"
    if term == "(ZeroShare)":
        return "zero_share"
    if term.lower().startswith("log(surf_age_yrs"):
        return "log_age"
    if term.lower().startswith("log(max(inp_adt"):
        return "log_adt"
    if term == "inp_lmd_d0_75th":
        return "d0"
    if term == "inp_heavy_perc":
        return "heavy_perc"
    raise ValueError("unrecognised term: " + term)


def load_model(folder, filename):
    path = os.path.join(folder, filename)
    coefficients = {}
    with open(path, newline="", encoding="utf-8-sig") as handle:
        for row in csv.DictReader(handle):
            coefficients[canonicalise(row["term"])] = float(row["estimate"])
    return coefficients


def linear_predictor(model, log_age, log_adt, d0, heavy_perc):
    return (model.get("intercept", 0.0)
            + model.get("log_age", 0.0) * log_age
            + model.get("log_adt", 0.0) * log_adt
            + model.get("d0", 0.0) * d0
            + model.get("heavy_perc", 0.0) * heavy_perc)


def load_all(folder):
    fits = {}
    for group in ("ac", "cs"):
        fits[("onset", group)] = load_model(folder, "logistic_crack_onset_%s.csv" % group)
        fits[("severity", group)] = load_model(folder, "lognormal_crack_severity_%s.csv" % group)
        fits[("below", group)] = load_model(folder, "lognormal_crack_below_%s.csv" % group)
        fits[("rut", group)] = load_model(folder, "lognormal_rut_%s.csv" % group)
        fits[("iri", group)] = load_model(folder, "lognormal_iri_%s.csv" % group)
    return fits


# ---------------------------------------------------------------------------------------------------
# The segment, and the model - a transcription of DeteriorationModels.cs
# ---------------------------------------------------------------------------------------------------

class Segment(object):
    def __init__(self, group, surf_age, adt, d0, heavy_perc):
        self.group = group
        self.surf_age = surf_age
        self.adt = adt
        self.d0 = d0
        self.heavy_perc = heavy_perc

        self.rut_growth_yrs = 0.0
        self.rehabilitated = False

        self.rut_z = 0.0
        self.iri_z = 0.0
        self.crack_u_onset = 0.0
        self.crack_w_sev = 0.0
        self.crack_below = 0.0

        self.prerep_dz_crack = 0.0
        self.prerep_dz_rut = 0.0
        self.prerep_dz_iri = 0.0
        self.prerep_yrs = 0.0

        self.cracking = 0.0
        self.rut = 0.0
        self.iri = 0.0

    def copy(self):
        other = Segment(self.group, self.surf_age, self.adt, self.d0, self.heavy_perc)
        other.__dict__.update(self.__dict__)
        return other


class Models(object):
    def __init__(self, fits):
        self.fits = fits

    # --- shared covariates -------------------------------------------------------------------------

    def log_age(self, surf_age):
        return math.log(min(max(0.0, surf_age), AGE_CAP_YEARS) + AGE_OFFSET)

    def log_traffic(self, adt):
        return math.log(max(adt, 1.0))

    def mu(self, kind, segment, surf_age):
        model = self.fits[(kind, segment.group)]
        value = linear_predictor(model, self.log_age(surf_age), self.log_traffic(segment.adt),
                                 segment.d0, segment.heavy_perc)
        if segment.rehabilitated:
            if kind == "rut":
                value += REHAB_OFFSET_RUT[segment.group]
            elif kind == "iri":
                value += REHAB_OFFSET_IRI[segment.group]
        return value

    def sigma(self, kind, segment):
        return self.fits[(kind, segment.group)]["sigma"]

    # --- the pre-repair credit ---------------------------------------------------------------------

    def decayed_credit(self, segment, credit_at_repair):
        if credit_at_repair <= 0.0:
            return 0.0
        retained = PRE_REPAIR["retained_fraction"]
        tau = PRE_REPAIR["decay_tau_yrs"][segment.group]
        years = max(0.0, segment.prerep_yrs)
        transient = math.exp(-years / tau) if tau > 0.0 else 0.0
        return credit_at_repair * (retained + (1.0 - retained) * transient)

    def credit_crack(self, segment):
        return self.decayed_credit(segment, segment.prerep_dz_crack)

    def credit_rut(self, segment):
        return self.decayed_credit(segment, segment.prerep_dz_rut)

    def credit_iri(self, segment):
        return self.decayed_credit(segment, segment.prerep_dz_iri)

    # --- cracking ----------------------------------------------------------------------------------

    def onset_probability(self, segment, surf_age):
        model = self.fits[("onset", segment.group)]
        eta = linear_predictor(model, self.log_age(surf_age), self.log_traffic(segment.adt),
                               segment.d0, segment.heavy_perc)
        if segment.rehabilitated:
            eta += REHAB_OFFSET_CRACK_ONSET[segment.group]
        return 1.0 / (1.0 + math.exp(-eta)) if eta >= 0.0 else math.exp(eta) / (1.0 + math.exp(eta))

    def truncation_point(self, mu, sigma):
        return norm.cdf((math.log(CRACK_ONSET_PCT) - mu) / sigma)

    def clamp_severity_deviate(self, truncation_point, quantile):
        truncation_deviate = norm.ppf(truncation_point)
        deviate = norm.ppf(truncation_point + quantile * (1.0 - truncation_point))
        return min(MULTIPLIER_CLAMP_SD, max(truncation_deviate, deviate))

    def get_cracking(self, segment, surf_age):
        if segment.crack_u_onset >= self.onset_probability(segment, surf_age):
            return segment.crack_below
        sigma = self.sigma("severity", segment)
        mu = self.mu("severity", segment, surf_age)
        deviate = (self.clamp_severity_deviate(self.truncation_point(mu, sigma), segment.crack_w_sev)
                   - self.credit_crack(segment))
        return math.exp(mu + sigma * deviate)

    # --- rutting and roughness ---------------------------------------------------------------------

    def chipseal_rut_growth(self, segment):
        return RUT_CS_GROWTH_PER_YEAR * max(0.0, segment.rut_growth_yrs)

    def get_rutting(self, segment, surf_age, cracking):
        rut = math.exp(self.mu("rut", segment, surf_age)
                       + self.sigma("rut", segment) * (segment.rut_z - self.credit_rut(segment)))
        if segment.group == "cs":
            rut += self.chipseal_rut_growth(segment)
        return rut * (1.0 + FEEDBACK_CRACK_ON_RUT * cracking / 100.0)

    def get_roughness(self, segment, surf_age, cracking, rut):
        iri = math.exp(self.mu("iri", segment, surf_age)
                       + self.sigma("iri", segment) * (segment.iri_z - self.credit_iri(segment)))
        iri *= (1.0 + FEEDBACK_CRACK_ON_IRI * cracking / 100.0)
        iri *= (1.0 + FEEDBACK_RUT_ON_IRI * rut / 10.0)
        return iri

    # --- inversions --------------------------------------------------------------------------------

    def raw_level_deviate(self, observed, mu, sigma):
        """The deviate before the clamp. Only the checks use this, to tell a clamped segment apart -
        a clamped segment deliberately does NOT reproduce its own reading, which is the cost of the
        clamp and is recorded in par_rut_init_src rather than being a defect."""
        return (math.log(max(observed, MIN_OBSERVABLE)) - mu) / sigma

    def invert_level(self, observed, mu, sigma):
        deviate = self.raw_level_deviate(observed, mu, sigma)
        return max(-MULTIPLIER_CLAMP_SD, min(MULTIPLIER_CLAMP_SD, deviate))

    def invert_rutting(self, segment, observed, cracking):
        target = observed / (1.0 + FEEDBACK_CRACK_ON_RUT * cracking / 100.0)
        if segment.group == "cs":
            target -= self.chipseal_rut_growth(segment)
        effective = self.invert_level(target, self.mu("rut", segment, segment.surf_age),
                                      self.sigma("rut", segment))
        return effective + self.credit_rut(segment)

    def invert_roughness(self, segment, observed, cracking, rut):
        target = observed / (1.0 + FEEDBACK_CRACK_ON_IRI * cracking / 100.0)
        target /= (1.0 + FEEDBACK_RUT_ON_IRI * rut / 10.0)
        effective = self.invert_level(target, self.mu("iri", segment, segment.surf_age),
                                      self.sigma("iri", segment))
        return effective + self.credit_iri(segment)

    def invert_cracking_above_onset(self, segment, observed, onset_quantile=0.5):
        surf_age = segment.surf_age
        sigma = self.sigma("severity", segment)
        mu = self.mu("severity", segment, surf_age)
        trunc = self.truncation_point(mu, sigma)
        onset_position = onset_quantile * self.onset_probability(segment, surf_age)
        observed_deviate = (math.log(max(observed, MIN_OBSERVABLE)) - mu) / sigma
        target_deviate = observed_deviate + self.credit_crack(segment)
        quantile = (norm.cdf(target_deviate) - trunc) / (1.0 - trunc)
        return onset_position, min(1.0, max(0.0, quantile))

    # --- the reset paths ---------------------------------------------------------------------------

    def apply_pre_repair_credits(self, segment):
        surf_age = segment.surf_age

        crack_credit = 0.0
        if segment.crack_u_onset < self.onset_probability(segment, surf_age):
            sigma = self.sigma("severity", segment)
            mu = self.mu("severity", segment, surf_age)
            trunc = self.truncation_point(mu, sigma)
            deviate_now = self.clamp_severity_deviate(trunc, segment.crack_w_sev)
            post = max(0.0, segment.cracking - PRE_REPAIR["max_extent_crack_pct"])
            deviate_post = max(-MULTIPLIER_CLAMP_SD,
                               (math.log(max(post, MIN_OBSERVABLE)) - mu) / sigma)
            crack_credit = max(0.0, deviate_now - deviate_post)

        rut_post = max(0.0, segment.rut - PRE_REPAIR["max_extent_rut_mm"])
        rut_deviate_post = self.invert_rutting(segment, rut_post, segment.cracking) - self.credit_rut(segment)
        rut_credit = max(0.0, segment.rut_z - rut_deviate_post)

        iri_post = max(0.0, segment.iri - PRE_REPAIR["max_extent_iri"])
        iri_deviate_post = (self.invert_roughness(segment, iri_post, segment.cracking, segment.rut)
                            - self.credit_iri(segment))
        iri_credit = max(0.0, segment.iri_z - iri_deviate_post)

        segment.prerep_dz_crack = crack_credit
        segment.prerep_dz_rut = rut_credit
        segment.prerep_dz_iri = iri_credit
        segment.prerep_yrs = 0.0

    def maximum_credit(self, stored, mu, sigma, core_floor):
        return stored - (math.log(core_floor) - mu) / sigma

    def update_conditions(self, segment, surf_age, guard=False):
        segment.cracking = self.get_cracking(segment, surf_age)
        if guard and segment.prerep_dz_crack > 0.0:
            floor = PRE_REPAIR["guard_factor"] * REHAB_RESET["crack_pct"]
            if floor > 0.0 and segment.cracking < floor:
                sigma = self.sigma("severity", segment)
                mu = self.mu("severity", segment, surf_age)
                deviate_now = self.clamp_severity_deviate(self.truncation_point(mu, sigma),
                                                          segment.crack_w_sev)
                segment.prerep_dz_crack = max(0.0, self.maximum_credit(deviate_now, mu, sigma, floor))
                segment.cracking = self.get_cracking(segment, surf_age)

        segment.rut = self.get_rutting(segment, surf_age, segment.cracking)
        if guard and segment.prerep_dz_rut > 0.0:
            floor = PRE_REPAIR["guard_factor"] * REHAB_RESET["rut_mm"]
            if floor > 0.0 and segment.rut < floor:
                core = floor / (1.0 + FEEDBACK_CRACK_ON_RUT * segment.cracking / 100.0)
                if segment.group == "cs":
                    core -= self.chipseal_rut_growth(segment)
                if core > 0.0:
                    segment.prerep_dz_rut = max(0.0, self.maximum_credit(
                        segment.rut_z, self.mu("rut", segment, surf_age), self.sigma("rut", segment), core))
                    segment.rut = self.get_rutting(segment, surf_age, segment.cracking)

        segment.iri = self.get_roughness(segment, surf_age, segment.cracking, segment.rut)
        if guard and segment.prerep_dz_iri > 0.0:
            floor = PRE_REPAIR["guard_factor"] * REHAB_RESET["iri"]
            if floor > 0.0 and segment.iri < floor:
                core = floor / (1.0 + FEEDBACK_CRACK_ON_IRI * segment.cracking / 100.0)
                core /= (1.0 + FEEDBACK_RUT_ON_IRI * segment.rut / 10.0)
                if core > 0.0:
                    segment.prerep_dz_iri = max(0.0, self.maximum_credit(
                        segment.iri_z, self.mu("iri", segment, surf_age), self.sigma("iri", segment), core))
                    segment.iri = self.get_roughness(segment, surf_age, segment.cracking, segment.rut)

    def step_untreated(self, segment):
        segment.surf_age += 1
        segment.rut_growth_yrs += 1
        segment.prerep_yrs += 1
        self.update_conditions(segment, segment.surf_age)

    def step_reseal(self, segment):
        segment.prerep_yrs += 1
        segment.surf_age = 0.0
        segment.rut_growth_yrs += 1
        self.update_conditions(segment, segment.surf_age)

    def step_pre_repair(self, segment, with_overlay=False):
        """One modelling period in which a pre-repair lands, mirroring Resetter.Reset: the credit is
        worked out BEFORE any clock moves, and the clock then moves as the treatment dictates - not at
        all for a repair on its own, back to zero for one that comes with an overlay."""
        self.apply_pre_repair_credits(segment)
        segment.surf_age = 0.0 if with_overlay else segment.surf_age + 1
        segment.rut_growth_yrs += 1
        self.update_conditions(segment, segment.surf_age, guard=True)

    def step_rehabilitation(self, segment):
        segment.d0 = REHAB_RESET_D0[segment.group]
        segment.rehabilitated = True
        segment.surf_age = 0.0
        segment.rut_growth_yrs = 0.0
        segment.prerep_dz_crack = segment.prerep_dz_rut = segment.prerep_dz_iri = 0.0
        segment.prerep_yrs = 0.0
        segment.cracking = REHAB_RESET["crack_pct"]
        segment.rut = REHAB_RESET["rut_mm"]
        segment.iri = REHAB_RESET["iri"]


# ---------------------------------------------------------------------------------------------------
# Checks
# ---------------------------------------------------------------------------------------------------

RESULTS = []


def check(name, ok, detail=""):
    RESULTS.append((name, bool(ok), detail))
    print(("  PASS  " if ok else "  FAIL  ") + name + ((" | " + detail) if detail else ""))


def seeded_segment(models, group, surf_age, adt, d0, heavy_perc, rut, iri, cracking, clamped=None):
    """A segment placed at a given surveyed condition by the year-zero inversion, as the Initialiser
    does. Pass a list as `clamped` to be told which quantities the clamp bit on - a clamped segment
    deliberately does not reproduce its own reading."""
    if clamped is None:
        clamped = []
    segment = Segment(group, surf_age, adt, d0, heavy_perc)
    if cracking >= CRACK_ONSET_PCT:
        onset, quantile = models.invert_cracking_above_onset(segment, cracking)
        segment.crack_u_onset = onset
        segment.crack_w_sev = quantile
        segment.crack_below = 0.5
        mu = models.mu("severity", segment, surf_age)
        sigma = models.sigma("severity", segment)
        if (math.log(cracking) - mu) / sigma > MULTIPLIER_CLAMP_SD:
            clamped.append("cracking")
    else:
        segment.crack_u_onset = 1.0          # never onset
        segment.crack_below = cracking
    segment.cracking = models.get_cracking(segment, surf_age)

    raw = models.raw_level_deviate(rut - (models.chipseal_rut_growth(segment) if group == "cs" else 0.0),
                                   models.mu("rut", segment, surf_age), models.sigma("rut", segment))
    if abs(raw) > MULTIPLIER_CLAMP_SD:
        clamped.append("rut")
    segment.rut_z = models.invert_rutting(segment, rut, segment.cracking)
    segment.rut = models.get_rutting(segment, surf_age, segment.cracking)

    raw = models.raw_level_deviate(iri, models.mu("iri", segment, surf_age), models.sigma("iri", segment))
    if abs(raw) > MULTIPLIER_CLAMP_SD:
        clamped.append("iri")
    segment.iri_z = models.invert_roughness(segment, iri, segment.cracking, segment.rut)
    segment.iri = models.get_roughness(segment, surf_age, segment.cracking, segment.rut)
    return segment


def main():
    folder = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        os.path.dirname(os.path.abspath(__file__)), "..", "..", "starter-model-setup", "supporting")
    folder = os.path.abspath(folder)
    if not os.path.isdir(folder):
        print("Coefficient folder not found: " + folder)
        return 1

    print("Coefficients from: " + folder)
    models = Models(load_all(folder))

    # -----------------------------------------------------------------------------------------------
    print("\n1. Year zero still reproduces the survey, with and without a credit in force")
    # -----------------------------------------------------------------------------------------------
    # The inversions now add the pre-repair credit back. At year zero every credit is zero, so the
    # mirror must be a no-op there - and it must also be EXACT when a credit is present, which is the
    # thing that would otherwise rot unnoticed.
    worst_plain, worst_credited = 0.0, 0.0
    tested, skipped = 0, 0
    for group in ("ac", "cs"):
        for surf_age in (0.1, 3.0, 12.0, 22.0, 40.0):
            for rut, iri, crack in ((2.5, 3.0, 0.0), (4.0, 5.5, 6.0), (6.0, 7.0, 20.0)):
                clamped = []
                segment = seeded_segment(models, group, surf_age, 3000.0, 1.1, 8.0,
                                         rut, iri, crack, clamped)
                if clamped:
                    skipped += 1
                    continue
                tested += 1
                worst_plain = max(worst_plain,
                                  abs(segment.rut - rut) / rut,
                                  abs(segment.iri - iri) / iri)
                if crack >= CRACK_ONSET_PCT:
                    worst_plain = max(worst_plain, abs(segment.cracking - crack) / crack)

                # Now give the segment a credit and re-invert against the same reading. The round trip
                # has to still land on the reading, which is true only if the mirror is exact. Small
                # credits, so the mirrored deviate does not run into the clamp itself.
                credited = segment.copy()
                credited.prerep_dz_rut = 0.30
                credited.prerep_dz_iri = 0.20
                credited.prerep_dz_crack = 0.25
                credited.prerep_yrs = 3.0
                credited.rut_z = models.invert_rutting(credited, rut, segment.cracking)
                got_rut = models.get_rutting(credited, surf_age, segment.cracking)
                credited.iri_z = models.invert_roughness(credited, iri, segment.cracking, got_rut)
                got_iri = models.get_roughness(credited, surf_age, segment.cracking, got_rut)
                worst_credited = max(worst_credited,
                                     abs(got_rut - rut) / rut, abs(got_iri - iri) / iri)

                if crack >= CRACK_ONSET_PCT:
                    # The credit pushes the stored severity deviate UP by the credit, so a reading that
                    # sat just inside the clamp without one can sit outside it with one. That is the
                    # clamp doing its job, not a broken mirror, so those cases are skipped here.
                    sev_mu = models.mu("severity", credited, surf_age)
                    sev_sigma = models.sigma("severity", credited)
                    target = ((math.log(crack) - sev_mu) / sev_sigma) + models.credit_crack(credited)
                    trunc_deviate = norm.ppf(models.truncation_point(sev_mu, sev_sigma))
                    if trunc_deviate <= target <= MULTIPLIER_CLAMP_SD:
                        onset, quantile = models.invert_cracking_above_onset(credited, crack)
                        credited.crack_u_onset, credited.crack_w_sev = onset, quantile
                        got_crack = models.get_cracking(credited, surf_age)
                        worst_credited = max(worst_credited, abs(got_crack - crack) / crack)

    check("no credit: year zero reproduces the survey", worst_plain < 1e-12,
          "%d segments, worst relative error %.2e (%d clamped, skipped)"
          % (tested, worst_plain, skipped))
    check("credit in force: the inversion is still the exact inverse", worst_credited < 1e-9,
          "worst relative error %.2e" % worst_credited)

    # -----------------------------------------------------------------------------------------------
    print("\n2. The specification's own chipseal rut table (section 9a), at its stated 3.0 mm extent")
    # -----------------------------------------------------------------------------------------------
    # 8.0 mm of rut, surface age 22, D0 1.8. This is the case that matters most: a reseal does nothing
    # at all to chipseal rut, so the pre-repair is the only lever between no effect and rebuilding.
    saved_extent = PRE_REPAIR["max_extent_rut_mm"]
    PRE_REPAIR["max_extent_rut_mm"] = 3.0

    def rut_path(kind, rho, tau, horizons):
        """Follows the specification's own convention for this table: the treatment lands with no year
        passing ("yr 0"), and the years counted after it are years of ageing. The model's own Resetter
        always advances a period as it treats, so this is the table's convention, not the model's -
        what is being checked is the arithmetic of the credit and its decay, which is identical."""
        PRE_REPAIR["retained_fraction"] = rho
        PRE_REPAIR["decay_tau_yrs"]["cs"] = tau
        clamped = []
        segment = seeded_segment(models, "cs", 22.0, 3000.0, 1.8, 8.0, 8.0, 5.0, 0.0, clamped)
        assert not clamped, "the illustration segment should not clamp: " + str(clamped)

        if kind == "prerepair":
            models.apply_pre_repair_credits(segment)
            models.update_conditions(segment, segment.surf_age, guard=True)
        elif kind == "reseal":
            segment.surf_age = 0.0
            models.update_conditions(segment, segment.surf_age)
        elif kind == "rehab":
            models.step_rehabilitation(segment)

        out = {0: segment.rut}
        for year in range(1, max(horizons) + 1):
            models.step_untreated(segment)
            if year in horizons:
                out[year] = segment.rut
        return out

    years = [4, 8, 20]
    reseal = rut_path("reseal", 0.4, 4.0, years)
    pre0 = rut_path("prerepair", 0.0, 4.0, years)
    pre04 = rut_path("prerepair", 0.4, 4.0, years)
    rehab = rut_path("rehab", 0.4, 4.0, years)

    expected = [
        ("reseal", reseal, {0: 8.00, 4: 8.26, 8: 8.51, 20: 9.28}),
        ("pre-repair rho=0", pre0, {0: 5.00, 4: 6.99, 8: 8.02, 20: 9.25}),
        ("pre-repair rho=0.4", pre04, {0: 5.00, 4: 6.23, 8: 6.89, 20: 7.90}),
    ]
    worst = 0.0
    for label, got, table in expected:
        for year, value in table.items():
            worst = max(worst, abs(got[year] - value))
        print("      %-20s " % label
              + "  ".join("yr%-3d %6.2f (spec %5.2f)" % (y, got[y], v) for y, v in sorted(table.items())))
    print("      %-20s " % "rehabilitation"
          + "  ".join("yr%-3d %6.2f" % (y, rehab[y]) for y in [0] + years)
          + "   (the specification's 2.00/2.26/2.51/3.28 assumes the redrawn deviate lands exactly")
    print("      %-20s " % ""
          + "    on the median; this segment's own covariates put it slightly above)")

    check("reproduces all twelve entries of the section 9a rut table", worst < 0.01,
          "largest difference %.4f mm" % worst)

    check("a pre-repair is never worse than a reseal, in any year",
          all(pre04[y] <= reseal[y] + 1e-9 for y in [0] + years))
    check("a pre-repair is never better than a rehabilitation, in any year",
          all(pre04[y] >= rehab[y] - 1e-9 for y in [0] + years))
    check("the retained fraction decides where the segment ends up, not the decay constant",
          abs(pre0[20] - reseal[20]) < 0.10 and (reseal[20] - pre04[20]) > 1.0,
          "at rho=0 the segment ends %.2f mm ahead at year 20; at rho=0.4, %.2f mm"
          % (reseal[20] - pre0[20], reseal[20] - pre04[20]))

    PRE_REPAIR["max_extent_rut_mm"] = saved_extent
    PRE_REPAIR["retained_fraction"] = 0.4
    PRE_REPAIR["decay_tau_yrs"]["cs"] = 4.0

    # -----------------------------------------------------------------------------------------------
    print("\n3. The subtractive rule: effectiveness depends on how bad the segment was")
    # -----------------------------------------------------------------------------------------------
    # The specification's table quotes a post-treatment ZERO for the two lightest cases. That is not
    # reachable, and not because of anything here: driving a distress to zero implies a deviate of
    # minus infinity, so the specification itself clamps the post-repair deviate at minus two standard
    # deviations. On asphalt cracking that floor is worth more than two percentage points, which is
    # ABOVE the one per cent onset threshold - so a fully repaired segment still reads as cracked. The
    # shape the table is about survives intact; only the two zeros do not.
    surf_age = 15.0
    probe = Segment("ac", surf_age, 3000.0, 1.1, 6.0)
    floor_at_repair = math.exp(models.mu("severity", probe, surf_age)
                               - MULTIPLIER_CLAMP_SD * models.sigma("severity", probe))
    print("      the two-sigma floor on asphalt cracking at surface age %.0f is %.2f%%, against a %.1f%% onset threshold"
          % (surf_age, floor_at_repair, CRACK_ONSET_PCT))
    print("      %-12s %-12s %-12s %-12s %-12s"
          % ("pre-treat", "post-treat", "removed", "spec says", "10 yr later"))
    rows = []
    for pre_crack in (8.0, 15.0, 25.0, 45.0, 80.0):
        clamped = []
        segment = seeded_segment(models, "ac", surf_age, 3000.0, 1.1, 6.0, 5.5, 6.0, pre_crack, clamped)
        assert not clamped, "seed clamped at %.1f%%: %s" % (pre_crack, clamped)
        pre = segment.cracking
        models.step_pre_repair(segment)
        post = segment.cracking
        expected_post = max(0.0, pre - PRE_REPAIR["max_extent_crack_pct"])
        for _ in range(10):
            models.step_untreated(segment)
        rows.append((pre, post, expected_post, segment.cracking))
        print("      %-12.1f %-12.2f %-12s %-12.1f %-12.2f"
              % (pre, post, "%.0f%%" % (100.0 * (pre - post) / pre), expected_post, segment.cracking))

    # Where the floor does not bind, the subtraction is exact. A year of ageing passes in the treatment
    # period exactly as it does for every other treatment, so the value read back is the post-repair
    # deviate carried forward one year, not the post-repair value itself.
    unfloored = [row for row in rows if row[2] > floor_at_repair * 1.5]
    check("where the two-sigma floor does not bind, the repair removes exactly the repair extent",
          all(abs(post - expected) / expected < 0.06 for _pre, post, expected, _later in unfloored),
          "%d cases, largest departure %.1f%% (one year of ageing, taken in the treatment period)"
          % (len(unfloored),
             100.0 * max(abs(post - expected) / expected for _p, post, expected, _l in unfloored)))

    check("severe cracking keeps a residual the repair cannot reach",
          rows[-1][1] > 40.0, "80%% pre-treatment comes out at %.1f%%" % rows[-1][1])
    check("and that residual still dominates ten years later",
          rows[-1][3] > 4.0 * rows[0][3],
          "%.1f%% against %.1f%% for the lightly cracked segment" % (rows[-1][3], rows[0][3]))
    check("above the floor, the share removed falls as the segment worsens",
          all((rows[i][0] - rows[i][1]) / rows[i][0] >= (rows[i + 1][0] - rows[i + 1][1]) / rows[i + 1][0] - 1e-9
              for i in range(2, len(rows) - 1)))

    # -----------------------------------------------------------------------------------------------
    print("\n4. Repeated pre-repairs cannot stack up to a rehabilitation")
    # -----------------------------------------------------------------------------------------------
    segment = seeded_segment(models, "cs", 20.0, 3000.0, 1.5, 7.0, 7.0, 6.0, 30.0)
    rebuilt = seeded_segment(models, "cs", 20.0, 3000.0, 1.5, 7.0, 7.0, 6.0, 30.0)
    models.step_rehabilitation(rebuilt)
    path = []
    for repeat in range(6):
        models.step_pre_repair(segment)
        path.append((segment.rut, segment.cracking))
        for _ in range(5):
            models.step_untreated(segment)
    print("      rut after each of six repeats, five years apart: "
          + ", ".join("%.2f" % r for r, _ in path))
    check("six repeats never take the rut below the guard floor",
          all(r >= PRE_REPAIR["guard_factor"] * REHAB_RESET["rut_mm"] - 1e-9 for r, _ in path),
          "floor %.2f mm, lowest reached %.2f mm"
          % (PRE_REPAIR["guard_factor"] * REHAB_RESET["rut_mm"], min(r for r, _ in path)))

    # -----------------------------------------------------------------------------------------------
    print("\n5. The guard: an overlay with repairs may not out-perform a rebuild")
    # -----------------------------------------------------------------------------------------------
    # Arrangement (a) - the ThinAC_H treatment - resets the clock AND credits the deviate. Without the
    # guard those two compound.
    cases = [(d0, rut, iri, crack)
             for d0 in (0.8, 1.2, 1.8, 2.5)
             for rut, iri, crack in ((5.0, 6.0, 20.0), (9.0, 9.5, 60.0), (3.0, 4.0, 5.0))]

    rut_floor = PRE_REPAIR["guard_factor"] * REHAB_RESET["rut_mm"]
    iri_floor = PRE_REPAIR["guard_factor"] * REHAB_RESET["iri"]

    # WHAT THE GUARD CAN AND CANNOT DO, because on asphalt it runs into something it was not designed
    # for. It works by shrinking the repair credit, and a credit can only shrink to zero. So where a
    # PLAIN overlay - no repairs at all - already sits below the floor, the guard removes the whole of
    # the repair's contribution and can go no further. It must not go further: forcing the value up
    # would make a treatment WITH repairs read worse than the same treatment without them.
    #
    # That is exactly the asphalt case. The fitted asphalt rut model puts a fresh surface at about
    # 1.3 mm, below the 2.5 mm floor, before any repair is considered.
    #
    # So the contract to check is per case and is an either/or: the floor holds, OR the credit was
    # cancelled outright and the segment reads exactly what a plain overlay would.
    honoured, floor_bound, worst_iri_margin, worst_penalty = 0, 0, None, 0.0
    for d0, rut, iri, crack in cases:
        overlay = seeded_segment(models, "ac", 18.0, 5000.0, d0, 8.0, rut, iri, crack)
        plain = overlay.copy()
        rebuild = overlay.copy()

        models.step_pre_repair(overlay, with_overlay=True)   # ThinAC_H - overlay WITH repairs
        models.step_reseal(plain)                            # ThinAC_P - the same overlay, no repairs
        models.step_rehabilitation(rebuild)

        rut_ok = overlay.rut >= rut_floor - 1e-9 or abs(overlay.rut - plain.rut) < 1e-9
        iri_ok = overlay.iri >= iri_floor - 1e-9 or abs(overlay.iri - plain.iri) < 1e-9
        if rut_ok and iri_ok:
            honoured += 1
        if overlay.rut < rut_floor - 1e-9 or overlay.iri < iri_floor - 1e-9:
            floor_bound += 1

        margin = overlay.iri - rebuild.iri
        worst_iri_margin = margin if worst_iri_margin is None else min(worst_iri_margin, margin)
        worst_penalty = max(worst_penalty, overlay.rut - plain.rut, overlay.iri - plain.iri)

    check("on every case the floor holds, or the repair credit was cancelled outright",
          honoured == len(cases),
          "%d of %d; in %d the plain overlay was already below the floor, so the credit went to zero"
          % (honoured, len(cases), floor_bound))

    check("adding repairs to an overlay never makes the segment worse than the overlay alone",
          worst_penalty <= 1e-9,
          "worst case is %.2e - a credit can shrink to zero and no further" % worst_penalty)

    check("a rehabilitation still beats an overlay with repairs on ROUGHNESS",
          worst_iri_margin >= -1e-9,
          "worst margin %.3f IRI - the asphalt roughness as-new offset is worth x%.2f"
          % (worst_iri_margin, math.exp(REHAB_OFFSET_IRI["ac"])))

    unguarded_breaches = 0
    saved_guard = PRE_REPAIR["guard_factor"]
    PRE_REPAIR["guard_factor"] = 0.0
    for d0, rut, iri, crack in cases:
        overlay = seeded_segment(models, "ac", 18.0, 5000.0, d0, 8.0, rut, iri, crack)
        models.step_pre_repair(overlay, with_overlay=True)
        if overlay.rut < rut_floor - 1e-9 or overlay.iri < iri_floor - 1e-9:
            unguarded_breaches += 1
    PRE_REPAIR["guard_factor"] = saved_guard
    check("and the guard is doing real work, not decorative", unguarded_breaches > 0,
          "%d of %d cases would fall below the floor with the guard switched off"
          % (unguarded_breaches, len(cases)))

    # REPORTED, NOT ASSERTED. Section 9a calls this out as a known property rather than a defect: the
    # asphalt rutting as-new offset is 0.0, so nothing separates a structural inlay from a rebuild on
    # AC rut. The fitted asphalt rut model carries no deflection term at all, which is the other half
    # of the same story - the one covariate a rehabilitation changes does not appear in it.
    probe = Segment("ac", 0.0, 5000.0, 1.2, 8.0)
    print("      asphalt rut, surface age 0: the model's own median is %.2f mm, while a rehabilitation"
          % math.exp(models.mu("rut", probe, 0.0)))
    print("      imposes %.2f mm in the treatment year. So ANY asphalt overlay - with repairs or"
          % REHAB_RESET["rut_mm"])
    print("      without - reads better than a rebuild on rut in that year. Section 9a says to know")
    print("      this rather than to fix it: the asphalt rut as-new offset is 0.0 by construction.")
    print("      The consequence for the guard is above: on asphalt it has nothing left to cap.")

    # -----------------------------------------------------------------------------------------------
    print("\n6. What a pre-repair must NOT touch")
    # -----------------------------------------------------------------------------------------------
    before = seeded_segment(models, "cs", 20.0, 3000.0, 1.5, 7.0, 7.0, 6.0, 30.0)
    after = before.copy()
    models.step_pre_repair(after)
    check("the surface age advances by a year, exactly as an untreated segment's would",
          abs(after.surf_age - (before.surf_age + 1)) < 1e-12,
          "%.1f -> %.1f" % (before.surf_age, after.surf_age))
    check("the chipseal rut accumulator keeps running",
          abs(after.rut_growth_yrs - (before.rut_growth_yrs + 1)) < 1e-12)
    check("the deflection is unchanged", after.d0 == before.d0)
    check("the onset position is unchanged - the credit is on severity only",
          after.crack_u_onset == before.crack_u_onset)
    check("no as-new offset is earned", after.rehabilitated is False)
    check("roughness gets no credit as delivered, because the extent is zero",
          after.prerep_dz_iri == 0.0 and abs(after.iri - models.get_roughness(
              after, after.surf_age, after.cracking, after.rut)) < 1e-12)

    # A segment below the onset threshold has nothing for a repair to remove.
    uncracked = seeded_segment(models, "cs", 20.0, 3000.0, 1.5, 7.0, 7.0, 6.0, 0.3)
    models.step_pre_repair(uncracked)
    check("a segment below the cracking threshold earns no cracking credit",
          uncracked.prerep_dz_crack == 0.0)

    # -----------------------------------------------------------------------------------------------
    print("\n7. As delivered: what the shipped numbers do to a typical chipseal segment")
    # -----------------------------------------------------------------------------------------------
    # Reported rather than asserted - these are the numbers Fritz's choice of parameters produces.
    base = seeded_segment(models, "cs", 20.0, 3000.0, 1.5, 7.0, 6.5, 6.0, 25.0)
    rows = {}
    for label, action in (("untreated", None), ("reseal", "reseal"),
                          ("pre-repair", "prerepair"), ("rehabilitation", "rehab")):
        segment = base.copy()
        if action == "reseal":
            models.step_reseal(segment)
        elif action == "prerepair":
            models.step_pre_repair(segment)
        elif action == "rehab":
            models.step_rehabilitation(segment)
        else:
            models.step_untreated(segment)
        track = {0: (segment.rut, segment.cracking)}
        for year in range(1, 31):
            models.step_untreated(segment)
            if year in (5, 10, 30):
                track[year] = (segment.rut, segment.cracking)
        rows[label] = track
    print("      %-16s %s" % ("", "  ".join("yr%-2d rut/crack" % y for y in (0, 5, 10, 30))))
    for label, track in rows.items():
        print("      %-16s %s" % (label, "  ".join("%5.2f /%6.1f" % track[y] for y in (0, 5, 10, 30))))

    check("as delivered, a pre-repair still improves a chipseal segment the reseal cannot help",
          rows["pre-repair"][0][0] < rows["reseal"][0][0] - 1e-9,
          "rut %.2f against %.2f in the treatment year"
          % (rows["pre-repair"][0][0], rows["reseal"][0][0]))

    # -----------------------------------------------------------------------------------------------
    failures = [name for name, ok, _ in RESULTS if not ok]
    print("\n%d checks, %d failed" % (len(RESULTS), len(failures)))
    for name in failures:
        print("  FAILED: " + name)
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
