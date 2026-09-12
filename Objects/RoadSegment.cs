using JCass_Core.JFunctions;
using JCass_ModelCore.Models;

namespace StarterModel.Objects;


/// <summary>
/// Object representing a road segment with various properties and attributes.
/// </summary>
public class RoadSegment
{

    private double _surfaceAge;
    private double _surfaceAgeBeforeReset;
    private string _surfaceFunction = "unknown"; // Default value for previous surface function
    private string _previousSurfaceFunction = "unknown"; // Default value for previous surface function

    #region Identification

    /// <summary>
    /// Zero-based index of the element in the model. This is set by the Framework Model and is used to identify the element in the model.
    /// </summary>
    public int ElementIndex { get; set; }

    /// <summary>
    /// Short code for identifying the segment in debug/feeback messages
    /// </summary>
    public string FeebackCode
    {
        get
        {
            return $"elem_index: {this.ElementIndex:D4} - {this.SegmentName}";
        }
    }

    /// <summary>
    /// Segment identifier. Maps to input column "file_seg_name".
    /// </summary>
    public string SegmentName { get; set; } = string.Empty;

    /// <summary>
    /// Section ID. Maps to "file_section_id".
    /// </summary>
    public double SectionID { get; set; }

    /// <summary>
    /// Name of the section. Maps to "file_section_name".
    /// </summary>
    public string SectionName { get; set; } = string.Empty;

    /// <summary>
    /// Start metre of the segment. Maps to "file_loc_from".
    /// </summary>
    public double LocFrom { get; set; }

    /// <summary>
    /// End metre of the segment. Maps to "file_loc_to".
    /// </summary>
    public double LocTo { get; set; }

    /// <summary>
    /// Lane code. Maps to "file_lane_name".
    /// </summary>
    public string LaneCode { get; set; } = string.Empty;

    #endregion

    #region Quantity 

    /// <summary>
    /// Length of the segment in metres.
    /// </summary>
    public double LengthInMetre { get; set; }

    /// <summary>
    /// Square metre area.
    /// </summary>
    public double AreaSquareMetre { get; set; }

    /// <summary>
    /// Width in metres. By default, this is calculated on initialisation from Area and Length
    /// </summary>
    public double WidthInMetre { get; set; }

    #endregion

    #region Situational and Treatment Flags

    
    /// <summary>
    /// Can this segment be considered for treatment (client specific based on policy).
    /// </summary>
    public bool CanTreatFlag { get; set; }

    /// <summary>
    /// Can this segment be considered for Rehab (client specific).
    /// </summary>
    public bool CanRehabFlag { get; set; }

    /// <summary>
    /// Is the pavement suitable for asphalt resurfacing.
    /// </summary>
    public bool AsphaltOkFlag { get; set; }

    /// <summary>
    /// Earliest modelling period the first treatment may be triggered.
    /// </summary>
    public double EarliestTreatmentPeriod { get; set; }

    #endregion
    
    #region Surface and Pavement Properties

    private string _surfaceClass = string.Empty;

    /// <summary>
    /// Surface class ('cs', 'ac', 'blocks', 'concrete', 'other').
    /// </summary>
    public string SurfaceClass
    {
        get => _surfaceClass;
        set => _surfaceClass = value?.ToLower() ?? string.Empty;
    }

    /// <summary>
    /// Code that determines exceedance thresholds and improvement factors based on Urban/Rural, Road Class, and Surf Class. This code
    /// is a concatenation of SurfaceClass and RoadType using an underscore to delimit.
    /// </summary>    
    public string SurfaceRoadType
    {
        get
        {
            return this.SurfaceClass + "_" + this.RoadType;
        }
    }

    /// <summary>
    /// Flag indicating if the surface is a chip seal. This is calculated based on the SurfaceClass property.
    /// </summary>
    public int SurfaceIsChipSealFlag
    {
        get
        {
            // Return 1 if the surface class is 'cs' (chip seal), otherwise return 0.
            return this.SurfaceClass == "cs" ? 1 : 0;
        }
    }

    /// <summary>
    /// Flag indicating if the surface is either chip seal or asphalt concrete. This is calculated based on the SurfaceClass property.
    /// </summary>
    public int SurfaceIsChipSealOrACFlag
    {
        get
        {
            // Return 1 if the surface class is 'cs' (chip seal) or 'ac' (asphalt concrete), otherwise return 0.
            return this.SurfaceClass == "cs" || this.SurfaceClass == "ac" ? 1 : 0;
        }
    }

    /// <summary>
    /// Replacement surfacing type. Could be 'ac', 'cs', 'blocks', 'concrete' etc.
    /// </summary>
    public string NextSurface { get; set; } = string.Empty;

    /// <summary>
    /// Surfacing date as a text/string value in dd/mm/yyyy format.
    /// </summary>
    public string SurfacingDateString { get; set; } = string.Empty;

    /// <summary>
    /// Surfacing date in fractional years, calculated from the SurfacingDateString during Initialisation.
    /// </summary>
    public double SurfaceAge 
    { get {
            return _surfaceAge;
        }
      set {
            _surfaceAgeBeforeReset = this.SurfaceAge;
            _surfaceAge = value;
        } 
    }
    
    /// <summary>
    /// Intermediate variable holding the Surface Age before a reset was applied.
    /// </summary>
    public double SurfaceAgeBeforeReset { get { return _surfaceAgeBeforeReset; } }

    /// <summary>
    /// Surface function.
    /// </summary>
    public string SurfaceFunction
    {
        get { return _surfaceFunction;  }
        set
        {
            _previousSurfaceFunction = _surfaceFunction;
            _surfaceFunction = value;
        }
    }

    /// <summary>
    /// Preceding Surface function - use this to check what the situation was before the last reset.
    /// </summary>
    public string SurfaceFunctionPrevious
    {
        get { return _previousSurfaceFunction; }        
    }

    /// <summary>
    /// Surfacing material.
    /// </summary>
    public string SurfaceMaterial { get; set; } = string.Empty;

    /// <summary>
    /// Surfacing expected life (years) from RAMM.
    /// </summary>
    public double SurfaceExpectedLife { get; set; }

    /// <summary>
    /// Returns the Surface Expective life minus the Surface Age, which gives the remaining life of the surface in years.
    /// </summary>
    public double SurfaceRemainingLife
    {
        get
        {            
            return this.SurfaceExpectedLife - this.SurfaceAge;
        }
    }

    /// <summary>
    /// Checks if a second coat is needed. Will only return true if the following conditions are met:
    /// <para>1. Surface is a Chipseal</para>
    /// <para>2. Surface function is currently '1'</para>
    /// <para>2. Next surface flag is also a Chipseal</para>
    /// <para>2. Surface remaining life is less than or equal to 1</para>
    /// </summary>
    public bool SecondCoatNeeded
    {
        get
        {
            if (this.SurfaceClass == "cs" && this.SurfaceFunction == "1" && this.NextSurfaceIsChipSeal == true && this.SurfaceRemainingLife <= 1) 
            { 
                return true; 
            } 
            return false;
        }
    }

    /// <summary>
    /// Flag to indicate if the next surface is a chip seal. This is determined by checking the NextSurface property.
    /// </summary>
    public bool NextSurfaceIsChipSeal
    {
        get
        {
            // Return true if the next surface is chip seal, otherwise false.
            return this.NextSurface == "cs";
        }
    }

    /// <summary>
    /// Returns the percentage of the Surface Expected Life that has been achieved based on the Surface Age.
    /// </summary>
    public double SurfaceAchievedLifePercent
    {
        get
        {
            if (this.SurfaceExpectedLife <= 0.0)
            {
                throw new Exception($"Surface expected life is zero or negative for segment {this.FeebackCode}. Surface Age: {this.SurfaceAge}, Expected Life: {this.SurfaceExpectedLife}.");
            }
            // As per JFunctions, limit the value to 200 to prevent very high values from distorting MCDA
            // TODO: Re-think this
            return Math.Min(200, 100 * (this.SurfaceAge / this.SurfaceExpectedLife));
        }
    }

    /// <summary>
    /// Surfacing number of layers
    /// </summary>
    public double SurfaceNumberOfLayers { get; set; }

    /// <summary>
    /// Surfacing thickness in millimetres.
    /// </summary>
    public double SurfaceThickness { get; set; }

    
    /// <summary>
    /// Pavement construction date as a text/string value in dd/mm/yyyy format.
    /// </summary>
    public string PavementDateString { get; set; } = string.Empty;

    /// <summary>
    /// Pavement Age in fractional years, calculated from the PavementDateString during Initialisation.
    /// </summary>
    public double PavementAge { get; set; }

    /// <summary>
    /// Age-based pavement remaining life.
    /// </summary>
    public double PavementRemainingLife { get; set; }

    /// <summary>
    /// Returns the percentage of the Expected Pavement Life based on the Pavement Age and Remaining Life.
    /// </summary>
    public double PavementAchievedLife
    {
        get
        {
            double expectedLife = this.PavementAge + this.PavementRemainingLife;
            if (expectedLife <= 0.0)
            {
                throw new Exception($"Pavement expected life is zero or negative for segment {this.FeebackCode}. Pavement Age: {this.PavementAge}, Remaining Life: {this.PavementRemainingLife}.");
            }
            return this.PavementAge / expectedLife * 100.0;
        }
    }

    /// <summary>
    ///  Pavement Risk factor based on traffic loading and pavement life achieved
    /// </summary>
    public double HCVRisk
    {
        get
        {
            // Pavement Risk factor traffic loading component
            double hcv_risk_a = Math.Pow(this.HeavyVehiclesPerDay, 0.1);

            // Pavement Risk factor pavement life achieved component
            double hcv_risk_b = Math.Pow(this.PavementAchievedLife, 0.5);

            // Combine the two components by multiplying them
            double hcv_risk = hcv_risk_a * hcv_risk_b;

            return hcv_risk;
        }
    }


    #endregion

    #region ONRC and Carriageway Attributes

    private string _urbanRural = string.Empty;
    private string _onrc = string.Empty;
    private string _roadClass = string.Empty;

    /// <summary>
    /// Urban/Rural flag.
    /// </summary>
    public string UrbanRural
    {
        get => _urbanRural;
        set => _urbanRural = value?.ToLower() ?? string.Empty;
    }

    /// <summary>
    /// ONRC Category.
    /// </summary>
    public string ONRC
    {
        get => _onrc;
        set => _onrc = value?.ToLower() ?? string.Empty;
    }

    /// <summary>
    /// Road class based on ONRC value mapped to a Road Class in lookup set 'road_class'. Note: this does NOT
    /// map to the (now deprecated) input column "file_road_class" as that column contains client-variant values.
    /// </summary>
    public string RoadClass
    {
        get => _roadClass;
        set => _roadClass = value?.ToLower() ?? string.Empty;
    }

    
    /// <summary>
    /// Combined road type based on Urban/Rural and Road Class. This is simply a concatenation of the two
    /// values, so it will return e.g. 'RL' for Rural, Low Volume, or 'UL' for Urban, Low Volume.
    /// </summary>
    public string RoadType
    {
        get
        {
            return this.UrbanRural + this.RoadClass;
        }
    }

    #endregion

    #region Traffic and Growth

    /// <summary>
    /// Average daily traffic.
    /// </summary>
    public double AverageDailyTraffic { get; set; }

    /// <summary>
    /// Heavy vehicle percentage.
    /// </summary>
    public double HeavyVehiclePercentage { get; set; }

    /// <summary>
    /// Number of bus routes (not in use).
    /// </summary>
    public double NumberOfBusRoutes { get; set; }

    /// <summary>
    /// Traffic growth percentage.
    /// </summary>
    public double TrafficGrowthPercent { get; set; }

    /// <summary>
    /// Heavy vehicles per day, calculated as a percentage of Average Daily Traffic using HeavyVehiclePercentage.
    /// </summary>
    public double HeavyVehiclesPerDay
    {
        get
        {
            return this.AverageDailyTraffic * (this.HeavyVehiclePercentage / 100.0);
        }
    }

    #endregion

    #region Faults and Maintenance

    private double _faultsAndMaintenanceSurfacingM2;
    private double _faultsAndMaintenancePavementM2;
    private double _faultsAndMaintenanceSurfacingPercent;
    private double _faultsAndMaintenancePavementPercent;

    /// <summary>
    /// Surfacing faults area in square metres. Depending on how inputs are prepared, this field may include
    /// any recent historical maintenance. Updating this value will also update the FaultsAndMaintenanceSurfacingPercent 
    /// property based on the AreaSquareMetre.
    /// </summary>
    public double FaultsAndMaintenanceSurfacingM2
    {
        get
        {
            return _faultsAndMaintenanceSurfacingM2;
        }
        set
        {
            _faultsAndMaintenanceSurfacingM2 = value;
            // Calculate the percentage of faults and maintenance based on the area.
            if (this.AreaSquareMetre > 0)
            {
                _faultsAndMaintenanceSurfacingPercent = (value / this.AreaSquareMetre) * 100.0;
            }
            else
            {
                _faultsAndMaintenanceSurfacingPercent = 0.0;
            }
        }
    }

    /// <summary>
    /// Percentage of Surfacing related Faults and Maintenance - calculated on the basis of FaultsAndMaintenanceSurfacingM2 and AreaSquareMetre.
    /// </summary>
    public double FaultsAndMaintenanceSurfacingPercent { get { return _faultsAndMaintenanceSurfacingPercent; } }

    /// <summary>
    /// Pavement faults area in square metres. Depending on how inputs are prepared, this field may include
    /// any recent historical maintenance. Updating this value will also update the FaultsAndMaintenancePavementPercent 
    /// property based on the AreaSquareMetre.
    /// </summary>
    public double FaultsAndMaintenancePavementM2
    {
        get
        {
            return _faultsAndMaintenancePavementM2;
        }
        set
        {
            _faultsAndMaintenancePavementM2 = value;
            // Calculate the percentage of faults and maintenance based on the area.
            if (this.AreaSquareMetre > 0)
            {
                _faultsAndMaintenancePavementPercent = (value / this.AreaSquareMetre) * 100.0;
            }
            else
            {
                _faultsAndMaintenancePavementPercent = 0.0;
            }
        }
    }


    /// <summary>
    /// Percentage of Pavement related Faults and Maintenance - calculated on the basis of FaultsAndMaintenancePavementM2 and AreaSquareMetre.
    /// </summary>
    public double FaultsAndMaintenancePavementPercent
    {
        get
        {
            return _faultsAndMaintenancePavementPercent;
        }
    }

    /// <summary>
    /// Helper to reset the underlying faults and maintenance values to zero. Use this on itiation if the Surface or Pavement age
    /// indicates a recent resurfacing or rehabilitation.
    /// </summary>
    public void ResetFaultsAndMaintenance()
    {
        _faultsAndMaintenanceSurfacingM2 = 0.0;
        _faultsAndMaintenanceSurfacingPercent = 0.0;
        _faultsAndMaintenancePavementM2 = 0.0;
        _faultsAndMaintenancePavementPercent = 0.0;
    }


    #endregion

    #region High Speed Data (HSD) (Rut, Roughness, Texture etc.)

    /// <summary>
    /// HSD survey date as a string in dd/mm/yyyy format. Do not use this
    /// after initialitation - use the RutParameterValue property instead.
    /// </summary>
    public string RutSurveyDateString { get; set; } = string.Empty;

    /// <summary>
    /// Roughness segment survey date as string in dd/mm/yyyy format.
    /// </summary>
    public string RoughnessSurveyDateString { get; set; } = string.Empty;

    /// <summary>
    /// NAASRA 85th percentile roughness.
    /// </summary>
    public double Naasra85 { get; set; }

    /// <summary>
    /// Increment for Naasra in counts per year. This is calculated during initialisation based on the Roughness survey date and the Naasra85 value. After
    /// the first treatment, the Roughness Increment model is used to estimate the increment each year.
    /// </summary>
    public double NaasraIncrement { get; set; }

    /// <summary>
    /// LWP mean rut 85th percentile from raw input values. Do not use this 
    /// after initialisation - use the RutParameterValue property instead.
    /// </summary>
    public double RutLwpMean85 { get; set; }

    /// <summary>
    /// RWP mean rut 85th percentile from raw input values.
    /// </summary>
    public double RutRwpMean85 { get; set; }

    /// <summary>
    /// Rut parameter value calculated during initialisation and used to represent the rutting condition of the road segment.     
    /// </summary>
    public double RutParameterValue { get; set; }

    /// <summary>
    /// Rut increment in mm/year. During initialisation, this is calculated based on the RutParameterValue and the HSD survey date. After 
    /// the first treatment, the rut prediction model is used to estimate the increment.
    /// </summary>
    public double RutIncrement { get; set; }

    /// <summary>
    /// Calculates the probability of high rutting based on various parameters such as surface type, urban/rural classification, HCV risk, and distress percentages.
    /// </summary>    
    public double GetHighRutProbability()
    {
        // logit(-1.6 + 1.1 * para_surf_cs_flag + -0.4 * pcal_is_urban_flag + 0.02 * para_hcv_risk + 0.06 * para_shove_pct + 0.02 * para_mesh_cracks_pct + 0.04 * para_scabb_pct + 0.01 * para_flush_pct)
        double value = -1.6 + 1.1 * this.SurfaceIsChipSealFlag +
                            -0.4 * (this.UrbanRural == "u" ? 1 : 0) +
                            0.02 * this.HCVRisk +
                            0.06 * this.PctShoving +
                            0.02 * this.PctMeshCracks +
                            0.04 * this.PctScabbing +
                            0.01 * this.PctFlushing;

        return CalculationUtilities.Logit(value);
    }

    /// <summary>
    /// Calculates the increment in rutting using an inverse distribution based on the high rut probability. This is done using a JFuncInverseDistribution 
    /// function. Note that this function should only be called after a first treatment has been applied to the segment. Before any treatment is applied, the
    /// historical rut rate is used. TODO: Modify this so that the historical rate will only be used for a certain number of years.
    /// </summary>
    /// <returns>Estimated Rut Increment in mm/year</returns>
    public double GetRutIncrementAfterTreatment()
    {
        // TODO: this setup code contains (a) distribution type; (b) central tendency. Make these lookup values instead
        // of hardcoding them here.
        string setupCode = "a : 0.1 : incr_rutting_proba";
        JFuncInverseDistribution incrementDistribution = new JFuncInverseDistribution(setupCode);
        Dictionary<string, object> keyValuePairs = new Dictionary<string, object>
        {
            { "incr_rutting_proba", this.GetHighRutProbability() }
        };
        double increment = Convert.ToDouble(incrementDistribution.Evaluate(keyValuePairs));
        return increment;
    }

    /// <summary>
    /// Calculates the probability of high Naasra (rapid deterioration) based on various parameters such as 
    /// surface type, urban/rural classification, HCV risk, and distress percentages.
    /// </summary>    
    public double GetHighNaasraProbability()
    {
        //logit(-2.8 + 0.6 * para_surf_cs_flag + 0.5 * pcal_is_urban_flag + 0.03 * para_hcv_risk + 0.02 * para_shove_pct + 0.01 * para_mesh_cracks_pct + 0.03 * para_scabb_pct + 1.67 * para_poth_pct + 0.09 * para_rut)
        double value = -2.8 + 0.6 * this.SurfaceIsChipSealFlag +
                            0.5 * (this.UrbanRural == "u" ? 1 : 0) +
                            0.03 * this.HCVRisk +
                            0.02 * this.PctShoving +
                            0.01 * this.PctMeshCracks +
                            0.03 * this.PctScabbing +
                            1.67 * this.PctPotholes +
                            0.09 * this.RutParameterValue;
        return CalculationUtilities.Logit(value);
    }

    /// <summary>
    /// Calculates the increment in Naara  using an inverse distribution based on the high Naasra probability. This is done using a JFuncInverseDistribution 
    /// function. Note that this function should only be called after a first treatment has been applied to the segment. Before any treatment is applied, the
    /// historical rate is used. TODO: Modify this so that the historical rate will only be used for a certain number of years.
    /// </summary>
    /// <returns>Estimated Rut Increment in mm/year</returns>
    public double GetNaasraIncrementAfterTreatment()
    {
        // TODO: this setup code contains (a) distribution type; (b) central tendency. Make these lookup values instead
        // of hardcoding them here.
        string setupCode = "a : 0.9 : incr_naasra_proba";
        JFuncInverseDistribution incrementDistribution = new JFuncInverseDistribution(setupCode);
        Dictionary<string, object> keyValuePairs = new Dictionary<string, object>
        {
            { "incr_naasra_proba", this.GetHighNaasraProbability() }
        };
        double increment = Convert.ToDouble(incrementDistribution.Evaluate(keyValuePairs));
        return increment;
    }

    #endregion

    #region Visual Condition Distresses

    /// <summary>
    /// Condition survey date as string in dd/mm/yyyy format.
    /// </summary>
    public string ConditionSurveyDateString { get; set; } = string.Empty;

    /// <summary>
    /// Percentage of flushing.
    /// </summary>
    public double PctFlushing { get; set; }

    /// <summary>
    /// Percentage of edge breaks.
    /// </summary>
    public double PctEdgeBreaks { get; set; }

    /// <summary>
    /// Percentage of scabbing.
    /// </summary>
    public double PctScabbing { get; set; }

    /// <summary>
    /// Percentage of longitudinal and transverse cracks.
    /// </summary>
    public double PctLongTransCracks { get; set; }

    /// <summary>
    /// Percentage of alligator or mesh cracks.
    /// </summary>
    public double PctMeshCracks { get; set; }

    /// <summary>
    /// Percentage of shoving.
    /// </summary>
    public double PctShoving { get; set; }

    /// <summary>
    /// Percentage of potholes.
    /// </summary>

    public double PctPotholes { get; set; }

    /// <summary>
    /// Coded information on the current values for the S-curve model for this distress. Values are stored as:
    /// [AADI_InitialValue_T100] 
    /// where: AADI is the Age at Distress Initiation, InitialValue is the percent distress observed right
    /// after initiation, and T100 is the time it takes for the distress to reach 100% of the segment area.
    /// </summary>
    public string FlushingModelInfo { get; set; } = string.Empty;

    /// <summary>
    /// Coded information on the current values for the S-curve model for this distress. Values are stored as:
    /// [AADI_InitialValue_T100] 
    /// where: AADI is the Age at Distress Initiation, InitialValue is the percent distress observed right
    /// after initiation, and T100 is the time it takes for the distress to reach 100% of the segment area.
    /// </summary>
    public string EdgeBreakModelInfo { get; set; } = string.Empty;

    /// <summary>
    /// Coded information on the current values for the S-curve model for this distress. Values are stored as:
    /// [AADI_InitialValue_T100] 
    /// where: AADI is the Age at Distress Initiation, InitialValue is the percent distress observed right
    /// after initiation, and T100 is the time it takes for the distress to reach 100% of the segment area.
    /// </summary>
    public string ScabbingModelInfo { get; set; } = string.Empty;

    /// <summary>
    /// Coded information on the current values for the S-curve model for this distress. Values are stored as:
    /// [AADI_InitialValue_T100] 
    /// where: AADI is the Age at Distress Initiation, InitialValue is the percent distress observed right
    /// after initiation, and T100 is the time it takes for the distress to reach 100% of the segment area.
    /// </summary>
    public string LTCracksModelInfo { get; set; } = string.Empty;

    /// <summary>
    /// Coded information on the current values for the S-curve model for this distress. Values are stored as:
    /// [AADI_InitialValue_T100] 
    /// where: AADI is the Age at Distress Initiation, InitialValue is the percent distress observed right
    /// after initiation, and T100 is the time it takes for the distress to reach 100% of the segment area.
    /// </summary>
    public string MeshCrackModelInfo { get; set; } = string.Empty;

    /// <summary>
    /// Coded information on the current values for the S-curve model for this distress. Values are stored as:
    /// [AADI_InitialValue_T100] 
    /// where: AADI is the Age at Distress Initiation, InitialValue is the percent distress observed right
    /// after initiation, and T100 is the time it takes for the distress to reach 100% of the segment area.
    /// </summary>
    public string ShovingModelInfo { get; set; } = string.Empty;

    /// <summary>
    /// Coded information on the current values for the S-curve model for this distress. Values are stored as:
    /// [AADI_InitialValue_T100] 
    /// where: AADI is the Age at Distress Initiation, InitialValue is the percent distress observed right
    /// after initiation, and T100 is the time it takes for the distress to reach 100% of the segment area.
    /// </summary>
    public string PotholeModelInfo { get; set; } = string.Empty;

    #endregion

    #region Indexes and Objective Value

    private double _pavementDistressIndex;
    private double _surfaceDistressIndex;
    
    private double _objectiveDistressIndex;
    private double _objectiveRemainingSurfaceLife;
    private double _objectiveRutting;
    private double _objectiveNaasra;
    private double _objectiveValueRaw;
    private double _objectiveValue;
    private double _objectiveAreaUnderCurve;

    // Convert all of the above backing variables into read-nonly properties

    public double PavementDistressIndex { get { return _pavementDistressIndex; } }

    public double SurfaceDistressIndex { get { return _surfaceDistressIndex; } }


    /// <summary>
    /// BCA objective distress condition placed on scaling curve (part 2 of 3)
    /// </summary>
    public double ObjectiveDistress { get { return _objectiveDistressIndex; } }

    /// <summary>
    /// BCA objective remaining surface life on scaling curve (part 1 of 3)
    /// </summary>    
    public double ObjectiveRemainingSurfaceLife { get { return _objectiveRemainingSurfaceLife; } }

    /// <summary>
    /// BCA objective rutting on scaling curve (part 3 of 3)
    /// </summary>    
    public double ObjectiveRutting { get { return _objectiveRutting; } }

    /// <summary>
    /// BCA objective roughness on scaling curve (part 3 of 3)
    /// </summary>   
    public double ObjectiveNaasra { get { return _objectiveNaasra; } }

    /// <summary>
    /// BCA objective raw value, based on weighted sum of the objective components
    /// </summary>  
    public double ObjectiveValueRaw { get { return _objectiveValueRaw; } }

    /// <summary>
    /// BCA objective value weighted by Road Type
    /// </summary>
    public double ObjectiveValue { get { return _objectiveValue; } }

    /// <summary>
    /// Goes to BCA objective (menu in Model Configuration), this is the BCA objective scaled by multiplying with treatment area to normalise the cost, 
    /// to use for AUC calculation in BCA model
    /// </summary>
    public double ObjectiveAreaUnderCurve { get { return _objectiveAreaUnderCurve; } }

    /// <summary>
    /// Percent Rank of the PDI for this segment
    /// </summary>
    public double PavementDistressIndexRank { get; set; }

    /// <summary>
    /// Percent Rank of the SDI for this segment
    /// </summary>
    public double SurfaceDistressIndexRank { get; set; }

    /// <summary>
    /// Percent Rank of the Rut Value for this segment
    /// </summary>
    public double RutRank { get; set; }

    /// <summary>
    /// Percent Rank of the Surface Life Achieved for this segment
    /// </summary>
    public double SurfaceLifeAchievedRank { get; set; }


    #endregion

    #region Maintenance Cost

    private double _maintenanceCostPerKm;

    /// <summary>
    /// Maintenance Cost per Km
    /// </summary>
    public double MaintenanceCostPerKm { get { return _maintenanceCostPerKm; } }

    private double GetMaintenanceCostPerKm(ModelBase frameworkModel, StarterModel domainModel, int currentPeriod)
    {
        if (this.SurfaceIsChipSealOrACFlag == 0)
        {
            // If the surface is not chip seal or asphalt concrete, return 0.0
            return 0.0;
        }

        if (PavementDistressIndex < domainModel.Constants.MaintenanceCostPDIThreshold)
        {
            // If the PDI is below the minimum threshold, return 0.0
            return 0.0;
        }

        // 0.0122 * para_naasra + 0.055 * ln(post_maintpred_shove) + 0.048 * ln(post_maintpred_mesh) + 0.243 * ln(para_adt) + 0.644 * ln(para_rut) + 0.01 * para_pave_age +
        // 0.03 * ln(post_maintpred_poth) + 5.227
        double preFactor = 0.0122 * this.Naasra85 +
                           0.055 * Math.Log(Math.Max(this.PctShoving, 0.001)) + 
                           0.048 * Math.Log(Math.Max(this.PctMeshCracks, 0.001)) + 
                           0.243 * Math.Log(Math.Max(this.AverageDailyTraffic, 0.001)) + 
                           0.644 * Math.Log(Math.Max(this.RutParameterValue, 0.001)) +
                           0.01 * this.PavementAge +
                           0.03 * Math.Log(Math.Max(this.PctPotholes, 0.001)) + 
                           5.227;

        double calibrationFactor = domainModel.Constants.MaintenanceCostCalibrationFactor;        
        return (calibrationFactor * Math.Exp(preFactor));
        
    }

    #endregion

    #region Treatment and Candidate Selection Related

    private bool _isTreated = false;
    private int _treatmentCount = 0;

    private int _isCandidateForTreatment = 0;
    private string _candidateSelectionInfo = string.Empty;

    /// <summary>
    /// Flag to be set whenever the model applies a treatment. If this flag is set to true. Value is determined by treatment count.    
    /// </summary>
    public bool IsTreated
    {
        get => _isTreated;        
    }

    /// <summary>
    /// Treatment count. This should be incremented each time a treatment is applied to the segment. If the count is greater than zero, 
    /// the IsTreated flag will automatically be set to true. This property also resets the FaultsAndMaintenancePavementM2 and FaultsAndMaintenanceSurfacingM2
    /// </summary>
    public int TreatmentCount
    {
        get => _treatmentCount;
        set
        {
            // Increment the treatment count and set the IsTreated flag to true.
            _treatmentCount = value;
            if (_treatmentCount > 0)
            {
                _isTreated = true;
                this.FaultsAndMaintenancePavementM2 = 0.0; // Reset pavement faults and maintenance area after treatment
                this.FaultsAndMaintenanceSurfacingM2 = 0.0; // Reset surfacing faults and maintenance area after treatment
            }
        }
    }

    /// <summary>
    /// Flag to indicate if the segment is a candidate for treatment. This is determined by the CandidateSelector class based on various criteria.
    /// </summary>
    public int IsCandidateForTreatment   {  get { return _isCandidateForTreatment; } }

    /// <summary>
    /// Expanatory string for candidate selection outcome. If this is a valid candidate, it will just say 'ok', else the reason
    /// why it is not a valid candidate will be provided.
    /// </summary>
    private string CandidateSelectionOutcome
    {     
        get { return _candidateSelectionInfo; }
    }

    #endregion

    #region Helper Methods

    public void UpdateFormulaValues(ModelBase frameworkModel, StarterModel domainModel, int currentPeriod,
                                    Dictionary<string, object> specialPlaceholders)
    {
        // PDI and SDI
        _pavementDistressIndex = this.GetPavementDistressIndex(frameworkModel, domainModel, currentPeriod);
        _surfaceDistressIndex = this.GetSurfaceDistressIndex(frameworkModel, domainModel, currentPeriod);

        // Sub-Parameters for Objective Values
        _objectiveDistressIndex = this.GetObjectiveDistress(frameworkModel, domainModel, currentPeriod);
        _objectiveRemainingSurfaceLife = this.GetObjectiveRemainingSurfaceLife(frameworkModel, domainModel);
        _objectiveRutting = this.GetObjectiveRutting(frameworkModel, domainModel);
        _objectiveNaasra = this.GetObjectiveNaasra(frameworkModel, domainModel);
        _objectiveValueRaw = this.GetObjectiveValueRaw(frameworkModel, domainModel, currentPeriod);
        _objectiveValue = this.GetObjectiveValue(frameworkModel, domainModel, currentPeriod);
        _objectiveAreaUnderCurve = this.GetObjectiveAreaUnderCurve(frameworkModel, domainModel, currentPeriod);

        // Maintenance Cost
        _maintenanceCostPerKm = this.GetMaintenanceCostPerKm(frameworkModel, domainModel, currentPeriod);

        this.UpdateCandidateSelectionResult(frameworkModel, domainModel, currentPeriod, specialPlaceholders);
    }

    public void UpdateCandidateSelectionResult(ModelBase frameworkModel, StarterModel domainModel, int currentPeriod,
                                               Dictionary<string, object> specialPlaceholders)
    {
        int periodsToNextTreatment = Convert.ToInt32(specialPlaceholders["periods_to_next_treatment"]);
        var csResult = CandidateSelector.EvaluateCandidate(this, frameworkModel, domainModel, currentPeriod, periodsToNextTreatment);
        _isCandidateForTreatment = csResult.IsValidCandidate ? 1 : 0;
        _candidateSelectionInfo = csResult.Outcome;        
    }

    public void UpdateFormulaValuesFromParameters(Dictionary<string, double> numParamValues, Dictionary<string, string> textParamValues)
    {        
        _surfaceDistressIndex = numParamValues["para_sdi"]; 
        _pavementDistressIndex = numParamValues["para_pdi"]; 
        _objectiveDistressIndex = numParamValues["para_obj_distress"]; 
        _objectiveRemainingSurfaceLife = numParamValues["para_obj_rsl"]; 
        _objectiveRutting = numParamValues["para_obj_rutting"]; 
        _objectiveNaasra = numParamValues["para_obj_naasra"]; 
        _objectiveValueRaw = numParamValues["para_obj_o"];
        _objectiveValue = numParamValues["para_obj"];
        _objectiveAreaUnderCurve = numParamValues["para_obj_auc"];
        _maintenanceCostPerKm = numParamValues["para_maint_cost_perkm"]; 
        _candidateSelectionInfo = textParamValues["para_csl_status"]; 
        _isCandidateForTreatment = Convert.ToInt32(numParamValues["para_csl_flag"]); 
    }

    private double GetPavementDistressIndex(ModelBase frameworkModel, StarterModel domainModel, int currentPeriod)
    {
        // Calculate the Pavement Distress Index (PDI) based on the current period.
        return CalculationUtilities.GetPavementDistressIndex(this, frameworkModel, domainModel, currentPeriod);
    }

    private double GetSurfaceDistressIndex(ModelBase frameworkModel, StarterModel domainModel, int currentPeriod)
    {
        // Calculate the Surface Distress Index (SDI) based on the current period.
        return CalculationUtilities.GetSurfacingDistressIndex(this, frameworkModel, domainModel, currentPeriod);
    }


    /// <summary>
    /// BCA objective distress condition placed on scaling curve (part 2 of 3)
    /// </summary>
    /// <param name="currentPeriod">Current modelling period (1,2,3, etc) used to determine PDI and SDI (need to know long or short term)</param>
    private double GetObjectiveDistress(ModelBase frameworkModel, StarterModel domainModel, int currentPeriod)
    {
        double pdi = this.GetPavementDistressIndex(frameworkModel, domainModel, currentPeriod);
        double sdi = this.GetSurfaceDistressIndex(frameworkModel, domainModel, currentPeriod);
        double objectiveDistressPre1 = 0.7 * pdi + 0.3 * sdi;

        //0.4 * post_obj_distress_pre1 + -4
        double objectiveDistressPre = 0.4 * objectiveDistressPre1 - 4.0;
        double objectiveDistress = 100 * CalculationUtilities.Logit(objectiveDistressPre);

        return objectiveDistress;
    }

    /// <summary>
    /// BCA objective remaining surface life on scaling curve (part 1 of 3)
    /// </summary>    
    private double GetObjectiveRemainingSurfaceLife(ModelBase frameworkModel, StarterModel domainModel)
    {
        //-0.5 * para_surf_remain_life + -2.5
        double rslPre = -0.5 * this.SurfaceRemainingLife - 2.5;

        //100 * logit(post_obj_rsl_pre)
        double objectiveRemainingSurfaceLife = 100 * CalculationUtilities.Logit(rslPre);
        return objectiveRemainingSurfaceLife;
    }

    /// <summary>
    /// BCA objective rutting on scaling curve (part 3 of 3)
    /// </summary>    
    private double GetObjectiveRutting(ModelBase frameworkModel, StarterModel domainModel)
    {
        //TODO: This does not distinguish between preserve and holding like MCDA does. Can be fixed.
        double rutExceedanceThreshold = frameworkModel.GetLookupValueNumber("reset_exceed_thresh_rut", "preserve");
        double objRutPre1 = this.RutParameterValue - rutExceedanceThreshold;
        //0.55 * post_obj_rutting_pre1 + -1.65
        double objRutPre = 0.55 * objRutPre1 - 1.65;

        //100 * logit(post_obj_rutting_pre)
        double objectiveRutting = 100 * CalculationUtilities.Logit(objRutPre);

        return objectiveRutting;
    }

    /// <summary>
    /// BCA objective roughness on scaling curve (part 3 of 3)
    /// </summary>    
    private double GetObjectiveNaasra(ModelBase frameworkModel, StarterModel domainModel)
    {
        double naasraExceedanceThreshold = frameworkModel.GetLookupValueNumber("reset_exceed_thresh_naasra", this.SurfaceRoadType);
        double objNaasraPre1 = this.Naasra85 - naasraExceedanceThreshold;

        //0.044 * post_obj_naasra_pre1 + -1.76
        double objNaasraPre = 0.044 * objNaasraPre1 - 1.76;

        //100 * logit(post_obj_naasra_pre)
        double objectiveNaasra = 100 * CalculationUtilities.Logit(objNaasraPre);
        return objectiveNaasra;

    }

    /// <summary>
    /// BCA objective raw value, based on weighted sum of the objective components
    /// </summary>    
    /// <param name="currentPeriod">Current modelling period (1,2,3, etc) used to determine PDI and SDI (need to know long or short term)</param>    
    private double GetObjectiveValueRaw(ModelBase frameworkModel, StarterModel domainModel, int currentPeriod)
    {
        double objDistress = this.GetObjectiveDistress(frameworkModel, domainModel, currentPeriod);
        double objRutting = this.GetObjectiveRutting(frameworkModel, domainModel);
        double objNaasra = this.GetObjectiveNaasra(frameworkModel, domainModel);
        double objRemainingSurfaceLife = this.GetObjectiveRemainingSurfaceLife(frameworkModel, domainModel);

        double objectiveO = 0.3 * objDistress +
                            0.2 * objRemainingSurfaceLife +
                            0.25 * objRutting +
                            0.25 * objNaasra;
        return objectiveO;
    }

    /// <summary>
    /// BCA objective value weighted by Road Type
    /// </summary>
    /// <param name="currentPeriod">Current modelling period (1,2,3, etc) used to determine PDI and SDI (need to know long or short term)</param>    
    private double GetObjectiveValue(ModelBase frameworkModel, StarterModel domainModel, int currentPeriod)
    {
        double objConst = 30;
        double objWeighting = frameworkModel.GetLookupValueNumber("bca_weighting", this.RoadType);
        //post_obj_o * post_obj_weighting + post_obj_c * 1min(post_obj_weighting)
        double objectiveO = this.GetObjectiveValueRaw(frameworkModel, domainModel, currentPeriod) * objWeighting + objConst * (1 - objWeighting);
        return objectiveO;
    }

    /// <summary>
    /// Goes to BCA objective (menu in Model Configuration), this is the BCA objective scaled by multiplying with treatment area to normalise the cost, 
    /// to use for AUC calculation in BCA model
    /// </summary>
    /// <param name="currentPeriod">Current modelling period (1,2,3, etc) used to determine PDI and SDI (need to know long or short term)</param>    
    private double GetObjectiveAreaUnderCurve(ModelBase frameworkModel, StarterModel domainModel, int currentPeriod)
    {
        double objectiveValue = this.GetObjectiveValue(frameworkModel, domainModel, currentPeriod);
        return objectiveValue * this.AreaSquareMetre; // Scale by area
    }

    /// <summary>
    /// Updates the sinks mapping back to parameter values in the model. 
    /// </summary>
    /// <param name="numModParamValues">Return value: Sink holding values for numeric parameters (to be updated by Domain Model). Keys are parameter names, values are assigned values</param>
    /// <param name="textModParamValues">Return value: Sink holding values for text parameters (to be updated by Domain Model). Keys are parameter names, values are assigned values</param>     
    public void SetParameterValues(Action<string, double> numModParamValues, Action<string, string> textModParamValues)
    {
        numModParamValues("para_adt", this.AverageDailyTraffic);
        numModParamValues("para_hcv", this.HeavyVehiclesPerDay);

        numModParamValues("para_pave_age", this.PavementAge);
        numModParamValues("para_pave_remlife", this.PavementRemainingLife);
        numModParamValues("para_pave_life_ach", this.PavementAchievedLife);
        numModParamValues("para_hcv_risk", this.HCVRisk);

        textModParamValues("para_surf_mat", this.SurfaceMaterial);
        textModParamValues("para_surf_class", this.SurfaceClass);
        numModParamValues("para_surf_cs_flag", this.SurfaceIsChipSealFlag);
        numModParamValues("para_surf_cs_or_ac_flag", this.SurfaceIsChipSealOrACFlag);
        textModParamValues("para_surf_road_type", this.SurfaceRoadType);
        numModParamValues("para_surf_thick", this.SurfaceThickness);
        numModParamValues("para_surf_layers", this.SurfaceNumberOfLayers);
        textModParamValues("para_surf_func", this.SurfaceFunction);
        numModParamValues("para_surf_exp_life", this.SurfaceExpectedLife);
        numModParamValues("para_surf_age", this.SurfaceAge);
        numModParamValues("para_surf_life_ach", this.SurfaceAchievedLifePercent);
        numModParamValues("para_surf_remain_life", this.SurfaceRemainingLife);

        numModParamValues("para_flush_pct", this.PctFlushing);
        textModParamValues("para_flush_info", this.FlushingModelInfo);

        numModParamValues("para_edgeb_pct", this.PctEdgeBreaks);
        textModParamValues("para_edgeb_info", this.EdgeBreakModelInfo);

        numModParamValues("para_scabb_pct", this.PctScabbing);
        textModParamValues("para_scabb_info", this.ScabbingModelInfo);

        numModParamValues("para_lt_cracks_pct", this.PctLongTransCracks);
        textModParamValues("para_lt_cracks_info", this.LTCracksModelInfo);

        numModParamValues("para_mesh_cracks_pct", this.PctMeshCracks);
        textModParamValues("para_mesh_cracks_info", this.MeshCrackModelInfo);

        numModParamValues("para_shove_pct", this.PctShoving);
        textModParamValues("para_shove_info", this.ShovingModelInfo);

        numModParamValues("para_poth_pct", this.PctPotholes);
        textModParamValues("para_poth_info", this.PotholeModelInfo);

        numModParamValues("para_rut_increm", this.RutIncrement);
        numModParamValues("para_rut", this.RutParameterValue);

        numModParamValues("para_naasra_increm", this.NaasraIncrement);
        numModParamValues("para_naasra", this.Naasra85);

        numModParamValues("para_sdi", this.SurfaceDistressIndex);
        numModParamValues("para_pdi", this.PavementDistressIndex);

        numModParamValues("para_obj_distress", this.ObjectiveDistress);
        numModParamValues("para_obj_rsl", this.ObjectiveRemainingSurfaceLife);
        numModParamValues("para_obj_rutting", this.ObjectiveRutting);
        numModParamValues("para_obj_naasra", this._objectiveNaasra); 
        numModParamValues("para_obj_o", this.ObjectiveValueRaw);
        numModParamValues("para_obj", this.ObjectiveValue);
        numModParamValues("para_obj_auc", this.ObjectiveAreaUnderCurve);

        numModParamValues("para_maint_cost_perkm", this.MaintenanceCostPerKm);

        textModParamValues("para_csl_status", this.CandidateSelectionOutcome);
        numModParamValues("para_csl_flag", this.IsCandidateForTreatment);

        numModParamValues("para_is_treated_flag", Convert.ToDouble(this.IsTreated)); // Defaults to false initially
        numModParamValues("para_treat_count", this.TreatmentCount); // Defaults to 0 initially

        // The following are Network Parameters - to be set automatically by the framework model:
        //para_pdi_rank
        //para_rut_rank
        //para_sdi_rank
        //para_sla_rank
    }

    #endregion

}


