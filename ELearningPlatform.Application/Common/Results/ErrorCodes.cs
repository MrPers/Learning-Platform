namespace ELearningPlatform.Application.Common.Results;

/// <summary>Stable error codes clients may rely on (returned as "code" in problem details).</summary>
public static class ErrorCodes
{
    public const string InvalidCredentials = "Auth.InvalidCredentials";
    public const string LockedOut = "Auth.LockedOut";
    public const string EmailNotConfirmed = "Auth.EmailNotConfirmed";
    public const string InvalidRole = "Auth.InvalidRole";
    public const string TwoFactorRequired = "Auth.TwoFactorRequired";
    public const string InvalidTwoFactorCode = "Auth.InvalidTwoFactorCode";
    public const string RefreshTokenMissing = "Auth.RefreshTokenMissing";
    public const string InvalidRefreshToken = "Auth.InvalidRefreshToken";
    public const string InvalidExternalToken = "Auth.InvalidExternalToken";
    public const string EmailAlreadyRegistered = "Auth.EmailAlreadyRegistered";

    public const string UserNotFound = "User.NotFound";
    public const string CourseNotFound = "Course.NotFound";
    public const string VideoNotFound = "Video.NotFound";
    public const string ExamNotFound = "Exam.NotFound";
    public const string QuestionNotFound = "Question.NotFound";

    public const string ExamNotStarted = "Exam.NotStarted";
    public const string ExamEnded = "Exam.Ended";
    public const string ExamAlreadySubmitted = "Exam.AlreadySubmitted";
    public const string QuestionLimitReached = "Question.LimitReached";
    public const string AlreadyRated = "Course.AlreadyRated";

    public const string InvalidFile = "Media.InvalidFile";

    public const string CourseAlreadyPurchased = "Course.AlreadyPurchased";
    public const string CourseHasPurchases = "Course.HasPurchases";

    public const string InsufficientFunds = "Wallet.InsufficientFunds";
    public const string ConcurrentUpdate = "Wallet.ConcurrentUpdate";
    public const string PaymentNotFound = "Payment.NotFound";
    public const string PaymentNotRefundable = "Payment.NotRefundable";
    public const string PaymentProviderError = "Payment.ProviderError";
    public const string PurchaseNotFound = "Purchase.NotFound";
    public const string PurchaseNotRefundable = "Purchase.NotRefundable";
    public const string PayoutNotFound = "Payout.NotFound";
    public const string PayoutNotPending = "Payout.NotPending";
    public const string NotAStudent = "Payment.NotAStudent";
    public const string NotATeacher = "Payment.NotATeacher";
}
