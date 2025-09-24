using com.laganga.app.Shared.Services;
using Duende.IdentityModel.OidcClient;

namespace com.laganga.app.Services;

public class FormFactor : IFormFactor
{
    // Declara una variable para almacenar la instancia de OidcClient
    private readonly OidcClient _oidcClient;

    // El constructor recibe OidcClient como un parámetro inyectado
    public FormFactor(OidcClient oidcClient)
    {
        _oidcClient = oidcClient;
    }

    public string GetFormFactor()
    {
        return DeviceInfo.Idiom.ToString();
    }

    public string GetPlatform()
    {
        return DeviceInfo.Platform.ToString() + " - " + DeviceInfo.VersionString;
    }

    public Task<OidcClient> GetClientOidc()
    {
        return Task.FromResult(_oidcClient);
    }
}
