// Copyright 2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only
//
// RelayPacketFilter -- C++/CLI component that sits between the mapper and the
// relay uplink. It maintains a per-type "last seen" buffer and an event
// history so that a late-connecting relay can receive a full history burst.
//
// Instead of parsing raw bytes, it reads the current packet from the
// PacketExtractor which already holds the deserialized C structs.
//
// Packet forwarding is controlled by a Hz-based lookup table:
//   Hz > 0  : forward at most N times per second (time-gated)
//   Hz == 0 : always drop (never forward)
// Events (type 3) are a special case: always stored in history, always forwarded.

#pragma once

#include "F1DataDefs.h"
#include "F1PacketExtractor.h"

#include <cstring>
#include <chrono>

using namespace System;
using namespace System::Collections::Generic;

namespace adjsw::F12026
{
   public ref class RelayPacketFilter
   {
   public:
      RelayPacketFilter();

      void SetUdpMapper(adjsw::F12026::F1UdpClrMapper^ mapper);

      // Called from PollUpdates_Tick after mapper.Proceed().
      // Reads the current packet from the extractor, buffers it for history,
      // and returns a serialized copy if it should be forwarded to the relay.
      // Returns nullptr if the packet should be dropped or rate-gated.
      array<Byte>^ ProcessPacket(PacketType type);

      // Build a list of packets representing the full current state.
      // Used when the driver first connects to the relay server so it can seed
      // the server's buffer immediately.
      List<array<Byte>^>^ BuildHistoryBurst();

      property bool HasData { bool get() { return m_sessionUID != 0; } }

   private:
      // Store a backwards reference to the extractor so we can read
      // the current packet struct directly instead of parsing raw bytes.
      void m_SetExtractor(F12026_PacketExtractor* extractor) { m_extractor = extractor; }

      void m_ClearBuffers();

      static void m_AddIfNotNull(List<array<Byte>^>^ list, array<Byte>^ pkt);

      // Serialize the current packet struct from the extractor into a managed byte array.
      // This produces the same binary layout as the original UDP packet.
      array<Byte>^ m_SerializePacket(PacketType type);

      bool m_IsValidEvent();

      static array<Byte>^ m_StructToBytes(const void* pStruct, size_t size);

      // ---- Rate lookup table ------------------------------------------------
      //
      // Hz value per packet type. 0 means always drop.
      // Events (type 3) are handled as a special case before this table
      // is consulted -- they are always forwarded.
      //
      //  [0]  Motion              position updates for track map
      //  [1]  Session             session info changes slowly
      //  [2]  LapData             lap times, positions, pit status
      //  [3]  Event               always pass (handled above)
      //  [4]  Participants        rarely changes mid-session
      //  [5]  CarSetup            drop (private driver data)
      //  [6]  CarTelemetry        temperatures
      //  [7]  CarStatus           tyre compound, ERS, flags
      //  [8]  Classification      final results
      //  [9]  Lobby               rarely changes
      // [10]  CarDamage           wing/tyre damage
      // [11]  SessionHistory      per-car lap/sector history
      // [12]  TyreSets            tyre set availability
      // [13]  MotionEx            drop (extended motion, player only)
      // [14]  TimeTrial           drop (not relevant for relay)
      // [15]  LapPositions        position data for track map
      // [16]  CarTelemetry2       overtake available

      static array<float>^ m_packetRateHz =
      {
         5.0f,   //  0: Motion
         0.33f,  //  1: Session
         1.0f,   //  2: LapData
         0.0f,   //  3: Event (special-cased, value unused)
         0.5f,   //  4: Participants
         0.0f,   //  5: CarSetup (drop)
         3.0f,   //  6: CarTelemetry
         1.25f,  //  7: CarStatus
         0.33f,  //  8: Classification
         0.5f,   //  9: Lobby
         0.5f,   // 10: CarDamage
         1.0f,   // 11: SessionHistory
         0.5f,   // 12: TyreSets
         0.0f,   // 13: MotionEx (drop)
         0.0f,   // 14: TimeTrial (drop)
         0.2f,   // 15: LapPositions
         0.75f   // 16: CarStatus 2
      };

      F12026_PacketExtractor* m_extractor = nullptr;
      adjsw::F12026::F1UdpClrMapper^ m_mapper = nullptr;

      array<array<Byte>^>^   m_lastPacket;
      List<array<Byte>^>^    m_eventHistory;
      array<array<Byte>^>^   m_sessionHistory;
      System::UInt64         m_sessionUID;
      array<Int64>^          m_lastPassTime;  // Stopwatch ticks of last forwarded packet per type
      unsigned               m_carHistoryIdx{0};
   };
}
