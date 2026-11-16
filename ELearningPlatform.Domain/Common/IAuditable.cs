namespace ELearningPlatform.Domain.Common;

/// <summary>
/// Marks entities whose changes are written to the audit log (who, what, when, old and new values).
/// Creation is recorded only for entities with client-generated keys (Guid), whose id is known before saving.
/// </summary>
public interface IAuditable;

/// <summary>Excludes a property of an <see cref="IAuditable"/> entity from audit snapshots.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotAuditedAttribute : Attribute;
