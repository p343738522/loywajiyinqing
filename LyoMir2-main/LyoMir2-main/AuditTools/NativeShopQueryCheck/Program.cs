// C4 P1: SeeShop read/query slices (CM 1046 / sub_63A254 and CM 1047 / sub_63A32C)
// through MallManager + the 180-byte TClientShop codec. shopMgr [[0x7D5D98]] stays
// unmapped, so CM 1054 remains fail-closed (no Ident-125, no YB debit).

using GameSvr;
using GameSvr.Services;
using SystemModule;

var root = AuditRepoRoot.Resolve();
var asserts = 0;

CheckShopMgrPointerUnmapped();
CheckCodecAndPlanner();
CheckCm1054StayClosed();
CheckQuerySliceWired(root);
CheckNoIdent125OrYbDebit(root);

Console.WriteLine(
    "PASS NativeShopQueryCheck wired=1046/sub_63A254+1047/sub_63A32C closed=1054 " +
    "shopMgr=[[0x7D5D98]] unmapped codec=180 asserts=" + asserts);
return 0;

void CheckShopMgrPointerUnmapped()
{
    Equal(0x007D5D98u, NativeShopQuery.ShopMgrVa, "shopMgr VA");
    Equal(0x00637A00u, NativeShopQuery.EnqueueEa, "sub_637A00 enqueue VA");
    Equal(0x006D3694u, NativeShopQuery.SubmitWrapperEa, "sub_6D3694 submit wrapper VA");
    Equal(0x006D942Fu, NativeShopQuery.Cm1054LeafEa, "CM 1054 leaf VA");
    Equal(0x7B, NativeShopQuery.Cm1054Subcmd, "CM 1054 subcmd 0x7B");
    Assert(!NativeShopQuery.ShopMgrPointerMapped,
        "shopMgr [[0x7D5D98]] must stay unmapped");
    Assert(NativeShopQuery.CatalogMapped, "MallManager catalog must stay mapped");
    Assert(!NativeMallSubmitChannel.IsActive,
        "shopMgr [[0x7D5D98]] channel must stay inactive");
    Assert(!NativeMallSubmitChannel.TrySubmit(null, NativeShopQuery.Cm1054Subcmd),
        "CM 1054 must not enqueue through unmapped shopMgr");
}

void CheckCodecAndPlanner()
{
    Equal(180, NativeShopQueryCodec.RecordSize, "TClientShop record size");
    Equal(15, NativeShopQueryCodec.NameCapacity, "name ShortString capacity");
    Equal(16, NativeShopQueryCodec.CategoryNameOffset, "category name offset");
    Equal(32, NativeShopQueryCodec.LooksOffset, "Looks offset");
    Equal(52, NativeShopQueryCodec.DescriptionOffset, "description offset");
    Equal(127, NativeShopQueryCodec.DescriptionCapacity, "description capacity");
    Equal(5, NativeShopQueryCodec.HotRecordCount, "hot-list slot count");
    Equal(900, NativeShopQueryCodec.HotBodySize, "SM_FIRSTSHOP 900-byte body");
    Equal(84, NativeShopQuery.PerTypeListOffset(0), "shop[+84] type-0 list");
    Equal(0x158, NativeShopQuery.HotListOffset, "shop[+0x158] hot list");
    Equal(8, NativeShopQuery.MaxShopType, "type<8 gate");
    Equal(1046, NativeShopQuery.CmReqSeeShop, "CM_REQSEESHOP");
    Equal(1047, NativeShopQuery.CmRenewSeeShop, "CM_RENEWSEESHOP");
    Equal(0x0063A254u, NativeShopQuery.ReqSeeShopEa, "sub_63A254");
    Equal(0x0063A32Cu, NativeShopQuery.RenewSeeShopEa, "sub_63A32C");
    Equal(NativeShopWriteTransaction.SentMaskOffset, NativeShopQuery.SentMaskOffset,
        "sent-mask offset +0xB87");

    var over = NativeShopQuery.EvaluateReqSeeShop(8, false, true, true);
    Assert(over.IsNoOp && over.NoOpReason == "type>=8", "1046 type>=8 -> noop");

    var masked = NativeShopQuery.EvaluateReqSeeShop(0, true, true, true);
    Assert(masked.IsNoOp && masked.NoOpReason == "sent-mask already set",
        "1046 mask-set -> noop");

    var both = NativeShopQuery.EvaluateReqSeeShop(3, false, true, true);
    Assert(both.SentShopItems && both.SentFirstShop && both.SentMaskSet,
        "1046 both lists -> 812+815+mask");

    var empty = NativeShopQuery.EvaluateReqSeeShop(7, false, false, false);
    Assert(!empty.SentShopItems && !empty.SentFirstShop && empty.SentMaskSet,
        "1046 empty lists still stamp the sent-mask");

    Assert(NativeShopQuery.EvaluateRenewSeeShop(8, true) == NativeShopEmit.None,
        "1047 type>=8 -> none");
    Assert(NativeShopQuery.EvaluateRenewSeeShop(2, true) == NativeShopEmit.ReShopItemsOk,
        "1047 list present -> 813");
    Assert(NativeShopQuery.EvaluateRenewSeeShop(2, false) == NativeShopEmit.ReShopItemsFail,
        "1047 list empty -> 814");

    Assert(!NativeShopWriteTransaction.NoGoPaymentSettlement(),
        "SeeShop payment settlement stays YBDB-6108 NO-GO");
}

void CheckCm1054StayClosed()
{
    Assert(NativeCmQ1FailClosed.All.TryGetValue(Grobal2.CM_1054, out var entry),
        "CM 1054 missing from Q1 fail-closed table");
    Equal(NativeShopQuery.Cm1054LeafEa, entry.HandlerVa, "Q1 1054 leaf VA");
    Equal(NativeShopQuery.SubmitWrapperEa, entry.CalleeVa, "Q1 1054 callee VA");
    Require(entry.Blocker, "[0x7D5D98]", "CM 1054 blocker must name shopMgr");
    Require(entry.Blocker, "0x637A00", "CM 1054 blocker must name enqueue");
    Equal("商城/mall 提交", entry.Subsystem, "CM 1054 subsystem label");
}

void CheckQuerySliceWired(string repoRoot)
{
    var mall = Read(repoRoot, "GameSvr", "Players", "TPlayObject.Mall.cs");
    Require(mall, "NativeShopQuery.EvaluateReqSeeShop",
        "ClientQueryWhitePigMall must consult NativeShopQuery.EvaluateReqSeeShop");
    Require(mall, "NativeShopQuery.EvaluateRenewSeeShop",
        "ClientRefreshWhitePigMall must consult NativeShopQuery.EvaluateRenewSeeShop");
    Require(mall, "NativeShopQueryCodec.RecordSize",
        "Mall codec must use NativeShopQueryCodec.RecordSize");
    Require(mall, "SM_SHOPITEMS", "1046 must still emit SM_SHOPITEMS");
    Require(mall, "SM_FIRSTSHOP", "1046 must still emit SM_FIRSTSHOP");
    Require(mall, "SM_RESHOPITEMS_OK", "1047 must still emit SM_RESHOPITEMS_OK");
    Require(mall, "SM_RESHOPITEMS_FAIL", "1047 must still emit SM_RESHOPITEMS_FAIL");

    var operate = Read(repoRoot, "GameSvr", "Players", "TPlayObject.Message.cs");
    Require(operate, "ClientQueryWhitePigMall(ProcessMsg.nParam1)",
        "Operate must keep CM_REQSEESHOP on ClientQueryWhitePigMall");
    Require(operate, "ClientRefreshWhitePigMall(ProcessMsg.nParam1)",
        "Operate must keep CM_RENEWSEESHOP on ClientRefreshWhitePigMall");

    var mallCm = Read(repoRoot, "GameSvr", "Players", "TPlayObject.MallCm.cs");
    Reject(mallCm, "ClientQueryWhitePigMall",
        "CM 1054 MallCm must not invent a 1046 catalog reply");
    Reject(mallCm, "ClientRefreshWhitePigMall",
        "CM 1054 MallCm must not invent a 1047 catalog reply");
    Reject(mallCm, "PurchaseItemByName",
        "CM 1054 MallCm must not purchase");
}

void CheckNoIdent125OrYbDebit(string repoRoot)
{
    var query = Read(repoRoot, "GameSvr", "Services", "NativeShopQuery.cs");
    Reject(query, "0x2742", "NativeShopQuery invented Ident-125 selector 10050");
    Reject(query, "ConsumeYB", "NativeShopQuery invented YB debit");
    Reject(query, "m_nGameGold", "NativeShopQuery invented gold debit");
    Require(query, "ShopMgrPointerMapped => false",
        "NativeShopQuery must keep shopMgr pointer unmapped");
    Require(query, "Do NOT invent the",
        "NativeShopQuery must keep the Ident-125 / YB-debit invention warning");
}

static string Read(string repoRoot, params string[] parts)
{
    var path = Path.Combine(new[] { repoRoot }.Concat(parts).ToArray());
    return File.ReadAllText(path);
}

void Require(string source, string needle, string message)
{
    Assert(source.IndexOf(needle, StringComparison.Ordinal) >= 0, message);
}

void Reject(string source, string needle, string message)
{
    Assert(source.IndexOf(needle, StringComparison.Ordinal) < 0, message);
}

void Equal<T>(T expected, T actual, string message)
{
    Assert(EqualityComparer<T>.Default.Equals(expected, actual),
        message + $" expected={expected} actual={actual}");
}

void Assert(bool cond, string msg)
{
    if (!cond) throw new Exception(msg);
    asserts++;
}
