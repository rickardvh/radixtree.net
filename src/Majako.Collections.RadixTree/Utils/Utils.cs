using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Majako.Collections.RadixTree;

internal static class Utils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int GetCommonPrefixLength(ReadOnlySpan<char> s1, ReadOnlySpan<char> s2)
    {
        var minLength = Math.Min(s1.Length, s2.Length);
        var i = 0;

        if (minLength >= 4)
        {
            ref var s1Ref = ref MemoryMarshal.GetReference(s1);
            ref var s2Ref = ref MemoryMarshal.GetReference(s2);

            while (i <= minLength - 4)
            {
                var lhs = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<char, byte>(ref Unsafe.Add(ref s1Ref, i)));
                var rhs = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<char, byte>(ref Unsafe.Add(ref s2Ref, i)));
                if (lhs != rhs)
                    break;

                i += 4;
            }
        }

        while (i < minLength && s1[i] == s2[i])
            i++;

        return i;
    }
}
