using JCass_ModelCore.Models;

namespace StarterModel.Objects;

/// <summary>
/// Builds a RoadSegment, either from the client's raw input data at the start of the run, or from the model's
/// own parameter values in any later period.
/// <para>The two methods below read the SAME set of raw input columns. Whenever a column is added, renamed or
/// dropped, both methods have to change together - a column updated in one and not the other is the classic
/// bug here, because initialisation then works on different data from every period after it.</para>
/// </summary>
public static class RoadSegmentFactory
{

    /// <summary>
    /// Creates a RoadSegment from the client's raw input data. Use this ONLY at initialisation: it reads the
    /// network data as loaded and knows nothing about model parameters.
    /// </summary>
    /// <param name="model">Framework model, used to read the raw input data and the lookup sets</param>
    /// <param name="domainModel">Domain model, used for its Constants - in particular the surface
    /// class to deterioration group mapping</param>
    /// <param name="elementIndex">Zero-based index of the element</param>
    public static RoadSegment GetFromRawData(ModelBase model, StarterModel domainModel, int elementIndex)
    {
        RoadSegment segment = new RoadSegment();

        segment.ElementIndex = elementIndex; // Set the element index for this segment

        // Identification
        segment.SegmentName = model.GetInputDataText(elementIndex, "inp_seg_code");
        segment.SectionID = model.GetInputDataNumber(elementIndex, "inp_section_id");
        segment.SectionName = model.GetInputDataText(elementIndex, "inp_section_name");
        segment.LocFrom = model.GetInputDataNumber(elementIndex, "inp_loc_from");
        segment.LocTo = model.GetInputDataNumber(elementIndex, "inp_loc_to");
        segment.LaneCode = model.GetInputDataText(elementIndex, "inp_lane");

        // Size and quantity. Width is now given directly rather than derived from area and length, which
        // divided by an input that can be zero.
        segment.LengthInMetre = model.GetInputDataNumber(elementIndex, "inp_length");
        segment.AreaSquareMetre = model.GetInputDataNumber(elementIndex, "inp_area_m2");
        segment.WidthInMetre = model.GetInputDataNumber(elementIndex, "inp_width");

        // Flags. These are declared as NUMBER columns holding 0 or 1, so they must be read as numbers -
        // reading a numeric column as text throws, naming the column.
        segment.CanTreatFlag = model.GetInputDataNumber(elementIndex, "inp_can_treat_flag") == 1;
        segment.CanRehabFlag = model.GetInputDataNumber(elementIndex, "inp_can_rehab_flag") == 1;
        segment.AsphaltOkFlag = model.GetInputDataNumber(elementIndex, "inp_thin_ac_ok_flag") == 1;
        segment.EarliestTreatmentPeriod = model.GetInputDataNumber(elementIndex, "inp_earliest_treat_period");

        // Classification. The UrbanRural, ONRC and RoadClass setters lower-case on assignment, which is what
        // keeps SurfaceRoadType matching the lower-case keys in the reset lookup sets.
        segment.UrbanRural = model.GetInputDataText(elementIndex, "inp_urban_rural");
        segment.ONRC = model.GetInputDataText(elementIndex, "inp_onrc");

        // Look up Road Class from the ONRC value. Do not expect a road class column in the input data: client
        // files carry client-variant values for it.
        segment.RoadClass = model.GetLookupValueText("road_class", segment.ONRC);

        // Traffic. The same column seeds both the modelled ADT, which grows each period, and the
        // surveyed ADT, which the deterioration models hold frozen at the measured value.
        segment.AverageDailyTraffic = model.GetInputDataNumber(elementIndex, "inp_adt");
        segment.SurveyedAverageDailyTraffic = segment.AverageDailyTraffic;
        segment.HeavyVehiclePercentage = model.GetInputDataNumber(elementIndex, "inp_heavy_perc");
        segment.TrafficGrowthPercent = model.GetInputDataNumber(elementIndex, "inp_traff_growth_perc");

        // Surfacing
        segment.SurfaceClass = model.GetInputDataText(elementIndex, "inp_surf_class");
        segment.DeteriorationGroup = domainModel.Constants.GetDeteriorationGroup(segment.SurfaceClass);
        segment.NextSurface = model.GetInputDataText(elementIndex, "inp_next_surf");
        segment.SurfacingDateString = model.GetInputDataText(elementIndex, "inp_surf_date");
        segment.SurfaceFunction = model.GetInputDataText(elementIndex, "inp_surf_function");
        segment.SurfaceMaterial = model.GetInputDataText(elementIndex, "inp_surf_material");
        segment.SurfaceExpectedLife = model.GetInputDataNumber(elementIndex, "inp_surf_life_expected");
        segment.SurfaceNumberOfLayers = model.GetInputDataNumber(elementIndex, "inp_surf_layers");
        segment.SurfaceThickness = model.GetInputDataNumber(elementIndex, "inp_surf_thick");

        // Pavement. Remaining life and central deflection both come from the Lightweight Deflectometer
        // survey, at the 75th percentile.
        segment.PavementDateString = model.GetInputDataText(elementIndex, "inp_pave_date");
        segment.PavementRemainingLife = model.GetInputDataNumber(elementIndex, "inp_lmd_remaining_life_75th");
        segment.CentralDeflection = model.GetInputDataNumber(elementIndex, "inp_lmd_d0_75th");

        // High speed data. One survey date covers rutting, roughness and the LCMS visual distresses.
        //
        // NOTE: inp_rut_rate and inp_iri_rate are deliberately NOT read. They are arithmetic rather
        // than measurement - each is the observed level minus an assumed as-new baseline, divided by
        // the surface age - so they carry nothing the level and the age do not already carry. The
        // replacement deterioration models recompute the level from age directly, and a rate column
        // has no part in them.
        segment.SurveyDateString = model.GetInputDataText(elementIndex, "inp_hsd_survey_date");
        segment.RutMeanSurveyed = model.GetInputDataNumber(elementIndex, "inp_rut_mean");
        segment.IriSurveyed = model.GetInputDataNumber(elementIndex, "inp_iri_mean");

        // Visual distresses, from the LCMS survey
        segment.PctCracking = model.GetInputDataNumber(elementIndex, "inp_pct_cracks");
        segment.PctFlushing = model.GetInputDataNumber(elementIndex, "inp_pct_flush");
        segment.PctRavelling = model.GetInputDataNumber(elementIndex, "inp_pct_ravel");

        // Historical maintenance. Read once, never modelled forward - see the RoadSegment region comment.
        segment.MaintPavementExtentLastYear = model.GetInputDataNumber(elementIndex, "inp_maint_pa_ext");
        segment.MaintPavementExtentLast3Years = model.GetInputDataNumber(elementIndex, "inp_maint_pa_rept_ext");
        segment.MaintPotholeExtentLastYear = model.GetInputDataNumber(elementIndex, "inp_maint_poth_ext");
        segment.MaintPotholeExtentLast3Years = model.GetInputDataNumber(elementIndex, "inp_maint_poth_rept_ext");
        segment.MaintSurfacingExtentLastYear = model.GetInputDataNumber(elementIndex, "inp_maint_su_ext");
        segment.MaintSurfacingExtentLast3Years = model.GetInputDataNumber(elementIndex, "inp_maint_su_rept_ext");

        return segment;
    }

    /// <summary>
    /// Gets a segment from the model's input and parameter values. Use this AFTER initialisation, when the
    /// model already holds values for its parameters - initial, incremented or reset.
    /// <para>Anything that evolves over the run is read from the parameter dictionaries. Anything static is
    /// read from the raw input, exactly as GetFromRawData reads it.</para>
    /// </summary>
    /// <param name="frameworkModel">Framework model, used for the lookup sets</param>
    /// <param name="domainModel">Domain model, used for its Constants - in particular the surface
    /// class to deterioration group mapping</param>
    /// <param name="numInputValues">Raw numeric input values for the element, keyed by column name</param>
    /// <param name="textInputValues">Raw text input values for the element, keyed by column name</param>
    /// <param name="numParamValues">Current values for numeric model parameters, keyed by parameter name</param>
    /// <param name="textParamValues">Current values for text model parameters, keyed by parameter name</param>
    /// <param name="elementIndex">Zero-based index of the element</param>
    /// <param name="iPeriod">Modelling period (1, 2, ... n)</param>
    public static RoadSegment GetFromModel(ModelBase frameworkModel, StarterModel domainModel,
        Dictionary<string, double> numInputValues, Dictionary<string, string> textInputValues,
        Dictionary<string, double> numParamValues, Dictionary<string, string> textParamValues, int elementIndex, int iPeriod)
    {
        RoadSegment segment = new RoadSegment();

        segment.ElementIndex = elementIndex; // Set the element index for this segment

        // ---------------------------------------------------------------------------------------------
        // Static properties, from the raw input data. These do not change over the modelling periods.
        // Keep this block matching the reads in GetFromRawData above.
        // ---------------------------------------------------------------------------------------------

        // Identification
        segment.SegmentName = textInputValues["inp_seg_code"];
        segment.SectionID = numInputValues["inp_section_id"];
        segment.SectionName = textInputValues["inp_section_name"];
        segment.LocFrom = numInputValues["inp_loc_from"];
        segment.LocTo = numInputValues["inp_loc_to"];
        segment.LaneCode = textInputValues["inp_lane"];

        // Size and quantity
        segment.LengthInMetre = numInputValues["inp_length"];
        segment.AreaSquareMetre = numInputValues["inp_area_m2"];
        segment.WidthInMetre = numInputValues["inp_width"];

        // Flags
        segment.CanTreatFlag = numInputValues["inp_can_treat_flag"] == 1;
        segment.CanRehabFlag = numInputValues["inp_can_rehab_flag"] == 1;
        segment.AsphaltOkFlag = numInputValues["inp_thin_ac_ok_flag"] == 1;
        segment.EarliestTreatmentPeriod = numInputValues["inp_earliest_treat_period"];

        // TODO: To discuss and make hardcoded value of 7 a lookup parameter
        if (iPeriod > 7) segment.CanRehabFlag = true; // For congruence with JFunction model, we allow rehab after 7 periods

        // Classification
        segment.UrbanRural = textInputValues["inp_urban_rural"];
        segment.ONRC = textInputValues["inp_onrc"];
        segment.RoadClass = frameworkModel.GetLookupValueText("road_class", segment.ONRC);

        // Traffic. The modelled ADT is a parameter (it grows each period) and is read below. The
        // SURVEYED ADT comes from the raw input every period, which is exactly what makes it frozen:
        // it is the measurement, and the deterioration models must be evaluated on it rather than on
        // the grown value. See the RoadSegment property for what goes wrong otherwise.
        segment.SurveyedAverageDailyTraffic = numInputValues["inp_adt"];
        segment.HeavyVehiclePercentage = numInputValues["inp_heavy_perc"];
        segment.TrafficGrowthPercent = numInputValues["inp_traff_growth_perc"];

        // Surfacing and pavement dates. Ages are parameters and are read below; the dates are kept because
        // they are what the ages were originally derived from.
        segment.NextSurface = textInputValues["inp_next_surf"];
        segment.SurfacingDateString = textInputValues["inp_surf_date"];
        segment.PavementDateString = textInputValues["inp_pave_date"];

        // Deflection - a static measure, not modelled forward
        segment.CentralDeflection = numInputValues["inp_lmd_d0_75th"];

        // High speed data survey date and the surveyed values, kept for reference. The modelled rut and IRI
        // are parameters and are read below.
        segment.SurveyDateString = textInputValues["inp_hsd_survey_date"];
        segment.RutMeanSurveyed = numInputValues["inp_rut_mean"];
        segment.IriSurveyed = numInputValues["inp_iri_mean"];

        // Historical maintenance. Static by definition - a treatment the model applies does not change what
        // was already done before the base date.
        segment.MaintPavementExtentLastYear = numInputValues["inp_maint_pa_ext"];
        segment.MaintPavementExtentLast3Years = numInputValues["inp_maint_pa_rept_ext"];
        segment.MaintPotholeExtentLastYear = numInputValues["inp_maint_poth_ext"];
        segment.MaintPotholeExtentLast3Years = numInputValues["inp_maint_poth_rept_ext"];
        segment.MaintSurfacingExtentLastYear = numInputValues["inp_maint_su_ext"];
        segment.MaintSurfacingExtentLast3Years = numInputValues["inp_maint_su_rept_ext"];

        // ---------------------------------------------------------------------------------------------
        // Evolving properties, from the model parameters. Worked in the order the parameters are declared
        // on the 'parameters' sheet, so that a missing one is easier to spot.
        // ---------------------------------------------------------------------------------------------

        segment.AverageDailyTraffic = numParamValues["par_adt"];
        // par_hcv is derived from AverageDailyTraffic and HeavyVehiclePercentage - do not assign it

        segment.PavementAge = numParamValues["par_pave_age"];
        segment.PavementRemainingLife = numParamValues["par_pave_remlife"];
        // par_pave_life_ach and par_hcv_risk are derived - do not assign them

        segment.SurfaceMaterial = textParamValues["par_surf_mat"];
        segment.SurfaceClass = textParamValues["par_surf_class"];
        segment.DeteriorationGroup = domainModel.Constants.GetDeteriorationGroup(segment.SurfaceClass);
        // par_surf_cs_flag, par_surf_cs_or_ac_flag and par_surf_road_type are derived - do not assign them
        segment.SurfaceThickness = numParamValues["par_surf_thick"];
        segment.SurfaceNumberOfLayers = numParamValues["par_surf_layers"];
        segment.SurfaceFunction = textParamValues["par_surf_func"];
        segment.SurfaceExpectedLife = numParamValues["par_surf_exp_life"];
        segment.SurfaceAge = numParamValues["par_surf_age"];
        // par_surf_life_ach and par_surf_remain_life are derived - do not assign them

        // Visual distresses
        segment.PctFlushing = numParamValues["par_flush_pct"];
        segment.PctCracking = numParamValues["par_crack_pct"];
        segment.PctRavelling = numParamValues["par_ravel_pct"];

        // Rutting and roughness. par_naasra is derived from Iri - do not assign it.
        segment.RutIncrement = numParamValues["par_rut_increm"];
        segment.RutParameterValue = numParamValues["par_rut"];
        segment.IriIncrement = numParamValues["par_iri_increm"];
        segment.Iri = numParamValues["par_iri"];

        // Treatment history. Setting the count also sets the IsTreated flag, so par_is_treated_flag is
        // derived and must not be assigned.
        segment.TreatmentCount = Convert.ToInt32(numParamValues["par_treat_count"]);

        // Deterioration model state. These are the draws made once at initialisation and carried for
        // the life of the surfacing. Miss one of these reads and the framework hands back zero: every
        // segment becomes exactly average, the run completes, and nothing says so.
        segment.RutDeviate = numParamValues["par_rut_z"];
        segment.IriDeviate = numParamValues["par_iri_z"];
        segment.RutGrowthMillimetres = numParamValues["par_rut_growth_mm"];
        segment.CrackOnsetPosition = numParamValues["par_crack_u_onset"];
        segment.CrackSeverityQuantile = numParamValues["par_crack_w_sev"];
        segment.CrackingBelowOnset = numParamValues["par_crack_below"];
        segment.CrackingInitSource = Convert.ToInt32(numParamValues["par_crack_init_src"]);
        segment.RutInitSource = Convert.ToInt32(numParamValues["par_rut_init_src"]);
        segment.IriInitSource = Convert.ToInt32(numParamValues["par_iri_init_src"]);
        segment.HasBeenRehabilitated = numParamValues["par_rehab_flag"] == 1;

        // The pre-repair credits, and the clock they decay on. Same warning as above and it bites
        // harder here: miss one of these and the credit silently vanishes the period after the repair,
        // so a pre-repair would improve the segment for exactly one year and then undo itself.
        segment.PreRepairCreditCracking = numParamValues["par_prerep_dz_crack"];
        segment.PreRepairCreditRut = numParamValues["par_prerep_dz_rut"];
        segment.PreRepairCreditIri = numParamValues["par_prerep_dz_iri"];
        segment.PreRepairYears = numParamValues["par_prerep_yrs"];

        // STAGE 4 (treatments trigger): par_csl_status and par_csl_flag carry the candidate selection
        // outcome from the previous period and are not read back yet. Neither are the PDI, SDI, objective
        // value and rank parameters the jFunction-era model carried - the bundle declares none of them, and
        // its 'network_functions' sheet is empty, so there is nothing to read. Settle what the trigger
        // needs, declare it in the bundle, then read it here.

        return segment;
    }

}
