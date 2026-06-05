CREATE TABLE IF NOT EXISTS `rpt_TenantReportViews` (
    `Id` char(36) NOT NULL,
    `TenantId` varchar(100) NOT NULL,
    `ReportTemplateId` char(36) NOT NULL,
    `BaseTemplateVersionNumber` int NOT NULL,
    `Name` varchar(200) NOT NULL,
    `Description` varchar(2000) NULL,
    `IsDefault` tinyint(1) NOT NULL DEFAULT 0,
    `IsActive` tinyint(1) NOT NULL DEFAULT 1,
    `LayoutConfigJson` longtext NULL,
    `ColumnConfigJson` longtext NULL,
    `FilterConfigJson` longtext NULL,
    `FormulaConfigJson` longtext NULL,
    `FormattingConfigJson` longtext NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `CreatedByUserId` varchar(100) NOT NULL,
    `UpdatedAtUtc` datetime(6) NOT NULL,
    `UpdatedByUserId` varchar(100) NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_rpt_TenantReportViews_TenantId` (`TenantId`),
    INDEX `IX_rpt_TenantReportViews_ReportTemplateId` (`ReportTemplateId`),
    INDEX `IX_rpt_TenantReportViews_TenantId_ReportTemplateId` (`TenantId`, `ReportTemplateId`),
    INDEX `IX_rpt_TenantReportViews_TenantId_ReportTemplateId_IsActive` (`TenantId`, `ReportTemplateId`, `IsActive`),
    CONSTRAINT `FK_rpt_TenantReportViews_rpt_ReportTemplates_ReportTemplateId`
        FOREIGN KEY (`ReportTemplateId`) REFERENCES `rpt_ReportTemplates` (`Id`)
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

ALTER TABLE `rpt_ReportSchedules`
    ADD COLUMN IF NOT EXISTS `ViewId` char(36) NULL AFTER `UseOverride`;
