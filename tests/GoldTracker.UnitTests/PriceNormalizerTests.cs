using FluentAssertions;
using GoldTracker.Application.Contracts.Repositories;
using GoldTracker.Application.Services;
using GoldTracker.Domain.Entities;
using GoldTracker.Domain.Enums;
using GoldTracker.Domain.Normalization;
using Moq;
using Xunit;

namespace GoldTracker.UnitTests;

public class PriceNormalizerTests
{
  [Fact]
  public async Task Normalize_should_map_Vietnamese_form_synonyms()
  {
    var sourceRepo = new Mock<ISourceRepository>();
    var productRepo = new Mock<IProductRepository>();
    
    var mockSourceId = Guid.NewGuid();
    sourceRepo.Setup(r => r.EnsureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Source { Id = mockSourceId, Name = "DOJI", BaseUrl = "https://doji.vn" });
    
    var mockProductId = Guid.NewGuid();
    productRepo.Setup(r => r.FindOrCreateAsync(It.IsAny<string>(), It.IsAny<GoldForm>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Product { Id = mockProductId, Brand = "DOJI", Form = GoldForm.Ring, Karat = 24, Region = "Hanoi" });

    var normalizer = new PriceNormalizer(sourceRepo.Object, productRepo.Object);

    var raw = new RawPriceRecord
    {
      SourceName = "DOJI",
      Brand = "DOJI",
      Form = "nhẫn tròn trơn 24K",
      Karat = "24K",
      Region = "Hanoi",
      PriceBuy = 7420000,
      PriceSell = 7520000,
      Currency = "Vnd",
      CollectedAt = DateTimeOffset.UtcNow,
      EffectiveAt = DateTimeOffset.UtcNow
    };

    var (productId, sourceId, tick) = await normalizer.NormalizeAsync(raw);

    productId.Should().NotBeEmpty();
    sourceId.Should().NotBeEmpty();
    tick.Currency.Should().Be("VND");
    productRepo.Verify(r => r.FindOrCreateAsync("DOJI", GoldForm.Ring, 24, "Hanoi", It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task Normalize_should_map_karat_from_strings()
  {
    var sourceRepo = new Mock<ISourceRepository>();
    var productRepo = new Mock<IProductRepository>();
    
    var sourceId2 = Guid.NewGuid();
    sourceRepo.Setup(r => r.EnsureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Source { Id = sourceId2, Name = "DOJI", BaseUrl = "https://doji.vn" });
    
    productRepo.Setup(r => r.FindOrCreateAsync(It.IsAny<string>(), It.IsAny<GoldForm>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Product { Id = Guid.NewGuid() });

    var normalizer = new PriceNormalizer(sourceRepo.Object, productRepo.Object);

    var raw = new RawPriceRecord
    {
      SourceName = "DOJI",
      Brand = "DOJI",
      Form = "ring",
      Karat = "9999",
      Region = "Hanoi",
      PriceBuy = 7420000,
      PriceSell = 7520000,
      Currency = "VND",
      CollectedAt = DateTimeOffset.UtcNow,
      EffectiveAt = DateTimeOffset.UtcNow
    };

    var (_, _, _) = await normalizer.NormalizeAsync(raw);

    productRepo.Verify(r => r.FindOrCreateAsync("DOJI", GoldForm.Ring, 24, "Hanoi", It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task Normalize_should_enforce_price_constraints()
  {
    var sourceRepo = new Mock<ISourceRepository>();
    var productRepo = new Mock<IProductRepository>();
    
    sourceRepo.Setup(r => r.EnsureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Source { Id = Guid.NewGuid(), Name = "DOJI" });

    var normalizer = new PriceNormalizer(sourceRepo.Object, productRepo.Object);

    var raw = new RawPriceRecord
    {
      SourceName = "DOJI",
      Brand = "DOJI",
      Form = "ring",
      PriceBuy = 100,
      PriceSell = 50, // Invalid: sell < buy
      Currency = "VND",
      CollectedAt = DateTimeOffset.UtcNow,
      EffectiveAt = DateTimeOffset.UtcNow
    };

    await Assert.ThrowsAsync<ArgumentException>(() => normalizer.NormalizeAsync(raw));
  }

  [Fact]
  public async Task Normalize_should_compute_stable_hash()
  {
    var sourceRepo = new Mock<ISourceRepository>();
    var productRepo = new Mock<IProductRepository>();
    
    sourceRepo.Setup(r => r.EnsureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Source { Id = Guid.NewGuid() });
    
    productRepo.Setup(r => r.FindOrCreateAsync(It.IsAny<string>(), It.IsAny<GoldForm>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Product { Id = Guid.NewGuid() });

    var normalizer = new PriceNormalizer(sourceRepo.Object, productRepo.Object);

    var raw = new RawPriceRecord
    {
      SourceName = "DOJI",
      Brand = "DOJI",
      Form = "ring",
      Karat = "24",
      Region = "Hanoi",
      PriceBuy = 7420000,
      PriceSell = 7520000,
      Currency = "VND",
      CollectedAt = new DateTimeOffset(2025, 11, 2, 9, 30, 0, TimeSpan.Zero),
      EffectiveAt = new DateTimeOffset(2025, 11, 2, 9, 30, 0, TimeSpan.Zero)
    };

    var (_, _, tick1) = await normalizer.NormalizeAsync(raw);
    var (_, _, tick2) = await normalizer.NormalizeAsync(raw);

    tick1.RawHash.Should().Be(tick2.RawHash);
    tick1.RawHash.Should().NotBeEmpty();
    tick1.RawHash.Length.Should().Be(64); // SHA-256 hex
  }
}

