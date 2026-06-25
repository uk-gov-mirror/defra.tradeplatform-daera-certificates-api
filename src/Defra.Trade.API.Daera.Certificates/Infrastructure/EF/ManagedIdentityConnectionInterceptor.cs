// Copyright DEFRA (c). All rights reserved.
// Licensed under the Open Government License v3.0.

using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Defra.Trade.API.Daera.Certificates.Infrastructure.EF;

/// <summary>
/// Acquires a Managed Identity access token and sets it on every SQL connection opened by EF Core.
/// The <c>Authentication=</c> keyword is stripped from the connection string at configuration time
/// (see <c>AddRepositoryRegistrations</c>); this interceptor supplies the Entra ID bearer token
/// via <see cref="SqlConnection.AccessToken"/> so SqlClient authenticates without needing a provider.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Infrastructure plumbing covered by integration tests.")]
internal sealed class ManagedIdentityConnectionInterceptor : DbConnectionInterceptor
{
    private static readonly TokenCredential Credential = new DefaultAzureCredential();
    private static readonly TokenRequestContext SqlTokenRequestContext =
        new(["https://database.windows.net/.default"]);

    public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        await ApplyAccessTokenAsync(connection, cancellationToken);
        return await base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
    }

    public override InterceptionResult ConnectionOpening(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        ApplyAccessToken(connection);
        return base.ConnectionOpening(connection, eventData, result);
    }

    private static async Task ApplyAccessTokenAsync(DbConnection connection, CancellationToken ct)
    {
        if (connection is not SqlConnection sqlConnection)
            return;

        var token = await Credential.GetTokenAsync(SqlTokenRequestContext, ct);
        sqlConnection.AccessToken = token.Token;
    }

    private static void ApplyAccessToken(DbConnection connection)
    {
        if (connection is not SqlConnection sqlConnection)
            return;

        var token = Credential.GetToken(SqlTokenRequestContext, default);
        sqlConnection.AccessToken = token.Token;
    }
}
