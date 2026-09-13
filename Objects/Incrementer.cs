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

        // Note: surface life achieved and surface remaining life are automatically calculated based on the surface age and expected life

        // The surface age just advanced is the clock every condition model below runs on.
        double surfaceAge = segment.SurfaceAge;

        this.IncrementConditionModels(segment, surfaceAge);
        this.IncrementRuleBasedDistresses(segment, surfaceAge);

        // Calculated parameters such as PDI, SDI and Objective Function Parameters should be calculated on return

        // Is treated flag and treatment count stays unchanged during regular increment

        // The persistent deterioration draws - the rut and IRI deviates, the two cracking draws and the
        // held sub-threshold cracking value - are NOT touched here. They are drawn once at
        // initialisation and kept for the life of the surfacing, which is what makes one segment differ
        // from another of the same age and traffic. The Resetter redraws them when a treatment lands.

        // Ranking parameters will be calculated by the framework model

        return segment;

    }

    /// <summary>
    /// Recomputes cracking, rutting and roughness for the new surface age, in that order.
    /// <para>Each is a level, so the previous value plays no part in building the new one. It is read
    /// first only so that the realised change can be reported.</para>
    /// </summary>
    private void IncrementConditionModels(RoadSegment segment, double surfaceAge)
    {
        DeteriorationModels models = _domainModel.DeteriorationModels;

        double rutBefore = segment.RutParameterValue;
        double iriBefore = segment.Iri;

        // 1. Cracking. Either the segment has cracked at this age, in which case its severity comes
        //    from the left-truncated distribution, or it has not, in which case it carries the
        //    sub-threshold value it has held since the surface was laid.
        segment.PctCracking = models.GetCracking(segment, surfaceAge);

        // 2. Rutting, which takes cracking as a feedback term.
        segment.RutParameterValue = models.GetRutting(segment, surfaceAge, segment.PctCracking);

        // 3. Roughness, which takes both cracking and rutting as feedback terms.
        segment.Iri = models.GetRoughness(segment, surfaceAge, segment.PctCracking, segment.RutParameterValue);

        // Naasra needs no update: it is derived from Iri on every read.

        // The two increment parameters are now DIAGNOSTIC ONLY - they report what the level models
        // actually did this period. Nothing reads them back to build the next value. They are worth
        // keeping because a realised year-on-year change is what a modeller looks at first, but do not
        // mistake them for a rate that drives anything.
        segment.RutIncrement = segment.RutParameterValue - rutBefore;
        segment.IriIncrement = segment.Iri - iriBefore;
    }

    /// <summary>
    /// Recomputes flushing and ravelling from the rule-based model: nothing until a fraction of the
    /// surface expected life has passed, then a straight line.
    /// <para>These are JUDGEMENT, not fitted. The survey data carries no magnitude worth fitting -
    /// nine in ten segments read zero flushing, and the 99th percentile is 1.5% - so the rates come
    /// from engineering expertise and live in lookups.xlsx, where they can be changed without a rebuild.</para>
    /// <para>Potholes take the same rule in the specification and are deliberately not modelled here:
    /// this model carries no pothole parameter, and pothole extent never exceeds 0.12% anywhere on the
    /// network, which is noise rather than signal.</para>
    /// </summary>
    private void IncrementRuleBasedDistresses(RoadSegment segment, double surfaceAge)
    {
        DeteriorationModels models = _domainModel.DeteriorationModels;

        segment.PctFlushing = models.GetFlushing(segment, surfaceAge);
        segment.PctRavelling = models.GetRavelling(segment, surfaceAge);
    }

}
