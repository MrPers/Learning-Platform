namespace ELearningPlatform.Application.DTOs.Authentication;

/// <summary>A freshly generated refresh token: the entity to store (hash only) and the raw value for the client.</summary>
public sealed record IssuedRefreshToken(RefreshToken Entity, string Token);
