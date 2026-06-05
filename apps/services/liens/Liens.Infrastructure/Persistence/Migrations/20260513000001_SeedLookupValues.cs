using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    public partial class SeedLookupValues : Migration
    {
        private const string SystemUserId = "00000000-0000-0000-0000-000000000001";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── States ────────────────────────────────────────────────────────────
            // Source: legacy _00003CreateStates / _00004InsertStates
            // Renamed: STATES → liens_LookupValues (Category = 'State')

            migrationBuilder.Sql($"""
                INSERT INTO `liens_LookupValues`
                    (`Id`, `TenantId`, `Category`, `Code`, `Name`, `Description`,
                     `SortOrder`, `IsActive`, `IsSystem`,
                     `CreatedByUserId`, `UpdatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`)
                SELECT UUID(), NULL, 'State', s.Code, s.Name, NULL, s.Sort, 1, 1,
                       '{SystemUserId}', NULL, NOW(), NOW()
                FROM (
                    SELECT 'AL' AS Code, 'Alabama'              AS Name, 1  AS Sort UNION ALL
                    SELECT 'AK',         'Alaska',                        2  UNION ALL
                    SELECT 'AZ',         'Arizona',                       3  UNION ALL
                    SELECT 'AR',         'Arkansas',                      4  UNION ALL
                    SELECT 'CA',         'California',                    5  UNION ALL
                    SELECT 'CO',         'Colorado',                      6  UNION ALL
                    SELECT 'CT',         'Connecticut',                   7  UNION ALL
                    SELECT 'DE',         'Delaware',                      8  UNION ALL
                    SELECT 'DC',         'District Of Columbia',          9  UNION ALL
                    SELECT 'FL',         'Florida',                       10 UNION ALL
                    SELECT 'GA',         'Georgia',                       11 UNION ALL
                    SELECT 'HI',         'Hawaii',                        12 UNION ALL
                    SELECT 'ID',         'Idaho',                         13 UNION ALL
                    SELECT 'IL',         'Illinois',                      14 UNION ALL
                    SELECT 'IN',         'Indiana',                       15 UNION ALL
                    SELECT 'IA',         'Iowa',                          16 UNION ALL
                    SELECT 'KS',         'Kansas',                        17 UNION ALL
                    SELECT 'KY',         'Kentucky',                      18 UNION ALL
                    SELECT 'LA',         'Louisiana',                     19 UNION ALL
                    SELECT 'ME',         'Maine',                         20 UNION ALL
                    SELECT 'MD',         'Maryland',                      21 UNION ALL
                    SELECT 'MA',         'Massachusetts',                 22 UNION ALL
                    SELECT 'MI',         'Michigan',                      23 UNION ALL
                    SELECT 'MN',         'Minnesota',                     24 UNION ALL
                    SELECT 'MS',         'Mississippi',                   25 UNION ALL
                    SELECT 'MO',         'Missouri',                      26 UNION ALL
                    SELECT 'MT',         'Montana',                       27 UNION ALL
                    SELECT 'NE',         'Nebraska',                      28 UNION ALL
                    SELECT 'NV',         'Nevada',                        29 UNION ALL
                    SELECT 'NH',         'New Hampshire',                 30 UNION ALL
                    SELECT 'NJ',         'New Jersey',                    31 UNION ALL
                    SELECT 'NM',         'New Mexico',                    32 UNION ALL
                    SELECT 'NY',         'New York',                      33 UNION ALL
                    SELECT 'NC',         'North Carolina',                34 UNION ALL
                    SELECT 'ND',         'North Dakota',                  35 UNION ALL
                    SELECT 'OH',         'Ohio',                          36 UNION ALL
                    SELECT 'OK',         'Oklahoma',                      37 UNION ALL
                    SELECT 'OR',         'Oregon',                        38 UNION ALL
                    SELECT 'PA',         'Pennsylvania',                  39 UNION ALL
                    SELECT 'RI',         'Rhode Island',                  40 UNION ALL
                    SELECT 'SC',         'South Carolina',                41 UNION ALL
                    SELECT 'SD',         'South Dakota',                  42 UNION ALL
                    SELECT 'TN',         'Tennessee',                     43 UNION ALL
                    SELECT 'TX',         'Texas',                         44 UNION ALL
                    SELECT 'UT',         'Utah',                          45 UNION ALL
                    SELECT 'VT',         'Vermont',                       46 UNION ALL
                    SELECT 'VA',         'Virginia',                      47 UNION ALL
                    SELECT 'WA',         'Washington',                    48 UNION ALL
                    SELECT 'WV',         'West Virginia',                 49 UNION ALL
                    SELECT 'WI',         'Wisconsin',                     50 UNION ALL
                    SELECT 'WY',         'Wyoming',                       51
                ) AS s
                WHERE NOT EXISTS (
                    SELECT 1 FROM `liens_LookupValues`
                    WHERE `Category` = 'State' AND `Code` = s.Code AND `TenantId` IS NULL
                );
                """);

            // ── ContactType ───────────────────────────────────────────────────────
            // Source: legacy _00006CreateContactType / _00007InsertContactType (LF/MP/FC)
            // Extended with v2.0 ContactType enum values.
            // Renamed: SL_CONTACT_TYPE → liens_LookupValues (Category = 'ContactType')

            migrationBuilder.Sql($"""
                INSERT INTO `liens_LookupValues`
                    (`Id`, `TenantId`, `Category`, `Code`, `Name`, `Description`,
                     `SortOrder`, `IsActive`, `IsSystem`,
                     `CreatedByUserId`, `UpdatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`)
                SELECT UUID(), NULL, 'ContactType', s.Code, s.Name, s.Description, s.Sort, 1, 1,
                       '{SystemUserId}', NULL, NOW(), NOW()
                FROM (
                    SELECT 'LawFirm'      AS Code, 'Law Firm'       AS Name, 'Law firm or attorney contact'        AS Description, 1 AS Sort UNION ALL
                    SELECT 'Provider',           'Medical Provider',          'Medical service provider or facility',              2 UNION ALL
                    SELECT 'LienHolder',          'Lien Holder',              'Entity holding a lien on a case',                   3 UNION ALL
                    SELECT 'CaseManager',         'Case Manager',             'Internal case management personnel',                4 UNION ALL
                    SELECT 'InternalUser',        'Internal User',            'Internal platform user',                            5
                ) AS s
                WHERE NOT EXISTS (
                    SELECT 1 FROM `liens_LookupValues`
                    WHERE `Category` = 'ContactType' AND `Code` = s.Code AND `TenantId` IS NULL
                );
                """);

            // ── AccidentType ──────────────────────────────────────────────────────
            // Source: legacy _00008CreateAccidentType / _00038InsertAccidentType
            // Renamed: SL_ACCIDENT_TYPE → liens_LookupValues (Category = 'AccidentType')

            migrationBuilder.Sql($"""
                INSERT INTO `liens_LookupValues`
                    (`Id`, `TenantId`, `Category`, `Code`, `Name`, `Description`,
                     `SortOrder`, `IsActive`, `IsSystem`,
                     `CreatedByUserId`, `UpdatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`)
                SELECT UUID(), NULL, 'AccidentType', s.Code, s.Name, NULL, s.Sort, 1, 1,
                       '{SystemUserId}', NULL, NOW(), NOW()
                FROM (
                    SELECT 'AirplaneAccident'    AS Code, 'Airplane Accident'    AS Name, 1  AS Sort UNION ALL
                    SELECT 'Asbestos',                    'Asbestos',                      2  UNION ALL
                    SELECT 'Assault',                     'Assault',                       3  UNION ALL
                    SELECT 'AttorneyFunding',             'Attorney Funding',              4  UNION ALL
                    SELECT 'BoatAccident',                'Boat Accident',                 5  UNION ALL
                    SELECT 'BreachOfContract',            'Breach of Contract',            6  UNION ALL
                    SELECT 'CivilRights',                 'Civil Rights',                  7  UNION ALL
                    SELECT 'Commercial',                  'Commercial',                    8  UNION ALL
                    SELECT 'DentalMalpractice',           'Dental Malpractice',            9  UNION ALL
                    SELECT 'Discrimination',              'Discrimination',                10 UNION ALL
                    SELECT 'DogBite',                     'Dog Bite',                      11 UNION ALL
                    SELECT 'EmploymentDisputes',          'Employment Disputes',           12 UNION ALL
                    SELECT 'FELA',                        'FELA',                          13 UNION ALL
                    SELECT 'HipReplacement',              'Hip Replacement',               14 UNION ALL
                    SELECT 'JonesAct',                    'Jones Act',                     15 UNION ALL
                    SELECT 'LaborLaw',                    'Labor Law',                     16 UNION ALL
                    SELECT 'LegalMalpractice',            'Legal Malpractice',             17 UNION ALL
                    SELECT 'MotorVehicleAccident',        'Motor Vehicle Accident',        18 UNION ALL
                    SELECT 'Negligence',                  'Negligence',                    19 UNION ALL
                    SELECT 'NFLConcussion',               'NFL Concussion',                20 UNION ALL
                    SELECT 'Other',                       'Other',                         21 UNION ALL
                    SELECT 'PedestrianKnockdown',         'Pedestrian Knockdown',          22 UNION ALL
                    SELECT 'Pharmaceutical',              'Pharmaceutical',                23 UNION ALL
                    SELECT 'PoliceBrutality',             'Police Brutality',              24 UNION ALL
                    SELECT 'PremisesLiability',           'Premises Liability',            25 UNION ALL
                    SELECT 'ProductsLiability',           'Products Liability',            26 UNION ALL
                    SELECT 'PropertyDamage',              'Property Damage',               27 UNION ALL
                    SELECT 'SettledCase',                 'Settled Case',                  28 UNION ALL
                    SELECT 'SexualAssault',               'Sexual Assault',                29 UNION ALL
                    SELECT 'SexualHarassment',            'Sexual Harassment',             30 UNION ALL
                    SELECT 'SlipAndFall',                 'Slip and Fall',                 31 UNION ALL
                    SELECT 'TransVaginalMesh',            'Trans Vaginal Mesh',            32 UNION ALL
                    SELECT 'UnpaidWages',                 'Unpaid Wages',                  33 UNION ALL
                    SELECT 'WhistleBlower',               'Whistle Blower',                34 UNION ALL
                    SELECT 'WorkersCompensation',         'Workers Compensation',          35 UNION ALL
                    SELECT 'WrongfulArrest',              'Wrongful Arrest',               36 UNION ALL
                    SELECT 'WrongfulDeath',               'Wrongful Death',                37 UNION ALL
                    SELECT 'WrongfulTermination',         'Wrongful Termination',          38
                ) AS s
                WHERE NOT EXISTS (
                    SELECT 1 FROM `liens_LookupValues`
                    WHERE `Category` = 'AccidentType' AND `Code` = s.Code AND `TenantId` IS NULL
                );
                """);

            // ── DocumentCategory ──────────────────────────────────────────────────
            // Source: legacy _00031CreateDocumentType / _00074LienAgreementDocType
            // Renamed: SL_DOCUMENT_TYPE → liens_LookupValues (Category = 'DocumentCategory')

            migrationBuilder.Sql($"""
                INSERT INTO `liens_LookupValues`
                    (`Id`, `TenantId`, `Category`, `Code`, `Name`, `Description`,
                     `SortOrder`, `IsActive`, `IsSystem`,
                     `CreatedByUserId`, `UpdatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`)
                SELECT UUID(), NULL, 'DocumentCategory', s.Code, s.Name, s.Description, s.Sort, 1, 1,
                       '{SystemUserId}', NULL, NOW(), NOW()
                FROM (
                    SELECT 'LienAgreement'     AS Code, 'Lien Agreement'      AS Name, 'Executed lien agreement document'             AS Description, 1 AS Sort UNION ALL
                    SELECT 'MedicalRecord',           'Medical Record',               'Medical records and treatment documentation',                  2 UNION ALL
                    SELECT 'DemandLetter',            'Demand Letter',                'Attorney demand letter to insurance carrier',                  3 UNION ALL
                    SELECT 'SettlementAgreement',     'Settlement Agreement',         'Case settlement agreement',                                   4 UNION ALL
                    SELECT 'BillOfSale',              'Bill of Sale',                 'Lien bill of sale for transfer of interest',                  5 UNION ALL
                    SELECT 'InsuranceDocument',       'Insurance Document',           'Insurance policy or claim documentation',                     6 UNION ALL
                    SELECT 'AttorneyContract',        'Attorney Contract',            'Retainer agreement or attorney contract',                     7 UNION ALL
                    SELECT 'CourtFiling',             'Court Filing',                 'Pleadings, motions, or other court filings',                  8 UNION ALL
                    SELECT 'PayoffStatement',         'Payoff Statement',             'Lien payoff or satisfaction statement',                       9 UNION ALL
                    SELECT 'CheckDocument',           'Check / Payment Record',       'Proof of payment or check documentation',                     10 UNION ALL
                    SELECT 'Other',                   'Other',                        'Miscellaneous document',                                      11
                ) AS s
                WHERE NOT EXISTS (
                    SELECT 1 FROM `liens_LookupValues`
                    WHERE `Category` = 'DocumentCategory' AND `Code` = s.Code AND `TenantId` IS NULL
                );
                """);

            // ── LienStatus ────────────────────────────────────────────────────────
            // Source: legacy _00032CreateLiensStatus / _00033InsertLiensStatus (Open/Close)
            // Extended with v2.0 LienStatus domain enum values.
            // Renamed: SL_LIENS_STATUS → liens_LookupValues (Category = 'LienStatus')

            migrationBuilder.Sql($"""
                INSERT INTO `liens_LookupValues`
                    (`Id`, `TenantId`, `Category`, `Code`, `Name`, `Description`,
                     `SortOrder`, `IsActive`, `IsSystem`,
                     `CreatedByUserId`, `UpdatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`)
                SELECT UUID(), NULL, 'LienStatus', s.Code, s.Name, s.Description, s.Sort, 1, 1,
                       '{SystemUserId}', NULL, NOW(), NOW()
                FROM (
                    SELECT 'Draft'        AS Code, 'Draft'         AS Name, 'Lien is in draft state, not yet submitted'        AS Description, 1 AS Sort UNION ALL
                    SELECT 'Offered',             'Offered',                'Lien has been offered for purchase',                               2 UNION ALL
                    SELECT 'UnderReview',         'Under Review',           'Lien is under review by potential buyer',                         3 UNION ALL
                    SELECT 'Sold',                'Sold',                   'Lien has been sold',                                              4 UNION ALL
                    SELECT 'Active',              'Active',                 'Lien is actively being serviced',                                 5 UNION ALL
                    SELECT 'Settled',             'Settled',                'Lien has been settled',                                           6 UNION ALL
                    SELECT 'Withdrawn',           'Withdrawn',              'Lien was withdrawn by the seller',                                7 UNION ALL
                    SELECT 'Cancelled',           'Cancelled',              'Lien was cancelled',                                              8 UNION ALL
                    SELECT 'Disputed',            'Disputed',               'Lien is in dispute',                                              9
                ) AS s
                WHERE NOT EXISTS (
                    SELECT 1 FROM `liens_LookupValues`
                    WHERE `Category` = 'LienStatus' AND `Code` = s.Code AND `TenantId` IS NULL
                );
                """);

            // ── LienType ──────────────────────────────────────────────────────────
            // Source: v2.0 LienType domain enum
            // Renamed: (inline enum) → liens_LookupValues (Category = 'LienType')

            migrationBuilder.Sql($"""
                INSERT INTO `liens_LookupValues`
                    (`Id`, `TenantId`, `Category`, `Code`, `Name`, `Description`,
                     `SortOrder`, `IsActive`, `IsSystem`,
                     `CreatedByUserId`, `UpdatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`)
                SELECT UUID(), NULL, 'LienType', s.Code, s.Name, s.Description, s.Sort, 1, 1,
                       '{SystemUserId}', NULL, NOW(), NOW()
                FROM (
                    SELECT 'MedicalLien'       AS Code, 'Medical Lien'        AS Name, 'Lien on medical services rendered'                AS Description, 1 AS Sort UNION ALL
                    SELECT 'AttorneyLien',             'Attorney Lien',               'Attorney fee lien',                                               2 UNION ALL
                    SELECT 'SettlementAdvance',        'Settlement Advance',          'Pre-settlement funding advance',                                  3 UNION ALL
                    SELECT 'WorkersCompLien',          'Workers Comp Lien',           'Workers compensation case lien',                                  4 UNION ALL
                    SELECT 'PropertyLien',             'Property Lien',               'Lien against real or personal property',                          5 UNION ALL
                    SELECT 'Other',                    'Other',                       'Other lien type',                                                 6
                ) AS s
                WHERE NOT EXISTS (
                    SELECT 1 FROM `liens_LookupValues`
                    WHERE `Category` = 'LienType' AND `Code` = s.Code AND `TenantId` IS NULL
                );
                """);

            // ── CaseStatus ────────────────────────────────────────────────────────
            // Source: v2.0 CaseStatus domain enum
            // Renamed: (inline enum) → liens_LookupValues (Category = 'CaseStatus')

            migrationBuilder.Sql($"""
                INSERT INTO `liens_LookupValues`
                    (`Id`, `TenantId`, `Category`, `Code`, `Name`, `Description`,
                     `SortOrder`, `IsActive`, `IsSystem`,
                     `CreatedByUserId`, `UpdatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`)
                SELECT UUID(), NULL, 'CaseStatus', s.Code, s.Name, s.Description, s.Sort, 1, 1,
                       '{SystemUserId}', NULL, NOW(), NOW()
                FROM (
                    SELECT 'PreDemand'     AS Code, 'Pre-Demand'       AS Name, 'Case is in pre-demand stage'            AS Description, 1 AS Sort UNION ALL
                    SELECT 'DemandSent',           'Demand Sent',               'Demand letter has been sent',                           2 UNION ALL
                    SELECT 'InNegotiation',        'In Negotiation',            'Case is in active negotiation',                         3 UNION ALL
                    SELECT 'CaseSettled',          'Case Settled',              'Case has reached a settlement',                         4 UNION ALL
                    SELECT 'Closed',               'Closed',                    'Case is closed',                                        5
                ) AS s
                WHERE NOT EXISTS (
                    SELECT 1 FROM `liens_LookupValues`
                    WHERE `Category` = 'CaseStatus' AND `Code` = s.Code AND `TenantId` IS NULL
                );
                """);

            // ── ServicingStatus ───────────────────────────────────────────────────
            // Source: v2.0 ServicingStatus domain enum

            migrationBuilder.Sql($"""
                INSERT INTO `liens_LookupValues`
                    (`Id`, `TenantId`, `Category`, `Code`, `Name`, `Description`,
                     `SortOrder`, `IsActive`, `IsSystem`,
                     `CreatedByUserId`, `UpdatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`)
                SELECT UUID(), NULL, 'ServicingStatus', s.Code, s.Name, s.Description, s.Sort, 1, 1,
                       '{SystemUserId}', NULL, NOW(), NOW()
                FROM (
                    SELECT 'Pending'    AS Code, 'Pending'     AS Name, 'Servicing item is pending action'          AS Description, 1 AS Sort UNION ALL
                    SELECT 'InProgress',        'In Progress',           'Servicing item is actively being worked',                  2 UNION ALL
                    SELECT 'Completed',         'Completed',             'Servicing item has been completed',                        3 UNION ALL
                    SELECT 'Escalated',         'Escalated',             'Servicing item has been escalated',                        4 UNION ALL
                    SELECT 'OnHold',            'On Hold',               'Servicing item is on hold',                                5
                ) AS s
                WHERE NOT EXISTS (
                    SELECT 1 FROM `liens_LookupValues`
                    WHERE `Category` = 'ServicingStatus' AND `Code` = s.Code AND `TenantId` IS NULL
                );
                """);

            // ── ServicingPriority ─────────────────────────────────────────────────
            // Source: v2.0 ServicingPriority domain enum

            migrationBuilder.Sql($"""
                INSERT INTO `liens_LookupValues`
                    (`Id`, `TenantId`, `Category`, `Code`, `Name`, `Description`,
                     `SortOrder`, `IsActive`, `IsSystem`,
                     `CreatedByUserId`, `UpdatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`)
                SELECT UUID(), NULL, 'ServicingPriority', s.Code, s.Name, s.Description, s.Sort, 1, 1,
                       '{SystemUserId}', NULL, NOW(), NOW()
                FROM (
                    SELECT 'Low'    AS Code, 'Low'    AS Name, 'Low priority'    AS Description, 1 AS Sort UNION ALL
                    SELECT 'Normal',         'Normal',          'Normal priority',                 2 UNION ALL
                    SELECT 'High',           'High',            'High priority',                   3 UNION ALL
                    SELECT 'Urgent',         'Urgent',          'Urgent — requires immediate attention', 4
                ) AS s
                WHERE NOT EXISTS (
                    SELECT 1 FROM `liens_LookupValues`
                    WHERE `Category` = 'ServicingPriority' AND `Code` = s.Code AND `TenantId` IS NULL
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove all system-seeded lookup values inserted by this migration.
            migrationBuilder.Sql("""
                DELETE FROM `liens_LookupValues`
                WHERE `IsSystem` = 1 AND `TenantId` IS NULL
                  AND `Category` IN (
                      'State', 'ContactType', 'AccidentType', 'DocumentCategory',
                      'LienStatus', 'LienType', 'CaseStatus', 'ServicingStatus', 'ServicingPriority'
                  );
                """);
        }
    }
}
