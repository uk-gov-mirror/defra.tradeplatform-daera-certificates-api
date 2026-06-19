// Copyright DEFRA (c). All rights reserved.
// Licensed under the Open Government License v3.0.

using Defra.Trade.API.Daera.Certificates.IntegrationTests.Helpers;
using Defra.Trade.API.Daera.Certificates.IntegrationTests.Infrastructure;
using Defra.Trade.Common.Api.Dtos;
using Defra.Trade.Common.ExternalApi.Auditing.Models.Enums;
using Microsoft.AspNetCore.Http;
using GeneralCertificate = Defra.Trade.API.Daera.Certificates.V1.Dtos.GeneralCertificate.GeneralCertificate;

namespace Defra.Trade.API.Daera.Certificates.IntegrationTests.V1.Controllers.GeneralCertificatesControllerTests;

public class GetGeneralCertificateByIdTests(DaeraCertificatesApplicationFactory<Program> webApplicationFactory) : IClassFixture<DaeraCertificatesApplicationFactory<Program>>
{
    private readonly string _defaultClientIpAddress = "12.34.56.789";
    private readonly DaeraCertificatesApplicationFactory<Program> _webApplicationFactory = webApplicationFactory;

    [Fact]
    public async Task GetGeneralCertificateById_ValidId_Ok()
    {
        // Arrange
        var savedGc = new Fixture().Create<Logic.Models.Ehco.EhcoGeneralCertificateApplication>();

        var customerContact = new Fixture()
            .Build<Logic.Models.Idcoms.CustomerContact>()
            .With(c => c.DefraCustomerId, savedGc.ExchangedDocument.Applicant.DefraCustomer.UserId)
            .Create();
        var customerOrganisation = new Fixture()
            .Build<Logic.Models.Idcoms.Organisation>()
            .With(c => c.DefraCustomerId, savedGc.ExchangedDocument.Applicant.DefraCustomer.OrgId)
            .Create();
        var consignor = new Fixture()
            .Build<Logic.Models.Idcoms.Organisation>()
            .With(c => c.DefraCustomerId, savedGc.SupplyChainConsignment.Consignor.DefraCustomer.OrgId)
            .Create();
        var consignee = new Fixture()
            .Build<Logic.Models.Idcoms.Organisation>()
            .With(c => c.DefraCustomerId, savedGc.SupplyChainConsignment.Consignee.DefraCustomer.OrgId)
            .Create();
        var dispatchLocation = new Fixture()
            .Build<Logic.Models.Idcoms.Establishment>()
            .With(c => c.EstablishmentId, savedGc.SupplyChainConsignment.DispatchLocation.Idcoms.EstablishmentId)
            .Create();
        var destinationLocation = new Fixture()
            .Build<Logic.Models.Idcoms.Establishment>()
            .With(c => c.EstablishmentId, savedGc.SupplyChainConsignment.DestinationLocation.Idcoms.EstablishmentId)
            .Create();

        var savedEd = new Fixture()
            .Build<Logic.Models.Idcoms.IdcomsGeneralCertificateEnrichment>()
            .With(x => x.Applicant, customerContact)
            .With(e => e.Establishments,
                new List<Logic.Models.Idcoms.Establishment>
                {
                    destinationLocation,
                    dispatchLocation
                })
            .With(e => e.Organisations,
                new List<Logic.Models.Idcoms.Organisation>
                        {
                            customerOrganisation,
                            consignee,
                            consignor,
                        })
            .Create();

        string gcId = savedGc.ExchangedDocument.ID;
        var dbRow = new Fixture()
            .Build<Database.Models.GeneralCertificate>()
            .With(p => p.GeneralCertificateId, gcId)
            .With(p => p.Data, JsonSerializer.Serialize(savedGc, GetSerializerOptions()))
            .Without(p => p.GeneralCertificateDocument)
            .With(p => p.EnrichmentData, new Fixture()
                .Build<Database.Models.EnrichmentData>()
                .With(e => e.Data, JsonSerializer.Serialize(savedEd, GetSerializerOptions()))
                .Without(e => e.GeneralCertificate)
                .Create())
            .Create();

        _webApplicationFactory.CertificatesStoreRepository
            .Setup(r => r.GetGeneralCertificateWithEnrichmentAsync(dbRow.GeneralCertificateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbRow);

        var client = _webApplicationFactory.CreateClient();
        var clientId = Guid.NewGuid();

        _webApplicationFactory.AddApimUserContextHeaders(client, clientId, _defaultClientIpAddress);

        var sentAt = DateTime.UtcNow;

        // Act
        var response = await client.GetAsync($"general-certificate?gcId={gcId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsAsync<GeneralCertificate>();

        content.Should().NotBeNull();
        content.ExchangedDocument.Id.Content.Should().Be(gcId);
        content.ExchangedDocument.PrimarySignatoryAuthentication.ProvidingTradeParty.Id.Should()
            .Contain(i => i.SchemeId.Equals("RMS"));
        content.SupplyChainConsignment.Consignee.Should().NotBeNull();
        content.SupplyChainConsignment.Consignee.Id.Should()
            .Contain(i => i.Content.Equals(consignee.RmsId.ToString(), StringComparison.OrdinalIgnoreCase));
        content.SupplyChainConsignment.Consignee.Id.Should()
            .Contain(i => i.SchemeId.Equals("RMS"));
        content.SupplyChainConsignment.Consignee.Id.Should()
            .Contain(i => i.Content.Equals(consignee.RmsId.ToString(), StringComparison.OrdinalIgnoreCase));
        content.SupplyChainConsignment.Consignee.Id.Should()
            .Contain(i => i.SchemeId.Equals("RMS"));
        content.SupplyChainConsignment.Consignee.Name.Should()
            .Contain(i => i.Content.Equals(consignee.Name.ToString()));
        content.SupplyChainConsignment.Consignee.Name.Should()
            .Contain(i => i.LanguageId.Equals("en"));
        content.SupplyChainConsignment.Consignee.RoleCode.Should()
            .Contain(i => i.Content.Equals("DGP"));
        content.SupplyChainConsignment.CustomsID.Should()
            .Contain(i => i.SchemeId.Equals("GMR"));

        var consigneePostalAddress = content.SupplyChainConsignment.Consignee.PostalAddress;
        consigneePostalAddress.Postcode.Content.Should().Be(consignee.Address.PostCode);
        consigneePostalAddress.LineOne.Content.Should().Be(consignee.Address.AddressLine1);
        consigneePostalAddress.LineTwo.Content.Should().Be(consignee.Address.AddressLine2);
        consigneePostalAddress.LineThree.Content.Should().Be(consignee.Address.AddressLine3);
        consigneePostalAddress.LineFour.Content.Should().Be(consignee.Address.AddressLine4);
        consigneePostalAddress.LineFive.Content.Should().Be(consignee.Address.AddressLine5);
        consigneePostalAddress.CityName.Content.Should().Be(consignee.Address.Town);
        consigneePostalAddress.CountryCode.Content.Should().Be(consignee.Address.Country.Code);
        consigneePostalAddress.CountryCode.SchemeAgencyId.Should().Be("5");
        consigneePostalAddress.CountryName.Content.Should().Be(consignee.Address.Country.Name);
        consigneePostalAddress.CountryName.LanguageId.Should().Be("en");
        consigneePostalAddress.CountrySubDivisionCode.Content.Should().Be(consignee.Address.Country.SubDivisionCode);
        consigneePostalAddress.CountrySubDivisionCode.SchemeAgencyId.Should().Be("5");
        consigneePostalAddress.CountrySubDivisionName.Content.Should().Be(consignee.Address.Country.SubDivisionName);
        consigneePostalAddress.CountrySubDivisionName.LanguageId.Should().Be("en");
        consigneePostalAddress.TypeCode.Content.Should().Be("1");
        consigneePostalAddress.TypeCode.ListAgencyID.Should().Be("6");

        content.SupplyChainConsignment.Consignor.Should().NotBeNull();
        content.SupplyChainConsignment.Consignor.Id.Should()
            .Contain(i => i.Content.Equals(consignor.RmsId.ToString(), StringComparison.OrdinalIgnoreCase));
        content.SupplyChainConsignment.Consignor.Id.Should()
            .Contain(i => i.SchemeId.Equals("RMS"));
        content.SupplyChainConsignment.Consignor.Id.Should()
            .Contain(i => i.Content.Equals(consignor.RmsId.ToString(), StringComparison.OrdinalIgnoreCase));
        content.SupplyChainConsignment.Consignor.Id.Should()
            .Contain(i => i.SchemeId.Equals("RMS"));
        content.SupplyChainConsignment.Consignor.Name.Should()
            .Contain(i => i.Content.Equals(consignor.Name.ToString()));
        content.SupplyChainConsignment.Consignor.Name.Should()
            .Contain(i => i.LanguageId.Equals("en"));
        content.SupplyChainConsignment.Consignor.RoleCode.Should()
            .Contain(i => i.Content.Equals("DGP"));

        var consignorPostalAddress = content.SupplyChainConsignment.Consignor.PostalAddress;
        consignorPostalAddress.Postcode.Content.Should().Be(consignor.Address.PostCode);
        consignorPostalAddress.LineOne.Content.Should().Be(consignor.Address.AddressLine1);
        consignorPostalAddress.LineTwo.Content.Should().Be(consignor.Address.AddressLine2);
        consignorPostalAddress.LineThree.Content.Should().Be(consignor.Address.AddressLine3);
        consignorPostalAddress.LineFour.Content.Should().Be(consignor.Address.AddressLine4);
        consignorPostalAddress.LineFive.Content.Should().Be(consignor.Address.AddressLine5);
        consignorPostalAddress.CityName.Content.Should().Be(consignor.Address.Town);
        consignorPostalAddress.CountryCode.Content.Should().Be(consignor.Address.Country.Code);
        consignorPostalAddress.CountryCode.SchemeAgencyId.Should().Be("5");
        consignorPostalAddress.CountryName.Content.Should().Be(consignor.Address.Country.Name);
        consignorPostalAddress.CountryName.LanguageId.Should().Be("en");
        consignorPostalAddress.CountrySubDivisionCode.Content.Should().Be(consignor.Address.Country.SubDivisionCode);
        consignorPostalAddress.CountrySubDivisionCode.SchemeAgencyId.Should().Be("5");
        consignorPostalAddress.CountrySubDivisionName.Content.Should().Be(consignor.Address.Country.SubDivisionName);
        consignorPostalAddress.CountrySubDivisionName.LanguageId.Should().Be("en");
        consignorPostalAddress.TypeCode.Content.Should().Be("1");
        consignorPostalAddress.TypeCode.ListAgencyID.Should().Be("6");

        content.SupplyChainConsignment.DestinationLocation.Should().NotBeNull();
        content.SupplyChainConsignment.DestinationLocation.Id.Should()
            .Contain(i => i.Content.Equals(destinationLocation.RmsId.ToString(), StringComparison.OrdinalIgnoreCase));
        content.SupplyChainConsignment.DestinationLocation.Id.Should()
            .Contain(i => i.SchemeId.Equals("RMS"));
        content.SupplyChainConsignment.DestinationLocation.Id.Should()
            .Contain(i => i.Content.Equals(destinationLocation.RmsId.ToString(), StringComparison.OrdinalIgnoreCase));
        content.SupplyChainConsignment.DestinationLocation.Id.Should()
            .Contain(i => i.SchemeId.Equals("RMS"));
        content.SupplyChainConsignment.DestinationLocation.Name.Content.Should().Be(destinationLocation.Name);
        content.SupplyChainConsignment.DestinationLocation.Name.LanguageId.Should().Be("en");

        var destinationLocationAddress = content.SupplyChainConsignment.DestinationLocation.LocationAddress;
        destinationLocationAddress.Postcode.Content.Should().Be(destinationLocation.Address.PostCode);
        destinationLocationAddress.LineOne.Content.Should().Be(destinationLocation.Address.AddressLine1);
        destinationLocationAddress.LineTwo.Content.Should().Be(destinationLocation.Address.AddressLine2);
        destinationLocationAddress.LineThree.Content.Should().Be(destinationLocation.Address.AddressLine3);
        destinationLocationAddress.LineFour.Content.Should().Be(destinationLocation.Address.AddressLine4);
        destinationLocationAddress.LineFive.Content.Should().Be(destinationLocation.Address.AddressLine5);
        destinationLocationAddress.CityName.Content.Should().Be(destinationLocation.Address.Town);
        destinationLocationAddress.CountryCode.Content.Should().Be(destinationLocation.Address.Country.Code);
        destinationLocationAddress.CountryCode.SchemeAgencyId.Should().Be("5");
        destinationLocationAddress.CountryName.Content.Should().Be(destinationLocation.Address.Country.Name);
        destinationLocationAddress.CountryName.LanguageId.Should().Be("en");
        destinationLocationAddress.CountrySubDivisionCode.Content.Should().Be(destinationLocation.Address.Country.SubDivisionCode);
        destinationLocationAddress.CountrySubDivisionCode.SchemeAgencyId.Should().Be("5");
        destinationLocationAddress.CountrySubDivisionName.Content.Should().Be(destinationLocation.Address.Country.SubDivisionName);
        destinationLocationAddress.CountrySubDivisionName.LanguageId.Should().Be("en");
        destinationLocationAddress.TypeCode.Content.Should().Be("3");
        destinationLocationAddress.TypeCode.ListAgencyID.Should().Be("6");

        content.SupplyChainConsignment.DispatchLocation.Should().NotBeNull();
        content.SupplyChainConsignment.DispatchLocation.Id.Should()
            .Contain(i => i.Content.Equals(dispatchLocation.RmsId.ToString(), StringComparison.OrdinalIgnoreCase));
        content.SupplyChainConsignment.DispatchLocation.Id.Should()
            .Contain(i => i.SchemeId.Equals("RMS"));
        content.SupplyChainConsignment.DispatchLocation.Id.Should()
            .Contain(i => i.Content.Equals(dispatchLocation.RmsId.ToString(), StringComparison.OrdinalIgnoreCase));
        content.SupplyChainConsignment.DispatchLocation.Id.Should()
            .Contain(i => i.SchemeId.Equals("RMS"));
        content.SupplyChainConsignment.DispatchLocation.Name.Content.Should().Be(dispatchLocation.Name);
        content.SupplyChainConsignment.DispatchLocation.Name.LanguageId.Should().Be("en");
        content.SupplyChainConsignment.OperatorResponsibleForConsignment.Id.FirstOrDefault().Content
            .Should().Be(savedGc.SupplyChainConsignment.OperatorResponsibleForConsignment.Traces.OperatorId);

        var dispatchLocationAddress = content.SupplyChainConsignment.DispatchLocation.LocationAddress;
        dispatchLocationAddress.Postcode.Content.Should().Be(dispatchLocation.Address.PostCode);
        dispatchLocationAddress.LineOne.Content.Should().Be(dispatchLocation.Address.AddressLine1);
        dispatchLocationAddress.LineTwo.Content.Should().Be(dispatchLocation.Address.AddressLine2);
        dispatchLocationAddress.LineThree.Content.Should().Be(dispatchLocation.Address.AddressLine3);
        dispatchLocationAddress.LineFour.Content.Should().Be(dispatchLocation.Address.AddressLine4);
        dispatchLocationAddress.LineFive.Content.Should().Be(dispatchLocation.Address.AddressLine5);
        dispatchLocationAddress.CityName.Content.Should().Be(dispatchLocation.Address.Town);
        dispatchLocationAddress.CountryCode.Content.Should().Be(dispatchLocation.Address.Country.Code);
        dispatchLocationAddress.CountryCode.SchemeAgencyId.Should().Be("5");
        dispatchLocationAddress.CountryName.Content.Should().Be(dispatchLocation.Address.Country.Name);
        dispatchLocationAddress.CountryName.LanguageId.Should().Be("en");
        dispatchLocationAddress.CountrySubDivisionCode.Content.Should().Be(dispatchLocation.Address.Country.SubDivisionCode);
        dispatchLocationAddress.CountrySubDivisionCode.SchemeAgencyId.Should().Be("5");
        dispatchLocationAddress.CountrySubDivisionName.Content.Should().Be(dispatchLocation.Address.Country.SubDivisionName);
        dispatchLocationAddress.CountrySubDivisionName.LanguageId.Should().Be("en");
        dispatchLocationAddress.TypeCode.Content.Should().Be("3");
        dispatchLocationAddress.TypeCode.ListAgencyID.Should().Be("6");

        _webApplicationFactory.AuditRepository.VerifyAuditLogged(AuditLogType.DaeraCertificatesV1GeneralCertificateGetById,
            clientId, gcId, HttpMethods.Get,
            "/general-certificate", $"?gcId={gcId}", StatusCodes.Status200OK, sentAt, false, true, _defaultClientIpAddress);
    }

    [Fact]
    public async Task GetGeneralCertificateById_InvalidId_NotFound()
    {
        // Arrange
        var client = _webApplicationFactory.CreateClient();
        var clientId = Guid.NewGuid();

        _webApplicationFactory.AddApimUserContextHeaders(client, clientId, _defaultClientIpAddress);

        _webApplicationFactory.CertificatesStoreRepository
            .Setup(r => r.GetGeneralCertificateWithEnrichmentAsync("GC-12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Database.Models.GeneralCertificate)null);

        var sentAt = DateTime.UtcNow;

        // Act
        var response = await client.GetAsync("general-certificate?gcId=GC-12345");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsAsync<CommonProblemDetails>();

        content.VerifyNotFound();

        _webApplicationFactory.AuditRepository.VerifyAuditLogged(AuditLogType.DaeraCertificatesV1GeneralCertificateGetById,
            clientId, "GC-12345", HttpMethods.Get,
            "/general-certificate", "?gcId=GC-12345", StatusCodes.Status404NotFound, sentAt, false, false, _defaultClientIpAddress);
    }

    [Fact]
    public async Task GetGeneralCertificateById_MissingClientId_Forbidden()
    {
        // Arrange
        var client = _webApplicationFactory.CreateClient();

        _webApplicationFactory.AddApimUserContextHeaders(client, Guid.Empty, _defaultClientIpAddress);

        // Act
        var response = await client.GetAsync("general-certificate?gcId=ABC-123848");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var content = await response.Content.ReadAsAsync<CommonProblemDetails>();

        content.VerifyForbidden();
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
