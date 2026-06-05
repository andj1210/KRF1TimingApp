// Copyright 2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

#include "DriverSession.h"
#include <algorithm>

// ---- Lifecycle ----------------------------------------------------------

DriverSession::~DriverSession()
{
   RequestStop();
   Join();
}

void DriverSession::Start(TcpSocket&& driverSocket, const std::string& password)
{
   m_driverSocket = std::move(driverSocket);
   m_password     = password;
   m_lastPacketTime = OsSeconds();
   m_alive.store(true);
   m_thread = std::thread(&DriverSession::Exec, this);
}

void DriverSession::Join()
{
   if (m_thread.joinable())
      m_thread.join();
}

// ---- Thread-safe interface (called from main thread) --------------------

void DriverSession::AddEngineer(TcpSocket&& engSocket, bool secondary)
{
   EngineerConn ec;
   ec.socket       = std::move(engSocket);
   ec.is_secondary = secondary;
   ec.history_sent = false;

   std::lock_guard<std::mutex> lock(m_engineerMutex);
   m_pendingEngineers.push_back(std::move(ec));
}

RoomInfo DriverSession::GetInfo() const
{
   RoomInfo info;
   info.password = m_password;

   std::lock_guard<std::mutex> lock(m_engineerMutex);
   // Count pending engineers (approximate, good enough for listing).
   // m_engineers is session-thread-only so we can't read it here
   // without additional synchronization.
   info.engineer_count = static_cast<int>(m_pendingEngineers.size());
   return info;
}

// ---- Session thread entry point -----------------------------------------

void DriverSession::Exec()
{
   std::printf("[session %s] thread started\n", m_password.c_str());
   std::vector<poll_fd> pfds;

   while (!m_stopRequested.load())
   {
      // ---- Drain pending engineers into the active list ----
      {
         std::lock_guard<std::mutex> lock(m_engineerMutex);
         for (auto& pe : m_pendingEngineers)
         {
            std::printf("[session %s] engineer joined (secondary=%d)\n",
               m_password.c_str(), pe.is_secondary ? 1 : 0);

            // Send history burst before adding to active list
            SendHistoryBurst(pe.socket);
            pe.history_sent = true;

            m_engineers.push_back(std::move(pe));
         }
         m_pendingEngineers.clear();
      }

      // ---- Build poll fd array: [driver, engineer_0, engineer_1, ...] ----
      const size_t numEngineers = m_engineers.size();
      pfds.clear();
      pfds.reserve(1 + numEngineers);

      // Index 0: driver
      pfds.push_back({ m_driverSocket.Fd(), POLLIN, 0 });

      // Index 1..N: engineers
      for (size_t i = 0; i < numEngineers; i++)
      {
         if (m_engineers[i].socket.IsValid())
            pfds.push_back({ m_engineers[i].socket.Fd(), POLLIN, 0 });
         else
            pfds.push_back({ kInvalidSocket, 0, 0 });
      }

      int ret = OsPollFd(pfds.data(), static_cast<unsigned>(pfds.size()), 1000);
      if (ret < 0)
      {
         if (OsWouldBlock())
            continue;
         break;
      }

      double now = OsSeconds();

      // ---- Process driver socket ----
      if (pfds[0].revents & (POLLERR | POLLHUP | POLLNVAL))
      {
         std::printf("[session %s] driver connection error\n", m_password.c_str());
         break; // driver gone, session ends
      }

      if (pfds[0].revents & POLLIN)
      {
         int n = m_driverSocket.RecvInto();
         if (n <= 0 && !OsWouldBlock())
         {
            std::printf("[session %s] driver disconnected\n", m_password.c_str());
            break;
         }

         uint8_t        msg_type;
         const uint8_t* msg_payload;
         uint16_t       msg_len;
         while (m_driverSocket.TryConsume(msg_type, msg_payload, msg_len))
         {
            if (msg_type == RelayProtocol::MSG_F1_PACKET)
            {
               if (HandleF1Packet(msg_payload, msg_len))
                  ForwardToEngineers(msg_payload, msg_len, m_lastPktType);
            }
            m_driverSocket.Consume(msg_len);
         }
      }

      // Driver timeout check
      if (m_lastPacketTime > 0 && (now - m_lastPacketTime) > kDriverTimeoutSec)
      {
         std::printf("[session %s] driver timeout\n", m_password.c_str());
         break;
      }

      // ---- Process engineer sockets ----
      for (size_t ei = 0; ei < numEngineers; ei++)
      {
         auto& eng = m_engineers[ei];
         if (!eng.socket.IsValid())
            continue;

         size_t pfd_idx = 1 + ei;

         if (pfds[pfd_idx].revents & (POLLERR | POLLHUP | POLLNVAL))
         {
            DisconnectEngineer(ei);
            continue;
         }

         if (pfds[pfd_idx].revents & POLLIN)
         {
            int n = eng.socket.RecvInto();
            if (n <= 0 && !OsWouldBlock())
            {
               DisconnectEngineer(ei);
               continue;
            }

            uint8_t        msg_type;
            const uint8_t* msg_payload;
            uint16_t       msg_len;
            while (eng.socket.TryConsume(msg_type, msg_payload, msg_len))
            {
               if (msg_type == RelayProtocol::MSG_NAME_FIX)
                  HandleEngineerNameFix(eng, msg_payload, msg_len);
               eng.socket.Consume(msg_len);
            }
         }
      }

      // ---- Clean up dead engineer slots ----
      m_engineers.erase(
         std::remove_if(m_engineers.begin(), m_engineers.end(),
            [](const EngineerConn& e) { return !e.socket.IsValid(); }),
         m_engineers.end());
   }

   // ---- Session ending: notify and close all engineers ----
   for (auto& eng : m_engineers)
   {
      if (eng.socket.IsValid())
      {
         const char* reason = "driver disconnected";
         eng.socket.SendMsg(RelayProtocol::MSG_AUTH_FAIL,
            reinterpret_cast<const uint8_t*>(reason),
            static_cast<uint16_t>(std::strlen(reason)));
         eng.socket.Close();
      }
   }
   m_engineers.clear();

   // Also close any pending engineers that arrived during shutdown
   {
      std::lock_guard<std::mutex> lock(m_engineerMutex);
      for (auto& pe : m_pendingEngineers)
      {
         if (pe.socket.IsValid())
         {
            const char* reason = "driver disconnected";
            pe.socket.SendMsg(RelayProtocol::MSG_AUTH_FAIL,
               reinterpret_cast<const uint8_t*>(reason),
               static_cast<uint16_t>(std::strlen(reason)));
            pe.socket.Close();
         }
      }
      m_pendingEngineers.clear();
   }

   m_driverSocket.Close();
   ClearBuffers();

   std::printf("[session %s] thread ended\n", m_password.c_str());
   m_alive.store(false);
}

// ---- Packet processing --------------------------------------------------

bool DriverSession::HandleF1Packet(const uint8_t* payload, uint16_t len)
{
   // Validate and parse via F1PacketExtractor.
   // No server-side rate limiting -- pacing is done by the driver client.
   PacketType type = PacketType::UnknownOrIllformed;
   m_extractor.ProceedPacket(payload, len, &type);

   if (type == PacketType::UnknownOrIllformed)
      return false;

   uint8_t pktId = m_extractor.lastHeader.m_packetId;
   if (pktId >= static_cast<uint8_t>(PacketType::numPacketTypes))
      return false;

   m_lastPacketTime = OsSeconds();
   m_lastPktType = type;

   // Detect session change -- clear relay buffers on new session
   uint64_t currentUid = m_extractor.sessionUID;
   if (currentUid != 0 && currentUid != m_lastTrackedUid)
   {
      std::printf("[session %s] session change %llu -> %llu\n",
         m_password.c_str(),
         static_cast<unsigned long long>(m_lastTrackedUid),
         static_cast<unsigned long long>(currentUid));
      ClearBuffers();
      m_lastTrackedUid = currentUid;
   }

   // Buffer for late-join history, then forward immediately
   BufferPacket(type, pktId, payload, len);
   return true;
}

void DriverSession::ForwardToEngineers(const uint8_t* payload, uint16_t len, PacketType type)
{
   for (auto& eng : m_engineers)
   {
      if (!eng.socket.IsValid() || !eng.history_sent)
         continue;

      if (!eng.is_secondary)
      {
         if (ShouldForwardPrimary(type))
            eng.socket.SendMsg(RelayProtocol::MSG_F1_PACKET, payload, len);
      }
      else
      {
         if (ShouldForwardSecondary(type))
            eng.socket.SendMsg(RelayProtocol::MSG_F1_PACKET, payload, len);
      }
   }
}

void DriverSession::SendHistoryBurst(TcpSocket& engSocket)
{
   engSocket.SendMsgEmpty(RelayProtocol::MSG_HISTORY_BEGIN);

   // Order matters: session first, then participants, then per-car history, then state
   SendIfPresent(engSocket, m_lastPacket[static_cast<uint8_t>(PacketType::PacketSessionData)]);
   SendIfPresent(engSocket, m_lastPacket[static_cast<uint8_t>(PacketType::PacketParticipantsData)]);
   SendIfPresent(engSocket, m_lastPacket[static_cast<uint8_t>(PacketType::PacketLobbyInfoData)]);

   for (size_t i = 0; i < cs_maxNumCarsInUDPData; i++)
      SendIfPresent(engSocket, m_sessionHistory[i]);

   SendIfPresent(engSocket, m_lastPacket[static_cast<uint8_t>(PacketType::PacketTyreSetsData)]);
   SendIfPresent(engSocket, m_lastPacket[static_cast<uint8_t>(PacketType::PacketCarStatusData)]);
   SendIfPresent(engSocket, m_lastPacket[static_cast<uint8_t>(PacketType::PacketCarDamageData)]);
   SendIfPresent(engSocket, m_lastPacket[static_cast<uint8_t>(PacketType::PacketLapData)]);
   SendIfPresent(engSocket, m_lastPacket[static_cast<uint8_t>(PacketType::PacketMotionData)]);
   SendIfPresent(engSocket, m_lastPacket[static_cast<uint8_t>(PacketType::PacketFinalClassificationData)]);
   SendIfPresent(engSocket, m_lastPacket[static_cast<uint8_t>(PacketType::PacketLapPositions)]);

   for (auto& evt : m_eventHistory)
      engSocket.SendMsg(RelayProtocol::MSG_F1_PACKET, evt);

   engSocket.SendMsgEmpty(RelayProtocol::MSG_HISTORY_END);

   std::printf("[session %s] history burst sent (%zu events, uid=%llu)\n",
      m_password.c_str(),
      m_eventHistory.size(),
      static_cast<unsigned long long>(m_lastTrackedUid));
}

void DriverSession::BufferPacket(PacketType type, uint8_t pktId, const uint8_t* payload, uint16_t len)
{
   if (type == PacketType::PacketEventData)
   {
      m_eventHistory.emplace_back(payload, payload + len);
   }
   else if (type == PacketType::PacketSessionHistoryData)
   {
      // Read car index from the parsed struct instead of raw byte offset
      uint8_t carIdx = m_extractor.history.m_carIdx;
      if (carIdx < cs_maxNumCarsInUDPData)
         m_sessionHistory[carIdx].assign(payload, payload + len);

      m_lastPacket[pktId].assign(payload, payload + len);
   }
   else
   {
      m_lastPacket[pktId].assign(payload, payload + len);
   }
}

void DriverSession::ClearBuffers()
{
   for (auto& v : m_lastPacket)
      v.clear();
   for (auto& v : m_sessionHistory)
      v.clear();
   m_eventHistory.clear();
}

// ---- Engineer management ------------------------------------------------

void DriverSession::DisconnectEngineer(size_t idx)
{
   auto& eng = m_engineers[idx];
   if (!eng.socket.IsValid())
      return;
   std::printf("[session %s] engineer disconnected\n", m_password.c_str());
   eng.socket.Close();
}

void DriverSession::HandleEngineerNameFix(EngineerConn& eng, const uint8_t* payload, uint16_t len)
{
   if (m_driverSocket.IsValid())
      m_driverSocket.SendMsg(RelayProtocol::MSG_NAME_FIX, payload, len);
}
