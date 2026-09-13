using JCass_ModelCore.Models;

namespace StarterModel.Objects;

/// <summary>
/// The five deterioration models: cracking, rutting, roughness, flushing and ravelling.
///
/// <para>THE THING TO UNDERSTAND FIRST. Rutting and roughness are LEVEL models, not increments. The
/// value for a year is recomputed from the segment's surface age and scaled by a multiplier drawn once
/// for that segment - it is not last year's value plus a rate. Nothing accumulates, so a rounding
/// error cannot compound and a reset is simply a change of age. Cracking is a three-part stochastic
/// model and works the same way: recomputed each year from two uniform draws held for the life of the
/// surfacing.</para>
///
/// <para>ORDER MATTERS AND IT IS FIXED: cracking, then rutting, then roughness. Rutting and roughness
/// each carry a feedback term in cracking, and roughness carries one in rutting. The three feedback
/// coefficients default to zero, so today the order changes nothing - but they are switchable, and
/// evaluating in the wrong order once they are on changes the answer without reporting anything.</para>
///
/// <para>Flushing and ravelling are rule-based, not fitted. The survey data carries no magnitude worth
/// fitting - 90% of segments read zero flushing and the 99th percentile is 1.5% - so they are a
/// straight line from an onset age, with rates that are engineering judgement and live in lookups.xlsx.</para>
/// </summary>
public class DeteriorationModels
{
    /// <summary>
    /// The two surface class groups the models were fitted on. Every surface class maps to one of
    /// these through the 'surf_class_group' lookup set.
    /// </summary>
    public static readonly string[] ModelGroups = { "ac", "cs" };

    /// <summary>Group name for the chipseal models.</summary>
    public const string GroupChipSeal = "cs";

    private readonly DeteriorationCoefficients _coefficients;
    private readonly Constants _constants;

    public DeteriorationModels(DeteriorationCoefficients coefficients, Constants constants)
    {
        _coefficients = coefficients ?? throw new ArgumentNullException(nameof(coefficients));
        _constants = constants ?? throw new ArgumentNullException(nameof(constants));
    }

    /// <summary>
    /// Resolves one of the fitted model sets for a segment's group, naming the segment and the group if
    /// it is not there. Without this the failure is a bare KeyNotFoundException part way through a run
    /// of several thousand segments, which names neither.
    /// </summary>
    private FittedModel ModelFor(Dictionary<string, FittedModel> modelsByGroup, RoadSegment segment, string modelDescription)
    {
        if (modelsByGroup.TryGetValue(segment.DeteriorationGroup, out FittedModel? model))
        {
            return model;
        }

        throw new Exception($"No {modelDescription} model for deterioration group '{segment.DeteriorationGroup}' " +
                            $"on segment {segment.FeebackCode}. Groups the models were fitted on: " +
                            $"{string.Join(", ", ModelGroups)}. Check the 'surf_class_group' lookup set in " +
                            $"lookups.xlsx, which maps a surface class onto one of those.");
    }

    #region Covariates shared by every sub-model

    /// <summary>
    /// The age term every fitted model uses: log(min(surface age, cap) + offset).
    /// <para>The cap is what stops a never-treated segment being extrapolated far outside the evidence
    /// - uncapped it reaches about 126 years in a thirty year run. The offset keeps the logarithm
    /// finite on a new surface. Both came from the fit, so changing either invalidates the
    /// coefficients rather than recalibrating them.</para>
    /// </summary>
    public double LogAge(double surfaceAgeYears)
    {
        double age = Math.Max(0.0, surfaceAgeYears);
        double capped = Math.Min(age, _constants.DetAgeCapYears);
        return Math.Log(capped + _constants.DetAgeOffset);
    }

    /// <summary>
    /// The traffic term: log(max(surveyed ADT, 1)).
    /// <para>This reads the SURVEYED traffic, never the modelled traffic that grows each period. In
    /// the roughness models the traffic coefficient is negative - busier roads were built to a higher
    /// standard - so letting traffic grow would predict roads getting smoother as they get busier. The
    /// modelled ADT parameter still grows; it is used by the treatment trigger, not by these models.</para>
    /// </summary>
    public static double LogTraffic(double surveyedAverageDailyTraffic)
    {
        return Math.Log(Math.Max(surveyedAverageDailyTraffic, 1.0));
    }

    #endregion

    #region Cracking - the three part model

    /// <summary>
    /// Probability that the segment has started cracking at the given surface age (Part A), including
    /// the rehabilitation offset where the segment has been rebuilt.
    ///
    /// <para>ADDED HERE, IN ONE PLACE, for the same reason as in the two level models: every use of the
    /// onset probability reads this method - the year-on-year comparison, the year-zero inversion and
    /// the draw the Resetter makes after a treatment - so an offset added at a call site would put the
    /// draw and the comparison that reads it back out of step, and a segment would be handed a position
    /// that contradicts the probability it is tested against.</para>
    ///
    /// <para>The offset is ZERO unless the engineer sets it. Unlike the four level-model offsets it is
    /// not derived from anything, because cracking has no as-new value to derive it from.</para>
    /// </summary>
    public double CrackOnsetProbability(RoadSegment segment, double surfaceAgeYears)
    {
        FittedModel model = this.ModelFor(_coefficients.CrackOnset, segment, "cracking onset");
        double eta = model.LinearPredictor(this.LogAge(surfaceAgeYears), LogTraffic(segment.SurveyedAverageDailyTraffic),
                                           segment.CentralDeflection, segment.HeavyVehiclePercentage);
        eta += this.RehabilitationOffsetCrackOnset(segment);

        // Numerically stable inverse logit: the naive form overflows for a large negative eta.
        return eta >= 0.0 ? 1.0 / (1.0 + Math.Exp(-eta)) : Math.Exp(eta) / (1.0 + Math.Exp(eta));
    }

    /// <summary>
    /// The log-odds offset a rehabilitated segment carries in the cracking onset model, and zero for
    /// every segment that has not been rebuilt. Zero by default for a rebuilt one too - see the Constants
    /// property for why this offset is the engineer's to set rather than one derived here.
    /// </summary>
    private double RehabilitationOffsetCrackOnset(RoadSegment segment)
    {
        return segment.HasBeenRehabilitated ? _constants.GetRehabOffsetCrackOnset(segment.DeteriorationGroup) : 0.0;
    }

    /// <summary>
    /// Mean of the log cracking severity at the given surface age, for a segment that has started
    /// cracking (Part B).
    /// </summary>
    public double CrackSeverityMu(RoadSegment segment, double surfaceAgeYears)
    {
        FittedModel model = this.ModelFor(_coefficients.CrackSeverity, segment, "cracking severity");
        return model.LinearPredictor(this.LogAge(surfaceAgeYears), LogTraffic(segment.SurveyedAverageDailyTraffic),
                                     segment.CentralDeflection, segment.HeavyVehiclePercentage);
    }

    /// <summary>
    /// The lower truncation point of the severity distribution, as a probability. Part B was fitted
    /// left-truncated at the onset threshold, so a severity draw must start here and not at zero.
    /// </summary>
    private double SeverityTruncationPoint(double mu, double sigma)
    {
        return NormalDistribution.Phi((Math.Log(_constants.DetCrackOnsetPercent) - mu) / sigma);
    }

    /// <summary>
    /// Cracking percentage for one year, from the segment's two persistent uniform draws.
    ///
    /// <para>Onset is decided by comparing the segment's held position against the modelled onset
    /// probability for its current age. That comparison is deliberately NOT latched into a flag: it is
    /// already one-way within a surfacing cycle, because the onset probability only rises with age and
    /// every other term in it is frozen. Leaving it stateless is also what makes a resurfacing work
    /// without any special case - the age returns to zero, the probability falls, and a segment
    /// reverts to uncracked unless its position is low enough to keep it cracked, which is the
    /// reflective cracking the specification asks for.</para>
    ///
    /// <para>The severity draw comes from the TRUNCATED distribution. Using a raw normal deviate here
    /// is wrong in a way that does not announce itself: it would assign sub-threshold cracking to
    /// segments this method has just decided have cracked - two thirds of chipseal onset segments at
    /// age one, and still one in fifteen at age thirty.</para>
    /// </summary>
    public double GetCracking(RoadSegment segment, double surfaceAgeYears)
    {
        double onsetProbability = this.CrackOnsetProbability(segment, surfaceAgeYears);
        if (segment.CrackOnsetPosition >= onsetProbability)
        {
            // Not cracked at this age. The sub-threshold value carries no age trend, so it is held
            // rather than redrawn - redrawing it annually would invent variation the data does not show.
            return segment.CrackingBelowOnset;
        }

        double sigma = this.ModelFor(_coefficients.CrackSeverity, segment, "cracking severity").Sigma;
        double mu = this.CrackSeverityMu(segment, surfaceAgeYears);
        double truncationPoint = this.SeverityTruncationPoint(mu, sigma);

        double deviate = this.ClampSeverityDeviate(truncationPoint, segment.CrackSeverityQuantile);
        return Math.Exp(mu + sigma * deviate);
    }

    /// <summary>
    /// Maps a segment's held severity quantile onto the left-truncated distribution and applies the
    /// multiplier clamp. The truncation supplies the lower bound and the clamp the upper one; on this
    /// network they do not collide, because the largest truncation deviate at surface age zero is well
    /// below the clamp.
    /// </summary>
    private double ClampSeverityDeviate(double truncationPoint, double severityQuantile)
    {
        double truncationDeviate = NormalDistribution.PhiInverse(truncationPoint);
        double deviate = NormalDistribution.PhiInverse(truncationPoint + severityQuantile * (1.0 - truncationPoint));
        return Math.Min(_constants.DetMultiplierClampSd, Math.Max(truncationDeviate, deviate));
    }

    /// <summary>
    /// Draws a sub-threshold cracking value from Part C: a share of exact zeros, and otherwise a
    /// right-truncated lognormal.
    /// <para>The fitted parameters look wrong and are not - the asphalt intercept exponentiates to 964
    /// against a bound of 1. A truncated fit's parameters are not individually interpretable; only the
    /// distribution they imply is, and that one reproduces the observed median and ninetieth
    /// percentile. Draw by inverse CDF, never by rejection: barely one draw in 125 would be accepted.</para>
    /// </summary>
    public double DrawCrackingBelowOnset(RoadSegment segment, Random random)
    {
        FittedModel model = this.ModelFor(_coefficients.CrackBelow, segment, "sub-threshold cracking");
        if (random.NextDouble() < model.ZeroShare)
        {
            return 0.0;
        }

        double mu = model.LinearPredictor(0.0, 0.0, 0.0, 0.0);   // Part C has an intercept and nothing else
        double sigma = model.Sigma;
        double cap = NormalDistribution.Phi((Math.Log(_constants.DetCrackOnsetPercent) - mu) / sigma);
        double deviate = NormalDistribution.PhiInverse(random.NextDouble() * cap);
        return Math.Exp(mu + sigma * deviate);
    }

    #endregion

    #region Rutting and roughness - level models

    /// <summary>
    /// Mean of the log rut depth at the given surface age, including the rehabilitation offset where
    /// the segment has been rebuilt.
    ///
    /// <para>THE OFFSET IS ADDED HERE, IN ONE PLACE, AND THAT IS THE POINT. Everything that evaluates
    /// the rutting model reads this method - the forward model and the year-zero inversion both - so
    /// the two cannot drift apart. Add it at the call sites instead and the inversion stops being the
    /// exact inverse of the forward model, which shows up only as a segment that no longer reproduces
    /// its own starting condition.</para>
    /// </summary>
    public double RutMu(RoadSegment segment, double surfaceAgeYears)
    {
        FittedModel model = this.ModelFor(_coefficients.Rut, segment, "rutting");
        double mu = model.LinearPredictor(this.LogAge(surfaceAgeYears), LogTraffic(segment.SurveyedAverageDailyTraffic),
                                          segment.CentralDeflection, segment.HeavyVehiclePercentage);
        return mu + this.RehabilitationOffsetRut(segment);
    }

    /// <summary>
    /// The permanent log-space offset a rehabilitated segment carries in the rutting model, and zero
    /// for every segment that has not been rebuilt.
    /// <para>Without it, setting a rehabilitated segment to its as-new rut depth achieves nothing: the
    /// next period the model recomputes from a fit built entirely on surfacings over sixty-year-old
    /// pavements and snaps the segment straight back. The lookup comment carries the full reasoning.</para>
    /// </summary>
    private double RehabilitationOffsetRut(RoadSegment segment)
    {
        return segment.HasBeenRehabilitated ? _constants.GetRehabOffsetRut(segment.DeteriorationGroup) : 0.0;
    }

    /// <summary>Residual standard deviation of the rutting fit for this segment's group.</summary>
    public double RutSigma(RoadSegment segment) => this.ModelFor(_coefficients.Rut, segment, "rutting").Sigma;

    /// <summary>
    /// Rut depth in mm for one year: the modelled level, scaled by the segment's persistent multiplier.
    ///
    /// <para>Chipseal gets an extra growth term because surface age is not its rut clock - a seal laid
    /// over a rutted pavement inherits the rut, and fitted against surface age chipseal rutting
    /// actually runs backwards. The clock that would work is pavement age, and that column is a
    /// placeholder on almost every chipseal segment. So the growth is an explicit annual amount, and
    /// it is JUDGEMENT rather than evidence - the single number here most likely to need revisiting.</para>
    ///
    /// <para>THE GROWTH TERM DOES NOT RUN ON SURFACE AGE. It runs on RutGrowthYears, which starts at
    /// zero at year zero and is left alone by a resurfacing. Driving it from the surface age instead
    /// gets both ends wrong: at year zero it double-counts, because the chipseal level model has no age
    /// term and already reproduces the surveyed rut, and at a reseal it collapses to nothing, which
    /// would show a new seal removing rut that a chipseal in this network's data plainly inherits.</para>
    ///
    /// <para>Asphalt has no growth term at all - its rutting is a level model on surface age, and it
    /// does reset on a resurfacing, which is the opposite of chipseal and is measured rather than
    /// assumed: asphalt reads 2.13 mm of rut under surfaces up to six years old against 3.49 mm under
    /// surfaces over twenty, on pavements of the same age, while chipseal reads 4.19 against 3.77.</para>
    /// </summary>
    public double GetRutting(RoadSegment segment, double surfaceAgeYears, double crackingPercent)
    {
        // exp(mu) * exp(sigma * deviate), combined into one exponential so that sigma is read from the
        // fit in exactly one place and cannot drift out of step with the deviate that was inverted.
        double rut = Math.Exp(this.RutMu(segment, surfaceAgeYears) + this.RutSigma(segment) * segment.RutDeviate);

        if (segment.DeteriorationGroup == GroupChipSeal)
        {
            rut += this.ChipSealRutGrowth(segment);
        }

        return rut * (1.0 + _constants.DetFeedbackCrackOnRut * crackingPercent / 100.0);
    }

    /// <summary>
    /// The accumulated chipseal rut growth, in mm. Zero for asphalt, and zero for any chipseal segment
    /// in its first modelled period.
    /// <para>One place so the forward model and the year-zero inversion cannot drift apart.</para>
    /// </summary>
    private double ChipSealRutGrowth(RoadSegment segment)
    {
        return _constants.DetRutChipSealGrowthPerYear * Math.Max(0.0, segment.RutGrowthYears);
    }

    /// <summary>
    /// Mean of the log IRI at the given surface age, including the rehabilitation offset where the
    /// segment has been rebuilt. Added here for the same reason as in the rutting model: one place, so
    /// that the forward model and the inversion cannot disagree.
    /// </summary>
    public double IriMu(RoadSegment segment, double surfaceAgeYears)
    {
        FittedModel model = this.ModelFor(_coefficients.Iri, segment, "roughness");
        double mu = model.LinearPredictor(this.LogAge(surfaceAgeYears), LogTraffic(segment.SurveyedAverageDailyTraffic),
                                          segment.CentralDeflection, segment.HeavyVehiclePercentage);
        return mu + this.RehabilitationOffsetIri(segment);
    }

    /// <summary>
    /// The permanent log-space offset a rehabilitated segment carries in the roughness model, and zero
    /// for every segment that has not been rebuilt.
    /// </summary>
    private double RehabilitationOffsetIri(RoadSegment segment)
    {
        return segment.HasBeenRehabilitated ? _constants.GetRehabOffsetIri(segment.DeteriorationGroup) : 0.0;
    }

    /// <summary>Residual standard deviation of the roughness fit for this segment's group.</summary>
    public double IriSigma(RoadSegment segment) => this.ModelFor(_coefficients.Iri, segment, "roughness").Sigma;

    /// <summary>
    /// IRI in mm/m for one year: the modelled level, scaled by the segment's persistent multiplier,
    /// then adjusted by the cracking and rutting feedback terms.
    /// </summary>
    public double GetRoughness(RoadSegment segment, double surfaceAgeYears, double crackingPercent, double rutMillimetres)
    {
        double iri = Math.Exp(this.IriMu(segment, surfaceAgeYears) + this.IriSigma(segment) * segment.IriDeviate);

        iri *= (1.0 + _constants.DetFeedbackCrackOnIri * crackingPercent / 100.0);
        iri *= (1.0 + _constants.DetFeedbackRutOnIri * rutMillimetres / 10.0);

        return iri;
    }

    /// <summary>
    /// Recomputes a segment's cracking, rutting and roughness at the given surface age, and records
    /// what changed. This is the whole per-period condition update, and BOTH the Incrementer and the
    /// Resetter call it - an untreated segment at its new age, a treated one at whatever age its
    /// treatment left behind.
    ///
    /// <para>ONE COPY ON PURPOSE. The order is fixed - cracking, then rutting, then roughness - because
    /// rutting and roughness each take a feedback term in cracking and roughness takes one in rutting.
    /// All three feedback coefficients default to zero, so today the order changes no number, but they
    /// are switchable from lookups.xlsx and once they are on, an increment path and a reset path that
    /// had drifted out of step would give different answers with nothing reporting it.</para>
    ///
    /// <para>Nothing here reads the previous value to build the new one - these are levels, not
    /// increments. The previous values are read only so that the two diagnostic increments can report
    /// the realised change over the period.</para>
    /// </summary>
    public void UpdateConditions(RoadSegment segment, double surfaceAgeYears)
    {
        double rutBefore = segment.RutParameterValue;
        double iriBefore = segment.Iri;

        // 1. Cracking. Either the segment has cracked at this age, in which case its severity comes
        //    from the left-truncated distribution, or it has not, in which case it carries the
        //    sub-threshold value it has held since the surface was laid.
        segment.PctCracking = this.GetCracking(segment, surfaceAgeYears);

        // 2. Rutting, which takes cracking as a feedback term.
        segment.RutParameterValue = this.GetRutting(segment, surfaceAgeYears, segment.PctCracking);

        // 3. Roughness, which takes both cracking and rutting as feedback terms.
        segment.Iri = this.GetRoughness(segment, surfaceAgeYears, segment.PctCracking, segment.RutParameterValue);

        // Naasra needs no update: it is derived from Iri on every read.

        RecordRealisedChange(segment, rutBefore, iriBefore);
    }

    /// <summary>
    /// Sets the two diagnostic increment parameters to the change the period actually produced.
    ///
    /// <para>DIAGNOSTIC ONLY. Nothing reads either of them back to build the next value - these models
    /// are levels. They are worth keeping because a realised year-on-year change is what a modeller
    /// looks at first, but do not mistake them for a rate that drives anything. Both are declared with
    /// a minimum below zero, because a treatment makes them negative and the framework clamps every
    /// write silently: at a minimum of zero the post-treatment drop they exist to show would be
    /// recorded as zero with nothing saying so.</para>
    /// </summary>
    public static void RecordRealisedChange(RoadSegment segment, double rutBefore, double iriBefore)
    {
        segment.RutIncrement = segment.RutParameterValue - rutBefore;
        segment.IriIncrement = segment.Iri - iriBefore;
    }

    /// <summary>
    /// Recomputes flushing and ravelling from the rule-based model at the given surface age. Both are
    /// zero on a new surface and stay zero until a fraction of its expected life has passed, so a
    /// resurfacing and a rehabilitation reset them with no arithmetic at all.
    /// <para>Potholes take the same rule in the specification and are deliberately NOT modelled here:
    /// this model carries no pothole parameter, and pothole extent never exceeds 0.12% anywhere on the
    /// network, which is noise rather than signal. Worth knowing before the pothole reads still left in
    /// CalculationUtilities are dealt with, because the answer there is to delete them rather than to
    /// add a model behind them.</para>
    /// </summary>
    public void UpdateRuleBasedDistresses(RoadSegment segment, double surfaceAgeYears)
    {
        segment.PctFlushing = this.GetFlushing(segment, surfaceAgeYears);
        segment.PctRavelling = this.GetRavelling(segment, surfaceAgeYears);
    }

    /// <summary>
    /// Draws a fresh persistent deviate for one of the level models, clamped the same way an inverted
    /// one is.
    /// <para>The clamp matters as much on a draw as on an inversion: it is what stops one segment in
    /// forty being handed a lifetime of deterioration well outside the evidence, and applying it in
    /// both places is what keeps a rehabilitated segment's spread the same as that of the network it
    /// rejoins.</para>
    /// </summary>
    public double DrawLevelDeviate(Random random)
    {
        double deviate = NormalDistribution.PhiInverse(random.NextDouble());
        return Math.Clamp(deviate, -_constants.DetMultiplierClampSd, _constants.DetMultiplierClampSd);
    }

    #endregion

    #region Flushing and ravelling - rule based

    /// <summary>
    /// Surface age at which flushing and ravelling begin: a fraction of the surface expected life.
    /// Where the input expected life is zero the network median stands in, because an onset age of
    /// zero would start a new surface distressed.
    /// </summary>
    public double RuleDistressOnsetAge(RoadSegment segment)
    {
        double expectedLife = segment.SurfaceExpectedLife > 0.0
            ? segment.SurfaceExpectedLife
            : _constants.RuleSurfaceLifeDefaultYears;

        return _constants.RuleOnsetLifeFraction * expectedLife;
    }

    /// <summary>
    /// Flushing percentage. Zero until the onset age, then a straight line. Both the rate and the
    /// traffic factor are engineering judgement held in lookups.xlsx, not fitted values.
    /// </summary>
    public double GetFlushing(RoadSegment segment, double surfaceAgeYears)
    {
        return this.RuleDistressExtent(segment, surfaceAgeYears, _constants.RuleFlushingRatePerYear);
    }

    /// <summary>
    /// Ravelling percentage. Same rule and the same caveat as flushing.
    /// </summary>
    public double GetRavelling(RoadSegment segment, double surfaceAgeYears)
    {
        return this.RuleDistressExtent(segment, surfaceAgeYears, _constants.RuleRavellingRatePerYear);
    }

    private double RuleDistressExtent(RoadSegment segment, double surfaceAgeYears, double ratePerYear)
    {
        double onsetAge = this.RuleDistressOnsetAge(segment);
        if (surfaceAgeYears < onsetAge)
        {
            return 0.0;
        }
        return ratePerYear * (surfaceAgeYears - onsetAge) * _constants.RuleTrafficFactor;
    }

    #endregion

    #region Year zero - turning an observation into a persistent draw

    /// <summary>
    /// Inverts a level model at the segment's year-zero age, so the segment starts the forecast at
    /// exactly the condition that was surveyed.
    /// <para>Returns the deviate, clamped. The clamp is what stops one extreme reading locking a
    /// segment into fast deterioration for thirty years; the cost is that a clamped segment does NOT
    /// reproduce its surveyed value, which is why the caller records that it was clamped.</para>
    /// </summary>
    /// <param name="observedValue">Surveyed condition, in the model's own units</param>
    /// <param name="mu">Mean of the log condition at the year-zero age</param>
    /// <param name="sigma">Residual standard deviation of the fit</param>
    /// <param name="wasClamped">Return value: true if the clamp changed the deviate</param>
    private double InvertLevelModel(double observedValue, double mu, double sigma, out bool wasClamped)
    {
        // A non-positive reading has no logarithm. It should not occur on rutting or roughness, but a
        // zero would otherwise produce negative infinity and silently poison the whole trajectory.
        double safeObserved = Math.Max(observedValue, MinimumObservableCondition);

        double deviate = (Math.Log(safeObserved) - mu) / sigma;
        double clamped = Math.Clamp(deviate, -_constants.DetMultiplierClampSd, _constants.DetMultiplierClampSd);

        wasClamped = Math.Abs(clamped - deviate) > ClampDetectionTolerance;
        return clamped;
    }

    /// <summary>
    /// Inverts the RUTTING model at the segment's year-zero age, returning the deviate that makes the
    /// model reproduce the surveyed rut depth.
    /// <para>The inversion has to undo everything the forward model adds after the lognormal core, or
    /// the deviate absorbs it and year zero no longer matches the survey. For chipseal that means
    /// removing the accumulated growth term, and for any segment it means removing the cracking
    /// feedback. With the feedback coefficients at their default of zero the second step does nothing,
    /// but the two must stay in step: change the forward model and this has to change with it.</para>
    ///
    /// <para>At year zero the growth term is zero by construction, so the subtraction is a no-op there
    /// and the inversion is the plain one the specification gives. It is kept because the two halves
    /// must mirror each other for any later inversion - the post-rehabilitation reset among them - and
    /// because a subtraction that quietly stopped matching the forward model would show up only as a
    /// year-zero condition that no longer equals the survey.</para>
    /// </summary>
    public double InvertRutting(RoadSegment segment, double observedRut, double crackingPercent, out bool wasClamped)
    {
        double target = observedRut / (1.0 + _constants.DetFeedbackCrackOnRut * crackingPercent / 100.0);

        if (segment.DeteriorationGroup == GroupChipSeal)
        {
            target -= this.ChipSealRutGrowth(segment);
        }

        return this.InvertLevelModel(target, this.RutMu(segment, segment.SurfaceAge), this.RutSigma(segment), out wasClamped);
    }

    /// <summary>
    /// Inverts the ROUGHNESS model at the segment's year-zero age, returning the deviate that makes
    /// the model reproduce the surveyed IRI. Undoes both feedback terms for the same reason as above.
    /// </summary>
    public double InvertRoughness(RoadSegment segment, double observedIri, double crackingPercent, double rutMillimetres, out bool wasClamped)
    {
        double target = observedIri / (1.0 + _constants.DetFeedbackCrackOnIri * crackingPercent / 100.0);
        target /= (1.0 + _constants.DetFeedbackRutOnIri * rutMillimetres / 10.0);

        return this.InvertLevelModel(target, this.IriMu(segment, segment.SurfaceAge), this.IriSigma(segment), out wasClamped);
    }

    /// <summary>
    /// Turns a surveyed cracking reading at or above the onset threshold into the two persistent
    /// draws, so that year zero reproduces the reading.
    /// <para>The onset position is drawn below the segment's current onset probability - it must be a
    /// position that produces onset at this age, or year zero would contradict the survey. The
    /// severity quantile is an exact inversion, not a draw.</para>
    /// </summary>
    public void InvertCrackingAboveOnset(RoadSegment segment, double observedCracking, Random random,
                                         out double onsetPosition, out double severityQuantile, out bool wasClamped)
    {
        double surfaceAge = segment.SurfaceAge;
        double onsetProbability = this.CrackOnsetProbability(segment, surfaceAge);
        double sigma = this.ModelFor(_coefficients.CrackSeverity, segment, "cracking severity").Sigma;
        double mu = this.CrackSeverityMu(segment, surfaceAge);
        double truncationPoint = this.SeverityTruncationPoint(mu, sigma);

        // Any position strictly below the onset probability gives onset at this age.
        onsetPosition = random.NextDouble() * onsetProbability;

        double observedDeviate = (Math.Log(Math.Max(observedCracking, MinimumObservableCondition)) - mu) / sigma;
        double quantile = (NormalDistribution.Phi(observedDeviate) - truncationPoint) / (1.0 - truncationPoint);
        severityQuantile = Math.Clamp(quantile, 0.0, 1.0);

        // The recursion clamps the deviate when it evaluates, so record here whether that will bite.
        wasClamped = observedDeviate > _constants.DetMultiplierClampSd + ClampDetectionTolerance;
    }

    /// <summary>
    /// Draws the onset position for a segment whose surveyed cracking is below the threshold: it must
    /// be a position that does NOT produce onset at this age.
    /// <para>This is genuinely informative and more so the older the segment - an old segment still
    /// below the threshold must sit high in the onset order, and will crack late or never.</para>
    /// </summary>
    public double DrawOnsetPositionBelowOnset(RoadSegment segment, Random random)
    {
        double onsetProbability = this.CrackOnsetProbability(segment, segment.SurfaceAge);
        return onsetProbability + random.NextDouble() * (1.0 - onsetProbability);
    }

    /// <summary>
    /// Smallest condition value a logarithm is taken of. Guards against a zero or negative reading
    /// producing negative infinity.
    /// </summary>
    private const double MinimumObservableCondition = 1e-6;

    /// <summary>
    /// Tolerance for deciding that the clamp actually changed a deviate, rather than a deviate landing
    /// on the bound by arithmetic.
    /// </summary>
    private const double ClampDetectionTolerance = 1e-9;

    #endregion
}
