using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

[DbContext(typeof(CareConnect.Infrastructure.Data.CareConnectDbContext))]
[Migration("20260404000000_AddNotificationDedupeKey")]
partial class AddNotificationDedupeKey
{
}
