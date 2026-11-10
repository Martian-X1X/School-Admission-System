using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Domain.Enums;

namespace SchoolAdmission.Infrastructure.Persistence.Configurations;

public class AdmissionHoldConfiguration : IEntityTypeConfiguration<AdmissionHold>
{
    public void Configure(EntityTypeBuilder<AdmissionHold> builder)
    {
        builder.ToTable("admission_holds");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.ClassId)
            .HasColumnName("class_id");

        builder.Property(x => x.StudentId)
            .HasColumnName("student_id")
            .IsRequired()
            .HasMaxLength(17);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<int>();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(x => x.ConfirmedAt)
            .HasColumnName("confirmed_at");

        builder.HasOne<AdmissionClass>()
            .WithMany()
            .HasForeignKey(x => x.ClassId);

        // Database-level backstop: one active or confirmed application per student per class.
        builder.HasIndex(x => new { x.ClassId, x.StudentId })
            .HasDatabaseName("ux_admission_holds_active_student_class")
            .IsUnique()
            .HasFilter("status IN (1, 2)");

        builder.HasIndex(x => new { x.Status, x.ExpiresAt })
            .HasDatabaseName("ix_admission_holds_status_expires");

        builder.HasIndex(x => x.StudentId)
            .HasDatabaseName("ix_admission_holds_student");

        builder.HasCheckConstraint(
            "ck_admission_holds_student_id_format",
            "student_id ~ '^[0-9]{17}$'");

        builder.HasCheckConstraint(
            "ck_admission_holds_expires_after_created",
            "expires_at > created_at");
    }
}
