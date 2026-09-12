
using JCass_ModelCore.Models;

namespace StarterModel.Objects;

public static class RoadSegmentFactory
{

    /// <summary>
    /// Creates a RoadSegment object from raw data provided in a string array. We assume columns are in the order defined in the model's raw data schema.
    /// </summary>
    /// <param name="model">Model object from which to refer the Raw Data schema</param>
    /// <param name="rawRow">Row of raw data values for each column in the schema</param>
    /// <returns></returns>
    public static RoadSegment GetFromRawData(ModelBase model, int elementIndex)
    {
        RoadSegment segment = new RoadSegment();

        segment.ElementIndex = elementIndex; // Set the element index for this segment
        

        // Identification
        segment.SegmentName = model.GetInputDataText(segment.ElementIndex, "file_seg_name");
        segment.SectionID = model.GetInputDataNumber(segment.ElementIndex, "file_section_id");
        segment.SectionName = model.GetInputDataText(segment.ElementIndex, "file_section_name");
        segment.LocFrom = model.GetInputDataNumber(segment.ElementIndex, "file_loc_from");
        segment.LocTo = model.GetInputDataNumber(segment.ElementIndex, "file_loc_to");
        segment.LaneCode = model.GetInputDataText(segment.ElementIndex, "file_lane_name");

        // Core measures
        segment.LengthInMetre = model.GetInputDataNumber(segment.ElementIndex, "file_length");
        segment.AreaSquareMetre = model.GetInputDataNumber(segment.ElementIndex, "file_area_m2");
        segment.WidthInMetre = segment.AreaSquareMetre / segment.LengthInMetre;

        // Flags        
        segment.CanTreatFlag = Convert.ToBoolean(model.GetInputDataText(segment.ElementIndex, "file_can_treat_flag"));
        segment.CanRehabFlag = Convert.ToBoolean(model.GetInputDataText(segment.ElementIndex, "file_can_rehab_flag"));
        segment.AsphaltOkFlag = Convert.ToBoolean(model.GetInputDataText(segment.ElementIndex, "file_ac_ok_flag"));
        segment.EarliestTreatmentPeriod = model.GetInputDataNumber(segment.ElementIndex, "file_earliest_treat_period");

        // Classification
        segment.UrbanRural = model.GetInputDataText(segment.ElementIndex, "file_urban_rural").ToLower();
        segment.ONRC = model.GetInputDataText(segment.ElementIndex, "file_onrc").ToLower();
        
        
        //Lookup Road Class based on ONRC value (do NOTnuse file_road_class as this contains client-variant values)
        segment.RoadClass = model.GetLookupValueText("road_class", segment.ONRC);

        // Traffic        
        segment.AverageDailyTraffic = model.GetInputDataNumber(segment.ElementIndex, "file_adt");
        segment.HeavyVehiclePercentage = model.GetInputDataNumber(segment.ElementIndex, "file_heavy_perc");
        segment.NumberOfBusRoutes = model.GetInputDataNumber(segment.ElementIndex, "file_no_of_bus_routes");
        segment.TrafficGrowthPercent = model.GetInputDataNumber(segment.ElementIndex, "file_traff_growth_perc");

        // Surfacing
        segment.SurfaceClass = model.GetInputDataText(segment.ElementIndex, "file_surf_class").ToLower();
        segment.NextSurface = model.GetInputDataText(segment.ElementIndex, "file_next_surf");        
        segment.SurfacingDateString = model.GetInputDataText(segment.ElementIndex, "file_surf_date");
        segment.SurfaceFunction = model.GetInputDataText(segment.ElementIndex, "file_surf_function");
        segment.SurfaceMaterial = model.GetInputDataText(segment.ElementIndex, "file_surf_material");
        segment.SurfaceExpectedLife = model.GetInputDataNumber(segment.ElementIndex, "file_surf_life_expected");
        segment.SurfaceNumberOfLayers = model.GetInputDataNumber(segment.ElementIndex, "file_surf_layer_no");
        segment.SurfaceThickness = model.GetInputDataNumber(segment.ElementIndex, "file_surf_thick");

        // Pavement        
        segment.PavementDateString = model.GetInputDataText(segment.ElementIndex, "file_pave_date");
        segment.PavementRemainingLife = model.GetInputDataNumber(segment.ElementIndex, "file_pave_remlife");
        segment.FaultsAndMaintenanceSurfacingM2 = model.GetInputDataNumber(segment.ElementIndex, "file_su_fault_qty");
        segment.FaultsAndMaintenancePavementM2 = model.GetInputDataNumber(segment.ElementIndex, "file_pa_fault_qty");

        // Roughness and rutting
        segment.RoughnessSurveyDateString = model.GetInputDataText(segment.ElementIndex, "file_rough_survey_date");
        segment.Naasra85 = model.GetInputDataNumber(segment.ElementIndex, "file_naasra_85");
        segment.RutSurveyDateString = model.GetInputDataText(segment.ElementIndex, "file_rut_survey_date");
        segment.RutLwpMean85 = model.GetInputDataNumber(segment.ElementIndex, "file_rut_lwpmean_85");
        segment.RutRwpMean85 = model.GetInputDataNumber(segment.ElementIndex, "file_rut_rwpmean_85");

        // Condition survey
        segment.ConditionSurveyDateString = model.GetInputDataText(segment.ElementIndex, "file_cond_survey_date");

        // Condition percentages
        segment.PctMeshCracks = model.GetInputDataNumber(segment.ElementIndex, "file_pct_allig");
        segment.PctLongTransCracks = model.GetInputDataNumber(segment.ElementIndex, "file_pct_lt_crax");
        segment.PctPotholes = model.GetInputDataNumber(segment.ElementIndex, "file_pct_poth");
        segment.PctScabbing = model.GetInputDataNumber(segment.ElementIndex, "file_pct_scabb");
        segment.PctFlushing = model.GetInputDataNumber(segment.ElementIndex, "file_pct_flush");
        segment.PctShoving = model.GetInputDataNumber(segment.ElementIndex, "file_pct_shove");
        segment.PctEdgeBreaks = model.GetInputDataNumber(segment.ElementIndex, "file_pct_edgebreak");

        return segment;
    }

    /// <summary>
    /// Gets a segment object from a model's input and parameter values dictionary. Use this method AFTER intialisation, when the model
    /// has already calculated initial values for model parameters and holds these values (initial or iterated/resetted). The inputAndParameterValues
    /// dictionary holds keys mapping to both the raw input columns and to the model parameters, with the Values mapping to the corresponding values.
    /// </summary>
    /// <param name="frameworkModel">Model object from which to refer the Raw Data schema</param>
    /// <param name="numParamValues">Dictionary provided by model containing last/current values for numeric model parameters./param>
    /// <param name="textParamValues">Dictionary provided by model containing last/current values for numeric model parameters./param>
    /// <returns></returns>
    public static RoadSegment GetFromModel(ModelBase frameworkModel, Dictionary<string, double> numInputValues, Dictionary<string, string> textInputValues, 
        Dictionary<string, double> numParamValues, Dictionary<string, string> textParamValues, int elementIndex, int iPeriod)
    {
        RoadSegment segment = new RoadSegment();

        //First set all properties that are still dependend on the raw input data and that do not change over
        // the modelling periods

        segment.ElementIndex = elementIndex; // Set the element index for this segment

        // Identification
        segment.SegmentName = textInputValues["file_seg_name"];
        segment.SectionID = Convert.ToInt32(numInputValues["file_section_id"]);
        segment.SectionName = textInputValues["file_section_name"];
        segment.LocFrom = Convert.ToInt32(numInputValues["file_loc_from"]);
        segment.LocTo = Convert.ToInt32(numInputValues["file_loc_to"]);
        segment.LaneCode = textInputValues["file_lane_name"];

        // Core measures
        segment.LengthInMetre = numInputValues["file_length"];
        segment.AreaSquareMetre = numInputValues["file_area_m2"];
        segment.WidthInMetre = segment.AreaSquareMetre / segment.LengthInMetre;

        // Flags        
        segment.CanTreatFlag = Convert.ToBoolean(textInputValues["file_can_treat_flag"]);
        segment.CanRehabFlag = Convert.ToBoolean(textInputValues["file_can_rehab_flag"]);        
        segment.AsphaltOkFlag = Convert.ToBoolean(textInputValues["file_ac_ok_flag"]);
        segment.EarliestTreatmentPeriod = Convert.ToInt32(numInputValues["file_earliest_treat_period"]);

        // TODO: To discuss and make hardcoded value of 7 a lookup parameter
        if (iPeriod > 7) segment.CanRehabFlag = true; // For congruence with JFunction model, we allow rehab after 7 periods

        segment.AsphaltOkFlag = Convert.ToBoolean(textInputValues["file_ac_ok_flag"]);

        // Classification
        segment.UrbanRural = textInputValues["file_urban_rural"].ToLower();
        segment.ONRC = textInputValues["file_onrc"].ToLower();
                
        //Lookup Road Class based on ONRC value (do NOT use file_road_class as this contains client-variant values)
        segment.RoadClass = frameworkModel.GetLookupValueText("road_class", segment.ONRC);

        // Traffic                
        segment.HeavyVehiclePercentage = numInputValues["file_heavy_perc"];
        segment.NumberOfBusRoutes = numInputValues["file_no_of_bus_routes"];
        segment.TrafficGrowthPercent = numInputValues["file_traff_growth_perc"];

        // Surfacing
        segment.SurfaceClass = textInputValues["file_surf_class"].ToLower();
        segment.NextSurface = textInputValues["file_next_surf"];
        segment.SurfacingDateString = textInputValues["file_surf_date"];
        

        // Pavement        
        segment.PavementDateString = textInputValues["file_pave_date"];
        segment.PavementRemainingLife = numInputValues["file_pave_remlife"];
        segment.FaultsAndMaintenanceSurfacingM2 = numInputValues["file_su_fault_qty"];
        segment.FaultsAndMaintenancePavementM2 = numInputValues["file_pa_fault_qty"];

        // Roughness and rutting
        segment.RoughnessSurveyDateString = textInputValues["file_rough_survey_date"];        
        segment.RutSurveyDateString = textInputValues["file_rut_survey_date"];
        segment.RutLwpMean85 = numInputValues["file_rut_lwpmean_85"];  // Original raw rutting value
        segment.RutRwpMean85 = numInputValues["file_rut_rwpmean_85"];  // Original raw rutting value

        // Condition survey
        segment.ConditionSurveyDateString = textInputValues["file_cond_survey_date"];

        // Now set the properties that depend on model parameters: Work in order of model parameter definition set
        // in the setup file so that we can more easily spot missing parameters.

        segment.AverageDailyTraffic = numParamValues["para_adt"];
        // HCV is automatically updated based on ADT and HeavyVehiclePercentage
        segment.PavementAge = numParamValues["para_pave_age"];
        segment.PavementRemainingLife = numParamValues["para_pave_remlife"];
        // Note: segment.PavementAchievedLife will be automatically calculated by the model based on the PavementAge and PavementExpectedLife
        // Note segment.HCVRisk will be automatically calculated by the model based on the PavementUse and HeavyVehiclePercentage

        segment.SurfaceMaterial = textParamValues["para_surf_mat"];
        segment.SurfaceClass = textParamValues["para_surf_class"].ToLower();
        // Automatically updated:
        // segment.SurfaceIsChipSealFlag 
        // segment.SurfaceIsChipSealOrACFlag 
        // segment.SurfaceRoadType
        segment.SurfaceThickness = numParamValues["para_surf_thick"];
        segment.SurfaceNumberOfLayers = numParamValues["para_surf_layers"];
        segment.SurfaceFunction = textParamValues["para_surf_func"];
        segment.SurfaceExpectedLife = numParamValues["para_surf_exp_life"];
        segment.SurfaceAge = numParamValues["para_surf_age"];         
        // Automatically updated:
        // segment.SurfaceAchievedLifePercent
        // segment.SurfaceRemainingLife 

        // Visual Distresses
        segment.PctFlushing = numParamValues["para_flush_pct"];
        segment.FlushingModelInfo = textParamValues["para_flush_info"];

        segment.PctEdgeBreaks = numParamValues["para_edgeb_pct"];
        segment.EdgeBreakModelInfo = textParamValues["para_edgeb_info"];

        segment.PctScabbing = numParamValues["para_scabb_pct"];
        segment.ScabbingModelInfo = textParamValues["para_scabb_info"];

        segment.PctLongTransCracks = numParamValues["para_lt_cracks_pct"];
        segment.LTCracksModelInfo = textParamValues["para_lt_cracks_info"];

        segment.PctMeshCracks = numParamValues["para_mesh_cracks_pct"];
        segment.MeshCrackModelInfo = textParamValues["para_mesh_cracks_info"];

        segment.PctShoving = numParamValues["para_shove_pct"];
        segment.ShovingModelInfo =  textParamValues["para_shove_info"];

        segment.PctPotholes = numParamValues["para_poth_pct"];
        segment.PotholeModelInfo = textParamValues["para_poth_info"];

        //Rutting and Naasra
        segment.RutIncrement = numParamValues["para_rut_increm"];  // Updated rut
        segment.RutParameterValue = numParamValues["para_rut"];

        segment.NaasraIncrement = numParamValues["para_naasra_increm"];  // Updated Naasra increment
        segment.Naasra85 = numParamValues["para_naasra"];  // Updated Naasra value

        // Calculate SDI, PDI, SLA, Objective Function sub-values etc.        
        segment.UpdateFormulaValuesFromParameters(numParamValues, textParamValues);

        segment.TreatmentCount = Convert.ToInt32(numParamValues["para_treat_count"]); // Will update IsTreated flag
        // para_is_treated_flag = automatically calculated based on treatment count

        segment.PavementDistressIndexRank = numParamValues["para_pdi_rank"];
        segment.RutRank = Convert.ToInt32(numParamValues["para_rut_rank"]);
        segment.SurfaceDistressIndexRank = numParamValues["para_sdi_rank"];
        segment.SurfaceLifeAchievedRank = numParamValues["para_sla_rank"];

        // Ensure that the method to re-calculate index values are called on return

        return segment;
    }

}

