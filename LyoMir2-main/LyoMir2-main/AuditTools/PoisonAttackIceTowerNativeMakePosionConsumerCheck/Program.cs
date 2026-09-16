using System.Text;

// NativeMakePosion consumer: AttackIceTower Run helper sub_674D38
//   push 1 / mov cx,3 / mov dl,0x1D / call [target.VMT+0xC8]
// plus the empty VMT+0xC8 override sub_674D78. MagGroupAmyounsul (wMagicID 48)
// stays on the legacy byte predicate — native that site calls sub_76FBBC.
// NativeParalysisResistPercent stays 0. RecalcAbilitys native adds word
// [edi+0xAC] to [self+0x180]; property 30 麻痹抗性 is wAntiPoison / +0x26C,
// not that field. Do not alias until agg1+0xAC is a proven producer.

var root = AuditRepoRoot.Resolve(args);
var failures = new List<string>();

var tower = Read("GameSvr", "Monsters", "Monster", "AttackIceTower.cs");
Require(tower.Contains("target.NativeMakePosion(0x1D, 3, 1)"),
    "AttackIceTower Run helper must apply NativeMakePosion(0x1D, 3, 1) (@0x674D38)");
Require(tower.Contains("internal override bool NativeMakePosion"),
    "AttackIceTower VMT+0xC8 must override NativeMakePosion (empty sub_674D78)");
Require(tower.Contains("return false;"),
    "AttackIceTower NativeMakePosion must stay empty (ret 4 / refuse apply)");
Reject(tower.Contains("宁缺毋滥：不落地半个 Run"),
    "AttackIceTower Run must no longer stay dump-only");
Require(tower.Contains("SearchViewRange()"),
    "AttackIceTower Run must refresh visibility via SearchViewRange (@0x765DEC)");
Require(tower.Contains("m_WAbil.HP = 0"),
    "AttackIceTower Run must zero HP after 15s ([self+0x2AC]=0 @ +0x4DC)");
Require(tower.Contains("fail-closed") && tower.Contains("+0x084 Die"),
    "AttackIceTower Die must stay CLOSED (0x76B4F8 / 0x76B518 unmapped)");

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
    "NativeParalysisResistPercent must stay 0 until agg1+0xAC producer is proven");
Require(resist.Contains("Do not alias") && resist.Contains("wAntiPoison") &&
        resist.Contains("+0x26C") && resist.Contains("agg1+0x3C"),
    "NativeParalysisResistPercent must keep wAntiPoison / property 30 as +0x26C, not [self+0x180]");
Reject(resist.Contains("NativeParalysisResistPercent => m_") ||
       resist.Contains("NativeParalysisResistPercent => (int)") ||
       resist.Contains("NativeParalysisResistPercent => wAntiPoison"),
    "NativeParalysisResistPercent must not alias wAntiPoison or another live field");

var effect = Read("GameSvr", "Actors", "TBaseObject.NativeEffectAbility.cs");
Require(effect.Contains("case 30:") && effect.Contains("addAbility.wAntiPoison"),
    "property 30 麻痹抗性 must keep mapping to wAntiPoison (+0x26C path)");
Reject(effect.Contains("NativeParalysisResistPercent"),
    "property 30 must not be wired into NativeParalysisResistPercent");

if (failures.Count != 0)
{
    Console.Error.WriteLine("FAIL PoisonAttackIceTowerNativeMakePosionConsumerCheck");
    foreach (var failure in failures)
        Console.Error.WriteLine("  " + failure);
    return 1;
}

Console.WriteLine("PASS PoisonAttackIceTowerNativeMakePosionConsumerCheck");
Console.WriteLine("  AttackIceTower Run wired to NativeMakePosion(0x1D, 3, 1)");
Console.WriteLine("  AttackIceTower VMT+0xC8 stays empty (immune)");
Console.WriteLine("  MagGroupAmyounsul stays CLOSED on m_btAntiPoison + 7");
Console.WriteLine("  NativeParalysisResistPercent stays 0 (no wAntiPoison alias)");
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
