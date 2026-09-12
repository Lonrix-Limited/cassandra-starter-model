using JCass_ModelCore.Models;


namespace StarterModel.Objects;

/// <summary>
/// Class to handle initialisation, including helper functions and some domain logic.
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
        
        segment.PctFlushing = _domainModel.FlushingModel.GetInitialValue(segment, segment.PctFlushing, _domainModel.Constants.BaseDate);
        segment.FlushingModelInfo = _domainModel.FlushingModel.GetCalibratedInitialSetupValues(segment, segment.PctFlushing, 0.5);

        segment.PctEdgeBreaks = _domainModel.EdgeBreakModel.GetInitialValue(segment, segment.PctEdgeBreaks, _domainModel.Constants.BaseDate);
        segment.EdgeBreakModelInfo = _domainModel.EdgeBreakModel.GetCalibratedInitialSetupValues(segment, segment.PctEdgeBreaks, 0.5);

        segment.PctScabbing = _domainModel.ScabbingModel.GetInitialValue(segment, segment.PctScabbing, _domainModel.Constants.BaseDate);
        segment.ScabbingModelInfo = _domainModel.ScabbingModel.GetCalibratedInitialSetupValues(segment, segment.PctScabbing, 0.5);

        segment.PctLongTransCracks = _domainModel.LTCracksModel.GetInitialValue(segment, segment.PctLongTransCracks, _domainModel.Constants.BaseDate);
        segment.LTCracksModelInfo = _domainModel.LTCracksModel.GetCalibratedInitialSetupValues(segment, segment.PctLongTransCracks, 0.5);

        segment.PctMeshCracks = _domainModel.MeshCrackModel.GetInitialValue(segment, segment.PctMeshCracks, _domainModel.Constants.BaseDate);
        segment.MeshCrackModelInfo = _domainModel.MeshCrackModel.GetCalibratedInitialSetupValues(segment, segment.PctMeshCracks, 0.5);

        segment.PctShoving = _domainModel.ShovingModel.GetInitialValue(segment, segment.PctShoving, _domainModel.Constants.BaseDate);
        segment.ShovingModelInfo = _domainModel.ShovingModel.GetCalibratedInitialSetupValues(segment, segment.PctShoving, 0.5);

        segment.PctPotholes = _domainModel.PotholeModel.GetInitialValue(segment, segment.PctPotholes, _domainModel.Constants.BaseDate);
        segment.PotholeModelInfo = _domainModel.PotholeModel.GetCalibratedInitialSetupValues(segment, segment.PctPotholes, 0.5);

        segment.RutParameterValue = GetInitialRuttingValue(segment);
        segment.RutIncrement = GetRutIncrementEstimate(segment);

        segment.Naasra85 = GetInitialNaasraValue(segment);
        segment.NaasraIncrement = GetNaasraIncrementEstimate(segment);

        // When returning from this method, the PDI and SDI values will be automatically calculated based on some of the above values.
        // However, it may also involve the Mainenance and Faults percentages. So do a heuristic check to see if these values are outdated
        if (segment.SurfaceAge < 1)
        {
            // If surface age is less than 1 year, we assume the maintenance and faults percentage is zero
            segment.ResetFaultsAndMaintenance();
        }
        
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

    private double GetRutSurveyAge(RoadSegment segment)
    {
        DateTime surveyDate = JCass_Core.Utils.HelperMethods.ParseISODateNoTime(segment.RutSurveyDateString);
        double age = (_domainModel.Constants.BaseDate - surveyDate).TotalDays / 365.25; // Use 365.25 to account for leap years        
        if (age < 0)
        {
            _frameworkModel.LogMessage($"HSD Survey date for segment {segment.FeebackCode} is in the future", false);
        }
        return age;
    }

    private double GetNaasraSurveyAge(RoadSegment segment)
    {
        DateTime surveyDate = JCass_Core.Utils.HelperMethods.ParseISODateNoTime(segment.RoughnessSurveyDateString);
        double age = (_domainModel.Constants.BaseDate - surveyDate).TotalDays / 365.25; // Use 365.25 to account for leap years        
        if (age < 0)
        {
            _frameworkModel.LogMessage($"Roughness Survey date for segment {segment.FeebackCode} is in the future", false);
        }
        return age;
    }

    
    /// <summary>
    /// Get the initial rutting value, taking into account the HSD survey age and the Surfacing and Pavement ages. There are
    /// three possibilities:
    /// <para>1. The HSD survey is older than the Pavement Age: In this case we presume the segment has been rehabilitated
    /// after the survey and return the value in lookup set 'rehab_resets_rut'</para>
    /// <para>2. The HSD survey is not older than the Pavement Age but older than Surface Age: In this case we presume the 
    /// segment has been resurfaced after the survey and calculate the resetted value based on how much the raw rutting value 
    /// (calculated as the maximum of the LWP and RWP 85th percentile rut values) exceeds the reset exceedance threshold, and 
    /// return the resetted value using a formula</para>
    ///</para>
    /// <para>3. The HSD survey is not older than the Pavement Age or the Surface age - return the maximum of the LWP and RWP 85th percentile ruts    
    ///</para>
    /// </summary>
    /// <param name="segment"></param>
    /// <returns></returns>
    private double GetInitialRuttingValue(RoadSegment segment)
    {
        double surveyAge = GetRutSurveyAge(segment);

        // If segment has been rehabilitated, return the lookup value for the rutting reset
        bool hasBeenRehabilitated = segment.PavementAge < surveyAge;
        if (hasBeenRehabilitated) {
            return _domainModel.GetLookupValueNumber("rehab_resets_rut", "all_cats");
        }

        double ruttingRaw = Math.Max(segment.RutLwpMean85, segment.RutRwpMean85);

        // If segment has been resurfaced, determine the rutting exceedance and the reset
        bool hasBeenResurfaced = segment.SurfaceAge < surveyAge;
        if (hasBeenResurfaced)
        {
            double resetExceedenceThreshold = _domainModel.GetLookupValueNumber("reset_exceed_thresh_rut", "preserve");
            double resetImprovementFactor = _domainModel.GetLookupValueNumber("reset_perc_improv_facts_rut", "preserve");

            double resetValue = CalculationUtilities.GetResetBasedOnExceedanceConcept(ruttingRaw, resetExceedenceThreshold, resetImprovementFactor);
            return resetValue;
        }

        // If segment has not been rehabilitated or resurfaced, use the raw rutting value
        return ruttingRaw;

    }


    /// <summary>
    /// Get an estimate of the initial rut rate, in mm per year, based on the current rut value and the surface age. Here,
    /// we take into consideration that there is always some initial settlement. The effective rut rate is 
    /// based on the current rut minus the setting-in value (from lookup table) divided by the surface age. 
    /// A check is done to ensure the returned value is within a reasonable range (0.05 minimum to 1.5 maximum).
    /// </summary>    
    /// <returns>The estimated current rut rate, in mm/year</returns>
    private double GetRutIncrementEstimate(RoadSegment segment)
    {

        // If a treatment has been applied, use the post-treatment rut increment
        // and not the estimate based on surface age
        double surveyAge = GetRutSurveyAge(segment);
        bool hasBeenTreated = segment.SurfaceAge < surveyAge;
        if (hasBeenTreated)
        {
            return segment.GetRutIncrementAfterTreatment();
        }

        // Get the estimated "settling-in" rut depth from the lookup table
        double settingInRutDepth = _domainModel.GetLookupValueNumber("settling_in_values", "rut");

        // Get the rut increase after settlement. Ensure the value is not negative
        double rutAfterSettlement = Math.Max(0, segment.RutParameterValue - settingInRutDepth);

        double surfAgeSafe = segment.SurfaceAge + 0.1; // Ensure surface age is not zero to avoid division by zero errors
        double rutIncrementEstimate = rutAfterSettlement/ surfAgeSafe;

        //TODO: Make the min and max values configurable in lookups
        return Math.Clamp(rutIncrementEstimate, 0.05, 1.5);

    }

    /// <summary>
    /// Get the initial Naasra value, taking into account the Roughness survey age and the Surfacing and Pavement ages. There are
    /// three possibilities:
    /// <para>1. The HSD survey is older than the Pavement Age: In this case we presume the segment has been rehabilitated
    /// after the survey and return the value in lookup set 'rehab_resets_naasra' mapping to the segment's RoadType</para>
    /// <para>2. The HSD survey is not older than the Pavement Age but older than Surface Age: In this case we presume the 
    /// segment has been resurfaced after the survey and calculate the resetted value based on how much the raw Naasra value 
    /// (based on the 85th percentile Naasra value) exceeds the reset exceedance threshold, and 
    /// return the resetted value using a formula</para>
    ///</para>
    /// <para>3. The HSD survey is not older than the Pavement Age or the Surface age - return the 85th percentile Naasra value    
    ///</para>
    /// </summary>    
    /// <returns></returns>
    private double GetInitialNaasraValue(RoadSegment segment)
    {
        double surveyAge = GetNaasraSurveyAge(segment);

        // If segment has been rehabilitated, return the lookup value for the  reset
        bool hasBeenRehabilitated = segment.PavementAge < surveyAge;
        if (hasBeenRehabilitated)
        {
            return _domainModel.GetLookupValueNumber("rehab_resets_naasra", segment.SurfaceRoadType);
        }

        double naasraRaw = segment.Naasra85;

        // If segment has been resurfaced, determine the rutting exceedance and the reset
        bool hasBeenResurfaced = segment.SurfaceAge < surveyAge;
        if (hasBeenResurfaced)
        {
            double resetExceedenceThreshold = _domainModel.GetLookupValueNumber("reset_exceed_thresh_naasra", segment.SurfaceRoadType);
            double resetImprovementFactor = _domainModel.GetLookupValueNumber("reset_perc_improv_facts_naasra", segment.SurfaceRoadType);

            double resetValue = CalculationUtilities.GetResetBasedOnExceedanceConcept(naasraRaw, resetExceedenceThreshold, resetImprovementFactor);

            return resetValue;

        }

        // If segment has not been rehabilitated or resurfaced, use the raw value
        return naasraRaw;

    }

    /// <summary>
    /// Get an estimate of the initial Naasra rate, in counts per year, based on the current Naasra value and the surface age. Here,
    /// we estimate (from lookup table) the initial post construction Naasra value based on Road Class code. The effective Naasra rate is 
    /// based on the current Naasra value minus the initial value divided by the surface age. 
    /// A check is done to ensure the returned value is within a reasonable range (0.2 minimum to 1.5 maximum).
    /// </summary>    
    /// <returns>The estimated current rut rate, in mm/year</returns>
    private double GetNaasraIncrementEstimate(RoadSegment segment)
    {
        // If a treatment has been applied, use the post-treatment rut increment
        // and not the estimate based on surface age
        double surveyAge = GetRutSurveyAge(segment);
        bool hasBeenTreated = segment.SurfaceAge < surveyAge;
        if (hasBeenTreated)
        {
            return segment.GetNaasraIncrementAfterTreatment();
        }

        // Get the estimated "settling-in" value depth from the lookup table
        double settingInValue= _domainModel.GetLookupValueNumber("settling_in_values", "naasra");

        // Get the rut increase after settlement. Ensure the value is not negative
        double naasrafterSettlement = Math.Max(0, segment.Naasra85 - settingInValue);

        double surfAgeSafe = segment.SurfaceAge + 0.1; // Ensure surface age is not zero to avoid division by zero errors
        double incrementEstimate = naasrafterSettlement / surfAgeSafe;

        //TODO: Make the min and max values configurable in lookups
        return Math.Clamp(incrementEstimate, 0.2, 1.5);

    }


}
