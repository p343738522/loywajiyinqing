// Pins the 元宝寄售 WRITE family fail-closed until a real session + YBDB 310..323
// codec + debit/credit executor exists.
//
// Native split (image, not invented):
//   CM 1252/1253/1256/1257  LIVE reads via manager [[0x7D6ABC]]
//   CM 1251 / 1254 / 1255 / 1258  Q1 Drop — manager write / 0x6CB94C / dormant
//                                 NativeYbDealPurchaseStateMachine (CM 1254)
//   CM 1350..1364  workers 0x6F09C4..0x6F120C forward req 0x136..0x146
//                  (YBDB 310..326) through [0x7D5D98]/sub_637A00
//   sub_633EB0 / sub_63426C  CM 1254 buyer-debit / seller-credit, not 1350..1364
//
// This tool must stay green while those write hooks are missing. Do not invent
// a 310..323 codec, a SellItems INSERT, or a shop-protocol substitute.

using System.Text.RegularExpressions;
using GameSvr;
using GameSvr.Services;
using SystemModule;

var root = AuditRepoRoot.Resolve();
var asserts = 0;

CheckRuntimeFailClosed();
CheckNativeEvidenceTables();
CheckOperateWiring(root);
CheckCm1251StillDropped(root);
CheckForwardNeverQueues(root);
CheckNoYbDb310Codec(root);
CheckNoSellItemsMutation(root);
CheckPurchaseMachineStaysDormant(root);
CheckYbDealDialogShowModeClosed(root);
CheckBatchCancelIsInboundOnly(root);
CheckSellAuditUncalled(root);
CheckMallChannelNotShared(root);

Console.WriteLine(
    $"NativeYbConsignmentWriteCheck PASS still-closed " +
    $"cm=1251,1350-1364 ybdb310=missing session=off " +
    $"debit=dormant dialog=RejectUnsupportedNativeApi asserts={asserts}");
return;

void CheckRuntimeFailClosed()
{
    Assert(!NativeYbConsignmentWrite.WriteFeatureEnabled,
        "busy-gate feature switch dword[0x7D7038]+3&0x80 must default OFF");
    Equal((byte)0, NativeYbConsignmentWrite.ForwardWrite(null, 0x136),
        "sub_6D3694 forward must not queue without [0x7D5D98] session");
    Equal((byte)0, NativeYbConsignmentWrite.ForwardWrite(null, 0x143),
        "YBDB 323 forward must not queue");
    Assert(!NativeYbConsignmentWrite.ForwardReclaim(null, 1, true),
        "reclaim req 0x13F must not queue");
    Assert(!NativeYbConsignmentWrite.ForwardReclaim(null, 1, false),
        "reclaim req 0x140 must not queue");
    Assert(!NativeMallSubmitChannel.IsActive,
        "shopMgr [[0x7D5D98]] channel must stay inactive");
    Assert(!NativeMallSubmitChannel.TrySubmit(null, 0x136),
        "mall submit must not enqueue consignment YBDB 310");
}

void CheckNativeEvidenceTables()
{
    Assert(NativeCmQ1FailClosed.All.TryGetValue(Grobal2.CM_1251, out var cm1251),
        "CM 1251 missing from Q1 fail-closed table");
    Equal(0x006DA66Au, cm1251.HandlerVa, "CM 1251 leaf VA");
    Equal(0x006E7E0Cu, cm1251.CalleeVa, "CM 1251 worker VA");
    Require(cm1251.Blocker, "0x6F9594", "CM 1251 yuanbao-open gate missing");
    Require(cm1251.Blocker, "0x6CB94C", "CM 1251 0x6CB94C worker missing");

    var expected = new (int Cm, uint Leaf, uint Worker)[]
    {
        (1350, 0x006DAC8E, 0x006F09C4),
        (1351, 0x006DACA7, 0x006F0A98),
        (1352, 0x006DACD0, 0x006F0B84),
        (1353, 0x006DACE4, 0x006F0E0C),
        (1354, 0x006DACF6, 0x006F0E64),
        (1355, 0x006DAD08, 0x006F0EBC),
        (1356, 0x006DAD21, 0x006F0F28),
        (1357, 0x006DAD33, 0x006F0F80),
        (1358, 0x006DAD45, 0x006F0FD8),
        (1359, 0x006DAD57, 0x006F1028),
        (1360, 0x006DAD6B, 0x006F1028),
        (1361, 0x006DAD7F, 0x006F110C),
        (1362, 0x006DAD91, 0x006F1164),
        (1363, 0x006DADA3, 0x006F11BC),
        (1364, 0x006DADB5, 0x006F120C),
    };
    foreach (var row in expected)
    {
        Assert(NativeCmQ2FailClosed.All.TryGetValue(row.Cm, out var entry),
            $"CM {row.Cm} missing from Q2 fail-closed table");
        Equal(row.Leaf, entry.HandlerVa, $"CM {row.Cm} leaf VA");
        Equal(row.Worker, entry.CalleeVa, $"CM {row.Cm} worker VA");
        Require(entry.Blocker, "[0x7D5D98]",
            $"CM {row.Cm} must name manager [0x7D5D98]");
    }

    Equal(1250, Grobal2.SM_1250, "SM_1250 ident");
    Equal(1257, Grobal2.SM_1257, "SM_1257 reclaim unavailable ident");
    Equal(1263, Grobal2.SM_1263, "SM_1263 ident");
}

void CheckOperateWiring(string repoRoot)
{
    var operate = Read(repoRoot, "GameSvr", "Players", "TPlayObject.Message.cs");
    var writeCall = operate.IndexOf("TryHandleYbConsignWriteCm(ProcessMsg)",
        StringComparison.Ordinal);
    var q2Call = operate.IndexOf("TryHandleNativeCmQ2(ProcessMsg)",
        StringComparison.Ordinal);
    Assert(writeCall >= 0, "Operate is missing TryHandleYbConsignWriteCm");
    Assert(q2Call > writeCall,
        "TryHandleYbConsignWriteCm must run before TryHandleNativeCmQ2");

    var write = Read(repoRoot, "GameSvr", "Players", "TPlayObject.YbConsignWrite.cs");
    for (var cm = 1350; cm <= 1364; cm++)
        Require(write, $"case Grobal2.CM_{cm}:",
            $"YbConsignWrite missing CM {cm} arm");
}

void CheckCm1251StillDropped(string repoRoot)
{
    var q1 = Read(repoRoot, "GameSvr", "Players",
        "TPlayObject.NativeCmProtocol_Q1.cs");
    var start = q1.IndexOf("private void ClientNativeYbConsignmentGated()",
        StringComparison.Ordinal);
    Assert(start >= 0, "CM 1251 handler missing");
    var end = q1.IndexOf("private void ClientNativeYbConsignmentAccept()",
        start, StringComparison.Ordinal);
    Assert(end > start, "CM 1251 handler boundary missing");
    var body = StripCommentsAndLiterals(q1[start..end]);
    Require(body, "NativeCmQ1FailClosed.Drop(Grobal2.CM_1251",
        "CM 1251 is no longer fail-closed");
    Reject(body, "SendDefMessage", "CM 1251 emitted a substitute SM");
    Reject(body, "YbDbClient", "CM 1251 opened YbDbClient");
    Reject(body, "NativeYbDealPurchaseStateMachine",
        "CM 1251 invoked the dormant purchase machine");
}

void CheckForwardNeverQueues(string repoRoot)
{
    var write = Read(repoRoot, "GameSvr", "Players", "TPlayObject.YbConsignWrite.cs");
    Require(write,
        "internal static byte ForwardWrite(TPlayObject self, int reqIdent) => 0;",
        "ForwardWrite is no longer a hard closed no-op");
    Require(write,
        "internal static bool ForwardReclaim(TPlayObject self, int nRecog, bool cl) => false;",
        "ForwardReclaim is no longer a hard closed no-op");
    Require(write, "WriteFeatureEnabled;",
        "write feature switch field missing");
}

void CheckNoYbDb310Codec(string repoRoot)
{
    var packetDir = Path.Combine(repoRoot, "SystemModule", "Packet");
    foreach (var path in Directory.EnumerateFiles(packetDir, "YbDb*.cs"))
    {
        var source = File.ReadAllText(path);
        foreach (var ident in Enumerable.Range(310, 14))
        {
            RejectWord(source, ident,
                $"{Path.GetFileName(path)} exposed YBDB request {ident}");
            RejectWord(source, ident + 1000,
                $"{Path.GetFileName(path)} exposed YBDB response {ident + 1000}");
        }
    }

    var ybDb = Read(repoRoot, "GameSvr", "Services", "YbDbClient.cs");
    foreach (var ident in Enumerable.Range(310, 18))
    {
        RejectWord(ybDb, ident,
            $"YbDbClient partially exposed YBDB request {ident}");
        RejectWord(ybDb, ident + 1000,
            $"YbDbClient partially exposed YBDB response {ident + 1000}");
    }
    foreach (var forbidden in new[]
             {
                 "RequestYbDeal", "ProcessYbDeal", "SellItems", "YBDealHis",
                 "ForwardWrite", "CM_1350"
             })
    {
        Reject(ybDb, forbidden,
            "YbDbClient gained a consignment-write authority: " + forbidden);
    }

    Assert(!File.Exists(Path.Combine(packetDir, "YbDbConsignmentWriteProtocol.cs")),
        "invented YbDbConsignmentWriteProtocol.cs");
}

void CheckNoSellItemsMutation(string repoRoot)
{
    var writeFiles = new[]
    {
        Path.Combine(repoRoot, "GameSvr", "Players", "TPlayObject.YbConsignWrite.cs"),
        Path.Combine(repoRoot, "GameSvr", "Services", "NativeYbConsignmentBatchCancel.cs"),
        Path.Combine(repoRoot, "GameSvr", "Services", "NativeYbConsignmentSellAudit.cs"),
        Path.Combine(repoRoot, "GameSvr", "Services", "NativeYbConsignmentQuery.cs"),
    };
    foreach (var path in writeFiles)
    {
        var source = File.ReadAllText(path);
        foreach (var verb in new[] { "INSERT", "UPDATE", "DELETE", "Replace into" })
        {
            if (Regex.IsMatch(source, verb + @"\s+(into\s+)?(gamedata\.)?(SellItems|ybDealHis)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                throw new InvalidOperationException(
                    Path.GetFileName(path) + " invented " + verb + " on SellItems/ybDealHis");
            }
        }
    }

    var query = Read(repoRoot, "GameSvr", "Services", "NativeYbConsignmentQuery.cs");
    Require(query, "Select Count(*) from gamedata.SellItems",
        "read-side SellItems count SQL missing");
    Require(query, "class EmptyStore",
        "read-side EmptyStore missing");
}

void CheckPurchaseMachineStaysDormant(string repoRoot)
{
    var helper = Read(repoRoot, "GameSvr", "Services",
        "NativeYbDealPurchaseStateMachine.cs");
    Require(helper, "It does not model the separate CM 1350..1363 / YBDB 310..323 surface.",
        "purchase machine claimed the 1350..1364 / YBDB 310 surface");
    var helperCode = StripCommentsAndLiterals(helper);
    foreach (var forbidden in new[]
             {
                 "m_nGameGold", "NativeYuanbaoManager", "YbDbClient",
                 "MySql", "Socket", "Task.Run", "Retry"
             })
    {
        if (helperCode.IndexOf(forbidden, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException(
                "dormant purchase helper gained runtime authority: " + forbidden);
    }

    var write = StripCommentsAndLiterals(
        Read(repoRoot, "GameSvr", "Players", "TPlayObject.YbConsignWrite.cs"));
    Reject(write, "NativeYbDealPurchaseStateMachine",
        "YbConsignWrite invoked the dormant CM 1254 machine");
    Reject(write, "YbDbClient",
        "YbConsignWrite opened YbDbClient without a 310 codec");
}

void CheckYbDealDialogShowModeClosed(string repoRoot)
{
    var bridge = Read(repoRoot, "GameSvr", "ScriptSystem", "PasEngine",
        "PasApiBridge.cs");
    var marker = "case \"ybdealdialogshowmode\":";
    var start = bridge.IndexOf(marker, StringComparison.Ordinal);
    Assert(start >= 0, "YBDealDialogShowMode PAS case missing");
    var end = bridge.IndexOf("case \"notifyclientopenupdateclothes\":", start,
        StringComparison.Ordinal);
    Assert(end > start, "YBDealDialogShowMode PAS case boundary missing");
    var dispatch = bridge[start..end];
    Require(dispatch, "RejectUnsupportedNativeApi(out result)",
        "YBDealDialogShowMode production entry opened");
    Reject(dispatch, "NativeYbDealPurchaseStateMachine",
        "PAS invoked dormant YBDeal state machine");
    Reject(dispatch, "TryHandleYbConsignWriteCm",
        "PAS routed YBDealDialogShowMode onto CM 1350..1364");
}

void CheckBatchCancelIsInboundOnly(string repoRoot)
{
    var cancel = Read(repoRoot, "GameSvr", "Services",
        "NativeYbConsignmentBatchCancel.cs");
    Require(cancel, "internal static void HandleCallback(",
        "batch-cancel inbound callback missing");
    Reject(cancel, "TrySubmit", "batch-cancel gained an initiator");
    Reject(cancel, "ForwardWrite", "batch-cancel gained a YBDB forward");
    Reject(cancel, "INSERT", "batch-cancel invented SQL");

    var write = Read(repoRoot, "GameSvr", "Players", "TPlayObject.YbConsignWrite.cs");
    Require(write, "HandleBatchCancelCallback",
        "write surface lost the inbound batch-cancel ack hook");
}

void CheckSellAuditUncalled(string repoRoot)
{
    var audit = Read(repoRoot, "GameSvr", "Services",
        "NativeYbConsignmentSellAudit.cs");
    Require(audit, "0x0063CA4C", "sell-audit native VA missing");
    Require(audit, "GameDataLogAction = 0x2D", "sell-audit log action missing");

    var hits = 0;
    foreach (var path in Directory.EnumerateFiles(
                 Path.Combine(repoRoot, "GameSvr"), "*.cs", SearchOption.AllDirectories))
    {
        if (path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var source = StripCommentsAndLiterals(File.ReadAllText(path));
        if (source.Contains("WriteConsignmentSellLog", StringComparison.Ordinal))
            hits++;
    }

    Equal(1, hits,
        "sell-audit logger must remain declaration-only until a sell executor exists");
}

void CheckMallChannelNotShared(string repoRoot)
{
    var write = StripCommentsAndLiterals(
        Read(repoRoot, "GameSvr", "Players", "TPlayObject.YbConsignWrite.cs"));
    Reject(write, "NativeMallSubmitChannel",
        "consignment write piggybacked the mall channel without a 310 codec");
}

static string Read(string repoRoot, params string[] parts) =>
    File.ReadAllText(Path.Combine(new[] { repoRoot }.Concat(parts).ToArray()));

static void Require(string source, string value, string message)
{
    if (source.IndexOf(value, StringComparison.Ordinal) < 0)
        throw new InvalidOperationException(message);
}

static void Reject(string source, string value, string message)
{
    if (source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
        throw new InvalidOperationException(message);
}

static void RejectWord(string source, int value, string message)
{
    if (Regex.IsMatch(source, $@"\b{value}\b", RegexOptions.CultureInvariant))
        throw new InvalidOperationException(message);
}

void Equal<T>(T expected, T actual, string message)
{
    asserts++;
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException(
            $"{message}: expected {expected}, actual {actual}");
}

void Assert(bool condition, string message)
{
    asserts++;
    if (!condition) throw new InvalidOperationException(message);
}

static string StripCommentsAndLiterals(string source)
{
    var output = new System.Text.StringBuilder(source.Length);
    for (var i = 0; i < source.Length; i++)
    {
        var c = source[i];
        var next = i + 1 < source.Length ? source[i + 1] : '\0';
        if (c == '/' && next == '/')
        {
            while (i < source.Length && source[i] != '\n') i++;
            output.Append('\n');
            continue;
        }
        if (c == '/' && next == '*')
        {
            i += 2;
            while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/'))
                i++;
            i++;
            continue;
        }
        if (c == '@' && next == '"')
        {
            i += 2;
            while (i < source.Length)
            {
                if (source[i] == '"')
                {
                    if (i + 1 < source.Length && source[i + 1] == '"')
                    {
                        i += 2;
                        continue;
                    }
                    break;
                }
                i++;
            }
            continue;
        }
        if (c == '"' || c == '\'')
        {
            var quote = c;
            i++;
            while (i < source.Length && source[i] != quote)
            {
                if (source[i] == '\\') i++;
                i++;
            }
            continue;
        }
        output.Append(c);
    }
    return output.ToString();
}
