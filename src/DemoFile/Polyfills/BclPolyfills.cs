using System.Diagnostics;

namespace DemoFile;

/// <summary>
/// .NET 6+ BCL members used by DemoFile that are missing from netstandard2.1.
/// Static members use C# 14 extension blocks, so call sites such as <c>Math.Log2(x)</c> compile unchanged.
/// </summary>
internal static class BclPolyfills
{
    extension(Math)
    {
        public static (int Quotient, int Remainder) DivRem(int left, int right)
        {
            var quotient = left / right;
            return (quotient, left - quotient * right);
        }

        /// <summary>
        /// Exact for powers of two (callers truncate or ceil the result), otherwise <c>e + log2(mantissa)</c>.
        /// </summary>
        public static double Log2(double x)
        {
            var bits = BitConverter.DoubleToInt64Bits(x);
            var biasedExponent = (int)((bits >> 52) & 0x7FF);
            if (x <= 0 || biasedExponent == 0 || biasedExponent == 0x7FF)
            {
                // Zero, negative, subnormal, infinity or NaN: no exact shortcut needed
                return Math.Log(x) / Ln2;
            }

            var exponent = biasedExponent - 1023;
            var mantissaBits = bits & 0xFFFFFFFFFFFFFL;
            if (mantissaBits == 0)
                return exponent;

            var mantissa = BitConverter.Int64BitsToDouble(mantissaBits | (1023L << 52)); // in (1, 2)
            return exponent + Math.Log(mantissa) / Ln2;
        }
    }

    extension(BitConverter)
    {
        public static float UInt32BitsToSingle(uint value) => BitConverter.Int32BitsToSingle((int)value);
    }

    extension(Stopwatch)
    {
        public static TimeSpan GetElapsedTime(long startingTimestamp) =>
            new((long)((Stopwatch.GetTimestamp() - startingTimestamp) * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency)));
    }

    public static async ValueTask ReadExactlyAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException();

            totalRead += read;
        }
    }

    public static int EnsureCapacity<T>(this List<T> list, int capacity)
    {
        if (list.Capacity < capacity)
            list.Capacity = capacity;

        return list.Capacity;
    }

    private const double Ln2 = 0.69314718055994530941723212145818;
}

/// <summary>
/// Stand-in for <c>System.Numerics.BitOperations</c>, which is public only in .NET Core 3.0+.
/// Declared in the <c>DemoFile</c> namespace so it wins name lookup over the inaccessible BCL type.
/// </summary>
internal static class BitOperations
{
    public static uint RoundUpToPowerOf2(uint value)
    {
        --value;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }
}
