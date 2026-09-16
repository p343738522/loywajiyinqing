using SystemModule;
using GameSvr.Services;

namespace GameSvr.PasEngine
{
    public partial class PasApiBridge
    {
        private bool CallQueryGloryPointByGoodsNum(List<PasValue> args,
            out PasValue result)
        {
            result = PasValue.Nil;
            if (args == null || args.Count != 2)
                return false;
            result = PasValue.FromInt(QueryNativeSuperMerchantGloryQuote(
                args[0].AsInt(), args[1].AsInt()));
            return true;
        }

        private bool CallSellGoodsToGetGloryPoint(List<PasValue> args,
            out PasValue result)
        {
            result = PasValue.Nil;
            if (args == null || args.Count != 2)
                return false;
            result = PasValue.FromInt(SellNativeSuperMerchantGoods(
                args[0].AsInt(), args[1].AsInt()));
            return true;
        }

        /// <summary>
        /// QueryGloryPointByGoodsNum sub_6E4F60: quote via sub_61617C, stash
        /// goodsType→[self+0x9CC] and glory→[self+0x9D0], return the glory.
        /// Native has no manager-null gate (would AV); C# returns 0.
        /// </summary>
        private int QueryNativeSuperMerchantGloryQuote(int goodsType, int goodsNum)
        {
            var manager = M2Share.SuperMerchantManager;
            if (manager == null)
                return 0;
            var glory = manager.ComputeGloryQuote(goodsType, goodsNum);
            CurrentPlayer.m_nSuperMerchantSellGoodsType = goodsType;
            CurrentPlayer.m_nSuperMerchantSellGloryQuote = glory;
            return glory;
        }

        /// <summary>
        /// SellGoodsToGetGloryPoint sub_6E4FB0. Order is native: recalc → pending
        /// check → bag count → <b>commit storage</b> → take items → add glory
        /// (sub_6E2108 / TryAddNativeGloryPoint) → type-9 log → clear pending.
        /// </summary>
        private int SellNativeSuperMerchantGoods(int goodsType, int goodsNum)
        {
            var manager = M2Share.SuperMerchantManager;
            if (manager == null)
                return NativeSuperMerchantManager.SellMismatch;

            var glory = manager.ComputeGloryQuote(goodsType, goodsNum);
            if (goodsType != CurrentPlayer.m_nSuperMerchantSellGoodsType
                || glory != CurrentPlayer.m_nSuperMerchantSellGloryQuote)
                return NativeSuperMerchantManager.SellMismatch;

            var itemName = manager.GetGoodsName(goodsType);
            if (CountBagItem(itemName) < goodsNum)
                return NativeSuperMerchantManager.SellBagShort;

            if (!manager.TryCommitAdd(goodsType, goodsNum))
                return NativeSuperMerchantManager.SellStorageRejected;

            if (!TakeItemsCore(itemName, goodsNum))
                return NativeSuperMerchantManager.SellTakeFailed;

            if (glory > 0)
                _ = TryAddNativeGloryPoint(glory);

            M2Share.AddGameDataLog(string.Join('\t', 9,
                CurrentPlayer.m_sMapName, CurrentPlayer.m_nCurrX,
                CurrentPlayer.m_nCurrY, CurrentPlayer.m_sCharName,
                itemName, goodsType, glory, "大药商人"));
            CurrentPlayer.m_nSuperMerchantSellGoodsType = 0;
            CurrentPlayer.m_nSuperMerchantSellGloryQuote = 0;
            return NativeSuperMerchantManager.SellOk;
        }
    }
}
