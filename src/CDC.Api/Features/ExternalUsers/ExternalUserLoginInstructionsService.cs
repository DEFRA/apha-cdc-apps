using CDC.Api.Application;
using CDC.Api.Domain.Common;
using CDC.Api.Features.ExternalUsers.Commands;
using CDC.Api.Features.ExternalUsers.Interfaces;
using CDC.Api.Features.ReviewNotifications;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using MediatR;
using Microsoft.Extensions.Options;

namespace CDC.Api.Features.ExternalUsers;

/// <summary>Default <see cref="IExternalUserLoginInstructionsService"/>.</summary>
/// <param name="repository">External users data access.</param>
/// <param name="emailSender">Sends the email through GOV.UK Notify.</param>
/// <param name="reviewOptions">Test recipient setting shared with the review reminder email.</param>
/// <param name="appOptions">The address D2R2 is opened at, linked from the email.</param>
/// <param name="timeProvider">Clock used for the sent date.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ExternalUserLoginInstructionsService(
    IExternalUsersRepository repository,
    IEmailSender emailSender,
    IOptions<ReviewNotificationsOptions> reviewOptions,
    IOptions<AppOptions> appOptions,
    TimeProvider timeProvider,
    ILogger<ExternalUserLoginInstructionsService> logger)
    : IExternalUserLoginInstructionsService
{
    /// <inheritdoc />
    public async Task<Result<Unit>> SendAsync(SendExternalUserLoginInstructionsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var recipient = await repository.GetLoginRecipientAsync(command.Id, cancellationToken);
        if (recipient is null)
        {
            return Result.NotFound<Unit>($"External user '{command.Id}' was not found.");
        }

        if (recipient.LoginInstructionsSent)
        {
            return Result.Conflict<Unit>("The login instructions have already been sent to this user.");
        }

        if (string.IsNullOrWhiteSpace(recipient.EmailAddress))
        {
            logger.LoginInstructionsNoEmailAddress(command.Id);

            return Result.ValidationFailed<Unit>("There is no email address saved to send the login instructions to.");
        }

        var message = ExternalUserLoginInstructionsComposer.Compose(recipient, appOptions.Value.BaseUrl);
        if (message is null)
        {
            logger.LoginInstructionsLinkNotConfigured(command.Id);

            return Result.DependencyFailed<Unit>("The login instructions email could not be sent.");
        }

        var testRecipient = reviewOptions.Value.TestRecipient;
        if (!string.IsNullOrWhiteSpace(testRecipient))
        {
            message = message with { ToAddress = testRecipient };
            logger.LoginInstructionsRedirectedToTestRecipient(command.Id);
        }

        try
        {
            await emailSender.SendAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LoginInstructionsSendFailed(exception, command.Id);

            return Result.DependencyFailed<Unit>("The login instructions email could not be sent.");
        }

        // The email has been accepted, so recording it is not cancellable: stopping here would offer "Send" again for an email already sent.
        var recorded = await repository.MarkLoginInstructionsSentAsync(
            recipient.Id, recipient.EmailAddress, timeProvider.GetUtcNow().UtcDateTime, CancellationToken.None);

        if (!recorded)
        {
            logger.LoginInstructionsEmailChanged(command.Id);

            return Result.Conflict<Unit>("The email address was changed while the instructions were being sent. Send the instructions again.");
        }

        logger.LoginInstructionsSent(command.Id);

        return Result.Success(Unit.Value);
    }
}
