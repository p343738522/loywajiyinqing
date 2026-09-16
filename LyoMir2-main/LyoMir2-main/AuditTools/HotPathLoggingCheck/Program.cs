using System.Text.RegularExpressions;

var root = FindRepositoryRoot();

var gameApp = Read("GameSvr/GameApp.cs");
var gameServer = Read("GameSvr/GameServer.cs");
var userEngine = Read("GameSvr/UsrSystem/UsrEngn.cs");
var gateService = Read("GameSvr/GameGate/GateService.cs");
var gateManager = Read("GameSvr/GameGate/GateManager.cs");
var mallProtocol = Read("GameSvr/Players/TPlayObject.Mall.cs");
var playerOperate = Read("GameSvr/Players/TPlayObject.Operate.cs");
var mallManager = Read("GameSvr/Mall/MallManager.cs");
var gameGate = Read("GameGate-CS/Core/GateServer.cs");
var gameGateForm = Read("GameGate-CS/Forms/MainForm.cs");
var loginGate = Read("LoginGate/Program.cs");
var dbLogin = Read("DBSvr/Services/LoginSocService.cs");
var dbGame = Read("DBSvr/Services/GameSocService.cs");
var dbUser = Read("DBSvr/Services/UserSocService.cs");

RequireConditional(gameGate, "GAMEGATE_PACKET_TRACE", "Trace");
RequireConditional(gateService, "GAMESVR_PACKET_TRACE", "PacketTrace");
RequireConditional(gateManager, "GAMESVR_PACKET_TRACE", "PacketTrace");
RequireConditional(dbLogin, "DBSVR_PROTOCOL_TRACE", "ProtocolTrace");
RequireConditional(dbGame, "DBSVR_PROTOCOL_TRACE", "FileLog");
RequireConditional(dbUser, "DBSVR_PROTOCOL_TRACE", "Log");

var traceSymbols = new[]
{
    "GAMEGATE_PACKET_TRACE",
    "LOGINGATE_PACKET_TRACE",
    "GAMESVR_PACKET_TRACE",
    "GAMESVR_DIAGNOSTICS",
    "DBSVR_PROTOCOL_TRACE"
};
foreach (var project in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
             .Where(path => !IsGeneratedPath(path)))
{
    var projectText = File.ReadAllText(project);
    foreach (var symbol in traceSymbols)
    {
        if (Regex.IsMatch(projectText,
                $@"<DefineConstants>[^<]*\b{Regex.Escape(symbol)}\b[^<]*</DefineConstants>",
                RegexOptions.IgnoreCase))
        {
            Fail($"packet trace symbol enabled by project: {Relative(project)} -> {symbol}");
        }
    }
}

AssertAbsent(gameApp, "gateservice_diag.log", "GameSvr startup diagnostic file");
AssertAbsent(gameGateForm, "gategate.log", "GameGate GUI diagnostic file");
AssertAbsent(gameServer, "[Stats]", "periodic M2 statistics output");
AssertAbsent(userEngine, "[OpenMake]", "per-character creation output");
AssertAbsent(userEngine, "[OpenBindGate]", "per-character gate binding output");
AssertAbsent(mallProtocol, "MainOutMessage", "per-query mall output");
AssertAbsent(playerOperate, "MainOutMessage(\"Fail\")", "per-packet user-set failure output");
AssertAbsent(playerOperate, "MainOutMessage(format(\"OK:", "per-packet user-set success output");
AssertAbsent(mallManager, "LogPurchase(player, mallItem, quantity, totalPrice);\r\n                M2Share.MainOutMessage",
    "per-purchase console output");
AssertAbsent(mallManager, "LogPurchase(player, mallItem, quantity, totalPrice);\n                M2Share.MainOutMessage",
    "per-purchase console output");
AssertAbsent(dbUser, "DBShare.MainOutMessage($\"[AwardPlayer] PTID=", "per-character award output");
AssertAbsent(gameGate, "_speed.OnViolation +=", "per-violation GUI output");
AssertAbsent(gameGate, "var hex = string.Join", "eager packet hex formatting");

foreach (var category in new[] { "SEND", "DOWN", "CRYPT", "HWID", "RECOVERY", "TURNPACK", "SPEED", "OPEN", "DISCONNECT" })
{
    AssertAbsent(gameGate, $"Log(\"{category}\"", $"unconditional GameGate {category} output");
}

// LoginGate no longer has a Trace method to guard: Program.cs is now a 95-line entry point
// and the accept/read loops live in LoginGate/Core. Requiring a [Conditional] Trace in a file
// that no longer owns a socket also made the five AssertAbsent probes below vacuous -- they
// were scanning the wrong file and would have passed no matter what the loops logged. Pin the
// stronger property instead: the three socket services emit nothing at all, per session or
// per packet, so there is no output left to guard with a symbol.
foreach (var socketService in new[]
         {
             "LoginGate/Core/ClientSelectionService.cs",
             "LoginGate/Core/NativeDbServerService.cs",
             "LoginGate/Core/PigCompatibilityService.cs"
         })
{
    var source = Read(socketService);
    foreach (var emitter in new[]
             {
                 "Console.Write", "Console.Error", "WriteLog(", "LogReceived",
                 "File.AppendAllText", "Debug.WriteLine", "Trace.WriteLine"
             })
    {
        AssertAbsent(source, emitter,
            $"unconditional LoginGate socket-loop output in {socketService}: {emitter}");
    }
}
AssertAbsent(loginGate, "Console.Write", "LoginGate entry-point console output");
AssertAbsent(loginGate, "File.AppendAllText", "LoginGate entry-point file append");
// Lifecycle logger stays off the socket path: start, 2001-port note, ticket source, stop.
var loginGateServer = Read("LoginGate/Core/LoginGateServer.cs");
var lifecycleLogs = Regex.Matches(loginGateServer, @"WriteLog\(""INFO""").Count;
if (lifecycleLogs != 4)
    Fail($"LoginGate lifecycle logging changed: expected 4 start/note/ticket/stop lines, found {lifecycleLogs}");

var frameProtocol = Read("GameGate-CS/Core/FrameProtocol.cs");
Require(frameProtocol,
    "private readonly List<(byte flags, byte cmd, uint ident, byte[] payload)> _frames = new();",
    "GameGate FrameParser list reuse missing");
var tiger = Read("GameGate-CS/Core/TigerCodec.cs");
Require(tiger, "private static readonly string[] RotatedKeys",
    "TigerCodec rotated-key cache missing");
Require(gameGate, "EnsureScratch(ref gsBodyScratch, gsBodyLen)",
    "GameGate RelayUp gsBody scratch reuse missing");
Require(gameGate, "EnsureScratch(ref delayedPayloadScratch, delayedLen)",
    "GameGate delayed-payload scratch reuse missing");
Require(gameGate, "TigerCodec.Decode(accBuf.AsSpan(0, lhIdx)",
    "GameGate Tiger decode still allocates an ASCII string");
Require(gameGate, "var pongBytes = new byte[12];",
    "GameGate PING pong buffer missing");
Require(gameGate, "EmptyLoginPromptBody",
    "GameGate SM_LOGIN 44-byte body reuse missing");
Require(gameGate, "PayloadLength = length",
    "GameGate CreateGameDataPacket scratch payload length missing");
var sharedHub = Read("GameGate-CS/Core/SharedBackendHub.cs");
Require(sharedHub, "dbFrames.Clear();",
    "GameGate DB dispatcher list reuse missing");
Require(sharedHub, "gameFrames.Clear();",
    "GameGate M2 dispatcher list reuse missing");
Require(Read("SystemModule/Packet/GameGateServerFrameParser.cs"),
    "List<GameGateServerFrame> frames, out string error)",
    "GameGate server parser reusable-list overload missing");
Require(Read("SystemModule/Packet/DbServerGatewayFrameParser.cs"),
    "List<DbServerGatewayFrame> frames, out string error)",
    "DB gateway parser reusable-list overload missing");
Require(Read("SystemModule/Packet/InternalPacket77.cs"), "public int PayloadLength = -1;",
    "InternalPacket77 scratch payload length missing");
Require(Read("SystemModule/MobileCodec.cs"),
    "int bodyLength, uint seq, ushort marker)",
    "MobileCodec WriteFrame body-slice overload missing");
var loginClient = Read("LoginGate/Core/ClientSelectionService.cs");
Require(loginClient, "frames.Clear();",
    "LoginGate client read-loop list reuse missing");
Require(loginClient, "TryEncodeClientFrame(frame, encodeScratch,",
    "LoginGate client encode scratch missing");
Require(Read("LoginGate/Core/NativeDbServerService.cs"), "frames.Clear();",
    "LoginGate DBServer read-loop list reuse missing");
Require(Read("LoginGate/Core/NativeDbServerService.cs"), "connection.EncodeScratch",
    "LoginGate DBServer encode scratch missing");

var pasBridge = Read("GameSvr/ScriptSystem/PasEngine/PasApiBridge.cs");
Require(pasBridge, "private static void LogHotPathOnce(",
    "PAS hot-path log throttle helper missing");
Require(pasBridge, "private static List<TBaseObject> RentActorScanList(",
    "PAS GetMapRageHuman/GetMapMonster list pool missing");
Require(pasBridge, "private static void ReturnActorScanList(",
    "PAS actor-scan list return missing");
Require(Read("GameSvr/Castle/UserCastle.cs"), "private static List<TBaseObject> RentRageHumanList(",
    "castle GetMapRageHuman list pool missing");
Require(Read("GameSvr/Spells/MagicManager.cs"), "private static List<TBaseObject> RentSpellScanList(",
    "MagicManager spell-scan list pool missing");
Require(pasBridge, "scriptdestroyitem:",
    "ScriptDestroyItem is not throttled per item name");
Require(pasBridge, "setplayerlevel:",
    "SetPlayerLevel is not throttled per character");
var robotPlay = Read("GameSvr/RobotPlay/RobotPlayObject.cs");
Require(robotPlay, "private static void LogAttackOnce(",
    "robot attack-catch log throttle helper missing");
Require(Read("GameSvr/RobotPlay/RobotPlayObject.Attack.cs"), "LogAttackOnce(",
    "robot Attack.cs catch path is not throttled");
Require(robotPlay, "private static List<TBaseObject> RentRangeScanList(",
    "robot GetRangeTargetCount list pool missing");
Require(robotPlay, "private static void ReturnRangeScanList(",
    "robot range-scan list return missing");
Require(robotPlay, "List<TBaseObject> BaseObjectList = RentRangeScanList();",
    "robot GetRangeTargetCount does not rent from the scan pool");
AssertAbsent(robotPlay, "IList<TBaseObject> BaseObjectList = new List<TBaseObject>();",
    "robot GetRangeTargetCount per-call list allocation");

var animalScan = Read("GameSvr/Actors/TAnimalObject.cs");
Require(animalScan, "private static Stack<List<TBaseObject>> _monsterScanPool;",
    "monster attack/around-scan ThreadStatic stack missing");
Require(animalScan, "protected static List<TBaseObject> RentMonsterScanList(",
    "monster attack/around-scan list pool missing");
Require(animalScan, "protected static void ReturnMonsterScanList(",
    "monster attack/around-scan list return missing");
Require(animalScan, "List<TBaseObject> BaseObjectList = RentMonsterScanList();",
    "HitMagAttackTarget does not rent from the monster scan pool");
AssertAbsent(animalScan, "IList<TBaseObject> BaseObjectList = new List<TBaseObject>();",
    "HitMagAttackTarget per-call list allocation");

var ronObject = Read("GameSvr/Monsters/Monster/RonObject.cs");
Require(ronObject, "List<TBaseObject> xTargetList = RentMonsterScanList();",
    "RonObject AroundAttack does not rent from the monster scan pool");
Require(ronObject, "ReturnMonsterScanList(xTargetList);",
    "RonObject AroundAttack does not return the monster scan list");
AssertAbsent(ronObject, "IList<TBaseObject> xTargetList = new List<TBaseObject>();",
    "RonObject AroundAttack per-call list allocation");

var scultureMonster = Read("GameSvr/Monsters/Monster/ScultureMonster.cs");
Require(scultureMonster, "List<TBaseObject> List10 = RentMonsterScanList();",
    "ScultureMonster MeltStoneAll does not rent from the monster scan pool");
Require(scultureMonster, "ReturnMonsterScanList(List10);",
    "ScultureMonster MeltStoneAll does not return the monster scan list");
AssertAbsent(scultureMonster, "IList<TBaseObject> List10 = new List<TBaseObject>();",
    "ScultureMonster MeltStoneAll per-call list allocation");

foreach (var monsterFile in Directory.EnumerateFiles(
             Path.Combine(root, "GameSvr", "Monsters"), "*.cs", SearchOption.AllDirectories))
{
    if (IsGeneratedPath(monsterFile)) continue;
    foreach (var line in File.ReadAllLines(monsterFile))
    {
        if (!line.Contains("new List<TBaseObject>", StringComparison.Ordinal))
            continue;
        if (line.Contains("BBList =", StringComparison.Ordinal)
            || line.Contains("m_SlaveObjectList =", StringComparison.Ordinal)
            || line.Contains("CertList =", StringComparison.Ordinal)
            || line.Contains("return new List<TBaseObject>(", StringComparison.Ordinal))
            continue;
        Fail($"monster Attack/Around/Run still allocates a scan list: {Relative(monsterFile)}");
    }
}

Require(Read("GameSvr/Monsters/Monster/BeeQueen.cs"), "BBList = new List<TBaseObject>();",
    "BeeQueen constructor-owned BBList must remain");
Require(Read("GameSvr/Monsters/Monster/SpiderHouseMonster.cs"), "BBList = new List<TBaseObject>();",
    "SpiderHouseMonster constructor-owned BBList must remain");
Require(Read("GameSvr/Monsters/Monster/BoneKingMonster.cs"), "m_SlaveObjectList = new List<TBaseObject>();",
    "BoneKingMonster constructor-owned slave list must remain");
Require(Read("GameSvr/Monsters/Monster/ScultureKingMonster.cs"), "m_SlaveObjectList = new List<TBaseObject>();",
    "ScultureKingMonster constructor-owned slave list must remain");

var fireBurn = Read("GameSvr/Events/FireBurnEvent.cs");
Require(fireBurn, "private static Stack<List<TBaseObject>> _fireScanPool;",
    "FireBurnEvent Run ThreadStatic stack missing");
Require(fireBurn, "private static List<TBaseObject> RentFireScanList(",
    "FireBurnEvent Run list pool missing");
Require(fireBurn, "private static void ReturnFireScanList(",
    "FireBurnEvent Run list return missing");
Require(fireBurn, "IList<TBaseObject> BaseObjectList = RentFireScanList();",
    "FireBurnEvent Run does not rent from the fire scan pool");
AssertAbsent(fireBurn, "IList<TBaseObject> BaseObjectList = new List<TBaseObject>();",
    "FireBurnEvent Run per-call list allocation");

var areaMagic = Read("GameSvr/Actors/TBaseObject.NativeState26Effects.cs");
Require(areaMagic, "private static Stack<List<TBaseObject>> _areaMagicScanPool;",
    "area-magic ThreadStatic stack missing");
Require(areaMagic, "private static List<TBaseObject> RentAreaMagicScanList(",
    "area-magic list pool missing");
Require(areaMagic, "private static void ReturnAreaMagicScanList(",
    "area-magic list return missing");
Require(areaMagic, "var targets = RentAreaMagicScanList();",
    "ApplyNativeAreaMagicEffect does not rent from the area-magic scan pool");
AssertAbsent(areaMagic, "var targets = new List<TBaseObject>();",
    "ApplyNativeAreaMagicEffect per-call list allocation");

var heroUnion = Read("GameSvr/Actors/HeroObject.cs");
Require(heroUnion, "var objects = RentMonsterScanList();",
    "DealNativeUnionMagicAreaHit does not rent from the monster scan pool");
Require(heroUnion, "ReturnMonsterScanList(objects);",
    "DealNativeUnionMagicAreaHit does not return the monster scan list");
AssertAbsent(heroUnion, "var objects = new List<TBaseObject>();",
    "DealNativeUnionMagicAreaHit per-call list allocation");

var yanshenApi = Read("GameSvr/Plugins/YanshenApi.cs");
Require(yanshenApi, "private static Stack<List<TBaseObject>> _pluginScanPool;",
    "YanshenApi plugin-scan ThreadStatic stack missing");
Require(yanshenApi, "private static List<TBaseObject> RentPluginScanList(",
    "YanshenApi plugin-scan list pool missing");
Require(yanshenApi, "private static void ReturnPluginScanList(",
    "YanshenApi plugin-scan list return missing");
Require(yanshenApi, "var areaTargets = NativeCollectAreaTargets(",
    "NativeCollectAreaTargets callers do not capture the rented area list");
Require(yanshenApi, "ReturnPluginScanList(areaTargets);",
    "NativeCollectAreaTargets callers do not return the plugin scan list");
Require(yanshenApi, "var chain = RentPluginScanList();",
    "NativeWalkCell/NativeChainDamage does not rent from the plugin scan pool");
Require(yanshenApi, "var raw = RentPluginScanList();",
    "NativeEnumerateAreaCells/PushEnemyCore does not rent from the plugin scan pool");
Require(yanshenApi, "var cell = RentPluginScanList();",
    "NativeEnumerateAreaCells does not rent the cell list from the plugin scan pool");
AssertAbsent(yanshenApi, "foreach (var t in NativeCollectAreaTargets(",
    "NativeCollectAreaTargets foreach still skips ReturnPluginScanList");
foreach (var line in File.ReadAllLines(Path.Combine(root, "GameSvr", "Plugins", "YanshenApi.cs")))
{
    if (!line.Contains("new List<TBaseObject>", StringComparison.Ordinal))
        continue;
    if (line.Contains("return new List<TBaseObject>(", StringComparison.Ordinal))
        continue;
    Fail($"YanshenApi still allocates a scan list: {line.Trim()}");
}

Require(dbUser, "private static List<TUserInfo> RentUserScratchList(",
    "UserSocService user scratch list pool missing");
Require(dbUser, "private static void ReturnUserScratchList(",
    "UserSocService user scratch list return missing");
Require(dbUser, "private static List<TGateInfo> RentGateScratchList(",
    "UserSocService gate scratch list pool missing");
Require(dbUser, "var targets = RentUserScratchList();",
    "UserSocService DisconnectNativeGateByAddress does not rent targets");
Require(dbUser, "var usersToCleanup = RentUserScratchList();",
    "UserSocService usersToCleanup is not rented");
Require(dbUser, "var gatesToComplete = RentGateScratchList();",
    "UserSocService gatesToComplete is not rented");
AssertAbsent(dbUser, "var targets = new List<TUserInfo>();",
    "UserSocService per-call targets allocation");
AssertAbsent(dbUser, "var usersToCleanup = new List<TUserInfo>();",
    "UserSocService per-call usersToCleanup allocation");
AssertAbsent(dbUser, "var gatesToComplete = new List<TGateInfo>();",
    "UserSocService per-call gatesToComplete allocation");
Require(dbUser, "_gateList = new List<TGateInfo>();",
    "UserSocService constructor-owned gate list must remain");
Require(dbUser, "UserList = new List<TUserInfo>(),",
    "UserSocService constructor-owned session list must remain");
Require(dbUser, "IList<TQuickID> chrList = new List<TQuickID>();",
    "UserSocService select-char lookup list must remain unpooled");
Require(dbUser, "byte[] chrBody = new byte[",
    "UserSocService login/select-char bytes must remain unpooled");

var auditedRoots = new[] { "GameSvr", "DBSvr", "GameGate-CS", "LoginGate" };
var auditedFiles = 0;
foreach (var sourceRoot in auditedRoots)
{
    foreach (var file in Directory.EnumerateFiles(Path.Combine(root, sourceRoot), "*.cs", SearchOption.AllDirectories))
    {
        if (IsGeneratedPath(file) || IsExcludedBusinessWriter(file)) continue;
        auditedFiles++;
        var source = File.ReadAllText(file);
        // What this forbids is open-append-close per event. A single long-lived append stream
        // opened with FileOptions.Asynchronous and fed from a bounded channel is the approved
        // shape, so match on the per-call helper and on synchronous append streams only.
        if (source.Contains("File.AppendAllText", StringComparison.Ordinal)
            || (source.Contains("FileMode.Append", StringComparison.Ordinal)
                && !source.Contains("FileOptions.Asynchronous", StringComparison.Ordinal)))
        {
            Fail($"synchronous append remains outside an approved business writer: {Relative(file)}");
        }
    }
}

Console.WriteLine($"PASS files={auditedFiles} traceSymbols=disabled hotOutputs=removed appendWrites=0");
return;

string Read(string relativePath) => File.ReadAllText(Path.Combine(root,
    relativePath.Replace('/', Path.DirectorySeparatorChar)));

void Require(string source, string value, string message)
{
    if (!source.Contains(value, StringComparison.Ordinal))
        Fail(message);
}

void RequireConditional(string source, string symbol, string method)
{
    var pattern = $@"\[\s*(?:System\.Diagnostics\.)?Conditional\(\""{Regex.Escape(symbol)}\""\)\s*\]\s*" +
                  $@"(?:(?:private|internal|public)\s+)?(?:static\s+)?void\s+{Regex.Escape(method)}\s*\(";
    if (!Regex.IsMatch(source, pattern, RegexOptions.CultureInvariant))
        Fail($"{method} must be compile-time guarded by {symbol}");
}

static void AssertAbsent(string source, string value, string description)
{
    if (source.Contains(value, StringComparison.Ordinal))
        Fail(description + " remains enabled");
}

static bool IsGeneratedPath(string path) =>
    path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
    || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
    || path.Contains($"{Path.DirectorySeparatorChar}staging{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

static bool IsExcludedBusinessWriter(string path)
{
    var normalized = path.Replace('\\', '/');
    return normalized.EndsWith("GameSvr/ScriptSystem/PasEngine/PasApiBridge.cs", StringComparison.OrdinalIgnoreCase)
           || normalized.EndsWith("GameSvr/Players/TPlayObject.Message.cs", StringComparison.OrdinalIgnoreCase)
           // The two GameGate GUI writers reproduce the gateway's own procMsgLog artifacts
           // (网关<date>.log and 聊天<date_hour>.log, both present in the production
           // GateServer/GameGate2/procMsgLog directory). They run on the UI thread off the
           // log channel, and every per-packet category that could make that channel hot --
           // SEND / DOWN / CRYPT / HWID / RECOVERY / TURNPACK / SPEED / OPEN / DISCONNECT --
           // is asserted absent above, so what reaches them is connect-rate at worst.
           || normalized.EndsWith("GameGate-CS/Forms/ClassicMainForm.cs", StringComparison.OrdinalIgnoreCase)
           || normalized.EndsWith("GameGate-CS/Forms/GgAcManagementPages.cs", StringComparison.OrdinalIgnoreCase);
}

string Relative(string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

static string FindRepositoryRoot()
{
    foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
    {
        var directory = new DirectoryInfo(start);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GameSvr", "GameSvr.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }
    }
    throw new DirectoryNotFoundException("Repository root containing GameSvr/GameSvr.csproj was not found.");
}

static void Fail(string message) => throw new InvalidOperationException(message);
