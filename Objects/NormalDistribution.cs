namespace StarterModel.Objects;

/// <summary>
/// Standard normal distribution function and its inverse.
/// <para>These are STRUCTURAL constants, not tunable numbers, and that is why they are in C# rather
/// than in lookups.xlsx. They are the published rational approximations to a mathematical function:
/// changing one would not change the forecast, it would make the code wrong. The deterioration models
/// need both because their lognormal parts are truncated, and a truncated distribution is sampled by
/// mapping a uniform draw through the inverse CDF - see DeteriorationModels.</para>
/// <para>The framework's NormalGenerator draws normal variates but exposes neither the CDF nor the
/// quantile function, which is why this class exists.</para>
/// </summary>
public static class NormalDistribution
{
    /// <summary>
    /// Standard normal cumulative distribution function - the probability that a standard normal
    /// variate is at most <paramref name="z"/>. Returns a value in (0, 1).
    /// </summary>
    /// <param name="z">Standard normal deviate</param>
    public static double Phi(double z)
    {
        // Phi(z) = 0.5 * erfc(-z / sqrt(2)). Uses the complementary error function below so that the
        // far left tail keeps its precision rather than cancelling against 1.
        return 0.5 * Erfc(-z / Math.Sqrt(2.0));
    }

    /// <summary>
    /// Inverse of the standard normal cumulative distribution function: returns the deviate z for
    /// which Phi(z) equals <paramref name="p"/>.
    /// </summary>
    /// <param name="p">Probability, strictly between 0 and 1. Values at or outside the bounds are
    /// nudged inside it rather than returning an infinity, because an infinite deviate would
    /// propagate into a segment's condition and show up much later as a meaningless number.</param>
    public static double PhiInverse(double p)
    {
        // Guard the endpoints. A truncation point can legitimately land on 0 or 1 in double precision
        // when the truncated tail is vanishingly small, and the caller wants an extreme deviate, not
        // an infinity that poisons every later calculation.
        if (p <= 0.0) { p = SmallestProbability; }
        if (p >= 1.0) { p = 1.0 - SmallestProbability; }

        // Acklam's algorithm: a rational approximation in three regions, refined by one step of
        // Halley's method against Erfc. Accurate to about 1e-15 over the whole range.
        double q;
        double r;
        double x;

        if (p < LowerRegionBound)
        {
            q = Math.Sqrt(-2.0 * Math.Log(p));
            x = (((((TailC[0] * q + TailC[1]) * q + TailC[2]) * q + TailC[3]) * q + TailC[4]) * q + TailC[5]) /
                ((((TailD[0] * q + TailD[1]) * q + TailD[2]) * q + TailD[3]) * q + 1.0);
        }
        else if (p <= UpperRegionBound)
        {
            q = p - 0.5;
            r = q * q;
            x = (((((CentreA[0] * r + CentreA[1]) * r + CentreA[2]) * r + CentreA[3]) * r + CentreA[4]) * r + CentreA[5]) * q /
                (((((CentreB[0] * r + CentreB[1]) * r + CentreB[2]) * r + CentreB[3]) * r + CentreB[4]) * r + 1.0);
        }
        else
        {
            q = Math.Sqrt(-2.0 * Math.Log(1.0 - p));
            x = -(((((TailC[0] * q + TailC[1]) * q + TailC[2]) * q + TailC[3]) * q + TailC[4]) * q + TailC[5]) /
                 ((((TailD[0] * q + TailD[1]) * q + TailD[2]) * q + TailD[3]) * q + 1.0);
        }

        // One Halley refinement step, which is what takes the approximation to full double precision.
        double e = 0.5 * Erfc(-x / Math.Sqrt(2.0)) - p;
        double u = e * Math.Sqrt(2.0 * Math.PI) * Math.Exp(x * x / 2.0);
        x = x - u / (1.0 + x * u / 2.0);

        return x;
    }

    /// <summary>
    /// Complementary error function, erfc(x) = 1 - erf(x), by the Numerical Recipes Chebyshev fit.
    /// Relative error is below 1.2e-7 across the whole range, which is well inside what the
    /// deterioration models need.
    /// </summary>
    private static double Erfc(double x)
    {
        double z = Math.Abs(x);
        double t = 2.0 / (2.0 + z);
        double ty = 4.0 * t - 2.0;

        double d = 0.0;
        double dd = 0.0;
        for (int j = ErfcCoefficients.Length - 1; j > 0; j--)
        {
            double tmp = d;
            d = ty * d - dd + ErfcCoefficients[j];
            dd = tmp;
        }
        double result = t * Math.Exp(-z * z + 0.5 * (ErfcCoefficients[0] + ty * d) - dd);

        return x >= 0.0 ? result : 2.0 - result;
    }

    /// <summary>
    /// Smallest probability accepted by PhiInverse. Corresponds to a deviate of roughly -8.2, far
    /// beyond the plus/minus 2 the deterioration models clamp to.
    /// </summary>
    private const double SmallestProbability = 1e-16;

    /// <summary>Probability below which Acklam's lower tail branch is used.</summary>
    private const double LowerRegionBound = 0.02425;

    /// <summary>Probability above which Acklam's upper tail branch is used.</summary>
    private const double UpperRegionBound = 1.0 - LowerRegionBound;

    private static readonly double[] CentreA =
    {
        -3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02,
         1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00
    };

    private static readonly double[] CentreB =
    {
        -5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02,
         6.680131188771972e+01, -1.328068155288572e+01
    };

    private static readonly double[] TailC =
    {
        -7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00,
        -2.549732539343734e+00,  4.374664141464968e+00,  2.938163982698783e+00
    };

    private static readonly double[] TailD =
    {
        7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00, 3.754408661907416e+00
    };

    private static readonly double[] ErfcCoefficients =
    {
        -1.3026537197817094, 6.4196979235649026e-1, 1.9476473204185836e-2, -9.561514786808631e-3,
        -9.46595344482036e-4, 3.66839497852761e-4, 4.2523324806907e-5, -2.0278578112534e-5,
        -1.624290004647e-6, 1.303655835580e-6, 1.5626441722e-8, -8.5238095915e-8,
         6.529054439e-9, 5.059343495e-9, -9.91364156e-10, -2.27365122e-10,
         9.6467911e-11, 2.394038e-12, -6.886027e-12, 8.94487e-13,
         3.13092e-13, -1.12708e-13, 3.81e-16, 7.106e-15
    };
}
