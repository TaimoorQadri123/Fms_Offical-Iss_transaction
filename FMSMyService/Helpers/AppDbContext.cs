using FMSMyService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FMSMyService.Helpers
{
    internal class AppDbContext :DbContext
    {
        private readonly string _connectionString;
        public AppDbContext (string connectionString)
        {
            _connectionString = connectionString;

        }
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<ModifyPoints> ModifyPoints { get; set; }
        protected  override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured && !string.IsNullOrEmpty(_connectionString))
            {
                optionsBuilder.UseSqlServer(_connectionString);
            }
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ModifyPoints>(entity =>
            {
                entity.ToTable("ModifyPoints");
                entity.HasKey(e => e.ModifyType);
                entity.Property(e => e.ModifyType).HasMaxLength(50);
                entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
                entity.Property(e => e.TrxLogId2).HasConversion(
                    v => v.ToString(),
                    v => long.Parse(v)
                );
            }
            );
        }
    }
}
