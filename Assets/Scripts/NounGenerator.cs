using System;
using System.Text;

/// <summary>
/// Shared deterministic noun list used by beams and locations.
/// </summary>
public static class NounGenerator
{
    private static readonly string[] Nouns = new[]
    {
        "soldier","guard","ranger","scout","smith","watcher","nomad","sage","scribe","pilot",
        "courier","drifter","keeper","artisan","finder","seer","mage","hunter","herald","warden",
        "wolf","fox","hawk","falcon","raven","lion","tiger","panther","lynx","otter",
        "badger","hare","stag","boar","cougar","elk","bison","shark","orca","kite",
        "cactus","fern","moss","pine","oak","birch","willow","cypress","cedar","laurel",
        "spruce","bamboo","reed","juniper","thistle","lavender","bloom","vine","briar","orchid",
        "sailor","bard","navigator","miner","tinkerer","marshal","captain","archer","knight","envoy",
        "poet","sentinel","emissary","healer","paladin","monk","heron","manta","jaguar","puma",
        "cormorant","stallion","ram","coyote","wolverine","harrier","ibex","ocelot","bittern","gopher",
        "lotus","aloe","ivy","agave","sagebrush","clover","eucalyptus","thicket","dune","canyon"
    };

    public static string GetFromSeed(int seed)
    {
        if (Nouns.Length == 0) return "unknown";
        uint hash = Fnv1a(seed, "noun");
        int index = (int)(hash % (uint)Nouns.Length);
        return Nouns[index];
    }

    public static string GetFromKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return "unknown";
        uint hash = Fnv1a(0, key);
        int index = (int)(hash % (uint)Nouns.Length);
        return Nouns[index];
    }

    private static uint Fnv1a(int seed, string label)
    {
        const uint offset = 2166136261u;
        const uint prime = 16777619u;
        unchecked
        {
            uint hash = offset;
            byte[] seedBytes = BitConverter.GetBytes(seed);
            foreach (byte b in seedBytes)
            {
                hash ^= b;
                hash *= prime;
            }

            if (!string.IsNullOrEmpty(label))
            {
                byte[] labelBytes = Encoding.UTF8.GetBytes(label);
                foreach (byte b in labelBytes)
                {
                    hash ^= b;
                    hash *= prime;
                }
            }

            return hash;
        }
    }
}
