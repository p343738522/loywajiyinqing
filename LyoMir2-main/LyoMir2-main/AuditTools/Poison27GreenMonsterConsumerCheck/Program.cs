using System.Text;

// POIS-27 GreenMonster consumer: native @0x674378 reads word[target+0x26C]
// (+ m_wEffectResistance) then Random(res+7)<=6. MagGroupAmyounsul (wMagicID 48)
// stays on the legacy byte predicate — native that site calls sub_76FBBC.

var root = AuditRepoRoot.Resolve(args);
var failures = new List<string>();

var green = Read("GameSvr", "Monsters", "Monster", "GreenMonster.cs");
Require(green.Contains("m_wEffectResistance + 7"),
    "GreenMonster POIS-27 must roll word[+0x26C] = m_wEffectResistance + 7 (@0x674378)");
Reject(green.Contains("m_btAntiPoison + 7"),
    "GreenMonster must not keep the dump-only m_btAntiPoison byte projection");
Require(green.Contains("RandomNumber.Random(m_TargetCret.m_wEffectResistance + 7) <= 6"),
    "GreenMonster must keep Random(res+7)<=6 (cmp eax,6 / jg skip)");
Require(green.Contains("MakePosion(Grobal2.POISON_DECHEALTH, 30, 1)"),
    "GreenMonster must still apply green poison 30s/level 1");

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

if (failures.Count != 0)
{
    Console.Error.WriteLine("FAIL Poison27GreenMonsterConsumerCheck");
    foreach (var failure in failures)
        Console.Error.WriteLine("  " + failure);
    return 1;
}

Console.WriteLine("PASS Poison27GreenMonsterConsumerCheck");
Console.WriteLine("  GreenMonster POIS-27 wired to m_wEffectResistance + 7");
Console.WriteLine("  MagGroupAmyounsul stays CLOSED on m_btAntiPoison + 7");
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
