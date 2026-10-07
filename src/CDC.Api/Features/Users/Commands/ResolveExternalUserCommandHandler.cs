using CDC.Api.Domain.Common;
using CDC.Api.Features.Users.Dtos;
using CDC.Api.Features.Users.Interfaces;
using MediatR;

namespace CDC.Api.Features.Users.Commands;

/// <summary>Handles <see cref="ResolveExternalUserCommand"/>.</summary>
/// <param name="userService">External-user resolution application service.</param>
public sealed class ResolveExternalUserCommandHandler(IUserService userService)
    : IRequestHandler<ResolveExternalUserCommand, Result<ExternalUserDto>>
{
    /// <inheritdoc />
    public async Task<Result<ExternalUserDto>> Handle(ResolveExternalUserCommand request, CancellationToken cancellationToken)
    {
        var result = await userService.ResolveExternalUserAsync(request, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Status switch
            {
                ResultStatus.Forbidden => Result.Forbidden<ExternalUserDto>(result.Error!),
                ResultStatus.NotFound => Result.NotFound<ExternalUserDto>(result.Error!),
                ResultStatus.Conflict => Result.Conflict<ExternalUserDto>(result.Error!),
                _ => Result.Forbidden<ExternalUserDto>(result.Error ?? "Unable to resolve the external user.")
            };
        }

        var user = result.Value;

        return Result.Success(new ExternalUserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            EmailAddress = user.EmailAddress,
            Organisation = user.Organisation
        });
    }
}
