using CabinetDesigner.Application.Services;
using Xunit;

namespace CabinetDesigner.Tests.Services;

public sealed class CatalogServicePricingTests
{
    [Fact]
    public void IsPricingConfigured_DefaultV1Instance_IsFalse()
    {
        var service = new CatalogService();

        Assert.False(service.IsPricingConfigured);
    }
}
