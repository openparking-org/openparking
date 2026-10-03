using Microsoft.Extensions.Configuration;
using OpenParking.Api.Configuration;
using Xunit;

namespace OpenParking.Tests;

public class FlatEnvAliasesTests
{
    private static IConfiguration Build(Dictionary<string, string?> appsettings, Dictionary<string, string?> env)
    {
        var builder = new ConfigurationManager();
        builder.AddInMemoryCollection(appsettings);
        builder.AddInMemoryCollection(env);
        FlatEnvAliases.Apply(builder, builder);
        return builder;
    }

    [Fact]
    public void JwtSecretFromEnv_OverridesTheAppsettingsValue()
    {
        // The issuer reads JWT:Secret and so does the validator. Both must see
        // the .env value, or tokens are signed and checked with different keys.
        var config = Build(
            new() { ["JWT:Secret"] = "appsettings-secret-appsettings-secret" },
            new() { ["JWT_SECRET"] = "env-secret-env-secret-env-secret-0000" });

        Assert.Equal("env-secret-env-secret-env-secret-0000", config["JWT:Secret"]);
    }

    [Fact]
    public void JwtExpiryFromEnv_IsMappedToo()
    {
        var config = Build(new() { ["JWT:ExpiryHours"] = "24" }, new() { ["JWT_EXPIRY_HOURS"] = "6" });

        Assert.Equal("6", config["JWT:ExpiryHours"]);
    }

    [Fact]
    public void WithoutEnvValues_AppsettingsIsLeftAlone()
    {
        var config = Build(new() { ["JWT:Secret"] = "appsettings-secret-appsettings-secret" }, new());

        Assert.Equal("appsettings-secret-appsettings-secret", config["JWT:Secret"]);
    }

    [Fact]
    public void ABlankEnvValue_DoesNotWipeTheConfiguredSecret()
    {
        var config = Build(new() { ["JWT:Secret"] = "appsettings-secret-appsettings-secret" }, new() { ["JWT_SECRET"] = "  " });

        Assert.Equal("appsettings-secret-appsettings-secret", config["JWT:Secret"]);
    }
}
