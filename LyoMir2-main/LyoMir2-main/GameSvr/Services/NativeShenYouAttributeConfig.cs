using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using SystemModule;

namespace GameSvr
{
    /// <summary>
    /// Loader for Config\神佑属性.txt — native sub_755350 @0x755350.
    /// Each non-comment line with '=' parses into a 0x2B-byte record:
    ///   +0x00 int, +0x04 int (base value for 0x747B38), +0x08 int,
    ///   +0x0C ShortString[0x1E] name.
    /// Records are stored in the singleton table [[0x7D6014]]; slot cap
    /// [[0x7D5AEC]] is set to 4 at 0x7553F9.
    /// CM 4125 / sub_746C34 is the read-only table broadcast (SM 4032 + SM 4038).
    /// </summary>
    public sealed class NativeShenYouAttributeEntry
    {
        public int Id { get; init; }
        public int BaseValue { get; init; }
        public int Param3 { get; init; }
        public string Name { get; init; }
    }

    public readonly struct NativeShenYouTableQueryResult
    {
        public NativeShenYouTableQueryResult(bool sendTable, int recog,
            ushort tag, ushort flagParam, byte[] body)
        {
            SendTable = sendTable;
            Recog = recog;
            Tag = tag;
            FlagParam = flagParam;
            Body = body ?? Array.Empty<byte>();
        }

        public bool SendTable { get; }
        public int Recog { get; }
        public ushort Tag { get; }
        public ushort FlagParam { get; }
        public byte[] Body { get; }
    }

    public sealed class NativeShenYouAttributeConfig
    {
        public const string ConfigRelativePath = @"Share\config\神佑属性.txt";
        public const int NativeRecordSize = 0x2B;
        public const int NativeMaxSlots = 4;
        public const int NameOffset = 0x0C;
        public const int NameCapacity = 0x1E;
        public const uint TableVa = 0x007D6014;
        public const uint SlotCapVa = 0x007D5AEC;
        public const uint FlagVa = 0x007D6938;
        public const uint QueryWorkerEa = 0x00746C34;
        public const int CmQuery = 4125;
        public const int SmTable = 4032;
        public const int SmFlag = 4038;

        /// <summary>
        /// [[0x7D6938]] is not a C# singleton. SM 4038 Param stays 0; do not invent 1.
        /// </summary>
        public static bool FlagByteMapped => false;

        private static readonly NativeShenYouAttributeConfig _shared =
            new NativeShenYouAttributeConfig();

        public static NativeShenYouAttributeConfig Shared => _shared;

        private readonly object _sync = new();
        private readonly Dictionary<int, NativeShenYouAttributeEntry> _byId =
            new Dictionary<int, NativeShenYouAttributeEntry>();
        private readonly List<NativeShenYouAttributeEntry> _order = new();

        public int Count
        {
            get { lock (_sync) { return _order.Count; } }
        }

        public static string ResolveDefaultPath(string rootPath, string baseDir)
        {
            return Path.Combine(rootPath ?? string.Empty, baseDir ?? string.Empty,
                "config", "神佑属性.txt");
        }

        public bool TryGet(int id, out NativeShenYouAttributeEntry entry)
        {
            lock (_sync)
                return _byId.TryGetValue(id, out entry);
        }

        public NativeShenYouAttributeEntry[] Snapshot()
        {
            lock (_sync)
                return _order.ToArray();
        }

        /// <summary>
        /// CM 4125 / 0x746C4A jle: count&lt;=0 sends nothing. Else SM 4032 Recog=count
        /// Tag=[[0x7D5AEC]] body=count*0x2B, then SM 4038 Param=[[0x7D6938]]!=0.
        /// </summary>
        public static NativeShenYouTableQueryResult Evaluate(
            IReadOnlyList<NativeShenYouAttributeEntry> rows)
        {
            if (rows == null || rows.Count <= 0)
                return new NativeShenYouTableQueryResult(false, 0, 0, 0,
                    Array.Empty<byte>());

            return new NativeShenYouTableQueryResult(true, rows.Count, NativeMaxSlots,
                FlagByteMapped ? (ushort)1 : (ushort)0, EncodeTable(rows));
        }

        public static byte[] EncodeRecord(NativeShenYouAttributeEntry entry)
        {
            var record = new byte[NativeRecordSize];
            if (entry == null)
                return record;

            BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(0, 4), entry.Id);
            BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(4, 4), entry.BaseValue);
            BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(8, 4), entry.Param3);
            WriteName(record.AsSpan(NameOffset, NameCapacity + 1), entry.Name);
            return record;
        }

        public static byte[] EncodeTable(IReadOnlyList<NativeShenYouAttributeEntry> rows)
        {
            if (rows == null || rows.Count <= 0)
                return Array.Empty<byte>();

            var body = new byte[rows.Count * NativeRecordSize];
            for (var i = 0; i < rows.Count; i++)
                EncodeRecord(rows[i]).CopyTo(body, i * NativeRecordSize);
            return body;
        }

        private static void WriteName(Span<byte> dest, string name)
        {
            dest.Clear();
            var bytes = HUtil32.GbkEncoding.GetBytes(name ?? string.Empty);
            var n = Math.Min(bytes.Length, NameCapacity);
            dest[0] = unchecked((byte)n);
            if (n > 0)
                bytes.AsSpan(0, n).CopyTo(dest.Slice(1));
        }

        /// <summary>0x747B38 — sum [entry+4] for each non-zero slot word.</summary>
        public int ComputeBaseFromSlots(ReadOnlySpan<ushort> slotIds)
        {
            lock (_sync)
            {
                var total = 0;
                for (var i = 0; i < slotIds.Length; i++)
                {
                    var id = slotIds[i];
                    if (id == 0)
                        continue;
                    if (!_byId.TryGetValue(id, out var entry))
                        return -1;
                    total += entry.BaseValue;
                }
                return total;
            }
        }

        public bool Reload(string fileName, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
            {
                lock (_sync)
                {
                    _byId.Clear();
                    _order.Clear();
                }
                error = "[Error]:神佑属性文件不存在！！";
                M2Share.ErrorMessage(error);
                return false;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(fileName, HUtil32.GbkEncoding);
            }
            catch (Exception ex)
            {
                lock (_sync)
                {
                    _byId.Clear();
                    _order.Clear();
                }
                error = "[Error]:神佑属性文件加载错误: " + ex.Message;
                M2Share.ErrorMessage(error);
                return false;
            }

            lock (_sync)
            {
                _byId.Clear();
                _order.Clear();

                foreach (var raw in lines)
                {
                    var line = raw?.Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;
                    if (line[0] == ';' || line[0] == '/')
                        continue;

                    if (!TryParseLine(line, out var entry, out var lineError))
                    {
                        error = "[Error]:神佑属性文件加载错误: " + lineError;
                        M2Share.ErrorMessage(error);
                        return false;
                    }

                    if (_byId.ContainsKey(entry.Id))
                    {
                        error = "[Error]:神佑属性文件加载错误: duplicate id " + entry.Id;
                        M2Share.ErrorMessage(error);
                        return false;
                    }

                    _byId[entry.Id] = entry;
                    _order.Add(entry);
                    if (_order.Count > NativeMaxSlots * 64)
                        break;
                }
            }

            return true;
        }

        private static bool TryParseLine(string line,
            out NativeShenYouAttributeEntry entry, out string error)
        {
            entry = null;
            error = string.Empty;

            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                error = "missing '=' in: " + line;
                return false;
            }

            var name = line.Substring(0, eq).Trim();
            var rest = line.Substring(eq + 1);
            var parts = rest.Split('|');
            if (parts.Length < 3)
            {
                error = "need id|base|param: " + line;
                return false;
            }

            if (!int.TryParse(parts[0].Trim(), out var id)
                || !int.TryParse(parts[1].Trim(), out var baseValue)
                || !int.TryParse(parts[2].Trim(), out var param3))
            {
                error = "bad numeric fields: " + line;
                return false;
            }

            if (string.IsNullOrEmpty(name))
                name = id.ToString();

            var nameBytes = HUtil32.GbkEncoding.GetBytes(name);
            if (nameBytes.Length > 0x1E)
            {
                error = "name too long: " + name;
                return false;
            }

            entry = new NativeShenYouAttributeEntry
            {
                Id = id,
                BaseValue = baseValue,
                Param3 = param3,
                Name = name
            };
            return true;
        }
    }
}
