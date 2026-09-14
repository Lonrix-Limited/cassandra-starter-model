
namespace StarterModel.Objects;

/// <summary>
/// The treatment names this model can produce. These strings are a contract with two files outside
/// the C# project and must match both exactly:
///
/// <list type="bullet">
///   <item><description>the <c>treatment_name</c> column of the <c>treatments</c> sheet in
///   <c>domain_model_setup.xlsx</c>;</description></item>
///   <item><description>the <c>setting_key</c> column of the <c>unit_rates_general</c> set in the
///   <c>lkp_unit_rates</c> sheet of the client's <c>inputs\lookups.xlsx</c>, and the
///   <c>treat_surf_class</c> / <c>treat_surf_materials</c> sets that the Resetter reads on every
///   treatment.</description></item>
/// </list>
///
/// <para>THE NAMES ARE WRITTEN OUT IN FULL HERE AND NEVER ASSEMBLED FROM PIECES, which matters for a
/// reason beyond readability: <c>jcass-dm check</c> compares the bundle against the C# as text. A name
/// built at run time as <c>family + "_rehab"</c> is invisible to it, and the treatment would be
/// reported as declared-but-never-produced for as long as the model exists.</para>
///
/// <para>THREE SURFACING FAMILIES, NOT TWO. Chipseal, asphalt and OGPA each carry their own set of
/// names. Which family a treatment belongs to is decided in <see cref="TreatmentsTrigger"/>: the route
/// through the trigger follows the segment's NEXT surface, but the name follows its CURRENT surface
/// class, because <c>inp_next_surf</c> has no value for OGPA and the current class is the only place
/// the model knows OGPA exists. One consequence of that, worth knowing: an OGPA segment stays OGPA for
/// as long as its next surface is not chipseal, since the Resetter writes the class back from
/// <c>treat_surf_class</c>. A segment whose <c>inp_next_surf</c> reads 'cs' takes the chipseal route
/// instead, and comes back from that lookup as plain chipseal.</para>
/// </summary>
public static class TreatmentNames
{
    #region Chipseal

    /// <summary>Chipseal resurfacing with minimal or no structural repairs. Budget: Resurfacing.</summary>
    public const string ChipsealResurfacing = "cs_resurf";

    /// <summary>
    /// Preseal repairs on a chipseal road. A PRE-REPAIR: it lays no chip, so it renews no surface and
    /// does not move the surface age. It leaves the segment on surface function '1a', which is what
    /// forces <see cref="ChipsealSecondCoatAfterPreseal"/> to follow it. Budget: Pre-Repairs.
    /// </summary>
    public const string ChipsealPresealRepairs = "cs_preseal";

    /// <summary>
    /// The seal laid over preseal repairs, a year or two after them. Forced, not a candidate.
    /// Budget: Resurfacing.
    /// </summary>
    public const string ChipsealSecondCoatAfterPreseal = "cs_2nd_coat_h";

    /// <summary>
    /// The second coat that follows a chipseal rehabilitation. Forced, not a candidate.
    /// Budget: Resurfacing.
    /// </summary>
    public const string ChipsealSecondCoatAfterRehab = "cs_2nd_coat_r";

    /// <summary>
    /// Chipseal rehabilitation - a pavement rebuild, not a resurfacing. Priced from the
    /// <c>cs_rehab_rate</c> lookup set, by ONRC category. Budget: Rehab.
    /// </summary>
    public const string ChipsealRehabilitation = "cs_rehab";

    #endregion

    #region Asphalt

    /// <summary>Thin asphalt resurfacing, no or minimal repairs. Budget: Resurfacing.</summary>
    public const string AsphaltResurfacing = "ac_resurf";

    /// <summary>
    /// Asphalt inlay or overlay INCLUDING heavy maintenance repairs, in the same year. The one
    /// arrangement that is a pre-repair and a resurfacing at once, which is why its cost is split
    /// across two budget categories and its rate in <c>unit_rates_general</c> is the sentinel
    /// <c>N/A</c> - the cost is carried in the quantity. Budget: Pre-Repairs (split).
    /// </summary>
    public const string AsphaltHolding = "ac_holding";

    /// <summary>
    /// Heavy maintenance on asphalt, on its own. A PRE-REPAIR: repairs alone, no surfacing, so it
    /// renews no surface and does not move the surface age. Budget: Pre-Repairs.
    /// </summary>
    public const string AsphaltHeavyMaintenance = "ac_hmaint";

    /// <summary>
    /// Asphalt rehabilitation. Priced from the <c>ac_rehab_rate</c> lookup set, by ONRC category.
    /// Budget: Rehab.
    /// </summary>
    public const string AsphaltRehabilitation = "ac_rehab";

    #endregion

    #region OGPA

    /// <summary>OGPA resurfacing, no or minimal repairs. Budget: Resurfacing.</summary>
    public const string OgpaResurfacing = "ogpa_resurf";

    /// <summary>
    /// OGPA inlay or overlay INCLUDING heavy maintenance repairs. The OGPA counterpart of
    /// <see cref="AsphaltHolding"/>, and its rate is the same <c>N/A</c> sentinel for the same reason.
    /// Budget: Pre-Repairs (split).
    /// </summary>
    public const string OgpaHolding = "ogpa_holding";

    /// <summary>Heavy maintenance on OGPA, on its own. A PRE-REPAIR. Budget: Pre-Repairs.</summary>
    public const string OgpaHeavyMaintenance = "ogpa_hmaint";

    /// <summary>
    /// OGPA rehabilitation. Priced from the <c>ogpa_rehab_rate</c> lookup set, by ONRC category.
    /// Budget: Rehab.
    /// </summary>
    public const string OgpaRehabilitation = "ogpa_rehab";

    #endregion

    #region Birthday treatments

    /// <summary>Block paving repairs, triggered at the end of the expected life. Budget: BlockRep.</summary>
    public const string BlockRepairs = "blocks";

    /// <summary>Concrete paving repairs, triggered at the end of the expected life. Budget: ConcRep.</summary>
    public const string ConcreteRepairs = "concrete";

    /// <summary>
    /// Repairs to any other surfacing, triggered at the end of the expected life. Budget: Xtreat.
    /// </summary>
    public const string OtherRepairs = "xtreat";

    #endregion

    /// <summary>
    /// Routine maintenance. Declared in the bundle and rated in <c>unit_rates_general</c>, but THIS
    /// MODEL DOES NOT PRODUCE IT: <c>StarterModel.GetTriggeredMaintenance</c> returns null, which is
    /// the framework's documented contract for a model that is not costing routine maintenance.
    /// <para>The name is kept here because the trigger reads it when it looks back over previous
    /// treatments to find the last non-routine one, and because the Resetter names it in the message
    /// it throws if one ever arrives.</para>
    /// </summary>
    public const string RoutineMaintenance = "RMaint";
}
