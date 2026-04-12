// Copyright 2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only
//
// DriverSession -- per-driver room with own thread.
// Owns the driver TcpSocket, all engineer connections, packet buffers,
// session change detection, and history burst.
//
// The server does NOT filter or rate-limit packets. Pacing is done
// client-side by the driver's RelayPacketFilter. The server validates,
// buffers for late-join history, and forwards immediately.
//
// Thread-safe boundary:
//   - AddEngineer()  called from main thread
//   - GetInfo()      called from main thread (for room listing)
//   - IsAlive()      called from main thread
//   - RequestStop()  called from main thread
//   Everything else runs on the session thread.

#pragma once

#include "TcpSocket.h"
#include "F1DataDefs.h"
#include "F1PacketExtractor.h"
#include "RelayProtocol.h"

#include <atomic>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

// ---- Relay-specific constants ----

static constexpr double kDriverTimeoutSec = 30.0;

// ---- Number of F1 packet types (PacketType enum has 16 valid entries) ----

static constexpr size_t kNumF1PacketTypes = 16;

// ---- Engineer connection (lives inside DriverSession) ----

struct EngineerConn
{
   TcpSocket socket;
   bool      is_secondary = false;
   bool      history_sent = false;
};

// ---- Room info snapshot (thread-safe read from main) ----

struct RoomInfo
{
   std::string password;
   int         engineer_count = 0;
};

// ---- DriverSession ----

class DriverSession
{
public:
   DriverSession() = default;
   ~DriverSession();

   // Non-copyable, non-movable (because of thread + mutex)
   DriverSession(const DriverSession&) = delete;
   DriverSession& operator=(const DriverSession&) = delete;
   DriverSession(DriverSession&&) = delete;
   DriverSession& operator=(DriverSession&&) = delete;

   // Called from main thread after auth negotiation.
   // Takes ownership of the driver socket and starts the session thread.
   void Start(TcpSocket&& driverSocket, const std::string& password);

   // Called from main thread when an engineer authenticates.
   // The session thread will pick it up on the next poll cycle.
   void AddEngineer(TcpSocket&& engSocket, bool secondary);

   // Thread-safe: is the session thread still running?
   bool IsAlive() const { return m_alive.load(); }

   // Thread-safe: ask the session thread to stop.
   void RequestStop() { m_stopRequested.store(true); }

   // Thread-safe: get a snapshot of room info for listing.
   RoomInfo GetInfo() const;

   // Thread-safe: get the password (immutable after Start).
   const std::string& Password() const { return m_password; }

   // Wait for the session thread to finish.
   void Join();

   // ---- Static forwarding filter helpers ----
   static bool ShouldForwardPrimary(PacketType type);
   static bool ShouldForwardSecondary(PacketType type);

private:
   // ---- Session thread entry point ----
   void Exec();

   // ---- Packet processing (session thread only) ----

   // Validate and buffer incoming F1 packet. Always returns true if valid
   // (no server-side rate limiting -- pacing is done by the driver client).
   bool HandleF1Packet(const uint8_t* payload, uint16_t len);
   void ForwardToEngineers(const uint8_t* payload, uint16_t len, PacketType type);
   void SendHistoryBurst(TcpSocket& engSocket);
   void BufferPacket(PacketType type, uint8_t pktId, const uint8_t* payload, uint16_t len);
   void ClearBuffers();

   // ---- Engineer management (session thread only) ----
   void DisconnectEngineer(size_t idx);
   void HandleEngineerNameFix(EngineerConn& eng, const uint8_t* payload, uint16_t len);

   // ---- Helpers ----

   static void SendIfPresent(TcpSocket& sock, const std::vector<uint8_t>& pkt)
   {
      if (!pkt.empty())
         sock.SendMsg(RelayProtocol::MSG_F1_PACKET, pkt);
   }

   // ---- Thread state ----
   std::thread       m_thread;
   std::atomic<bool> m_alive{false};
   std::atomic<bool> m_stopRequested{false};

   // ---- Identity (immutable after Start) ----
   std::string m_password;

   // ---- Driver socket (session thread only) ----
   TcpSocket m_driverSocket;
   double    m_lastPacketTime = 0;

   // ---- Engineer list ----
   // m_pendingEngineers is written by main thread (AddEngineer),
   // read+drained by session thread under m_engineerMutex.
   // m_engineers is session-thread-only.
   mutable std::mutex            m_engineerMutex;
   std::vector<EngineerConn>     m_pendingEngineers;  // guarded by m_engineerMutex
   std::vector<EngineerConn>     m_engineers;         // session thread only

   // ---- Packet extractor (session thread only) ----
   // Provides validated struct access and session change detection.
   F12025_PacketExtractor m_extractor{};
   PacketType             m_lastPktType = PacketType::UnknownOrIllformed;
   uint64_t               m_lastTrackedUid = 0;

   // ---- Packet buffers (session thread only) ----
   // Raw packet bytes, stored for history burst to late-joining engineers.
   std::vector<uint8_t>              m_lastPacket[kNumF1PacketTypes];
   std::vector<std::vector<uint8_t>> m_eventHistory;
   std::vector<uint8_t>              m_sessionHistory[cs_maxNumCarsInUDPData];
};


inline bool DriverSession::ShouldForwardPrimary(PacketType type)
{
   switch (type)
   {
   case PacketType::PacketCarSetupData:
   case PacketType::PacketMotionExData:
   case PacketType::PacketTimeTrialData:
      return false;
   default:
      return true;
   }
}


inline bool DriverSession::ShouldForwardSecondary(PacketType type)
{
   switch (type)
   {
   case PacketType::PacketCarStatusData:
   case PacketType::PacketCarDamageData:
   case PacketType::PacketCarTelemetryData:
   case PacketType::PacketParticipantsData:
      return true;
   default:
      return false;
   }
}
