using KartCategoryService.Domain.Categories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KartCategoryService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps Category to the `categories` table exactly as database-design.md specifies - materialized
/// path (ancestor_path) storage, the redundant depth column, and the three write-time invariant
/// CHECK constraints (defense-in-depth only; the real cycle/depth checks are domain code).
/// </summary>
public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories", t =>
        {
            t.HasCheckConstraint("CK_categories_depth_range", "depth BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_categories_depth_matches_path", "depth = COALESCE(array_length(ancestor_path, 1), 0) + 1");
            t.HasCheckConstraint("CK_categories_parent_not_self", "parent_id IS DISTINCT FROM category_id");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("category_id")
            .ValueGeneratedNever();

        builder.Property(c => c.Name)
            .HasColumnName("name")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.ParentId)
            .HasColumnName("parent_id");

        builder.Property<List<Guid>>("_ancestorPath")
            .HasColumnName("ancestor_path")
            .HasColumnType("uuid[]")
            .IsRequired();

        builder.Property(c => c.Depth)
            .HasColumnName("depth")
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(c => c.DisplayOrder)
            .HasColumnName("display_order")
            .HasColumnType("integer")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasColumnType("text")
            .HasConversion(
                status => status.ToString().ToLowerInvariant(),
                value => value == "deprecated" ? CategoryStatus.Deprecated : CategoryStatus.Active)
            .IsRequired();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by").HasColumnType("text").IsRequired();
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by").HasColumnType("text").IsRequired();

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(c => c.ParentId)
            .HasConstraintName("FK_categories_parent_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.ParentId, c.Status })
            .HasDatabaseName("idx_categories_parent_status");

        builder.HasIndex(c => new { c.ParentId, c.DisplayOrder })
            .HasDatabaseName("idx_categories_parent_display_order");

        builder.HasIndex(c => new { c.Status, c.Depth })
            .HasDatabaseName("idx_categories_status_depth");

        builder.HasIndex("_ancestorPath")
            .HasDatabaseName("idx_categories_ancestor_path")
            .HasMethod("gin");

        builder.Ignore(c => c.AncestorPath);
        builder.Ignore(c => c.DomainEvents);
    }
}
