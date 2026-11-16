namespace ELearningPlatform.Application.DTOs.Notification;

/// <summary>Attachment copied out of the request, so it can be serialized into a background job.</summary>
public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);
