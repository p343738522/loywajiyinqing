using System.Reflection;
using System.Text.RegularExpressions;
using GameSvr;
using GameSvr.PasEngine;
using SystemModule;

PrepareRuntimeConfig();
M2Share.g_Config = new GameSvrConfig();
M2Share.g_Config.boShowPreFixMsg = false;
M2Share.UserEngine = new UserEngine();
M2Share.ObjectManager = new ObjectManager();
M2Share.MapManager = new MapManager();
M2Share.ProcessMsgCriticalSection = new object();
M2Share.ProcessHumanCriticalSection = new object();
M2Share.LogMsgCriticalSection = new object();

var root = FindRepositoryRoot();
AssertNoDedicatedIdent(root);

var player = new TPlayObject
{
    m_boOffLineFlag = true,
    m_boGhost = false,
    m_sCharName = "bzaction-probe"
};
PlayObjectList().Add(player);

var bridge = new PasApiBridge
{
    CurrentPlayer = player,
    CurrentNpc = new NormNpc()
};

const string tips = "bzaction|TIPSBAR|<公告栏\\fcolor~250>恭喜probe召唤了一只BOSS";
const string fade = "bzaction|FADELB|背包空间不足，无法领取物品！";
const string npcText = "NPC SAY reaches SM_SYSMESSAGE";

player.m_MsgList.Clear();
Assert(bridge.CallPlayerMethod("PlayerNotice", Values(tips, 2)),
    "PlayerNotice bzaction|TIPSBAR was not dispatched");
AssertSysMsg(player, tips, 0xFCFF, "PlayerNotice color 2");

player.m_MsgList.Clear();
Assert(bridge.CallPlayerMethod("PlayerNotice", Values(fade, 2)),
    "PlayerNotice bzaction|FADELB was not dispatched");
AssertSysMsg(player, fade, 0xFCFF, "PlayerNotice FADELB");

player.m_MsgList.Clear();
bridge.ServerSay(tips, 2);
AssertSysMsg(player, tips, 0xFCFF, "ServerSay color 2");

player.m_MsgList.Clear();
bridge.ServerSay(tips, 5);
AssertSysMsg(player, tips, 0xDF00, "ServerSay color 5");

player.m_MsgList.Clear();
Assert(bridge.CallStandaloneFunction("ServerSay",
        Values(tips, 5), out _),
    "CallStandaloneFunction ServerSay was not dispatched");
AssertSysMsg(player, tips, 0xDF00, "standalone ServerSay color 5");

player.m_MsgList.Clear();
Assert(bridge.CallNpcMethod("NpcSay", Values(npcText), out _),
    "NpcSay was not dispatched");
var npcMessages = SysMessages(player);
Equal(1, npcMessages.Length, "NpcSay message count");
Assert(npcMessages[0].Buff == npcText, "NpcSay body");
Equal(Grobal2.RM_SYSMESSAGE, npcMessages[0].wIdent, "NpcSay ident");

Console.WriteLine(
    "PASS BaiZhuBzactionSysMsgCheck ident=SM_SYSMESSAGE/100 " +
    "new-ident=none PlayerNotice=0xFCFF ServerSay=0xFCFF/0xDF00 " +
    "NpcSay=RM_SYSMESSAGE body=verbatim");
return;

static void AssertSysMsg(TPlayObject player, string body, int packed, string name)
{
    var messages = SysMessages(player);
    Equal(1, messages.Length, name + " message count");
    Equal(Grobal2.RM_SYSMESSAGE, messages[0].wIdent, name + " ident");
    Assert(messages[0].Buff == body, name + " body");
    Equal(packed & 0xFF, messages[0].nParam1, name + " FColor");
    Equal((packed >> 8) & 0xFF, messages[0].nParam2, name + " BColor");
}

static SendMessage[] SysMessages(TPlayObject player) =>
    player.m_MsgList.Where(message => message.wIdent == Grobal2.RM_SYSMESSAGE)
        .ToArray();

static IList<TPlayObject> PlayObjectList()
{
    var field = typeof(UserEngine).GetField("m_PlayObjectList",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("UserEngine.m_PlayObjectList");
    return (IList<TPlayObject>)field.GetValue(M2Share.UserEngine)
        ?? throw new InvalidOperationException("UserEngine.m_PlayObjectList is null");
}

static void AssertNoDedicatedIdent(string root)
{
    Equal(100, Grobal2.SM_SYSMESSAGE, "SM_SYSMESSAGE ident");
    var grobal = File.ReadAllText(Path.Combine(root, "SystemModule", "Grobal2.cs"));
    foreach (var token in new[] { "SM_TIPSBAR", "SM_BZACTION", "CM_TIPSBAR", "CM_BZACTION" })
    {
        Assert(!grobal.Contains(token, StringComparison.Ordinal),
            "invented ident " + token);
    }

    var patches = File.ReadAllText(Path.Combine(root, "GameSvr", "Plugins",
        "YanshenPangu2Patches.cs"));
    Assert(patches.Contains("Grobal2.RM_SYSMESSAGE", StringComparison.Ordinal),
        "BroadcastServerSay left SM_SYSMESSAGE");
    Assert(!Regex.IsMatch(patches, @"SM_TIPSBAR|SM_BZACTION"),
        "BroadcastServerSay invented a TIPSBAR ident");

    var bridge = File.ReadAllText(Path.Combine(root, "GameSvr", "ScriptSystem",
        "PasEngine", "PasApiBridge.cs"));
    var playerNotice = Slice(bridge, "case \"playernotice\":", "case \"npcsay\":");
    Assert(playerNotice.Contains("Grobal2.RM_SYSMESSAGE", StringComparison.Ordinal),
        "PlayerNotice left SM_SYSMESSAGE");
    Assert(!playerNotice.Contains("SM_TIPSBAR", StringComparison.Ordinal),
        "PlayerNotice invented a TIPSBAR ident");
}

static string Slice(string source, string startMarker, string endMarker)
{
    var start = source.IndexOf(startMarker, StringComparison.Ordinal);
    Assert(start >= 0, "missing marker: " + startMarker);
    var end = source.IndexOf(endMarker, start + startMarker.Length,
        StringComparison.Ordinal);
    Assert(end > start, "missing marker: " + endMarker);
    return source[start..end];
}

static List<PasValue> Values(params object[] values)
{
    var list = new List<PasValue>(values.Length);
    foreach (var value in values)
    {
        list.Add(value switch
        {
            int i => PasValue.FromInt(i),
            string s => PasValue.FromString(s),
            _ => throw new InvalidOperationException("unsupported fixture " + value)
        });
    }
    return list;
}

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
    foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
    {
        var current = new DirectoryInfo(start);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "GameSvr", "GameSvr.csproj")))
                return current.FullName;
            current = current.Parent;
        }
    }
    throw new DirectoryNotFoundException(
        "repository root containing GameSvr/GameSvr.csproj was not found");
}

static void Equal(int expected, int actual, string message)
{
    if (expected != actual)
        throw new InvalidOperationException($"{message}: expected {expected}, actual {actual}");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
