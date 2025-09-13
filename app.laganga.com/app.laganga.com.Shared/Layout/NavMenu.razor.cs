using Microsoft.AspNetCore.Components;
using Microsoft.Maui.Storage;
using System.Threading.Tasks;

namespace app.laganga.com.Shared.Layout
{
    public partial class NavMenu
    {
        [Inject]
        public NavigationManager NavigationManager { get; set; }

        public async Task Logout()
        {
            SecureStorage.Remove("access_token");
            NavigationManager.NavigateTo("/login");
            await Task.CompletedTask;
        }
    }
}
