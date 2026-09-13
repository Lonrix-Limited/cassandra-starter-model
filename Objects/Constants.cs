
namespace StarterModel.Objects;

/// <summary>
/// General model constants set up from model Lookup sets.
/// </summary>
public class Constants
{
    private DateTime _baseDate; 
    private int _shortTermPeriod;

    // Related to Candidate Selection
    private double _minSlaToTreatAc;
    private double _minSlaToTreatCs;    
    private double _min_periods_to_next_treat;
    private double _min_sdi_to_treat;
    private double _min_pdi_to_treat;


    private double _potholeBoostFactor;

    private double _maintenanceCostCalibrationFactor;
    private double _maintenanceCostPDIThreshold;

    // Related to TSS (Treatment Suitability Scores - MCDA)
    private double _rehabExcessRutThresh;
    private double _rehabExcessRutFact;
    private double _rehabPdiRank;
    private double _holdingPdiRankPt1;
    private double _holdingPdiRankPt2;
    private double _holdingPdiRankPt3;
    private double _holdingMaxRut;

    private double _preserveSdiRank;

    private double _preserveMaxPdiChipSeal;
    private double _preserveMaxPdiAC;
    private double _holdingMaxPdiAC;

    private double _preserveMaxRut;    
    private double _preserveMinSla;           

    // Related to MCDA Treatment Triggering
    private double _maxSlaForACHeavyMaint;
    private int _minPeriodsBetweenACHeavyMaint;

    // Related to the deterioration models
    private double _detAgeOffset;
    private double _detAgeCapYears;
    private double _detCrackOnsetPercent;
    private double _detRutChipSealGrowthPerYear;
    private double _detMultiplierClampSd;

    private double _detFeedbackCrackOnRut;
    private double _detFeedbackCrackOnIri;
    private double _detFeedbackRutOnIri;

    private double _ruleOnsetLifeFraction;
    private double _ruleSurfaceLifeDefaultYears;
    private double _ruleFlushingRatePerYear;
    private double _ruleRavellingRatePerYear;
    private double _ruleTrafficFactor;

    private double _rehabResetCrackingPercent;
    private double _rehabResetRutMillimetres;
    private double _rehabResetIri;

    private Dictionary<string, object> _surfaceClassGroups = new Dictionary<string, object>();

    // Group-keyed deterioration numbers, unpacked once at setup so that a missing key stops the run
    // there rather than part way through a period.
    private Dictionary<string, double> _rehabResetDeflection = new Dictionary<string, double>();
    private Dictionary<string, double> _rehabOffsetRut = new Dictionary<string, double>();
    private Dictionary<string, double> _rehabOffsetIri = new Dictionary<string, double>();
    private Dictionary<string, double> _rehabOffsetCrackOnset = new Dictionary<string, double>();
    private Dictionary<string, double> _staleSurveyResurfacingRut = new Dictionary<string, double>();
    private Dictionary<string, double> _staleSurveyResurfacingIri = new Dictionary<string, double>();


    /// <summary>
    /// Base date for the model run. Maps to lookup set "gernal" and setting key "base_date".
    /// </summary>
    public DateTime BaseDate { get { return _baseDate; } }

    #region Candidate Selection related constants

    /// <summary>
    /// Number of modelling periods considered short term for purposes of trigger adjustment. Used in Candidate Selection.
    /// </summary>
    public int CSShortTermPeriod
    {
        get { return _shortTermPeriod; }     
    }
    
    /// <summary>
    /// Minimum Surface Life Achieved to consider for AC - gatekeeper that can be used to throttle treatments
    /// </summary>
    public double CSMinSlaToTreatAc
    {
        get { return _minSlaToTreatAc; }
    }

    /// <summary>
    /// Minimum periods to next treatment (i.e. do not consider treatment if periods to a committed future treatment is less than this)
    /// </summary>
    public double CSMinPeriodsToNextTreat
    {
        get { return _min_periods_to_next_treat; }
    }


    /// <summary>
    /// Minimum Surface Life Achieved to consider for Chipseals - gatekeeper that can be used to throttle treatments
    /// </summary>
    public double CSMinSlaToTreatCs
    {
        get { return _minSlaToTreatCs; }
    }

    /// <summary>
    /// Minimum Surface Distress Index (SDI) to consider for treatment (EITHER condition applied with minimum PDI)
    /// </summary>
    public double CSMinSDIToTreat
    {
        get { return _min_sdi_to_treat; }       
    }

    /// <summary>
    /// Minimum Pavemenbt Distress Index (PDI) to consider for treatment.  (EITHER condition applied with minimum SDI)    
    /// </summary>
    public double CSMinPDIToTreat
    {
        get { return _min_pdi_to_treat; }
    }
    
    #endregion

    /// <summary>
    /// Boosting factor for pothole area to bring it to scale with other distresses
    /// </summary>
    public double PotholeBoostFactor
    {
        get { return _potholeBoostFactor; }
    }

    /// <summary>
    /// Calibration factor for maintenance cost
    /// TODO: Discussion with D&K
    /// </summary>
    public double MaintenanceCostCalibrationFactor
    {
        get { return _maintenanceCostCalibrationFactor; }     
    }

    /// <summary>
    /// Maintenance PDI threshold (force maintenance cost to zero if PDI is below this value)
    /// </summary>
    public double MaintenanceCostPDIThreshold
    {
        get { return _maintenanceCostPDIThreshold; }     
    }

    /// <summary>
    /// Rut threshold above which a penalty(for Holding Actions) or boost(for Rehabs) is applied(see below)
    /// </summary>
    public double TSSRehabExcessRutThresh
    {
        get { return _rehabExcessRutThresh; }
    }

    /// <summary>
    /// Multiply excessive rut with this value to get the boost for Rehab TSS based on excessive rut(if any)
    /// </summary>
    public double TSSRehabExcessRutFact
    {
        get { return _rehabExcessRutFact; }
    }

    /// <summary>
    /// PDI rank below which TSS score for Rehab becomes zero (i.e. no rehab if PDI is below this value)
    /// </summary>
    public double TSSRehabPdiRank
    {
        get { return _rehabPdiRank; }
    }

    /// <summary>
    /// PDI rank below which TSS score for Holding Action becomes zero (i.e. no holding action if PDI is below this value)
    /// </summary>
    public double TSSHoldingPdiRankPt1
    {
        get { return _holdingPdiRankPt1; }
    }


    /// <summary>
    /// PDI rank at which score for holding action is maximal(100)
    /// </summary>
    public double TSSHoldingPdiRankPt2
    {
        get { return _holdingPdiRankPt2; }
    }
    
    /// <summary>
    /// TSS for holding action based on PDI when PDI rank is 100
    /// </summary>
    public double TSSHoldingPdiRankPt3
    {
        get { return _holdingPdiRankPt3; }
    }

    /// <summary>
    /// Do not consider holding action if rut is above this value (unless it is not a rehab route in which case it is ignored)
    /// </summary>
    public double TSSHoldingMaxRut
    {
        get { return _holdingMaxRut; }
    }

    
    /// <summary>
    /// SDI Rank below which score for Preservation becomes zero (we want to apply preservation where there is some surface distress)
    /// </summary>
    public double TSSPreserveSdiRank
    {
        get { return _preserveSdiRank; }
    }
        
    /// <summary>
    /// Do not consider Preservation ChipSeal treatment if PDI is above this value 
    /// </summary>
    public double TSSPreserveMaxPdiChipseal
    {
        get { return _preserveMaxPdiChipSeal; }
    }

    /// <summary>
    /// Do not consider Preservation AC treatment if PDI is above this value 
    /// </summary>
    public double TSSPreserveMaxPdiAC
    {
        get { return _preserveMaxPdiAC; }
    }

    /// <summary>
    /// Do not consider Holding AC treatment if PDI is above this value 
    /// </summary>
    public double TSSHoldingMaxPdiAC
    {
        get { return _holdingMaxPdiAC; }
    }

    /// <summary>
    /// Do not consider preservation if rut is above this value
    /// </summary>
    public double TSSPreserveMaxRut
    {
        get { return _preserveMaxRut; }
    }

    /// <summary>
    /// Do not consider preservation if Surface Life Achieved % is below this value
    /// </summary>
    public double TSSPreserveMinSla
    {
        get { return _preserveMinSla; }
    }

    /// <summary>
    /// Maximum Surface Life Achieved % to consider AC Heavy Maintenance (i.e. do not consider AC Heavy Maintenance if SLA is above this value)
    /// </summary>
    public double MaxSlaForACHeavyMaint
    {
        get { return _maxSlaForACHeavyMaint; }
    }

    /// <summary>
    /// Minimum number of periods between AC Heavy Maintenance treatment and any other previous treatment (excluding Routine Maintenance)
    /// </summary>
    public int MinPeriodsBetweenACHeavyMaint
    {
        get { return _minPeriodsBetweenACHeavyMaint; }
    }

    #region Deterioration model constants

    // These are the single tunable numbers behind the deterioration models. The FITTED coefficients
    // that go with them are a different kind of thing - regenerated as a whole set by a refit - and
    // live in CSV files in the client's 'supporting' folder, loaded by DeteriorationCoefficients.

    /// <summary>
    /// Years added to surface age inside every log(age) term. Comes from the regression fit, so
    /// changing it invalidates the coefficients rather than recalibrating them.
    /// </summary>
    public double DetAgeOffset { get { return _detAgeOffset; } }

    /// <summary>
    /// Surface age is capped at this value inside every log(age) term. Beyond it the fit has too
    /// little data to stand on, and an untreated segment would be extrapolated far outside the evidence.
    /// </summary>
    public double DetAgeCapYears { get { return _detAgeCapYears; } }

    /// <summary>
    /// Cracking percentage marking the onset regime change. Below it a segment shows no age trend.
    /// </summary>
    public double DetCrackOnsetPercent { get { return _detCrackOnsetPercent; } }

    /// <summary>
    /// JUDGEMENT, not fitted. Rut growth in mm added to chipseal for each year since surfacing,
    /// because surface age is not the rut clock for chipseal. Report it as judgement wherever it appears.
    /// </summary>
    public double DetRutChipSealGrowthPerYear { get { return _detRutChipSealGrowthPerYear; } }

    /// <summary>
    /// Each segment's persistent deviate is clamped to plus or minus this many standard deviations,
    /// so a single extreme reading cannot lock a segment into fast deterioration for the whole run.
    /// </summary>
    public double DetMultiplierClampSd { get { return _detMultiplierClampSd; } }

    /// <summary>Effect of cracking on rutting. Deliberately not fitted; zero means off.</summary>
    public double DetFeedbackCrackOnRut { get { return _detFeedbackCrackOnRut; } }

    /// <summary>Effect of cracking on roughness. Deliberately not fitted; zero means off.</summary>
    public double DetFeedbackCrackOnIri { get { return _detFeedbackCrackOnIri; } }

    /// <summary>Effect of rutting on roughness. Deliberately not fitted; zero means off.</summary>
    public double DetFeedbackRutOnIri { get { return _detFeedbackRutOnIri; } }

    /// <summary>
    /// Fraction of the surface expected life at which flushing and ravelling begin. Judgement.
    /// </summary>
    public double RuleOnsetLifeFraction { get { return _ruleOnsetLifeFraction; } }

    /// <summary>
    /// Surface expected life used where the input value is zero, so that a missing life does not put
    /// the onset age at zero and start a new surface distressed.
    /// </summary>
    public double RuleSurfaceLifeDefaultYears { get { return _ruleSurfaceLifeDefaultYears; } }

    /// <summary>Flushing percentage added per year once onset is reached. Judgement, not fitted.</summary>
    public double RuleFlushingRatePerYear { get { return _ruleFlushingRatePerYear; } }

    /// <summary>Ravelling percentage added per year once onset is reached. Judgement, not fitted.</summary>
    public double RuleRavellingRatePerYear { get { return _ruleRavellingRatePerYear; } }

    /// <summary>
    /// Multiplier on the flushing and ravelling growth rate. 1.0 is neutral; a traffic-dependent
    /// adjustment is expected to replace it.
    /// </summary>
    public double RuleTrafficFactor { get { return _ruleTrafficFactor; } }

    /// <summary>
    /// Maps a surface class onto one of the two groups the deterioration models were fitted on.
    /// <para>The set is kept whole rather than unpacked into properties, so that a new surface class
    /// needs a row in lookups.xlsx and nothing in C#. Anything not named in the set falls back to the
    /// 'default' key, which is how blocks, concrete and other surfaces are placed.</para>
    /// </summary>
    /// <param name="surfaceClass">Surface class of the segment, lower case</param>
    public string GetDeteriorationGroup(string surfaceClass)
    {
        string key = surfaceClass ?? string.Empty;
        if (_surfaceClassGroups.ContainsKey(key))
        {
            return Convert.ToString(_surfaceClassGroups[key])!.Trim().ToLower();
        }

        if (!_surfaceClassGroups.ContainsKey(DefaultKey))
        {
            throw new Exception($"Lookup set '{SurfaceClassGroupSet}' in lookups.xlsx has no entry for surface " +
                                $"class '{surfaceClass}' and no '{DefaultKey}' key to fall back on. Add one or " +
                                $"the other: without it, a surface class nobody anticipated stops the run.");
        }

        return Convert.ToString(_surfaceClassGroups[DefaultKey])!.Trim().ToLower();
    }

    #endregion

    #region Treatment reset values

    // WHY THESE ARE LOOKUPS AND NOT COEFFICIENTS. Every number in this region is a modelling decision
    // taken from the as-new condition the engineer specified, not a fitted result. A refit regenerates
    // the CSV files in the 'supporting' folder as a whole set; it does not touch these, and they must
    // stay where a modeller can change one of them without a rebuild.

    /// <summary>
    /// Cracking percentage a rehabilitation resets to. Zero - the pavement is new, so there is nothing
    /// left underneath for cracking to reflect through.
    /// </summary>
    public double RehabResetCrackingPercent { get { return _rehabResetCrackingPercent; } }

    /// <summary>
    /// Rut depth in mm a rehabilitation resets to. JUDGEMENT, not fitted: it is this network's own
    /// asphalt 'as new' baseline.
    /// </summary>
    public double RehabResetRutMillimetres { get { return _rehabResetRutMillimetres; } }

    /// <summary>
    /// IRI a rehabilitation resets to. JUDGEMENT, not fitted.
    /// </summary>
    public double RehabResetIri { get { return _rehabResetIri; } }

    /// <summary>
    /// Central deflection a rehabilitation resets a segment of this group to - the group median for a
    /// new pavement. A domain rule: the survey data contains no reconstructed pavement to measure.
    /// </summary>
    public double GetRehabResetDeflection(string deteriorationGroup)
    {
        return GetForGroup(_rehabResetDeflection, deteriorationGroup, RehabResetSet, "d0");
    }

    /// <summary>
    /// Permanent offset added to the log rut level for the rest of a rehabilitated segment's life.
    ///
    /// <para>WITHOUT IT THE RESET DOES NOT HOLD. Every segment the models were fitted on is a surfacing
    /// over an old pavement - median pavement age 63 years, and not one reconstructed pavement in the
    /// file - so the model's prediction at surface age zero means "a fresh surface on a sixty-year-old
    /// pavement", not "a new road". Set a rehabilitated segment to 2.0 mm, recompute the next period,
    /// and the model snaps it straight back to 3.66 mm.</para>
    ///
    /// <para>Each offset is min(0, log(as-new target) - median log level at surface age zero). The floor
    /// at zero is not decoration: asphalt rutting already predicts better than its as-new target, so an
    /// unfloored offset would make a full reconstruction WORSE than a reseal.</para>
    /// </summary>
    public double GetRehabOffsetRut(string deteriorationGroup)
    {
        return GetForGroup(_rehabOffsetRut, deteriorationGroup, RehabOffsetSet, "rut");
    }

    /// <summary>
    /// Permanent offset added to the log IRI level for the rest of a rehabilitated segment's life. Same
    /// reasoning as the rutting offset: without it a rebuilt segment snaps back to IRI 4.44 on asphalt
    /// and 5.72 on chipseal the period after it was rebuilt.
    /// </summary>
    public double GetRehabOffsetIri(string deteriorationGroup)
    {
        return GetForGroup(_rehabOffsetIri, deteriorationGroup, RehabOffsetSet, "iri");
    }

    /// <summary>
    /// Permanent offset added to the cracking ONSET model, in log-odds, for the rest of a rehabilitated
    /// segment's life. ZERO by default, which is the behaviour as delivered.
    ///
    /// <para>THIS ONE IS NOT DERIVED, AND THAT IS WHY IT DEFAULTS TO OFF. The four level-model offsets
    /// follow from the as-new condition the engineer set - 2.0 mm of rut, IRI 2.5 - and there is no
    /// as-new CRACKING level to derive a fifth from, because cracking has no as-new value: a new road
    /// has none. So this is pure engineering judgement and belongs to the engineer, not to the code.</para>
    ///
    /// <para>WHAT IT IS FOR. At zero, 40% of rebuilt asphalt segments and 26% of rebuilt chipseal are
    /// cracked again one year after reconstruction. That is the fitted onset model doing what it was
    /// fitted to do - it was built on surfacings over sixty-year-old pavements, where reflective
    /// cracking comes through fast - and it is too fast for a road that has just been rebuilt. A
    /// negative value here delays it. The lookup comment carries the figures for a range of values.</para>
    /// </summary>
    public double GetRehabOffsetCrackOnset(string deteriorationGroup)
    {
        return GetForGroup(_rehabOffsetCrackOnset, deteriorationGroup, RehabOffsetSet, "crack_onset");
    }

    /// <summary>
    /// Factor applied to a surveyed rut depth when the segment was RESURFACED after that survey, so
    /// that year zero describes the surface the segment actually has.
    /// <para>The chipseal factor is 1.0 on purpose - a seal follows the shape of what it is laid on and
    /// inherits the rut rather than renewing it, which this network's data shows directly.</para>
    /// </summary>
    public double GetStaleSurveyResurfacingFactorRut(string deteriorationGroup)
    {
        return GetForGroup(_staleSurveyResurfacingRut, deteriorationGroup, StaleSurveyResurfacingSet, "rut");
    }

    /// <summary>
    /// Factor applied to a surveyed IRI when the segment was RESURFACED after that survey. Roughness
    /// renews on both classes - strongly on asphalt, weakly on chipseal.
    /// </summary>
    public double GetStaleSurveyResurfacingFactorIri(string deteriorationGroup)
    {
        return GetForGroup(_staleSurveyResurfacingIri, deteriorationGroup, StaleSurveyResurfacingSet, "iri");
    }

    /// <summary>
    /// Reads one group-keyed value, naming the group, the set and the key it was looking for. The
    /// dictionaries are filled at setup, so this only fires if a segment resolves to a group that the
    /// 'surf_class_group' lookup set names but these sets do not.
    /// </summary>
    private static double GetForGroup(Dictionary<string, double> valuesByGroup, string deteriorationGroup,
                                      string setName, string quantity)
    {
        if (valuesByGroup.TryGetValue(deteriorationGroup, out double value))
        {
            return value;
        }

        throw new Exception($"Lookup set '{setName}' in lookups.xlsx has no '{quantity}_{deteriorationGroup}' " +
                            $"row, so there is no value for deterioration group '{deteriorationGroup}'. Every " +
                            $"group named in the 'surf_class_group' set needs one.");
    }

    #endregion

    #region Guarded lookup readers

    private const string DeteriorationSet = "deterioration";
    private const string FeedbackSet = "deterioration_feedback";
    private const string RuleDistressSet = "rule_distress";
    private const string SurfaceClassGroupSet = "surf_class_group";
    private const string RehabResetSet = "rehab_resets";
    private const string RehabOffsetSet = "rehab_offsets";
    private const string StaleSurveyResurfacingSet = "stale_survey_resurf";
    private const string DefaultKey = "default";

    /// <summary>
    /// Reads one numeric lookup value, naming the set and the key if it is absent. Without the guard a
    /// spreadsheet typo surfaces as a KeyNotFoundException naming nothing at all.
    /// </summary>
    private static double GetNumber(Dictionary<string, Dictionary<string, object>> lookupSets, string setName, string key)
    {
        if (!lookupSets.ContainsKey(setName))
        {
            throw new Exception($"Lookup set '{setName}' is not in lookups.xlsx. The deterioration models " +
                                $"cannot start without it.");
        }
        if (!lookupSets[setName].ContainsKey(key))
        {
            throw new Exception($"'{key}' has no value in lookup set '{setName}' in lookups.xlsx.");
        }

        // Convert, never cast: setting_value arrives as text whatever the cell looks like in Excel, and
        // a cast throws an InvalidCastException that mentions nothing about spreadsheets.
        return Convert.ToDouble(lookupSets[setName][key]);
    }

    /// <summary>
    /// Returns a whole lookup set, naming it if it is absent.
    /// </summary>
    private static Dictionary<string, object> GetSet(Dictionary<string, Dictionary<string, object>> lookupSets, string setName)
    {
        if (!lookupSets.ContainsKey(setName))
        {
            throw new Exception($"Lookup set '{setName}' is not in lookups.xlsx.");
        }
        return lookupSets[setName];
    }

    #endregion

    public Constants(Dictionary<string, Dictionary<string, object>> lookupSets)
    {
        _baseDate = JCass_Core.Utils.HelperMethods.ParseISODateNoTime(lookupSets["general"]["base_date"].ToString()!);
        _shortTermPeriod = Convert.ToInt32(lookupSets["general"]["short_term_periods"]);

        // Candidate Selection related constants
        _min_periods_to_next_treat = Convert.ToInt32(lookupSets["candidate_selection"]["min_periods_to_next_treat"]);
        _min_sdi_to_treat = Convert.ToDouble(lookupSets["candidate_selection"]["min_sdi_to_treat"]);
        _min_pdi_to_treat = Convert.ToDouble(lookupSets["candidate_selection"]["min_pdi_to_treat"]);
        _minSlaToTreatAc = Convert.ToDouble(lookupSets["candidate_selection"]["min_sla_to_treat_ac"]);
        _minSlaToTreatCs = Convert.ToDouble(lookupSets["candidate_selection"]["min_sla_to_treat_cs"]);
        
        _potholeBoostFactor = Convert.ToDouble(lookupSets["distress"]["poth_booster"]);

        _maintenanceCostCalibrationFactor = Convert.ToDouble(lookupSets["maint_pred"]["cal_maint_pred"]);
        _maintenanceCostPDIThreshold = Convert.ToDouble(lookupSets["maint_pred"]["maint_pdi_threshold"]);

        // Related to TSS
        _rehabExcessRutThresh = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["rehab_excess_rut_thresh"]);
        _rehabExcessRutFact = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["rehab_excess_rut_fact"]);
        _rehabPdiRank = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["rehab_pdi_rank"]);
        _holdingPdiRankPt1 = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["holding_pdi_rank_pt1"]);
        _holdingPdiRankPt2 = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["holding_pdi_rank_pt2"]);
        _holdingPdiRankPt3 = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["holding_pdi_rank_pt3"]);
        _holdingMaxRut = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["holding_max_rut"]);
        _preserveSdiRank = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["preserve_sdi_rank"]);
        
        _preserveMaxPdiChipSeal = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["preserve_max_pdi_chipseal"]);
        _preserveMaxPdiAC = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["preserve_max_pdi_ac"]);
        _holdingMaxPdiAC = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["holding_max_pdi_ac"]);

        _preserveMaxRut = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["preserve_max_rut"]);
        _preserveMinSla = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["preserve_min_sla"]);

        // Related to MCDA Treatment Triggering
        _maxSlaForACHeavyMaint = Convert.ToDouble(lookupSets["mcda_treatment_triggering"]["ac_hmaint_maximum_sla"]);
        _minPeriodsBetweenACHeavyMaint = Convert.ToInt32(lookupSets["mcda_treatment_triggering"]["ac_hmaint_min_periods_between"]);

        // Related to the deterioration models. Each of these is read through a guard that names the set
        // and the key, so that a spreadsheet typo is a message a modeller can act on, reported at setup
        // before a single period is modelled.
        _detAgeOffset = GetNumber(lookupSets, DeteriorationSet, "age_offset");
        _detAgeCapYears = GetNumber(lookupSets, DeteriorationSet, "age_cap_years");
        _detCrackOnsetPercent = GetNumber(lookupSets, DeteriorationSet, "crack_onset_pct");
        _detRutChipSealGrowthPerYear = GetNumber(lookupSets, DeteriorationSet, "rut_cs_growth_mm_per_year");
        _detMultiplierClampSd = GetNumber(lookupSets, DeteriorationSet, "multiplier_clamp_sd");

        _detFeedbackCrackOnRut = GetNumber(lookupSets, FeedbackSet, "crack_on_rut");
        _detFeedbackCrackOnIri = GetNumber(lookupSets, FeedbackSet, "crack_on_iri");
        _detFeedbackRutOnIri = GetNumber(lookupSets, FeedbackSet, "rut_on_iri");

        _ruleOnsetLifeFraction = GetNumber(lookupSets, RuleDistressSet, "onset_life_fraction");
        _ruleSurfaceLifeDefaultYears = GetNumber(lookupSets, RuleDistressSet, "surf_life_default_years");
        _ruleFlushingRatePerYear = GetNumber(lookupSets, RuleDistressSet, "rate_flushing_pct_per_year");
        _ruleRavellingRatePerYear = GetNumber(lookupSets, RuleDistressSet, "rate_ravelling_pct_per_year");
        _ruleTrafficFactor = GetNumber(lookupSets, RuleDistressSet, "traffic_factor");

        _surfaceClassGroups = GetSet(lookupSets, SurfaceClassGroupSet);

        // The treatment reset values, and the three group-keyed sets that go with them. Unpacked per
        // group here rather than read on demand, so that a missing row stops the run at setup instead
        // of part way through the first period that happens to treat a segment of that group.
        _rehabResetCrackingPercent = GetNumber(lookupSets, RehabResetSet, "crack_pct");
        _rehabResetRutMillimetres = GetNumber(lookupSets, RehabResetSet, "rut_mm");
        _rehabResetIri = GetNumber(lookupSets, RehabResetSet, "iri");

        foreach (string group in DeteriorationModels.ModelGroups)
        {
            _rehabResetDeflection[group] = GetNumber(lookupSets, RehabResetSet, $"d0_{group}");
            _rehabOffsetRut[group] = GetNumber(lookupSets, RehabOffsetSet, $"rut_{group}");
            _rehabOffsetIri[group] = GetNumber(lookupSets, RehabOffsetSet, $"iri_{group}");
            _rehabOffsetCrackOnset[group] = GetNumber(lookupSets, RehabOffsetSet, $"crack_onset_{group}");
            _staleSurveyResurfacingRut[group] = GetNumber(lookupSets, StaleSurveyResurfacingSet, $"rut_{group}");
            _staleSurveyResurfacingIri[group] = GetNumber(lookupSets, StaleSurveyResurfacingSet, $"iri_{group}");
        }

    }




}
