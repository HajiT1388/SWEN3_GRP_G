using Microsoft.EntityFrameworkCore;
using DMSG3.Domain.Entities;

namespace DMSG3.Infrastructure
{
    public class DMSG3_DbContext : DbContext
    {
        public DMSG3_DbContext(DbContextOptions<DMSG3_DbContext> options) : base(options) { }

        public DbSet<Document> Documents => Set<Document>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("public");

            modelBuilder.Entity<Document>(entity =>
            {
                entity.ToTable("documents");

                entity.HasKey(d => d.Id);

                entity.Property(d => d.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(d => d.Name)
                    .HasColumnName("name")
                    .IsRequired();

                entity.Property(d => d.OriginalFileName)
                    .HasColumnName("original_file_name")
                    .IsRequired();

                entity.Property(d => d.ContentType)
                    .HasColumnName("content_type")
                    .IsRequired();

                entity.Property(d => d.SizeBytes)
                    .HasColumnName("size_bytes")
                    .IsRequired();

                entity.Property(d => d.Content)
                    .HasColumnName("content")
                    .HasColumnType("bytea")
                    .IsRequired();

                entity.Property(d => d.UploadTime)
                    .HasColumnName("upload_time")
                    .HasDefaultValueSql("now()");

                entity.HasIndex(d => d.UploadTime).HasDatabaseName("ix_documents_upload_time");
            });
        }
    }
}