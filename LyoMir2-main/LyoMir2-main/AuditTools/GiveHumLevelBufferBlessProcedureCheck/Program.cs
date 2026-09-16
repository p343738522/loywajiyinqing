using GameSvr;
using GameSvr.PasEngine;
using SystemModule;

PrepareRuntimeConfig();
M2Share.g_Config = new GameSvrConfig();
M2Share.ObjectManager = new ObjectManager();
M2Share.LogMsgCriticalSection = new object();
M2Share.ProcessMsgCriticalSection = new object();
M2Share.LogStringList = new System.Collections.ArrayList();

var player = new TPlayObject { m_sCharName = "bless-proc" };
var bridge = new PasApiBridge { CurrentPlayer = player };
var empty = new List<PasValue>();
var twoInts = new List<PasValue> { PasValue.FromInt(0), PasValue.FromInt(60) };

Assert(!bridge.CallPlayerMethod("GiveHumLevelBuffer", empty),
    "GiveHumLevelBuffer zero-arg procedure was dispatched");
Assert(!bridge.CallPlayerMethod("GiveHumLevelBuffer", twoInts),
    "GiveHumLevelBuffer two-arg procedure was dispatched");
Assert(!bridge.CallPlayerFunc("GiveHumLevelBuffer", twoInts, out var humFunc),
    "GiveHumLevelBuffer function face was invented");
Equal(PasValueType.Nil, humFunc.Type,
    "rejected GiveHumLevelBuffer function result must remain Nil");

Assert(!bridge.CallPlayerMethod("ShowCurrentBless", empty),
    "ShowCurrentBless procedure was dispatched");
Assert(!bridge.CallPlayerMethod("ShowCurrentBless", twoInts),
    "ShowCurrentBless procedure accepted arguments");
Assert(bridge.CallPlayerFunc("ShowCurrentBless", empty, out _),
    "ShowCurrentBless function face was unwired");

Equal(0, player.m_MsgList.Count,
    "rejected procedure faces emitted a client message");
Equal(0, M2Share.LogStringList.Count,
    "rejected procedure faces emitted a game log");

Equal(0x006F28B0, NativeGiveHumLevelBufferPlanner.WrapperAddress,
    "GiveHumLevelBuffer wrapper address");
Equal(0x00746870, NativeGiveHumLevelBufferPlanner.ExecutorAddress,
    "GiveHumLevelBuffer executor sub_746870");
Equal(-9, NativeGiveHumLevelBufferPlanner.NoHeroCode,
    "GiveHumLevelBuffer no-hero code");
Equal(NativeGiveHumLevelBufferOutcome.SelfApply,
    NativeGiveHumLevelBufferPlanner.Plan(0, false),
    "GiveHumLevelBuffer target 0 is self");

var root = FindRepositoryRoot();
var bridgeSource = File.ReadAllText(Path.Combine(root, "GameSvr", "ScriptSystem",
    "PasEngine", "PasApiBridge.cs"));
var ladderSource = File.ReadAllText(Path.Combine(root, "GameSvr", "Services",
    "NativeRegisteredBodyScriptApiLadders.cs"));

Require(ladderSource, "function GiveHumLevelBuffer(target,",
    "native GiveHumLevelBuffer compiler signature must remain a function");
Reject(ladderSource, "procedure GiveHumLevelBuffer",
    "invented GiveHumLevelBuffer procedure signature");

var methodStart = bridgeSource.IndexOf("public bool CallPlayerMethod(",
    StringComparison.Ordinal);
var funcStart = bridgeSource.IndexOf("public bool CallPlayerFunc(",
    StringComparison.Ordinal);
Assert(methodStart >= 0 && funcStart > methodStart,
    "CallPlayerMethod / CallPlayerFunc boundaries missing");

var methodSource = bridgeSource.Substring(methodStart, funcStart - methodStart);
var funcSource = bridgeSource.Substring(funcStart);

var humMethodAt = methodSource.IndexOf("case \"givehumlevelbuffer\":",
    StringComparison.Ordinal);
Assert(humMethodAt >= 0, "GiveHumLevelBuffer procedure case missing");
var humMethodEnd = methodSource.IndexOf("case \"", humMethodAt + 1,
    StringComparison.Ordinal);
Assert(humMethodEnd > humMethodAt, "GiveHumLevelBuffer procedure boundary missing");
var humMethod = methodSource.Substring(humMethodAt, humMethodEnd - humMethodAt);
Require(humMethod, "RejectUnsupportedNativeApi()",
    "GiveHumLevelBuffer procedure must remain fail-closed");
Reject(humMethod, "AddTimedAbility",
    "GiveHumLevelBuffer procedure must not invent a timed-ability substitute");
Reject(humMethod, "SetPlayerVar",
    "GiveHumLevelBuffer procedure must not invent a V-variable substitute");
Equal(0, Count(funcSource, "case \"givehumlevelbuffer\":"),
    "GiveHumLevelBuffer function face must stay unwired");

var blessMethodAt = methodSource.IndexOf("case \"showcurrentbless\":",
    StringComparison.Ordinal);
Assert(blessMethodAt >= 0, "ShowCurrentBless procedure case missing");
var blessMethodEnd = methodSource.IndexOf("case \"", blessMethodAt + 1,
    StringComparison.Ordinal);
Assert(blessMethodEnd > blessMethodAt, "ShowCurrentBless procedure boundary missing");
var blessMethod = methodSource.Substring(blessMethodAt,
    blessMethodEnd - blessMethodAt);
Require(blessMethod, "RejectUnsupportedNativeApi()",
    "ShowCurrentBless procedure must remain fail-closed");
Reject(blessMethod, "ShowCurrentNativeBless",
    "ShowCurrentBless procedure must not forward without procedure-form RTTI");

var blessFuncAt = funcSource.IndexOf("case \"showcurrentbless\":",
    StringComparison.Ordinal);
Assert(blessFuncAt >= 0, "ShowCurrentBless function case missing");
var blessFuncEnd = funcSource.IndexOf("case \"", blessFuncAt + 1,
    StringComparison.Ordinal);
Assert(blessFuncEnd > blessFuncAt, "ShowCurrentBless function boundary missing");
var blessFunc = funcSource.Substring(blessFuncAt, blessFuncEnd - blessFuncAt);
Require(blessFunc, "ShowCurrentNativeBless()",
    "ShowCurrentBless function must keep the live mutator");
Reject(blessFunc, "RejectUnsupportedNativeApi",
    "ShowCurrentBless function was closed");

Console.WriteLine(
    "PASS GiveHumLevelBuffer=procedure-closed/function-unwired " +
    "native=function-sub_6F28B0 ShowCurrentBless=procedure-closed/function-mutator");
return;

static void PrepareRuntimeConfig()
{
    var runtimeDirectory = AppContext.BaseDirectory;
    File.WriteAllText(Path.Combine(runtimeDirectory, "!Setup.txt"),
        "[Server]" + Environment.NewLine);
    File.WriteAllText(Path.Combine(runtimeDirectory, "String.ini"),
        "[String]" + Environment.NewLine);
    File.WriteAllText(Path.Combine(runtimeDirectory, "Command.conf"),
        "[Command]" + Environment.NewLine);
    var shareDirectory = Path.Combine(Path.GetFullPath(
        Path.Combine(runtimeDirectory, "..")), "Share");
    Directory.CreateDirectory(shareDirectory);
    File.WriteAllText(Path.Combine(shareDirectory, "PlayerUpgradeExp.ini"),
        "[PlayerLevelExp]" + Environment.NewLine);
    File.WriteAllText(Path.Combine(shareDirectory, "ServerData.ini"),
        "[Integer]" + Environment.NewLine);
}

static string FindRepositoryRoot()
{
    foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
    {
        var directory = new DirectoryInfo(start);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName,
                    "GameSvr", "GameSvr.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }
    }
    throw new DirectoryNotFoundException("GameSvr repository root not found");
}

static int Count(string source, string value)
{
    var count = 0;
    for (var index = 0; (index = source.IndexOf(value, index,
             StringComparison.Ordinal)) >= 0; index += value.Length)
        count++;
    return count;
}

static void Require(string source, string value, string message) =>
    Assert(source.Contains(value, StringComparison.Ordinal), message);

static void Reject(string source, string value, string message) =>
    Assert(!source.Contains(value, StringComparison.Ordinal), message);

static void Equal<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException(
            $"{message}: expected={expected} actual={actual}");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
