// Pins CM 1084 remaining-lock-seconds CLOSED until THumanRcd+0x48 persistence
// AND SM 0x2733/0x2737 builders both exist.
//
// Native (image, not invented):
//   CM 1084 leaf 0x6D95C9 -> worker sub_6D1AB8
//   mode [self+0xB78]==0  -> send nothing (jbe 0x6D1BE2)
//   mode >0               -> remaining=(0x2BF20-(now-[self+0x740]))/1000
//                            then internal-ext 0x2733(10035)/0x2737(10039)
//   lock mode BYTE loaded at 0x6B0A5F from THumanRcd+0x48 -> [player+0xB78]
//
// This C# port intercepts CM 1084 in TryHandleEquipLockCm (ahead of Q1 Drop)
// and reproduces mode-0 silence. Do not invent remaining-seconds: rec[0x48]
// in NativeHumanDataCodec is HP, and there is no 0x2733/0x2737 builder.

using System.Reflection;
using System.Text.RegularExpressions;
using GameSvr;
using GameSvr.Services;
using SystemModule;

var root = AuditRepoRoot.Resolve();
var asserts = 0;

CheckQ1Evidence();
CheckOperateIntercept(root);
CheckModeZeroSilence(root);
CheckArmedPathStayClosed(root);
CheckHumanRcd48IsHpNotLock(root);
CheckNoLockModePersistence(root);
CheckNoRemainingSecondsBuilders(root);
CheckDefaultDisarmed();

Console.WriteLine(
    "NativeEquipLockRemainingSecondsCheck PASS closed " +
    "cm=1084 mode0=silence persist=THumanRcd+0x48-missing " +
    "sm=0x2733/0x2737-missing asserts=" + asserts);
return;

void CheckQ1Evidence()
{
    Equal(1084, Grobal2.CM_1084, "CM_1084 ident");
    Assert(NativeCmQ1FailClosed.All.TryGetValue(Grobal2.CM_1084, out var entry),
        "CM 1084 missing from Q1 fail-closed table");
    Equal(0x006D95C9u, entry.HandlerVa, "CM 1084 leaf VA");
    Equal(0x006D1AB8u, entry.CalleeVa, "CM 1084 worker VA");
    Equal("装备密码锁计时", entry.Subsystem, "CM 1084 subsystem");
    Require(entry.Blocker, "0x2733", "CM 1084 blocker must name SM 0x2733");
    Require(entry.Blocker, "0x2737", "CM 1084 blocker must name SM 0x2737");
    Require(entry.Blocker, "[self+0xB78]", "CM 1084 blocker must name lock mode");
}

void CheckOperateIntercept(string repoRoot)
{
    var operate = Read(repoRoot, "GameSvr", "Players", "TPlayObject.Message.cs");
    var lockCall = operate.IndexOf("TryHandleEquipLockCm(ProcessMsg)",
        StringComparison.Ordinal);
    var q1Call = operate.IndexOf("TryHandleNativeCmQ1(ProcessMsg)",
        StringComparison.Ordinal);
    Assert(lockCall >= 0, "Operate is missing TryHandleEquipLockCm");
    Assert(q1Call > lockCall,
        "TryHandleEquipLockCm must run before TryHandleNativeCmQ1");

    var handler = Read(repoRoot, "GameSvr", "Players", "TPlayObject.EquipLock.cs");
    Require(handler, "case Grobal2.CM_1084:",
        "TryHandleEquipLockCm missing CM 1084 arm");
    Require(handler, "NativeEquipLockTimer();",
        "CM 1084 must call NativeEquipLockTimer");
}

void CheckModeZeroSilence(string repoRoot)
{
    var handler = Read(repoRoot, "GameSvr", "Players", "TPlayObject.EquipLock.cs");
    var start = handler.IndexOf("private void NativeEquipLockTimer()",
        StringComparison.Ordinal);
    Assert(start >= 0, "NativeEquipLockTimer missing");
    var end = handler.IndexOf("private bool EquipLockConfirmGateProceed",
        start, StringComparison.Ordinal);
    Assert(end > start, "NativeEquipLockTimer boundary missing");
    var body = StripCommentsAndLiterals(handler[start..end]);
    Require(body, "if (_nativeEquipLockMode == 0)",
        "CM 1084 must gate on mode 0");
    Require(body, "return;",
        "mode 0 must return without sending");
    Reject(body, "SendDefMessage",
        "mode-0 timer invented a SendDefMessage");
    Reject(body, "BuildSm",
        "mode-0 timer invented an SM builder call");
}

void CheckArmedPathStayClosed(string repoRoot)
{
    var handler = Read(repoRoot, "GameSvr", "Players", "TPlayObject.EquipLock.cs");
    var start = handler.IndexOf("private void NativeEquipLockTimer()",
        StringComparison.Ordinal);
    var end = handler.IndexOf("private bool EquipLockConfirmGateProceed",
        start, StringComparison.Ordinal);
    var body = StripCommentsAndLiterals(handler[start..end]);
    Require(body, "EquipLockFailClosed(1084, 0,",
        "armed remaining-seconds must fail closed");
    Reject(body, "SendDefMessage",
        "armed remaining-seconds invented SendDefMessage(0x2737/0xFFDB)");
    Reject(body, "BuildSm689",
        "armed remaining-seconds invented BuildSm689");
    Reject(body, "BuildSm2733",
        "armed remaining-seconds invented BuildSm2733");
    Reject(body, "BuildSm2737",
        "armed remaining-seconds invented BuildSm2737");
}

void CheckHumanRcd48IsHpNotLock(string repoRoot)
{
    var codec = Read(repoRoot, "DBSvr", "Core", "NativeHumanDataCodec.cs");
    Require(codec,
        "data.Abil.HP = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(0x48, 4));",
        "NativeHumanDataCodec rec[0x48] must stay HP");
    Require(codec,
        "BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(0x48, 4), data.Abil?.HP ?? 0);",
        "NativeHumanDataCodec rec[0x48] save must stay HP");
    Reject(codec, "_nativeEquipLockMode",
        "NativeHumanDataCodec invented lock-mode persistence at rec[0x48]");
    Reject(codec, "EquipLock",
        "NativeHumanDataCodec invented equip-lock fields");

    Equal(0x48, NativeHumanDbCodec.MessageSize,
        "Type1 message header is 0x48, not lock mode");
}

void CheckNoLockModePersistence(string repoRoot)
{
    var load = Read(repoRoot, "GameSvr", "UsrSystem", "UsrEngn.cs");
    var getHum = Slice(load, "private void GetHumData(TPlayObject PlayObject",
        "HumItems = HumanRcd.Data.HumItems");
    Reject(getHum, "_nativeEquipLockMode",
        "GetHumData invented THumanRcd+0x48 -> [player+0xB78]");
    Reject(getHum, "_nativeEquipLockActive",
        "GetHumData invented [player+0x711] lock flag load");
    Reject(getHum, "0xB78",
        "GetHumData invented [player+0xB78] restore");

    var save = Read(repoRoot, "GameSvr", "Players", "TPlayObject.cs");
    var makeSave = Slice(save, "public void MakeSaveRcd(ref THumDataInfo HumanRcd)",
        "HumanRcd.Data.HumItems = HumItems");
    Reject(makeSave, "_nativeEquipLockMode",
        "MakeSaveRcd invented lock-mode save");
    Reject(makeSave, "_nativeEquipLockActive",
        "MakeSaveRcd invented lock-flag save");

    var handler = Read(repoRoot, "GameSvr", "Players", "TPlayObject.EquipLock.cs");
    var assigns = Regex.Matches(StripCommentsAndLiterals(handler),
        @"_nativeEquipLockMode\s*=");
    Equal(1, assigns.Count,
        "lock mode may only default to 0; extra writers invent persistence");
    Require(handler, "private byte _nativeEquipLockMode = 0;",
        "lock mode default must stay disarmed 0");
}

void CheckNoRemainingSecondsBuilders(string repoRoot)
{
    var smDir = Path.Combine(repoRoot, "GameSvr", "Actors");
    foreach (var path in Directory.EnumerateFiles(smDir, "TBaseObject.SmIdent*.cs"))
    {
        var source = File.ReadAllText(path);
        Reject(source, "BuildSm2733",
            Path.GetFileName(path) + " invented BuildSm2733");
        Reject(source, "BuildSm2737",
            Path.GetFileName(path) + " invented BuildSm2737");
        Reject(source, "SM_2733",
            Path.GetFileName(path) + " invented SM_2733");
        Reject(source, "SM_2737",
            Path.GetFileName(path) + " invented SM_2737");
    }

    var grobal = Read(repoRoot, "SystemModule", "Grobal2.cs");
    Reject(grobal, "SM_2733", "Grobal2 invented SM_2733");
    Reject(grobal, "SM_2737", "Grobal2 invented SM_2737");
    Reject(grobal, "public const int SM_10035",
        "Grobal2 invented SM_10035 remaining-seconds ident");
    Reject(grobal, "public const int SM_10039",
        "Grobal2 invented SM_10039 remaining-seconds ident");
}

void CheckDefaultDisarmed()
{
    var mode = typeof(TPlayObject).GetField("_nativeEquipLockMode",
        BindingFlags.Instance | BindingFlags.NonPublic);
    Assert(mode != null && mode.FieldType == typeof(byte),
        "_nativeEquipLockMode byte field missing");
    var active = typeof(TPlayObject).GetField("_nativeEquipLockActive",
        BindingFlags.Instance | BindingFlags.NonPublic);
    Assert(active != null && active.FieldType == typeof(bool),
        "_nativeEquipLockActive bool field missing");
    var timer = typeof(TPlayObject).GetMethod("NativeEquipLockTimer",
        BindingFlags.Instance | BindingFlags.NonPublic);
    Assert(timer != null, "NativeEquipLockTimer missing");
    var intercept = typeof(TPlayObject).GetMethod("TryHandleEquipLockCm",
        BindingFlags.Instance | BindingFlags.NonPublic);
    Assert(intercept != null, "TryHandleEquipLockCm missing");
}

static string Slice(string source, string startMarker, string endMarker)
{
    var start = source.IndexOf(startMarker, StringComparison.Ordinal);
    if (start < 0)
        throw new InvalidOperationException("missing slice start: " + startMarker);
    var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
    if (end < 0)
        throw new InvalidOperationException("missing slice end: " + endMarker);
    return source[start..end];
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
    if (source.IndexOf(value, StringComparison.Ordinal) >= 0)
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
