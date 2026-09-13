
using JCass_Core.JFunctions;
using JCass_ModelCore.Models;
using JCass_ModelCore.Treatments;


namespace StarterModel.Objects;

/// <summary>
/// Class for checking treatments triggering 
/// </summary>
public class TreatmentsTrigger
{
    /// <summary>
    /// The surface class that takes the OGPA family of treatment names. See the region "Which family
    /// of treatment names this segment takes" for why it is the CURRENT class that decides.
    /// </summary>
    private const string OgpaSurfaceClass = "ogpa";

    /// <summary>
    /// The unit rate passed for a holding treatment, whose cost is carried in the quantity rather
    /// than in the rate. Its row in 'unit_rates_general' reads 'N/A' on purpose and must never be
    /// read as a number.
    /// </summary>
    private const double HoldingTreatmentUnitRate = 1.0;

    private ModelBase _frameworkModel;
    private StarterModel _domainModel;

    public TreatmentsTrigger(ModelBase frameworkModel, StarterModel domainModel)
    {
        _frameworkModel = frameworkModel ?? throw new ArgumentNullException(nameof(frameworkModel), "Domain model cannot be null");
        _domainModel = domainModel ?? throw new ArgumentNullException(nameof(domainModel), "Domain model cannot be null");
    }

    public List<TreatmentInstance> GetTriggeredTreatments(RoadSegment segment, int period, Dictionary<string, object> infoFromModel)
    {
        List<TreatmentInstance> triggeredTreatments = new List<TreatmentInstance>();

        // Check if the segment passes the Candidate Selection checks. If not, return an empty list.
        if (segment.IsCandidateForTreatment == 0) return triggeredTreatments;

        // Although we check if Periods to Next Treatment (i.e. committed) in the Candidate Selection, we need to do it 
        // again here, because the Candidate Selection result was last evaluated at the last epoch, while the periods to
        // next treatment have now changed since the period has changed
        int periodsToNextTreatment = Convert.ToInt32(infoFromModel["periods_to_next_treatment"]);
        if (periodsToNextTreatment <= 6) { return triggeredTreatments; }        
        
        // Check if second coat after Rehabilitation should be added. If so, since we are forcing it, do not look
        // for other candidate treatments
        this.AddSecondCoatIfValid(segment, period, triggeredTreatments);
        if (triggeredTreatments.Count > 0) return triggeredTreatments;

        // Check if a second coat after Preseal Repairs should be added, If so, since we are forcing it, do not look
        // for other candidate treatments
        this.AddHoldingFollowUpChipsealIfValid(segment, period, triggeredTreatments);
        if (triggeredTreatments.Count > 0) return triggeredTreatments;

        // Check if a birthday treatment should be added. If so, since we are forcing it, do not look for other candidate treatments
        this.AddBirthdayTreatmentBlocksOrConcreteIfValid(segment, period, triggeredTreatments);
        if (triggeredTreatments.Count > 0) return triggeredTreatments;

        //---------------------------------------------------------------------------------------------------------------------------------
        //      If we get here, we know that no second coats or birthday treatments are added.
        //      Now find candidate treatments to add to the optimisation stage
        //---------------------------------------------------------------------------------------------------------------------------------

        this.AddPreservationChipsealIfValid(segment, period, triggeredTreatments);        
        this.AddPresealOnChipsealIfValid(segment, period, triggeredTreatments);

        this.AddPreservationAcOrOgpaIfValid(segment, period, triggeredTreatments);
        this.AddHoldingAcOrOgpaIfValid(segment, period, triggeredTreatments);
        this.AddHeavyMaintenanceAcOrOgpaIfValid(segment, period, triggeredTreatments, infoFromModel);

        this.AddRehabilitationIfValid(segment, period, triggeredTreatments);

        //---------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------

        return triggeredTreatments;
    }

    #region Preliminary checks for treatments
        
    
    private bool CanDoRehabilitationOnChipSeal(RoadSegment segment)
    {
        //n : para_csl_flag = 1 AND n : pcal_can_rehab_flag = 1 AND n : pcal_next_surf_cs_flag = 1 AND n : periods_to_next_treatment > 6

        //Note: Check for 'periods_to_next_treatment > 6' is done in CandidateSelector.EvaluateCandidate method, so we do not need to check it here again.
        
        if (segment.CanRehabFlag == false) return false; // If the segment cannot be rehabilitated, do not add a treatment

        // If next surface is not intended to be ChipSeal, do not add a treatment
        if (segment.NextSurfaceIsChipSeal == false) return false;

        // Do not add a treatment if the current surface is not ChipSeal
        // ToDo: needs discussion. May be cases where current surfacing is AC
        if (segment.SurfaceIsChipSealFlag == 0) return false;
        
        return true;

    }

    private bool CanDoRehabilitationOnAsphalt(RoadSegment segment)
    {
        //n : para_csl_flag = 1 AND n : pcal_can_rehab_flag = 1 AND n : pcal_next_surf_ac_flag = 1 AND n : periods_to_next_treatment > 6

        //Note: Check for 'periods_to_next_treatment > 6' is done in CandidateSelector.EvaluateCandidate method, so we do not need to check it here again.

        if (segment.CanRehabFlag == false) return false; // If the segment cannot be rehabilitated, do not add a treatment

        // If next surface is not intended to be AC, do not consider
        if (segment.NextSurface != "ac") return false;

        // Only valid if surface is asphalt
        // ToDo: needs discussion. May be cases where current surfacing is AC
        if (segment.SurfaceIsChipSealFlag == 1) return false;

        return true;

    }


    #endregion

    #region Which family of treatment names this segment takes

    // THE ROUTE THROUGH THIS CLASS IS CHOSEN BY THE NEXT SURFACE; THE NAME IS CHOSEN BY THE CURRENT
    // SURFACE CLASS. The two are different questions and only one of them can answer for OGPA.
    //
    // 'inp_next_surf' carries only cs, ac, blocks, concrete and other - the default arm of
    // AddBirthdayTreatmentBlocksOrConcreteIfValid is the proof, since it treats everything that is not
    // blocks, concrete or other as chipseal or asphalt. So an OGPA road's next surface reads 'ac', and
    // the segment's current surface class is the only place in the model where OGPA exists at all.
    //
    // One consequence, and it is deliberate rather than overlooked: an OGPA segment stays OGPA for the
    // whole run, because the Resetter writes the class straight back from 'treat_surf_class'. There is
    // no way to say "this OGPA road is rehabilitated as plain asphalt". That needs an input column of
    // its own and is a separate change.

    /// <summary>
    /// The resurfacing treatment for a segment on the asphalt route - the route taken when the next
    /// surface is not chipseal. 'slurry' takes the asphalt names, as it does everywhere else in the
    /// setup.
    /// </summary>
    private static string ResurfacingNameForAsphaltRoute(RoadSegment segment)
    {
        return segment.SurfaceClass == OgpaSurfaceClass
            ? TreatmentNames.OgpaResurfacing
            : TreatmentNames.AsphaltResurfacing;
    }

    /// <summary>The overlay-with-repairs treatment for a segment on the asphalt route.</summary>
    private static string HoldingNameForAsphaltRoute(RoadSegment segment)
    {
        return segment.SurfaceClass == OgpaSurfaceClass
            ? TreatmentNames.OgpaHolding
            : TreatmentNames.AsphaltHolding;
    }

    /// <summary>The repairs-alone treatment for a segment on the asphalt route.</summary>
    private static string HeavyMaintenanceNameForAsphaltRoute(RoadSegment segment)
    {
        return segment.SurfaceClass == OgpaSurfaceClass
            ? TreatmentNames.OgpaHeavyMaintenance
            : TreatmentNames.AsphaltHeavyMaintenance;
    }

    /// <summary>
    /// The rehabilitation treatment for this segment. Chipseal is decided by the next surface because
    /// a rehabilitation may change the surfacing type; asphalt and OGPA are separated by the current
    /// class, for the reason at the top of this region.
    /// </summary>
    private static string RehabilitationName(RoadSegment segment)
    {
        if (segment.NextSurfaceIsChipSeal) return TreatmentNames.ChipsealRehabilitation;

        return segment.SurfaceClass == OgpaSurfaceClass
            ? TreatmentNames.OgpaRehabilitation
            : TreatmentNames.AsphaltRehabilitation;
    }

    #endregion

    private void AddBirthdayTreatmentBlocksOrConcreteIfValid(RoadSegment segment, int iPeriod, List<TreatmentInstance> treatments)
    {
        //n : pcal_can_treat_flag = 1 AND n : pcal_next_surf_blocks_flag = 1 AND n : period >= file_earliest_treat_period AND n : para_surf_remain_life <= 1

        if (segment.CanTreatFlag == false) return; // If the segment cannot be treated, do not add a treatment
        string treatmentName = "";

        switch (segment.NextSurface)
        {
            case "blocks":
                treatmentName = TreatmentNames.BlockRepairs;
                break;
            case "concrete":
                treatmentName = TreatmentNames.ConcreteRepairs;
                break;
            case "other":
                treatmentName = TreatmentNames.OtherRepairs;
                break;
            default:
                //If we get here, it is ChipSeal or Asphalt, which are not valid for this treatment
                return;
        }

        if (segment.SurfaceRemainingLife > 1) return; // If the surface remaining life is greater than 1, do not add a treatment
        if (iPeriod < segment.EarliestTreatmentPeriod) return; // If the period is less than the earliest treatment period, do not add a treatment

        //If we get here, a birthday treatment is valid
        double quantity = segment.AreaSquareMetre;
        bool forceTreatment = true;

        double unitRate = _domainModel.Constants.GetUnitRate(treatmentName);

        TreatmentInstance treatment = new TreatmentInstance(segment.ElementIndex, treatmentName, iPeriod, quantity, unitRate, forceTreatment,  "Birthday treatment", "");
        treatment.TreatmentSuitabilityScore = 102; // Set a high suitability score for second coat treatments
        treatments.Add(treatment);

    }

    private void AddHoldingFollowUpChipsealIfValid(RoadSegment segment, int iPeriod, List<TreatmentInstance> treatments)
    {
        // Only add a holding follow-up treatment if current surface function has code '1a'
        if (segment.SurfaceFunction != "1a") return;

        if (segment.NextSurfaceIsChipSeal == false) return; // If the next surface is not ChipSeal, do not add a treatment

        string treatmentName = "";
        string reason = "";
        string comment = "";

        treatmentName = TreatmentNames.ChipsealSecondCoatAfterPreseal;
        reason = "Pre-seal follow-up";
        double quantity = segment.AreaSquareMetre;

        double unitRate = _domainModel.Constants.GetUnitRate(treatmentName);

        TreatmentInstance treatment = new TreatmentInstance(segment.ElementIndex, treatmentName, iPeriod, quantity, unitRate, true, reason, comment);
        treatment.TreatmentSuitabilityScore = 102;  //fixed high score to force this treatment to be selected if it is valid
        treatments.Add(treatment);
    }

    private void AddSecondCoatIfValid(RoadSegment segment, int iPeriod, List<TreatmentInstance> treatments)
    {
        // Only add a second coat if it is needed and the segment is a valid candidate
        if (segment.SecondCoatNeeded)
        {
            double quantity = segment.AreaSquareMetre;

            string treatmentName = TreatmentNames.ChipsealSecondCoatAfterRehab;

            double unitRate = _domainModel.Constants.GetUnitRate(treatmentName);

            TreatmentInstance treatment = new TreatmentInstance(segment.ElementIndex, treatmentName, iPeriod, quantity: quantity, unitRate: unitRate, 
                                                                true, "Second coat", "Second coat");
            treatment.TreatmentSuitabilityScore = 102; // Set a high suitability score for second coat treatments
            treatments.Add(treatment);
        }
    }

    private void AddPreservationChipsealIfValid(RoadSegment segment, int iPeriod, List<TreatmentInstance> treatments)
    {
        string treatmentName = TreatmentNames.ChipsealResurfacing;
        if (segment.NextSurfaceIsChipSeal == false) return;
        
        // If the rut depth is above the maximum threshold, do not add a treatment
        if (segment.RutParameterValue > _domainModel.Constants.TSSPreserveMaxRut) return;

        // If the surface life achieved is not greater than the minimum required, do not add a treatment
        if (segment.SurfaceAchievedLifePercent < _domainModel.Constants.TSSPreserveMinSla) return;

        // For preservation, if PDI is above the maximum threshold, do not add a treatment
        if (segment.PavementDistressIndex > _domainModel.Constants.TSSPreserveMaxPdiChipseal) return;

        double tssScore = TreatmentSuitabilityScorer.GetTSSForPreservationTreatment(segment, _domainModel, iPeriod);
        if (tssScore <= _frameworkModel.Configuration.MinimumTreatmentSuitabilityScoreAllowed) return; // If the TSS score is below the minimum allowed, do not add a treatment

        double sdi = segment.SurfaceDistressIndex;        
        string reason = $"SLA={Math.Round(segment.SurfaceAchievedLifePercent, 1)}";
        string comment = $"SDI={Math.Round(sdi, 1)}, TSS={Math.Round(tssScore, 2)}";

        double unitRate = _domainModel.Constants.GetUnitRate(treatmentName);

        double quantity = segment.AreaSquareMetre;
        TreatmentInstance treatment = new TreatmentInstance(segment.ElementIndex, treatmentName, iPeriod, quantity: quantity, unitRate: unitRate,
                                                            false, reason, comment);
        treatment.TreatmentSuitabilityScore = tssScore;
        treatments.Add(treatment);
    }

    /// <summary>
    /// Thin asphalt or OGPA resurfacing with no or minimal repairs. Which of the two it is named as
    /// follows the segment's CURRENT surface class - see the naming region above.
    /// </summary>
    private void AddPreservationAcOrOgpaIfValid(RoadSegment segment, int iPeriod, List<TreatmentInstance> treatments)
    {
        string treatmentName = ResurfacingNameForAsphaltRoute(segment);
        if (segment.NextSurfaceIsChipSeal == true) return;

        // If the rut depth is above the maximum threshold, do not add a treatment
        if (segment.RutParameterValue > _domainModel.Constants.TSSPreserveMaxRut) return;

        // If the surface life achieved is not greater than the minimum required, do not add a treatment
        if (segment.SurfaceAchievedLifePercent < _domainModel.Constants.TSSPreserveMinSla) return;

        // For preservation, if PDI is above the maximum threshold, do not add a treatment
        if (segment.PavementDistressIndex > _domainModel.Constants.TSSPreserveMaxPdiAC) return;

        // If asphalt overlay is not allowed because of too high deflection etc, do not add a treatment
        if (segment.AsphaltOkFlag == false) return; 

        double tssScore = TreatmentSuitabilityScorer.GetTSSForPreservationTreatment(segment, _domainModel, iPeriod);
        
        // If the TSS score is below the minimum allowed, do not add a treatment
        if (tssScore <= _frameworkModel.Configuration.MinimumTreatmentSuitabilityScoreAllowed) return; 

        double sdi = segment.SurfaceDistressIndex;
        string reason = $"SLA={Math.Round(segment.SurfaceAchievedLifePercent, 1)}";
        string comment = $"SDI={Math.Round(sdi, 1)}, TSS={Math.Round(tssScore, 2)}";
        
        double overlayQuantity = segment.AreaSquareMetre;

        double unitRate = _domainModel.Constants.GetUnitRate(treatmentName);

        TreatmentInstance treatment = new TreatmentInstance(segment.ElementIndex, treatmentName, iPeriod, quantity: overlayQuantity, unitRate:unitRate,
                                                            false, reason, comment);
        
        treatment.TreatmentSuitabilityScore = tssScore;
        treatments.Add(treatment);
    }

    /// <summary>
    /// An asphalt or OGPA inlay/overlay that INCLUDES heavy maintenance repairs, in the same year.
    /// Costed as the two pieces added together and carried in the quantity, then split back across the
    /// Resurfacing and Pre-Repairs budgets in the fractions that produced it.
    /// </summary>
    private void AddHoldingAcOrOgpaIfValid(RoadSegment segment, int iPeriod, List<TreatmentInstance> treatments)
    {
        string treatmentName = HoldingNameForAsphaltRoute(segment);
        if (segment.NextSurfaceIsChipSeal == true) return;

        // If the rut depth is above the maximum threshold, do not add a treatment
        if (segment.RutParameterValue > _domainModel.Constants.TSSPreserveMaxRut) return;

        // If the surface life achieved is not greater than the minimum required, do not add a treatment
        if (segment.SurfaceAchievedLifePercent < _domainModel.Constants.TSSPreserveMinSla) return;

        // For preservation, if PDI is above the maximum threshold, do not add a treatment
        if (segment.PavementDistressIndex > _domainModel.Constants.TSSHoldingMaxPdiAC) return;

        // For Holding AC, do not eliminate if asphalt overlay is not allowed (in 'segment.AsphaltOkFlag') because of too high deflection etc.
        // This is because this treatment is assumed to include strengthening repairs to adress weak areas

        double tssScore = TreatmentSuitabilityScorer.GetTSSForPreservationTreatment(segment, _domainModel, iPeriod);
        // If the TSS score is below the minimum allowed, do not add a treatment
        if (tssScore <= _frameworkModel.Configuration.MinimumTreatmentSuitabilityScoreAllowed) return; 

        double sdi = segment.SurfaceDistressIndex;
        string reason = $"SLA={Math.Round(segment.SurfaceAchievedLifePercent, 1)}";
        string comment = $"SDI={Math.Round(sdi, 1)}, TSS={Math.Round(tssScore, 2)}";

        double quantity = segment.AreaSquareMetre;

        double overlayQuantity = quantity;
        double repairQuantity = quantity * Math.Min(100, segment.PavementDistressIndex) / 100;
        
        // The two halves are priced from the treatments they are made of - the plain resurfacing and
        // the repairs-alone heavy maintenance - and both follow the same asphalt-or-OGPA family as the
        // holding treatment itself.
        double overlayUnitRate = _domainModel.Constants.GetUnitRate(ResurfacingNameForAsphaltRoute(segment));
        double repairUnitRate = _domainModel.Constants.GetUnitRate(HeavyMaintenanceNameForAsphaltRoute(segment));

        double overlayCost = overlayQuantity * overlayUnitRate;
        double repairCost = repairQuantity * repairUnitRate;

        double totalCost = overlayCost + repairCost;

        // A HOLDING TREATMENT WITH NO COST CANNOT BE COSTED OR SPLIT. The budget fractions below divide
        // by this total, so a zero would reach AssignBudgetCategoryFractions as NaN and surface as an
        // OverflowException naming nothing. It can only happen if BOTH rates this treatment is built
        // from are still sitting at zero in lookups.xlsx, which is a treatment that has not been priced
        // rather than one that is free - and a free treatment would win every optimisation it entered.
        if (totalCost <= 0)
        {
            throw new Exception($"Treatment '{treatmentName}' has no cost: the unit rates for " +
                                $"'{ResurfacingNameForAsphaltRoute(segment)}' and " +
                                $"'{HeavyMaintenanceNameForAsphaltRoute(segment)}' in the " +
                                $"'unit_rates_general' set of lookups.xlsx are both zero. Price them on " +
                                $"the Treatment Rates tab of the Tuning page before running this model.");
        }

        double dummyArea = totalCost; // Dummy area which is effectively the cost

        // THE HOLDING TREATMENT'S OWN RATE IS NEVER READ, and its row in 'unit_rates_general' says so:
        // it reads 'N/A', not a number. The cost is already in the quantity above, so the rate has to
        // be exactly 1.0 - it is passed as a constant here rather than fetched and then asserted, which
        // is what this used to do and what turned the deliberate 'N/A' into a FormatException.
        double unitRate = HoldingTreatmentUnitRate;

        TreatmentInstance treatment = new TreatmentInstance(segment.ElementIndex, treatmentName, iPeriod, quantity: dummyArea, unitRate: unitRate,
                                                            false, reason, comment);

        // Assign the relative fractions of the cost to the appropriate budget categories
        decimal repairFraction = Convert.ToDecimal(repairCost / totalCost);
        decimal overlayFraction = Convert.ToDecimal(overlayCost / totalCost);
        Dictionary<string, decimal> treatmentFractions = new Dictionary<string, decimal>
        {
            { "Resurfacing", overlayFraction },
            { "Pre-Repairs", repairFraction }
        };
        treatment.AssignBudgetCategoryFractions(treatmentFractions);


        treatment.TreatmentSuitabilityScore = tssScore;
        treatments.Add(treatment);
    }

    /// <summary>
    /// Heavy maintenance repairs on asphalt or OGPA, with no surfacing over them. A pre-repair: it
    /// renews no surface and does not move the surface age.
    /// </summary>
    private void AddHeavyMaintenanceAcOrOgpaIfValid(RoadSegment segment, int iPeriod, List<TreatmentInstance> treatments, Dictionary<string, object> infoFromModel)
    {
        double presealAreaFraction = 0.0; // Default value

        if (segment.NextSurfaceIsChipSeal == true) return;

        int periodsToLastNonRoutineTreatment = this.PeriodsToLastTreatmentNotRoutineMaintenance(infoFromModel, iPeriod);
                
        // Do not add AC Heavy Maintenance if the periods since last non-routine treatment is less than the minimum allowed
        if (periodsToLastNonRoutineTreatment < _domainModel.Constants.MinPeriodsBetweenACHeavyMaint) return; 

        // If an asphalt overlay is allowed, then only consider this treatment if the Surface Life Achieved is less than the maximum allowed for AC Heavy Maintenance
        // If an asphalt overlay is not allowed (e.g. due to deflection), then we can consider this treatment regardless of the SLA, otherwise the element will
        // have to wait until it can be rehabilitated
        if (segment.AsphaltOkFlag == true)
        {            
            if (segment.SurfaceAchievedLifePercent > this._domainModel.Constants.MaxSlaForACHeavyMaint) return;
        }
        
        JFuncLookupNumber presealAreaFractionLookup = new JFuncLookupNumber("preseal_effective: para_pdi", _frameworkModel.Lookups);
        Dictionary<string, object> paramVals = new Dictionary<string, object>
        {
            { "para_pdi", segment.PavementDistressIndex }
        };
        presealAreaFraction = Convert.ToDouble(presealAreaFractionLookup.Evaluate(paramVals));
        if (presealAreaFraction <= 0.0) return; // If preseal area fraction is zero or negative, do not add a treatment

        TreatmentInstance? treatment = this.GetPresealTreatment(segment, iPeriod, HeavyMaintenanceNameForAsphaltRoute(segment), presealAreaFraction);
        if (treatment is not null)
        {
            treatments.Add(treatment);
        }
    }

    private void AddPresealOnChipsealIfValid(RoadSegment segment, int iPeriod, List<TreatmentInstance> treatments)
    {
        double presealAreaFraction = 0.0; // Default value

        if (segment.NextSurfaceIsChipSeal == false) return;

        if (segment.SurfaceFunction == "1a") return; // Do not add a preseal treatment if the surface function is "1a"

        JFuncLookupNumber presealAreaFractionLookup = new JFuncLookupNumber("preseal_effective: para_pdi", _frameworkModel.Lookups);
        Dictionary<string, object> paramVals = new Dictionary<string, object>
        {
            { "para_pdi", segment.PavementDistressIndex }
        };
        presealAreaFraction = Convert.ToDouble(presealAreaFractionLookup.Evaluate(paramVals));
        if (presealAreaFraction <= 0.0) return; // If preseal area fraction is zero or negative, do not add a treatment
        
        TreatmentInstance? treatment = this.GetPresealTreatment(segment, iPeriod, TreatmentNames.ChipsealPresealRepairs, presealAreaFraction);
        if (treatment is not null) treatments.Add(treatment);

    }
    
    private void AddRehabilitationIfValid(RoadSegment segment, int iPeriod, List<TreatmentInstance> treatments)
    {        
        if (segment.NextSurfaceIsChipSeal == true)
        {
            if (this.CanDoRehabilitationOnChipSeal(segment) == false) return;            
        }
        else
        {
            if (this.CanDoRehabilitationOnAsphalt(segment) == false) return;
            
        }

        // ONE NAME PER SURFACING FAMILY, WHERE THERE USED TO BE TWELVE. The old name carried the
        // surface class and the urban/rural road type because the rate was flat and had to be looked up
        // by the whole combination. Rehabilitation rates are now keyed by ONRC category in their own
        // lookup sets, so the road type has no business in the name.
        string treatmentName = RehabilitationName(segment);

        double pdi = segment.PavementDistressIndex;
        
        double tssScore = TreatmentSuitabilityScorer.GetTSSForRehabilitation(segment, _domainModel, iPeriod);
        if (tssScore <= _frameworkModel.Configuration.MinimumTreatmentSuitabilityScoreAllowed) return; // If the TSS score is below the minimum allowed, do not add a treatment

        string reason = $"SLA={Math.Round(segment.SurfaceAchievedLifePercent, 1)}";
        string comment = $"PDI={Math.Round(pdi, 1)}, TSS={Math.Round(tssScore, 2)}";

        double unitRate = _domainModel.Constants.GetRehabUnitRate(treatmentName, segment.ONRC);

        double quantity = segment.AreaSquareMetre;
        TreatmentInstance treatment = new TreatmentInstance(segment.ElementIndex, treatmentName, iPeriod, quantity: quantity, unitRate: unitRate,
                                                            false, reason, comment);
        treatment.TreatmentSuitabilityScore = tssScore;
        treatments.Add(treatment);
    }

    private TreatmentInstance? GetPresealTreatment(RoadSegment segment, int iPeriod, string treatmentName, double treatmentAreaFraction)
    {
        double tssScore = 0;
        if (segment.CanRehabFlag == true)
        {
            // If this is a rehab route, then Preseal must compete with Rehab. Thus calculate the TSS for Preseal since
            // Rehab will be competing based on its TSS score.
            tssScore = TreatmentSuitabilityScorer.GetTSSForPresealRepairs(segment, _domainModel, iPeriod);            
        }
        else
        {
            // If this is NOT a rehab route, then Preseal is considered as a Rehabilitation. Thus the TSS in this case
            // should be based on the TSS for Rehabilitation.
            tssScore = TreatmentSuitabilityScorer.GetTSSForRehabilitation(segment, _domainModel, iPeriod);
        }

        if (tssScore <= _frameworkModel.Configuration.MinimumTreatmentSuitabilityScoreAllowed) return null; // If the TSS score is below the minimum allowed, do not add a treatment


        double pdi = segment.PavementDistressIndex;

        string reason = $"SLA={Math.Round(segment.SurfaceAchievedLifePercent, 1)}";
        string comment = $"PDI={Math.Round(pdi, 1)}, TSS={Math.Round(tssScore, 2)}";
        
        double unitRate = _domainModel.Constants.GetUnitRate(treatmentName);

        double quantity = segment.AreaSquareMetre * treatmentAreaFraction;
        TreatmentInstance treatment = new TreatmentInstance(segment.ElementIndex, treatmentName, iPeriod, quantity: quantity, unitRate: unitRate,
                                                            false, reason, comment);
        treatment.TreatmentSuitabilityScore = tssScore;
        return treatment;
    }
       
    private int PeriodsToLastTreatmentNotRoutineMaintenance(Dictionary<string, object> infoFromModel, int iPeriod)
    {
        if (infoFromModel["previous_treatments"] is null) return 999; // Indicates that no treatments have been placed yet

        List<TreatmentInstance> previousTreatments = (List<TreatmentInstance>)infoFromModel["previous_treatments"];

        TreatmentInstance? lastNonRoutineMaintenanceTreatment = null;

        // Loop over all previous treatments to find the most recent non-routine maintenance treatment
        int minTreatmentPeriod = int.MaxValue;
        foreach (TreatmentInstance treatment in previousTreatments)
        {
            if (treatment.TreatmentName != TreatmentNames.RoutineMaintenance)
            {
                int periodsToTreatment = iPeriod - treatment.TreatmentPeriod;
                if (periodsToTreatment < minTreatmentPeriod)
                {
                    minTreatmentPeriod = periodsToTreatment;
                    lastNonRoutineMaintenanceTreatment = treatment;
                }
            }
        }
        if (lastNonRoutineMaintenanceTreatment is not null)
        {
            return iPeriod - lastNonRoutineMaintenanceTreatment.TreatmentPeriod;
        }
        else
        {
            return 999; // Indicates that no non-routine treatment has been placed yet
        }
    }

}
