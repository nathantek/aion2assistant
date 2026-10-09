using System.Text;

namespace Aion2Assistant.Protocol;

public static class NameText
{
    public const int MaxBytes = 36;
    private const int MaxChars = 16;
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>UTF-8 strict, lettres et chiffres de n'importe quelle écriture, au moins une lettre, 16 caractères au plus.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> bytes, out string name)
    {
        name = "";
        if (bytes.Length is < 1 or > MaxBytes) return false;
        string text;
        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
        if (text.Length > MaxChars || !text.All(char.IsLetterOrDigit) || !text.Any(char.IsLetter)) return false;
        name = text;
        return true;
    }
}
