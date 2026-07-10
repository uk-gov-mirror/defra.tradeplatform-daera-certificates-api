// Copyright DEFRA (c). All rights reserved.
// Licensed under the Open Government License v3.0.

using Defra.Trade.API.Daera.Certificates.IntegrationTests.Helpers;
using Defra.Trade.API.Daera.Certificates.IntegrationTests.Infrastructure;
using Defra.Trade.API.Daera.Certificates.V1.Dtos;
using Defra.Trade.API.Daera.Certificates.V1.Dtos.Enums;
using Defra.Trade.Common.Api.Dtos;
using Defra.Trade.Common.ExternalApi.Auditing.Models.Enums;
using Microsoft.AspNetCore.Http;
using DbModels = Defra.Trade.API.Daera.Certificates.Database.Models;

namespace Defra.Trade.API.Daera.Certificates.IntegrationTests.V1.Controllers.GeneralCertificatesSummaryControllerTests;

public class GetSummaryTests(DaeraCertificatesApplicationFactory<Program> webApplicationFactory) : IClassFixture<DaeraCertificatesApplicationFactory<Program>>
{
    private readonly string _defaultClientIpAddress = "12.34.56.789";
    private readonly DaeraCertificatesApplicationFactory<Program> _webApplicationFactory = webApplicationFactory;

    [Fact]
    public async Task GetSummaries_NoQueryParametersRequest_Ok()
    {
        var pagedSummary =
            new DbModels.PagedGeneralCertificateSummary(
                new Fixture()
                    .Build<DbModels.GeneralCertificateSummary>()
                    .Without(s => s.Documents)
                    .CreateMany(1000),
                2000);

        _webApplicationFactory.CertificatesStoreRepository
            .Setup(r => r.GetGeneralCertificateSummariesAsync(
                It.Is<Database.Models.GeneralCertificateSummariesQuery>(
                    gc => gc.ModifiedSince == null
                          && gc.PageNumber == 1 && gc.PageSize == 1000 && gc.SortOrder == DbModels.Enum.SortOrder.Asc),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedSummary);

        var client = _webApplicationFactory.CreateClient();
        var clientId = Guid.NewGuid();
        _webApplicationFactory.AddApimUserContextHeaders(client, clientId, _defaultClientIpAddress);
        var sentAt = DateTime.UtcNow;

        // Act
        var response = await client.GetAsync("general-certificate-summaries");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsAsync<PagedResult<GeneralCertificateSummary>>();

        content.PageNumber.Should().Be(1);
        content.PageSize.Should().Be(1000);
        content.Records.Should().Be(1000);
        content.TotalPages.Should().Be(2);
        content.TotalRecords.Should().Be(2000);
        content.Data.Should().HaveCount(1000);

        var firstApp = content.Data.First();
        var firstExpected = pagedSummary.Data.First();

        firstApp.GeneralCertificateId.Should().Be(firstExpected.GeneralCertificateId);
        firstApp.Status.Should().Be(CertificateStatus.Complete);
        firstApp.CreatedOn.Should().Be(firstExpected.CreatedOn);
        firstApp.LastUpdated.Should().Be(firstExpected.LastUpdated);
        firstApp.Documents.Should().BeNullOrEmpty();

        firstApp.Links.Should().HaveCount(2);
        Assert.Contains(firstApp.Links, l => l.IsLinkToGetGeneralCertificateById(firstApp.GeneralCertificateId));
        Assert.Contains(firstApp.Links, l => l.IsLinkToGetGeneralCertificateSummaryById(firstApp.GeneralCertificateId));

        _webApplicationFactory.AuditRepository.VerifyAuditLogged(AuditLogType.DaeraCertificatesV1GeneralCertificateSummaryGet,
            clientId, null, HttpMethods.Get,
            "/general-certificate-summaries", null, StatusCodes.Status200OK, sentAt, false, true, _defaultClientIpAddress);
    }

    [Fact]
    public async Task GetSummaries_WithParametersRequest_Ok()
    {
        // Arrange
        var generalCertificateSummariesQuery = new Database.Models.GeneralCertificateSummariesQuery
        {
            PageNumber = 2,
            PageSize = 20,
            ModifiedSince = new DateTimeOffset(2023, 3, 1, 0, 0, 0, TimeSpan.Zero),
            SortOrder = DbModels.Enum.SortOrder.Desc
        };

        var pagedSummary =
            new DbModels.PagedGeneralCertificateSummary(
                new Fixture()
                    .Build<DbModels.GeneralCertificateSummary>()
                    .Without(s => s.Documents)
                    .CreateMany(20),
                45);

        _webApplicationFactory.CertificatesStoreRepository
            .Setup(r => r.GetGeneralCertificateSummariesAsync(
                It.Is<Database.Models.GeneralCertificateSummariesQuery>(
                    gc => gc.ModifiedSince == generalCertificateSummariesQuery.ModifiedSince &&
                    gc.PageNumber == generalCertificateSummariesQuery.PageNumber &&
                    gc.PageSize == generalCertificateSummariesQuery.PageSize &&
                    gc.SortOrder == generalCertificateSummariesQuery.SortOrder),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedSummary);

        var client = _webApplicationFactory.CreateClient();
        var clientId = Guid.NewGuid();

        _webApplicationFactory.AddApimUserContextHeaders(client, clientId, _defaultClientIpAddress);

        var sentAt = DateTime.UtcNow;

        // Act
        var response = await client.GetAsync($"general-certificate-summaries?pageNumber={generalCertificateSummariesQuery.PageNumber}&pageSize={generalCertificateSummariesQuery.PageSize}&modifiedSince={generalCertificateSummariesQuery.ModifiedSince.Value:yyyy-MM-ddTHH:mm:ss.fffZ}&sortOrder={generalCertificateSummariesQuery.SortOrder}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsAsync<PagedResult<GeneralCertificateSummary>>();

        content.PageNumber.Should().Be(2);
        content.PageSize.Should().Be(20);
        content.Records.Should().Be(20);
        content.TotalPages.Should().Be(3);
        content.TotalRecords.Should().Be(45);
        content.Data.Should().HaveCount(20);

        var firstApp = content.Data.First();
        var firstExpected = pagedSummary.Data.First();

        firstApp.GeneralCertificateId.Should().Be(firstExpected.GeneralCertificateId);
        firstApp.Status.Should().Be(CertificateStatus.Complete);
        firstApp.CreatedOn.Should().Be(firstExpected.CreatedOn);
        firstApp.LastUpdated.Should().Be(firstExpected.LastUpdated);
        firstApp.Documents.Should().BeNullOrEmpty();

        firstApp.Links.Should().HaveCount(2);
        Assert.Contains(firstApp.Links, l => l.IsLinkToGetGeneralCertificateById(firstApp.GeneralCertificateId));
        Assert.Contains(firstApp.Links, l => l.IsLinkToGetGeneralCertificateSummaryById(firstApp.GeneralCertificateId));

        _webApplicationFactory.AuditRepository.VerifyAuditLogged(AuditLogType.DaeraCertificatesV1GeneralCertificateSummaryGet,
            clientId, null, HttpMethods.Get,
            "/general-certificate-summaries", "?pageNumber=2&pageSize=20&modifiedSince=2023-03-01T00:00:00.000Z&sortOrder=Desc", StatusCodes.Status200OK, sentAt, false, true, _defaultClientIpAddress);
    }

    [Fact]
    public async Task GetSummaries_InvalidPageNumber_BadRequest()
    {
        var client = _webApplicationFactory.CreateClient();
        var clientId = Guid.NewGuid();

        _webApplicationFactory.AddApimUserContextHeaders(client, clientId, _defaultClientIpAddress);

        var sentAt = DateTime.UtcNow;

        // Act
        var response = await client.GetAsync("general-certificate-summaries?pageNumber=-1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsAsync<CommonProblemDetails>();
        content.VerifyBadRequest();

        _webApplicationFactory.AuditRepository.VerifyAuditLogged(AuditLogType.DaeraCertificatesV1GeneralCertificateSummaryGet,
            clientId, null, HttpMethods.Get,
            "/general-certificate-summaries", "?pageNumber=-1", StatusCodes.Status400BadRequest, sentAt, false, false, _defaultClientIpAddress);
    }
}
