
using JCass_ModelCore.Models;
using JCass_ModelCore.Treatments;


namespace StarterModel.Objects;

/// <summary>
/// Class to handle Resetting, with supporting logic and helper functions
/// </summary>
public class Resetter
{

    private ModelBase _frameworkModel;
    private StarterModel _domainModel;

    public Resetter(ModelBase frameworkModel, StarterModel domainModel)
    {
        _frameworkModel = frameworkModel ?? throw new ArgumentNullException(nameof(frameworkModel), "Domain model cannot be null");
        _domainModel = domainModel ?? throw new ArgumentNullException(nameof(domainModel), "Domain model cannot be null");
    }

    public RoadSegment Reset(RoadSegment segment, int period, TreatmentInstance treatment)
    {
        
        if (treatment is null) return segment;

        string treatmentCategory = _frameworkModel.TreatmentTypes[treatment.TreatmentName].Category;
        string treatmentName = treatment.TreatmentName.ToLower();
        bool isRehab = treatmentName.StartsWith("rehab");
        bool isPreseal = treatmentName.StartsWith("hmaint") || treatmentName.StartsWith("preseal");

        // Reset (or increment where not applicable) all properties related to model parameters
        // Keep the code same order as the model parameter list

        segment.AverageDailyTraffic = segment.AverageDailyTraffic * (1 + segment.TrafficGrowthPercent / 100);
        // No need to reset HCV count as it is automatically calculated based on the AverageDailyTraffic and HCVPercent

        if (isRehab)
        {
            segment.PavementAge = 0;
            segment.PavementRemainingLife = _frameworkModel.GetLookupValueNumber("pavement_expected_life", segment.RoadType);
        }
        else
        {
            segment.PavementAge = segment.PavementAge + 1;
            segment.PavementRemainingLife = segment.PavementRemainingLife - 1;
        }

        // No need to update Pavement Life Achieved and HCV Risk because it is automatically calculated based on the HCV and Pavement Life Achieved

        segment.SurfaceMaterial = _frameworkModel.GetLookupValueText("treat_surf_materials", treatment.TreatmentName);
        segment.SurfaceClass = _frameworkModel.GetLookupValueText("treat_surf_class", treatment.TreatmentName);        
        
        // If rehab, number of surfacing becomes 1. Otherwise, increase number of surfacings but only if it is a chipseal. If AC, then it remains the same.                
        if (isRehab)
        {
            //Surface Thickness to reset to, based on lookup of surface material type applied if treatment is pavement renewal (Rehab)
            segment.SurfaceThickness = _frameworkModel.GetLookupValueNumber("surf_thickness_new", segment.SurfaceMaterial);
            segment.SurfaceNumberOfLayers = 1;  // Reset number of surface layer count
        }
        else
        {
            //Surface Thickness to add, based on lookup of surface material type applied if treatment is surface renewal
            segment.SurfaceThickness = segment.SurfaceThickness + _frameworkModel.GetLookupValueNumber("surf_thickness_add", segment.SurfaceMaterial);

            // If this is a chipseal, increase number of layers. If this is an AC, then it remains the same.
            segment.SurfaceNumberOfLayers = segment.SurfaceIsChipSealFlag == 1 ? segment.SurfaceNumberOfLayers + 1 : segment.SurfaceNumberOfLayers;
        }

        segment.SurfaceFunction = this.GetSurfaceFunction(treatment.TreatmentName, isPreseal, segment.SurfaceFunction);
        
        segment.SurfaceExpectedLife = this.GetExpectedSurfaceLife(segment);
        segment.SurfaceAge = isPreseal ? segment.SurfaceAge + 1 : 0;  //All treatments reset surface age to zero except if Preseal
        // Note: surface life achieved and surface remaining life are automatically calculated based on the surface age and expected life

        // The chipseal rut growth accumulator is a DIFFERENT clock from the surface age and must not be
        // reset alongside it. A reseal leaves it running, because a chipseal follows the shape of what
        // it is laid on and inherits the rut rather than renewing it - which is why chipseal rut reads
        // 4.19 mm under surfaces up to six years old against 3.77 mm under surfaces over twenty, where
        // asphalt goes the other way. Only a rehabilitation returns it to zero.
        segment.RutGrowthYears = isRehab ? 0.0 : segment.RutGrowthYears + 1;

        // Reset visual distresses
        double flushingPrevious = segment.PctFlushing;
        segment.PctFlushing = _domainModel.FlushingModel.GetValueAfterReset(segment, segment.PctFlushing,treatmentCategory);
        segment.FlushingModelInfo = _domainModel.FlushingModel.GetResettedSetupValues(segment, flushingPrevious, treatmentCategory, segment.FlushingModelInfo);

        double edgeBreaksPrevious = segment.PctEdgeBreaks;
        segment.PctEdgeBreaks = _domainModel.EdgeBreakModel.GetValueAfterReset(segment, segment.PctEdgeBreaks, treatmentCategory);
        segment.EdgeBreakModelInfo = _domainModel.EdgeBreakModel.GetResettedSetupValues(segment, edgeBreaksPrevious, treatmentCategory, segment.EdgeBreakModelInfo);

        double scabbingPrevious = segment.PctScabbing;
        segment.PctScabbing = _domainModel.ScabbingModel.GetValueAfterReset(segment, segment.PctScabbing, treatmentCategory);
        segment.ScabbingModelInfo = _domainModel.ScabbingModel.GetResettedSetupValues(segment, scabbingPrevious, treatmentCategory, segment.ScabbingModelInfo);

        double ltCrackingPrevious = segment.PctLongTransCracks;
        segment.PctLongTransCracks = _domainModel.LTCracksModel.GetValueAfterReset(segment, segment.PctLongTransCracks, treatmentCategory);
        segment.LTCracksModelInfo = _domainModel.LTCracksModel.GetResettedSetupValues(segment, ltCrackingPrevious, treatmentCategory, segment.LTCracksModelInfo);

        double meshCracksPrevious = segment.PctMeshCracks;
        segment.PctMeshCracks = _domainModel.MeshCrackModel.GetValueAfterReset(segment, segment.PctMeshCracks, treatmentCategory);
        segment.MeshCrackModelInfo = _domainModel.MeshCrackModel.GetResettedSetupValues(segment, meshCracksPrevious, treatmentCategory, segment.MeshCrackModelInfo);

        double shovingPrevious = segment.PctShoving;
        segment.PctShoving = _domainModel.ShovingModel.GetValueAfterReset(segment, segment.PctShoving, treatmentCategory);
        segment.ShovingModelInfo = _domainModel.ShovingModel.GetResettedSetupValues(segment, shovingPrevious, treatmentCategory, segment.ShovingModelInfo);

        double potholesPrevious = segment.PctPotholes;
        segment.PctPotholes = _domainModel.PotholeModel.GetValueAfterReset(segment, segment.PctPotholes, treatmentCategory);
        segment.PotholeModelInfo = _domainModel.PotholeModel.GetResettedSetupValues(segment, potholesPrevious, treatmentCategory, segment.PotholeModelInfo);

        segment.RutParameterValue = this.GetResetttedRut(segment, treatmentCategory);
        segment.RutIncrement = segment.GetRutIncrementAfterTreatment();
        
        segment.Naasra85 = this.GetResetttedNaasra(segment, isRehab);
        segment.NaasraIncrement = segment.GetNaasraIncrementAfterTreatment();

        // Increase the treatment count for the segment. This will also mark the treatment as treated, and reset the 
        // historical maintenance quantities so that it no longer influences PDI and SDI calculations.
        segment.TreatmentCount++;

        // Ranking parameters will be calculated by the framework model

        return segment;

    }

    private string GetSurfaceFunction(string treatmentName, bool isPreseal, string currentSurfaceFunction)
    {
        if (isPreseal) return "1a";
        
        if (treatmentName.ToLower().StartsWith("rehab_ac")) return "2";

        if (treatmentName.ToLower().StartsWith("rehab_cs")) return "1";

        if (currentSurfaceFunction == "1a") return "H";

        if (currentSurfaceFunction == "1") return "2";

        if (currentSurfaceFunction == "2") return "R";

        // If none of the others are fired, return the current surface function, e.g surface is aready a "R"
        return currentSurfaceFunction;


    }

    private double GetExpectedSurfaceLife(RoadSegment segment)
    {
        if (segment.SurfaceClass == "blocks") return segment.SurfaceExpectedLife; // Blocks have a fixed expected life, no lookup needed
        if (segment.SurfaceClass == "concrete") return segment.SurfaceExpectedLife; // Concrete has a fixed expected life, no lookup needed
        if (segment.SurfaceClass == "other") return segment.SurfaceExpectedLife;

        // Since preseal is a temporary treatment, it does not have an actual expected life
        // So based the expected life on the Reseal 'R' surface function - this is needed for the S-curve reset 
        string surfFuncToUse = segment.SurfaceFunction == "1a" ? "R" : segment.SurfaceFunction;

        string lookupKey = $"{surfFuncToUse}_{segment.SurfaceMaterial}_{segment.RoadClass}".ToLower();
        bool keyExists = _frameworkModel.Lookups["surf_life_exp"].ContainsKey(lookupKey);
        if (keyExists)
        {
            return _frameworkModel.GetLookupValueNumber("surf_life_exp", lookupKey);
        }
        else
        {
            throw new KeyNotFoundException($"Expected surface life not found for {segment.FeebackCode}. Surface function = '{segment.SurfaceFunction}', Material = '{segment.SurfaceMaterial}', Road class = '{segment.RoadClass}'.");
        }        
    }
    
    private double GetResetttedRut(RoadSegment segment, string treatmnentCatAnyCase)
    {        
        if (segment.SurfaceIsChipSealOrACFlag == 0)
        {
            return segment.RutParameterValue; // Rut remains constant for non ChipSeal or AC surfaces
        }

        if (segment.SurfaceFunction == "1a")
        {
            return segment.RutParameterValue; // This indicates preseal has just been applied, so no reset
        }

        string treatmentCategory = treatmnentCatAnyCase.ToLower();
        
        if (treatmentCategory.Contains("rehab"))
        {
            return _frameworkModel.GetLookupValueNumber("rehab_resets_rut", "all_cats");
        }
        else if (treatmentCategory.Contains("holding") || treatmentCategory.Contains("heavymaint"))
        {
            double exceedanceThreshold = _frameworkModel.GetLookupValueNumber("reset_exceed_thresh_rut", "holding_or_repairs");
            double improvementFactor = _frameworkModel.GetLookupValueNumber("reset_perc_improv_facts_rut", "holding_or_repairs");
            return CalculationUtilities.GetResetBasedOnExceedanceConcept(segment.RutParameterValue, exceedanceThreshold, improvementFactor);
        }
        else if (treatmentCategory.Contains("preserve"))
        {
            double exceedanceThreshold = _frameworkModel.GetLookupValueNumber("reset_exceed_thresh_rut", "preserve");
            double improvementFactor = _frameworkModel.GetLookupValueNumber("reset_perc_improv_facts_rut", "preserve");
            return CalculationUtilities.GetResetBasedOnExceedanceConcept(segment.RutParameterValue, exceedanceThreshold, improvementFactor);
        }
        else
        {
            throw new ArgumentException($"Treatment category '{treatmentCategory}' not handled in generic reset for S-Curve parameters");
        }

    }

    private double GetResetttedNaasra(RoadSegment segment, bool isRehab)
    {
        if (segment.SurfaceIsChipSealOrACFlag == 0)
        {
            return segment.Naasra85; // Rut remains constant for non ChipSeal or AC surfaces
        }

        if (segment.SurfaceFunction == "1a")
        {
            return segment.Naasra85; // This indicates preseal has just been applied, so no reset
        }

        if (isRehab)
        {
            return _frameworkModel.GetLookupValueNumber("rehab_resets_naasra", segment.SurfaceRoadType);
        }
        else
        {
            double exceedanceThreshold = _frameworkModel.GetLookupValueNumber("reset_exceed_thresh_naasra", segment.SurfaceRoadType);
            double improvementFactor = _frameworkModel.GetLookupValueNumber("reset_perc_improv_facts_naasra", segment.SurfaceRoadType);
            return CalculationUtilities.GetResetBasedOnExceedanceConcept(segment.Naasra85, exceedanceThreshold, improvementFactor);
        }


    }


}
