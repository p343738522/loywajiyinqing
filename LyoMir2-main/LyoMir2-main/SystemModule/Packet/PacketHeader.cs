using System.IO;

namespace SystemModule.Packages
{
    
    
    
    public class PacketHeader : Packets
    {
        public uint PacketCode;
        public int Socket;
        public ushort SocketIdx;
        public ushort Ident;
        public int UserIndex;
        public int PackLength;

        public const int PacketSize = 20;

        protected override void ReadPacket(BinaryReader reader)
        {
            PacketCode = reader.ReadUInt32();
            Socket = reader.ReadInt32();
            SocketIdx = reader.ReadUInt16();
            Ident = reader.ReadUInt16();
            UserIndex = reader.ReadInt32();
            PackLength = reader.ReadInt32();
        }

        protected override void WritePacket(BinaryWriter writer)
        {
            writer.Write(PacketCode);
            writer.Write(Socket);
            writer.Write(SocketIdx);
            writer.Write(Ident);
            writer.Write(UserIndex);
            writer.Write(PackLength);
        }
    }

    public class ClientOutMessage : Packets
    {
        private PacketHeader MessageHeader;
        private ClientPacket DefaultMessage;

        public ClientOutMessage()
        {
            MessageHeader = new PacketHeader();
            DefaultMessage = new ClientPacket();
        }

        public ClientOutMessage(PacketHeader messageHeader, ClientPacket defaultMessage)
        {
            MessageHeader = messageHeader ?? new PacketHeader();
            DefaultMessage = defaultMessage ?? new ClientPacket();
        }

        protected override void ReadPacket(BinaryReader reader)
        {
            // Wire: int32 nLen (= PackLength + PacketHeader.PacketSize)
            //        + PacketHeader (20) + ClientPacket (PackLength, typically 12).
            var nLen = reader.ReadInt32();
            var headerBytes = reader.ReadBytes(PacketHeader.PacketSize);
            MessageHeader = Packets.ToPacket<PacketHeader>(headerBytes) ?? new PacketHeader();
            var bodyLen = MessageHeader.PackLength;
            if (bodyLen < 0) bodyLen = 0;
            var expected = nLen - PacketHeader.PacketSize;
            if (expected > 0 && (bodyLen == 0 || bodyLen > expected))
                bodyLen = expected;
            var msgBytes = bodyLen > 0 ? reader.ReadBytes(bodyLen) : System.Array.Empty<byte>();
            if (msgBytes.Length >= ClientPacket.PackSize)
            {
                var slice = new byte[ClientPacket.PackSize];
                System.Buffer.BlockCopy(msgBytes, 0, slice, 0, ClientPacket.PackSize);
                DefaultMessage = Packets.ToPacket<ClientPacket>(slice) ?? new ClientPacket();
            }
            else
            {
                DefaultMessage = new ClientPacket();
            }
        }

        protected override void WritePacket(BinaryWriter writer)
        {
            var nLen = MessageHeader.PackLength + 20;
            writer.Write(nLen);
            writer.Write(MessageHeader.GetBuffer());
            writer.Write(DefaultMessage.GetBuffer());
        }
    }
}