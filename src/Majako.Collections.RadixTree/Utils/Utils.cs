using System.Runtime.CompilerServices;

namespace Majako.Collections.RadixTree;

internal static class Utils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int GetCommonPrefixLength(ReadOnlySpan<char> s1, ReadOnlySpan<char> s2)
    {
        var i = 0;
        var minLength = Math.Min(s1.Length, s2.Length);

        while (i < minLength && s2[i] == s1[i])
            i++;

        return i;
    }
}
