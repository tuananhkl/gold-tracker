using GoldTracker.Domain.Entities;
using GoldTracker.Domain.Enums;

namespace GoldTracker.Application.Contracts.Repositories;

public interface IProductRepository
{
  Task<Product?> FindAsync(string brand, GoldForm form, int? karat, string? region, CancellationToken ct = default);
  Task<Product> FindOrCreateAsync(string brand, GoldForm form, int? karat, string? region, CancellationToken ct = default);
}

