using System.Text;

// POIS-27 RedMonster consumer: native @0x674518 reads word[target+0x26C]
// (+ m_wEffectResistance) then Random(res+7)<=6. MagGroupAmyounsul (wMagicID 48)
// stays on the legacy byte predicate — native that site calls sub_76FBBC.

var root = AuditRepoRoot.Resolve(args);
var failures = new List<string>();

var red = Read("GameSvr", "Monsters", "Monster", "RedMonster.cs");
Require(red.Contains("m_wEffectResistance + 7"),
    "RedMonster POIS-27 must roll word[+0x26C] = m_wEffectResistance + 7 (@0x674518)");
Reject(red.Contains("m_btAntiPoison + 7"),
    "RedMonster must not keep the dump-only m_btAntiPoison byte projection");
Require(red.Contains("RandomNumber.Random(m_TargetCret.m_wEffectResistance + 7) <= 6"),
    "RedMonster must keep Random(res+7)<=6 (cmp eax,6 / jg skip)");
Require(red.Contains("MakePosion(Grobal2.POISON_DAMAGEARMOR, 30, 1)"),
    "RedMonster must still apply red poison 30s/level 1");

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
    Console.Error.WriteLine("FAIL Poison27RedMonsterConsumerCheck");
    foreach (var failure in failures)
        Console.Error.WriteLine("  " + failure);
    return 1;
}

Console.WriteLine("PASS Poison27RedMonsterConsumerCheck");
Console.WriteLine("  RedMonster POIS-27 wired to m_wEffectResistance + 7");
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
