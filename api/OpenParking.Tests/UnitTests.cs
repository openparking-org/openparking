using Xunit;
using OpenParking.Core.Models;
using OpenParking.Core.Entities;

namespace OpenParking.Tests;

public class CoreTests
{
    [Fact]
    public void PaginatedQuery_Clamps_Maximum_Page_Size()
    {
        var query = new PaginatedQuery { Page = 1, PageSize = 500 };
        Assert.Equal(100, query.PageSize);
    }

    [Fact]
    public void PaginatedQuery_Prevents_Zero_Or_Negative_Page_Size()
    {
        var query = new PaginatedQuery { Page = 1, PageSize = -5 };
        Assert.Equal(1, query.PageSize);
    }

    [Fact]
    public void PagedResult_Calculates_TotalPages_Accurately()
    {
        var items = new List<string> { "item1", "item2" };
        var paged = PagedResult<string>.Create(items, totalCount: 25, page: 1, pageSize: 10);

        Assert.Equal(3, paged.TotalPages);
        Assert.True(paged.HasNextPage);
        Assert.False(paged.HasPreviousPage);
    }

    [Fact]
    public void SystemSetting_Defaults_Are_Consistent()
    {
        var setting = new SystemSetting
        {
            Key = "pricing.base_hourly_rate",
            Value = "5.00"
        };

        Assert.Equal("pricing.base_hourly_rate", setting.Key);
        Assert.Equal("General", setting.Category);
        Assert.Equal("System", setting.UpdatedBy);
    }
}
