using System.Collections;
using GameSvr;
using GameSvr.PasEngine;
using SystemModule;

// NativeValidScriptFuncGateCheck
//
// Pins the sub_6B8CC4 validScriptFunc.txt Find gate:
//   • Find is called from TPlayObject.TryNativeScriptInteractionFunction
//     (CM_MERCHANTDLGSELECT non-@ client-invoked function names).
//   • A name not in the list is rejected (false / no-op).
//   • A name in the list passes.
//   • CallPlayerFunc of normal TPlayer APIs still works without the list.
// NativeGmSystemCommandsCheck already covers Reload / TStringList.Find shape.

PrepareRuntimeConfig();
M2Share.g_Config = new GameSvrConfig();
M2Share.ObjectManager = new ObjectManager();
M2Share.UserEngine = new UserEngine();
M2Share.ProcessMsgCriticalSection = new object();
M2Share.LogMsgCriticalSection = new object();
M2Share.LogStringList = new ArrayList();

var root = FindRepositoryRoot();
VerifySourceContract(root);

var validFuncRoot = Path.Combine(Path.GetTempPath(),
    "native-valid-func-gate-" + Guid.NewGuid().ToString("N"));
try
{
    var configDirectory = Path.Combine(validFuncRoot, "Config");
    Directory.CreateDirectory(configDirectory);
    var validFuncFile = Path.Combine(configDirectory, "validScriptFunc.txt");
    File.WriteAllText(validFuncFile, "ListedFunc\r\n", HUtil32.GbkEncoding);
    Equal(1, NativeValidScriptFunctionRegistry.Reload(validFuncRoot),
        "gate list reload count");

    var player = new TPlayObject();
    Equal(true, player.TryNativeScriptInteractionFunction("ListedFunc"),
        "listed name passes the interaction gate");
    Equal(true, player.TryNativeScriptInteractionFunction("listedfunc"),
        "listed name is case-insensitive");
    Equal(false, player.TryNativeScriptInteractionFunction("NotListed"),
        "unknown name is rejected");
    Equal(false, player.TryNativeScriptInteractionFunction("IsMale"),
        "CallPlayerFunc API IsMale is rejected when not in the list");

    player.m_boGhost = true;
    Equal(false, player.TryNativeScriptInteractionFunction("ListedFunc"),
        "ghost is a silent reject");
    player.m_boGhost = false;
    player.m_boDeath = true;
    Equal(false, player.TryNativeScriptInteractionFunction("ListedFunc"),
        "death is a silent reject");
    player.m_boDeath = false;
    player.m_boDealing = true;
    Equal(false, player.TryNativeScriptInteractionFunction("ListedFunc"),
        "dealing is a silent reject");
    player.m_boDealing = false;
    Equal(false, player.TryNativeScriptInteractionFunction(""),
        "empty text is a silent reject");
    Equal(false, player.TryNativeScriptInteractionFunction(null),
        "nil text is a silent reject");

    var bridge = new PasApiBridge { CurrentPlayer = player };
    player.m_btGender = PlayGender.Man;
    Equal(true, bridge.CallPlayerFunc("IsMale", new List<PasValue>(), out var male)
                && male.Type == PasValueType.Boolean && male.AsBool(),
        "CallPlayerFunc IsMale still works without being in validScriptFunc.txt");
    player.m_btGender = PlayGender.WoMan;
    Equal(true, bridge.CallPlayerFunc("IsFemale", new List<PasValue>(), out var female)
                && female.Type == PasValueType.Boolean && female.AsBool(),
        "CallPlayerFunc IsFemale still works without the list");
}
finally
{
    if (Directory.Exists(validFuncRoot))
        Directory.Delete(validFuncRoot, true);
}

Console.WriteLine("PASS NativeValidScriptFuncGateCheck: Find is the interaction gate; CallPlayerFunc stays ungated");

static void VerifySourceContract(string repositoryRoot)
{
    var gate = File.ReadAllText(Path.Combine(repositoryRoot, "GameSvr",
        "Players", "TPlayObject.NativeScriptInteraction.cs"));
    var operate = File.ReadAllText(Path.Combine(repositoryRoot, "GameSvr",
        "Players", "TPlayObject.Operate.cs"));
    var board = File.ReadAllText(Path.Combine(repositoryRoot, "GameSvr",
        "Players", "TPlayObject.TaskBoardScript.cs"));
    var bridge = File.ReadAllText(Path.Combine(repositoryRoot, "GameSvr",
        "ScriptSystem", "PasEngine", "PasApiBridge.cs"));
    var npcGoto = File.ReadAllText(Path.Combine(repositoryRoot, "GameSvr",
        "Npcs", "NormNpc.GotoLable.cs"));
    var gmCheck = File.ReadAllText(Path.Combine(repositoryRoot, "AuditTools",
        "NativeGmSystemCommandsCheck", "Program.cs"));

    Require(gate, "NativeValidScriptFunctionRegistry.Find(",
        "interaction gate does not call Find");
    Require(gate, "PasApiBridge.TraceUnknownPasName(\"ScriptInteraction\"",
        "unknown interaction name is not throttled through TraceUnknownPasName");
    Require(Slice(gate, "internal bool TryNativeScriptInteractionFunction",
            "namespace"),
        "m_boGhost || m_boDeath || m_boDealing",
        "interaction gate dropped the sub_6B8CC4 prologue");

    var dlgSelect = Slice(operate, "private void ClientMerchantDlgSelect",
        "private NormNpc GetMerchantQueryNpc");
    Require(dlgSelect, "TryNativeScriptInteractionFunction(selectMsg)",
        "CM_MERCHANTDLGSELECT does not call the interaction gate");
    Require(dlgSelect, "selectMsg[0] != '@'",
        "CM_MERCHANTDLGSELECT gates @labels with validScriptFunc.txt");
    Require(dlgSelect, "npc.UserSelect(this, selectMsg)",
        "CM_MERCHANTDLGSELECT dropped the @label UserSelect path");

    Reject(Slice(board, "private void NativeTaskBoardTextCommandRun",
            "M2Share.PasEngine?.TryCallHelperQuestLabel"),
        "NativeValidScriptFunctionRegistry.Find(",
        "HelperQuest board label leg of sub_6B8CC4 was Find-gated");
    Require(board, "M2Share.PasEngine?.TryCallHelperQuestLabel(this, text)",
        "HelperQuest board label leg no longer dispatches the client text");

    var callPlayerFunc = Slice(bridge, "public bool CallPlayerFunc",
        "public bool CallNpcMethod");
    Reject(callPlayerFunc, "NativeValidScriptFunctionRegistry",
        "CallPlayerFunc was wired as a global validScriptFunc filter");

    Reject(npcGoto, "NativeValidScriptFunctionRegistry.Find(",
        "NPC GotoLabel was wired as the validScriptFunc gate");

    Require(gmCheck, "NativeValidScriptFunctionRegistry.Reload(",
        "NativeGmSystemCommandsCheck no longer covers Reload");
    Require(gmCheck, "NativeValidScriptFunctionRegistry.Find(",
        "NativeGmSystemCommandsCheck no longer covers Find");
}

static string Slice(string source, string startMarker, string endMarker)
{
    var start = source.IndexOf(startMarker, StringComparison.Ordinal);
    if (start < 0)
        return string.Empty;
    var end = source.IndexOf(endMarker, start + startMarker.Length,
        StringComparison.Ordinal);
    if (end < 0)
        return source.Substring(start);
    return source.Substring(start, end - start);
}

static void Require(string source, string marker, string message)
{
    if (!source.Contains(marker, StringComparison.Ordinal))
        throw new InvalidOperationException(message + ": " + marker);
}

static void Reject(string source, string marker, string message)
{
    if (source.Contains(marker, StringComparison.Ordinal))
        throw new InvalidOperationException(message + ": " + marker);
}

static void Equal<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException(
            $"{message}: expected {expected}, actual {actual}");
}

static string FindRepositoryRoot()
{
    foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
    {
        var directory = new DirectoryInfo(start);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GameSvr",
                    "GameSvr.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }
    }
    throw new DirectoryNotFoundException("GameSvr/GameSvr.csproj was not found");
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
