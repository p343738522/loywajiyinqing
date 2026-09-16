using System.Text;

// NativeMakePosion override: ItemAttMon VMT+0xC8 sub_66D1D8 = `C2 04 00`
// (ret 4 / refuse apply). MagGroupAmyounsul (wMagicID 48) stays on the
// legacy byte predicate — native that site calls sub_76FBBC.
// NativeParalysisResistPercent stays 0 (item +0xAC producer unmapped).

var root = AuditRepoRoot.Resolve(args);
var failures = new List<string>();

var item = Read("GameSvr", "Monsters", "Monster", "ItemAttMon.cs");
Require(item.Contains("internal override bool NativeMakePosion"),
    "ItemAttMon VMT+0xC8 must override NativeMakePosion (empty sub_66D1D8)");
Require(item.Contains("return false;"),
    "ItemAttMon NativeMakePosion must stay empty (ret 4 / refuse apply)");
Require(item.Contains("0x66D1D8") && item.Contains("C2 04 00"),
    "ItemAttMon NativeMakePosion must keep the ret-4 dump (@0x66D1D8)");
Reject(item.Contains("全为属性/空虚槽（C# 侧非虚具体函数，无可覆写入口）或依赖未命名父级字段，故全保留父实现"),
    "ItemAttMon +0xC8 must no longer stay dump-only");

var group = Read("GameSvr", "Spells", "MagicManager.cs");
Require(group.Contains("POIS-27 BLOCKED"),
    "MagGroupAmyounsul must stay CLOSED (POIS-27 BLOCKED, sub_76FBBC unmapped)");
Require(group.Contains("Random(BaseObject.m_btAntiPoison + 7) <= 6"),
    "MagGroupAmyounsul must keep the legacy byte predicate until sub_76FBBC is mapped");
Reject(group.Contains("Random(BaseObject.m_wEffectResistance + 7)"),
    "MagGroupAmyounsul must not be rewritten onto the +0x26C word roll");

var rng = Read("SystemModule", "RandomNumber.cs");
Require(rng.Contains("POIS-26"),
    "gameplay RNG facade must remain the POIS-26 Delphi LCG path");

var resist = Read("GameSvr", "Actors", "TBaseObject.NativeMakePosion.cs");
Require(resist.Contains("protected virtual int NativeParalysisResistPercent => 0;"),
    "NativeParalysisResistPercent must stay 0 until item +0xAC producer is proven");

if (failures.Count != 0)
{
    Console.Error.WriteLine("FAIL PoisonItemAttMonNativeMakePosionConsumerCheck");
    foreach (var failure in failures)
        Console.Error.WriteLine("  " + failure);
    return 1;
}

Console.WriteLine("PASS PoisonItemAttMonNativeMakePosionConsumerCheck");
Console.WriteLine("  ItemAttMon VMT+0xC8 stays empty (immune, ret 4 @0x66D1D8)");
Console.WriteLine("  MagGroupAmyounsul stays CLOSED on m_btAntiPoison + 7");
Console.WriteLine("  NativeParalysisResistPercent stays 0");
Console.WriteLine("  RNG facade unchanged");
return 0;

string Read(params string[] parts) =>
    File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()), Encoding.UTF8);

void Require(bool condition, string message)
{
    if (!condition)
        failures.Add(message);
}

void Reject(bool condition, string message)
{
    if (condition)
        failures.Add(message);
}
