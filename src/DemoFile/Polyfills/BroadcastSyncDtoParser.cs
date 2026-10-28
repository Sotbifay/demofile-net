using System.Globalization;
using System.Text;

namespace DemoFile;

/// <summary>
/// Minimal parser for the broadcast <c>/sync</c> response, used instead of System.Text.Json on netstandard2.1.
/// Unknown keys are skipped. Reflection-free, so it is safe under IL2CPP.
/// </summary>
internal static class BroadcastSyncDtoParser
{
    public static BroadcastSyncDto Parse(string json)
    {
        var dto = new BroadcastSyncDto();
        var pos = 0;

        SkipWhitespace(json, ref pos);
        Expect(json, ref pos, '{');
        SkipWhitespace(json, ref pos);
        if (Peek(json, pos) == '}')
            return dto;

        while (true)
        {
            SkipWhitespace(json, ref pos);
            var key = ReadString(json, ref pos);
            SkipWhitespace(json, ref pos);
            Expect(json, ref pos, ':');
            SkipWhitespace(json, ref pos);

            switch (key)
            {
                case "rtdelay":
                    dto.RetransmitDelaySeconds = ReadDouble(json, ref pos);
                    break;
                case "rcvage":
                    dto.ReceiveAgeSeconds = ReadDouble(json, ref pos);
                    break;
                case "fragment":
                    dto.Fragment = ReadInt(json, ref pos);
                    break;
                case "signup_fragment":
                    dto.SignupFragment = ReadInt(json, ref pos);
                    break;
                case "tps":
                    dto.TickRate = ReadInt(json, ref pos);
                    break;
                case "token_redirect":
                    dto.TokenRedirect = ReadNullableString(json, ref pos);
                    break;
                case "protocol":
                    dto.Protocol = ReadInt(json, ref pos);
                    break;
                default:
                    SkipValue(json, ref pos);
                    break;
            }

            SkipWhitespace(json, ref pos);
            var next = Read(json, ref pos);
            if (next == '}')
                return dto;
            if (next != ',')
                throw Error(json, pos - 1, "expected ',' or '}'");
        }
    }

    private static char Peek(string json, int pos) => pos < json.Length ? json[pos] : '\0';

    private static char Read(string json, ref int pos)
    {
        if (pos >= json.Length)
            throw Error(json, pos, "unexpected end of input");

        return json[pos++];
    }

    private static void Expect(string json, ref int pos, char expected)
    {
        if (Read(json, ref pos) != expected)
            throw Error(json, pos - 1, $"expected '{expected}'");
    }

    private static void SkipWhitespace(string json, ref int pos)
    {
        while (pos < json.Length && char.IsWhiteSpace(json[pos]))
            pos++;
    }

    private static string ReadString(string json, ref int pos)
    {
        Expect(json, ref pos, '"');

        var sb = new StringBuilder();
        while (true)
        {
            var c = Read(json, ref pos);
            if (c == '"')
                return sb.ToString();

            if (c != '\\')
            {
                sb.Append(c);
                continue;
            }

            var escape = Read(json, ref pos);
            switch (escape)
            {
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                case '/': sb.Append('/'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'u':
                    if (pos + 4 > json.Length)
                        throw Error(json, pos, "truncated \\u escape");
                    sb.Append((char)int.Parse(json.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    pos += 4;
                    break;
                default:
                    throw Error(json, pos - 1, $"invalid escape '\\{escape}'");
            }
        }
    }

    private static string? ReadNullableString(string json, ref int pos)
    {
        if (Peek(json, pos) != 'n')
            return ReadString(json, ref pos);

        ReadLiteral(json, ref pos, "null");
        return null;
    }

    private static string ReadNumberToken(string json, ref int pos)
    {
        var start = pos;
        while (pos < json.Length && "+-.eE0123456789".IndexOf(json[pos]) >= 0)
            pos++;

        if (pos == start)
            throw Error(json, pos, "expected a number");

        return json.Substring(start, pos - start);
    }

    private static double ReadDouble(string json, ref int pos) =>
        double.Parse(ReadNumberToken(json, ref pos), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static int ReadInt(string json, ref int pos) =>
        int.Parse(ReadNumberToken(json, ref pos), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static void ReadLiteral(string json, ref int pos, string literal)
    {
        if (string.CompareOrdinal(json, pos, literal, 0, literal.Length) != 0)
            throw Error(json, pos, $"expected '{literal}'");

        pos += literal.Length;
    }

    private static void SkipValue(string json, ref int pos)
    {
        switch (Peek(json, pos))
        {
            case '"':
                ReadString(json, ref pos);
                break;
            case '{':
            case '[':
                var depth = 0;
                do
                {
                    var c = Peek(json, pos);
                    if (c == '"')
                    {
                        ReadString(json, ref pos);
                        continue;
                    }

                    Read(json, ref pos);
                    if (c is '{' or '[')
                        depth++;
                    else if (c is '}' or ']')
                        depth--;
                } while (depth > 0);
                break;
            case 't':
                ReadLiteral(json, ref pos, "true");
                break;
            case 'f':
                ReadLiteral(json, ref pos, "false");
                break;
            case 'n':
                ReadLiteral(json, ref pos, "null");
                break;
            default:
                ReadNumberToken(json, ref pos);
                break;
        }
    }

    private static FormatException Error(string json, int pos, string message) =>
        new($"Invalid broadcast sync JSON at position {pos}: {message}. JSON: {json}");
}
