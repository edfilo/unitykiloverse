using System;
using System.Text;

/// <summary>
/// Deterministic per-beam attributes derived from a single seed.
/// Each field is hashed independently so adding new fields later
/// does not change existing values.
/// </summary>
public struct BeamAttributes
{
    public int a;
    public int b;
    public int c;
    public int d;
    public int e;
    public int f;
    public int g;
    public int h;
    public int i;
    public int j;

    public static BeamAttributes FromSeed(int seed)
    {
        return new BeamAttributes
        {
            a = HashToRange(seed, "a", 0, 1000),
            b = HashToRange(seed, "b", 0, 1000),
            c = HashToRange(seed, "c", 0, 1000),
            d = HashToRange(seed, "d", 0, 1000),
            e = HashToRange(seed, "e", 0, 1000),
            f = HashToRange(seed, "f", 0, 1000),
            g = HashToRange(seed, "g", 0, 1000),
            h = HashToRange(seed, "h", 0, 1000),
            i = HashToRange(seed, "i", 0, 1000),
            j = HashToRange(seed, "j", 0, 1000)
        };
    }

    private static int HashToRange(int seed, string label, int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive)
        {
            int tmp = minInclusive;
            minInclusive = maxInclusive;
            maxInclusive = tmp;
        }

        uint hash = Fnv1a(seed, label);
        uint range = (uint)(maxInclusive - minInclusive + 1);
        return (int)(hash % range) + minInclusive;
    }

    private static uint Fnv1a(int seed, string label)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;

        unchecked
        {
            uint hash = offset;
            byte[] seedBytes = BitConverter.GetBytes(seed);
            for (int i = 0; i < seedBytes.Length; i++)
            {
                hash ^= seedBytes[i];
                hash *= prime;
            }

            if (!string.IsNullOrEmpty(label))
            {
                byte[] labelBytes = Encoding.UTF8.GetBytes(label);
                for (int i = 0; i < labelBytes.Length; i++)
                {
                    hash ^= labelBytes[i];
                    hash *= prime;
                }
            }

            return hash;
        }
    }
}
