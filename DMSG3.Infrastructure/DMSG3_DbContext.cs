using Microsoft.EntityFrameworkCore;
using DMSG3.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

                entity.HasKey(document => document.Id);
                entity.Property(d => d.Id)
                   .HasColumnName("id")
                   .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(document => document.FileName)
                   .HasColumnName("file_name")
                   .IsRequired();

                entity.Property(document => document.FileContent)
                   .HasColumnName("file_content")
                   .IsRequired();

                entity.Property(d => d.UploadTime)
                    .HasColumnName("upload_time")
                    .HasDefaultValueSql("now()");
            });
        }
    }
}
