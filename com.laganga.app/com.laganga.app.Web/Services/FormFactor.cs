using com.laganga.app.Shared.Services;
using Duende.IdentityModel.OidcClient;

namespace com.laganga.app.Web.Services
{
    public class FormFactor : IFormFactor
    {
        public string GetFormFactor()
        {
            return "Web";
        }

        public string GetPlatform()
        {
            return Environment.OSVersion.ToString();
        }
        public Task<OidcClient> GetClientOidc()
        {
            return default!;
        }
    }
}
