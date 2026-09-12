
using JCass_Core.JFunctions;

namespace StarterModel.Objects;

public static class TreatmentSuitabilityScorer
{

    public static double GetTSSForPreservationTreatment(RoadSegment segment, StarterModel domainModel, int iPeriod)
    {
        string tssModelSetup = $"{domainModel.Constants.TSSPreserveSdiRank},0|100,100";
        PieceWiseLinearModelGeneric tssModel = new PieceWiseLinearModelGeneric(tssModelSetup, true);

        double pdi = segment.PavementDistressIndex;
        double tssScore1 = tssModel.GetValue(segment.SurfaceDistressIndexRank);   //Use RANK, not the SDI itself!!
        double tssScore = tssScore1 - 0.5*pdi;
        return tssScore;
    }

    public static double GetTSSForRehabilitation(RoadSegment segment, StarterModel domainModel, int iPeriod)
    {
        double excessRutThreshold = domainModel.Constants.TSSRehabExcessRutThresh;
        double rutPenaltyFactor = domainModel.Constants.TSSRehabExcessRutFact;
        double excessRutPenalty = segment.RutParameterValue > excessRutThreshold ? (segment.RutParameterValue - excessRutThreshold) * rutPenaltyFactor : 0.0;

        string tssModelSetup = $"{domainModel.Constants.TSSRehabPdiRank},0|100,100";
        PieceWiseLinearModelGeneric tssModel = new PieceWiseLinearModelGeneric(tssModelSetup, false);

        double pdi = segment.PavementDistressIndex;
        double tssScore1 = tssModel.GetValue(segment.PavementDistressIndexRank);   //Use RANK, not the PDI itself!!
        double tssScore = tssScore1 + excessRutPenalty;

        return tssScore;
    }

    public static double GetTSSForPresealRepairs(RoadSegment segment, StarterModel domainModel, int iPeriod)
    {        
        // If Rut is above the allowed value for Preseal Repairs, then TSS is zero
        if (segment.RutParameterValue > domainModel.Constants.TSSHoldingMaxRut) return 0.0;

        // If we get here, a preservation treatment is valid. Calculate the relative suitability score based on the Surface Distress Index (SDI)
        string tssModelSetup = $"{domainModel.Constants.TSSHoldingPdiRankPt1},0|{domainModel.Constants.TSSHoldingPdiRankPt2},100 | 100,{domainModel.Constants.TSSHoldingPdiRankPt3}";
        PieceWiseLinearModelGeneric tssModel = new PieceWiseLinearModelGeneric(tssModelSetup, true);        
        double tssScore = tssModel.GetValue(segment.PavementDistressIndexRank);
        return tssScore;
    }

}
