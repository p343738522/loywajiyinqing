using System.Text;

namespace GameGate.Core;

/// <summary>
/// Tiger (BaiZhu) encryption protocol — custom base-64 encoding with key rotation.
/// Tiger frames are STRING-based (ASCII), ending with "|LH", decoded to binary
/// MobileCodec frames before normal processing.
/// </summary>
public static class TigerCodec
{
    private const string BASE_KEY = "1Y0lSUQMH+mbKXRTBtFiWvLx32/gNAzGr674oeyn5dCEp8jDqasI9VcwJPhufkOZ";
    private const string LH_SUFFIX = "|LH";
    private const long CHUNK_DIVISOR = 262144; // 64^3
    private static readonly string[] RotatedKeys = CreateRotatedKeys();

    public const byte CMD_HEARTBEAT = 29;

    /// <summary>Decode Tiger-encrypted base64 string to binary bytes.</summary>
    public static byte[] Decode(string tigerData, uint keyOffset)
    {
        if (tigerData.EndsWith(LH_SUFFIX))
            tigerData = tigerData.Substring(0, tigerData.Length - LH_SUFFIX.Length);

        var scratch = new byte[tigerData.Length];
        var written = DecodeChars(tigerData.AsSpan(), keyOffset, scratch);
        if (written == 0) return Array.Empty<byte>();
        if (written == scratch.Length) return scratch;
        var exact = new byte[written];
        Buffer.BlockCopy(scratch, 0, exact, 0, written);
        return exact;
    }

    public static int Decode(ReadOnlySpan<byte> tigerAscii, uint keyOffset, Span<byte> destination)
    {
        if (tigerAscii.EndsWith("|LH"u8))
            tigerAscii = tigerAscii[..^3];

        string keyStr = GetRotatedKey(keyOffset);
        int pos = 0;
        int written = 0;

        while (pos < tigerAscii.Length)
        {
            long value = 0;
            int charsInGroup = 0;

            for (int i = 0; i < 4 && pos < tigerAscii.Length; i++)
            {
                char c = (char)tigerAscii[pos];
                if (c == '=') { pos++; break; }
                int idx = keyStr.IndexOf(c);
                if (idx < 0)
                    throw new FormatException($"Invalid Tiger base64 char: '{c}' (0x{(int)c:X2}) at position {pos}");
                value = value * 64 + idx;
                charsInGroup++;
                pos++;
            }

            int bytesInGroup = charsInGroup - 1;
            if (bytesInGroup > 0)
            {
                if (written + bytesInGroup > destination.Length)
                    throw new ArgumentException("Tiger decode buffer is too small", nameof(destination));
                for (int i = bytesInGroup - 1; i >= 0; i--)
                    destination[written++] = (byte)((value >> (i * 8)) & 0xFF);
            }
        }

        return written;
    }

    /// <summary>Encode binary bytes to Tiger-encrypted base64 string.</summary>
    public static string Encode(byte[] data, uint keyOffset)
    {
        string keyStr = GetRotatedKey(keyOffset);
        var sb = new StringBuilder();
        int pos = 0;

        while (pos < data.Length)
        {
            long chunk = 0;
            int bytesInGroup = 0;

            // Read up to 3 bytes into chunk (big-endian base-256)
            for (int i = 0; i < 3; i++)
            {
                chunk <<= 8;
                if (pos < data.Length)
                {
                    chunk |= data[pos++];
                    bytesInGroup++;
                }
            }

            // Output bytesInGroup+1 characters from keyStr
            for (int i = 0; i < bytesInGroup + 1; i++)
            {
                int idx = (int)((chunk / CHUNK_DIVISOR) % 64);
                sb.Append(keyStr[idx]);
                chunk *= 64;
            }

            // Pad with '=' to reach 4 chars per group
            for (int i = 0; i < 3 - bytesInGroup; i++)
                sb.Append('=');
        }

        sb.Append(LH_SUFFIX);
        return sb.ToString();
    }

    /// <summary>Check if raw bytes end with "|LH" (0x7C 0x4C 0x48).</summary>
    public static bool HasLHSuffix(byte[] data, int len)
    {
        if (len < 3) return false;
        return data[len - 3] == 0x7C && data[len - 2] == 0x4C && data[len - 1] == 0x48;
    }

    /// <summary>Find index of first "|LH" in buffer, or -1.</summary>
    public static int FindLHSuffix(byte[] data, int offset, int len)
    {
        for (int i = offset; i <= offset + len - 3; i++)
        {
            if (data[i] == 0x7C && data[i + 1] == 0x4C && data[i + 2] == 0x48)
                return i;
        }
        return -1;
    }

    /// <summary>Rotate BASE_KEY by offset.</summary>
    internal static string GetRotatedKey(uint offset)
    {
        return RotatedKeys[offset % 63];
    }

    private static int DecodeChars(ReadOnlySpan<char> tigerData, uint keyOffset, Span<byte> destination)
    {
        string keyStr = GetRotatedKey(keyOffset);
        int pos = 0;
        int written = 0;

        while (pos < tigerData.Length)
        {
            long value = 0;
            int charsInGroup = 0;

            for (int i = 0; i < 4 && pos < tigerData.Length; i++)
            {
                char c = tigerData[pos];
                if (c == '=') { pos++; break; }
                int idx = keyStr.IndexOf(c);
                if (idx < 0)
                    throw new FormatException($"Invalid Tiger base64 char: '{c}' (0x{(int)c:X2}) at position {pos}");
                value = value * 64 + idx;
                charsInGroup++;
                pos++;
            }

            int bytesInGroup = charsInGroup - 1;
            if (bytesInGroup > 0)
            {
                if (written + bytesInGroup > destination.Length)
                    throw new ArgumentException("Tiger decode buffer is too small", nameof(destination));
                for (int i = bytesInGroup - 1; i >= 0; i--)
                    destination[written++] = (byte)((value >> (i * 8)) & 0xFF);
            }
        }

        return written;
    }

    private static string[] CreateRotatedKeys()
    {
        var keys = new string[63];
        keys[0] = BASE_KEY;
        for (int rot = 1; rot < keys.Length; rot++)
            keys[rot] = BASE_KEY.Substring(rot) + BASE_KEY.Substring(0, rot);
        return keys;
    }
}
