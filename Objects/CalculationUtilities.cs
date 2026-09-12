using JCass_ModelCore.Models;

namespace StarterModel.Objects;

/// <summary>
/// Static class to help with calculation of Indexes (PDI, SDI, etc.) and Objective Functions.
/// </summary>
public static class CalculationUtilities
{

    /// <summary>
    /// Calculates a reset value based on the exceedance concept. If the value before treatment is below or equal to the exceedance threshold, 
    /// it returns the value as is. If the value before treatment is greater than the exceedance threshold, it calculates a reset value by reducing 
    /// the value by [the difference between the value before treatment and the exceedance threshold], multiplied with the improvement fraction.
    /// </summary>
    /// <param name="valueBeforeTreatment">Value before treatment is effected</param>
    /// <param name="exceedanceThreshold">Exceedance threshold</param>
    /// <param name="improvementFraction">Improvement fraction (value like 0.5, 0.8 etc)</param>
    /// <returns>Value after treatment</returns>
    public static double GetResetBasedOnExceedanceConcept(double valueBeforeTreatment, double exceedanceThreshold, double improvementFraction)
    {
        if (valueBeforeTreatment <= exceedanceThreshold)
        {
            return valueBeforeTreatment;
        }
        else
        {
            double resetValue = valueBeforeTreatment - (valueBeforeTreatment - exceedanceThreshold) * improvementFraction;
            return resetValue;
        }
    }


    /// <summary>
    /// Utility function to calculate the Logit function on a value, where the function is
    /// defined as 'Math.Exp(value) / (1 + Math.Exp(value))'
    /// </summary>
    /// <param name="value">Value on which to calculate logic</param>
    /// <returns></returns>
    public static double Logit(double value)
    {
        return Math.Exp(value) / (1 + Math.Exp(value));
    }

    /// <summary>
    /// Calculates the Pavement Distress Index (PDI) for a road segment based on the current period. Short term includes maintenance and faults
    /// </summary>    
    /// <param name="currentPeriod">Current modelling period (e.g. 1,2,3...) used to determine whether we are in short or long term</param>
    /// <returns></returns>
    public static double GetPavementDistressIndex(RoadSegment segment, ModelBase frameworkModel, StarterModel roadModel, int currentPeriod)
    {
        double boostedPotholes = segment.PctPotholes * roadModel.Constants.PotholeBoostFactor;
        bool isShortTerm = currentPeriod <= roadModel.Constants.CSShortTermPeriod;
        if (isShortTerm)
        {
            return GetPavementDistressIndexShortTerm(segment, frameworkModel, boostedPotholes);
        }
        else
        {
            return GetPavementDistressIndexLongTerm(segment, frameworkModel, boostedPotholes);
        }
    }

    /// <summary>
    /// Calculates the Surface Distress Index (SDI) for a road segment based on the current period. Short term includes maintenance and faults
    /// </summary>    
    /// <param name="currentPeriod">Current modelling period (e.g. 1,2,3,...) used to determine whether we are in short or long term</param>
    /// <returns></returns>
    public static double GetSurfacingDistressIndex(RoadSegment segment, ModelBase frameworkModel, StarterModel roadModel, int currentPeriod)
    {
        double boostedPotholes = segment.PctPotholes * roadModel.Constants.PotholeBoostFactor;
        bool isShortTerm = currentPeriod <= roadModel.Constants.CSShortTermPeriod;
        if (isShortTerm)
        {
            return GetSurfacingDistressIndexShortTerm(segment, frameworkModel, boostedPotholes);
        }
        else
        {
            return GetSurfacingDistressIndexLongTerm(segment, frameworkModel, boostedPotholes);
        }
    }

    /// <summary>
    /// Calcuates the Pavement Distress Index (PDI) for a road segment based. Short term includes maintenance and faults 
    /// percentage.
    /// </summary>
    /// <param name="segment"></param>
    /// <param name="frameworkModel"></param>    
    private static double GetPavementDistressIndexShortTerm(RoadSegment segment, ModelBase frameworkModel, double boostedPotholes)
    {     
        double value = 0.2 * segment.PctLongTransCracks 
            + segment.PctMeshCracks 
            + segment.PctShoving 
            + boostedPotholes 
            + segment.FaultsAndMaintenancePavementPercent;

        return value;            
    }

    /// <summary>
    /// Calculates the Pavement Distress Index (PDI) for a road segment based on long-term distress measures. Long term 
    /// currently also includes maintenance and faults to prevent a sudden drop in PDI after the short term period is over.
    /// TODO: To discuss the inclusion of maintenance and faults in the long term PDI.
    /// </summary>    
    private static double GetPavementDistressIndexLongTerm(RoadSegment segment, ModelBase frameworkModel, double boostedPotholes)
    {        
        double value = 0.2 * segment.PctLongTransCracks
            + segment.PctMeshCracks
            + segment.PctShoving
            + boostedPotholes
            + segment.FaultsAndMaintenancePavementPercent;

        return value;
    }

    /// <summary>
    /// Calculates the Surface Distress Index (SDI) for a road segment based on short-term distress measures. Short term includes maintenance and faults 
    /// percentage.
    /// </summary>    
    private static double GetSurfacingDistressIndexShortTerm(RoadSegment segment, ModelBase frameworkModel, double boostedPotholes)
    {        
        
        double value = segment.PctFlushing + segment.PctScabbing + 0.5*segment.PctLongTransCracks + boostedPotholes + segment.FaultsAndMaintenanceSurfacingPercent;        
        return value;
    }

    /// <summary>
    /// Calculates the Surface Distress Index (PDI) for a road segment based on long-term distress measures. Long term 
    /// currently also includes maintenance and faults to prevent a sudden drop in PDI after the short term period is over.
    /// TODO: To discuss the inclusion of maintenance and faults in the long term PDI.
    /// </summary>  
    private static double GetSurfacingDistressIndexLongTerm(RoadSegment segment, ModelBase frameworkModel, double boostedPotholes)
    {
        double value = segment.PctFlushing + segment.PctScabbing + 0.5 * segment.PctLongTransCracks + boostedPotholes + segment.FaultsAndMaintenanceSurfacingPercent;
        return value;
    }



}
