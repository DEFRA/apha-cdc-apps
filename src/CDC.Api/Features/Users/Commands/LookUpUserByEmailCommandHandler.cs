using CDC.Api.Domain.Common;
using CDC.Api.Features.Users.Dtos;
using CDC.Api.Features.Users.Interfaces;
using MediatR;

namespace CDC.Api.Features.Users.Commands;

/// <summary>Handles <see cref="LookUpUserByEmailCommand"/>.</summary>
/// <param name="userService">User application service.</param>
public sealed class LookUpUserByEmailCommandHandler(IUserService userService)
    : IRequestHandler<LookUpUserByEmailCommand, Result<UserLookupDto>>
{
    /// <inheritdoc />
    public async Task<Result<UserLookupDto>> Handle(LookUpUserByEmailCommand request, CancellationToken cancellationToken)
    {
        var result = await userService.LookUpByEmailAsync(request, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Status switch
            {
                ResultStatus.NotFound => Result.NotFound<UserLookupDto>(result.Error!),
                ResultStatus.Conflict => Result.Conflict<UserLookupDto>(result.Error!),
                _ => Result.Forbidden<UserLookupDto>(result.Error ?? "Unable to look up the user.")
            };
        }

        var user = result.Value;

        return Result.Success(new UserLookupDto
        {
            Id = user.Id,
            FullName = user.FullName,
            IsProfileEditor = user.IsProfileEditor,
            IsPolicyProfileUser = user.IsPolicyProfileUser,
            IsInternalProfileEditor = user.SsoUserIdInt.HasValue && user.IsProfileEditor
        });
    }
}
