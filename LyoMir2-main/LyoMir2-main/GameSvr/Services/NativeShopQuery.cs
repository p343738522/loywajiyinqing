namespace GameSvr
{
    // C4 P1: LOCAL SeeShop read/query slices (CM_REQSEESHOP 1046 / sub_63A254
    // and CM_RENEWSEESHOP 1047 / sub_63A32C).
    //
    // Manager + codec that already exist:
    //   MallManager           — per-type lists and the hot list (native shop[+36*type+84]
    //                           / shop[+0x158]) loaded from PAS @GetYBShopConfig
    //   TPlayObject.Mall.cs   — 180-byte TClientShop encoder (sub_636D68 @ record+0x58)
    //   NativeShopWriteTransaction.RenderReqSeeShop — the type<8 / sent-mask / 812+815 ladder
    //   NativeShopWriteTransaction.RenderRenewSeeShop — the type<8 / 813/814 ladder
    //
    // shopMgr pointer [[0x7D5D98]] is NOT mapped as a live C# singleton. Native CM 1054
    // (leaf 0x6D942F) packs a header through sub_6D3694 and enqueues subcmd 0x7B via
    // shopMgr vtable 0x637A00; that enqueue returns true only when [chan+0x2c] != 0.
    // This port never constructs that object or raises the active flag
    // (NativeMallSubmitChannel.IsActive is permanently false). Do NOT invent the
    // async success reply, Ident-125, or a YB debit for 1054-1057 / 1048.
    //
    // KEEP FAIL-CLOSED: CM 1054 stays on the unmapped-channel path (MallCm busy SysMsg
    // when enqueue returns false; NativeCmQ1FailClosed still records the ident). The
    // catalog queries wired here are 1046 and 1047, different native arms from 1054.

    /// <summary>180-byte TClientShop field offsets, filled by native sub_636D68.</summary>
    public static class NativeShopQueryCodec
    {
        public const int RecordSize = 180;          // native 180-byte TClientShop
        public const int NameOffset = 0;            // ShortString[15]  cl=0x0F @0x637157
        public const int NameCapacity = 15;
        public const int CategoryNameOffset = 16;   // ShortString[15]  cl=0x0F @0x63717A
        public const int CategoryNameCapacity = 15;
        public const int LooksOffset = 32;          // word; patched by 1101 Looks fill
        public const int PageOffset = 34;           // word; send-side category/page
        public const int SrcPriceOffset = 36;       // word vSrcPrice   @0x637191
        public const int CurPriceOffset = 38;       // word vCurPrice   @0x637199
        public const int LimitTypeOffset = 40;      // word vLimitType  @0x6371A1
        public const int LimitCountOffset = 42;     // word vLimitCount @0x6371A9
        public const int CurrentLimitOffset = 44;   // word; sub_63CD0C backfill
        public const int EffectCountOffset = 46;    // word vEffectCount @0x6371BD
        public const int EffectImgOffset = 48;      // dword vEffectImg  @0x6371B6
        public const int DescriptionOffset = 52;    // ShortString[127] cl=0x7F @0x6371DF
        public const int DescriptionCapacity = 127;
        public const int HotRecordCount = 5;        // SM_FIRSTSHOP 900-byte slots
        public const int HotBodySize = HotRecordCount * RecordSize;
    }

    public static class NativeShopQuery
    {
        /// <summary>shopMgr global pointer off_7D5D98. Unmapped in this port.</summary>
        public const uint ShopMgrVa = 0x007D5D98;

        /// <summary>sub_637A00 — shopMgr enqueue. Needs [chan+0x2c] != 0.</summary>
        public const uint EnqueueEa = 0x00637A00;

        /// <summary>sub_6D3694 — CM 1054 submit wrapper that calls [[0x7D5D98]]/0x637A00.</summary>
        public const uint SubmitWrapperEa = 0x006D3694;

        /// <summary>CM 1054 leaf 0x6D942F.</summary>
        public const uint Cm1054LeafEa = 0x006D942F;

        /// <summary>CM 1054 subcmd written into the 0x6D3694 header (ESI=0x7B).</summary>
        public const int Cm1054Subcmd = 0x7B;

        /// <summary>sub_63A254 — LOCAL REQSEESHOP render (CM 1046).</summary>
        public const uint ReqSeeShopEa = 0x0063A254;

        /// <summary>sub_63A32C — LOCAL RENEWSEESHOP render (CM 1047).</summary>
        public const uint RenewSeeShopEa = 0x0063A32C;

        /// <summary>shop[+36*type+84] per-type list pointer base.</summary>
        public const int PerTypeListBase = 84;

        /// <summary>36-byte stride of the native per-type shop slot.</summary>
        public const int PerTypeListStride = 36;

        /// <summary>shop[+0x158] hot-list pointer.</summary>
        public const int HotListOffset = 0x158;

        public const int MaxShopType = NativeShopWriteTransaction.MaxShopType;
        public const int CmReqSeeShop = (int)NativeShopWriteOp.ReqSeeShop;
        public const int CmRenewSeeShop = (int)NativeShopWriteOp.RenewSeeShop;
        public const int SentMaskOffset = NativeShopWriteTransaction.SentMaskOffset;

        /// <summary>
        /// [[0x7D5D98]] is not a C# object. 0x637A00 cannot run, so CM 1054's
        /// async success reply is unreachable. Do not flip this.
        /// </summary>
        public static bool ShopMgrPointerMapped => false;

        /// <summary>MallManager + 180-byte TClientShop encoder cover the local catalog.</summary>
        public static bool CatalogMapped => true;

        public static int PerTypeListOffset(int type) => PerTypeListBase + PerTypeListStride * type;

        public static int SentMaskBit(int type) => 2 << type;

        /// <summary>
        /// CM 1046 / sub_63A254 decision. Live ClientQueryWhitePigMall consults this
        /// before emitting SM_SHOPITEMS(812) / SM_FIRSTSHOP(815).
        /// </summary>
        public static NativeReqSeeShopResult EvaluateReqSeeShop(
            int requestedType, bool sentMaskAlreadySet,
            bool perTypeListPresent, bool hotListPresent)
        {
            return NativeShopWriteTransaction.RenderReqSeeShop(new NativeReqSeeShopContext
            {
                RequestedType = requestedType,
                SentMaskAlreadySet = sentMaskAlreadySet,
                PerTypeListPresent = perTypeListPresent,
                HotListPresent = hotListPresent
            });
        }

        /// <summary>
        /// CM 1047 / sub_63A32C decision. Live ClientRefreshWhitePigMall consults this
        /// before emitting SM_RESHOPITEMS_OK(813) / SM_RESHOPITEMS_FAIL(814). Not
        /// sent-mask gated. Does not consult shopMgr [[0x7D5D98]].
        /// </summary>
        public static NativeShopEmit EvaluateRenewSeeShop(int requestedType, bool perTypeListPresent)
        {
            return NativeShopWriteTransaction.RenderRenewSeeShop(new NativeRenewSeeShopContext
            {
                RequestedType = requestedType,
                PerTypeListPresent = perTypeListPresent
            });
        }
    }
}
