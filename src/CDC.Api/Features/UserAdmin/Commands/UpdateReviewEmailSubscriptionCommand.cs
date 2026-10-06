using CDC.Api.Domain.Common;
using CDC.Api.Features.UserAdmin.Dtos;
using MediatR;

namespace CDC.Api.Features.UserAdmin.Commands;

/// <summary>
/// Subscribes a user to, or unsubscribes them from, review notification emails. Every other
/// attribute of the account is left unchanged.
/// </summary>
public sealed record UpdateReviewEmailSubscriptionCommand : IRequest<Result<UpdateReviewEmailSubscriptionResultDto>>
{
    /// <summary>Gets the user whose subscription is changing.</summary>
    public Guid UserId { get; init; }

    /// <summary>Gets a value indicating whether the user should receive review notification emails.</summary>
    public bool SubscribedToReviewEmails { get; init; }

    /// <summary>Gets the row version read with the user, so a concurrent edit is detected.</summary>
    public byte[] LastUpdated { get; init; } = [];
}
