// Pins CM 4123/4124: invalid-tag SM 4035 is live; Tag==0 / Tag==1&&hero stay
// fail-closed until apply executors 0x747878 / 0x74738C are mapped.
// Do not open CM 4126 consume 0x746F10 from this cluster.
using System.Text.RegularExpressions;

var root = AuditRepoRoot.Resolve();
var q3 = File.ReadAllText(Path.Combine(root, "GameSvr", "Players",
    "TPlayObject.NativeCmProtocol_Q3.cs"));
var ledger = File.ReadAllText(Path.Combine(root, "GameSvr", "Services",
    "NativeCmQ3FailClosed.cs"));
var soulWash = File.ReadAllText(Path.Combine(root, "GameSvr", "Players",
    "TPlayObject.SoulWash.cs"));
var smIdent = File.ReadAllText(Path.Combine(root, "GameSvr", "Actors",
    "TBaseObject.SmIdent_Sm4.cs"));
var grobal = File.ReadAllText(Path.Combine(root, "SystemModule", "Grobal2.cs"));

Require(grobal, "public const int CM_4123", "CM_4123 constant");
Require(grobal, "public const int CM_4124", "CM_4124 constant");
Require(grobal, "public const int SM_4035", "SM_4035 constant");
Require(smIdent, "internal static (ClientPacket Header, byte[] Body) BuildSm4035",
    "SM 4035 builder");
Require(smIdent, "Grobal2.MakeDefaultMsg(Grobal2.SM_4035, recog, 0, tag, 0)",
    "SM 4035 empty-body frame Recog/Param=0/Tag/Series=0");

var cm4123 = MethodBody(q3, "private void Q3Cm4123(int nTag)");
Require(cm4123, "nTag != 0 && !(nTag == 1 && m_HeroObject != null)",
    "CM 4123 invalid-tag gate");
Require(cm4123, "SendDefMessage(Grobal2.SM_4035, 1, 0, 0, 0, string.Empty)",
    "CM 4123 invalid-tag SM 4035 Recog=1");
Require(cm4123, "NativeCmQ3FailClosed.Q3Drop(Grobal2.CM_4123, m_sCharName)",
    "CM 4123 Tag==0 / Tag==1&&hero still Q3Drop");
Reject(cm4123, "0x747878", "CM 4123 Tag==0 invoked unmapped 0x747878");
Reject(cm4123, "0x74738C", "CM 4123 Tag==0 invoked unmapped 0x74738C");

var cm4124 = MethodBody(q3, "private void Q3Cm4124(int nTag)");
Require(cm4124, "nTag != 0 && !(nTag == 1 && m_HeroObject != null)",
    "CM 4124 invalid-tag gate");
Require(cm4124, "SendDefMessage(Grobal2.SM_4035, 0, 0, 0, 0, string.Empty)",
    "CM 4124 invalid-tag SM 4035 Recog=0");
Require(cm4124, "NativeCmQ3FailClosed.Q3Drop(Grobal2.CM_4124, m_sCharName)",
    "CM 4124 Tag==0 / Tag==1&&hero still Q3Drop");
Reject(cm4124, "0x747878", "CM 4124 Tag==0 invoked unmapped 0x747878");
Reject(cm4124, "0x74738C", "CM 4124 Tag==0 invoked unmapped 0x74738C");

Require(ledger, "Add(4123, 0x006DAE32, 0x006BF908", "CM 4123 fail-closed ledger");
Require(ledger, "Add(4124, 0x006DAE53, 0x006BFA88", "CM 4124 fail-closed ledger");
Require(ledger, "0x747878", "ledger still names 0x747878 as the Tag==0 blocker");
Require(ledger, "0x74738C", "ledger still names 0x74738C as the Tag==0 blocker");

Assert(!HasMappedEa(q3, "0x747878") && !HasMappedEa(ledger, "0x747878"),
    "0x747878 acquired an EA const without a Tag==0 wire");
Assert(!HasMappedEa(q3, "0x74738C") && !HasMappedEa(ledger, "0x74738C"),
    "0x74738C acquired an EA const without a Tag==0 wire");
Assert(!GameSvrHasMappedApplyEa(root, "747878"),
    "0x747878 apply executor is now mapped; Tag==0 must be wired, not this pin");
Assert(!GameSvrHasMappedApplyEa(root, "74738C"),
    "0x74738C apply executor is now mapped; Tag==0 must be wired, not this pin");

var apply = Slice(soulWash, "private void SoulWashApply(int nTag, int nRecog)",
    "private bool TryComputeSoulWashBaseFromConfig");
Require(apply, "0x746F10", "CM 4126 consume gate still documented");
Require(apply, "NativeCmTailFailClosed.Drop(Grobal2.CM_4126, m_sCharName)",
    "CM 4126 consume 0x746F10 stayed fail-closed");
Reject(apply, "0x747878", "CM 4126 consume path must not steal 4123 apply");
foreach (var forbidden in new[]
         {
             "DeleteFromBag", "DelBagItem", "WeightChanged", "MakeItemToBag",
             "ItemObjectList.Remove", "m_ItemList.Remove"
         })
{
    Assert(!apply.Contains(forbidden, StringComparison.Ordinal),
        "CM 4126 consume 0x746F10 acquired a bag mutator: " + forbidden);
}

Console.WriteLine(
    "PASS NativeCm4123Tag0FailClosedCheck closed tag0=4123/4124 " +
    "invalid-tag=SM4035 apply=unmapped 0x747878/0x74738C consume=4126-closed");
return;

static bool GameSvrHasMappedApplyEa(string root, string hex)
{
    var pattern = new Regex(@"=\s*0x0*" + hex + @"\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    var gamesvr = Path.Combine(root, "GameSvr");
    foreach (var file in Directory.EnumerateFiles(gamesvr, "*.cs",
                 SearchOption.AllDirectories))
    {
        if (pattern.IsMatch(File.ReadAllText(file)))
            return true;
    }

    return false;
}

static bool HasMappedEa(string source, string ea)
    => Regex.IsMatch(source, @"=\s*" + Regex.Escape(ea) + @"\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

static string MethodBody(string source, string signature)
{
    var start = source.IndexOf(signature, StringComparison.Ordinal);
    Assert(start >= 0, "missing method: " + signature);
    var open = source.IndexOf('{', start);
    Assert(open > start, "missing body: " + signature);
    var depth = 0;
    for (var i = open; i < source.Length; i++)
    {
        var c = source[i];
        if (c == '{') depth++;
        else if (c == '}' && --depth == 0)
            return source[open..(i + 1)];
    }

    throw new InvalidOperationException("unclosed body: " + signature);
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

static void Require(string source, string value, string label)
    => Assert(source.Contains(value, StringComparison.Ordinal), label + " missing");

static void Reject(string source, string value, string label)
    => Assert(!source.Contains(value, StringComparison.Ordinal), label);

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
