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
        RoadSegment segment = RoadSegmentFactory.GetFromRawData(_frameworkModel, iElemIndex);

        // Now do checks on the values and handle any anomalous data

        segment.AverageDailyTraffic = Math.Max(1, segment.AverageDailyTraffic); // Ensure ADT is at least 1
        segment.PavementAge = GetPavementAge(segment);
        segment.SurfaceAge = GetSurfacingAge(segment);

        // Rutting and roughness: reset where the survey predates the last surfacing or rehabilitation
        segment.RutParameterValue = GetInitialRuttingValue(segment);
        segment.Iri = GetInitialIriValue(segment);

        // Deterioration rates. These come from the client's own estimates in the input data, already read by
        // the factory; all that is done here is to floor them at zero.
        //
        // TODO (stage 2, Incrementer): the replacement increment models for rutting, roughness, cracking,
        // flushing and ravelling are still to be specified. When they land, decide whether the initial rate
        // still comes from the input columns or is derived by the new models, and whether a segment whose
        // survey has been overtaken by a treatment should get a post-treatment rate instead of the surveyed
        // one. The jFunction-era model estimated both rates from the current value and the surface age, and
        // substituted a post-treatment rate in that case.
        segment.RutIncrement = Math.Max(0, segment.RutIncrement);
        segment.IriIncrement = Math.Max(0, segment.IriIncrement);

        // TODO - COME BACK TO THIS. A stale-survey reset for cracking, flushing and ravelling IS required.
        // It is not optional and it is not yet here: the three distresses are currently taken exactly as
        // surveyed, so a segment resurfaced after its last LCMS run starts the model carrying cracking that
        // has since been sealed over.
        //
        // WHEN: once the Resetter class has been updated. The Resetter is where the reset lookups get
        // settled, and this correction is the same operation applied to historical data - the surfacing has
        // already happened, before the base date, rather than being applied by the model. Doing it before
        // the Resetter is in place would mean inventing the lookup twice.
        //
        // HOW: a simple lookup, not the piecewise reset curves the jFunction-era S-curve models used. Those
        // were removed with the S-curve models and are not coming back.
        //
        // The shape to follow is GetInitialRuttingValue and GetInitialIriValue below: compare the survey age
        // against the pavement age (rehabilitation) and then the surface age (resurfacing), and reset on
        // whichever applies.

        return segment;
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

    /// <summary>
    /// Gets the age of the high speed data survey, in years, relative to the model's base date. One survey
    /// date now covers rutting, roughness and the LCMS visual distresses, because they are collected on the
    /// same run.
    /// </summary>
    private double GetSurveyAge(RoadSegment segment)
    {
        DateTime surveyDate = JCass_Core.Utils.HelperMethods.ParseISODateNoTime(segment.SurveyDateString);
        double age = (_domainModel.Constants.BaseDate - surveyDate).TotalDays / 365.25; // Use 365.25 to account for leap years
        if (age < 0)
        {
            _frameworkModel.LogMessage($"HSD Survey date for segment {segment.FeebackCode} is in the future", false);
        }
        return age;
    }

    /// <summary>
    /// Get the initial rutting value, taking into account the HSD survey age and the Surfacing and Pavement ages. There are
    /// three possibilities:
    /// <para>1. The HSD survey is older than the Pavement Age: In this case we presume the segment has been rehabilitated
    /// after the survey and return the value in lookup set 'rehab_resets_rut'</para>
    /// <para>2. The HSD survey is not older than the Pavement Age but older than Surface Age: In this case we presume the
    /// segment has been resurfaced after the survey and calculate the resetted value based on how much the surveyed rutting
    /// value exceeds the reset exceedance threshold, and return the resetted value using a formula</para>
    /// <para>3. The HSD survey is not older than the Pavement Age or the Surface age - return the surveyed rut value</para>
    /// </summary>
    private double GetInitialRuttingValue(RoadSegment segment)
    {
        double surveyAge = GetSurveyAge(segment);

        // If segment has been rehabilitated, return the lookup value for the rutting reset
        bool hasBeenRehabilitated = segment.PavementAge < surveyAge;
        if (hasBeenRehabilitated) {
            return _domainModel.GetLookupValueNumber("rehab_resets_rut", "all_cats");
        }

        double ruttingRaw = segment.RutMeanSurveyed;

        // If segment has been resurfaced, determine the rutting exceedance and the reset
        bool hasBeenResurfaced = segment.SurfaceAge < surveyAge;
        if (hasBeenResurfaced)
        {
            double resetExceedenceThreshold = _domainModel.GetLookupValueNumber("reset_exceed_thresh_rut", "preserve");
            double resetImprovementFactor = _domainModel.GetLookupValueNumber("reset_perc_improv_facts_rut", "preserve");

            double resetValue = CalculationUtilities.GetResetBasedOnExceedanceConcept(ruttingRaw, resetExceedenceThreshold, resetImprovementFactor);
            return resetValue;
        }

        // If segment has not been rehabilitated or resurfaced, use the surveyed rutting value
        return ruttingRaw;
    }

    /// <summary>
    /// Get the initial IRI value, taking into account the HSD survey age and the Surfacing and Pavement ages. The three
    /// possibilities are the same as for rutting:
    /// <para>1. The HSD survey is older than the Pavement Age: presume the segment has been rehabilitated after the
    /// survey and return the value in lookup set 'rehab_resets_naasra' mapping to the segment's SurfaceRoadType</para>
    /// <para>2. The HSD survey is not older than the Pavement Age but older than Surface Age: presume the segment has
    /// been resurfaced after the survey and reset the value based on how much it exceeds the reset exceedance threshold</para>
    /// <para>3. Otherwise - return the surveyed IRI value</para>
    /// <para>IMPORTANT: the roughness reset lookup sets ('rehab_resets_naasra', 'reset_exceed_thresh_naasra' and
    /// 'reset_perc_improv_facts_naasra') hold their values on the NAASRA scale, which is how the engineer set them and
    /// how they are read elsewhere. So the reset is applied in Naasra terms and the result converted back to IRI, rather
    /// than restating the engineer's numbers on a different scale.</para>
    /// </summary>
    private double GetInitialIriValue(RoadSegment segment)
    {
        double surveyAge = GetSurveyAge(segment);

        // If segment has been rehabilitated, return the lookup value for the reset
        bool hasBeenRehabilitated = segment.PavementAge < surveyAge;
        if (hasBeenRehabilitated)
        {
            double rehabResetNaasra = _domainModel.GetLookupValueNumber("rehab_resets_naasra", segment.SurfaceRoadType);
            return RoadSegment.ConvertNaasraToIri(rehabResetNaasra);
        }

        double iriRaw = segment.IriSurveyed;

        // If segment has been resurfaced, determine the exceedance and the reset
        bool hasBeenResurfaced = segment.SurfaceAge < surveyAge;
        if (hasBeenResurfaced)
        {
            double resetExceedenceThreshold = _domainModel.GetLookupValueNumber("reset_exceed_thresh_naasra", segment.SurfaceRoadType);
            double resetImprovementFactor = _domainModel.GetLookupValueNumber("reset_perc_improv_facts_naasra", segment.SurfaceRoadType);

            double naasraRaw = RoadSegment.ConvertIriToNaasra(iriRaw);
            double naasraReset = CalculationUtilities.GetResetBasedOnExceedanceConcept(naasraRaw, resetExceedenceThreshold, resetImprovementFactor);

            return RoadSegment.ConvertNaasraToIri(naasraReset);
        }

        // If segment has not been rehabilitated or resurfaced, use the surveyed value
        return iriRaw;
    }

}
