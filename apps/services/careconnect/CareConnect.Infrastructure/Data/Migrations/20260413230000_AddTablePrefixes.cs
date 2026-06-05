using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

  #nullable disable

  namespace CareConnect.Infrastructure.Data.Migrations;

  [DbContext(typeof(CareConnectDbContext))]
  [Migration("20260413230000_AddTablePrefixes")]
  public partial class AddTablePrefixes : Migration
  {
      protected override void Up(MigrationBuilder migrationBuilder)
      {
              migrationBuilder.RenameTable(name: "ActivationRequests", newName: "cc_ActivationRequests");
            migrationBuilder.RenameTable(name: "AppointmentAttachments", newName: "cc_AppointmentAttachments");
            migrationBuilder.RenameTable(name: "Appointments", newName: "cc_Appointments");
            migrationBuilder.RenameTable(name: "AppointmentNotes", newName: "cc_AppointmentNotes");
            migrationBuilder.RenameTable(name: "AppointmentSlots", newName: "cc_AppointmentSlots");
            migrationBuilder.RenameTable(name: "AppointmentStatusHistories", newName: "cc_AppointmentStatusHistories");
            migrationBuilder.RenameTable(name: "BlockedProviderAccessLogs", newName: "cc_BlockedProviderAccessLogs");
            migrationBuilder.RenameTable(name: "CareConnectNotifications", newName: "cc_CareConnectNotifications");
            migrationBuilder.RenameTable(name: "Categories", newName: "cc_Categories");
            migrationBuilder.RenameTable(name: "Facilities", newName: "cc_Facilities");
            migrationBuilder.RenameTable(name: "Parties", newName: "cc_Parties");
            migrationBuilder.RenameTable(name: "PartyContacts", newName: "cc_PartyContacts");
            migrationBuilder.RenameTable(name: "ProviderAvailabilityExceptions", newName: "cc_ProviderAvailabilityExceptions");
            migrationBuilder.RenameTable(name: "ProviderAvailabilityTemplates", newName: "cc_ProviderAvailabilityTemplates");
            migrationBuilder.RenameTable(name: "ProviderCategories", newName: "cc_ProviderCategories");
            migrationBuilder.RenameTable(name: "Providers", newName: "cc_Providers");
            migrationBuilder.RenameTable(name: "ProviderFacilities", newName: "cc_ProviderFacilities");
            migrationBuilder.RenameTable(name: "ProviderServiceOfferings", newName: "cc_ProviderServiceOfferings");
            migrationBuilder.RenameTable(name: "ReferralAttachments", newName: "cc_ReferralAttachments");
            migrationBuilder.RenameTable(name: "Referrals", newName: "cc_Referrals");
            migrationBuilder.RenameTable(name: "ReferralNotes", newName: "cc_ReferralNotes");
            migrationBuilder.RenameTable(name: "ReferralStatusHistories", newName: "cc_ReferralStatusHistories");
            migrationBuilder.RenameTable(name: "ServiceOfferings", newName: "cc_ServiceOfferings");
      }

      protected override void Down(MigrationBuilder migrationBuilder)
      {
              migrationBuilder.RenameTable(name: "cc_ActivationRequests", newName: "ActivationRequests");
            migrationBuilder.RenameTable(name: "cc_AppointmentAttachments", newName: "AppointmentAttachments");
            migrationBuilder.RenameTable(name: "cc_Appointments", newName: "Appointments");
            migrationBuilder.RenameTable(name: "cc_AppointmentNotes", newName: "AppointmentNotes");
            migrationBuilder.RenameTable(name: "cc_AppointmentSlots", newName: "AppointmentSlots");
            migrationBuilder.RenameTable(name: "cc_AppointmentStatusHistories", newName: "AppointmentStatusHistories");
            migrationBuilder.RenameTable(name: "cc_BlockedProviderAccessLogs", newName: "BlockedProviderAccessLogs");
            migrationBuilder.RenameTable(name: "cc_CareConnectNotifications", newName: "CareConnectNotifications");
            migrationBuilder.RenameTable(name: "cc_Categories", newName: "Categories");
            migrationBuilder.RenameTable(name: "cc_Facilities", newName: "Facilities");
            migrationBuilder.RenameTable(name: "cc_Parties", newName: "Parties");
            migrationBuilder.RenameTable(name: "cc_PartyContacts", newName: "PartyContacts");
            migrationBuilder.RenameTable(name: "cc_ProviderAvailabilityExceptions", newName: "ProviderAvailabilityExceptions");
            migrationBuilder.RenameTable(name: "cc_ProviderAvailabilityTemplates", newName: "ProviderAvailabilityTemplates");
            migrationBuilder.RenameTable(name: "cc_ProviderCategories", newName: "ProviderCategories");
            migrationBuilder.RenameTable(name: "cc_Providers", newName: "Providers");
            migrationBuilder.RenameTable(name: "cc_ProviderFacilities", newName: "ProviderFacilities");
            migrationBuilder.RenameTable(name: "cc_ProviderServiceOfferings", newName: "ProviderServiceOfferings");
            migrationBuilder.RenameTable(name: "cc_ReferralAttachments", newName: "ReferralAttachments");
            migrationBuilder.RenameTable(name: "cc_Referrals", newName: "Referrals");
            migrationBuilder.RenameTable(name: "cc_ReferralNotes", newName: "ReferralNotes");
            migrationBuilder.RenameTable(name: "cc_ReferralStatusHistories", newName: "ReferralStatusHistories");
            migrationBuilder.RenameTable(name: "cc_ServiceOfferings", newName: "ServiceOfferings");
      }
  }
  