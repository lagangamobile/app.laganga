using app.laganga.com.Configuration;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace app.laganga.com.Authentication
{
    public class OAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly OauthSettings _oauthSettings;

        public OAuthService(SettingsService settingsService)
        {
            _httpClient = new HttpClient();
            _oauthSettings = settingsService.GetOauthSettings();
        }

        public async Task<TokenResponse> LoginAsync(string username, string password)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_oauthSettings.Authority}/connect/token");

            var keyValues = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", _oauthSettings.GrantType),
                new KeyValuePair<string, string>("client_id", _oauthSettings.ClientId),
                new KeyValuePair<string, string>("client_secret", _oauthSettings.ClientSecret),
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password)
            };

            if (_oauthSettings.Audience != null && _oauthSettings.Audience.Length > 0)
            {
                keyValues.Add(new KeyValuePair<string, string>("scope", string.Join(" ", _oauthSettings.Audience)));
            }

            request.Content = new FormUrlEncodedContent(keyValues);

            var response = await _httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content);

            return tokenResponse;
        }
    }
}
