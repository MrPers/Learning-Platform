namespace ELearningPlatform.Domain.Enums;

public enum LedgerTransactionType
{
    TopUp = 1,
    CoursePurchase = 2,
    PayoutRequest = 3,
    PayoutCompletion = 4,
    /// <summary>Mirror image of an earlier transaction (refund, rejected payout); see LedgerTransaction.ReversalOfId.</summary>
    Reversal = 5,
}
