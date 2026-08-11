using KartCategoryService.Domain.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KartCategoryService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps ProductAttribute to `attributes` (+ its owned `attribute_values` child table), mirroring
/// CategoryConfiguration's own shape for the platform's second aggregate in this service.
/// </summary>
public sealed class AttributeConfiguration : IEntityTypeConfiguration<ProductAttribute>
{
    public void Configure(EntityTypeBuilder<ProductAttribute> builder)
    {
        builder.ToTable("attributes");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnName("attribute_id")
            .ValueGeneratedNever();

        builder.Property(a => a.Name)
            .HasColumnName("name")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(a => a.CategoryId)
            .HasColumnName("category_id");

        builder.Property(a => a.DataType)
            .HasColumnName("data_type")
            .HasColumnType("text")
            .HasConversion(
                dataType => dataType.ToString().ToLowerInvariant(),
                value => Enum.Parse<AttributeDataType>(value, true))
            .IsRequired();

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasColumnType("text")
            .HasConversion(
                status => status.ToString().ToLowerInvariant(),
                value => value == "deprecated" ? AttributeStatus.Deprecated : AttributeStatus.Active)
            .IsRequired();

        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by").HasColumnType("text").IsRequired();
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by").HasColumnType("text").IsRequired();

        // CategoryId deliberately has no FK constraint against categories - a category can be
        // deprecated (never physically deleted) after an attribute already references it, and this
        // service tolerates that the same way Category itself tolerates deprecated parents.
        builder.HasIndex(a => new { a.CategoryId, a.Status })
            .HasDatabaseName("idx_attributes_category_status");

        // The Values collection is owned by ProductAttribute (never queried/mutated independently) -
        // accessed only through the encapsulated `Values` navigation, backed by the private
        // `_values` field EF discovers by its standard backing-field naming convention.
        builder.OwnsMany(a => a.Values, valuesBuilder =>
        {
            valuesBuilder.ToTable("attribute_values");
            valuesBuilder.WithOwner().HasForeignKey("AttributeId");
            valuesBuilder.HasKey(v => v.Id);

            valuesBuilder.Property(v => v.Id).HasColumnName("attribute_value_id").ValueGeneratedNever();
            valuesBuilder.Property(v => v.Value).HasColumnName("value").HasColumnType("text").IsRequired();
            valuesBuilder.Property(v => v.DisplayOrder).HasColumnName("display_order").HasColumnType("integer").IsRequired();
            valuesBuilder.Property<Guid>("AttributeId").HasColumnName("attribute_id");

            valuesBuilder.HasIndex("AttributeId").HasDatabaseName("idx_attribute_values_attribute_id");
        });
        builder.Navigation(a => a.Values).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(a => a.DomainEvents);
    }
}
