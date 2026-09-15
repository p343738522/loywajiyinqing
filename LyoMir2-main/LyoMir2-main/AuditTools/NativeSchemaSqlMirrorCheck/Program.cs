// NativeSchemaSqlMirrorCheck — NativeSchemaProvisioner CREATE TABLE vs scripts/sql.
// Does not execute MySQL. Source-only. account_ticket.sql is C#-only and is not
// required to appear in the provisioner.
using System.Text.RegularExpressions;

var failures = new List<string>();
var asserts = 0;
var root = AuditRepoRoot.Resolve();

var provisioner = File.ReadAllText(Path.Combine(root,
    "DBSvr", "Core", "NativeSchemaProvisioner.cs"));
var mir3Sql = File.ReadAllText(Path.Combine(root, "scripts", "sql", "mir3_user.sql"));
var guildSql = File.ReadAllText(Path.Combine(root, "scripts", "sql", "guild.sql"));
var gamedataSql = File.ReadAllText(Path.Combine(root, "scripts", "sql", "gamedata.sql"));

var provTables = ParseCreateTables(string.Join("\n", ExtractExecSql(provisioner)));
var sqlTables = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
Merge(sqlTables, ParseCreateTables(StripSqlComments(mir3Sql)));
Merge(sqlTables, ParseCreateTables(StripSqlComments(guildSql)));
Merge(sqlTables, ParseCreateTables(StripSqlComments(gamedataSql)));

var required = new (string Prov, string Sql)[]
{
    ("user_index", "mir3.user_index"),
    ("user_data", "mir3.user_data"),
    ("hero_index", "mir3.hero_index"),
    ("hero_data", "mir3.hero_data"),
    ("user_storage", "mir3.user_storage"),
    ("awardplayers", "mir3.awardplayers"),
    ("HallOfFame", "mir3.HallOfFame"),
    ("dominatorpet", "mir3.dominatorpet"),
    ("gamedata.ZongpaiBase", "gamedata.ZongpaiBase"),
    ("gamedata.ZongpaiRole", "gamedata.ZongpaiRole"),
    ("gamedata.ZongpaiMember", "gamedata.ZongpaiMember"),
    ("gamedata.mirparams", "gamedata.mirparams"),
    ("gamedata.TransferAreaScoreSendRecord", "gamedata.TransferAreaScoreSendRecord"),
    ("gamedata.TransferAreaScore", "gamedata.TransferAreaScore"),
    ("Guild.Castle", "Guild.Castle"),
    ("Guild.guild_list", "Guild.guild_list"),
    ("Guild.guild_rank", "Guild.guild_rank"),
    ("Guild.guild_user", "Guild.guild_user"),
    ("Guild.guild_relation", "Guild.guild_relation"),
    ("Guild.guild_log", "Guild.guild_log"),
};

var allowedSqlExtras = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
{
    ["mir3.hero_data"] = new[] { "NameLayout" },
    ["mir3.hero_index"] = new[] { "lvChangeTime" },
    ["gamedata.HallOfFame"] = Array.Empty<string>(),
};

foreach (var (provName, sqlName) in required)
{
    True(provTables.ContainsKey(provName), $"provisioner missing {provName}");
    True(sqlTables.ContainsKey(sqlName), $"scripts/sql missing {sqlName}");
    if (!provTables.TryGetValue(provName, out var provCols)
        || !sqlTables.TryGetValue(sqlName, out var sqlCols))
        continue;
    foreach (var col in provCols)
        True(sqlCols.Contains(col),
            $"{sqlName} missing provisioner column {col}");
    allowedSqlExtras.TryGetValue(sqlName, out var extras);
    extras ??= Array.Empty<string>();
    foreach (var col in sqlCols)
    {
        if (provCols.Contains(col)) continue;
        True(extras.Contains(col, StringComparer.OrdinalIgnoreCase),
            $"{sqlName} unexpected extra column {col}");
    }
}

True(sqlTables.ContainsKey("gamedata.HallOfFame"),
    "gamedata.sql must keep HallOfFame for MySqlNativeHallOfFameService");
if (provTables.TryGetValue("HallOfFame", out var hof)
    && sqlTables.TryGetValue("gamedata.HallOfFame", out var gHof))
{
    foreach (var col in hof)
        True(gHof.Contains(col),
            $"gamedata.HallOfFame missing provisioner HallOfFame column {col}");
}

True(mir3Sql.Contains("CREATE TABLE IF NOT EXISTS mir3_backup.user_index LIKE",
        StringComparison.OrdinalIgnoreCase)
     && mir3Sql.Contains("CREATE TABLE IF NOT EXISTS mir3_backup.hero_data LIKE",
         StringComparison.OrdinalIgnoreCase)
     && mir3Sql.Contains("CREATE TABLE IF NOT EXISTS mir3_backup.dominatorpet LIKE",
         StringComparison.OrdinalIgnoreCase),
    "mir3_user.sql backup LIKE clones missing");

True(provisioner.Contains("Create database if not exists gamedata",
        StringComparison.OrdinalIgnoreCase)
     && gamedataSql.Contains("CREATE DATABASE IF NOT EXISTS gamedata",
         StringComparison.OrdinalIgnoreCase)
     && provisioner.Contains("CREATE DATABASE IF NOT EXISTS Guild",
         StringComparison.OrdinalIgnoreCase)
     && guildSql.Contains("CREATE DATABASE IF NOT EXISTS Guild",
         StringComparison.OrdinalIgnoreCase),
    "gamedata/Guild CREATE DATABASE must stay aligned");

if (failures.Count != 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}

Console.WriteLine(
    $"NativeSchemaSqlMirrorCheck PASS asserts={asserts} "
    + $"required={required.Length} sqlTables={sqlTables.Count} "
    + $"provCreates={provTables.Count}");
return 0;

void True(bool ok, string what)
{
    asserts++;
    if (!ok) failures.Add("FAIL " + what);
}

static void Merge(Dictionary<string, HashSet<string>> dest,
    Dictionary<string, HashSet<string>> src)
{
    foreach (var kv in src)
        dest[kv.Key] = kv.Value;
}

static List<string> ExtractExecSql(string cs)
{
    var list = new List<string>();
    const string marker = "Exec(\"";
    var i = 0;
    while (true)
    {
        var start = cs.IndexOf(marker, i, StringComparison.Ordinal);
        if (start < 0) break;
        start += marker.Length;
        var end = cs.IndexOf("\");", start, StringComparison.Ordinal);
        if (end < 0) break;
        list.Add(cs[start..end].Replace("\\\"", "\""));
        i = end + 3;
    }
    return list;
}

static string StripSqlComments(string sql)
{
    var lines = sql.Replace("\r\n", "\n").Split('\n');
    return string.Join("\n",
        lines.Select(l =>
        {
            var t = l.TrimStart();
            return t.StartsWith("--", StringComparison.Ordinal) ? string.Empty : l;
        }));
}

static Dictionary<string, HashSet<string>> ParseCreateTables(string sql)
{
    var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    var rx = new Regex(
        @"CREATE\s+TABLE\s+IF\s+NOT\s+EXISTS\s+([`\w.]+)\s*\(",
        RegexOptions.IgnoreCase);
    foreach (Match m in rx.Matches(sql))
    {
        var name = m.Groups[1].Value.Trim('`');
        var open = m.Index + m.Length - 1;
        var close = FindMatchingParen(sql, open);
        if (close < 0) continue;
        var body = sql.Substring(open + 1, close - open - 1);
        result[name] = ParseColumns(body);
    }
    return result;
}

static HashSet<string> ParseColumns(string body)
{
    var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var depth = 0;
    var start = 0;
    for (var i = 0; i <= body.Length; i++)
    {
        var c = i < body.Length ? body[i] : ',';
        if (c == '(') depth++;
        else if (c == ')') depth--;
        if (c != ',' || depth != 0) continue;
        var piece = body[start..i].Trim();
        start = i + 1;
        if (piece.Length == 0) continue;
        var first = piece.Split((char[])null,
            StringSplitOptions.RemoveEmptyEntries)[0];
        if (IsConstraint(first)) continue;
        cols.Add(first.Trim('`', '"'));
    }
    return cols;
}

static bool IsConstraint(string first)
{
    return first.Equals("PRIMARY", StringComparison.OrdinalIgnoreCase)
           || first.Equals("UNIQUE", StringComparison.OrdinalIgnoreCase)
           || first.Equals("KEY", StringComparison.OrdinalIgnoreCase)
           || first.Equals("INDEX", StringComparison.OrdinalIgnoreCase)
           || first.Equals("INDEX", StringComparison.OrdinalIgnoreCase)
           || first.Equals("CONSTRAINT", StringComparison.OrdinalIgnoreCase);
}

static int FindMatchingParen(string s, int open)
{
    var depth = 0;
    for (var i = open; i < s.Length; i++)
    {
        if (s[i] == '(') depth++;
        else if (s[i] == ')')
        {
            depth--;
            if (depth == 0) return i;
        }
    }
    return -1;
}
