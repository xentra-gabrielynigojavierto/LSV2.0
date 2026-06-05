using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    private static readonly Guid ChiroId   = new("40000000-0000-0000-0000-000000000001");
    private static readonly Guid PtId      = new("40000000-0000-0000-0000-000000000002");
    private static readonly Guid OrthoId   = new("40000000-0000-0000-0000-000000000003");
    private static readonly Guid ImgId     = new("40000000-0000-0000-0000-000000000004");
    private static readonly Guid PainId    = new("40000000-0000-0000-0000-000000000005");
    private static readonly Guid ExtremId  = new("40000000-0000-0000-0000-000000000006");
    private static readonly Guid SpineId   = new("40000000-0000-0000-0000-000000000007");
    private static readonly Guid NeuroId   = new("40000000-0000-0000-0000-000000000008");
    private static readonly Guid SurgeryId = new("40000000-0000-0000-0000-000000000009");

    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("cc_Categories");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).IsRequired();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Code).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Description).HasMaxLength(1000);
        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.CreatedAtUtc).IsRequired();

        builder.HasIndex(c => c.Code).IsUnique();

        builder.HasMany(c => c.ProviderCategories)
               .WithOne(pc => pc.Category)
               .HasForeignKey(pc => pc.CategoryId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(
            CreateCategory(ChiroId,   "Chiropractic",     "CHIRO",   null, true),
            CreateCategory(PtId,      "Physical Therapy",  "PT",      null, true),
            CreateCategory(OrthoId,   "Orthopedic",        "ORTHO",   null, true),
            CreateCategory(ImgId,     "Imaging",           "IMG",     null, true),
            CreateCategory(PainId,    "Pain Management",   "PAIN",    null, true),
            CreateCategory(ExtremId,  "Extremities",       "EXTREM",  null, true),
            CreateCategory(SpineId,   "Spine Surgeon",     "SPINE",   null, true),
            CreateCategory(NeuroId,   "Neurology",         "NEURO",   null, true),
            CreateCategory(SurgeryId, "Surgery Center",    "SURGERY", null, true)
        );
    }

    private static object CreateCategory(Guid id, string name, string code, string? description, bool isActive)
    {
        return new
        {
            Id = id,
            Name = name,
            Code = code,
            Description = description,
            IsActive = isActive,
            CreatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }
}
