using JCass_ModelCore.Models;
using JCass_ModelCore.Treatments;


namespace StarterModel.Objects;

/// <summary>
/// Applies a treatment to a segment and advances it by one modelling period.
///
/// <para>MOST OF A RESET IS A CHANGE OF CLOCK, NOT ARITHMETIC ON A VALUE. The condition models are
/// levels: cracking, rutting and roughness are recomputed every period from the segment's surface age
/// and its own persistent draws. So resetting the surface age to zero IS the resurfacing reset - it
/// takes cracking back to what a new surface carries, asphalt rutting to about 36% of where it was,
/// and roughness to 74% on asphalt and 92% on chipseal. There is no reset lookup behind any of that
/// and there must not be one.</para>
///
/// <para>THERE ARE TWO CLOCKS AND KEEPING THEM APART IS THE WHOLE JOB. The surface age drives
/// cracking, asphalt rutting, roughness on both classes, flushing and ravelling. The chipseal rut
/// growth accumulator drives the chipseal rut increment and nothing else. A resurfacing resets the
/// first and leaves the second running, because a seal follows the shape of what it is laid on and
/// inherits the rut rather than renewing it. A rehabilitation resets both.</para>
///
/// <para>THERE ARE THREE TREATMENT CLASSES, NOT TWO, AND THE THIRD IS NOT A SMALL VERSION OF EITHER.
/// Beside resurfacing and rehabilitation, Cassandra applies PRE-REPAIRS - preseal repairs on chipseal,
/// heavy maintenance on asphalt. A pre-repair touches neither clock and does not touch the deflection.
/// It acts on the segment's persistent deviate, subtractively and with the credit decaying, because
/// the deviate is what carries a segment's excess distress relative to what its age predicts - which
/// is exactly what localised repairs remove. See the pre-repair region of DeteriorationModels.</para>
///
/// <para>PRE-REPAIRS AND SURFACE RENEWAL ARE NOT ALTERNATIVES HERE, and this class used to treat them
/// as if they were. They are two independent questions about a treatment, and one treatment answers
/// yes to both. The three arrangements the specification names all map onto treatments this model
/// already produces: ThinAC_H is an asphalt overlay WITH repairs in the same year, HMaint_AC is
/// repairs alone, and PreSeal is repairs followed by a ChipSeal_H a year or two later, which the
/// surface function '1a' is what forces.</para>
///
/// <para>A REHABILITATION IS THE ONE TREATMENT THE FITTED MODELS CANNOT DESCRIBE, so it is the one
/// place values are imposed. Every segment in the fitted data is a surfacing over an old pavement -
/// median pavement age 63 years, not one reconstructed pavement in the file - so the models' age-zero
/// prediction means "a fresh surface on a sixty-year-old pavement". Setting a rebuilt segment to its
/// as-new condition and recomputing next period would snap it straight back, which is what the
/// permanent offsets carried by RoadSegment.HasBeenRehabilitated exist to stop.</para>
///
/// <para>EVERY DRAW HERE COMES FROM THE SEGMENT'S OWN STREAM, not from the shared model generator.
/// The reason is not thread safety - the framework does not process one model's elements in parallel -
/// but that a shared generator makes each segment's draws depend on how many draws every segment
/// treated before it happened to take, and a rehabilitation takes more of them than a reseal. See
/// SegmentRandom. The Initialiser draws the same way, at period 0.</para>
/// </summary>
public class Resetter
{

    /// <summary>
    /// The one treatment that is a pre-repair AND a resurfacing in the same year, lower case: an
    /// asphalt overlay or inlay that includes heavy maintenance repairs. The treatments trigger already
    /// costs it as both, splitting it between the Resurfacing and Pre-Repairs budget categories.
    /// <para>Matched in full rather than by prefix, because 'thinac' also matches ThinAC_P, which is
    /// the same overlay with no or minimal repairs and earns no repair credit.</para>
    /// </summary>
    private const string PreRepairWithOverlayTreatment = "thinac_h";

    private ModelBase _frameworkModel;
    private StarterModel _domainModel;

    public Resetter(ModelBase frameworkModel, StarterModel domainModel)
    {
        _frameworkModel = frameworkModel ?? throw new ArgumentNullException(nameof(frameworkModel), "Domain model cannot be null");
        _domainModel = domainModel ?? throw new ArgumentNullException(nameof(domainModel), "Domain model cannot be null");
    }

    public RoadSegment Reset(RoadSegment segment, int period, TreatmentInstance treatment)
    {

        if (treatment is null) return segment;

        string treatmentName = treatment.TreatmentName.ToLower();

        // THESE ARE TWO INDEPENDENT QUESTIONS AND ThinAC_H ANSWERS YES TO BOTH - an asphalt overlay
        // that includes heavy maintenance repairs, which is why the treatments trigger already splits
        // its cost between the Resurfacing and Pre-Repairs budget categories. Treating pre-repair and
        // resurfacing as alternative branches, as this method used to, left that arrangement nowhere
        // to go: it reset the clock and earned no repair credit.
        bool isRehab = treatmentName.StartsWith("rehab");
        bool isPreRepair = treatmentName.StartsWith("hmaint") || treatmentName.StartsWith("preseal")
                           || treatmentName == PreRepairWithOverlayTreatment;
        bool renewsSurface = !treatmentName.StartsWith("hmaint") && !treatmentName.StartsWith("preseal");

        // Every draw below comes from here. See SegmentRandom for why it is not model.Random.
        Random random = SegmentRandom.ForSegment(_frameworkModel.RandomSeed, segment.ElementIndex, period);

        // Reset (or increment where not applicable) all properties related to model parameters
        // Keep the code same order as the model parameter list

        segment.AverageDailyTraffic = segment.AverageDailyTraffic * (1 + segment.TrafficGrowthPercent / 100);
        // No need to reset HCV count as it is automatically calculated based on the AverageDailyTraffic and HCVPercent

        if (isRehab)
        {
            segment.PavementAge = 0;
            segment.PavementRemainingLife = _frameworkModel.GetLookupValueNumber("pavement_expected_life", segment.RoadType);
        }
        else
        {
            segment.PavementAge = segment.PavementAge + 1;
            segment.PavementRemainingLife = segment.PavementRemainingLife - 1;
        }

        // No need to update Pavement Life Achieved and HCV Risk because it is automatically calculated based on the HCV and Pavement Life Achieved

        // WHAT A PRE-REPAIR REMOVED IS WORKED OUT HERE, BEFORE ANY CLOCK MOVES. The credit is the
        // distance between where the segment is now and where the repair leaves it, and both ends have
        // to be read at the same surface age or it means nothing - which matters because the overlay
        // arrangement below sends that age to zero a few lines later. A rehabilitation does not compute
        // one at all: it redraws the deviates a credit would have been measured against.
        if (isPreRepair && !isRehab)
        {
            _domainModel.DeteriorationModels.ApplyPreRepairCredits(segment);
        }
        else
        {
            // Every other period the credit simply ages, and decays with it. It survives a resurfacing
            // on purpose - a reseal does not undo a digout, and the deviate the credit sits on is
            // retained across a reseal too.
            segment.PreRepairYears = segment.PreRepairYears + 1;
        }

        segment.SurfaceMaterial = _frameworkModel.GetLookupValueText("treat_surf_materials", treatment.TreatmentName);
        segment.SurfaceClass = _frameworkModel.GetLookupValueText("treat_surf_class", treatment.TreatmentName);

        // The surface class just changed, and the deterioration group follows it. Re-resolve it here or
        // every model evaluated below runs on the group the segment had BEFORE the treatment - an
        // asphalt rehabilitation on a chipseal segment would be costed with the chipseal models for the
        // period it lands in, and correct itself silently the period after.
        segment.DeteriorationGroup = _domainModel.Constants.GetDeteriorationGroup(segment.SurfaceClass);

        // A rehabilitation starts the surfacing again; a resurfacing adds a layer to it; a pre-repair
        // that renews no surface adds nothing at all.
        //
        // THAT LAST CASE WAS WRONG UNTIL THE PRE-REPAIR RESET LANDED, and it was inherited rather than
        // decided. PreSeal used to add 10 mm of chip and a layer for a treatment that lays no chip -
        // and then the ChipSeal_H that follows it a year or two later added another 15 mm and another
        // layer for the seal that actually went down. HMaint_AC escaped only because asphalt's
        // 'thickness to add' happens to be zero. Neither quantity drives any model; both are reported
        // as par_surf_thick and par_surf_layers, so this was a reporting defect and not a forecast one.
        if (isRehab)
        {
            //Surface Thickness to reset to, based on lookup of surface material type applied if treatment is pavement renewal (Rehab)
            segment.SurfaceThickness = _frameworkModel.GetLookupValueNumber("surf_thickness_new", segment.SurfaceMaterial);
            segment.SurfaceNumberOfLayers = 1;  // Reset number of surface layer count
        }
        else if (renewsSurface)
        {
            //Surface Thickness to add, based on lookup of surface material type applied if treatment is surface renewal
            segment.SurfaceThickness = segment.SurfaceThickness + _frameworkModel.GetLookupValueNumber("surf_thickness_add", segment.SurfaceMaterial);

            // If this is a chipseal, increase number of layers. If this is an AC, then it remains the same.
            segment.SurfaceNumberOfLayers = segment.SurfaceIsChipSealFlag == 1 ? segment.SurfaceNumberOfLayers + 1 : segment.SurfaceNumberOfLayers;
        }

        segment.SurfaceFunction = this.GetSurfaceFunction(treatment.TreatmentName, renewsSurface, segment.SurfaceFunction);

        segment.SurfaceExpectedLife = this.GetExpectedSurfaceLife(segment);

        // THE CLOCK IS WHAT SEPARATES A PRE-REPAIR FROM A RESURFACING, and it is the one thing a
        // pre-repair on its own must never move. It advances by a year exactly as an untreated segment
        // would, because a year did pass. The overlay arrangement renews the surface as well, so it
        // resets the clock like any other resurfacing - and the guard in the deterioration models is
        // what stops that plus the repair credit compounding to better than a rebuild.
        segment.SurfaceAge = renewsSurface ? 0 : segment.SurfaceAge + 1;
        // Note: surface life achieved and surface remaining life are automatically calculated based on the surface age and expected life

        // The chipseal rut growth accumulator is a DIFFERENT clock from the surface age and must not be
        // reset alongside it. A reseal leaves it running, because a chipseal follows the shape of what
        // it is laid on and inherits the rut rather than renewing it - which is why chipseal rut reads
        // 4.19 mm under surfaces up to six years old against 3.77 mm under surfaces over twenty, where
        // asphalt goes the other way. Only a rehabilitation returns it to zero.
        segment.RutGrowthYears = isRehab ? 0.0 : segment.RutGrowthYears + 1;

        if (isRehab)
        {
            this.ApplyRehabilitation(segment, random);
        }
        else
        {
            this.ApplySurfaceTreatment(segment, renewsSurface, isPreRepair, random);
        }

        // Increase the treatment count for the segment. This will also mark the treatment as treated, and reset the
        // historical maintenance quantities so that it no longer influences PDI and SDI calculations.
        segment.TreatmentCount++;

        // Ranking parameters will be calculated by the framework model

        return segment;

    }

    #region The two condition resets

    /// <summary>
    /// Applies a rehabilitation: a new pavement under a new surface.
    ///
    /// <para>THIS IS THE ONLY PLACE CONDITION VALUES ARE IMPOSED, and it has to be, because the fitted
    /// models have never seen a reconstructed pavement. At surface age zero with rehabilitated
    /// deflection they predict 3.66 mm of chipseal rut and IRI 4.44 on asphalt, 5.72 on chipseal - all
    /// worse than the as-new condition a rebuilt road is meant to start at. So the as-new values are
    /// set here, and the permanent offsets the segment now carries are what stop the model pulling it
    /// back the moment the next period recomputes.</para>
    ///
    /// <para>The persistent draws are all replaced, because it is a different pavement: the rut and IRI
    /// deviates are redrawn, and the cracking draws are redrawn from the band that means "has not
    /// cracked at this age", which is the same rule the Initialiser uses for a segment surveyed below
    /// the threshold. A blind redraw instead would put 62% of rebuilt asphalt segments back above the
    /// cracking threshold one year after reconstruction; drawn this way it is 40%.</para>
    ///
    /// <para>THAT 40% IS STILL TOO HIGH AND THE DRAW CANNOT FIX IT. The onset probability itself climbs
    /// steeply with surface age - 0.36 at age zero and 0.62 at age one on asphalt at rehabilitated
    /// deflection - and cracking carries no as-new offset the way rutting and roughness do, because
    /// there is no as-new cracking value to derive one from. Closing the gap needs an offset on onset,
    /// and that needs a number from the engineer. Flagged, not invented.</para>
    /// </summary>
    private void ApplyRehabilitation(RoadSegment segment, Random random)
    {
        DeteriorationModels models = _domainModel.DeteriorationModels;
        Constants constants = _domainModel.Constants;

        double rutBefore = segment.RutParameterValue;
        double iriBefore = segment.Iri;

        // The pavement is new, so its structural measurement is too. This has to be set before anything
        // below is evaluated: deflection is a covariate in the cracking onset, chipseal rutting and both
        // roughness models, and it is the one covariate a rehabilitation changes.
        segment.CentralDeflection = constants.GetRehabResetDeflection(segment.DeteriorationGroup);

        // From here on the segment is a rebuilt pavement, and every later period evaluates its rutting
        // and roughness with the as-new offsets. Never set back to false: a reseal in twenty years does
        // not make the pavement underneath old again.
        segment.HasBeenRehabilitated = true;

        // A different pavement deteriorates differently, so the segment's character is redrawn rather
        // than kept. Clamped exactly as an inverted deviate is, which is what keeps the spread of
        // rebuilt segments the same as the spread of the network they rejoin.
        segment.RutDeviate = models.DrawLevelDeviate(random);
        segment.IriDeviate = models.DrawLevelDeviate(random);

        // The surface age is already zero here, so this draws a position that does not produce onset on
        // a new surface over a new pavement.
        segment.CrackOnsetPosition = models.DrawOnsetPositionBelowOnset(segment, random);
        segment.CrackSeverityQuantile = random.NextDouble();
        segment.CrackingBelowOnset = models.DrawCrackingBelowOnset(segment, random);

        // Any credit from an earlier pre-repair goes with the deviates it was measured against. Left in
        // place it would subtract from the fresh draws above and hand a rebuilt pavement a discount it
        // did not earn - on top of the as-new offsets it is already entitled to.
        DeteriorationModels.ClearPreRepairCredits(segment);

        // The as-new condition. Imposed, not modelled - see the class summary for why the models cannot
        // supply it themselves.
        segment.PctCracking = constants.RehabResetCrackingPercent;
        segment.RutParameterValue = constants.RehabResetRutMillimetres;
        segment.Iri = constants.RehabResetIri;

        DeteriorationModels.RecordRealisedChange(segment, rutBefore, iriBefore);

        // Flushing and ravelling fall out of the surface age with no arithmetic: a new surface is below
        // the onset age, so both come back zero.
        models.UpdateRuleBasedDistresses(segment, segment.SurfaceAge);
    }

    /// <summary>
    /// Applies a treatment that renews the surface but not the pavement - a reseal, a thin asphalt
    /// overlay, or a preseal repair that renews neither.
    ///
    /// <para>THERE IS ALMOST NOTHING TO DO HERE, AND THAT IS THE DESIGN. The clocks were moved by the
    /// caller, and the models read them: cracking comes back to what a new surface carries, asphalt
    /// rutting to about 36% of where it was, roughness to 74% on asphalt and 92% on chipseal, and
    /// chipseal rutting carries straight on because its own accumulator was left alone.</para>
    ///
    /// <para>Cracking is NOT forced to zero, and that is deliberate: the fitted model puts 36% of
    /// asphalt and 31% of chipseal above the threshold on a brand new surface, which is reflective
    /// cracking coming through from the old pavement below. Forcing zero would delete a real effect.</para>
    ///
    /// <para>The persistent draws are all retained - that is what makes a segment keep its own character
    /// across a reseal - except the held sub-threshold cracking value, which is redrawn with the new
    /// surface. One consequence, so it is not mistaken for a bug: a segment below the threshold can
    /// come out very slightly higher after a reseal, bounded by one percentage point, because both
    /// values are sub-threshold by construction. A run that asserts condition never worsens across a
    /// treatment should apply that assertion at or above the onset threshold only.</para>
    ///
    /// <para>A PRE-REPAIR'S EFFECT IS ALREADY DECIDED BY THE TIME THIS RUNS. Its credit was computed
    /// by the caller before the clocks moved, and the models read it wherever they read the deviate, so
    /// there is nothing to apply here either. What this method does add is the guard: a pre-repair that
    /// arrives WITH an overlay resets the clock as well as crediting the deviate, and the two together
    /// must not compound to a condition better than a full rebuild.</para>
    /// </summary>
    private void ApplySurfaceTreatment(RoadSegment segment, bool renewsSurface, bool isPreRepair, Random random)
    {
        DeteriorationModels models = _domainModel.DeteriorationModels;

        if (renewsSurface)
        {
            segment.CrackingBelowOnset = models.DrawCrackingBelowOnset(segment, random);
        }

        models.UpdateConditions(segment, segment.SurfaceAge, applyPreRepairGuard: isPreRepair);
        models.UpdateRuleBasedDistresses(segment, segment.SurfaceAge);
    }

    #endregion

    /// <summary>
    /// The surface function the segment carries after the treatment.
    /// <para>'1a' means preseal repairs are down and the seal over them has not been laid yet. It is
    /// load-bearing rather than descriptive: the candidate selector reads it to force the follow-up
    /// ChipSeal_H, the trigger reads it to avoid stacking a second lot of repairs on top, and the
    /// expected-life lookup falls back to the reseal life because repairs have no life of their own.
    /// A pre-repair that comes WITH an overlay is a real resurfacing and must not take it.</para>
    /// </summary>
    private string GetSurfaceFunction(string treatmentName, bool renewsSurface, string currentSurfaceFunction)
    {
        if (!renewsSurface) return "1a";

        if (treatmentName.ToLower().StartsWith("rehab_ac")) return "2";

        if (treatmentName.ToLower().StartsWith("rehab_cs")) return "1";

        if (currentSurfaceFunction == "1a") return "H";

        if (currentSurfaceFunction == "1") return "2";

        if (currentSurfaceFunction == "2") return "R";

        // If none of the others are fired, return the current surface function, e.g surface is aready a "R"
        return currentSurfaceFunction;


    }

    private double GetExpectedSurfaceLife(RoadSegment segment)
    {
        if (segment.SurfaceClass == "blocks") return segment.SurfaceExpectedLife; // Blocks have a fixed expected life, no lookup needed
        if (segment.SurfaceClass == "concrete") return segment.SurfaceExpectedLife; // Concrete has a fixed expected life, no lookup needed
        if (segment.SurfaceClass == "other") return segment.SurfaceExpectedLife;

        // Preseal repairs have no expected life of their own - they are a holding action waiting for
        // the seal that follows. So base the expected life on the Reseal 'R' surface function.
        string surfFuncToUse = segment.SurfaceFunction == "1a" ? "R" : segment.SurfaceFunction;

        string lookupKey = $"{surfFuncToUse}_{segment.SurfaceMaterial}_{segment.RoadClass}".ToLower();
        bool keyExists = _frameworkModel.Lookups["surf_life_exp"].ContainsKey(lookupKey);
        if (keyExists)
        {
            return _frameworkModel.GetLookupValueNumber("surf_life_exp", lookupKey);
        }
        else
        {
            throw new KeyNotFoundException($"Expected surface life not found for {segment.FeebackCode}. Surface function = '{segment.SurfaceFunction}', Material = '{segment.SurfaceMaterial}', Road class = '{segment.RoadClass}'.");
        }
    }

}
