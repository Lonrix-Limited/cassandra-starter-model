
using JCass_ModelCore.Models;

namespace StarterModel.Objects;

public record CandidateSelectionResult(bool IsValidCandidate, string reason)
{
    public string Outcome
    {
        get
        {
            return IsValidCandidate ? "ok" : $"{reason}";
        }
    }
}


public static class CandidateSelector
{

    public static CandidateSelectionResult EvaluateCandidate(RoadSegment segment, ModelBase frameworkModel, 
        StarterModel domainModel, int currentPeriod, int periodsToNextTreatment)
    {
		try
		{
            // Is the specified earliest treatment period for the segment reached?
            // TODO: Needs discussion - should we override second coats with this flag (as is the case now), or not?
            int adjustedPeriod = currentPeriod + 1;  //Adjusted modelling period to account for a post calc lag in para csl flag
            if (adjustedPeriod < segment.EarliestTreatmentPeriod)
            {
                return new CandidateSelectionResult(false, $"Earliest treatment period {segment.EarliestTreatmentPeriod} not reached");
            }

            //Does this segment require a second coat now? If so, it is a valid candidate, so look no further.
            if (segment.SecondCoatNeeded) { return new CandidateSelectionResult(true, "Second-Coat Needed"); }

            // If surface function is '1a' a follow-up surfacing after Preseal repairs is needed, so look no further.
            if (segment.SurfaceFunction == "1a") { return new CandidateSelectionResult(true, "Second-Coat Needed over Preseal Repairs"); }

            // If there is a committed treatment in the near future, then this segment is not a candidate for treatment
            if (periodsToNextTreatment <= domainModel.Constants.CSMinPeriodsToNextTreat) 
            {
                return new CandidateSelectionResult(false, $"Next treatment in {periodsToNextTreatment} periods: too soon");
            }

            // Note: check on minimum surface age removed because it is redundant with the SLA check below

            // Is the specified minimum Surface Life Achieved (SLA) reached? 
            // Check for Asphalt and Chip Seal surfaces separately, as they have different thresholds
            if (segment.SurfaceClass == "ac" && segment.SurfaceAchievedLifePercent < domainModel.Constants.CSMinSlaToTreatAc)
            {
                return new CandidateSelectionResult(false, 
                    $"SLA = {Math.Round(segment.SurfaceAchievedLifePercent, 2)}: below threshold ({domainModel.Constants.CSMinSlaToTreatAc})");
            }

            if (segment.SurfaceClass == "cs" && segment.SurfaceAchievedLifePercent < domainModel.Constants.CSMinSlaToTreatCs)
            {
                return new CandidateSelectionResult(false, 
                    $"SLA = {Math.Round(segment.SurfaceAchievedLifePercent, 2)}: below threshold ({domainModel.Constants.CSMinSlaToTreatCs})");
            }

            // Block, concrete and 'other' surfaces are due a repair on AGE, not on condition, and they
            // cannot pass the distress test below: none of them is given flushing or ravelling, and
            // blocks are given no cracking either, so a block segment's surface distress index is zero
            // for the whole run and its pavement index is rut excess on its own. Gated on distress, the
            // repair these surfaces exist to receive would simply never be offered. The treatments
            // trigger applies the same condition before it names the treatment.
            if (segment.BirthdayRepairNeeded) { return new CandidateSelectionResult(true, "Birthday Repair Needed"); }

            // Finally, check if the segment meets the minimum distress indices for treatment
            if (segment.SurfaceDistressIndex < domainModel.Constants.CSMinSDIToTreat && segment.PavementDistressIndex < domainModel.Constants.CSMinPDIToTreat)
            {
                return new CandidateSelectionResult(false,
                    $"SDI = {Math.Round(segment.SurfaceDistressIndex, 2)}, PDI = {Math.Round(segment.PavementDistressIndex, 2)}: below thresholds ({domainModel.Constants.CSMinSDIToTreat}, {domainModel.Constants.CSMinPDIToTreat})");
            }

            return new CandidateSelectionResult(true, "OK. Passed CSA Checks");        
        }
        catch (Exception ex)
		{
			throw new Exception($"Error checking if {segment.FeebackCode} is a valid Candidate for treatment. Details: {ex.Message}");
		}
    }       

}
