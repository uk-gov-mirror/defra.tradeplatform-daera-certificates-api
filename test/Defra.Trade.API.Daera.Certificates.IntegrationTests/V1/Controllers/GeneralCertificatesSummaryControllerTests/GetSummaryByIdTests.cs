// Copyright DEFRA (c). All rights reserved.
// Licensed under the Open Government License v3.0.

using Defra.Trade.API.Daera.Certificates.IntegrationTests.Helpers;
using Defra.Trade.API.Daera.Certificates.IntegrationTests.Infrastructure;
using Defra.Trade.API.Daera.Certificates.V1.Dtos.Enums;
using Defra.Trade.Common.Api.Dtos;
using Defra.Trade.Common.ExternalApi.Auditing.Models.Enums;
using Microsoft.AspNetCore.Http;
using GeneralCertificateSummary = Defra.Trade.API.Daera.Certificates.V1.Dtos.GeneralCertificateSummary;

namespace Defra.Trade.API.Daera.Certificates.IntegrationTests.V1.Controllers.GeneralCertificatesSummaryControllerTests;

public class GetSummaryByIdTests(DaeraCertificatesApplicationFactory<Program> webApplicationFactory) : IClassFixture<DaeraCertificatesApplicationFactory<Program>>
{
    private readonly string _defaultClientIpAddress = "12.34.56.789";
    private readonly DaeraCertificatesApplicationFactory<Program> _webApplicationFactory = webApplicationFactory;

    [Fact]
    public async Task GetSummaryById_KnownIdWithSingleDocument_Ok()
    {
        // Arrange
        var fixture = new Fixture();
        var gcCreatedOn = DateTime.Now.AddDays(10);
        var enrichmentCreatedOn = DateTime.Now.AddDays(10);
        var gcUpdatedOn = DateTime.Now.AddDays(10);
        var enrichmentUpdatedOn = DateTime.Now.AddDays(10);

        var savedGc = fixture.Create<Logic.Models.Ehco.EhcoGeneralCertificateApplication>();
        savedGc.ExchangedDocument.CertificatePDFLocation = null!;
        savedGc.ExchangedDocument.PackingListFileLocation = "https://www.defra.org.uk/remos/packinglists/sample.jpeg";
        string gcId = savedGc.ExchangedDocument.ID;
        var dbRow = fixture
            .Build<Database.Models.GeneralCertificate>()
            .With(p => p.GeneralCertificateId, gcId)
            .With(p => p.CreatedOn, gcCreatedOn)
            .With(p => p.LastUpdatedOn, gcUpdatedOn)
            .With(p => p.Data, JsonSerializer.Serialize(savedGc, GetSerializerOptions()))
            .Without(p => p.GeneralCertificateDocument)
            .With(p => p.EnrichmentData,
                fixture.Build<Database.Models.EnrichmentData>()
                    .With(x => x.CreatedOn, enrichmentCreatedOn)
                    .With(x => x.LastUpdatedOn, enrichmentUpdatedOn)
                .Without(e => e.Data)
                .Without(e => e.GeneralCertificate)
                .Create())
            .Create();

        _webApplicationFactory.CertificatesStoreRepository
            .Setup(r => r.GetGeneralCertificateSummaryAsync(gcId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbRow);
        var client = _webApplicationFactory.CreateClient();
        var clientId = Guid.NewGuid();

        _webApplicationFactory.AddApimUserContextHeaders(client, clientId, _defaultClientIpAddress);

        var sentAt = DateTime.UtcNow;

        // Act
        var response = await client.GetAsync($"general-certificate-summary?gcId={gcId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsAsync<GeneralCertificateSummary>();

        content.GeneralCertificateId.Should().Be(gcId);
        content.Status.Should().Be(CertificateStatus.Complete);
        content.Documents.Count.Should().Be(1);
        var firstDoc = content.Documents.First();
        firstDoc.Id.Should().Be("sample.jpeg");
        firstDoc.TypeCode.Should().Be("271");
        content.LastUpdated.Should().Be(dbRow.EnrichmentData.LastUpdatedOn);
        content.CreatedOn.Should().Be(dbRow.CreatedOn);

        content.Links
            .Should().HaveCount(1, "because 1 link id required")
            .And.Contain(link => link.IsLinkToGetGeneralCertificateById(content.GeneralCertificateId));

        _webApplicationFactory.AuditRepository.VerifyAuditLogged(AuditLogType.DaeraCertificatesV1GeneralCertificateSummaryGetById,
            clientId, gcId, HttpMethods.Get,
            "/general-certificate-summary", $"?gcId={gcId}", StatusCodes.Status200OK, sentAt, false, true, _defaultClientIpAddress);
    }

    [Fact]
    public async Task GetSummaryById_KnownIdWithTwoDocuments_Ok()
    {
        // Arrange
        var fixture = new Fixture();
        var gcCreatedOn = DateTime.Now.AddDays(10);
        var enrichmentCreatedOn = DateTime.Now.AddDays(10);
        var gcUpdatedOn = DateTime.Now.AddDays(10);
        var enrichmentUpdatedOn = DateTime.Now.AddDays(10);

        var savedGc = fixture.Create<Logic.Models.Ehco.EhcoGeneralCertificateApplication>();
        savedGc.ExchangedDocument.PackingListFileLocation = "https://www.defra.org.uk/remos/packinglists/sample.jpeg";
        savedGc.ExchangedDocument.CertificatePDFLocation = "https://www.defra.org.uk/remos/packinglists/anotherSample.jpeg";
        string gcId = savedGc.ExchangedDocument.ID;
        var dbRow = fixture
            .Build<Database.Models.GeneralCertificate>()
            .With(p => p.GeneralCertificateId, gcId)
            .With(p => p.CreatedOn, gcCreatedOn)
            .With(p => p.LastUpdatedOn, gcUpdatedOn)
            .With(p => p.Data, JsonSerializer.Serialize(savedGc, GetSerializerOptions()))
            .Without(p => p.GeneralCertificateDocument)
            .With(p => p.EnrichmentData,
                fixture.Build<Database.Models.EnrichmentData>()
                    .With(x => x.CreatedOn, enrichmentCreatedOn)
                    .With(x => x.LastUpdatedOn, enrichmentUpdatedOn)
                .Without(e => e.Data)
                .Without(e => e.GeneralCertificate)
                .Create())
            .Create();

        _webApplicationFactory.CertificatesStoreRepository
            .Setup(r => r.GetGeneralCertificateSummaryAsync(gcId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbRow);
        var client = _webApplicationFactory.CreateClient();
        var clientId = Guid.NewGuid();

        _webApplicationFactory.AddApimUserContextHeaders(client, clientId, _defaultClientIpAddress);

        var sentAt = DateTime.UtcNow;

        // Act
        var response = await client.GetAsync($"general-certificate-summary?gcId={gcId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsAsync<GeneralCertificateSummary>();

        content.GeneralCertificateId.Should().Be(gcId);
        content.Status.Should().Be(CertificateStatus.Complete);

        content.Documents.Count.Should().Be(2);
        var firstDoc = content.Documents.First();
        firstDoc.Id.Should().Be("sample.jpeg");
        firstDoc.TypeCode.Should().Be("271");
        var secondDoc = content.Documents.ToList()[1];
        secondDoc.Id.Should().Be("anotherSample.jpeg");
        secondDoc.TypeCode.Should().Be("16");

        content.LastUpdated.Should().Be(dbRow.EnrichmentData.LastUpdatedOn);
        content.CreatedOn.Should().Be(dbRow.CreatedOn);

        content.Links
            .Should().HaveCount(1, "because 1 link id required")
            .And.Contain(link => link.IsLinkToGetGeneralCertificateById(content.GeneralCertificateId));

        _webApplicationFactory.AuditRepository.VerifyAuditLogged(AuditLogType.DaeraCertificatesV1GeneralCertificateSummaryGetById,
            clientId, gcId, HttpMethods.Get,
            "/general-certificate-summary", $"?gcId={gcId}", StatusCodes.Status200OK, sentAt, false, true, _defaultClientIpAddress);
    }

    [Fact]
    public async Task GetSummaryById_UnknownId_NotFound()
    {
        // Arrange
        var client = _webApplicationFactory.CreateClient();
        var clientId = Guid.NewGuid();
        string gcId = Guid.NewGuid().ToString();

        _webApplicationFactory.AddApimUserContextHeaders(client, clientId, _defaultClientIpAddress);

        var sentAt = DateTime.UtcNow;

        // Act
        var response = await client.GetAsync($"general-certificate-summary?gcId={gcId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsAsync<CommonProblemDetails>();

        content.VerifyNotFound();

        _webApplicationFactory.AuditRepository.VerifyAuditLogged(AuditLogType.DaeraCertificatesV1GeneralCertificateSummaryGetById,
            clientId, gcId, HttpMethods.Get,
            "/general-certificate-summary", $"?gcId={gcId}", StatusCodes.Status404NotFound, sentAt, false, false, _defaultClientIpAddress);
    }

    private static JsonSerializerOptions GetSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}
