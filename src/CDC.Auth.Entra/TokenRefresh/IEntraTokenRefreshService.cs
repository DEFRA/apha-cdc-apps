using CDC.Common.Auth;

namespace CDC.Auth.Entra.TokenRefresh;

/// <summary>Exchanges an Entra ID refresh_token for a new token set.</summary>
public interface IEntraTokenRefreshService : ITokenRefreshService
{
}
