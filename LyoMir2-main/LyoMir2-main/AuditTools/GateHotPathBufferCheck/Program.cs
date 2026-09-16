using System.Buffers.Binary;
using GameGate.Core;
using GameGate.Models;
using SystemModule;
using SystemModule.Packet;

using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

RotatedKeysMatchOriginalFormula();
TigerStringMatchesBytes();
TigerEncodeIsStable();
FrameParserReusesList();
InternalPacketScratchPayload();
WriteFrameBodySlice();
ParserListOverloadReuses();
ClientPacketHeaderBytes();
Legacy77EncodeScratch();

Console.WriteLine(
    "GateHotPathBufferCheck PASS tiger=roundtrip parser=reuse payloadLength=exact writeFrame=slice encode=scratch");

static void RotatedKeysMatchOriginalFormula()
{
    const string baseKey = "1Y0lSUQMH+mbKXRTBtFiWvLx32/gNAzGr674oeyn5dCEp8jDqasI9VcwJPhufkOZ";
    foreach (uint offset in new uint[] { 0, 1, 17, 62, 63, 126 })
    {
        var expected = offset == 0
            ? baseKey
            : baseKey.Substring((int)(offset % 63)) + baseKey.Substring(0, (int)(offset % 63));
        Require(expected == TigerCodec.GetRotatedKey(offset),
            $"rotated Tiger key diverged at offset={offset}");
    }
}

static void TigerEncodeIsStable()
{
    var payload = new byte[] { 0x44, 0xFF, 0x44, 0xFF, 0x00, 0x17, 0x04, 0x00, 1, 2, 3, 4, 9, 8, 7, 6 };
    var first = TigerCodec.Encode(payload, 17);
    var second = TigerCodec.Encode(payload, 17);
    Require(first == second, "Tiger encode is not deterministic");
    Require(first.EndsWith("|LH", StringComparison.Ordinal), "Tiger encode lost |LH suffix");
}

static void TigerStringMatchesBytes()
{
    var payload = Enumerable.Range(0, 48).Select(i => (byte)i).ToArray();
    var encoded = TigerCodec.Encode(payload, 5);
    var fromString = TigerCodec.Decode(encoded, 5);
    var ascii = System.Text.Encoding.ASCII.GetBytes(encoded);
    var scratch = new byte[ascii.Length];
    var written = TigerCodec.Decode(ascii, 5, scratch);
    Require(fromString.AsSpan().SequenceEqual(scratch.AsSpan(0, written)),
        "Tiger byte decode diverged from string decode");
}

static void FrameParserReusesList()
{
    var parser = new FrameParser();
    var first = FrameProtocol.BuildFrame(0, 0x03, new byte[] { 1, 2, 3 }, 7);
    var second = FrameProtocol.BuildFrame(0, 0x04, new byte[] { 9 }, 8);
    var frames = parser.Feed(first, 0, first.Length);
    Require(frames.Count == 1, "first Feed count");
    Require(ReferenceEquals(frames, parser.Feed(second, 0, second.Length)),
        "FrameParser.Feed allocated a new list");
    Require(frames.Count == 1 && frames[0].cmd == 0x04 && frames[0].payload.Length == 1,
        "reused FrameParser list did not hold the second frame");
}

static void InternalPacketScratchPayload()
{
    var body = new byte[] { 1, 2, 3, 4, 5 };
    var scratch = new byte[32];
    scratch.AsSpan().Fill(0xAA);
    body.CopyTo(scratch, 0);
    var exact = new InternalPacket77
    {
        Magic = InternalPacket77.MAGIC,
        ConnID = 11,
        SeqID = 22,
        Cmd = NativeGameGateCommands.GateClientData,
        FrameLen = (ushort)(InternalPacket77.HEADER_SIZE + body.Length),
        Payload = body
    }.ToBytes();
    var reused = new InternalPacket77
    {
        Magic = InternalPacket77.MAGIC,
        ConnID = 11,
        SeqID = 22,
        Cmd = NativeGameGateCommands.GateClientData,
        FrameLen = (ushort)(InternalPacket77.HEADER_SIZE + body.Length),
        Payload = scratch,
        PayloadLength = body.Length
    }.ToBytes();
    Require(exact.AsSpan().SequenceEqual(reused),
        "InternalPacket77 PayloadLength changed wire bytes");
}

static void WriteFrameBodySlice()
{
    var inner = new MobileCodec.InnerHeader
    {
        Recog = 101,
        Ident = 50,
        Param = 1,
        Tag = 2,
        Series = 3
    };
    var packed = new byte[ClientPacket.PackSize + 4];
    packed[ClientPacket.PackSize] = 0x10;
    packed[ClientPacket.PackSize + 1] = 0x20;
    packed[ClientPacket.PackSize + 2] = 0x30;
    packed[ClientPacket.PackSize + 3] = 0x40;
    var copied = packed.AsSpan(ClientPacket.PackSize, 4).ToArray();
    var fromCopy = MobileCodec.WriteFrame(inner, copied, 0, MobileCodec.MARKER_DATA);
    var fromSlice = MobileCodec.WriteFrame(inner, packed, ClientPacket.PackSize, 4,
        0, MobileCodec.MARKER_DATA);
    Require(fromCopy.AsSpan().SequenceEqual(fromSlice),
        "WriteFrame body slice changed wire bytes");
}

static void ParserListOverloadReuses()
{
    var header = new byte[InternalPacket77.HEADER_SIZE];
    BinaryPrimitives.WriteUInt32LittleEndian(header, InternalPacket77.MAGIC);
    BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(12, 2), 1);
    var parser = new GameGateServerFrameParser();
    var frames = new List<GameGateServerFrame>(2);
    Require(parser.TryAppend(header, 0, header.Length, frames, out var error), error);
    Require(frames.Count == 1, "GameGate parser did not emit the 16-byte control frame");
    Require(parser.TryAppend(header, 0, 0, frames, out error), error);
    Require(frames.Count == 0, "GameGate parser reusable list was not cleared");
    var dbParser = new DbServerGatewayFrameParser();
    var dbFrames = new List<DbServerGatewayFrame>(2);
    Require(dbParser.TryAppend(header, 0, header.Length, dbFrames, out error), error);
    Require(dbFrames.Count == 1, "DB parser did not emit the native control frame");
    Require(dbParser.TryAppend(header, 0, 0, dbFrames, out error), error);
    Require(dbFrames.Count == 0, "DB parser reusable list was not cleared");
}

static void ClientPacketHeaderBytes()
{
    var packet = new ClientPacket
    {
        Recog = unchecked((int)0xAABBCCDD),
        Ident = 0x1122,
        Param = 0x3344,
        Tag = 0x5566,
        Series = 0x7788
    };
    var fromWriter = packet.GetBuffer();
    var scratch = new byte[ClientPacket.PackSize + 8];
    scratch.AsSpan().Fill(0xEE);
    GateServer.WriteClientPacketHeader(scratch.AsSpan(0, ClientPacket.PackSize), packet);
    Require(fromWriter.AsSpan().SequenceEqual(scratch.AsSpan(0, ClientPacket.PackSize)),
        "WriteClientPacketHeader diverged from ClientPacket.GetBuffer");
}

static void Legacy77EncodeScratch()
{
    var frame = new YbDbLegacy77Frame(7, 9, 1000, new byte[] { 0x11, 0x22, 0x33 });
    Require(YbDbLegacy77Codec.TryEncode(frame, out var encoded, out var error), error);
    var scratch = new byte[encoded.Length + 8];
    scratch.AsSpan().Fill(0xDD);
    Require(YbDbLegacy77Codec.TryEncode(frame, scratch, out var written, out error), error);
    Require(written == encoded.Length, "legacy 77 scratch written length");
    Require(encoded.AsSpan().SequenceEqual(scratch.AsSpan(0, written)),
        "legacy 77 scratch encode changed wire bytes");
}

static void Require(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException(label);
}
