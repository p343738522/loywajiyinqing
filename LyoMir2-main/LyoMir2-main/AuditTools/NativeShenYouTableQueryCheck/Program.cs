// Pins CM 4125 / sub_746C34: empty table is silent; otherwise SM 4032
// (count*0x2B rows) then SM 4038 (empty body, Param=[[0x7D6938]]!=0).
// [[0x7D6938]] is not a C# singleton, so Param stays 0. Native UserLogon
// does not emit 4038; 3556/4367 already leave through NativeColdTime.
using System.Buffers.Binary;
using GameSvr;
using GameSvr.Services;
using SystemModule;

var empty = NativeShenYouAttributeConfig.Evaluate(Array.Empty<NativeShenYouAttributeEntry>());
Assert(!empty.SendTable, "empty table must send nothing (0x746C4A jle)");
Equal(0, empty.Recog, "empty Recog");
Equal(0, empty.Body.Length, "empty body");

var row = new NativeShenYouAttributeEntry
{
    Id = 7,
    BaseValue = 12,
    Param3 = 3,
    Name = "甲"
};
var live = NativeShenYouAttributeConfig.Evaluate(new[] { row });
Assert(live.SendTable, "non-empty table must send");
Equal(1, live.Recog, "SM 4032 Recog=count");
Equal((ushort)NativeShenYouAttributeConfig.NativeMaxSlots, live.Tag,
    "SM 4032 Tag=[[0x7D5AEC]]=4");
Assert(!NativeShenYouAttributeConfig.FlagByteMapped,
    "[[0x7D6938]] is not mapped; do not invent Param=1");
Equal((ushort)0, live.FlagParam, "SM 4038 Param stays 0");
Equal(NativeShenYouAttributeConfig.NativeRecordSize, live.Body.Length,
    "one 0x2B row");
Equal(7, BinaryPrimitives.ReadInt32LittleEndian(live.Body.AsSpan(0, 4)),
    "row +0x00 id");
Equal(12, BinaryPrimitives.ReadInt32LittleEndian(live.Body.AsSpan(4, 4)),
    "row +0x04 base");
Equal(3, BinaryPrimitives.ReadInt32LittleEndian(live.Body.AsSpan(8, 4)),
    "row +0x08 param3");
var nameBytes = HUtil32.GbkEncoding.GetBytes("甲");
Equal((byte)nameBytes.Length, live.Body[0x0C], "ShortString length");
for (var i = 0; i < nameBytes.Length; i++)
    Equal(nameBytes[i], live.Body[0x0D + i], "ShortString char");
for (var i = 0x0D + nameBytes.Length; i < NativeShenYouAttributeConfig.NativeRecordSize; i++)
    Equal((byte)0, live.Body[i], "record tail is zero, not uninit padding");

var table = TBaseObject.BuildSm4032(live.Recog, live.Tag, live.Body);
Equal((ushort)4032, table.Header.Ident, "SM 4032 ident");
Equal(1, table.Header.Recog, "SM 4032 Recog");
Equal((ushort)0, table.Header.Param, "SM 4032 Param");
Equal((ushort)4, table.Header.Tag, "SM 4032 Tag");
Equal((ushort)0, table.Header.Series, "SM 4032 Series");
Equal(live.Body.Length, table.Body.Length, "SM 4032 body length");

var flag = TBaseObject.BuildSm4038(live.FlagParam);
Equal((ushort)4038, flag.Header.Ident, "SM 4038 ident");
Equal(0, flag.Header.Recog, "SM 4038 Recog");
Equal((ushort)0, flag.Header.Param, "SM 4038 Param");
Equal((ushort)0, flag.Header.Tag, "SM 4038 Tag");
Equal((ushort)0, flag.Header.Series, "SM 4038 Series");
Equal(0, flag.Body.Length, "SM 4038 empty body");

var armed = TBaseObject.BuildSm4038(1);
Equal((ushort)1, armed.Header.Param, "mapped Param=1 path @0x746D28");
Equal(0, armed.Body.Length, "armed flag still empty");

var root = FindRepositoryRoot();
var tail = File.ReadAllText(Path.Combine(root, "GameSvr", "Players",
    "TPlayObject.NativeCmTailProtocol.cs"));
Require(tail, "NativeShenYouAttributeConfig.Evaluate(",
    "CM 4125 must use the table planner");
Require(tail, "BuildSm4032(result.Recog, result.Tag, result.Body)",
    "CM 4125 must emit SM 4032 first");
Require(tail, "BuildSm4038(result.FlagParam)",
    "CM 4125 must emit SM 4038 flag notify");
Reject(tail, "Drop(Grobal2.CM_4125", "CM 4125 remained fail-closed");

var ledger = File.ReadAllText(Path.Combine(root, "GameSvr", "Services",
    "NativeCmTailFailClosed.cs"));
Reject(ledger, "Add(4125,", "CM 4125 fail-closed ledger entry");

var logon = File.ReadAllText(Path.Combine(root, "GameSvr", "Players",
    "TPlayObject.NativeLogonStateSync.cs"));
Reject(logon, "BuildSm4038",
    "UserLogon must not emit SM 4038; native cluster is 3324/1264/3554/optional3556");
var heroLogon = File.ReadAllText(Path.Combine(root, "GameSvr", "Actors",
    "HeroObject.NativeLogonStateSync.cs"));
Reject(heroLogon, "BuildSm4038",
    "hero logon must not emit SM 4038; native cluster is 3324/optional4367");

Console.WriteLine(
    "PASS NativeShenYouTableQueryCheck cm=4125 sm=4032(count*0x2B)+4038(param0) " +
    "empty=silent logon=no-4038");
return;

static string FindRepositoryRoot()
{
    foreach (var start in new[]
             {
                 Environment.CurrentDirectory, AppContext.BaseDirectory
             })
    {
        var directory = new DirectoryInfo(start);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GameSvr",
                    "GameSvr.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }
    }

    throw new DirectoryNotFoundException("LyoMir2 repository root not found");
}

static void Require(string source, string value, string label)
    => Assert(source.Contains(value, StringComparison.Ordinal),
        label + " missing");

static void Reject(string source, string value, string label)
    => Assert(!source.Contains(value, StringComparison.Ordinal), label);

static void Equal<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException(
            $"{label}: expected={expected}, actual={actual}");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
