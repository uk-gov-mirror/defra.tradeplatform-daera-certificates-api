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
/// Acquires a Managed Identity access token and sets it on every SQL connection opened
/// by EF Core, replacing the legacy <c>Authentication=ActiveDirectoryManagedIdentity</c>
/// connection-string keyword that was removed from <c>Microsoft.Data.SqlClient</c> v6+.
/// Uses <see cref="SqlConnection.AccessToken"/> (plain string) to avoid type-ambiguity
/// between <c>Microsoft.Data.SqlClient</c> and <c>Microsoft.Data.SqlClient.Extensions.Abstractions</c>.
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

        StripAuthenticationKeyword(sqlConnection);
        var token = await Credential.GetTokenAsync(SqlTokenRequestContext, ct);
        sqlConnection.AccessToken = token.Token;
    }

    private static void ApplyAccessToken(DbConnection connection)
    {
        if (connection is not SqlConnection sqlConnection)
            return;

        StripAuthenticationKeyword(sqlConnection);
        var token = Credential.GetToken(SqlTokenRequestContext, default);
        sqlConnection.AccessToken = token.Token;
    }

    private static void StripAuthenticationKeyword(SqlConnection sqlConnection)
    {
        // SqlClient v6+ removed the built-in provider for Authentication=ActiveDirectoryManagedIdentity.
        // Remove the keyword so SqlClient does not attempt a (missing) provider lookup.
        if (!sqlConnection.ConnectionString.Contains("Authentication=", StringComparison.OrdinalIgnoreCase))
            return;

        var builder = new SqlConnectionStringBuilder(sqlConnection.ConnectionString);
        builder.Remove("Authentication");
        sqlConnection.ConnectionString = builder.ConnectionString;
    }
}
