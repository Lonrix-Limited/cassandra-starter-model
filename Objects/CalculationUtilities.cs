using JCass_ModelCore.Models;

namespace StarterModel.Objects;

/// <summary>
/// Static class to help with calculation of Indexes (PDI, SDI, etc.) and Objective Functions.
/// </summary>
public static class CalculationUtilities
{
        
    /// <summary>
    /// Calculates the Pavement Distress Index (PDI) for a road segment based on the current period. Short term includes maintenance
    /// </summary>    
    /// <param name="currentPeriod">Current modelling period (e.g. 1,2,3...) used to determine whether we are in short or long term</param>
    /// <returns></returns>
    public static double GetPavementDistressIndex(RoadSegment segment, ModelBase frameworkModel, StarterModel roadModel, int currentPeriod)
    {
        Constants constants = roadModel.Constants;
        double basePDI = GetPavementDistressIndexBase(segment, constants);
        bool isShortTerm = currentPeriod <= constants.CSShortTermPeriod;
        if (isShortTerm && segment.TreatmentCount == 0)
        {
            // Only add maintenance penalties if we are in the short term period and the segment has not received any treatments yet, as per the model's design.
            return basePDI + segment.MaintPotholeExtentLastYear * constants.PdiMaintPotholeFactor +
                segment.MaintPavementExtentLastYear * constants.PdiMaintPavementFactor;
        }
        else
        {
            return basePDI;   //Do not consider historical maintenance beyond the short term period for PDI calculation, as per the model's design.
        }
    }

    /// <summary>
    /// Calculates the Surface Distress Index (SDI) for a road segment based on the current period. Short term includes maintenance
    /// </summary>    
    /// <param name="currentPeriod">Current modelling period (e.g. 1,2,3,...) used to determine whether we are in short or long term</param>
    /// <returns></returns>
    public static double GetSurfacingDistressIndex(RoadSegment segment, ModelBase frameworkModel, StarterModel roadModel, int currentPeriod)
    {
        Constants constants = roadModel.Constants;
        double baseSDI = GetSurfacingDistressIndexBase(segment);
        bool isShortTerm = currentPeriod <= constants.CSShortTermPeriod;
        if (isShortTerm && segment.TreatmentCount == 0)
        {
            // Only add maintenance penalties if we are in the short term period and the segment has not received any treatments yet, as per the model's design.
            return baseSDI + segment.MaintPotholeExtentLastYear * constants.SdiMaintPotholeFactor +
                segment.MaintSurfacingExtentLastYear * constants.SdiMaintSurfacingFactor;
        }
        else
        {
            return baseSDI;   //Do not consider historical maintenance beyond the short term period for SDI calculation, as per the model's design.
        }
    }

    
    /// <summary>
    /// Calculates the Pavement Distress Index (PDI) for a road segment.
    /// </summary>    
    private static double GetPavementDistressIndexBase(RoadSegment segment, Constants constants)
    {
        // Rutting only counts above a dead band, and then disproportionately. Both numbers are the
        // engineer's and live in the 'distress_index' lookup set - see Constants for what each does.
        double rutExcess = Math.Max(0, segment.RutParameterValue - constants.PdiRutExcessThresholdMm);
        double rutPenalty = Math.Pow(rutExcess, constants.PdiRutPenaltyExponent);
        double value = segment.PctCracking + rutPenalty;

        return value;
    }



    /// <summary>
    /// Calculates the Surface Distress Index (SDI) for a road segment.
    /// </summary>  
    private static double GetSurfacingDistressIndexBase(RoadSegment segment)
    {
        double value = segment.PctFlushing + segment.PctRavelling + segment.PctCracking;
        return value;
    }



}
