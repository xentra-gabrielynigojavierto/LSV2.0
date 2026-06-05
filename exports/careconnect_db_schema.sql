-- ============================================================
-- LegalSynq Platform — careconnect_db
-- Generated: 2026-03-28 20:36:28 UTC
-- Host: legalsynqplatform.cpq48wc2krn5.us-east-2.rds.amazonaws.com
-- ============================================================
--
-- Tables (20):
--   AppointmentAttachments, AppointmentNotes, AppointmentSlots,
--   AppointmentStatusHistories, Appointments, CareConnectNotifications,
--   Categories, Facilities, ProviderAvailabilityExceptions,
--   ProviderAvailabilityTemplates, ProviderCategories, ProviderFacilities,
--   ProviderServiceOfferings, Providers, ReferralAttachments, ReferralNotes,
--   ReferralStatusHistories, Referrals, ServiceOfferings,
--   __EFMigrationsHistory
--
-- Applied migrations (9):
--   20260328053757_InitialCareConnectSchema
--   20260328140005_AddReferralStatusHistory
--   20260328150708_AddSchedulingFoundation
--   20260328162450_AddAppointmentSlotsAndAppointments
--   20260328165342_AddAppointmentStatusHistory
--   20260328170924_AddProviderAvailabilityExceptions
--   20260328174425_AddReferralAppointmentNotesAndAttachments
--   20260328180426_AddCareConnectNotifications
--   20260328190001_AddProviderGeoLocation
--
-- ============================================================

CREATE DATABASE IF NOT EXISTS `careconnect_db` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `careconnect_db`;

-- ------------------------------------------------------------
-- Table: AppointmentAttachments
-- ------------------------------------------------------------
CREATE TABLE `AppointmentAttachments` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `AppointmentId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `FileName` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ContentType` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `FileSizeBytes` bigint NOT NULL,
  `ExternalDocumentId` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `ExternalStorageProvider` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `Status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Notes` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_AppointmentAttachments_AppointmentId` (`AppointmentId`),
  KEY `IX_AppointmentAttachments_TenantId_AppointmentId_CreatedAtUtc` (`TenantId`,`AppointmentId`,`CreatedAtUtc`),
  KEY `IX_AppointmentAttachments_TenantId_Status` (`TenantId`,`Status`),
  CONSTRAINT `FK_AppointmentAttachments_Appointments_AppointmentId` FOREIGN KEY (`AppointmentId`) REFERENCES `Appointments` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: AppointmentNotes
-- ------------------------------------------------------------
CREATE TABLE `AppointmentNotes` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `AppointmentId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `NoteType` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Content` varchar(4000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `IsInternal` tinyint(1) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_AppointmentNotes_AppointmentId` (`AppointmentId`),
  KEY `IX_AppointmentNotes_TenantId_AppointmentId_CreatedAtUtc` (`TenantId`,`AppointmentId`,`CreatedAtUtc`),
  KEY `IX_AppointmentNotes_TenantId_NoteType` (`TenantId`,`NoteType`),
  CONSTRAINT `FK_AppointmentNotes_Appointments_AppointmentId` FOREIGN KEY (`AppointmentId`) REFERENCES `Appointments` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: AppointmentSlots
-- ------------------------------------------------------------
CREATE TABLE `AppointmentSlots` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ProviderId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `FacilityId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ServiceOfferingId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `ProviderAvailabilityTemplateId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `StartAtUtc` datetime(6) NOT NULL,
  `EndAtUtc` datetime(6) NOT NULL,
  `Capacity` int NOT NULL,
  `ReservedCount` int NOT NULL,
  `Status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_AppointmentSlots_TenantId_ProviderId_ProviderAvailabilityTem~` (`TenantId`,`ProviderId`,`ProviderAvailabilityTemplateId`,`StartAtUtc`),
  KEY `IX_AppointmentSlots_FacilityId` (`FacilityId`),
  KEY `IX_AppointmentSlots_ProviderAvailabilityTemplateId` (`ProviderAvailabilityTemplateId`),
  KEY `IX_AppointmentSlots_ProviderId` (`ProviderId`),
  KEY `IX_AppointmentSlots_ServiceOfferingId` (`ServiceOfferingId`),
  KEY `IX_AppointmentSlots_TenantId_FacilityId_StartAtUtc` (`TenantId`,`FacilityId`,`StartAtUtc`),
  KEY `IX_AppointmentSlots_TenantId_ProviderId_StartAtUtc` (`TenantId`,`ProviderId`,`StartAtUtc`),
  KEY `IX_AppointmentSlots_TenantId_ServiceOfferingId_StartAtUtc` (`TenantId`,`ServiceOfferingId`,`StartAtUtc`),
  KEY `IX_AppointmentSlots_TenantId_Status` (`TenantId`,`Status`),
  CONSTRAINT `FK_AppointmentSlots_Facilities_FacilityId` FOREIGN KEY (`FacilityId`) REFERENCES `Facilities` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_AppointmentSlots_ProviderAvailabilityTemplates_ProviderAvail~` FOREIGN KEY (`ProviderAvailabilityTemplateId`) REFERENCES `ProviderAvailabilityTemplates` (`Id`) ON DELETE SET NULL,
  CONSTRAINT `FK_AppointmentSlots_Providers_ProviderId` FOREIGN KEY (`ProviderId`) REFERENCES `Providers` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_AppointmentSlots_ServiceOfferings_ServiceOfferingId` FOREIGN KEY (`ServiceOfferingId`) REFERENCES `ServiceOfferings` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: AppointmentStatusHistories
-- ------------------------------------------------------------
CREATE TABLE `AppointmentStatusHistories` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `AppointmentId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `OldStatus` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `NewStatus` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ChangedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `ChangedAtUtc` datetime(6) NOT NULL,
  `Notes` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_AppointmentStatusHistories_AppointmentId` (`AppointmentId`),
  KEY `IX_AppointmentStatusHistories_TenantId_AppointmentId` (`TenantId`,`AppointmentId`),
  KEY `IX_AppointmentStatusHistories_TenantId_ChangedAtUtc` (`TenantId`,`ChangedAtUtc`),
  CONSTRAINT `FK_AppointmentStatusHistories_Appointments_AppointmentId` FOREIGN KEY (`AppointmentId`) REFERENCES `Appointments` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: Appointments
-- ------------------------------------------------------------
CREATE TABLE `Appointments` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ReferralId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ProviderId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `FacilityId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ServiceOfferingId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `AppointmentSlotId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `ScheduledStartAtUtc` datetime(6) NOT NULL,
  `ScheduledEndAtUtc` datetime(6) NOT NULL,
  `Status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Notes` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_Appointments_AppointmentSlotId` (`AppointmentSlotId`),
  KEY `IX_Appointments_FacilityId` (`FacilityId`),
  KEY `IX_Appointments_ProviderId` (`ProviderId`),
  KEY `IX_Appointments_ReferralId` (`ReferralId`),
  KEY `IX_Appointments_ServiceOfferingId` (`ServiceOfferingId`),
  KEY `IX_Appointments_TenantId_AppointmentSlotId` (`TenantId`,`AppointmentSlotId`),
  KEY `IX_Appointments_TenantId_ProviderId_ScheduledStartAtUtc` (`TenantId`,`ProviderId`,`ScheduledStartAtUtc`),
  KEY `IX_Appointments_TenantId_ReferralId` (`TenantId`,`ReferralId`),
  KEY `IX_Appointments_TenantId_Status` (`TenantId`,`Status`),
  CONSTRAINT `FK_Appointments_AppointmentSlots_AppointmentSlotId` FOREIGN KEY (`AppointmentSlotId`) REFERENCES `AppointmentSlots` (`Id`) ON DELETE SET NULL,
  CONSTRAINT `FK_Appointments_Facilities_FacilityId` FOREIGN KEY (`FacilityId`) REFERENCES `Facilities` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_Appointments_Providers_ProviderId` FOREIGN KEY (`ProviderId`) REFERENCES `Providers` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_Appointments_Referrals_ReferralId` FOREIGN KEY (`ReferralId`) REFERENCES `Referrals` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_Appointments_ServiceOfferings_ServiceOfferingId` FOREIGN KEY (`ServiceOfferingId`) REFERENCES `ServiceOfferings` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: CareConnectNotifications
-- ------------------------------------------------------------
CREATE TABLE `CareConnectNotifications` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `NotificationType` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `RelatedEntityType` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `RelatedEntityId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `RecipientType` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `RecipientAddress` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `Subject` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `Message` varchar(4000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `Status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ScheduledForUtc` datetime(6) DEFAULT NULL,
  `SentAtUtc` datetime(6) DEFAULT NULL,
  `FailedAtUtc` datetime(6) DEFAULT NULL,
  `FailureReason` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_CareConnectNotifications_TenantId_NotificationType` (`TenantId`,`NotificationType`),
  KEY `IX_CareConnectNotifications_TenantId_RelatedEntityType_RelatedE~` (`TenantId`,`RelatedEntityType`,`RelatedEntityId`),
  KEY `IX_CareConnectNotifications_TenantId_Status_ScheduledForUtc` (`TenantId`,`Status`,`ScheduledForUtc`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: Categories
-- ------------------------------------------------------------
CREATE TABLE `Categories` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Code` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Description` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Categories_Code` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: Facilities
-- ------------------------------------------------------------
CREATE TABLE `Facilities` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `AddressLine1` varchar(300) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `City` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `State` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `PostalCode` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Phone` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_Facilities_TenantId_Name` (`TenantId`,`Name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: ProviderAvailabilityExceptions
-- ------------------------------------------------------------
CREATE TABLE `ProviderAvailabilityExceptions` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ProviderId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `FacilityId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `StartAtUtc` datetime(6) NOT NULL,
  `EndAtUtc` datetime(6) NOT NULL,
  `ExceptionType` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Reason` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ProviderAvailabilityExceptions_FacilityId` (`FacilityId`),
  KEY `IX_ProviderAvailabilityExceptions_ProviderId` (`ProviderId`),
  KEY `IX_ProviderAvailabilityExceptions_TenantId_FacilityId_StartAtUtc` (`TenantId`,`FacilityId`,`StartAtUtc`),
  KEY `IX_ProviderAvailabilityExceptions_TenantId_ProviderId_StartAtUtc` (`TenantId`,`ProviderId`,`StartAtUtc`),
  KEY `IX_ProviderAvailabilityExceptions_TenantId_StartAtUtc_EndAtUtc` (`TenantId`,`StartAtUtc`,`EndAtUtc`),
  CONSTRAINT `FK_ProviderAvailabilityExceptions_Facilities_FacilityId` FOREIGN KEY (`FacilityId`) REFERENCES `Facilities` (`Id`) ON DELETE SET NULL,
  CONSTRAINT `FK_ProviderAvailabilityExceptions_Providers_ProviderId` FOREIGN KEY (`ProviderId`) REFERENCES `Providers` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: ProviderAvailabilityTemplates
-- ------------------------------------------------------------
CREATE TABLE `ProviderAvailabilityTemplates` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ProviderId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `FacilityId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ServiceOfferingId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `DayOfWeek` int NOT NULL,
  `StartTimeLocal` time NOT NULL,
  `EndTimeLocal` time NOT NULL,
  `SlotDurationMinutes` int NOT NULL,
  `Capacity` int NOT NULL,
  `EffectiveFrom` datetime(6) DEFAULT NULL,
  `EffectiveTo` datetime(6) DEFAULT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ProviderAvailabilityTemplates_FacilityId` (`FacilityId`),
  KEY `IX_ProviderAvailabilityTemplates_ProviderId` (`ProviderId`),
  KEY `IX_ProviderAvailabilityTemplates_ServiceOfferingId` (`ServiceOfferingId`),
  KEY `IX_ProviderAvailabilityTemplates_TenantId_FacilityId` (`TenantId`,`FacilityId`),
  KEY `IX_ProviderAvailabilityTemplates_TenantId_ProviderId_DayOfWeek` (`TenantId`,`ProviderId`,`DayOfWeek`),
  KEY `IX_ProviderAvailabilityTemplates_TenantId_ServiceOfferingId` (`TenantId`,`ServiceOfferingId`),
  CONSTRAINT `FK_ProviderAvailabilityTemplates_Facilities_FacilityId` FOREIGN KEY (`FacilityId`) REFERENCES `Facilities` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_ProviderAvailabilityTemplates_Providers_ProviderId` FOREIGN KEY (`ProviderId`) REFERENCES `Providers` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_ProviderAvailabilityTemplates_ServiceOfferings_ServiceOfferi~` FOREIGN KEY (`ServiceOfferingId`) REFERENCES `ServiceOfferings` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: ProviderCategories
-- ------------------------------------------------------------
CREATE TABLE `ProviderCategories` (
  `ProviderId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `CategoryId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  PRIMARY KEY (`ProviderId`,`CategoryId`),
  KEY `IX_ProviderCategories_CategoryId` (`CategoryId`),
  CONSTRAINT `FK_ProviderCategories_Categories_CategoryId` FOREIGN KEY (`CategoryId`) REFERENCES `Categories` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_ProviderCategories_Providers_ProviderId` FOREIGN KEY (`ProviderId`) REFERENCES `Providers` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: ProviderFacilities
-- ------------------------------------------------------------
CREATE TABLE `ProviderFacilities` (
  `ProviderId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `FacilityId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `IsPrimary` tinyint(1) NOT NULL,
  PRIMARY KEY (`ProviderId`,`FacilityId`),
  KEY `IX_ProviderFacilities_FacilityId` (`FacilityId`),
  CONSTRAINT `FK_ProviderFacilities_Facilities_FacilityId` FOREIGN KEY (`FacilityId`) REFERENCES `Facilities` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_ProviderFacilities_Providers_ProviderId` FOREIGN KEY (`ProviderId`) REFERENCES `Providers` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: ProviderServiceOfferings
-- ------------------------------------------------------------
CREATE TABLE `ProviderServiceOfferings` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ProviderId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ServiceOfferingId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `FacilityId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `IsActive` tinyint(1) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ProviderServiceOfferings_ProviderId_ServiceOfferingId_Facili~` (`ProviderId`,`ServiceOfferingId`,`FacilityId`),
  KEY `IX_ProviderServiceOfferings_FacilityId` (`FacilityId`),
  KEY `IX_ProviderServiceOfferings_ServiceOfferingId` (`ServiceOfferingId`),
  CONSTRAINT `FK_ProviderServiceOfferings_Facilities_FacilityId` FOREIGN KEY (`FacilityId`) REFERENCES `Facilities` (`Id`) ON DELETE SET NULL,
  CONSTRAINT `FK_ProviderServiceOfferings_Providers_ProviderId` FOREIGN KEY (`ProviderId`) REFERENCES `Providers` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_ProviderServiceOfferings_ServiceOfferings_ServiceOfferingId` FOREIGN KEY (`ServiceOfferingId`) REFERENCES `ServiceOfferings` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: Providers
-- ------------------------------------------------------------
CREATE TABLE `Providers` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `OrganizationName` varchar(300) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `Email` varchar(320) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Phone` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `AddressLine1` varchar(300) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `City` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `State` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `PostalCode` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `AcceptingReferrals` tinyint(1) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `Latitude` decimal(10,7) DEFAULT NULL,
  `Longitude` decimal(10,7) DEFAULT NULL,
  `GeoPointSource` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `GeoUpdatedAtUtc` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Providers_TenantId_Email` (`TenantId`,`Email`),
  KEY `IX_Providers_TenantId_Name` (`TenantId`,`Name`),
  KEY `IX_Providers_TenantId_Latitude_Longitude` (`TenantId`,`Latitude`,`Longitude`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: ReferralAttachments
-- ------------------------------------------------------------
CREATE TABLE `ReferralAttachments` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ReferralId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `FileName` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ContentType` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `FileSizeBytes` bigint NOT NULL,
  `ExternalDocumentId` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `ExternalStorageProvider` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `Status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Notes` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ReferralAttachments_ReferralId` (`ReferralId`),
  KEY `IX_ReferralAttachments_TenantId_ReferralId_CreatedAtUtc` (`TenantId`,`ReferralId`,`CreatedAtUtc`),
  KEY `IX_ReferralAttachments_TenantId_Status` (`TenantId`,`Status`),
  CONSTRAINT `FK_ReferralAttachments_Referrals_ReferralId` FOREIGN KEY (`ReferralId`) REFERENCES `Referrals` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: ReferralNotes
-- ------------------------------------------------------------
CREATE TABLE `ReferralNotes` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ReferralId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `NoteType` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Content` varchar(4000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `IsInternal` tinyint(1) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ReferralNotes_ReferralId` (`ReferralId`),
  KEY `IX_ReferralNotes_TenantId_NoteType` (`TenantId`,`NoteType`),
  KEY `IX_ReferralNotes_TenantId_ReferralId_CreatedAtUtc` (`TenantId`,`ReferralId`,`CreatedAtUtc`),
  CONSTRAINT `FK_ReferralNotes_Referrals_ReferralId` FOREIGN KEY (`ReferralId`) REFERENCES `Referrals` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: ReferralStatusHistories
-- ------------------------------------------------------------
CREATE TABLE `ReferralStatusHistories` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ReferralId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `OldStatus` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `NewStatus` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ChangedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `ChangedAtUtc` datetime(6) NOT NULL,
  `Notes` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ReferralStatusHistories_ReferralId` (`ReferralId`),
  KEY `IX_ReferralStatusHistories_TenantId_ChangedAtUtc` (`TenantId`,`ChangedAtUtc`),
  KEY `IX_ReferralStatusHistories_TenantId_ReferralId` (`TenantId`,`ReferralId`),
  CONSTRAINT `FK_ReferralStatusHistories_Referrals_ReferralId` FOREIGN KEY (`ReferralId`) REFERENCES `Referrals` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: Referrals
-- ------------------------------------------------------------
CREATE TABLE `Referrals` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ProviderId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ClientFirstName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ClientLastName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ClientDob` datetime(6) DEFAULT NULL,
  `ClientPhone` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ClientEmail` varchar(320) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `CaseNumber` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `RequestedService` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Urgency` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Notes` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_Referrals_ProviderId` (`ProviderId`),
  KEY `IX_Referrals_TenantId_CreatedAtUtc` (`TenantId`,`CreatedAtUtc`),
  KEY `IX_Referrals_TenantId_ProviderId` (`TenantId`,`ProviderId`),
  KEY `IX_Referrals_TenantId_Status` (`TenantId`,`Status`),
  CONSTRAINT `FK_Referrals_Providers_ProviderId` FOREIGN KEY (`ProviderId`) REFERENCES `Providers` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: ServiceOfferings
-- ------------------------------------------------------------
CREATE TABLE `ServiceOfferings` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Code` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Description` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `DurationMinutes` int NOT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ServiceOfferings_TenantId_Code` (`TenantId`,`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ------------------------------------------------------------
-- Table: __EFMigrationsHistory
-- ------------------------------------------------------------
CREATE TABLE `__EFMigrationsHistory` (
  `MigrationId` varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ProductVersion` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ============================================================
-- Applied EF Core Migrations
-- ============================================================
--   20260328053757_InitialCareConnectSchema  (EF 8.0.8)
--   20260328140005_AddReferralStatusHistory  (EF 8.0.8)
--   20260328150708_AddSchedulingFoundation  (EF 8.0.8)
--   20260328162450_AddAppointmentSlotsAndAppointments  (EF 8.0.8)
--   20260328165342_AddAppointmentStatusHistory  (EF 8.0.8)
--   20260328170924_AddProviderAvailabilityExceptions  (EF 8.0.8)
--   20260328174425_AddReferralAppointmentNotesAndAttachments  (EF 8.0.8)
--   20260328180426_AddCareConnectNotifications  (EF 8.0.8)
--   20260328190001_AddProviderGeoLocation  (EF 8.0.8)

