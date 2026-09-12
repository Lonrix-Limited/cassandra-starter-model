
using JCass_Core.JFunctions;
using JCass_ModelCore.Models;
using JCass_ModelCore.Treatments;


namespace StarterModel.Objects;

/// <summary>
/// Class for checking treatments triggering 
/// </summary>
public class TreatmentsTrigger
{
    private ModelBase _frameworkModel;
    private StarterModel _domainModel;
    Dictionary<string, object> _unitRateSet = null!;

    public TreatmentsTrigger(ModelBase frameworkModel, StarterModel domainModel)
    {
        _frameworkModel = frameworkModel ?? throw new ArgumentNullException(nameof(frameworkModel), "Domain model cannot be null");
        _domainModel = domainModel ?? throw new ArgumentNullException(nameof(domainModel), "Domain model cannot be null");
    }

    public List<TreatmentInstance> GetTriggeredTreatments(RoadSegment segment, int period, Dictionary<string, object> infoFromModel)
    {     
        _unitRateSet = _frameworkModel.Lookups["unit_rate_set"] as Dictionary<string, object>;

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

        this.AddPreservationThinACIfValid(segment, period, triggeredTreatments);
        this.AddHoldingThinACIfValid(segment, period, triggeredTreatments);
        this.AddAcHeavyMaintenanceIfValid(segment, period, triggeredTreatments, infoFromModel);

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

    private void AddBirthdayTreatmentBlocksOrConcreteIfValid(RoadSegment segment, int iPeriod, List<TreatmentInstance> treatments)
    {
        //n : pcal_can_treat_flag = 1 AND n : pcal_next_surf_blocks_flag = 1 AND n : period >= file_earliest_treat_period AND n : para_surf_remain_life <= 1

        if (segment.CanTreatFlag == false) return; // If the segment cannot be treated, do not add a treatment
        string treatmentName = "";

        switch (segment.NextSurface)
        {
            case "blocks":
                treatmentName = "BlockRep";
                break;
            case "concrete":
                treatmentName = "ConcRep";
                break;
            case "other":
                treatmentName = "Xtreat";
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

        
        if (!_unitRateSet.ContainsKey(treatmentName)) throw new Exception($"Unit rate for treatment {treatmentName} not found in lookup sets.");
        double unitRate = Convert.ToDouble(_unitRateSet[treatmentName]);

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

        treatmentName = "ChipSeal_H";
        reason = "Pre-seal follow-up";
        double quantity = segment.AreaSquareMetre;

        
        if (!_unitRateSet.ContainsKey(treatmentName)) throw new Exception($"Unit rate for treatment {treatmentName} not found in lookup sets.");
        double unitRate = Convert.ToDouble(_unitRateSet[treatmentName]);

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

            string treatmentName = "ChipSeal_S";

            if (!_unitRateSet.ContainsKey(treatmentName)) throw new Exception($"Unit rate for treatment {treatmentName} not found in lookup sets.");
            double unitRate = Convert.ToDouble(_unitRateSet[treatmentName]);

            TreatmentInstance treatment = new TreatmentInstance(segment.ElementIndex, treatmentName, iPeriod, quantity: quantity, unitRate: unitRate, 
                                                                true, "Second coat", "Second coat");
            treatment.TreatmentSuitabilityScore = 102; // Set a high suitability score for second coat treatments
            treatments.Add(treatment);
        }
    }

    private void AddPreservationChipsealIfValid(RoadSegment segment, int iPeriod, List<TreatmentInstance> treatments)
    {
        string treatmentName = "ChipSeal_P";
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

        if (!_unitRateSet.ContainsKey(treatmentName)) throw new Exception($"Unit rate for treatment {treatmentName} not found in lookup sets.");
        double unitRate = Convert.ToDouble(_unitRateSet[treatmentName]);

        double quantity = segment.AreaSquareMetre;
        TreatmentInstance treatment = new TreatmentInstance(segment.ElementIndex, treatmentName, iPeriod, quantity: quantity, unitRate: unitRate, 
                                                            false, reason, comment);
        treatment.TreatmentSuitabilityScore = tssScore;
        treatments.Add(treatment);
    }

    private void AddPreservationThinACIfValid(RoadSegment segment, int iPeriod, List<TreatmentInstance> treatments)
    {
        string treatmentName = "ThinAC_P";
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

        if (!_unitRateSet.ContainsKey(treatmentName)) throw new Exception($"Unit rate for treatment {treatmentName} not found in lookup sets.");
        double unitRate = Convert.ToDouble(_unitRateSet[treatmentName]);

        TreatmentInstance treatment = new TreatmentInstance(segment.ElementIndex, treatmentName, iPeriod, quantity: overlayQuantity, unitRate:unitRate,
                                                            false, reason, comment);
        
        treatment.TreatmentSuitabilityScore = tssScore;
        treatments.Add(treatment);
    }

    private void AddHoldingThinACIfValid(RoadSegment segment, int iPeriod, List<TreatmentInstance> treatments)
    {        
        string treatmentName = "ThinAC_H";
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
        
        if (!_unitRateSet.ContainsKey("ThinAC_P")) throw new Exception($"Unit rate for treatment {"ThinAC_P"} not found in lookup sets.");
        double acOverlayUnitRate = Convert.ToDouble(_unitRateSet["ThinAC_P"]);
        
        if (!_unitRateSet.ContainsKey("HMaint_AC")) throw new Exception($"Unit rate for treatment {"HMaint_AC"} not found in lookup sets.");
        double acRepairUnitRate = Convert.ToDouble(_unitRateSet["HMaint_AC"]);

        double overlayCost = overlayQuantity * acOverlayUnitRate;
        double repairCost = repairQuantity * acRepairUnitRate;

        double totalCost = overlayCost + repairCost;

        double dummyArea = totalCost; // Dummy area which is effectively the cost
                        
        if (!_unitRateSet.ContainsKey(treatmentName)) throw new Exception($"Unit rate for treatment {treatmentName} not found in lookup sets.");
        double unitRate = Convert.ToDouble(_unitRateSet[treatmentName]);
        if (unitRate != 1.0) throw new Exception($"Unit rate for treatment {treatmentName} should be 1.0, but found {unitRate}. This is because the cost is calculated based on the relative fractions of the overlay and repair costs.");

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

    private void AddAcHeavyMaintenanceIfValid(RoadSegment segment, int iPeriod, List<TreatmentInstance> treatments, Dictionary<string, object> infoFromModel)
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

        TreatmentInstance? treatment = this.GetPresealTreatment(segment, iPeriod, "HMaint_AC", presealAreaFraction);
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
        
        TreatmentInstance? treatment = this.GetPresealTreatment(segment, iPeriod, "PreSeal", presealAreaFraction);
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

        string treatmentName = "Rehab_" + segment.SurfaceRoadType.ToUpper(); ;

        double pdi = segment.PavementDistressIndex;
        
        double tssScore = TreatmentSuitabilityScorer.GetTSSForRehabilitation(segment, _domainModel, iPeriod);
        if (tssScore <= _frameworkModel.Configuration.MinimumTreatmentSuitabilityScoreAllowed) return; // If the TSS score is below the minimum allowed, do not add a treatment

        string reason = $"SLA={Math.Round(segment.SurfaceAchievedLifePercent, 1)}";
        string comment = $"PDI={Math.Round(pdi, 1)}, TSS={Math.Round(tssScore, 2)}";

        if (!_unitRateSet.ContainsKey(treatmentName)) throw new Exception($"Unit rate for treatment {treatmentName} not found in lookup sets.");
        double unitRate = Convert.ToDouble(_unitRateSet[treatmentName]);

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
        
        if (!_unitRateSet.ContainsKey(treatmentName)) throw new Exception($"Unit rate for treatment {treatmentName} not found in lookup sets.");
        double unitRate = Convert.ToDouble(_unitRateSet[treatmentName]);

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
            if (treatment.TreatmentName != "RMaint")
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
