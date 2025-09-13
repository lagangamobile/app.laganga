using Microsoft.AspNetCore.Components;
using Microsoft.Maui.Storage;
using System.Threading.Tasks;

namespace app.laganga.com.Shared.Layout
{
    public partial class MainLayout
    {
        [Inject]
        public NavigationManager NavigationManager { get; set; }

        protected override async Task OnInitializedAsync()
        {
            var accessToken = await SecureStorage.GetAsync("access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                NavigationManager.NavigateTo("/login");
            }
        }
    }
}
