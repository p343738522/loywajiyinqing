using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SystemModule;

namespace GameSvr.Services
{
    /// <summary>
    /// 大药商人 <c>TSuperMerchant</c>：900s 库存刷新广播 <c>sub_6160A4</c>，
    /// ini 读 <c>sub_616258</c> / 写 <c>sub_616484</c>，全局实例原生 <c>0x7D6D10</c>。
    /// </summary>
    public sealed class NativeSuperMerchantManager
    {
        public const uint TickEa = 0x006160A4;
        public const uint LoadIniEa = 0x00616258;
        public const uint SaveIniEa = 0x00616484;
        public const uint UnitPriceEa = 0x006161EC;   // sub_6161EC
        public const uint GloryQuoteEa = 0x0061617C;  // sub_61617C
        public const uint CommitAddEa = 0x00615F44;   // sub_615F44
        public const uint QueryGloryEa = 0x006E4F60;  // QueryGloryPointByGoodsNum
        public const uint SellGloryEa = 0x006E4FB0;   // SellGoodsToGetGloryPoint
        public const uint BuyYbEa = 0x006E5104;       // ConsumeYBToBuyGoods CLOSED: missing Ident-125 reply body + delivery sub_6D5344@0x6D56E0
        public const int RefreshIntervalMs = 0xDBBA0; // 900000 ms @0x6160AF
        // 0x616254 = 00 00 B3 42 (single). 0x616248 tbyte documented as 10.242.
        public const float BasePriceSingle = 89.5f;
        public const double LnCoefficient = 10.242;

        public const int SellMismatch = -1;
        public const int SellBagShort = -2;
        public const int SellStorageRejected = -3;
        public const int SellTakeFailed = -4;
        public const int SellOk = 1;

        private const string IniRelativePath = "Config\\SuperMerchant.ini";
        private const string BroadcastTitle = "大药商人"; // 0x61612C
        private const string StockFmt = "大药商人当前库存 %s %d"; // 0x616154

        private static readonly string[] DefaultGoodsNames =
        {
            string.Empty,
            "疗伤药包",   // type 1 @0x7B45AC
            "万年雪霜包"  // type 2
        };

        private readonly object _sync = new();
        private readonly NativeSuperMerchantSlot[] _slots =
        {
            new(), // index 0 unused; native loops ebx=1..2
            new(20, 2500, 1000, "疗伤药包"), // ctor defaults @0x615FC4
            new(20, 2500, 1000, "万年雪霜包")
        };

        private int _lastTick;
        private bool _dirty;

        public bool IsAvailable => true;

        public void EnsureLoaded(string shareDirectory)
        {
            lock (_sync)
            {
                TryLoad(Path.Combine(shareDirectory ?? string.Empty, IniRelativePath));
                if (_lastTick == 0)
                    _lastTick = HUtil32.GetTickCount();
            }
        }

        /// <summary><c>sub_6160A4(Self, now)</c></summary>
        public void RunTick(int nowTick)
        {
            lock (_sync)
            {
                if (unchecked((uint)(nowTick - _lastTick)) < RefreshIntervalMs)
                    return;

                _lastTick = nowTick;
                if (!_dirty)
                    return;

                SaveIfDirty(Path.Combine(M2Share.sRootPath ?? string.Empty,
                    M2Share.g_Config?.sBaseDir ?? "Share", IniRelativePath));

                BroadcastRestock();
                _dirty = false;
            }
        }

        internal void MarkDirty() => _dirty = true;

        /// <summary>
        /// Applies the three-field stock update accepted by the native GM command.
        /// Goods types are 1 (healing pack) and 2 (snow-flower pack); storage types
        /// are 1 (minimum), 2 (maximum), and 3 (current).
        /// </summary>
        public bool TrySetStock(int goodsType, int storageType, int amount)
        {
            lock (_sync)
            {
                if (goodsType is < 1 or > 2 ||
                    storageType is < 1 or > 3 ||
                    amount <= 0)
                    return false;

                var slot = _slots[goodsType];
                switch (storageType)
                {
                    case 1:
                        slot.Min = amount;
                        break;
                    case 2:
                        slot.Max = amount;
                        break;
                    default:
                        slot.Current = amount;
                        break;
                }

                _dirty = true;
                return true;
            }
        }

        public string GetGoodsName(int goodsType)
        {
            lock (_sync)
                return GetGoodsNameUnlocked(goodsType);
        }

        public int GetCurrentStorage(int goodsType)
        {
            lock (_sync)
            {
                if (goodsType is < 1 or > 2)
                    return 0;
                return _slots[goodsType].Current;
            }
        }

        /// <summary>
        /// sub_615F44: <c>Current := Min(Current+delta, Max)</c>, dirty:=1, return 1.
        /// Silent saturate at Max; type outside 1..2 returns 0.
        /// </summary>
        public bool TryCommitAdd(int goodsType, int delta)
        {
            lock (_sync)
            {
                if (goodsType is < 1 or > 2)
                    return false;
                var slot = _slots[goodsType];
                var next = unchecked(slot.Current + delta);
                if (next > slot.Max)
                    next = slot.Max;
                slot.Current = next;
                _dirty = true;
                return true;
            }
        }

        /// <summary>
        /// sub_6161EC: type ∉ {1,2} → 0.0; else 89.5 − 10.242 × ln(Current).
        /// Current ≤ 0 is outside ln's domain (native fyl2x of 0 is −inf); C# returns 0.
        /// </summary>
        public double ComputeUnitPrice(int goodsType)
        {
            lock (_sync)
                return ComputeUnitPriceUnlocked(goodsType);
        }

        /// <summary>
        /// sub_61617C: truncating fistp of unitPrice×qty (sub_403580, RC=11).
        /// </summary>
        public int ComputeGloryQuote(int goodsType, int goodsNum)
        {
            lock (_sync)
                return TruncateNative(ComputeUnitPriceUnlocked(goodsType) * goodsNum);
        }

        private string GetGoodsNameUnlocked(int goodsType)
        {
            if (goodsType is < 1 or > 2)
                return string.Empty;
            var name = _slots[goodsType].ItemName;
            return string.IsNullOrEmpty(name) ? DefaultGoodsNames[goodsType] : name;
        }

        private double ComputeUnitPriceUnlocked(int goodsType)
        {
            if (goodsType is < 1 or > 2)
                return 0.0;
            var current = _slots[goodsType].Current;
            if (current <= 0)
                return 0.0;
            return (double)BasePriceSingle - LnCoefficient * Math.Log(current);
        }

        private static int TruncateNative(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0;
            return (int)value;
        }

        private void BroadcastRestock()
        {
            // 0x6160D4..0x616117: two world broadcasts via sub_79D3D8, dx=0xA.
            for (var type = 1; type <= 2; type++)
            {
                var name = GetGoodsNameUnlocked(type);
                var current = _slots[type].Current;
                var msg = string.Format(StockFmt.Replace("%s", "{0}").Replace("%d", "{1}"),
                    name, current);
                M2Share.UserEngine?.SendBroadCastMsg(msg, MsgType.System);
            }
        }

        private void TryLoad(string path)
        {
            if (!File.Exists(path))
                return;

            var lines = File.ReadAllLines(path);
            string section = null;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                    continue;
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    section = line[1..^1];
                    continue;
                }
                var eq = line.IndexOf('=');
                if (eq <= 0 || section == null)
                    continue;
                if (!TryMapSection(section, out var type))
                    continue;
                var key = line[..eq].Trim();
                var val = line[(eq + 1)..].Trim();
                if (key.Equals("ItemName", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(val))
                        _slots[type].ItemName = val;
                    continue;
                }
                if (!int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out var n))
                    continue;
                switch (key)
                {
                    case "MinStorage":
                        _slots[type].Min = n;
                        break;
                    case "MaxStorage":
                        _slots[type].Max = n;
                        break;
                    case "CurrentStorage":
                        _slots[type].Current = n;
                        break;
                }
            }
        }

        private void SaveIfDirty(string path)
        {
            if (!_dirty)
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var lines = new List<string>();
            for (var type = 1; type <= 2; type++)
            {
                lines.Add($"[GoodsInfo{type}]");
                lines.Add($"ItemName={GetGoodsNameUnlocked(type)}");
                lines.Add($"MinStorage={_slots[type].Min}");
                lines.Add($"MaxStorage={_slots[type].Max}");
                lines.Add($"CurrentStorage={_slots[type].Current}");
                lines.Add(string.Empty);
            }
            File.WriteAllLines(path, lines);
        }

        private static bool TryMapSection(string section, out int type)
        {
            type = 0;
            if (!section.StartsWith("GoodsInfo", StringComparison.OrdinalIgnoreCase))
                return false;
            return int.TryParse(section["GoodsInfo".Length..],
                NumberStyles.Integer, CultureInfo.InvariantCulture, out type)
                   && type is >= 1 and <= 2;
        }

        private sealed class NativeSuperMerchantSlot
        {
            internal NativeSuperMerchantSlot() { }
            internal NativeSuperMerchantSlot(int min, int max, int current,
                string itemName)
            {
                Min = min;
                Max = max;
                Current = current;
                ItemName = itemName ?? string.Empty;
            }

            public int Min { get; set; }
            public int Max { get; set; }
            public int Current { get; set; }
            public string ItemName { get; set; } = string.Empty;
        }
    }
}
