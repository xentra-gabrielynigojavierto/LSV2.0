-- ============================================================
-- LegalSynq Platform — fund_db
-- Generated: 2026-03-28 20:36:27 UTC
-- Host: legalsynqplatform.cpq48wc2krn5.us-east-2.rds.amazonaws.com
-- ============================================================
--
-- Tables (2):
--   Applications, __EFMigrationsHistory
--
-- Applied migrations (2):
--   20260328041847_InitialFundSchema
--   20260328043739_AddUpdatedByUserId
--
-- ============================================================

CREATE DATABASE IF NOT EXISTS `fund_db` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `fund_db`;

-- ------------------------------------------------------------
-- Table: Applications
-- ------------------------------------------------------------
CREATE TABLE `Applications` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ApplicationNumber` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ApplicantFirstName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ApplicantLastName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Email` varchar(320) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Phone` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Status` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Applications_TenantId_ApplicationNumber` (`TenantId`,`ApplicationNumber`),
  KEY `IX_Applications_TenantId_CreatedAtUtc` (`TenantId`,`CreatedAtUtc`),
  KEY `IX_Applications_TenantId_Status` (`TenantId`,`Status`)
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
--   20260328041847_InitialFundSchema  (EF 8.0.8)
--   20260328043739_AddUpdatedByUserId  (EF 8.0.8)

