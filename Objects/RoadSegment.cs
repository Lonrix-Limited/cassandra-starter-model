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
    /// Segment identifier. Maps to input column "inp_seg_code".
    /// </summary>
    public string SegmentName { get; set; } = string.Empty;

    /// <summary>
    /// Section ID. Maps to "inp_section_id".
    /// </summary>
    public double SectionID { get; set; }

    /// <summary>
    /// Name of the section. Maps to "inp_section_name".
    /// </summary>
    public string SectionName { get; set; } = string.Empty;

    /// <summary>
    /// Start metre of the segment. Maps to "inp_loc_from".
    /// </summary>
    public double LocFrom { get; set; }

    /// <summary>
    /// End metre of the segment. Maps to "inp_loc_to".
    /// </summary>
    public double LocTo { get; set; }

    /// <summary>
    /// Lane code. Maps to "inp_lane".
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
    /// Width in metres. Read directly from input column "inp_width" - it is no longer derived from Area
    /// and Length, because Length can be zero.
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
    /// Surfacing date as a text/string value in ISO format 'yyyymmdd', as parsed by ParseISODateNoTime.
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
    /// Pavement construction date as a text/string value in ISO format 'yyyymmdd', as parsed by ParseISODateNoTime.
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
    /// Average daily traffic. This is the MODELLED value: it grows each period by the traffic growth
    /// percentage, and it is what the treatment trigger and the reporting use.
    /// <para>The deterioration models do NOT read it - they read SurveyedAverageDailyTraffic below.</para>
    /// </summary>
    public double AverageDailyTraffic { get; set; }

    /// <summary>
    /// Average daily traffic exactly as surveyed, held at its measured value for the whole run. Maps
    /// to input column "inp_adt".
    /// <para>This exists because the deterioration models must be evaluated on frozen traffic. In the
    /// roughness models the traffic coefficient is NEGATIVE - busier roads on this network were built
    /// and maintained to a higher standard - so feeding growing traffic to them predicts roads getting
    /// smoother as they get busier. Measured on this network: with traffic growing at 2% a year, 95%
    /// of chipseal segments come out smoother in thirty years than they are today, and network median
    /// roughness falls. Freezing costs almost nothing anywhere else, because thirty years of growth is
    /// worth about 2% of the age effect on rutting and 1% on cracking.</para>
    /// </summary>
    public double SurveyedAverageDailyTraffic { get; set; }

    /// <summary>
    /// Heavy vehicle percentage.
    /// </summary>
    public double HeavyVehiclePercentage { get; set; }

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

    #region Historical Maintenance

    // NOTE: Maintenance is NOT modelled forward in time. Every property in this region is a
    // HISTORICAL record taken from the client's input data, describing work already carried out
    // before the model's base date. The values are read once from the raw input and never change
    // over the modelling periods - a treatment applied by the model does not reset them, and no
    // increment model advances them.
    //
    // Their purpose is to inform treatment selection in the early periods: a segment that has
    // needed repeated patching recently is a different proposition from one that has not. The
    // trigger logic that uses them is specified separately.

    /// <summary>
    /// Pavement maintenance extent over the LAST YEAR, as a percentage of sub-segments with
    /// maintenance recorded. Historical record from the input data - not modelled forward.
    /// </summary>
    public double MaintPavementExtentLastYear { get; set; }

    /// <summary>
    /// Pavement maintenance extent over the LAST THREE YEARS, as a percentage of sub-segments with
    /// maintenance recorded. Historical record from the input data - not modelled forward.
    /// </summary>
    public double MaintPavementExtentLast3Years { get; set; }

    /// <summary>
    /// Pothole maintenance extent over the LAST YEAR, as a percentage of sub-segments with
    /// maintenance recorded. Historical record from the input data - not modelled forward.
    /// </summary>
    public double MaintPotholeExtentLastYear { get; set; }

    /// <summary>
    /// Pothole maintenance extent over the LAST THREE YEARS, as a percentage of sub-segments with
    /// maintenance recorded. Historical record from the input data - not modelled forward.
    /// </summary>
    public double MaintPotholeExtentLast3Years { get; set; }

    /// <summary>
    /// Surfacing maintenance extent over the LAST YEAR, as a percentage of sub-segments with
    /// maintenance recorded. Historical record from the input data - not modelled forward.
    /// </summary>
    public double MaintSurfacingExtentLastYear { get; set; }

    /// <summary>
    /// Surfacing maintenance extent over the LAST THREE YEARS, as a percentage of sub-segments with
    /// maintenance recorded. Historical record from the input data - not modelled forward.
    /// </summary>
    public double MaintSurfacingExtentLast3Years { get; set; }

    #endregion

    #region High Speed Data (HSD) - Rutting, Roughness and Deflection

    /// <summary>
    /// Date of the high speed data survey, as an ISO format string 'yyyymmdd'. One survey date now covers
    /// rutting, roughness, texture and the LCMS visual distresses, because they are collected on the same run.
    /// Used during initialisation to decide whether the surveyed condition predates the last surfacing or
    /// rehabilitation. Do not use it after initialisation.
    /// </summary>
    public string SurveyDateString { get; set; } = string.Empty;

    #region Rutting

    /// <summary>
    /// Mean rut depth in mm, exactly as surveyed. Do not use this after initialisation - use the
    /// RutParameterValue property instead, which carries the modelled value.
    /// </summary>
    public double RutMeanSurveyed { get; set; }

    /// <summary>
    /// Rut depth in mm. This is the modelled rutting condition of the segment: set during initialisation
    /// from the surveyed value, then advanced each period by the rut increment model.
    /// </summary>
    public double RutParameterValue { get; set; }

    /// <summary>
    /// Rut increment in mm/year.
    /// </summary>
    public double RutIncrement { get; set; }

    #endregion

    #region Roughness (IRI, and Naasra for reporting)

    /// <summary>
    /// IRI in mm/m, exactly as surveyed. Do not use this after initialisation - use the Iri property
    /// instead, which carries the modelled value.
    /// </summary>
    public double IriSurveyed { get; set; }

    /// <summary>
    /// IRI in mm/m. This is the modelled roughness condition of the segment and the quantity the model
    /// actually works in: set during initialisation from the surveyed value, then advanced each period by
    /// the roughness increment model.
    /// </summary>
    public double Iri { get; set; }

    /// <summary>
    /// IRI increment in mm/m per year.
    /// </summary>
    public double IriIncrement { get; set; }

    /// <summary>
    /// Naasra count, converted from IRI. This is a REPORTING value only - nothing in the model deteriorates,
    /// triggers or resets on it, and there is no separate Naasra state. It is derived from the Iri property
    /// on every read, so it cannot drift out of step with the roughness the model is actually carrying.
    /// </summary>
    public double Naasra
    {
        get { return ConvertIriToNaasra(this.Iri); }
    }

    /// <summary>
    /// Converts an IRI value (mm/m) to a Naasra count using the relationship NAASRA = 26.49 * IRI - 1.27.
    /// </summary>
    /// <param name="iri">IRI value in mm/m</param>
    /// <returns>Equivalent Naasra count</returns>
    public static double ConvertIriToNaasra(double iri)
    {
        return NaasraPerIri * iri + NaasraIriOffset;
    }

    /// <summary>
    /// Converts a Naasra count to an IRI value (mm/m) - the inverse of ConvertIriToNaasra. Needed because
    /// several lookup sets (roughness reset thresholds and improvement factors) hold their values on the
    /// Naasra scale, so a reset is applied in Naasra terms and converted back to IRI.
    /// </summary>
    /// <param name="naasra">Naasra count</param>
    /// <returns>Equivalent IRI value in mm/m</returns>
    public static double ConvertNaasraToIri(double naasra)
    {
        return (naasra - NaasraIriOffset) / NaasraPerIri;
    }

    /// <summary>
    /// Slope of the IRI to Naasra conversion: NAASRA = 26.49 * IRI - 1.27.
    /// </summary>
    private const double NaasraPerIri = 26.49;

    /// <summary>
    /// Intercept of the IRI to Naasra conversion: NAASRA = 26.49 * IRI - 1.27.
    /// </summary>
    private const double NaasraIriOffset = -1.27;

    #endregion

    #region Deflection

    /// <summary>
    /// Central deflection (D0) in mm, from the 75th percentile Lightweight Deflectometer measurement.
    /// A static input measure - it is not modelled forward in time.
    /// </summary>
    public double CentralDeflection { get; set; }

    #endregion

    #endregion

    #region Visual Condition Distresses (LCMS)

    // The model carries three visual distresses: cracking, flushing and ravelling. Each is surveyed by the
    // LCMS on the same run as the high speed data, so the survey date is SurveyDateString above.
    //
    // The seven jFunction-era distresses (edge breaks, scabbing, shoving, potholes, and the split between
    // mesh and longitudinal/transverse cracking) and their S-curve calibration strings have been removed:
    // the new input data does not carry them separately, and the replacement increment models are specified
    // against these three.

    /// <summary>
    /// Cracking, as a percentage of lane length. A value of 25 means the recorded cracking length equals a
    /// quarter of the segment's lane length. Can exceed 100 where several cracks run side by side in the
    /// same stretch.
    /// </summary>
    public double PctCracking { get; set; }

    /// <summary>
    /// Flushing, as a percentage of lane length. Can exceed 100, because the two wheel paths are added
    /// together: both wheel paths fully flushed reads as 200.
    /// </summary>
    public double PctFlushing { get; set; }

    /// <summary>
    /// Ravelling, as a percentage of lane length.
    /// </summary>
    public double PctRavelling { get; set; }

    #endregion

    #region Deterioration model state

    // WHY THIS REGION EXISTS. The deterioration models are LEVEL models, not increments: the value for
    // a year is recomputed from the segment's surface age rather than added to last year's value. What
    // makes one segment differ from another of the same age, surface and traffic is a random draw made
    // ONCE and then held for the life of the surfacing - the spread between otherwise identical
    // segments is the phenomenon itself, not error around a curve, so annual noise will not do.
    //
    // Every property here is therefore a model PARAMETER: it has to survive from one period to the
    // next. If any one of them stopped being written or read back, the framework would leave it at
    // zero, every segment would become exactly average, and nothing would report it.

    /// <summary>
    /// Which of the two fitted model groups this segment belongs to - "ac" or "cs". Resolved from the
    /// surface class through the 'surf_class_group' lookup set, which is also where blocks, concrete
    /// and other surfaces get placed. Set by the factory; not a parameter, because it follows from the
    /// surface class.
    /// </summary>
    public string DeteriorationGroup { get; set; } = string.Empty;

    /// <summary>
    /// The segment's persistent rutting deviate, drawn once and clamped to the configured number of
    /// standard deviations. The rut level is scaled by exp(sigma * this).
    /// <para>The clamp lives in the deterioration models, from the 'multiplier_clamp_sd' lookup. The
    /// parameter is declared wider than that on purpose: a declared range equal to a tunable bound
    /// would mean raising the lookup got silently truncated by the framework instead.</para>
    /// </summary>
    public double RutDeviate { get; set; }

    /// <summary>
    /// The segment's persistent roughness deviate, drawn once and clamped. The IRI level is scaled by
    /// exp(sigma * this).
    /// </summary>
    public double IriDeviate { get; set; }

    /// <summary>
    /// The segment's position in the cracking onset order, between 0 and 1. The segment has cracked in
    /// any year where this sits below the modelled onset probability for its current surface age.
    /// <para>Deliberately compared afresh each year rather than latched into a flag. The comparison is
    /// already one-way within a surfacing cycle, because the onset probability only rises with age,
    /// and leaving it stateless is what makes a resurfacing work with no special case at all.</para>
    /// </summary>
    public double CrackOnsetPosition { get; set; }

    /// <summary>
    /// The segment's quantile within the cracking severity distribution, between 0 and 1, used once it
    /// has cracked. Not interchangeable with a normal deviate: it indexes a LEFT-TRUNCATED
    /// distribution, and a raw normal in its place would put sub-threshold cracking on segments that
    /// have already cracked.
    /// </summary>
    public double CrackSeverityQuantile { get; set; }

    /// <summary>
    /// Cracking percentage carried in any year the segment has not cracked. Held rather than redrawn,
    /// because that group shows no age trend and redrawing it annually would invent variation the data
    /// does not support. Below the onset threshold by construction.
    /// </summary>
    public double CrackingBelowOnset { get; set; }

    /// <summary>
    /// How year-zero cracking was set: 0 surveyed and reproduced exactly, 1 surveyed but the deviate
    /// was clamped, 2 not surveyed so the segment was inferred from the model.
    /// <para>Reporting must be able to say how much of the network was measured and how much inferred.
    /// Nearly 30% of segments carry no cracking survey, which is far too much to leave silent.</para>
    /// </summary>
    public int CrackingInitSource { get; set; }

    /// <summary>
    /// How year-zero rutting was set: 0 surveyed and reproduced exactly, 1 surveyed but clamped. Every
    /// segment has a rut reading, so "inferred" does not arise.
    /// </summary>
    public int RutInitSource { get; set; }

    /// <summary>
    /// How year-zero roughness was set: 0 surveyed and reproduced exactly, 1 surveyed but clamped.
    /// Missing roughness is imputed before the run to a plausible value, so it cannot be detected here
    /// and "inferred" does not arise.
    /// </summary>
    public int IriInitSource { get; set; }

    /// <summary>Value of CrackingInitSource, RutInitSource or IriInitSource for a surveyed segment.</summary>
    public const int InitSourceSurveyed = 0;

    /// <summary>Value of the init-source properties for a surveyed segment whose deviate was clamped.</summary>
    public const int InitSourceClamped = 1;

    /// <summary>Value of the init-source properties for a segment with no usable survey.</summary>
    public const int InitSourceInferred = 2;

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
    /// the IsTreated flag will automatically be set to true.
    /// <para>Note: this no longer clears the maintenance properties. Those are a historical record of work
    /// already done before the base date, and a treatment applied by the model does not change what happened.</para>
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
        // Every parameter declared on the 'parameters' sheet of domain_model_setup.xlsx must be written here,
        // and the names must match exactly. A declared parameter that is never given a value is NOT an error:
        // the framework allocates it and leaves it at zero, so the run completes and the outputs carry a column
        // of zeros that reads as a result. Keep this list in the same order as the sheet so a gap is visible.

        // -- Traffic --
        numModParamValues("par_adt", this.AverageDailyTraffic);
        numModParamValues("par_hcv", this.HeavyVehiclesPerDay);

        // -- Pavement --
        numModParamValues("par_pave_age", this.PavementAge);
        numModParamValues("par_pave_remlife", this.PavementRemainingLife);
        numModParamValues("par_pave_life_ach", this.PavementAchievedLife);
        numModParamValues("par_d0", this.CentralDeflection);
        numModParamValues("par_hcv_risk", this.HCVRisk);

        // -- Surfacing --
        textModParamValues("par_surf_mat", this.SurfaceMaterial);
        textModParamValues("par_surf_class", this.SurfaceClass);
        numModParamValues("par_surf_cs_flag", this.SurfaceIsChipSealFlag);
        numModParamValues("par_surf_cs_or_ac_flag", this.SurfaceIsChipSealOrACFlag);
        textModParamValues("par_surf_road_type", this.SurfaceRoadType);
        numModParamValues("par_surf_thick", this.SurfaceThickness);
        numModParamValues("par_surf_layers", this.SurfaceNumberOfLayers);
        textModParamValues("par_surf_func", this.SurfaceFunction);
        numModParamValues("par_surf_exp_life", this.SurfaceExpectedLife);
        numModParamValues("par_surf_age", this.SurfaceAge);
        numModParamValues("par_surf_life_ach", this.SurfaceAchievedLifePercent);
        numModParamValues("par_surf_remain_life", this.SurfaceRemainingLife);

        // -- Visual distresses --
        numModParamValues("par_flush_pct", this.PctFlushing);
        numModParamValues("par_crack_pct", this.PctCracking);
        numModParamValues("par_ravel_pct", this.PctRavelling);

        // -- Rutting --
        numModParamValues("par_rut_increm", this.RutIncrement);
        numModParamValues("par_rut", this.RutParameterValue);

        // -- Roughness. par_naasra is derived from par_iri and is carried for reporting only --
        numModParamValues("par_iri_increm", this.IriIncrement);
        numModParamValues("par_iri", this.Iri);
        numModParamValues("par_naasra", this.Naasra);

        // -- Candidate selection and treatment history --
        textModParamValues("par_csl_status", this.CandidateSelectionOutcome);
        numModParamValues("par_csl_flag", this.IsCandidateForTreatment);
        numModParamValues("par_is_treated_flag", Convert.ToDouble(this.IsTreated));
        numModParamValues("par_treat_count", this.TreatmentCount);

        // -- Deterioration model state. Drawn once per segment and carried for the life of the
        //    surfacing; see the 'Deterioration model state' region for why these must persist --
        numModParamValues("par_rut_z", this.RutDeviate);
        numModParamValues("par_iri_z", this.IriDeviate);
        numModParamValues("par_crack_u_onset", this.CrackOnsetPosition);
        numModParamValues("par_crack_w_sev", this.CrackSeverityQuantile);
        numModParamValues("par_crack_below", this.CrackingBelowOnset);
        numModParamValues("par_crack_init_src", this.CrackingInitSource);
        numModParamValues("par_rut_init_src", this.RutInitSource);
        numModParamValues("par_iri_init_src", this.IriInitSource);
    }
    #endregion

}


