// Copyright DEFRA (c). All rights reserved.
// Licensed under the Open Government License v3.0.

using System.Diagnostics.CodeAnalysis;
using Defra.Trade.API.Daera.Certificates.Infrastructure;
using Defra.Trade.API.Daera.Certificates.Logic.Extensions;
using Defra.Trade.Common.Api.Infrastructure;
using Defra.Trade.Common.AppConfig;
using Defra.Trade.Common.ExternalApi.ApimIdentity;
using Defra.Trade.Common.ExternalApi.Auditing;
using Defra.Trade.Common.Security.Isolated.Authentication.Infrastructure;
using Defra.Trade.Common.Sql.Infrastructure;

namespace Defra.Trade.API.Daera.Certificates;

/// <summary>
/// Application entry point.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Process entry point covered by end-to-end tests.")]
public class Program
{
    /// <summary>
    /// Application main entry point.
    /// </summary>
    /// <param name="args">Args</param>
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration.ConfigureTradeAppConfiguration(cfg =>
        {
            cfg.UseKeyVaultSecrets = true;
            cfg.RefreshKeys.Add($"{ExtApiAppConfig.AppConfigSettingsName}:{ExtApiAppConfig.RefreshKey}");
        });

        builder.Services.AddTradeApi(builder.Configuration);
        builder.Services.AddTradeExternalApimIdentity(builder.Configuration);
        builder.Services.AddTradeExternalAuditing(builder.Configuration);
        builder.Services.AddApimAuthentication(builder.Configuration.GetSection(InternalApimSettings.SectionName));
        builder.Services.AddTradeSql(builder.Configuration);
        builder.Services.AddServiceRegistrations(builder.Configuration);

        var app = builder.Build();

        app.Logger.LogStartup(
            app.Environment.EnvironmentName,
            app.Environment.ApplicationName,
            app.Environment.ContentRootPath);

        app.UseTradeExternalAuditing();
        app.UseTradeApp(app.Environment);

        app.Run();
    }
}
