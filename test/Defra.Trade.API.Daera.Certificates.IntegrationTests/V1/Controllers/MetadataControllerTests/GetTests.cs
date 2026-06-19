// Copyright DEFRA (c). All rights reserved.
// Licensed under the Open Government License v3.0.

using Defra.Trade.API.Daera.Certificates.IntegrationTests.Helpers;
using Defra.Trade.API.Daera.Certificates.IntegrationTests.Infrastructure;
using Defra.Trade.Common.Api.Dtos;
using Defra.Trade.Common.ExternalApi.Auditing.Models.Enums;
using Microsoft.AspNetCore.Http;
using DaeraCertificatesDtos = Defra.Trade.API.Daera.Certificates.V1.Dtos;

namespace Defra.Trade.API.Daera.Certificates.IntegrationTests.V1.Controllers.MetadataControllerTests;

public class GetTests(DaeraCertificatesApplicationFactory<Program> webApplicationFactory) : IClassFixture<DaeraCertificatesApplicationFactory<Program>>
{
    private readonly string _defaultClientIpAddress = "12.34.56.789";
    private readonly DaeraCertificatesApplicationFactory<Program> _webApplicationFactory = webApplicationFactory;

    [Fact]
    public async Task Get_Default_OK()
    {
        // Arrange
        var client = _webApplicationFactory.CreateClient();
        var clientId = Guid.NewGuid();

        _webApplicationFactory.AddApimUserContextHeaders(client, clientId, _defaultClientIpAddress);

        var sentAt = DateTime.UtcNow;

        // Act
        var response = await client.GetAsync("metadata");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsAsync<DaeraCertificatesDtos.ServiceMetadata>();

        content.Links.Should().HaveCount(4, "because 4 links are required").And
            .Contain(link => link.IsLinkToGeneralCertificatesSummaries()).And
            .Contain(link => link.IsLinkToGetGeneralCertificateSummaryById("{gcId}")).And
            .Contain(link => link.IsLinkToGetGeneralCertificateById("{gcId}")).And
            .Contain(link => link.IsLinkToGetGeneralCertificateDocument("{gcId}", "{documentId}"));

        _webApplicationFactory.AuditRepository.VerifyAuditLogged(AuditLogType.DaeraCertificatesV1Metadata,
            clientId, null, HttpMethods.Get,
            "/metadata", null, StatusCodes.Status200OK, sentAt, false, false, _defaultClientIpAddress);
    }

    [Fact]
    public async Task Get_MissingClientId_Forbidden()
    {
        // Arrange
        var client = _webApplicationFactory.CreateClient();

        _webApplicationFactory.AddApimUserContextHeaders(client, Guid.Empty, _defaultClientIpAddress);

        // Act
        var response = await client.GetAsync("metadata");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var content = await response.Content.ReadAsAsync<CommonProblemDetails>();

        content.VerifyForbidden();
    }
}
