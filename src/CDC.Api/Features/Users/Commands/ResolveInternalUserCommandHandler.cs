using CDC.Api.Domain.Common;
using CDC.Api.Features.Users.Dtos;
using CDC.Api.Features.Users.Interfaces;
using MediatR;

namespace CDC.Api.Features.Users.Commands;

/// <summary>Handles <see cref="ResolveInternalUserCommand"/>.</summary>
/// <param name="userService">Internal-user resolution application service.</param>
public sealed class ResolveInternalUserCommandHandler(IUserService userService)
    : IRequestHandler<ResolveInternalUserCommand, Result<InternalUserDto>>
{
    /// <inheritdoc />
    public async Task<Result<InternalUserDto>> Handle(ResolveInternalUserCommand request, CancellationToken cancellationToken)
    {
        var result = await userService.ResolveInternalUserAsync(request, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Status switch
            {
                ResultStatus.NotFound => Result.NotFound<InternalUserDto>(result.Error!),
                ResultStatus.Forbidden => Result.Forbidden<InternalUserDto>(result.Error!),
                _ => Result.NotFound<InternalUserDto>(result.Error ?? "Unable to resolve the internal user.")
            };
        }

        var user = result.Value;

        return Result.Success(new InternalUserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            IsProfileEditor = user.IsProfileEditor,
            IsPolicyProfileUser = user.IsPolicyProfileUser
        });
    }
}
