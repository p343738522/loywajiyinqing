using System.Text;

// POIS-36/37 GasAttackMonster consumer: native @0x666D2F / @0x6670AB reads
// word[target+0x26C] (+ m_wEffectResistance) then Random(res+20)==0.
// MagGroupAmyounsul (wMagicID 48) stays on the legacy byte predicate —
// native that site calls sub_76FBBC.

var root = AuditRepoRoot.Resolve(args);
var failures = new List<string>();

var gas = Read("GameSvr", "Monsters", "Monster", "GasAttackMonster.cs");
Require(gas.Contains("m_wEffectResistance + 20"),
    "GasAttackMonster POIS-36 must roll word[+0x26C] = m_wEffectResistance + 20 (@0x666D2F/@0x6670AB)");
Reject(gas.Contains("m_btAntiPoison + 20"),
    "GasAttackMonster must not keep the dump-only m_btAntiPoison byte projection");
Require(gas.Contains("RandomNumber.Random(BaseObject.m_wEffectResistance + 20) == 0"),
    "GasAttackMonster must keep Random(res+20)==0 (test eax,eax / jne skip)");
Require(gas.Contains("MakePosion(Grobal2.POISON_STONE, 5, 0)"),
    "GasAttackMonster must still apply stone poison 5s/level 0");

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
    Console.Error.WriteLine("FAIL Poison36GasAttackMonsterConsumerCheck");
    foreach (var failure in failures)
        Console.Error.WriteLine("  " + failure);
    return 1;
}

Console.WriteLine("PASS Poison36GasAttackMonsterConsumerCheck");
Console.WriteLine("  GasAttackMonster POIS-36 wired to m_wEffectResistance + 20");
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
