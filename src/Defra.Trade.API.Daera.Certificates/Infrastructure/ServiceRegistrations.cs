// Copyright DEFRA (c). All rights reserved.
// Licensed under the Open Government License v3.0.

using System.Diagnostics.CodeAnalysis;
using Defra.Trade.API.Daera.Certificates;
using Defra.Trade.API.CertificatesStore.V1.ApiClient.Api;
using Defra.Trade.API.CertificatesStore.V1.ApiClient.Client;
using Defra.Trade.API.Daera.Certificates.Database.Context;
using Defra.Trade.API.Daera.Certificates.Ehco.BlobClient.Infrastructure;
using Defra.Trade.API.Daera.Certificates.Ehco.DocumentProvider;
using Defra.Trade.API.Daera.Certificates.Logic.Infrastructure;
using Defra.Trade.API.Daera.Certificates.Logic.Mappers;
using Defra.Trade.API.Daera.Certificates.Logic.Services;
using Defra.Trade.API.Daera.Certificates.Logic.Services.Interfaces;
using Defra.Trade.API.Daera.Certificates.Repository;
using Defra.Trade.API.Daera.Certificates.Repository.Interfaces;
using Defra.Trade.API.Daera.Certificates.V1.Examples;
using Defra.Trade.API.Daera.Certificates.Infrastructure.EF;
using Defra.Trade.API.Daera.Certificates.Infrastructure.Json;
using Defra.Trade.Common.Security.Isolated.Authentication.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Filters;
using GcModels = Defra.Trade.API.Daera.Certificates.Logic.Models.GeneralCertificate;

namespace Defra.Trade.API.Daera.Certificates.Infrastructure;

/// <summary>
/// Service registration class.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ServiceRegistrations
{
    /// <summary>
    /// Extension method for service registrations.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns></returns>
    public static IServiceCollection AddServiceRegistrations(this IServiceCollection services, IConfiguration configuration)
    {
        var appConfig = configuration.GetSection(InternalApimSettings.SectionName);
        services.AddOptions<InternalApimSettings>().Bind(appConfig);

        return services
            .AddValidatorsFromAssembly(typeof(Program).Assembly, lifetime: ServiceLifetime.Transient)
            .AddAutoMapper(typeof(Program).Assembly, typeof(GeneralCertificateRetrievalMapper).Assembly)
            .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly))
            .AddV1Registrations()
            .AddLogicRegistrations(configuration)
            .AddRepositoryRegistrations(configuration)
            .AddApiOptions(configuration)
            .AddSwaggerExamples()
            .AddProtectiveMonitoring(configuration)
            .AddGcDocumentProvider(configuration)
            .AddDaeraCertificatesHealthChecks()
            .AddDocumentRetrievalApi();
    }

    private static IServiceCollection AddApiOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ApimExternalApisSettings>().Bind(configuration.GetSection(ApimExternalApisSettings.OptionsName));
        var ehcoBlobConfig = configuration.GetSection(GcDocumentBlobStorageOptions.OptionsName);
        services.AddOptions<GcDocumentBlobStorageOptions>().Bind(ehcoBlobConfig);
        return services;
    }

    private static IServiceCollection AddDocumentRetrievalApi(this IServiceCollection services)
    {
        return services
            .AddTransient<IDocumentRetrievalApi>(provider => new DocumentRetrievalApi(CreateConfigurationSettings(provider)));
    }

    private static IServiceCollection AddDaeraCertificatesHealthChecks(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddDbContextCheck<DaeraCertificateDbContext>();

        // System.Text.Json in .NET 10 pre-registers UnsupportedTypeConverter<MethodBase> in
        // JsonSerializerOptions.Converters. Using Add() appends after it, so the unsupported
        // converter wins. Insert(0, ...) ensures our converter is checked first.
        // CanConvert is overridden to cover all MethodBase subtypes (e.g. RuntimeMethodInfo)
        // because Exception.TargetSite is never the abstract MethodBase at runtime.
        services.Configure<JsonOptions>(options =>
        {
            options.JsonSerializerOptions.Converters.Insert(0, new MethodBaseJsonConverter());
        });

        return services;
    }

    private static IServiceCollection AddLogicRegistrations(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddScoped<IGeneralCertificateAggregatorService, GeneralCertificateAggregatorService>()
            .AddTransient<IGeneralCertificateCompletionService, GeneralCertificateCompletionService>()
            .AddTransient<IEhcoNotificationService, EhcoNotificationService>()
            .AddTransient<IServiceBusClientFactory, ServiceBusClientFactory>()
            .AddScoped<IRemosService, RemosService>()
            .Configure<EhcoNotificationOptions>(configuration.GetSection("Queues:EhcoGc:Status"));
    }

    private static IServiceCollection AddProtectiveMonitoring(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProtectiveMonitoring(
            configuration,
            options => configuration.Bind(ProtectiveMonitoringSettings.OptionsName, options));
        return services;
    }

    private static IServiceCollection AddRepositoryRegistrations(this IServiceCollection services, IConfiguration configuration)
    {
        // Strip the Authentication= keyword before passing the connection string to EF Core.
        // SqlClient v7 resolves connection-pool options before ConnectionOpening fires, so the
        // keyword must be absent at UseSqlServer() time, not patched later in the interceptor.
        // The interceptor then supplies the Entra ID token via SqlConnection.AccessToken instead.
        // NOTE: Must use SqlConnectionStringBuilder (not the generic DbConnectionStringBuilder)
        // because the base class does not recognise SQL Server-specific keywords and Remove() is a no-op.
        var rawConnectionString = configuration.GetConnectionString("sql_db_ef") ?? string.Empty;
        var csb = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(rawConnectionString);
        csb.Remove("Authentication");

        return services
            .AddScoped<ICertificatesStoreRepository, CertificatesStoreRepository>()
            .AddDbContext<DaeraCertificateDbContext>(options =>
                options.UseSqlServer(csb.ConnectionString)
                       .UseLazyLoadingProxies()
                       .AddInterceptors(new ManagedIdentityConnectionInterceptor()));
    }

    private static IServiceCollection AddSwaggerExamples(this IServiceCollection services)
    {
        services.AddSwaggerExamplesFromAssemblyOf<GeneralCertificateExample>();

        return services;
    }

    private static IServiceCollection AddV1Registrations(this IServiceCollection services)
    {
        return services
            .AddScoped<IDateTimeProvider, DateTimeProvider>()
            .AddScoped<IProtectiveMonitoringService, ProtectiveMonitoringService>()
            .AddScoped<IModelMapper<Database.Models.GeneralCertificate, GcModels.GeneralCertificate>,
                GeneralCertificateMapper>();
    }

    private static Configuration CreateConfigurationSettings(IServiceProvider provider)
    {
        var authService = provider.GetService<IAuthenticationService>();
        var apimSettings = provider.GetService<IOptions<InternalApimSettings>>()!.Value;
        string authToken = authService.GetAuthenticationHeaderAsync().Result.ToString();
        var config = new Configuration
        {
            BasePath = $"{apimSettings.BaseUrl}{apimSettings.DaeraInternalCertificateStoreApi}",
            DefaultHeaders = new Dictionary<string, string>
                        {
                            { "Authorization", authToken },
                            { apimSettings.SubscriptionKeyHeaderName, apimSettings.SubscriptionKey }
                        }
        };

        return config;
    }
}
