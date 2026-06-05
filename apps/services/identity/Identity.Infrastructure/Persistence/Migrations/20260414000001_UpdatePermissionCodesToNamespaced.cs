using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260414000001_UpdatePermissionCodesToNamespaced")]
public partial class UpdatePermissionCodesToNamespaced : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_CARECONNECT.referral:create'         WHERE `Id` = '60000000-0000-0000-0000-000000000001' AND `Code` = 'referral:create';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_CARECONNECT.referral:read:own'        WHERE `Id` = '60000000-0000-0000-0000-000000000002' AND `Code` = 'referral:read:own';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_CARECONNECT.referral:cancel'          WHERE `Id` = '60000000-0000-0000-0000-000000000003' AND `Code` = 'referral:cancel';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_CARECONNECT.referral:read:addressed'  WHERE `Id` = '60000000-0000-0000-0000-000000000004' AND `Code` = 'referral:read:addressed';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_CARECONNECT.referral:accept'          WHERE `Id` = '60000000-0000-0000-0000-000000000005' AND `Code` = 'referral:accept';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_CARECONNECT.referral:decline'         WHERE `Id` = '60000000-0000-0000-0000-000000000006' AND `Code` = 'referral:decline';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_CARECONNECT.provider:search'          WHERE `Id` = '60000000-0000-0000-0000-000000000007' AND `Code` = 'provider:search';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_CARECONNECT.provider:map'             WHERE `Id` = '60000000-0000-0000-0000-000000000008' AND `Code` = 'provider:map';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_CARECONNECT.appointment:create'       WHERE `Id` = '60000000-0000-0000-0000-000000000009' AND `Code` = 'appointment:create';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_CARECONNECT.appointment:update'       WHERE `Id` = '60000000-0000-0000-0000-000000000010' AND `Code` = 'appointment:update';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_CARECONNECT.appointment:read:own'     WHERE `Id` = '60000000-0000-0000-0000-000000000011' AND `Code` = 'appointment:read:own';

            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_LIENS.lien:create'    WHERE `Id` = '60000000-0000-0000-0000-000000000012' AND `Code` = 'lien:create';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_LIENS.lien:offer'     WHERE `Id` = '60000000-0000-0000-0000-000000000013' AND `Code` = 'lien:offer';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_LIENS.lien:read:own'  WHERE `Id` = '60000000-0000-0000-0000-000000000014' AND `Code` = 'lien:read:own';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_LIENS.lien:browse'    WHERE `Id` = '60000000-0000-0000-0000-000000000015' AND `Code` = 'lien:browse';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_LIENS.lien:purchase'  WHERE `Id` = '60000000-0000-0000-0000-000000000016' AND `Code` = 'lien:purchase';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_LIENS.lien:read:held' WHERE `Id` = '60000000-0000-0000-0000-000000000017' AND `Code` = 'lien:read:held';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_LIENS.lien:service'   WHERE `Id` = '60000000-0000-0000-0000-000000000018' AND `Code` = 'lien:service';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_LIENS.lien:settle'    WHERE `Id` = '60000000-0000-0000-0000-000000000019' AND `Code` = 'lien:settle';

            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_FUND.application:create'         WHERE `Id` = '60000000-0000-0000-0000-000000000020' AND `Code` = 'application:create';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_FUND.application:read:own'        WHERE `Id` = '60000000-0000-0000-0000-000000000021' AND `Code` = 'application:read:own';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_FUND.application:cancel'          WHERE `Id` = '60000000-0000-0000-0000-000000000022' AND `Code` = 'application:cancel';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_FUND.application:read:addressed'  WHERE `Id` = '60000000-0000-0000-0000-000000000023' AND `Code` = 'application:read:addressed';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_FUND.application:evaluate'        WHERE `Id` = '60000000-0000-0000-0000-000000000024' AND `Code` = 'application:evaluate';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_FUND.application:approve'         WHERE `Id` = '60000000-0000-0000-0000-000000000025' AND `Code` = 'application:approve';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_FUND.application:decline'         WHERE `Id` = '60000000-0000-0000-0000-000000000026' AND `Code` = 'application:decline';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_FUND.party:create'                WHERE `Id` = '60000000-0000-0000-0000-000000000027' AND `Code` = 'party:create';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_FUND.party:read:own'              WHERE `Id` = '60000000-0000-0000-0000-000000000028' AND `Code` = 'party:read:own';
            UPDATE `idt_Capabilities` SET `Code` = 'SYNQ_FUND.application:status:view'     WHERE `Id` = '60000000-0000-0000-0000-000000000029' AND `Code` = 'application:status:view';
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            UPDATE `idt_Capabilities` SET `Code` = 'referral:create'         WHERE `Id` = '60000000-0000-0000-0000-000000000001';
            UPDATE `idt_Capabilities` SET `Code` = 'referral:read:own'       WHERE `Id` = '60000000-0000-0000-0000-000000000002';
            UPDATE `idt_Capabilities` SET `Code` = 'referral:cancel'         WHERE `Id` = '60000000-0000-0000-0000-000000000003';
            UPDATE `idt_Capabilities` SET `Code` = 'referral:read:addressed' WHERE `Id` = '60000000-0000-0000-0000-000000000004';
            UPDATE `idt_Capabilities` SET `Code` = 'referral:accept'         WHERE `Id` = '60000000-0000-0000-0000-000000000005';
            UPDATE `idt_Capabilities` SET `Code` = 'referral:decline'        WHERE `Id` = '60000000-0000-0000-0000-000000000006';
            UPDATE `idt_Capabilities` SET `Code` = 'provider:search'         WHERE `Id` = '60000000-0000-0000-0000-000000000007';
            UPDATE `idt_Capabilities` SET `Code` = 'provider:map'            WHERE `Id` = '60000000-0000-0000-0000-000000000008';
            UPDATE `idt_Capabilities` SET `Code` = 'appointment:create'      WHERE `Id` = '60000000-0000-0000-0000-000000000009';
            UPDATE `idt_Capabilities` SET `Code` = 'appointment:update'      WHERE `Id` = '60000000-0000-0000-0000-000000000010';
            UPDATE `idt_Capabilities` SET `Code` = 'appointment:read:own'    WHERE `Id` = '60000000-0000-0000-0000-000000000011';

            UPDATE `idt_Capabilities` SET `Code` = 'lien:create'    WHERE `Id` = '60000000-0000-0000-0000-000000000012';
            UPDATE `idt_Capabilities` SET `Code` = 'lien:offer'     WHERE `Id` = '60000000-0000-0000-0000-000000000013';
            UPDATE `idt_Capabilities` SET `Code` = 'lien:read:own'  WHERE `Id` = '60000000-0000-0000-0000-000000000014';
            UPDATE `idt_Capabilities` SET `Code` = 'lien:browse'    WHERE `Id` = '60000000-0000-0000-0000-000000000015';
            UPDATE `idt_Capabilities` SET `Code` = 'lien:purchase'  WHERE `Id` = '60000000-0000-0000-0000-000000000016';
            UPDATE `idt_Capabilities` SET `Code` = 'lien:read:held' WHERE `Id` = '60000000-0000-0000-0000-000000000017';
            UPDATE `idt_Capabilities` SET `Code` = 'lien:service'   WHERE `Id` = '60000000-0000-0000-0000-000000000018';
            UPDATE `idt_Capabilities` SET `Code` = 'lien:settle'    WHERE `Id` = '60000000-0000-0000-0000-000000000019';

            UPDATE `idt_Capabilities` SET `Code` = 'application:create'         WHERE `Id` = '60000000-0000-0000-0000-000000000020';
            UPDATE `idt_Capabilities` SET `Code` = 'application:read:own'       WHERE `Id` = '60000000-0000-0000-0000-000000000021';
            UPDATE `idt_Capabilities` SET `Code` = 'application:cancel'         WHERE `Id` = '60000000-0000-0000-0000-000000000022';
            UPDATE `idt_Capabilities` SET `Code` = 'application:read:addressed' WHERE `Id` = '60000000-0000-0000-0000-000000000023';
            UPDATE `idt_Capabilities` SET `Code` = 'application:evaluate'       WHERE `Id` = '60000000-0000-0000-0000-000000000024';
            UPDATE `idt_Capabilities` SET `Code` = 'application:approve'        WHERE `Id` = '60000000-0000-0000-0000-000000000025';
            UPDATE `idt_Capabilities` SET `Code` = 'application:decline'        WHERE `Id` = '60000000-0000-0000-0000-000000000026';
            UPDATE `idt_Capabilities` SET `Code` = 'party:create'               WHERE `Id` = '60000000-0000-0000-0000-000000000027';
            UPDATE `idt_Capabilities` SET `Code` = 'party:read:own'             WHERE `Id` = '60000000-0000-0000-0000-000000000028';
            UPDATE `idt_Capabilities` SET `Code` = 'application:status:view'    WHERE `Id` = '60000000-0000-0000-0000-000000000029';
        ");
    }
}
