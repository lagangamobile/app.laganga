using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace app.laganga.com.Configuration
{
    public class SettingsService
    {
        private readonly OauthSettings _oauthSettings;

        public SettingsService()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream("app.laganga.com.appsettings.json");

            var config = new ConfigurationBuilder()
                .AddJsonStream(stream)
                .Build();

            _oauthSettings = new OauthSettings();
            config.GetSection("Oauth").Bind(_oauthSettings);
        }

        public OauthSettings GetOauthSettings()
        {
            return _oauthSettings;
        }
    }
}
