using JCass_ModelCore.Models;


namespace StarterModel.Objects;

/// <summary>
/// Class to handle initialisation, including helper functions and some domain logic.
/// <para>Initialisation turns the client's surveyed data into the model's starting state. The work it does
/// beyond a straight copy is all of one kind: deciding whether a surveyed value still describes the segment.
/// A survey taken before the last resurfacing or rehabilitation describes a road surface that no longer
/// exists, so rutting and roughness are reset where the dates say the survey has been overtaken.</para>
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

        // Turn the surveyed condition into the segment's persistent random draws. Same order as the
        // Incrementer - cracking, then rutting, then roughness - because the rutting and roughness
        // inversions have to undo the cracking feedback term, which means cracking must already be set.
        InitialiseCracking(segment);
        InitialiseRutting(segment);
        InitialiseRoughness(segment);

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

        // TODO - COME BACK TO THIS. A stale-survey reset is still required, and it now covers RUTTING and
        // ROUGHNESS as well as the three visual distresses.
        //
        // What is missing: a segment REHABILITATED after its last survey starts the model carrying the
        // condition of a pavement that has since been rebuilt. The specification gives the values a
        // rehabilitation resets to - cracking 0%, rutting 2.0 mm, IRI 2.5, deflection to the group median
        // for a new pavement - but those are RESET values, and settling them is the Resetter's job.
        // Putting them in lookups.xlsx here would mean inventing the same lookup twice.
        //
        // What is NOT missing any more, and this is a change from the previous note: a segment merely
        // RESURFACED after its survey needs no correction for rutting or roughness. Neither resets on
        // resurfacing under the new models - the rut lives in the pavement and the new surface inherits
        // it, which is the same finding that explains why chipseal rutting shows no trend against
        // surface age in this network's own data. Cracking does recompute on a resurfacing, and because
        // onset is evaluated afresh from the surface age each period rather than latched, that happens
        // on its own with no code here.
        //
        // WHEN: once the Resetter class has been updated.

        return segment;
    }

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
    private void InitialiseCracking(RoadSegment segment)
    {
        DeteriorationModels models = _domainModel.DeteriorationModels;
        Random random = _frameworkModel.Random;

        // As read from inp_pct_cracks by the factory, so it may still carry the not-surveyed sentinel.
        double observedCracking = segment.PctCracking;

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
    /// rutting. Every segment has a rut reading, so there is no inferred case here.
    /// </summary>
    private void InitialiseRutting(RoadSegment segment)
    {
        DeteriorationModels models = _domainModel.DeteriorationModels;

        segment.RutDeviate = models.InvertRutting(segment, segment.RutMeanSurveyed, segment.PctCracking, out bool wasClamped);
        segment.RutInitSource = wasClamped ? RoadSegment.InitSourceClamped : RoadSegment.InitSourceSurveyed;

        segment.RutParameterValue = models.GetRutting(segment, segment.SurfaceAge, segment.PctCracking);
    }

    /// <summary>
    /// Sets the segment's persistent roughness deviate from the surveyed IRI, then its year-zero
    /// roughness.
    /// <para>There is no inferred case here either, and that is worth stating because a quarter of the
    /// network was not measured: those segments carry an imputed reading rather than a detectable
    /// sentinel, so the model cannot tell them apart and does not try to.</para>
    /// </summary>
    private void InitialiseRoughness(RoadSegment segment)
    {
        DeteriorationModels models = _domainModel.DeteriorationModels;

        segment.IriDeviate = models.InvertRoughness(segment, segment.IriSurveyed, segment.PctCracking,
                                                    segment.RutParameterValue, out bool wasClamped);
        segment.IriInitSource = wasClamped ? RoadSegment.InitSourceClamped : RoadSegment.InitSourceSurveyed;

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
