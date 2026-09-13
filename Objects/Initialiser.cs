using JCass_ModelCore.Models;


namespace StarterModel.Objects;

/// <summary>
/// Class to handle initialisation, including helper functions and some domain logic.
/// <para>Initialisation turns the client's surveyed data into the model's starting state. The work it does
/// beyond a straight copy is all of one kind: deciding whether a surveyed value still describes the segment.
/// A survey taken before the last resurfacing or rehabilitation describes a road surface that no longer
/// exists, so the reading is corrected - or discarded - where the dates say it has been overtaken.</para>
///
/// <para>WHY THAT MATTERS MORE HERE THAN ANYWHERE ELSE. The condition models are levels, and year zero
/// turns the reading into the segment's persistent deviate by inverting them. A deviate is carried for
/// the life of the surfacing, so a stale reading is not a stale starting value that washes out after a
/// year or two - it is baked into the segment's character for the whole run.</para>
/// </summary>
public class Initialiser
{
    private ModelBase _frameworkModel;
    private StarterModel _domainModel;

    public Initialiser(ModelBase frameworkModel, StarterModel domainModel)
    {
        _frameworkModel = frameworkModel ?? throw new ArgumentNullException(nameof(frameworkModel), "Domain model cannot be null");
        _domainModel = domainModel ?? throw new ArgumentNullException(nameof(domainModel), "Domain model cannot be null");
    }

    public RoadSegment InitialiseSegment(int iElemIndex)
    {
        // Create a new RoadSegment object based purely on the raw data provided in the string array.
        RoadSegment segment = RoadSegmentFactory.GetFromRawData(_frameworkModel, _domainModel, iElemIndex);

        // Now do checks on the values and handle any anomalous data

        segment.AverageDailyTraffic = Math.Max(1, segment.AverageDailyTraffic); // Ensure ADT is at least 1
        segment.PavementAge = GetPavementAge(segment);
        segment.SurfaceAge = GetSurfacingAge(segment);

        // The chipseal rut growth accumulator starts at ZERO, not at the surface age. The chipseal
        // level model has no age term, so exp(mu) already reproduces the rut as surveyed; seeding this
        // from the surface age would add the same growth a second time and start the median chipseal
        // segment 29% too high. Set before InitialiseRutting, which inverts against it.
        segment.RutGrowthYears = 0.0;

        // Has anything happened to this segment since the survey that makes the reading describe a road
        // surface it no longer has? Decided before anything is initialised, because it changes what each
        // of the three condition models below is given to work from.
        SurveyStatus surveyStatus = this.GetSurveyStatus(segment);
        if (surveyStatus == SurveyStatus.OvertakenByRehabilitation)
        {
            this.ApplyRehabilitationSinceSurvey(segment);
        }

        // Turn the surveyed condition into the segment's persistent random draws. Same order as the
        // Incrementer - cracking, then rutting, then roughness - because the rutting and roughness
        // inversions have to undo the cracking feedback term, which means cracking must already be set.
        InitialiseCracking(segment, surveyStatus);
        InitialiseRutting(segment, surveyStatus);
        InitialiseRoughness(segment, surveyStatus);

        // The two increment parameters are diagnostics reporting what the level models did over a
        // period. There is no previous period at year zero, so there is nothing to report yet.
        segment.RutIncrement = 0.0;
        segment.IriIncrement = 0.0;

        // Flushing and ravelling come from the rule-based model rather than from the survey. The survey
        // readings are not used at all: nine in ten segments read zero flushing and the 99th percentile
        // is 1.5%, so there is no magnitude to honour, and taking the reading would put a segment out of
        // step with the straight line it follows from the next period onwards.
        segment.PctFlushing = _domainModel.DeteriorationModels.GetFlushing(segment, segment.SurfaceAge);
        segment.PctRavelling = _domainModel.DeteriorationModels.GetRavelling(segment, segment.SurfaceAge);

        return segment;
    }

    #region Has the survey been overtaken?

    /// <summary>
    /// Whether anything has happened to a segment since its last high speed survey that makes the
    /// reading describe a surface, or a pavement, it no longer has.
    /// </summary>
    private enum SurveyStatus
    {
        /// <summary>Nothing has been done since the survey. The reading still describes the segment.</summary>
        Current,

        /// <summary>A new surface was laid after the survey. The reading describes the surface underneath it.</summary>
        OvertakenByResurfacing,

        /// <summary>The pavement was rebuilt after the survey. The reading describes a road that no longer exists.</summary>
        OvertakenByRehabilitation
    }

    /// <summary>
    /// Compares the survey date against the surfacing and pavement dates.
    ///
    /// <para>A rehabilitation takes precedence over a resurfacing, because it is the stronger statement
    /// about the same segment: a rebuilt pavement carries a new surface with it.</para>
    ///
    /// <para>The pavement date is a placeholder on most of this network - 66% fall on 1 January, and 400
    /// segments are dated 1960 - which is why no model term is allowed to run on it. It is safe HERE,
    /// and only here, because a placeholder is an old date and an old date cannot be later than a survey
    /// taken in the last year or two. It is only ever used as a comparison, never as an age.</para>
    /// </summary>
    private SurveyStatus GetSurveyStatus(RoadSegment segment)
    {
        DateTime surveyDate = this.ParseDate(segment.SurveyDateString, segment, "inp_hsd_survey_date");
        DateTime surfacingDate = this.ParseDate(segment.SurfacingDateString, segment, "inp_surf_date");
        DateTime pavementDate = this.ParseDate(segment.PavementDateString, segment, "inp_pave_date");

        if (pavementDate > surveyDate) return SurveyStatus.OvertakenByRehabilitation;
        if (surfacingDate > surveyDate) return SurveyStatus.OvertakenByResurfacing;
        return SurveyStatus.Current;
    }

    /// <summary>
    /// Parses one of the segment's ISO date strings, naming the segment and the input column if it will
    /// not parse. Deliberately an error rather than a fallback: a date this method cannot read is a date
    /// the comparison above cannot make, and quietly treating the survey as current is how a segment
    /// keeps a stale reading for a thirty year run with nothing reporting it.
    /// </summary>
    private DateTime ParseDate(string isoDate, RoadSegment segment, string inputColumn)
    {
        try
        {
            return JCass_Core.Utils.HelperMethods.ParseISODateNoTime(isoDate);
        }
        catch (Exception ex)
        {
            throw new Exception($"Cannot read '{inputColumn}' on segment {segment.FeebackCode}: the value " +
                                $"'{isoDate}' is not an ISO date in yyyymmdd form. Details: {ex.Message}");
        }
    }

    /// <summary>
    /// Marks a segment whose pavement was rebuilt after its last survey as a rehabilitated pavement, and
    /// gives it the deflection of one.
    ///
    /// <para>Everything else follows from that. The segment has no usable reading of any kind - cracking,
    /// rutting and roughness all describe the road that was dug up - so each of the three is initialised
    /// from the model instead, at the segment's current surface age and with the as-new offsets the flag
    /// now entitles it to. That is the same treatment the specification gives any segment with no usable
    /// reading, and it is why nothing is imposed here: the as-new values belong at the moment of
    /// treatment, and this rehabilitation happened before the model run began.</para>
    ///
    /// <para>The measured deflection goes too. It was measured on the pavement that has since been
    /// replaced, and deflection is a covariate in cracking onset, chipseal rutting and both roughness
    /// models.</para>
    /// </summary>
    private void ApplyRehabilitationSinceSurvey(RoadSegment segment)
    {
        segment.HasBeenRehabilitated = true;
        segment.CentralDeflection = _domainModel.Constants.GetRehabResetDeflection(segment.DeteriorationGroup);
    }

    #endregion

    /// <summary>
    /// Sets the segment's two persistent cracking draws and its held sub-threshold value, then its
    /// year-zero cracking, from the surveyed reading.
    ///
    /// <para>Three cases, and the difference between them is what the reading entitles the segment to.
    /// A reading at or above the onset threshold says the segment HAS cracked, so its onset position
    /// must be one that produces onset at this age, and its severity quantile is an exact inversion. A
    /// reading below the threshold says it has NOT cracked, which is real information and more of it
    /// the older the segment: an old surface still uncracked must sit high in the onset order and will
    /// crack late or never.</para>
    ///
    /// <para>The third case is a segment with no visual survey at all, which the input marks with -1 -
    /// nearly 30% of this network. It is entitled to NOTHING, and that is the point: it gets a blind
    /// onset draw, exactly as a segment of its age and traffic would have. Giving it the
    /// below-threshold treatment instead would turn "nobody looked" into "we know it has not cracked",
    /// and bias almost a third of the network towards late onset for the whole run. Its held
    /// sub-threshold value is set to zero, which is the engineer's call; the alternative differs by at
    /// most one percentage point, because Part C is bounded by the onset threshold.</para>
    /// </summary>
    private void InitialiseCracking(RoadSegment segment, SurveyStatus surveyStatus)
    {
        DeteriorationModels models = _domainModel.DeteriorationModels;
        Random random = _frameworkModel.Random;

        // As read from inp_pct_cracks by the factory, so it may still carry the not-surveyed sentinel.
        double observedCracking = segment.PctCracking;

        if (surveyStatus != SurveyStatus.Current)
        {
            // The segment has been resurfaced or rebuilt since the survey, so it starts the run with NO
            // CRACKING AT ALL - the engineer's decision, and the reason for it is that the alternative is
            // worse in a specific way: a segment that was resealed precisely because it was cracked would
            // otherwise carry the old surface's crack severity onto the new one, sit above the treatment
            // triggers, and be put forward for treatment again within a year or two of having had one.
            //
            // Both draws are replaced. The onset position comes from the band that means "has not cracked
            // at this age", which is what keeps the segment uncracked rather than leaving it one unlucky
            // draw away from being back where it started.
            segment.CrackOnsetPosition = models.DrawOnsetPositionBelowOnset(segment, random);
            segment.CrackSeverityQuantile = random.NextDouble();
            segment.CrackingBelowOnset = 0.0;
            segment.CrackingInitSource = RoadSegment.InitSourceInferred;

            segment.PctCracking = models.GetCracking(segment, segment.SurfaceAge);
            return;
        }

        if (observedCracking < 0.0)
        {
            // No visual distress survey on this segment. Initialise it from the model and flag it.
            segment.CrackOnsetPosition = random.NextDouble();
            segment.CrackSeverityQuantile = random.NextDouble();
            segment.CrackingBelowOnset = 0.0;
            segment.CrackingInitSource = RoadSegment.InitSourceInferred;
        }
        else if (observedCracking >= _domainModel.Constants.DetCrackOnsetPercent)
        {
            models.InvertCrackingAboveOnset(segment, observedCracking, random,
                                            out double onsetPosition, out double severityQuantile, out bool wasClamped);
            segment.CrackOnsetPosition = onsetPosition;
            segment.CrackSeverityQuantile = severityQuantile;

            // Not used while the segment stays cracked, but a resurfacing can take it back below the
            // threshold, and the value it lands on must already exist rather than being invented then.
            segment.CrackingBelowOnset = models.DrawCrackingBelowOnset(segment, random);

            segment.CrackingInitSource = wasClamped ? RoadSegment.InitSourceClamped : RoadSegment.InitSourceSurveyed;
        }
        else
        {
            segment.CrackOnsetPosition = models.DrawOnsetPositionBelowOnset(segment, random);
            segment.CrackSeverityQuantile = random.NextDouble();
            segment.CrackingBelowOnset = observedCracking;
            segment.CrackingInitSource = RoadSegment.InitSourceSurveyed;
        }

        // Run the model forward at the year-zero age. For a surveyed segment this returns the surveyed
        // value, which is the check that the inversion above is the exact inverse of the forward model.
        segment.PctCracking = models.GetCracking(segment, segment.SurfaceAge);
    }

    /// <summary>
    /// Sets the segment's persistent rutting deviate from the surveyed rut depth, then its year-zero
    /// rutting. Every segment has a rut reading, so the only inferred case is a stale one.
    ///
    /// <para>A REBUILT PAVEMENT HAS NO READING. Where the pavement was replaced after the survey, the
    /// measured rut belongs to the road that was dug up, so the deviate is drawn blind and the segment
    /// starts wherever the model puts a rebuilt pavement of its surface age.</para>
    ///
    /// <para>A RESURFACED SEGMENT HAS A READING THAT NEEDS SCALING, AND ONLY ON ASPHALT. Asphalt rutting
    /// renews with a new surface - 2.13 mm under surfaces up to six years old against 3.49 mm under
    /// surfaces over twenty, on pavements of the same age - so a reading taken before the last overlay
    /// overstates it, and inverting the overstatement would bake it into the segment's deviate for the
    /// whole run. Chipseal is the opposite and its factor is 1.0: a seal follows the shape of what it is
    /// laid on, so the surveyed rut still describes the segment after a reseal.</para>
    /// </summary>
    private void InitialiseRutting(RoadSegment segment, SurveyStatus surveyStatus)
    {
        DeteriorationModels models = _domainModel.DeteriorationModels;

        if (surveyStatus == SurveyStatus.OvertakenByRehabilitation)
        {
            segment.RutDeviate = models.DrawLevelDeviate(_frameworkModel.Random);
            segment.RutInitSource = RoadSegment.InitSourceInferred;
        }
        else
        {
            double observedRut = segment.RutMeanSurveyed;
            if (surveyStatus == SurveyStatus.OvertakenByResurfacing)
            {
                observedRut *= _domainModel.Constants.GetStaleSurveyResurfacingFactorRut(segment.DeteriorationGroup);
            }

            segment.RutDeviate = models.InvertRutting(segment, observedRut, segment.PctCracking, out bool wasClamped);
            segment.RutInitSource = wasClamped ? RoadSegment.InitSourceClamped : RoadSegment.InitSourceSurveyed;
        }

        segment.RutParameterValue = models.GetRutting(segment, segment.SurfaceAge, segment.PctCracking);
    }

    /// <summary>
    /// Sets the segment's persistent roughness deviate from the surveyed IRI, then its year-zero
    /// roughness.
    /// <para>There is no inferred case here either, and that is worth stating because a quarter of the
    /// network was not measured: those segments carry an imputed reading rather than a detectable
    /// sentinel, so the model cannot tell them apart and does not try to.</para>
    /// </summary>
    private void InitialiseRoughness(RoadSegment segment, SurveyStatus surveyStatus)
    {
        DeteriorationModels models = _domainModel.DeteriorationModels;

        if (surveyStatus == SurveyStatus.OvertakenByRehabilitation)
        {
            segment.IriDeviate = models.DrawLevelDeviate(_frameworkModel.Random);
            segment.IriInitSource = RoadSegment.InitSourceInferred;
        }
        else
        {
            // Roughness renews on BOTH classes, unlike rutting - strongly on asphalt and weakly on
            // chipseal - so a reading taken before the last resurfacing is scaled on either.
            double observedIri = segment.IriSurveyed;
            if (surveyStatus == SurveyStatus.OvertakenByResurfacing)
            {
                observedIri *= _domainModel.Constants.GetStaleSurveyResurfacingFactorIri(segment.DeteriorationGroup);
            }

            segment.IriDeviate = models.InvertRoughness(segment, observedIri, segment.PctCracking,
                                                        segment.RutParameterValue, out bool wasClamped);
            segment.IriInitSource = wasClamped ? RoadSegment.InitSourceClamped : RoadSegment.InitSourceSurveyed;
        }

        segment.Iri = models.GetRoughness(segment, segment.SurfaceAge, segment.PctCracking, segment.RutParameterValue);
    }

    private double GetPavementAge(RoadSegment segment)
    {
        try
        {
            DateTime pavDate = JCass_Core.Utils.HelperMethods.ParseISODateNoTime(segment.PavementDateString);
            double age = (_domainModel.Constants.BaseDate - pavDate).TotalDays / 365.25; // Use 365.25 to account for leap years

            // To duplicate jFunction setup, we must round age to 2 decimals
            age = Math.Round(age, 2);

            if (age < 0)
            {
                _frameworkModel.LogMessage($"Pavement date for segment {segment.FeebackCode} is in the future", false);
            }
            return age;
        }
        catch(Exception ex)
        {
            throw new Exception($"Error calculating pavement age for segment {segment.FeebackCode}: {ex.Message}");
        }
    }

    private double GetSurfacingAge(RoadSegment segment)
    {
        try
        {
            DateTime surfDate = JCass_Core.Utils.HelperMethods.ParseISODateNoTime(segment.SurfacingDateString);
            double age = (_domainModel.Constants.BaseDate - surfDate).TotalDays / 365.25; // Use 365.25 to account for leap years

            // To duplicate jFunction setup, we must round age to 2 decimals
            age = Math.Round(age, 2);

            if (age < 0)
            {
                _frameworkModel.LogMessage($"Surfacing date for segment {segment.FeebackCode} is in the future", false);
            }
            return Math.Max(age, 0.1);  //Ensure age is not zero to avoid division by zero errors
        }
        catch (Exception ex)
        {
            throw new Exception($"Error calculating surfacing age for segment {segment.FeebackCode}: {ex.Message}");
        }
    }

}
