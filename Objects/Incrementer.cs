using JCass_ModelCore.Models;

namespace StarterModel.Objects;

/// <summary>
/// Advances a segment by one modelling period where no treatment was applied.
///
/// <para>THE CONDITION MODELS ARE LEVELS, NOT INCREMENTS. Cracking, rutting and roughness are each
/// recomputed from the segment's surface age and its own persistent random draws - none of them is
/// last period's value plus a rate. That is why this class reads the previous value only to report
/// what changed, and never to build the new one. Nothing accumulates, so nothing can drift.</para>
///
/// <para>ORDER IS FIXED AND MATTERS: cracking, then rutting, then roughness. Rutting and roughness
/// each take a feedback term in cracking, and roughness takes one in rutting. All three feedback
/// coefficients are zero by default, so today the order changes no number - but they are switchable
/// from lookups.xlsx, and once they are on, the wrong order changes the forecast silently.</para>
/// </summary>
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
        //
        // Note that this grown value is NOT what the deterioration models below see: they read
        // SurveyedAverageDailyTraffic, which stays at the measured value for the whole run.

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

        // The chipseal rut growth accumulator advances with it, but it is NOT the same clock. A
        // resurfacing returns the surface age to zero and leaves this running, which is what makes
        // chipseal rutting inherited by the new seal rather than renewed by it. Only a rehabilitation
        // sends this back to zero. Asphalt never reads it.
        segment.RutGrowthYears = segment.RutGrowthYears + 1;

        // Years since the segment's last pre-repair, which is what the three pre-repair credits decay
        // on. It advances here like any other clock; it is set back to zero only by a pre-repair, and
        // cleared with the credits themselves by a rehabilitation. Zero for every segment that has
        // never had one, in which case there is no credit for it to decay.
        segment.PreRepairYears = segment.PreRepairYears + 1;

        // Note: surface life achieved and surface remaining life are automatically calculated based on the surface age and expected life

        // The surface age just advanced is the clock every condition model below runs on.
        double surfaceAge = segment.SurfaceAge;

        // Both of these live on DeteriorationModels rather than here, because the Resetter runs exactly
        // the same two steps after it has moved the clocks. One copy, so an increment and a reset cannot
        // evaluate the models in a different order or with different feedback terms.
        _domainModel.DeteriorationModels.UpdateConditions(segment, surfaceAge);
        _domainModel.DeteriorationModels.UpdateRuleBasedDistresses(segment, surfaceAge);

        // Calculated parameters such as PDI, SDI and Objective Function Parameters should be calculated on return

        // Is treated flag and treatment count stays unchanged during regular increment

        // The persistent deterioration draws - the rut and IRI deviates, the two cracking draws and the
        // held sub-threshold cracking value - are NOT touched here. They are drawn once at
        // initialisation and kept for the life of the surfacing, which is what makes one segment differ
        // from another of the same age and traffic. The Resetter redraws them when a treatment lands.

        // Ranking parameters will be calculated by the framework model

        return segment;

    }

}
