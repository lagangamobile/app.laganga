using app.laganga.com.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.Maui.Storage;
using System;
using System.Threading.Tasks;

namespace app.laganga.com.Shared.Pages
{
    public partial class Login
    {
        [Inject]
        public OAuthService OAuthService { get; set; }

        [Inject]
        public NavigationManager NavigationManager { get; set; }

        public string Username { get; set; }
        public string Password { get; set; }
        public string ErrorMessage { get; set; }

        private async Task HandleLogin()
        {
            try
            {
                ErrorMessage = null;
                var tokenResponse = await OAuthService.LoginAsync(Username, Password);
                await SecureStorage.SetAsync("access_token", tokenResponse.AccessToken);
                NavigationManager.NavigateTo("/");
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}
