// Pins SM 4230 (0x1086), the third send of UserLogon sub_6F05D8.
//
// Native UserLogon (sub_6B1D64) @0x6B23C6 calls sub_6F05D8, which emits:
//   SM 888  via [obj+0x250]
//   SM 889  via [obj+0x254]
//   SM 4230 via [obj+0x254] Recog=Self, Param=count, Tag=Series=0,
//           Len=count*22. Empty list still sends (0x6F06D8).
// The builder already existed; the leftover was the missing UserLogon call.
using System.Reflection;
using System.Text;
using GameSvr;
using SystemModule;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
PrepareRuntimeConfig();
InitializeRuntime();

CheckIdent();
CheckEmptyListStillSends();
CheckStartPointRecord();
CheckRangeFallbackAndSkip();
CheckPolygonBoundingBox();
CheckWiring();

Console.WriteLine(
    "PASS NativeSafeZoneLogon sm=4230(0x1086) recog=self param=count " +
    "record=22B{shortstring14+align+x+y+range} empty-list=send " +
    "userlogon=after-889-before-fixedcoord");
return 0;

static void CheckIdent()
{
    Equal(4230, Grobal2.SM_SAFE_ZONE_INFO, "SM_SAFE_ZONE_INFO decimal");
    Equal(0x1086, Grobal2.SM_SAFE_ZONE_INFO, "SM_SAFE_ZONE_INFO native 0x1086");
}

static void CheckEmptyListStillSends()
{
    ResetLists();
    var player = NewProbe();
    InvokeSend(player);
    Equal(1, player.Frames.Count, "empty list still sends");
    CheckHeader(player.Frames[0].Header, player.ObjectId, 0, "empty");
    Equal(0, player.Frames[0].Body.Length, "empty body length");
}

static void CheckStartPointRecord()
{
    ResetLists();
    M2Share.g_Config.nSafeZoneSize = 10;
    M2Share.StartPointList.Add(new TStartPoint
    {
        m_sMapName = "3",
        m_nCurrX = 330,
        m_nCurrY = 330,
        m_nRange = 12
    });
    var player = NewProbe();
    InvokeSend(player);
    Equal(1, player.Frames.Count, "one start point is one record");
    CheckHeader(player.Frames[0].Header, player.ObjectId, 1, "start-point");
    var body = player.Frames[0].Body;
    Equal(22, body.Length, "record size is native 0x16");
    Equal(1, body[0], "ShortString length of '3'");
    Equal((byte)'3', body[1], "map name payload");
    for (var i = 2; i < 16; i++)
        Equal((byte)0, body[i], "ShortString/align pad " + i);
    Equal((short)330, BitConverter.ToInt16(body, 16), "X");
    Equal((short)330, BitConverter.ToInt16(body, 18), "Y");
    Equal((short)12, BitConverter.ToInt16(body, 20), "per-entry range");
}

static void CheckRangeFallbackAndSkip()
{
    ResetLists();
    M2Share.g_Config.nSafeZoneSize = 10;
    M2Share.StartPointList.Add(new TStartPoint
    {
        m_sMapName = string.Empty,
        m_nCurrX = 1,
        m_nCurrY = 1,
        m_nRange = 99
    });
    M2Share.StartPointList.Add(null);
    M2Share.StartPointList.Add(new TStartPoint
    {
        m_sMapName = "D715",
        m_nCurrX = 40,
        m_nCurrY = 50,
        m_nRange = 0
    });
    var player = NewProbe();
    InvokeSend(player);
    Equal(1, player.Frames.Count, "empty/null start points skipped");
    CheckHeader(player.Frames[0].Header, player.ObjectId, 1, "fallback");
    var body = player.Frames[0].Body;
    Equal(22, body.Length, "one surviving record");
    Equal(4, body[0], "ShortString length of D715");
    Equal((short)40, BitConverter.ToInt16(body, 16), "fallback X");
    Equal((short)50, BitConverter.ToInt16(body, 18), "fallback Y");
    Equal((short)10, BitConverter.ToInt16(body, 20),
        "range 0 falls back to nSafeZoneSize");
}

static void CheckPolygonBoundingBox()
{
    ResetLists();
    var area = new TSafeZoneArea { MapName = "3" };
    area.Points.Add((0, 0));
    area.Points.Add((10, 0));
    area.Points.Add((10, 10));
    M2Share.SafeZoneList.Add(area);
    var skipped = new TSafeZoneArea { MapName = "skip" };
    skipped.Points.Add((1, 1));
    skipped.Points.Add((2, 2));
    M2Share.SafeZoneList.Add(skipped);
    var player = NewProbe();
    InvokeSend(player);
    Equal(1, player.Frames.Count, "polygon with 3+ points is one record");
    CheckHeader(player.Frames[0].Header, player.ObjectId, 1, "polygon");
    var body = player.Frames[0].Body;
    Equal(22, body.Length, "polygon record size");
    Equal((short)5, BitConverter.ToInt16(body, 16), "polygon center X");
    Equal((short)5, BitConverter.ToInt16(body, 18), "polygon center Y");
    Equal((short)5, BitConverter.ToInt16(body, 20), "polygon half-range");
}

static void CheckWiring()
{
    var root = AuditRepoRoot.Resolve();
    var baseSource = File.ReadAllText(Path.Combine(root, "GameSvr", "Players",
        "TPlayObject.Base.cs"));
    var userLogon = Between(baseSource, "public void UserLogon()",
        "private bool WeaptonMakeLuck()");
    Contains(userLogon, "SendSafeZoneInfo();", "UserLogon calls the leftover send");
    Before(userLogon, "SendNativeLoginNow();", "SendSafeZoneInfo();",
        "SM 4230 after SM 889");
    Before(userLogon, "SendSafeZoneInfo();", "ReplayNativeFixedCoordOnLogon();",
        "SM 4230 before 定位石 replay");
    Equal(1, Count(userLogon, "SendSafeZoneInfo();"),
        "UserLogon emits SM 4230 once");

    var message = File.ReadAllText(Path.Combine(root, "GameSvr", "Players",
        "TPlayObject.Message.cs"));
    var rmLogon = Between(message, "case Grobal2.RM_LOGON:",
        "case Grobal2.RM_NATIVE_REVIVE_MESSAGE:");
    NotContains(rmLogon, "SendSafeZoneInfo",
        "RM_LOGON does not steal sub_6F05D8's SM 4230");
}

static void CheckHeader(ClientPacket header, int recog, ushort param, string label)
{
    Assert(header != null, label + " header present");
    Equal((ushort)Grobal2.SM_SAFE_ZONE_INFO, header.Ident, label + " ident");
    Equal(recog, header.Recog, label + " Recog=Self");
    Equal(param, header.Param, label + " Param=count");
    Equal((ushort)0, header.Tag, label + " Tag");
    Equal((ushort)0, header.Series, label + " Series");
}

static void InvokeSend(TPlayObject player)
{
    var method = typeof(TPlayObject).GetMethod("SendSafeZoneInfo",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SendSafeZoneInfo missing");
    method.Invoke(player, null);
}

static SafeZoneProbe NewProbe()
{
    var player = new SafeZoneProbe();
    Assert(player.ObjectId != 0, "ObjectId assigned");
    return player;
}

static void ResetLists()
{
    M2Share.StartPointList = new List<TStartPoint>();
    M2Share.SafeZoneList = new List<TSafeZoneArea>();
}

static void InitializeRuntime()
{
    M2Share.g_Config = new GameSvrConfig { nSafeZoneSize = 10 };
    M2Share.UserEngine = new UserEngine();
    M2Share.ObjectManager = new ObjectManager();
    M2Share.MapManager = new MapManager();
    M2Share.ProcessMsgCriticalSection = new object();
    ResetLists();
}

static void PrepareRuntimeConfig()
{
    var runtimeDirectory = AppContext.BaseDirectory;
    File.WriteAllText(Path.Combine(runtimeDirectory, "!Setup.txt"),
        "[Server]" + Environment.NewLine);
    File.WriteAllText(Path.Combine(runtimeDirectory, "Command.conf"),
        "[Command]" + Environment.NewLine);
    var shareDirectory = Path.Combine(Path.GetFullPath(
        Path.Combine(runtimeDirectory, "..")), "Share");
    Directory.CreateDirectory(shareDirectory);
    File.WriteAllText(Path.Combine(shareDirectory, "PlayerUpgradeExp.ini"),
        "[PlayerLevelExp]" + Environment.NewLine);
}

static string Between(string source, string startText, string endText)
{
    var start = source.IndexOf(startText, StringComparison.Ordinal);
    Assert(start >= 0, startText + " start anchor");
    var end = source.IndexOf(endText, start + startText.Length, StringComparison.Ordinal);
    Assert(end > start, endText + " end anchor");
    return source[start..end];
}

static void Before(string source, string first, string second, string label)
{
    var firstIndex = source.IndexOf(first, StringComparison.Ordinal);
    var secondIndex = source.IndexOf(second, StringComparison.Ordinal);
    Assert(firstIndex >= 0 && secondIndex > firstIndex, label);
}

static int Count(string source, string value)
{
    var count = 0;
    for (var index = source.IndexOf(value, StringComparison.Ordinal); index >= 0;
         index = source.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        count++;
    return count;
}

static void Contains(string source, string value, string label) =>
    Assert(source.Contains(value, StringComparison.Ordinal), label);

static void NotContains(string source, string unexpected, string label) =>
    Assert(!source.Contains(unexpected, StringComparison.Ordinal), label);

static void Equal<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException(
            $"{label}: expected={expected}, actual={actual}");
}

static void Assert(bool condition, string label)
{
    if (!condition)
        throw new InvalidOperationException(label);
}

sealed class SafeZoneProbe : TPlayObject
{
    internal readonly List<(ClientPacket Header, byte[] Body)> Frames = new();

    internal override void SendSocket(ClientPacket defMsg, byte[] rawBody)
    {
        Frames.Add((defMsg, rawBody ?? Array.Empty<byte>()));
    }

    internal override void SendSocket(ClientPacket defMsg, string message)
    {
        Frames.Add((defMsg, Array.Empty<byte>()));
    }
}
