namespace PitCrm.Domain.Customers;

public static class CustomerDocument
{
    public static bool TryNormalize(string? value, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (value.Any(character => !IsAsciiDigit(character) && character is not '.' and not '-' and not '/' && !char.IsWhiteSpace(character)))
            return false;

        var digits = new string(value.Where(IsAsciiDigit).ToArray());
        if (digits.Length == 11 && IsValidCpf(digits) || digits.Length == 14 && IsValidCnpj(digits))
        {
            normalized = digits;
            return true;
        }

        return false;
    }

    public static string? Normalize(string? value)
    {
        if (TryNormalize(value, out var normalized)) return normalized;
        throw new ArgumentException("Document must be a valid numeric CPF or CNPJ.", nameof(value));
    }

    public static string DigitsForSearch(string value) => new(value.Where(IsAsciiDigit).ToArray());

    private static bool IsValidCpf(string value)
    {
        if (value.All(character => character == value[0])) return false;
        var first = CheckDigit(value[..9], [10, 9, 8, 7, 6, 5, 4, 3, 2]);
        var second = CheckDigit(value[..10], [11, 10, 9, 8, 7, 6, 5, 4, 3, 2]);
        return value[9] - '0' == first && value[10] - '0' == second;
    }

    private static bool IsValidCnpj(string value)
    {
        if (value.All(character => character == value[0])) return false;
        var first = CheckDigit(value[..12], [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        var second = CheckDigit(value[..12] + first, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        return value[12] - '0' == first && value[13] - '0' == second;
    }

    private static int CheckDigit(string digits, int[] weights)
    {
        var remainder = digits.Select((character, index) => (character - '0') * weights[index]).Sum() % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    private static bool IsAsciiDigit(char character) => character is >= '0' and <= '9';
}
