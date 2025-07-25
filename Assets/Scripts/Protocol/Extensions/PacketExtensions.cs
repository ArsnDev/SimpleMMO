using System;
using UnityEngine;
using Google.FlatBuffers;
using CppMMO.Protocol;
using SimpleMMO.Game;

namespace SimpleMMO.Protocol.Extensions
{
    public static class PacketExtensions
    {
        /// <summary>
        /// 패킷을 네트워크 전송용 바이트 배열로 변환 (4바이트 길이 헤더 포함)
        /// </summary>
        public static byte[] ToNetworkPacket(this byte[] packetData)
        {
            byte[] result = new byte[4 + packetData.Length];
            BitConverter.GetBytes(packetData.Length).CopyTo(result, 0);
            packetData.CopyTo(result, 4);
            return result;
        }

        /// <summary>
        /// 바이트 배열에서 UnifiedPacket 파싱
        /// </summary>
        public static bool TryParsePacket(byte[] data, out UnifiedPacket packet)
        {
            try
            {
                var byteBuffer = new ByteBuffer(data);
                packet = UnifiedPacket.GetRootAsUnifiedPacket(byteBuffer);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"PacketExtensions: Failed to parse packet - {e.Message}");
                packet = default;
                return false;
            }
        }

        /// <summary>
        /// C_PlayerInput 패킷 생성
        /// </summary>
        public static byte[] CreatePlayerInputPacket(byte inputFlags, Vector3 mousePosition, uint sequenceNumber)
        {
            var builder = new FlatBufferBuilder(256);
            
            var mouseVec3 = mousePosition.ToFlatBufferVec3(builder);
            var inputPacket = C_PlayerInput.CreateC_PlayerInput(
                builder,
                GetCurrentTick(),
                GetCurrentClientTime(),
                inputFlags,
                mouseVec3,
                sequenceNumber,
                0
            );
            
            var unifiedPacket = UnifiedPacket.CreateUnifiedPacket(
                builder, PacketId.C_PlayerInput, Packet.C_PlayerInput, inputPacket.Value);
            
            UnifiedPacket.FinishUnifiedPacketBuffer(builder, unifiedPacket);
            return builder.DataBuffer.ToSizedArray();
        }

        /// <summary>
        /// C_EnterZone 패킷 생성
        /// </summary>
        public static byte[] CreateEnterZonePacket(int zoneId)
        {
            var builder = new FlatBufferBuilder(128);
            
            var enterZonePacket = C_EnterZone.CreateC_EnterZone(builder, zoneId, 0);
            
            var unifiedPacket = UnifiedPacket.CreateUnifiedPacket(
                builder, PacketId.C_EnterZone, Packet.C_EnterZone, enterZonePacket.Value);
            
            UnifiedPacket.FinishUnifiedPacketBuffer(builder, unifiedPacket);
            return builder.DataBuffer.ToSizedArray();
        }

        /// <summary>
        /// C_Login 패킷 생성 (세션티켓 + 플레이어 ID)
        /// </summary>
        public static byte[] CreateLoginPacket(string sessionTicket, ulong playerId)
        {
            var builder = new FlatBufferBuilder(256);
            
            var sessionTicketOffset = builder.CreateString(sessionTicket);
            var loginPacket = C_Login.CreateC_Login(builder, sessionTicketOffset, playerId, 0);
            
            var unifiedPacket = UnifiedPacket.CreateUnifiedPacket(
                builder, PacketId.C_Login, Packet.C_Login, loginPacket.Value);
            
            UnifiedPacket.FinishUnifiedPacketBuffer(builder, unifiedPacket);
            return builder.DataBuffer.ToSizedArray();
        }

        /// <summary>
        /// C_Chat 패킷 생성
        /// </summary>
        public static byte[] CreateChatPacket(string message)
        {
            var builder = new FlatBufferBuilder(256);
            
            var messageOffset = builder.CreateString(message);
            var chatPacket = C_Chat.CreateC_Chat(builder, messageOffset, 0);
            
            var unifiedPacket = UnifiedPacket.CreateUnifiedPacket(
                builder, PacketId.C_Chat, Packet.C_Chat, chatPacket.Value);
            
            UnifiedPacket.FinishUnifiedPacketBuffer(builder, unifiedPacket);
            return builder.DataBuffer.ToSizedArray();
        }

        /// <summary>
        /// 현재 클라이언트 시간 (밀리초)
        /// </summary>
        private static ulong GetCurrentClientTime()
        {
            return (ulong)(Time.realtimeSinceStartup * 1000);
        }

        /// <summary>
        /// 현재 틱 번호 (나중에 NetworkManager에서 관리)
        /// </summary>
        private static ulong GetCurrentTick()
        {
            // TODO: NetworkManager에서 서버 틱과 동기화된 값 가져오기
            return (ulong)(Time.realtimeSinceStartup * GameConfig.TickRate);
        }
    }
}