using JCass_ModelCore.Models;

namespace StarterModel.Objects;

public class Incrementer
{

    private ModelBase _frameworkModel;
    private StarterModel _domainModel;

    public Incrementer(ModelBase frameworkModel, StarterModel domainModel)
    {
        _frameworkModel = frameworkModel ?? throw new ArgumentNullException(nameof(frameworkModel), "Domain model cannot be null");
        _domainModel = domainModel ?? throw new ArgumentNullException(nameof(domainModel), "Domain model cannot be null");
    }

    public RoadSegment Increment(RoadSegment segment, int period)
    {
        // Increment all properties related to model parameters
        // Keep the code same order as the model parameter list

        segment.AverageDailyTraffic = segment.AverageDailyTraffic * (1 + segment.TrafficGrowthPercent / 100);
        // No need to reset HCV count as it is automatically calculated based on the AverageDailyTraffic and HCVPercent

        segment.PavementAge = segment.PavementAge + 1;
        segment.PavementRemainingLife = segment.PavementRemainingLife - 1;

        // No need to update Pavement Life Achieved and HCV Risk because it is automatically calculated based on the HCV and Pavement Life Achieved

        // No change in these properties:
        // segment.SurfaceMaterial 
        // segment.SurfaceClass
        // segment.SurfaceThickness            
        // segment.SurfaceNumberOfLayers
        // segment.SurfaceFunction 
        // segment.SurfaceExpectedLife 

        segment.SurfaceAge = segment.SurfaceAge + 1;

        // Note: surface life achieved and surface remaining life are automatically calculated based on the surface age and expected life

        // TODO: Rutting and Roughness increments are dependent on Distresses and should therefore be determine only distresses have been
        // incremented. But the JFunction model does it the other way round. Modify this once models converge.

        // Only update Rutting increment if a treatment has been applied, otherwise we continue using (for now) the historical rate
        if (segment.TreatmentCount > 0)
        {
            segment.RutIncrement = segment.GetRutIncrementAfterTreatment();
        }
        segment.RutParameterValue += segment.RutIncrement;

        // Only update the Naasra increment if a treatment has been applied, otherwise we continue using the historical rate
        if (segment.TreatmentCount > 0)
        {
            segment.NaasraIncrement = segment.GetNaasraIncrementAfterTreatment();
        }
        segment.Naasra85 += segment.NaasraIncrement;

        // Increment visual distresses
        segment.PctFlushing = _domainModel.FlushingModel.GetNextValueAfterIncrement(segment, segment.PctFlushing, segment.FlushingModelInfo);
        // segment.FlushingModelInfo stays unchanged during increment

        segment.PctEdgeBreaks = _domainModel.EdgeBreakModel.GetNextValueAfterIncrement(segment, segment.PctEdgeBreaks, segment.EdgeBreakModelInfo);
        // segment.EdgeBreakModelInfo stays unchanged during increment

        segment.PctScabbing = _domainModel.ScabbingModel.GetNextValueAfterIncrement(segment, segment.PctScabbing, segment.ScabbingModelInfo);
        // segment.ScabbingModelInfo stays unchanged during increment

        segment.PctLongTransCracks = _domainModel.LTCracksModel.GetNextValueAfterIncrement(segment, segment.PctLongTransCracks, segment.LTCracksModelInfo);
        // segment.LTCracksModelInfo stays unchanged during increment

        segment.PctMeshCracks = _domainModel.MeshCrackModel.GetNextValueAfterIncrement(segment, segment.PctMeshCracks, segment.MeshCrackModelInfo);
        // segment.MeshCrackModelInfo stays unchanged during increment

        segment.PctShoving = _domainModel.ShovingModel.GetNextValueAfterIncrement(segment, segment.PctShoving, segment.ShovingModelInfo);
        // segment.ShovingModelInfo stays unchanged during increment

        segment.PctPotholes = _domainModel.PotholeModel.GetNextValueAfterIncrement(segment, segment.PctPotholes, segment.PotholeModelInfo);
        //segment.PotholeModelInfo stays unchanged during increment
        
        // Calculated parameters such as PDI, SDI and Objective Function Parameters should be calculated on return

        // Is treated flag and treatment count stays unchanged during regular increment

        // Ranking parameters will be calculated by the framework model

        return segment;

    }

}
