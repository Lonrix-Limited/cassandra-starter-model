namespace StarterModel.Objects;

/// <summary>
/// The random number generator for ONE segment in ONE modelling period, derived from the run's seed.
///
/// <para>WHY NOT THE SHARED GENERATOR. The framework does not process the elements of a single model
/// in parallel - only separate model runs go in parallel, and each of those carries its own seed - so
/// this is not a thread safety fix and must not be described as one. It is about which draws an
/// element receives.</para>
///
/// <para>Drawing every segment's deviates from one shared generator makes each segment's draws depend
/// on how many draws every segment before it happened to take. Those counts are not fixed: a segment
/// with no visual survey takes a different number from one with a reading, a rehabilitation takes more
/// than a reseal, and adding a draw site anywhere shifts the whole network downstream of it. The
/// result still reproduces from the seed, but it is fragile in a way nothing reports - reordering the
/// input file, or adding one draw to one branch, changes every later segment's character.</para>
///
/// <para>Deriving the seed from the run seed, the element index and the period gives each element its
/// own independent stream, the same stream whatever order the elements are processed in, and the same
/// stream whatever the code does to any other element. Change the run seed and the whole network
/// changes, which is what a seed is for.</para>
/// </summary>
public static class SegmentRandom
{
    /// <summary>
    /// Builds the generator for one segment in one period.
    ///
    /// <para>The mixing below is a standard 64-bit avalanche. It is not decoration: seeding
    /// <c>System.Random</c> with a raw sum of the three inputs would leave neighbouring elements with
    /// visibly similar first draws, which is exactly the correlation a per-element stream exists to
    /// avoid.</para>
    /// </summary>
    /// <param name="runSeed">The run's configured seed, from ModelBase.RandomSeed. Do NOT pass a
    /// clock-derived value: the seed is what makes the run reproducible.</param>
    /// <param name="elementIndex">Zero-based index of the element</param>
    /// <param name="period">Modelling period. Initialisation uses 0, so a segment's year-zero stream
    /// can never coincide with the stream a treatment in some later period draws from.</param>
    public static Random ForSegment(int runSeed, int elementIndex, int period)
    {
        unchecked
        {
            ulong z = (ulong)(uint)runSeed * 0x9E3779B97F4A7C15UL
                    + (ulong)(uint)elementIndex * 0xBF58476D1CE4E5B9UL
                    + (ulong)(uint)period * 0x94D049BB133111EBUL;

            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;

            return new Random((int)(z & 0x7FFFFFFFUL));
        }
    }
}
