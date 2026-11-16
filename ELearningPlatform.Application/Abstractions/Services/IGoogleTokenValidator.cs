namespace ELearningPlatform.Application.Abstractions.Services;

public sealed record GoogleUser(string Subject, string Email, bool EmailVerified, string? GivenName, string? FamilyName);

public interface IGoogleTokenValidator
{
    /// <summary>
    /// Verifies a Google id_token's signature, expiry, issuer and audience (our client ids).
    /// Returns null when the token is not valid for this application.
    /// </summary>
    Task<GoogleUser?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
