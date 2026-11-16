namespace ELearningPlatform.Domain.Enums;

/// <summary>
/// User wallets belong to a person and may never go negative. The others are platform (system) accounts
/// that let every movement of money be recorded as a balanced double entry.
/// </summary>
public enum WalletType
{
    User = 1,
    /// <summary>Commission the platform keeps from course sales.</summary>
    PlatformRevenue = 2,
    /// <summary>Money held by / sent through the payment provider. Negative = funds received from outside.</summary>
    ExternalClearing = 3,
    /// <summary>Teacher money reserved by a payout request until an admin completes or rejects it.</summary>
    PayoutHold = 4,
}
