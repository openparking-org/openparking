namespace OpenParking.Api.Configuration;

/// <summary>
/// Maps the flat variable names used in .env (JWT_SECRET) onto the
/// hierarchical keys the code reads (JWT:Secret).
///
/// Without this, Program.cs validated tokens with JWT_SECRET while UserService
/// signed them with JWT:Secret from appsettings.json. As soon as .env set
/// JWT_SECRET, which .env.example tells everyone to do, tokens were signed with
/// one key and checked against another, and every authenticated request
/// returned 401. Mapping once at startup gives every consumer the same value.
/// </summary>
public static class FlatEnvAliases
{
    private static readonly (string Flat, string Key)[] Aliases =
    [
        ("JWT_SECRET", "JWT:Secret"),
        ("JWT_EXPIRY_HOURS", "JWT:ExpiryHours"),
    ];

    public static void Apply(IConfigurationBuilder builder, IConfiguration current)
    {
        var overrides = new Dictionary<string, string?>();

        foreach (var (flat, key) in Aliases)
        {
            var value = current[flat];
            if (!string.IsNullOrWhiteSpace(value))
                overrides[key] = value;
        }

        // Added last, so it takes precedence over appsettings.json.
        if (overrides.Count > 0)
            builder.AddInMemoryCollection(overrides);
    }
}
