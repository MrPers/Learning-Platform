namespace ELearningPlatform.Domain.Common;

/// <summary>Rules every amount of money in the system follows: decimal with at most 2 fractional digits.</summary>
public static class Money
{
    public const int Scale = 2;
    public const int CurrencyLength = 3;

    public static bool HasValidScale(decimal amount) => decimal.Round(amount, Scale) == amount;

    public static decimal Round(decimal amount) => decimal.Round(amount, Scale, MidpointRounding.AwayFromZero);

    /// <summary>Amount in the smallest currency unit (cents), as payment providers expect it.</summary>
    public static long ToMinorUnits(decimal amount) => checked((long)(Round(amount) * 100m));

    public static decimal FromMinorUnits(long amount) => amount / 100m;

    public static string NormalizeCurrency(string currency) => currency.Trim().ToUpperInvariant();
}
