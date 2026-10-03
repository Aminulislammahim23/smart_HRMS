namespace smartHRMS.Application.Common.Money;

/// <summary>Monetary rounding used everywhere: 2 decimals, halves away from zero (stored as decimal(18,2)).</summary>
public static class Money
{
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
