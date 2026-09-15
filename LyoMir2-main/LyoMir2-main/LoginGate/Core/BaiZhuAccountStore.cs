using System.Security.Cryptography;
using System.Text;
using MySql.Data.MySqlClient;

namespace LoginGate.Core;

public sealed record BaiZhuAccountResult(int Code, string Description, string Ticket, string LastServer)
{
    public static BaiZhuAccountResult Ok(string ticket, string lastServer) =>
        new(0, "成功", ticket ?? string.Empty, lastServer ?? "null");

    public static BaiZhuAccountResult Fail(int code, string description) =>
        new(code, description ?? "失败", string.Empty, "null");
}

public interface IBaiZhuAccountStore
{
    bool IsConfigured { get; }
    string DescribeSource();
    ValueTask<BaiZhuAccountResult> LoginAsync(string id, string password, bool guest,
        CancellationToken cancellationToken);
    ValueTask<BaiZhuAccountResult> RegisterAsync(string id, string password, string safeCode,
        CancellationToken cancellationToken);
    ValueTask<BaiZhuAccountResult> ChangePasswordAsync(string id, string password, string safeCode,
        CancellationToken cancellationToken);
    ValueTask<BaiZhuAccountResult> BindAsync(string machineId, string id, string password,
        string safeCode, CancellationToken cancellationToken);
}

public sealed class RejectingBaiZhuAccountStore : IBaiZhuAccountStore
{
    public const string UnconfiguredError =
        "TicketDb unset; HTTP /account will not issue tickets (fail-closed, no fake login)";

    public bool IsConfigured => false;
    public string DescribeSource() => UnconfiguredError;

    public ValueTask<BaiZhuAccountResult> LoginAsync(string id, string password, bool guest,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(BaiZhuAccountResult.Fail(-1, UnconfiguredError));

    public ValueTask<BaiZhuAccountResult> RegisterAsync(string id, string password, string safeCode,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(BaiZhuAccountResult.Fail(-1, UnconfiguredError));

    public ValueTask<BaiZhuAccountResult> ChangePasswordAsync(string id, string password,
        string safeCode, CancellationToken cancellationToken) =>
        ValueTask.FromResult(BaiZhuAccountResult.Fail(-1, UnconfiguredError));

    public ValueTask<BaiZhuAccountResult> BindAsync(string machineId, string id, string password,
        string safeCode, CancellationToken cancellationToken) =>
        ValueTask.FromResult(BaiZhuAccountResult.Fail(-1, UnconfiguredError));
}

/// <summary>
/// BaiZhu LoginCenter tables in database <c>account</c>:
/// <c>normal(pt_id, uid, password, safecode)</c> and <c>ticket(ticket, pt_id, create_time)</c>.
/// Empty TicketDb never reaches this type.
/// </summary>
public sealed class MySqlBaiZhuAccountStore : IBaiZhuAccountStore
{
    private readonly string _connectionString;

    public MySqlBaiZhuAccountStore(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("A MySQL connection string is required.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public bool IsConfigured => true;
    public string DescribeSource() =>
        "MySQL account.normal + account.ticket (HTTP issue; fail-closed on miss/db error)";

    public async ValueTask<BaiZhuAccountResult> LoginAsync(string id, string password, bool guest,
        CancellationToken cancellationToken)
    {
        if (guest)
            return BaiZhuAccountResult.Fail(-110, "游客登录未配置（fail-closed）");
        if (string.IsNullOrEmpty(id))
            return BaiZhuAccountResult.Fail(-108, "用户名不存在");
        if (string.IsNullOrEmpty(password))
            return BaiZhuAccountResult.Fail(-102, "密码为空");

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"SELECT pt_id, password
FROM account.normal
WHERE BINARY uid = BINARY @id
LIMIT 1";
            command.Parameters.AddWithValue("@id", id);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return BaiZhuAccountResult.Fail(-108, "用户名不存在");
            var ptId = Convert.ToString(reader["pt_id"]) ?? string.Empty;
            var stored = Convert.ToString(reader["password"]) ?? string.Empty;
            await reader.DisposeAsync().ConfigureAwait(false);
            if (!string.Equals(stored, password, StringComparison.Ordinal))
                return BaiZhuAccountResult.Fail(-107, "密码错误");
            if (!IsNativeAccountSlot(ptId))
                return BaiZhuAccountResult.Fail(-1, "pt_id 超过原生 20 字节槽");
            var ticket = await IssueTicketAsync(connection, ptId, cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrEmpty(ticket))
                return BaiZhuAccountResult.Fail(-1, "ticket 写入失败（fail-closed）");
            var last = await ReadLastServerAsync(connection, ptId, cancellationToken)
                .ConfigureAwait(false);
            return BaiZhuAccountResult.Ok(ticket, last);
        }
        catch (MySqlException ex)
        {
            return BaiZhuAccountResult.Fail(-1, "ticket database unavailable: " + ex.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return BaiZhuAccountResult.Fail(-1, "ticket database error");
        }
    }

    public async ValueTask<BaiZhuAccountResult> RegisterAsync(string id, string password,
        string safeCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(password))
            return BaiZhuAccountResult.Fail(-1, "账号或密码为空");
        var ptId = NewPtId(id);
        if (!IsNativeAccountSlot(ptId))
            return BaiZhuAccountResult.Fail(-1, "pt_id 超过原生 20 字节槽");
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO account.normal (pt_id, uid, password, safecode, login_time, create_time)
SELECT @pt, @id, @psw, @sc, NOW(), NOW() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM account.normal WHERE BINARY uid = BINARY @id2)";
            command.Parameters.AddWithValue("@pt", ptId);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@psw", password);
            command.Parameters.AddWithValue("@sc", safeCode ?? string.Empty);
            command.Parameters.AddWithValue("@id2", id);
            var rows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return rows == 0
                ? BaiZhuAccountResult.Fail(-1, "账号已存在")
                : new BaiZhuAccountResult(0, "注册成功", string.Empty, "null");
        }
        catch (MySqlException ex)
        {
            return BaiZhuAccountResult.Fail(-1, "ticket database unavailable: " + ex.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return BaiZhuAccountResult.Fail(-1, "ticket database error");
        }
    }

    public async ValueTask<BaiZhuAccountResult> ChangePasswordAsync(string id, string password,
        string safeCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(password))
            return BaiZhuAccountResult.Fail(-1, "参数不全");
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"UPDATE account.normal
SET password = @pw
WHERE BINARY uid = BINARY @id AND BINARY safecode = BINARY @sc";
            command.Parameters.AddWithValue("@pw", password);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@sc", safeCode ?? string.Empty);
            var rows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return rows == 0
                ? BaiZhuAccountResult.Fail(-1, "安全码错误或账号不存在")
                : new BaiZhuAccountResult(0, "修改成功", string.Empty, "null");
        }
        catch (MySqlException ex)
        {
            return BaiZhuAccountResult.Fail(-1, "ticket database unavailable: " + ex.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return BaiZhuAccountResult.Fail(-1, "ticket database error");
        }
    }

    public ValueTask<BaiZhuAccountResult> BindAsync(string machineId, string id,
        string password, string safeCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(machineId) || string.IsNullOrEmpty(id) ||
            string.IsNullOrEmpty(password))
            return ValueTask.FromResult(BaiZhuAccountResult.Fail(-1, "参数不全"));
        return ValueTask.FromResult(
            BaiZhuAccountResult.Fail(-110, "游客绑定未配置（fail-closed）"));
    }

    public static IBaiZhuAccountStore Create(string? connectionString) =>
        string.IsNullOrWhiteSpace(connectionString)
            ? new RejectingBaiZhuAccountStore()
            : new MySqlBaiZhuAccountStore(connectionString);

    private static async Task<string> IssueTicketAsync(MySqlConnection connection, string ptId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var ticket = Md5Hex(ptId + "/" + now + "/" + Environment.ProcessId);
        await using (var cleanup = connection.CreateCommand())
        {
            cleanup.CommandText =
                "DELETE FROM account.ticket WHERE BINARY pt_id = BINARY @pt AND create_time < @exp";
            cleanup.Parameters.AddWithValue("@pt", ptId);
            cleanup.Parameters.AddWithValue("@exp", now - 300);
            await cleanup.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var insert = connection.CreateCommand();
        insert.CommandText = @"INSERT INTO account.ticket (ticket, pt_id, create_time)
VALUES (@t, @pt, @ct)
ON DUPLICATE KEY UPDATE pt_id = @pt2, create_time = @ct2";
        insert.Parameters.AddWithValue("@t", ticket);
        insert.Parameters.AddWithValue("@pt", ptId);
        insert.Parameters.AddWithValue("@pt2", ptId);
        insert.Parameters.AddWithValue("@ct", now);
        insert.Parameters.AddWithValue("@ct2", now);
        var rows = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0 ? ticket : string.Empty;
    }

    private static async Task<string> ReadLastServerAsync(MySqlConnection connection, string ptId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT servername FROM account.login WHERE BINARY pt_id = BINARY @pt LIMIT 1";
            command.Parameters.AddWithValue("@pt", ptId);
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var name = Convert.ToString(value);
            return string.IsNullOrEmpty(name) ? "null" : name;
        }
        catch (MySqlException)
        {
            return "null";
        }
    }

    private static string NewPtId(string id)
    {
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        return "pt" + Md5Hex(id + ":/" + stamp)[8..24];
    }

    private static bool IsNativeAccountSlot(string ptId)
    {
        if (string.IsNullOrEmpty(ptId)) return false;
        try
        {
            return Encoding.ASCII.GetByteCount(ptId) <= 20;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static string Md5Hex(string value)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
