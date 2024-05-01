namespace OpenTap.Metrics;

static class HashCode
{
    private const long Prime1 = 2654435761U;
    private const long Prime2 = 2246822519U;
    private const long Prime3 = 3266489917U;
    private const long Prime4 = 668265263U;

    public static long Combine(long a, long b) => (a + Prime2) * Prime1 + (b + Prime3) * Prime4;
    public static long GetHashCodeLong(this object x) => x?.GetHashCode() ?? 0;

    public static int Combine<T1, T2>(T1 a, T2 b, long seed = 0) =>
        (int)Combine(a.GetHashCodeLong(), Combine(b.GetHashCodeLong(), seed));

    public static int Combine<T1, T2, T3>(T1 a, T2 b, T3 c) =>
        Combine(Combine(a, b), c);

    public static int Combine<T1, T2, T3, T4>(T1 a, T2 b, T3 c, T4 d) =>
        Combine(Combine(a, b, c), d);
}