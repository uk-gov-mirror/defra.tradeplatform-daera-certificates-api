// Copyright DEFRA (c). All rights reserved.
// Licensed under the Open Government License v3.0.

using Defra.Trade.API.Daera.Certificates.Database.Models;
using Defra.Trade.API.Daera.Certificates.IntegrationTests.Helpers;
using Defra.Trade.API.Daera.Certificates.IntegrationTests.Infrastructure;
using Defra.Trade.Common.Api.Dtos;
using Defra.Trade.Common.ExternalApi.Auditing.Models.Enums;
using Microsoft.AspNetCore.Http;

namespace Defra.Trade.API.Daera.Certificates.IntegrationTests.V1.Controllers.GeneralCertificateDocumentsControllerTests;

public class GetGeneralCertificateDocumentByIdTests(DaeraCertificatesApplicationFactory<Program> webApplicationFactory) : IClassFixture<DaeraCertificatesApplicationFactory<Program>>
{
    private readonly string _defaultClientIpAddress = "12.34.56.789";
    private readonly DaeraCertificatesApplicationFactory<Program> _webApplicationFactory = webApplicationFactory;

    [Fact]
    public async Task GetGeneralCertificateDocumentById_UnknownGcId_NotFound()
    {
        // Arrange
        var client = _webApplicationFactory.CreateClient();
        var clientId = Guid.NewGuid();
        string gcId = Guid.NewGuid().ToString();

        _webApplicationFactory.AddApimUserContextHeaders(client, clientId, _defaultClientIpAddress);

        var sentAt = DateTime.UtcNow;

        // Act
        var response = await client.GetAsync($"general-certificate/document?gcId={gcId}&documentId=1");

        // Assert
        var content = await response.Content.ReadAsAsync<CommonProblemDetails>();
        content.VerifyNotFound();

        _webApplicationFactory.AuditRepository.VerifyAuditLogged(AuditLogType.DaeraCertificatesV1GeneralCertificateDocumentGetById,
            clientId, gcId, HttpMethods.Get,
            "/general-certificate/document", $"?gcId={gcId}&documentId=1", StatusCodes.Status404NotFound, sentAt, false, false, _defaultClientIpAddress);
    }

    [Fact]
    public async Task GetGeneralCertificateDocumentById_UnknownDocumentId_NotFound()
    {
        // Arrange
        var fixture = new Fixture();
        var client = _webApplicationFactory.CreateClient();
        var clientId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        const string gcId = "GC-12001";
        var gcCreatedOn = DateTime.Now.AddDays(10);
        var enrichmentCreatedOn = DateTime.Now.AddDays(10);
        var gcUpdatedOn = DateTime.Now.AddDays(10);
        var enrichmentUpdatedOn = DateTime.Now.AddDays(10);
        string mockedBlobUri = "https://sndexpinfsto002.blob.core.windows.net/application-forms/1651965954449/supplementary-documents/testehcdefault.jpeg";
        var savedGc = fixture.Create<Logic.Models.Ehco.EhcoGeneralCertificateApplication>();
        var docRetrieved = DateTime.UtcNow.AddDays(11);
        savedGc.ExchangedDocument.PackingListFileLocation = mockedBlobUri;

        var gcDocuments = fixture.Build<Database.Models.GeneralCertificateDocument>()
            .With(x => x.Url, string.Empty)
            .With(x => x.CreatedOn, enrichmentCreatedOn)
            .With(x => x.GeneralCertificate, new GeneralCertificate { GeneralCertificateId = gcId, Id = entityId })
            .With(x => x.LastUpdatedOn, enrichmentUpdatedOn)
            .With(x => x.Retrieved, docRetrieved)
            .CreateMany(1).ToList();

        var dbRow = fixture
            .Build<Database.Models.GeneralCertificate>()
            .With(x => x.Id, entityId)
            .With(p => p.GeneralCertificateId, gcId)
            .With(p => p.CreatedOn, gcCreatedOn)
            .With(p => p.LastUpdatedOn, gcUpdatedOn)
            .With(p => p.Data, JsonSerializer.Serialize(savedGc, GetSerializerOptions()))
            .With(p => p.GeneralCertificateDocument, gcDocuments)
            .With(p => p.EnrichmentData,
                fixture.Build<Database.Models.EnrichmentData>()
                    .With(x => x.CreatedOn, enrichmentCreatedOn)
                    .With(x => x.LastUpdatedOn, enrichmentUpdatedOn)
                    .Without(e => e.Data)
                    .Without(e => e.GeneralCertificate)
                    .Create())
            .Create();

        _webApplicationFactory.CertificatesStoreRepository
            .Setup(r => r.GetGeneralCertificateWithDocumentsAsync(gcId, "10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbRow);

        _webApplicationFactory.DocumentRetrievalApi
            .Setup(r => r.SaveDocumentRetrievedAsync(gcDocuments[0].Id, null, 0, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _webApplicationFactory.AddApimUserContextHeaders(client, clientId, _defaultClientIpAddress);

        var sentAt = DateTime.UtcNow;

        // Act
        var response = await client.GetAsync($"general-certificate/document?gcId={gcId}&documentId=10");

        // Assert
        var content = await response.Content.ReadAsAsync<CommonProblemDetails>();
        content.VerifyNotFound();

        _webApplicationFactory.AuditRepository.VerifyAuditLogged(AuditLogType.DaeraCertificatesV1GeneralCertificateDocumentGetById,
            clientId, gcId, HttpMethods.Get,
            "/general-certificate/document", $"?gcId={gcId}&documentId=10", StatusCodes.Status404NotFound, sentAt, false, false, _defaultClientIpAddress);
    }

    [Fact]
    public async Task GetGeneralCertificateDocumentById_ValidIds_DocumentOk()
    {
        // Arrange
        var client = _webApplicationFactory.CreateClient();
        var clientId = Guid.NewGuid();
        string stubImage = "R0lGODlhAQABAAAAACH5BAEKAAEALAAAAAABAAEAAAICTAEAOw==";

        var fixture = new Fixture();
        using var blobFromAzure = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(stubImage));
        var gcCreatedOn = DateTime.Now.AddDays(10);
        var enrichmentCreatedOn = DateTime.Now.AddDays(10);
        var gcUpdatedOn = DateTime.Now.AddDays(10);
        var enrichmentUpdatedOn = DateTime.Now.AddDays(10);
        var docRetrieved = DateTime.UtcNow.AddDays(11);

        string mockedBlobUri = "https://sndexpinfsto002.blob.core.windows.net/application-forms/1651965954449/supplementary-documents/testehcdefault.jpeg";
        var savedGc = fixture.Create<Logic.Models.Ehco.EhcoGeneralCertificateApplication>();
        savedGc.ExchangedDocument.PackingListFileLocation = mockedBlobUri;
        string gcId = savedGc.ExchangedDocument.ID;

        var gcEntityId = Guid.NewGuid();
        var gcDocuments = fixture.Build<Database.Models.GeneralCertificateDocument>()
            .With(x => x.Url, mockedBlobUri)
            .With(x => x.CreatedOn, enrichmentCreatedOn)
            .With(x => x.GeneralCertificate, new GeneralCertificate { GeneralCertificateId = gcId, Id = gcEntityId })
            .With(x => x.LastUpdatedOn, enrichmentUpdatedOn)
            .With(x => x.Retrieved, docRetrieved)
          .CreateMany(1).ToList();

        var dbRow = fixture
            .Build<Database.Models.GeneralCertificate>()
            .With(x => x.Id, gcEntityId)
            .With(p => p.GeneralCertificateId, gcId)
            .With(p => p.CreatedOn, gcCreatedOn)
            .With(p => p.LastUpdatedOn, gcUpdatedOn)
            .With(p => p.Data, JsonSerializer.Serialize(savedGc, GetSerializerOptions()))
            .With(p => p.GeneralCertificateDocument, gcDocuments)
            .With(p => p.EnrichmentData,
                fixture.Build<Database.Models.EnrichmentData>()
                    .With(x => x.CreatedOn, enrichmentCreatedOn)
                    .With(x => x.LastUpdatedOn, enrichmentUpdatedOn)
                    .Without(e => e.Data)
                    .Without(e => e.GeneralCertificate)
                    .Create())
            .Create();

        string documentId = dbRow.GeneralCertificateDocument.FirstOrDefault().DocumentId;
        _webApplicationFactory.CertificatesStoreRepository
            .Setup(r => r.GetGeneralCertificateWithDocumentsAsync(gcId, documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbRow);

        _webApplicationFactory.DocumentRetrievalApi
            .Setup(r => r.SaveDocumentRetrievedAsync(gcDocuments[0].Id, null, 0, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _webApplicationFactory.AddApimUserContextHeaders(client, clientId, _defaultClientIpAddress);

        var sentAt = DateTime.UtcNow;

        // Act
        var response = await client.GetAsync($"general-certificate/document?gcId={gcId}&documentId={documentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().BeEquivalentTo("image/jpeg");

        var responseContent = await response.Content.ReadAsStreamAsync();
        responseContent.ToString().Should().BeSameAs(blobFromAzure.ToString());

        _webApplicationFactory.AuditRepository.VerifyAuditLogged(AuditLogType.DaeraCertificatesV1GeneralCertificateDocumentGetById,
            clientId, gcId, HttpMethods.Get,
            "/general-certificate/document", $"?gcId={gcId}&documentId={documentId}", StatusCodes.Status200OK, sentAt, false, false, _defaultClientIpAddress);
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
