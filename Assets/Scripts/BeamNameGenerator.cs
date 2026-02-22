using System;
using System.Text;

/// <summary>
/// Deterministic two-syllable name generator from a stable seed or index.
/// </summary>
public static class BeamNameGenerator
{
    private static readonly string[] Consonants = new[]
    {
        "b","c","d","f","g","h","j","k","l","m",
        "n","p","r","s","t","v","w","y","z","th"
    };

    private static readonly string[] Vowels = new[]
    {
        "a","e","i","o","u"
    };

    public static string GetNameFromSeed(int seed)
    {
        int index = HashToIndex(seed);
        return GetNameFromIndex(index);
    }

    public static string GetNameFromIndex(int index)
    {
        int total = Consonants.Length * Vowels.Length * Consonants.Length * Vowels.Length;
        if (total <= 0) return "Anon";

        int normalized = ((index % total) + total) % total;

        int v2Count = Vowels.Length;
        int c2Count = Consonants.Length;
        int v1Count = Vowels.Length;
        int c1Count = Consonants.Length;

        int v2 = normalized % v2Count;
        normalized /= v2Count;
        int c2 = normalized % c2Count;
        normalized /= c2Count;
        int v1 = normalized % v1Count;
        normalized /= v1Count;
        int c1 = normalized % c1Count;

        string raw = $"{Consonants[c1]}{Vowels[v1]}{Consonants[c2]}{Vowels[v2]}";
        return char.ToUpperInvariant(raw[0]) + raw.Substring(1);
    }

    private static int HashToIndex(int seed)
    {
        uint hash = Fnv1a(seed, "beamName");
        return (int)(hash % 10000);
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
