using JCass_Data.Utils;
using JCass_Data.Objects;
using JCass_ModelCore.Models;

namespace StarterModel.Objects;

/// <summary>
/// The fitted coefficients behind the deterioration models, loaded once at setup from the client's
/// 'supporting' folder.
/// <para>These are a FITTED SET, regenerated as a whole by a refit, which is why they live in CSV
/// files rather than in lookups.xlsx. The single tunable numbers that go with them - the age cap, the
/// onset threshold, the feedback switches - are lookup values and are read by the Constants class.</para>
/// <para>Ten files, two per sub-model part, one for each surface class group. They share one column
/// layout, produced by the R fit: a 'term' column and an 'estimate' column, plus diagnostics this
/// model does not read.</para>
/// </summary>
public class DeteriorationCoefficients
{
    /// <summary>Folder, relative to the client work folder, holding the fitted coefficient files.</summary>
    private const string SupportingFolder = "supporting";

    /// <summary>Column in each coefficient file naming the model term.</summary>
    private const string TermColumn = "term";

    /// <summary>Column in each coefficient file holding the fitted value.</summary>
    private const string EstimateColumn = "estimate";

    /// <summary>Part A of the cracking model - probability that cracking has started - by group.</summary>
    public Dictionary<string, FittedModel> CrackOnset { get; } = new Dictionary<string, FittedModel>();

    /// <summary>Part B of the cracking model - how much cracking, given it has started - by group.</summary>
    public Dictionary<string, FittedModel> CrackSeverity { get; } = new Dictionary<string, FittedModel>();

    /// <summary>Part C of the cracking model - the sub-threshold distribution - by group.</summary>
    public Dictionary<string, FittedModel> CrackBelow { get; } = new Dictionary<string, FittedModel>();

    /// <summary>The rutting level model, by group.</summary>
    public Dictionary<string, FittedModel> Rut { get; } = new Dictionary<string, FittedModel>();

    /// <summary>The roughness (IRI) level model, by group.</summary>
    public Dictionary<string, FittedModel> Iri { get; } = new Dictionary<string, FittedModel>();

    /// <summary>
    /// Loads all ten coefficient files. Call this from SetupInstance, where the model configuration
    /// and therefore the work folder are available.
    /// </summary>
    /// <param name="frameworkModel">Framework model, used for its configuration work folder</param>
    public DeteriorationCoefficients(ModelBase frameworkModel)
    {
        string workFolder = frameworkModel.Configuration.WorkFolder;

        foreach (string group in DeteriorationModels.ModelGroups)
        {
            // The onset model is logistic and has no sigma: its randomness is the segment's uniform
            // onset draw. Every lognormal file must carry one.
            this.CrackOnset[group] = Load(workFolder, $"logistic_crack_onset_{group}.csv", requiresSigma: false);
            this.CrackSeverity[group] = Load(workFolder, $"lognormal_crack_severity_{group}.csv", requiresSigma: true);
            this.CrackBelow[group] = Load(workFolder, $"lognormal_crack_below_{group}.csv", requiresSigma: true);
            this.Rut[group] = Load(workFolder, $"lognormal_rut_{group}.csv", requiresSigma: true);
            this.Iri[group] = Load(workFolder, $"lognormal_iri_{group}.csv", requiresSigma: true);
        }
    }

    /// <summary>
    /// Reads one coefficient file into a FittedModel. Guards the read and names the full path, because
    /// an unguarded missing setup file surfaces much later as a wrong number rather than as a missing file.
    /// </summary>
    private static FittedModel Load(string workFolder, string fileName, bool requiresSigma)
    {
        string path = Path.Combine(workFolder, SupportingFolder, fileName);
        if (!File.Exists(path))
        {
            throw new Exception($"Deterioration coefficient file not found at: {path}. The fitted " +
                                $"coefficient files belong in the client's '{SupportingFolder}' folder, " +
                                $"uploaded on the Files page.");
        }

        jcDataSet coefficients = CSVHelper.ReadDataFromCsvFile(path, keyColumn: null);
        coefficients.CheckRequiredColumns(new List<string> { TermColumn, EstimateColumn }, throwErrorIfNotFound: true);

        Dictionary<string, string> rawTerms = coefficients.GetKeysAndValuesFromColumn(TermColumn, EstimateColumn);
        return new FittedModel(rawTerms, fileName, requiresSigma);
    }
}

/// <summary>
/// One fitted model: its linear predictor terms, and where the family needs them, a sigma and a share
/// of exact zeros.
/// <para>Why this rather than the framework's LogisticModel: all ten coefficient files share one
/// format, and eight of them are lognormal rather than logistic. LogisticModel also requires each
/// coefficient name to match a key in a predictor dictionary supplied per call, and these files name
/// their terms as the R formula wrote them - "log(surf_age_yrs + 0.5), surf_age_yrs capped at 30".
/// One class reading all ten uniformly is easier to follow than two mechanisms. The coefficients still
/// come from a CSV and never from C#, which is the rule that matters.</para>
/// </summary>
public class FittedModel
{
    private readonly Dictionary<string, double> _coefficients;
    private readonly string _sourceFile;
    private readonly double? _sigma;

    /// <summary>
    /// Residual standard deviation of the fit, scaling the segment's persistent multiplier. A logistic
    /// fit has none, and reading it there throws rather than returning zero - a zero spread would make
    /// every segment identical with nothing reporting it.
    /// </summary>
    public double Sigma => _sigma ?? throw new InvalidOperationException(
        $"Coefficient file '{_sourceFile}' is a fit with no sigma, and the model asked for one. " +
        $"Its randomness comes from elsewhere - check which sub-model the calling code meant to read.");

    /// <summary>
    /// Share of segments in the sub-threshold group whose cracking is exactly zero. Only the Part C
    /// cracking files carry this; it is zero elsewhere and nothing reads it there.
    /// </summary>
    public double ZeroShare { get; }

    /// <summary>
    /// Builds a fitted model from the raw term-to-estimate pairs of one coefficient file. Every term is
    /// translated to a canonical name, and an unrecognised term throws rather than being skipped - a
    /// refit that adds a covariate must be noticed here, not silently dropped from the forecast.
    /// </summary>
    /// <param name="rawTerms">Term name to estimate, as read from the file</param>
    /// <param name="sourceFile">File name, used in error messages</param>
    /// <param name="requiresSigma">True for a lognormal fit, which must carry a positive '(Sigma)' term</param>
    public FittedModel(Dictionary<string, string> rawTerms, string sourceFile, bool requiresSigma)
    {
        _sourceFile = sourceFile;
        _coefficients = new Dictionary<string, double>();

        double? sigma = null;
        double zeroShare = 0.0;

        foreach (KeyValuePair<string, string> rawTerm in rawTerms)
        {
            string canonical = Canonicalise(rawTerm.Key, sourceFile);
            double estimate = Convert.ToDouble(rawTerm.Value);

            if (canonical == TermSigma) { sigma = estimate; }
            else if (canonical == TermZeroShare) { zeroShare = estimate; }
            else { _coefficients[canonical] = estimate; }
        }

        if (requiresSigma && !(sigma > 0.0))
        {
            throw new Exception($"Coefficient file '{sourceFile}' has no positive '(Sigma)' term. Every " +
                                $"lognormal sub-model needs one: it scales the segment's persistent multiplier, " +
                                $"and a sigma of zero would make every segment identical with nothing reporting it.");
        }

        _sigma = requiresSigma ? sigma : null;
        this.ZeroShare = zeroShare;
    }

    /// <summary>
    /// Evaluates the linear predictor for one segment. A term the fit does not contain contributes
    /// nothing, which is deliberate in three places: deflection is absent from chipseal cracking onset
    /// and from chipseal cracking severity, and surface age is absent from chipseal rutting. Those
    /// omissions are findings, not gaps - see the specification.
    /// </summary>
    /// <param name="logAge">log(min(surface age, cap) + offset)</param>
    /// <param name="logAdt">log(max(average daily traffic, 1)), at the SURVEYED traffic</param>
    /// <param name="centralDeflection">Central deflection D0, in mm</param>
    /// <param name="heavyVehiclePercent">Heavy vehicle percentage</param>
    public double LinearPredictor(double logAge, double logAdt, double centralDeflection, double heavyVehiclePercent)
    {
        double result = Coefficient(TermIntercept);
        result += Coefficient(TermLogAge) * logAge;
        result += Coefficient(TermLogAdt) * logAdt;
        result += Coefficient(TermDeflection) * centralDeflection;
        result += Coefficient(TermHeavyPercent) * heavyVehiclePercent;
        return result;
    }

    /// <summary>
    /// Returns the estimate for a canonical term, or zero if this fit does not include it.
    /// </summary>
    private double Coefficient(string canonicalTerm)
    {
        return _coefficients.TryGetValue(canonicalTerm, out double value) ? value : 0.0;
    }

    /// <summary>
    /// Translates a term name as the R fit wrote it into the canonical name this model uses. Throws,
    /// naming the file and the term, on anything unrecognised.
    /// </summary>
    private static string Canonicalise(string rawTerm, string sourceFile)
    {
        string term = rawTerm.Trim();

        if (term == "(Intercept)") { return TermIntercept; }
        if (term == "(Sigma)") { return TermSigma; }
        if (term == "(ZeroShare)") { return TermZeroShare; }
        if (term.StartsWith("log(surf_age_yrs", StringComparison.OrdinalIgnoreCase)) { return TermLogAge; }
        if (term.StartsWith("log(max(inp_adt", StringComparison.OrdinalIgnoreCase)) { return TermLogAdt; }
        if (term == "inp_lmd_d0_75th") { return TermDeflection; }
        if (term == "inp_heavy_perc") { return TermHeavyPercent; }

        throw new Exception($"Coefficient file '{sourceFile}' contains the term '{rawTerm}', which this " +
                            $"model does not know how to evaluate. A refit that introduces a new covariate " +
                            $"needs a matching change in FittedModel.LinearPredictor - it is deliberately " +
                            $"an error rather than a silently ignored term.");
    }

    private const string TermIntercept = "intercept";
    private const string TermLogAge = "log_age";
    private const string TermLogAdt = "log_adt";
    private const string TermDeflection = "d0";
    private const string TermHeavyPercent = "heavy_perc";
    private const string TermSigma = "sigma";
    private const string TermZeroShare = "zero_share";
}
