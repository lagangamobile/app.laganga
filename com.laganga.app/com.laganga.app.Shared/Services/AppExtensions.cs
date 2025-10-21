using Blazored.SessionStorage;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.laganga.app.Shared.Services;

public static partial class AppExtensions
{

    public static IServiceCollection AddApplicationShared(this IServiceCollection services)
    {
        services.AddDevExpressBlazor(options =>
        {
            options.BootstrapVersion = DevExpress.Blazor.BootstrapVersion.v5;
            options.SizeMode = DevExpress.Blazor.SizeMode.Medium;
            
        });



        services.AddSingleton<ParameterService>();
        services.AddSingleton<NetworkStatusService>();
        services.AddBlazoredSessionStorage();

        return services;
    }
}
