using Npgsql;

namespace OpenParking.Infrastructure.Data;

/// <summary>
/// Accepts a Postgres connection string in either of the two forms in use on
/// this project and returns the keyword form Npgsql requires.
///
/// DATABASE_URL is shared with the Python AI service and is what Neon hands out,
/// and both of those use the URI form (postgresql://user:pass@host:5432/db).
/// Npgsql only parses keyword syntax (Host=...;Port=...) and throws "Format of
/// the initialization string does not conform to specification" on a URI, which
/// crashed the API at startup for anyone following .env.example.
/// </summary>
public static class PostgresConnectionString
{
    public static string Normalize(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var trimmed = connectionString.Trim();

        if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            // Already keyword syntax; leave it exactly as the operator wrote it.
            return trimmed;
        }

        var uri = new Uri(trimmed);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))
        };

        // user:pass is percent-encoded in a URI, so a password containing '@' or
        // ':' arrives as %40 or %3A and must be decoded before it is used.
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var separator = uri.UserInfo.IndexOf(':');
            builder.Username = Uri.UnescapeDataString(separator < 0 ? uri.UserInfo : uri.UserInfo[..separator]);
            if (separator >= 0)
                builder.Password = Uri.UnescapeDataString(uri.UserInfo[(separator + 1)..]);
        }

        foreach (var (key, value) in ParseQuery(uri.Query))
        {
            switch (key.ToLowerInvariant())
            {
                // Neon requires TLS and puts it in the URI as ?sslmode=require.
                // Dropping it would make the connection fail, or worse, fall back
                // to plaintext against a server that permits it.
                case "sslmode":
                    builder.SslMode = Enum.Parse<SslMode>(value.Replace("-", string.Empty), ignoreCase: true);
                    break;

                // libpq parameters with no Npgsql equivalent (channel_binding,
                // options, etc.) are ignored rather than passed through, since
                // Npgsql rejects keys it does not recognise.
            }
        }

        return builder.ConnectionString;
    }

    private static IEnumerable<(string Key, string Value)> ParseQuery(string query)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0) continue;

            yield return (
                Uri.UnescapeDataString(pair[..separator]),
                Uri.UnescapeDataString(pair[(separator + 1)..]));
        }
    }
}
