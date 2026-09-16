namespace GameSvr
{
    public partial class TPlayObject
    {
        /// <summary>
        /// 战神 [self+0x9CC]：QueryGloryPointByGoodsNum 写入的卖出挂单 goodsType。
        /// </summary>
        internal int m_nSuperMerchantSellGoodsType;

        /// <summary>
        /// 战神 [self+0x9D0]：QueryGloryPointByGoodsNum 写入的卖出挂单荣耀报价。
        /// </summary>
        internal int m_nSuperMerchantSellGloryQuote;

        // Unmapped SuperMerchant YB-buy native slots — KEEP CLOSED.
        //   [self+0x9D4]/[+0x9D8] QueryGoodsNumByYBNum hang tags (sub_6E4F88)
        //   [self+0x9DC]/[+0x9E0] ConsumeYBToBuyGoods delivery ticket (sub_6E5104)
        // MISSING HOOK: Ident-125 reply body (sub_6D3694 Ident=0x7D sel=0x2742)
        // + delivery executor sub_6D5344 SuperMerchant branch @0x6D56E0
        // (read +0x9DC in 1..2 → sub_6C87B4). No C# type, no SM builder,
        // no YbDbClient 125/sel=10050 reply body. Do not add these fields
        // until that executor and reply body exist together.
    }
}
