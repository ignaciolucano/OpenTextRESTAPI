
using OpenTextIntegrationAPI.DTOs;

namespace OpenTextIntegrationAPI.Services.Interfaces
{
    public interface IAuthService
    {
        Task<string> ExternalAuthenticateAsync(AuthRequest authRequest);
        Task<string> InternalAuthenticateAsync();
    }
}
