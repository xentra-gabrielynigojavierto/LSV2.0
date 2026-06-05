using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

  #nullable disable

  namespace PlatformAuditEventService.Data.Migrations;

  [DbContext(typeof(AuditEventDbContext))]
  [Migration("20260413230000_AddTablePrefixes")]
  public partial class AddTablePrefixes : Migration
  {
      protected override void Up(MigrationBuilder migrationBuilder)
      {
          migrationBuilder.RenameTable(name: "AuditEventRecords", newName: "aud_AuditEventRecords");
          migrationBuilder.RenameTable(name: "AuditExportJobs", newName: "aud_AuditExportJobs");
          migrationBuilder.RenameTable(name: "IngestSourceRegistrations", newName: "aud_IngestSourceRegistrations");
          migrationBuilder.RenameTable(name: "IntegrityCheckpoints", newName: "aud_IntegrityCheckpoints");
          migrationBuilder.RenameTable(name: "LegalHolds", newName: "aud_LegalHolds");
          migrationBuilder.RenameTable(name: "OutboxMessages", newName: "aud_OutboxMessages");

          migrationBuilder.Sql(@"
              SET @old_exists = (SELECT COUNT(*) FROM information_schema.tables
                                 WHERE table_schema = DATABASE() AND table_name = 'AuditEvents');
              SET @new_exists = (SELECT COUNT(*) FROM information_schema.tables
                                 WHERE table_schema = DATABASE() AND table_name = 'aud_AuditEvents');

              SET @stmt = IF(@old_exists > 0 AND @new_exists = 0,
                  'RENAME TABLE `AuditEvents` TO `aud_AuditEvents`',
                  IF(@old_exists = 0 AND @new_exists = 0,
                      CONCAT(
                          'CREATE TABLE `aud_AuditEvents` (',
                          '  `Id` char(36) COLLATE ascii_general_ci NOT NULL,',
                          '  `Source` varchar(200) CHARACTER SET utf8mb4 NOT NULL,',
                          '  `EventType` varchar(200) CHARACTER SET utf8mb4 NOT NULL,',
                          '  `Category` varchar(100) CHARACTER SET utf8mb4 NOT NULL,',
                          '  `Severity` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''INFO'',',
                          '  `Description` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,',
                          '  `Outcome` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''SUCCESS'',',
                          '  `TenantId` varchar(100) CHARACTER SET utf8mb4 DEFAULT NULL,',
                          '  `ActorId` varchar(200) CHARACTER SET utf8mb4 DEFAULT NULL,',
                          '  `ActorLabel` varchar(300) CHARACTER SET utf8mb4 DEFAULT NULL,',
                          '  `TargetType` varchar(200) CHARACTER SET utf8mb4 DEFAULT NULL,',
                          '  `TargetId` varchar(200) CHARACTER SET utf8mb4 DEFAULT NULL,',
                          '  `IpAddress` varchar(45) CHARACTER SET utf8mb4 DEFAULT NULL,',
                          '  `UserAgent` varchar(500) CHARACTER SET utf8mb4 DEFAULT NULL,',
                          '  `CorrelationId` varchar(200) CHARACTER SET utf8mb4 DEFAULT NULL,',
                          '  `Metadata` text CHARACTER SET utf8mb4 DEFAULT NULL,',
                          '  `IntegrityHash` varchar(64) CHARACTER SET utf8mb4 DEFAULT NULL,',
                          '  `OccurredAtUtc` datetime(6) NOT NULL,',
                          '  `IngestedAtUtc` datetime(6) NOT NULL,',
                          '  PRIMARY KEY (`Id`),',
                          '  KEY `IX_AuditEvents_ActorId` (`ActorId`),',
                          '  KEY `IX_AuditEvents_CorrelationId` (`CorrelationId`),',
                          '  KEY `IX_AuditEvents_IngestedAt` (`IngestedAtUtc`),',
                          '  KEY `IX_AuditEvents_Source_EventType` (`Source`,`EventType`),',
                          '  KEY `IX_AuditEvents_TargetType_TargetId` (`TargetType`,`TargetId`),',
                          '  KEY `IX_AuditEvents_TenantId_OccurredAt` (`TenantId`,`OccurredAtUtc`),',
                          '  KEY `IX_AuditEvents_Category_Severity_Outcome` (`Category`,`Severity`,`Outcome`)',
                          ') ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci'
                      ),
                      'SELECT 1'
                  )
              );

              PREPARE dynamic_stmt FROM @stmt;
              EXECUTE dynamic_stmt;
              DEALLOCATE PREPARE dynamic_stmt;
          ");
      }

      protected override void Down(MigrationBuilder migrationBuilder)
      {
          migrationBuilder.RenameTable(name: "aud_AuditEventRecords", newName: "AuditEventRecords");
          migrationBuilder.RenameTable(name: "aud_AuditExportJobs", newName: "AuditExportJobs");
          migrationBuilder.RenameTable(name: "aud_IngestSourceRegistrations", newName: "IngestSourceRegistrations");
          migrationBuilder.RenameTable(name: "aud_IntegrityCheckpoints", newName: "IntegrityCheckpoints");
          migrationBuilder.RenameTable(name: "aud_LegalHolds", newName: "LegalHolds");
          migrationBuilder.RenameTable(name: "aud_AuditEvents", newName: "AuditEvents");
          migrationBuilder.RenameTable(name: "aud_OutboxMessages", newName: "OutboxMessages");
      }
  }
