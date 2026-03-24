using Eva.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Eva.Tests.Infrastructure;

internal static class TestDbContextFactory
{
    public static EvaDbContext CreateInMemoryContext(string databaseName, DefaultHttpContext? httpContext = null)
    {
        var options = new DbContextOptionsBuilder<EvaDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var accessor = new HttpContextAccessor
        {
            HttpContext = httpContext ?? new DefaultHttpContext()
        };

        return new EvaDbContext(options, accessor);
    }
}
