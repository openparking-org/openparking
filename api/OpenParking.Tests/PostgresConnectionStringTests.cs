using Npgsql;
using OpenParking.Infrastructure.Data;
using Xunit;

namespace OpenParking.Tests;

public class PostgresConnectionStringTests
{
    private static NpgsqlConnectionStringBuilder Parse(string input) =>
        new(PostgresConnectionString.Normalize(input));

    [Fact]
    public void Normalize_ConvertsTheUriFromEnvExample()
    {
        // Exactly what `cp .env.example .env` produces. This used to crash the
        // API on startup with "Format of the initialization string does not
        // conform to specification".
        var result = Parse("postgresql://dev:dev@localhost:5432/openparking");

        Assert.Equal("localhost", result.Host);
        Assert.Equal(5432, result.Port);
        Assert.Equal("openparking", result.Database);
        Assert.Equal("dev", result.Username);
        Assert.Equal("dev", result.Password);
    }

    [Fact]
    public void Normalize_AcceptsTheShortPostgresScheme()
    {
        var result = Parse("postgres://u:p@db.internal:6543/app");

        Assert.Equal("db.internal", result.Host);
        Assert.Equal(6543, result.Port);
        Assert.Equal("app", result.Database);
    }

    [Fact]
    public void Normalize_DefaultsThePortWhenTheUriOmitsIt()
    {
        var result = Parse("postgresql://u:p@ep-cool-name.neon.tech/neondb");

        Assert.Equal(5432, result.Port);
    }

    [Fact]
    public void Normalize_KeepsSslModeRequireFromANeonUri()
    {
        // Losing this would make the Neon connection fail, or fall back to an
        // unencrypted connection against a server that allows one.
        var result = Parse("postgresql://u:p@ep-cool-name.neon.tech/neondb?sslmode=require");

        Assert.Equal(SslMode.Require, result.SslMode);
    }

    [Fact]
    public void Normalize_IgnoresLibpqParametersNpgsqlDoesNotKnow()
    {
        // Neon adds channel_binding; passing it through would make Npgsql throw.
        var result = Parse("postgresql://u:p@host/db?sslmode=require&channel_binding=require");

        Assert.Equal(SslMode.Require, result.SslMode);
    }

    [Fact]
    public void Normalize_DecodesAPercentEncodedPassword()
    {
        // A password containing '@' or ':' must be percent-encoded in a URI.
        var result = Parse("postgresql://admin:p%40ss%3Aword@localhost:5432/openparking");

        Assert.Equal("p@ss:word", result.Password);
    }

    [Fact]
    public void Normalize_LeavesAKeywordStringUnchanged()
    {
        const string keyword = "Host=localhost;Port=5433;Database=openparking;Username=dev;Password=dev";

        Assert.Equal(keyword, PostgresConnectionString.Normalize(keyword));
    }

    [Fact]
    public void Normalize_RejectsAnEmptyString()
    {
        Assert.ThrowsAny<ArgumentException>(() => PostgresConnectionString.Normalize("  "));
    }
}
