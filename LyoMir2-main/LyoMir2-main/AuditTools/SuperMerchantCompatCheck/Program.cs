using System.Collections;
using System.Text.RegularExpressions;
using GameSvr;
using GameSvr.PasEngine;
using GameSvr.Services;
using SystemModule;

PrepareRuntimeConfig();
M2Share.g_Config = new GameSvrConfig();
M2Share.ObjectManager = new ObjectManager();
M2Share.UserEngine = new UserEngine();
M2Share.ProcessMsgCriticalSection = new object();
M2Share.LogMsgCriticalSection = new object();
M2Share.LogStringList = new ArrayList();
M2Share.SuperMerchantManager = new NativeSuperMerchantManager();

SetDefinitions(
    new GoodItem { Name = "疗伤药包", StdMode = 0, Weight = 1 },
    new GoodItem { Name = "万年雪霜包", StdMode = 0, Weight = 1 });

var player = NewPlayer();
var bridge = new PasApiBridge { CurrentPlayer = player };
SeedBag(player, itemIndex: 1, count: 8);

var manager = M2Share.SuperMerchantManager;
Equal(1000, manager.GetCurrentStorage(1), "ctor current type1");
EqualText("疗伤药包", manager.GetGoodsName(1), "ctor name type1");
EqualText("万年雪霜包", manager.GetGoodsName(2), "ctor name type2");

var expectedUnit = (double)NativeSuperMerchantManager.BasePriceSingle
    - NativeSuperMerchantManager.LnCoefficient * Math.Log(1000);
var expectedGlory5 = (int)(expectedUnit * 5);
Equal(expectedGlory5, manager.ComputeGloryQuote(1, 5),
    "glory quote formula type1 x5");
Equal(0, manager.ComputeGloryQuote(0, 5), "invalid type unit price 0");
Equal(0, manager.ComputeGloryQuote(3, 5), "type3 unit price 0");

var queryArgs = Args(1, 5);
Assert(bridge.CallPlayerFunc("QueryGloryPointByGoodsNum", queryArgs,
        out var queryResult),
    "QueryGloryPointByGoodsNum function was not dispatched");
Equal(expectedGlory5, queryResult.AsInt(), "query return glory");
Equal(1, player.m_nSuperMerchantSellGoodsType, "pending goodsType");
Equal(expectedGlory5, player.m_nSuperMerchantSellGloryQuote, "pending glory");

Assert(bridge.CallPlayerFunc("SellGoodsToGetGloryPoint", queryArgs,
        out var sellResult),
    "SellGoodsToGetGloryPoint function was not dispatched");
Equal(NativeSuperMerchantManager.SellOk, sellResult.AsInt(), "sell ok");
Equal(3, player.m_ItemList.Count, "sell did not take 5 packs");
Equal(1005, manager.GetCurrentStorage(1), "sell did not add 5 to storage");
Equal(expectedGlory5, player.m_CreditCard.GloryPointValue,
    "sell glory used TryAdd amount");
Assert(player.m_CreditCard.GloryPointDirty, "sell did not mark glory dirty");
Equal(0, player.m_nSuperMerchantSellGoodsType, "sell did not clear pending type");
Equal(0, player.m_nSuperMerchantSellGloryQuote, "sell did not clear pending glory");
Equal(1, M2Share.LogStringList.Count, "sell type-9 log count");
EqualText(string.Join('\t', 9, player.m_sMapName, player.m_nCurrX,
        player.m_nCurrY, player.m_sCharName, "疗伤药包", 1, expectedGlory5,
        "大药商人"),
    (string)M2Share.LogStringList[0], "sell type-9 log body");
Assert(player.m_MsgList.Count >= 1, "sell glory prompt missing");

Assert(bridge.CallPlayerFunc("SellGoodsToGetGloryPoint", queryArgs,
        out var sellAgain),
    "second sell without query was not dispatched");
Equal(NativeSuperMerchantManager.SellMismatch, sellAgain.AsInt(),
    "second sell without query must be -1");
Equal(3, player.m_ItemList.Count, "mismatch sell mutated bag");
Equal(1005, manager.GetCurrentStorage(1), "mismatch sell mutated storage");

ResetEconomy(player);
M2Share.LogStringList.Clear();
player.m_MsgList.Clear();
Assert(bridge.CallPlayerFunc("QueryGloryPointByGoodsNum", Args(1, 4),
        out var quoted),
    "bag-short query failed");
var bagShortQuote = quoted.AsInt();
Assert(bridge.CallPlayerFunc("SellGoodsToGetGloryPoint", Args(1, 4),
        out var bagShort),
    "bag-short sell failed");
Equal(NativeSuperMerchantManager.SellBagShort, bagShort.AsInt(),
    "short bag must return -2");
Equal(1005, manager.GetCurrentStorage(1), "-2 must not commit storage");
Equal(0, player.m_CreditCard.GloryPointValue, "-2 must not add glory");
Equal(1, player.m_nSuperMerchantSellGoodsType, "-2 must keep pending");
Equal(bagShortQuote, player.m_nSuperMerchantSellGloryQuote,
    "-2 must keep pending glory");

Assert(manager.TrySetStock(1, 3, 2490), "set current 2490");
Assert(bridge.CallPlayerFunc("QueryGloryPointByGoodsNum", Args(1, 20),
        out var truncQuote),
    "truncate query failed");
var truncGlory = truncQuote.AsInt();
Assert(truncGlory > 0, "truncate quote was 0");
SeedBag(player, itemIndex: 1, count: 20);
ResetEconomy(player);
player.m_MsgList.Clear();
M2Share.LogStringList.Clear();
Assert(bridge.CallPlayerFunc("SellGoodsToGetGloryPoint", Args(1, 20),
        out var truncSell),
    "truncate sell failed");
Equal(NativeSuperMerchantManager.SellOk, truncSell.AsInt(),
    "silent truncate must still return 1");
Equal(2500, manager.GetCurrentStorage(1),
    "silent truncate must saturate at Max");
Equal(truncGlory, player.m_CreditCard.GloryPointValue,
    "silent truncate must pay the full quoted glory");

Assert(manager.TrySetStock(1, 3, 1000), "restore current 1000");
SeedBag(player, itemIndex: 1, count: 2);
ResetEconomy(player);
Assert(bridge.CallPlayerFunc("QueryGloryPointByGoodsNum", Args(1, 2),
        out _),
    "stale-quote query failed");
Assert(manager.TrySetStock(1, 3, 1100), "change storage after quote");
Assert(bridge.CallPlayerFunc("SellGoodsToGetGloryPoint", Args(1, 2),
        out var stale),
    "stale-quote sell failed");
Equal(NativeSuperMerchantManager.SellMismatch, stale.AsInt(),
    "storage change between query and sell must be -1");
Equal(2, player.m_ItemList.Count, "stale quote mutated bag");
Equal(1100, manager.GetCurrentStorage(1), "stale quote mutated storage");

var savedManager = M2Share.SuperMerchantManager;
M2Share.SuperMerchantManager = null;
Assert(bridge.CallPlayerFunc("QueryGloryPointByGoodsNum", Args(1, 1),
        out var nullQuery),
    "null-manager query not dispatched");
Equal(0, nullQuery.AsInt(), "null-manager query must return 0");
Assert(bridge.CallPlayerFunc("SellGoodsToGetGloryPoint", Args(1, 1),
        out var nullSell),
    "null-manager sell not dispatched");
Equal(NativeSuperMerchantManager.SellMismatch, nullSell.AsInt(),
    "null-manager sell must return -1");
M2Share.SuperMerchantManager = savedManager;

using System.Collections;
using System.Text.RegularExpressions;
using GameSvr;
using GameSvr.PasEngine;
using GameSvr.Services;
using SystemModule;

PrepareRuntimeConfig();
M2Share.g_Config = new GameSvrConfig();
M2Share.ObjectManager = new ObjectManager();
M2Share.UserEngine = new UserEngine();
M2Share.ProcessMsgCriticalSection = new object();
M2Share.LogMsgCriticalSection = new object();
M2Share.LogStringList = new ArrayList();
M2Share.SuperMerchantManager = new NativeSuperMerchantManager();

SetDefinitions(
    new GoodItem { Name = "疗伤药包", StdMode = 0, Weight = 1 },
    new GoodItem { Name = "万年雪霜包", StdMode = 0, Weight = 1 });

var player = NewPlayer();
var bridge = new PasApiBridge { CurrentPlayer = player };
SeedBag(player, itemIndex: 1, count: 8);

var manager = M2Share.SuperMerchantManager;
Equal(1000, manager.GetCurrentStorage(1), "ctor current type1");
EqualText("疗伤药包", manager.GetGoodsName(1), "ctor name type1");
EqualText("万年雪霜包", manager.GetGoodsName(2), "ctor name type2");

var expectedUnit = (double)NativeSuperMerchantManager.BasePriceSingle
    - NativeSuperMerchantManager.LnCoefficient * Math.Log(1000);
var expectedGlory5 = (int)(expectedUnit * 5);
Equal(expectedGlory5, manager.ComputeGloryQuote(1, 5),
    "glory quote formula type1 x5");
Equal(0, manager.ComputeGloryQuote(0, 5), "invalid type unit price 0");
Equal(0, manager.ComputeGloryQuote(3, 5), "type3 unit price 0");

var queryArgs = Args(1, 5);
Assert(bridge.CallPlayerFunc("QueryGloryPointByGoodsNum", queryArgs,
        out var queryResult),
    "QueryGloryPointByGoodsNum function was not dispatched");
Equal(expectedGlory5, queryResult.AsInt(), "query return glory");
Equal(1, player.m_nSuperMerchantSellGoodsType, "pending goodsType");
Equal(expectedGlory5, player.m_nSuperMerchantSellGloryQuote, "pending glory");

Assert(bridge.CallPlayerFunc("SellGoodsToGetGloryPoint", queryArgs,
        out var sellResult),
    "SellGoodsToGetGloryPoint function was not dispatched");
Equal(NativeSuperMerchantManager.SellOk, sellResult.AsInt(), "sell ok");
Equal(3, player.m_ItemList.Count, "sell did not take 5 packs");
Equal(1005, manager.GetCurrentStorage(1), "sell did not add 5 to storage");
Equal(expectedGlory5, player.m_CreditCard.GloryPointValue,
    "sell glory used TryAdd amount");
Assert(player.m_CreditCard.GloryPointDirty, "sell did not mark glory dirty");
Equal(0, player.m_nSuperMerchantSellGoodsType, "sell did not clear pending type");
Equal(0, player.m_nSuperMerchantSellGloryQuote, "sell did not clear pending glory");
Equal(1, M2Share.LogStringList.Count, "sell type-9 log count");
EqualText(string.Join('\t', 9, player.m_sMapName, player.m_nCurrX,
        player.m_nCurrY, player.m_sCharName, "疗伤药包", 1, expectedGlory5,
        "大药商人"),
    (string)M2Share.LogStringList[0], "sell type-9 log body");
Assert(player.m_MsgList.Count >= 1, "sell glory prompt missing");

Assert(bridge.CallPlayerFunc("SellGoodsToGetGloryPoint", queryArgs,
        out var sellAgain),
    "second sell without query was not dispatched");
Equal(NativeSuperMerchantManager.SellMismatch, sellAgain.AsInt(),
    "second sell without query must be -1");
Equal(3, player.m_ItemList.Count, "mismatch sell mutated bag");
Equal(1005, manager.GetCurrentStorage(1), "mismatch sell mutated storage");

ResetEconomy(player);
M2Share.LogStringList.Clear();
player.m_MsgList.Clear();
Assert(bridge.CallPlayerFunc("QueryGloryPointByGoodsNum", Args(1, 4),
        out var quoted),
    "bag-short query failed");
var bagShortQuote = quoted.AsInt();
Assert(bridge.CallPlayerFunc("SellGoodsToGetGloryPoint", Args(1, 4),
        out var bagShort),
    "bag-short sell failed");
Equal(NativeSuperMerchantManager.SellBagShort, bagShort.AsInt(),
    "short bag must return -2");
Equal(1005, manager.GetCurrentStorage(1), "-2 must not commit storage");
Equal(0, player.m_CreditCard.GloryPointValue, "-2 must not add glory");
Equal(1, player.m_nSuperMerchantSellGoodsType, "-2 must keep pending");
Equal(bagShortQuote, player.m_nSuperMerchantSellGloryQuote,
    "-2 must keep pending glory");

Assert(manager.TrySetStock(1, 3, 2490), "set current 2490");
Assert(bridge.CallPlayerFunc("QueryGloryPointByGoodsNum", Args(1, 20),
        out var truncQuote),
    "truncate query failed");
var truncGlory = truncQuote.AsInt();
Assert(truncGlory > 0, "truncate quote was 0");
SeedBag(player, itemIndex: 1, count: 20);
ResetEconomy(player);
player.m_MsgList.Clear();
M2Share.LogStringList.Clear();
Assert(bridge.CallPlayerFunc("SellGoodsToGetGloryPoint", Args(1, 20),
        out var truncSell),
    "truncate sell failed");
Equal(NativeSuperMerchantManager.SellOk, truncSell.AsInt(),
    "silent truncate must still return 1");
Equal(2500, manager.GetCurrentStorage(1),
    "silent truncate must saturate at Max");
Equal(truncGlory, player.m_CreditCard.GloryPointValue,
    "silent truncate must pay the full quoted glory");

Assert(manager.TrySetStock(1, 3, 1000), "restore current 1000");
SeedBag(player, itemIndex: 1, count: 2);
ResetEconomy(player);
Assert(bridge.CallPlayerFunc("QueryGloryPointByGoodsNum", Args(1, 2),
        out _),
    "stale-quote query failed");
Assert(manager.TrySetStock(1, 3, 1100), "change storage after quote");
Assert(bridge.CallPlayerFunc("SellGoodsToGetGloryPoint", Args(1, 2),
        out var stale),
    "stale-quote sell failed");
Equal(NativeSuperMerchantManager.SellMismatch, stale.AsInt(),
    "storage change between query and sell must be -1");
Equal(2, player.m_ItemList.Count, "stale quote mutated bag");
Equal(1100, manager.GetCurrentStorage(1), "stale quote mutated storage");

var savedManager = M2Share.SuperMerchantManager;
M2Share.SuperMerchantManager = null;
Assert(bridge.CallPlayerFunc("QueryGloryPointByGoodsNum", Args(1, 1),
        out var nullQuery),
    "null-manager query not dispatched");
Equal(0, nullQuery.AsInt(), "null-manager query must return 0");
Assert(bridge.CallPlayerFunc("SellGoodsToGetGloryPoint", Args(1, 1),
        out var nullSell),
    "null-manager sell not dispatched");
Equal(NativeSuperMerchantManager.SellMismatch, nullSell.AsInt(),
    "null-manager sell must return -1");
M2Share.SuperMerchantManager = savedManager;

Assert(!bridge.CallPlayerFunc("QueryGloryPointByGoodsNum",
        new List<PasValue> { PasValue.FromInt(1) }, out var badArity),
    "query accepted wrong arity");
Assert(badArity.Type == PasValueType.Nil, "wrong-arity query result");
Assert(!bridge.CallPlayerFunc("SellGoodsToGetGloryPoint",
        new List<PasValue> { PasValue.FromInt(1) }, out var badSellArity),
    "sell accepted wrong arity");
Assert(badSellArity.Type == PasValueType.Nil, "wrong-arity sell result");

Assert(bridge.CallPlayerMethod("QueryGloryPointByGoodsNum", Args(2, 1)),
    "query method was not dispatched");
Assert(bridge.CallPlayerMethod("SellGoodsToGetGloryPoint", Args(2, 1)),
    "sell method was not dispatched");

var closedArgs = Args(1, 1);
Assert(!bridge.CallPlayerFunc("ConsumeYBToBuyGoods", closedArgs,
        out var buyFunc),
    "ConsumeYBToBuyGoods function must stay closed");
Assert(buyFunc.Type == PasValueType.Nil, "buy function result");
Assert(!bridge.CallPlayerMethod("ConsumeYBToBuyGoods", closedArgs),
    "ConsumeYBToBuyGoods method must stay closed");
Assert(!bridge.CallPlayerFunc("QueryGoodsNumByYBNum", closedArgs,
        out var ybQuery),
    "QueryGoodsNumByYBNum must stay closed");
Assert(ybQuery.Type == PasValueType.Nil, "yb-query result");
Assert(!bridge.CallPlayerFunc("GetGoodsCurrentStorage",
        new List<PasValue> { PasValue.FromInt(1) }, out var storageQuery),
    "GetGoodsCurrentStorage must stay closed (sub_6166E0 body unmapped)");
Assert(storageQuery.Type == PasValueType.Nil, "storage-query result");

var root = AuditRepoRoot.Resolve();
var bridgeSource = File.ReadAllText(Path.Combine(root, "GameSvr",
    "ScriptSystem", "PasEngine", "PasApiBridge.cs"));
var helperSource = File.ReadAllText(Path.Combine(root, "GameSvr",
    "ScriptSystem", "PasEngine", "PasApiBridge.NativeSuperMerchant.cs"));
var managerSource = File.ReadAllText(Path.Combine(root, "GameSvr",
    "Services", "NativeSuperMerchantManager.cs"));

RequireMatches(bridgeSource,
    "case \\\"queryglorypointbygoodsnum\\\":\\s*" +
    "return CallQueryGloryPointByGoodsNum\\(args, out result\\);",
    1, "QueryGloryPointByGoodsNum function must share the sell helper");
RequireMatches(bridgeSource,
    "case \\\"sellgoodstogetglorypoint\\\":\\s*" +
    "return CallSellGoodsToGetGloryPoint\\(args, out result\\);",
    1, "SellGoodsToGetGloryPoint function must share the query helper");
RequireMatches(bridgeSource,
    "case \\\"consumeybtobuygoods\\\":\\s*" +
    "return RejectUnsupportedNativeApi\\(out result\\);",
    1, "ConsumeYBToBuyGoods function must stay fail-closed");
RequireMatches(bridgeSource,
    "case \\\"consumeybtobuygoods\\\":\\s*" +
    "return RejectUnsupportedNativeApi\\(\\);",
    1, "ConsumeYBToBuyGoods method must stay fail-closed");
Assert(helperSource.Contains("TryAddNativeGloryPoint(glory)",
        StringComparison.Ordinal),
    "sell must credit glory through TryAddNativeGloryPoint");
Assert(helperSource.Contains("TryCommitAdd(goodsType, goodsNum)",
        StringComparison.Ordinal),
    "sell must commit storage through TryCommitAdd");
Assert(helperSource.Contains("TakeItemsCore(itemName, goodsNum)",
        StringComparison.Ordinal),
    "sell must take bag items through TakeItemsCore");
Assert(helperSource.IndexOf("TryCommitAdd", StringComparison.Ordinal)
        < helperSource.IndexOf("TakeItemsCore", StringComparison.Ordinal),
    "native order is commit storage then take items");
Assert(managerSource.Contains("if (next > slot.Max)", StringComparison.Ordinal),
    "sub_615F44 silent saturate is missing");
Reject(helperSource, "m_nGameGold",
    "sell path must not invent a local yuanbao mutator");
Reject(helperSource, "ConsumeYBToBuyGoods",
    "sell helper must not implement the unmapped YB-buy half");
Assert(bridgeSource.Contains("sub_6D5344@0x6D56E0", StringComparison.Ordinal),
    "ConsumeYB closed comment must pin Ident-125 delivery VA 0x6D56E0");
Assert(bridgeSource.Contains("sel=0x2742=10050", StringComparison.Ordinal),
    "ConsumeYB closed comment must pin Ident-125 selector 10050");
Assert(managerSource.Contains("sub_6D5344@0x6D56E0", StringComparison.Ordinal),
    "BuyYbEa must pin missing Ident-125 delivery VA");

var playerSlots = File.ReadAllText(Path.Combine(root, "GameSvr",
    "Players", "TPlayObject.NativeSuperMerchant.cs"));
Reject(playerSlots, "m_nSuperMerchantBuy",
    "player SuperMerchant file must not grow YB-buy fields without the Ident-125 executor");
Assert(playerSlots.Contains("sub_6D5344 SuperMerchant branch @0x6D56E0",
        StringComparison.Ordinal),
    "player SuperMerchant file must pin missing Ident-125 delivery VA");

var ybClient = File.ReadAllText(Path.Combine(root, "GameSvr",
    "Services", "YbDbClient.cs"));
Assert(!Regex.IsMatch(ybClient,
        @"\bcase\s+1125\s*:|(?:frame|queued\.Frame)\.Ident\s*(?:==|is)\s*1125"),
    "Ident-125 SuperMerchant reply must not open via 1125 dispatch");
Reject(ybClient, "0x2742",
    "YbDbClient must not open SuperMerchant Ident-125 sel=10050");

Console.WriteLine(
    "PASS SuperMerchant query+sell=wired glory=TryAddNativeGloryPoint " +
    "commit=sub_615F44 truncate=pay-full mismatch=-1 bag=-2 " +
    "buy=closed queryYb=closed getStorage=closed " +
    "missing=sub_6D5344@0x6D56E0 Ident-125 sel=10050");
return;

static List<PasValue> Args(int a, int b) =>
    new() { PasValue.FromInt(a), PasValue.FromInt(b) };

static TPlayObject NewPlayer()
{
    return new TPlayObject
    {
        m_boOffLineFlag = true,
        m_sMapName = "audit-map",
        m_nCurrX = 12,
        m_nCurrY = 34,
        m_sCharName = "audit-role"
    };
}

static void SetDefinitions(params GoodItem[] definitions)
{
    M2Share.UserEngine.StdItemList.Clear();
    foreach (var definition in definitions)
        M2Share.UserEngine.StdItemList.Add(definition);
}

static void SeedBag(TPlayObject player, ushort itemIndex, int count)
{
    player.m_ItemList.Clear();
    for (var i = 0; i < count; i++)
    {
        player.m_ItemList.Add(new TUserItem
        {
            MakeIndex = 1000 + i,
            wIndex = itemIndex,
            Dura = 1,
            DuraMax = 1,
            btValue = new byte[14]
        });
    }
}

static void ResetEconomy(TPlayObject player)
{
    player.m_CreditCard.GloryPointValue = 0;
    player.m_CreditCard.GloryPointDirty = false;
    player.m_CreditCard.GloryPointDirtyVersion = 0;
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

static void RequireMatches(string source, string pattern, int expected,
    string message)
{
    var count = Regex.Matches(source, pattern).Count;
    if (count != expected)
        Fail($"{message}: expected {expected} matches, actual {count}");
}

static void Reject(string source, string marker, string message)
{
    if (source.Contains(marker, StringComparison.Ordinal))
        Fail(message);
}

static void Equal(int expected, int actual, string message)
{
    if (expected != actual)
        Fail($"{message}: expected {expected}, actual {actual}");
}

static void EqualText(string expected, string actual, string message)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
        Fail($"{message}: expected [{expected}], actual [{actual}]");
}

static void Assert(bool condition, string message)
{
    if (!condition) Fail(message);
}

static void Fail(string message) => throw new InvalidOperationException(message);
