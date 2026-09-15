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
    /// Surface class: 'cs', 'ac', 'ogpa', 'slurry', 'blocks', 'concrete' or 'other'.
    ///
    /// <para>OGPA AND SLURRY ARE SURFACE CLASSES BUT NOT DETERIORATION GROUPS, and the two must not be
    /// conflated. The class is what lets the treatments trigger name an OGPA treatment rather than an
    /// asphalt one, and what the Resetter writes back from 'treat_surf_class' so the segment is still
    /// recognisably OGPA next period. It is NOT what the condition models are keyed by - see
    /// <see cref="DeteriorationGroup"/>, which resolves both of these to 'ac' on purpose.</para>
    ///
    /// <para>Set from the input column, and overwritten after every treatment from the
    /// 'treat_surf_class' lookup, so a treatment can change it: a chipseal resurfacing on an OGPA
    /// segment leaves it reading 'cs'.</para>
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
    ///
    /// <para>THERE ARE TWO GROUPS AND ONLY TWO, AND 'ogpa' AND 'slurry' BOTH BELONG TO 'ac'. That
    /// mapping is deliberate and is still required: the increment models for rutting, roughness and
    /// cracking are fitted per GROUP, and there is no OGPA or slurry fit to move them to. Giving
    /// either one a group of its own in 'surf_class_group' would send it to a model that does not
    /// exist, and the deterioration models throw naming the group rather than guessing.</para>
    ///
    /// <para>This is the thing to remember when reading <see cref="SurfaceClass"/>: OGPA gained its own
    /// surface class when the treatment names were split out, and gained nothing at all here.</para>
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
    /// Millimetres of chipseal rut growth accumulated so far - the one part of the model that is NOT a
    /// function of surface age.
    ///
    /// <para>IT HOLDS MILLIMETRES, NOT YEARS, AND THE DIFFERENCE IS LOAD-BEARING. The growth rate behind
    /// it is scaled by the segment's modelled heavy vehicle count, which grows every period. Storing a
    /// year count and pricing it at today's rate would revalue everything the segment has ever
    /// accumulated whenever its traffic moved. Adding each period's millimetres as they are earned
    /// confines a traffic change to the year it happens in, so the forecast no longer depends on how wide
    /// the engineer set the traffic band - which is the point, rather than the size of any one step.</para>
    ///
    /// <para>That step is smaller than it first looks and is worth stating honestly, because the note in
    /// the model knowledge file originally put it fifteen times too high. On the delivered breakpoints of
    /// 60 and 600 heavy vehicles the worst single-period re-pricing under the old shape was about 0.6%;
    /// its ceiling is the full band width, 22%, but that needs a tenfold traffic jump in one year. The
    /// figure grows as the band narrows - about 9% at breakpoints of 100 and 110 - which is why removing
    /// the dependence entirely is better than checking it each time the band is retuned.</para>
    ///
    /// <para>It starts at ZERO at year zero, not at the segment's surface age, and that distinction is
    /// the whole point. The chipseal rut level model carries no age term, so exp(mu) already reproduces
    /// the segment's rut as surveyed today; driving the growth increment from the surface age as well
    /// would add that growth a second time, inflating year-zero chipseal rutting by about 29% at the
    /// median surface age and roughly doubling the share of the network reading above the 6 mm
    /// reporting threshold before the forecast has run a single year.</para>
    ///
    /// <para>A resurfacing leaves it alone: a chipseal follows the shape of what it is laid on, so the
    /// rut is inherited by the new seal rather than renewed. Measured on this network, chipseal rut is
    /// 4.19 mm under surfaces up to six years old against 3.77 mm under surfaces over twenty - it does
    /// not improve with a new seal, where asphalt rut does. A rehabilitation returns it to zero.</para>
    ///
    /// <para>Asphalt ignores this entirely; its rutting runs on surface age like everything else.</para>
    /// </summary>
    public double RutGrowthMillimetres { get; set; }

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

    /// <summary>
    /// True once the model has rehabilitated this segment, and never false again afterwards.
    ///
    /// <para>WHAT IT CARRIES. A rehabilitation is the one treatment the fitted models cannot describe:
    /// every segment they were built on is a surfacing over an old pavement - median pavement age 63
    /// years, not one reconstructed pavement in the whole file - so their prediction at surface age
    /// zero means "a fresh surface on a sixty-year-old pavement", not "a new road". A rebuilt segment
    /// therefore carries a permanent offset in log space, from the 'rehab_offsets' lookup set, for the
    /// rest of its life. This flag is what remembers that it is entitled to one.</para>
    ///
    /// <para>It survives a later resurfacing on purpose: a reseal does not undo the fact that the
    /// pavement underneath was rebuilt. It is also deliberately NOT the same thing as the treatment
    /// count, which counts reseals too.</para>
    ///
    /// <para>Segments rehabilitated before the base date do not carry it. Their condition comes from a
    /// survey, and the year-zero inversion already places them - see the Initialiser. The exception is
    /// a segment rehabilitated AFTER its last survey, which has no usable reading and is initialised
    /// from the model as a rebuilt pavement.</para>
    /// </summary>
    public bool HasBeenRehabilitated { get; set; }

    /// <summary>
    /// Size of the credit a pre-repair left on the segment's CRACKING SEVERITY deviate, in deviate
    /// units, before decay. Zero for a segment that has never had one.
    ///
    /// <para>WHAT A PRE-REPAIR IS, BECAUSE IT IS NOT A SMALL RESURFACING. Preseal repairs on chipseal
    /// and heavy maintenance on asphalt are the third treatment class, and they are defined by what
    /// they do NOT touch: not the clock, and not the deflection. They act on the persistent deviate,
    /// which carries the segment's excess distress relative to what its age predicts - exactly what
    /// localised repairs remove. That makes the benefit self-limiting with no rule to enforce it: a
    /// segment already average for its age has nothing anomalous to repair and gets nothing.</para>
    ///
    /// <para>CRACKING IS CREDITED ON SEVERITY AND NOT ON ONSET, and the distinction is the point:
    /// reduce how much the segment has cracked, do not claim the repair made it uncracked. The onset
    /// position is left exactly as it was.</para>
    ///
    /// <para>The credit decays towards the permanent fraction in the 'pre_repair' lookup set, on the
    /// clock below. A rehabilitation clears it, because a rehabilitation redraws the deviates the
    /// credit was measured against.</para>
    /// </summary>
    public double PreRepairCreditCracking { get; set; }

    /// <summary>
    /// Size of the credit a pre-repair left on the segment's RUTTING deviate, in deviate units, before
    /// decay.
    /// <para>This is the case that matters most, because a reseal does nothing at all to chipseal rut -
    /// the seal follows the shape of what it is laid on. Without a pre-repair reset there is no lever
    /// between no effect and rebuilding.</para>
    /// </summary>
    public double PreRepairCreditRut { get; set; }

    /// <summary>
    /// Size of the credit a pre-repair left on the segment's ROUGHNESS deviate, in deviate units.
    /// <para>ZERO in every period as delivered, because the roughness repair extent in the 'pre_repair'
    /// lookup set is zero: patching does not smooth a road and often roughens it. It is carried as
    /// state rather than left out so that the switch in the spreadsheet genuinely works.</para>
    /// </summary>
    public double PreRepairCreditIri { get; set; }

    /// <summary>
    /// Years since the segment's last pre-repair, and the clock the three credits above decay on:
    /// credit = size * (retained + (1 - retained) * exp(-years / tau)).
    /// <para>Set to zero at a pre-repair and advanced by one every other period, including across a
    /// resurfacing - a reseal does not undo a digout, and the deviate it credits is retained too. A
    /// rehabilitation returns it to zero along with the credits themselves.</para>
    /// </summary>
    public double PreRepairYears { get; set; }

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

    // The BCA objective-value fields went with the objective-value chain in stage 4. Nothing assigns or reads them.

    public double PavementDistressIndex { get { return _pavementDistressIndex; } }

    public double SurfaceDistressIndex { get { return _surfaceDistressIndex; } }

    /// <summary>
    /// Percent Rank (0 to 100) of the PDI for this segment across the network. Parameter 'par_pdi_rank', which is
    /// OWNED BY THE NETWORK FUNCTIONS: the framework recomputes it from 'par_pdi' after initialisation and at the end
    /// of every period. The domain model only reads it back in the factory and passes it through.
    /// </summary>
    public double PavementDistressIndexRank { get; set; }

    /// <summary>
    /// Percent Rank (0 to 100) of the SDI for this segment across the network. Parameter 'par_sdi_rank', owned by
    /// the network functions in the same way as <see cref="PavementDistressIndexRank"/>.
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

    /// <summary>
    /// Reads back the values <see cref="UpdateFormulaValues"/> worked out at the end of the previous period - PDI,
    /// SDI, their network ranks and the candidate selection result. Called from the factory.
    /// <para>This is not optional. <see cref="StarterModel.GetTreatmentCandidates"/> builds the segment from the
    /// factory and never calls <see cref="UpdateFormulaValues"/>, so without this read-back the treatments trigger
    /// sees every segment as not a candidate and returns nothing at all - not even a forced second coat - and, past
    /// that gate, PDI, SDI and both ranks at zero. The run completes and nothing reports it.</para>
    /// <para>Previous-period values are the design, not a compromise: the candidate selector adds one to the period
    /// for exactly this lag, and the ranks can only be computed once every element has been processed. An index, its
    /// rank and the candidate flag therefore always describe the same moment.</para>
    /// </summary>
    public void ReadPreviousPeriodResultsFromParameters(Dictionary<string, double> numParamValues,
                                                       Dictionary<string, string> textParamValues)
    {
        _pavementDistressIndex = numParamValues["par_pdi"];
        _surfaceDistressIndex = numParamValues["par_sdi"];
        PavementDistressIndexRank = numParamValues["par_pdi_rank"];
        SurfaceDistressIndexRank = numParamValues["par_sdi_rank"];
        _isCandidateForTreatment = Convert.ToInt32(numParamValues["par_csl_flag"]);
        _candidateSelectionInfo = textParamValues["par_csl_status"];
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
        numModParamValues("par_rut_growth_mm", this.RutGrowthMillimetres);
        numModParamValues("par_crack_u_onset", this.CrackOnsetPosition);
        numModParamValues("par_crack_w_sev", this.CrackSeverityQuantile);
        numModParamValues("par_crack_below", this.CrackingBelowOnset);
        numModParamValues("par_crack_init_src", this.CrackingInitSource);
        numModParamValues("par_rut_init_src", this.RutInitSource);
        numModParamValues("par_iri_init_src", this.IriInitSource);
        numModParamValues("par_rehab_flag", Convert.ToDouble(this.HasBeenRehabilitated));

        // -- Pre-repair credits. Zero for every segment until one is applied --
        numModParamValues("par_prerep_dz_crack", this.PreRepairCreditCracking);
        numModParamValues("par_prerep_dz_rut", this.PreRepairCreditRut);
        numModParamValues("par_prerep_dz_iri", this.PreRepairCreditIri);
        numModParamValues("par_prerep_yrs", this.PreRepairYears);

        // -- Distress indices, and their network ranks --
        numModParamValues("par_pdi", this.PavementDistressIndex);
        numModParamValues("par_sdi", this.SurfaceDistressIndex);
        // The two ranks are network function outputs: whatever is written here is overwritten once every element
        // has been processed. They are passed through rather than left unwritten so that the parameter never
        // holds a stray zero between the element step and the network calculation.
        numModParamValues("par_pdi_rank", this.PavementDistressIndexRank);
        numModParamValues("par_sdi_rank", this.SurfaceDistressIndexRank);
    }
    #endregion

}


