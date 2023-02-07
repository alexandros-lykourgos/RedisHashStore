using System.Collections.Generic;
using StackExchange.Redis;

namespace Common.Redis;

public static class RedisExtensions
{
    public static HashEntry[] ToRedisHashSetEntries(this Dictionary<string,string> input)
    {
        var hashEntries = new HashEntry[input.Count];
        int index = 0;

        foreach (var entry in input)
        {
            hashEntries[index++] = new HashEntry(entry.Key, entry.Value);
        }

        return hashEntries;
    }
}