using Microsoft.EntityFrameworkCore;
using EvaluacionDotNet.Api.Models;

namespace EvaluacionDotNet.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Currency>(entity =>
        {
            entity.ToTable("Currency");
            entity.Property(c => c.Symbol).HasMaxLength(3).IsUnicode(false);
            entity.Property(c => c.Name).HasMaxLength(100);
            entity.HasAlternateKey(c => c.Symbol);
        });
        model.Entity<ExchangeRate>(entity =>
        {
            entity.ToTable("ExchangeRates", table => table.HasCheckConstraint("CK_Rate_Positive", "[Rate] > 0 AND [BaseCurrency] <> [TargetCurrency]"));
            entity.Property(r => r.BaseCurrency).HasMaxLength(3).IsUnicode(false);
            entity.Property(r => r.TargetCurrency).HasMaxLength(3).IsUnicode(false);
            entity.Property(r => r.Rate).HasPrecision(28, 12);
            entity.Property(r => r.Date).HasColumnType("datetime2");
            entity.HasIndex(r => new { r.BaseCurrency, r.TargetCurrency, r.Date }).IsUnique();
            entity.HasOne<Currency>().WithMany().HasForeignKey(r => r.BaseCurrency)
                .HasPrincipalKey(c => c.Symbol).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Currency>().WithMany().HasForeignKey(r => r.TargetCurrency)
                .HasPrincipalKey(c => c.Symbol).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
