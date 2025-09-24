using Duende.IdentityModel.OidcClient;

namespace com.laganga.app.Shared.Services;

public interface IFormFactor
{
    public string GetFormFactor();
    public string GetPlatform();
    public Task<OidcClient> GetClientOidc();
}
