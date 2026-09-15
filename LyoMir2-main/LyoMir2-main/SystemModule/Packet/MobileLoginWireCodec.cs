using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace SystemModule.Packet
{
    /// <summary>
    /// LiveLoginChainCheck / 手游登录链公共编解码（4003/4004/4010/4017）。
    /// 布局对齐 DBSvr NativeLoginResultCodec / NativeCharacterListCodec，不发明地图包。
    /// </summary>
    public static class MobileLoginWireCodec
    {
        public const int LoginPromptIdent = Grobal2.SM_LOGIN;          // 4003
        public const int LoginAuthIdent = Grobal2.CM_LOGIN_AUTH;    // 4004
        public const int CharacterListIdent = Grobal2.SM_CHR_LIST; // 4010
        public const int SelectCharacterIdent = Grobal2.CM_SELCHR4017; // 4017

        public const int LoginAuthResultBodySize = 87;
        public const int AccountCapacity = 20;
        public const int ServerNameCapacity = 20;
        public const int ReconnectIdCapacity = 36;
        public const int CharacterRowSize = 20;
        public const int CharacterNameCapacity = 15;
        public const int CharacterListMaxRows = 200;
        public const ushort AuthSuccessParam = 1;
        public const ushort SelectSuccessParam = 1;

        public static MobileCodec.InnerHeader CreateLoginPrompt()
            => new MobileCodec.InnerHeader { Recog = 0, Ident = LoginPromptIdent };

        public static bool IsLoginPrompt(in MobileCodec.InnerHeader inner)
            => inner.Ident == LoginPromptIdent;

        /// <summary>
        /// BaiZhu <c>net.send(CM_LOGIN_AUTH, {ticket, deviceBlock, gameType, mac})</c>:
        /// UTF-8 Lua strings go through <c>ycFunction:u2a</c> (GBK), then NUL separators
        /// plus a trailing NUL. The second field is an opaque device block: empty in
        /// the plaintext G2.5 lua, or 4/8/14 bytes on captured ODSocket builds.
        /// </summary>
        public static byte[] EncodeAuthRequest(string ticket, ReadOnlySpan<byte> sessionTail,
            string gameType, string deviceName)
        {
            var ticketBytes = MobileCodec.Gbk.GetBytes(ticket ?? string.Empty);
            var gameBytes = MobileCodec.Gbk.GetBytes(gameType ?? string.Empty);
            var deviceBytes = MobileCodec.Gbk.GetBytes(deviceName ?? string.Empty);
            var body = new byte[ticketBytes.Length + 1 + sessionTail.Length + 1
                                + gameBytes.Length + 1 + deviceBytes.Length + 1];
            var offset = 0;
            ticketBytes.CopyTo(body, offset);
            offset += ticketBytes.Length + 1;
            if (!sessionTail.IsEmpty)
            {
                sessionTail.CopyTo(body.AsSpan(offset, sessionTail.Length));
                offset += sessionTail.Length;
            }
            offset += 1;
            gameBytes.CopyTo(body, offset);
            offset += gameBytes.Length + 1;
            deviceBytes.CopyTo(body, offset);
            return body;
        }

        public static bool TryReadAuthTicket(ReadOnlySpan<byte> body, out string ticket)
        {
            ticket = string.Empty;
            if (body.IsEmpty) return false;
            var end = 0;
            while (end < body.Length && body[end] != 0) end++;
            if (end == 0) return false;
            ticket = MobileCodec.Gbk.GetString(body.Slice(0, end).ToArray());
            return ticket.Length > 0;
        }

        public static byte[] EncodeAuthResult(string account, string serverName,
            int areaId, int groupId, string reconnectId)
        {
            var body = new byte[LoginAuthResultBodySize];
            WriteCString(body.AsSpan(0, AccountCapacity + 1), account, AccountCapacity);
            WriteCString(body.AsSpan(AccountCapacity + 1, ServerNameCapacity + 1),
                serverName, ServerNameCapacity);
            BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(42, 4), areaId);
            BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(46, 4), groupId);
            WriteShortString(body.AsSpan(50, ReconnectIdCapacity + 1),
                reconnectId, ReconnectIdCapacity);
            return body;
        }

        public static bool TryDecodeAuthResult(ReadOnlySpan<byte> body,
            out string account, out string serverName, out int areaId, out int groupId,
            out string reconnectId)
        {
            account = serverName = reconnectId = string.Empty;
            areaId = groupId = 0;
            if (body.Length < LoginAuthResultBodySize) return false;
            account = ReadCString(body.Slice(0, AccountCapacity + 1));
            serverName = ReadCString(body.Slice(AccountCapacity + 1, ServerNameCapacity + 1));
            areaId = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(42, 4));
            groupId = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(46, 4));
            reconnectId = ReadShortString(body.Slice(50, ReconnectIdCapacity + 1));
            return true;
        }

        public static bool IsAuthAccepted(in MobileCodec.InnerHeader inner)
            => inner.Ident == LoginAuthIdent && inner.Param == AuthSuccessParam;

        public static void WriteCharacterRow(Span<byte> destination,
            ReadOnlySpan<byte> name, int job, int sex, int level)
        {
            if (destination.Length < CharacterRowSize)
                throw new ArgumentException($"character row requires {CharacterRowSize} bytes",
                    nameof(destination));
            var row = destination.Slice(0, CharacterRowSize);
            row.Clear();
            var nameLength = Math.Min(name.Length, CharacterNameCapacity);
            row[0] = (byte)nameLength;
            name.Slice(0, nameLength).CopyTo(row.Slice(1, CharacterNameCapacity));
            var nativeLevel = unchecked((ushort)level);
            row[16] = unchecked((byte)((nativeLevel >> 8) + 1));
            row[17] = unchecked((byte)job);
            row[18] = unchecked((byte)sex);
            row[19] = (byte)nativeLevel;
        }

        public static List<string> ParseCharacterNames(ReadOnlySpan<byte> body, int paramCount)
        {
            var result = new List<string>();
            var count = Math.Min(paramCount, body.Length / CharacterRowSize);
            count = Math.Min(count, CharacterListMaxRows);
            for (var index = 0; index < count; index++)
            {
                var offset = index * CharacterRowSize;
                var length = Math.Min(body[offset], (byte)CharacterNameCapacity);
                if (length == 0) continue;
                result.Add(MobileCodec.Gbk.GetString(body.Slice(offset + 1, length).ToArray()));
            }
            return result;
        }

        public static byte[] EncodeSelectCharacterName(string characterName)
            => MobileCodec.EncodeGbk((characterName ?? string.Empty) + '\0');

        public static bool TryReadSelectCharacterName(ReadOnlySpan<byte> body, out string name)
        {
            name = MobileCodec.DecodeGbk(body.ToArray(), 0, body.Length);
            return !string.IsNullOrEmpty(name);
        }

        public static bool IsSelectAccepted(in MobileCodec.InnerHeader inner)
            => inner.Ident == SelectCharacterIdent && inner.Param == SelectSuccessParam;

        private static void WriteCString(Span<byte> destination, string value, int capacity)
        {
            var bytes = MobileCodec.Gbk.GetBytes(value ?? string.Empty);
            if (bytes.Length > capacity)
                throw new ArgumentException($"login string exceeds {capacity} bytes");
            destination.Clear();
            bytes.CopyTo(destination);
        }

        private static void WriteShortString(Span<byte> destination, string value, int capacity)
        {
            var bytes = MobileCodec.Gbk.GetBytes(value ?? string.Empty);
            if (bytes.Length > capacity)
                throw new ArgumentException($"login short string exceeds {capacity} bytes");
            destination.Clear();
            destination[0] = (byte)bytes.Length;
            bytes.CopyTo(destination.Slice(1));
        }

        private static string ReadCString(ReadOnlySpan<byte> source)
        {
            var end = 0;
            while (end < source.Length && source[end] != 0) end++;
            return end == 0 ? string.Empty : MobileCodec.Gbk.GetString(source.Slice(0, end).ToArray());
        }

        private static string ReadShortString(ReadOnlySpan<byte> source)
        {
            if (source.IsEmpty) return string.Empty;
            var length = Math.Min(source[0], source.Length - 1);
            return length <= 0 ? string.Empty : MobileCodec.Gbk.GetString(source.Slice(1, length).ToArray());
        }
    }
}
