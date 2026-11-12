namespace CDC.Api.Domain.Entities;

/// <summary>The details needed to email an external user their login instructions.</summary>
/// <param name="Id">The user's identifier.</param>
/// <param name="FullName">The user's full name.</param>
/// <param name="EmailAddress">The email address the instructions are sent to.</param>
/// <param name="LoginInstructionsSent">Whether the instructions have already been sent to that address.</param>
public sealed record ExternalUserLoginRecipient(Guid Id, string FullName, string EmailAddress, bool LoginInstructionsSent);
