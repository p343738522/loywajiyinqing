using MySql.Data.MySqlClient;
using SystemModule;

namespace GameGate.Core;

/// <summary>
/// Optional secondary ticket→pt_id lookup. CM_LOGIN_AUTH is authenticated by
/// LoginGate (Native77 2018), not GameGate. This resolver is only used if
/// something calls <see cref="MobileTicketStore.ResolveAccount"/>. Unconfigured
/// TicketDb never invents a successful mapping.
/// </summary>
public static class MobileTicketResolver
{
    public const string ConnectionStringEnvironmentVariable = "GAMEGATE_TICKET_DB";

    public static void Install(GateConfig config, Action<string, string>? log)
    {
        var connectionString = FirstNonEmpty(
            config?.TicketDb,
            Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable),
            Environment.GetEnvironmentVariable("LOGINGATE_TICKET_DB"));

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            log?.Invoke("WARN",
                "TicketDb unset; GameGate will not resolve tickets. LoginGate [Login]/TicketDb or LOGINGATE_TICKET_DB is required (fail-closed, no fake login).");
            MobileTicketStore.ExternalResolver = _ => null;
            return;
        }

        log?.Invoke("INFO",
            "GameGate TicketDb configured for optional in-process resolve; CM_LOGIN_AUTH still goes to LoginGate.");
        MobileTicketStore.ExternalResolver = ticket => Resolve(ticket, connectionString, log);
    }

    private static string? Resolve(string ticket, string connectionString,
        Action<string, string>? log)
    {
        if (string.IsNullOrEmpty(ticket)) return null;
        try
        {
            using var conn = new MySqlConnection(connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT t.pt_id
FROM account.ticket t
WHERE BINARY t.ticket = BINARY @t AND t.create_time > @exp
LIMIT 1";
            cmd.Parameters.AddWithValue("@t", ticket);
            cmd.Parameters.AddWithValue("@exp",
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 300);
            var value = cmd.ExecuteScalar();
            var account = Convert.ToString(value);
            return string.IsNullOrEmpty(account) ? null : account;
        }
        catch (Exception ex)
        {
            log?.Invoke("ERROR", "Mobile ticket resolve failed: " + ex.Message);
            return null;
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }
}
