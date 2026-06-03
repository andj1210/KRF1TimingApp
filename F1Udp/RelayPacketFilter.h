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
      RelayPacketFilter()
      {
         m_lastPacket = gcnew array<array<Byte>^>(cs_maxPacketTypes);
         m_eventHistory = gcnew List<array<Byte>^>();
         m_sessionHistory = gcnew array<array<Byte>^>(cs_maxNumCarsInUDPData);
         m_sessionUID = 0;

         // Initialize last-pass timestamps to zero (epoch)
         m_lastPassTime = gcnew array<Int64>(cs_maxPacketTypes);
      }

      void SetUdpMapper(adjsw::F12026::F1UdpClrMapper^ mapper);

      // Store a backwards reference to the extractor so we can read
      // the current packet struct directly instead of parsing raw bytes.
      void SetExtractor(F12026_PacketExtractor* extractor)
      {
         m_extractor = extractor;
      }

      // Called from PollUpdates_Tick after mapper.Proceed().
      // Reads the current packet from the extractor, buffers it for history,
      // and returns a serialized copy if it should be forwarded to the relay.
      // Returns nullptr if the packet should be dropped or rate-gated.
      array<Byte>^ ProcessPacket(PacketType type)
      {
         if (m_extractor == nullptr || type == PacketType::UnknownOrIllformed)
            return nullptr;

         uint8_t packetId = m_extractor->lastHeader.m_packetId;
         if (packetId >= cs_maxPacketTypes)
            return nullptr;

         // Session change: clear all buffers
         uint64_t uid = m_extractor->sessionUID;
         if ((uid != 0) && (uid != m_sessionUID))
         {
            ClearBuffers();
            m_sessionUID = uid;
         }

         // Serialize the struct to a managed byte array
         array<Byte>^ serialized = SerializePacket(type);
         if (serialized == nullptr)
            return nullptr;

         // Buffer the packet (always, regardless of rate gating)
         if (type == PacketType::PacketEventData)
         {
            // important Events are always forwarded
            if (m_IsValidEvent())
            {
               m_eventHistory->Add(serialized);
               return serialized;
            }
            else
               return nullptr;
         }
         else if (type == PacketType::PacketSessionHistoryData)
         {
            uint8_t carIdx = m_extractor->history.m_carIdx;
            if (carIdx < cs_maxNumCarsInUDPData)
               m_sessionHistory[carIdx] = serialized;
            m_lastPacket[packetId] = serialized;
         }
         else
         {
            m_lastPacket[packetId] = serialized;
         }

         // Rate gating: check the Hz lookup table
         float hz = m_packetRateHz[packetId];
         if (hz <= 0.0f)
            return nullptr; // always drop

         // Time-based gating: forward only if enough time has passed
         Int64 nowTicks = System::Diagnostics::Stopwatch::GetTimestamp();
         Int64 lastTicks = m_lastPassTime[packetId];
         Int64 freq = System::Diagnostics::Stopwatch::Frequency;

         // Minimum interval in ticks = freq / hz
         Int64 minInterval = static_cast<Int64>(static_cast<double>(freq) / hz);

         if (lastTicks != 0 && (nowTicks - lastTicks) < minInterval)
            return nullptr; // too soon, drop

         m_lastPassTime[packetId] = nowTicks;

         if (type == PacketType::PacketSessionHistoryData)
         {
            // return the rolling car idx history instead of the current packet
            // this makes sure all cars are transmitted at the same frequency
            ++m_carHistoryIdx;
            if (m_carHistoryIdx >= cs_maxNumCarsInUDPData)
               m_carHistoryIdx = 0;
            else if (m_carHistoryIdx >= m_extractor->participants.m_numActiveCars)
               m_carHistoryIdx = 0;

            return m_sessionHistory[m_carHistoryIdx];
         }

         return serialized;
      }

      // Build a list of packets representing the full current state.
      // Used when the driver first connects to the relay server so it can seed
      // the server's buffer immediately.
      List<array<Byte>^>^ BuildHistoryBurst()
      {
         auto burst = gcnew List<array<Byte>^>();

         AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketParticipantsData)]);
         AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketSessionData)]);
         AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketLobbyInfoData)]);

         for (int i = 0; i < static_cast<int>(cs_maxNumCarsInUDPData); i++)
            AddIfNotNull(burst, m_sessionHistory[i]);

         AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketTyreSetsData)]);
         AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketCarStatusData)]);
         AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketCarDamageData)]);
         AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketLapData)]);
         AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketMotionData)]);
         AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketLapPositions)]);
         AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketFinalClassificationData)]);

         for each (auto evt in m_eventHistory)
            burst->Add(evt);

         return burst;
      }

      property bool HasData
      {
         bool get() { return m_sessionUID != 0; }
      }

   private:
      void ClearBuffers()
      {
         m_eventHistory->Clear();

         for (int i = 0; i < cs_maxPacketTypes; i++)
         {
            m_lastPacket[i] = nullptr;
            m_lastPassTime[i] = 0;
         }

         for (int i = 0; i < static_cast<int>(cs_maxNumCarsInUDPData); i++)
            m_sessionHistory[i] = nullptr;
      }

      static void AddIfNotNull(List<array<Byte>^>^ list, array<Byte>^ pkt)
      {
         if (pkt != nullptr)
            list->Add(pkt);
      }

      // Serialize the current packet struct from the extractor into a managed byte array.
      // This produces the same binary layout as the original UDP packet.
      array<Byte>^ SerializePacket(PacketType type)
      {
         switch (type)
         {
         case PacketType::PacketMotionData:
            return StructToBytes(&m_extractor->motion, sizeof(PacketMotionData));
         case PacketType::PacketSessionData:
            return StructToBytes(&m_extractor->session, sizeof(PacketSessionData));
         case PacketType::PacketLapData:
            return StructToBytes(&m_extractor->lap, sizeof(PacketLapData));
         case PacketType::PacketEventData:
            return StructToBytes(&m_extractor->event, sizeof(PacketEventData));
         case PacketType::PacketParticipantsData:
            return StructToBytes(&m_extractor->participants, sizeof(PacketParticipantsData));
         case PacketType::PacketCarSetupData:
            return StructToBytes(&m_extractor->setups, sizeof(PacketCarSetupData));
         case PacketType::PacketCarTelemetryData:
            return StructToBytes(&m_extractor->telemetry, sizeof(PacketCarTelemetryData));
         case PacketType::PacketCarStatusData:
            return StructToBytes(&m_extractor->status, sizeof(PacketCarStatusData));
         case PacketType::PacketFinalClassificationData:
            return StructToBytes(&m_extractor->classification, sizeof(PacketFinalClassificationData));
         case PacketType::PacketLobbyInfoData:
            return StructToBytes(&m_extractor->lobby, sizeof(PacketLobbyInfoData));
         case PacketType::PacketCarDamageData:
            return StructToBytes(&m_extractor->cardamage, sizeof(PacketCarDamageData));
         case PacketType::PacketSessionHistoryData:
            return StructToBytes(&m_extractor->history, sizeof(PacketSessionHistoryData));
         case PacketType::PacketTyreSetsData:
            return StructToBytes(&m_extractor->tyreSets, sizeof(PacketTyreSetsData));
         case PacketType::PacketMotionExData:
            return StructToBytes(&m_extractor->motionEx, sizeof(PacketMotionExData));
         case PacketType::PacketTimeTrialData:
            return StructToBytes(&m_extractor->timeTrial, sizeof(PacketTimeTrialData));
         case PacketType::PacketLapPositions:
            return StructToBytes(&m_extractor->lapPositions, sizeof(PacketLapPositionsData));
         default:
            return nullptr;
         }
      }

      bool m_IsValidEvent()
      {
         const char* p = reinterpret_cast<const char*>(&m_extractor->event.m_eventStringCode);
         constexpr unsigned SZ = sizeof(m_extractor->event.m_eventStringCode);

         if (!strncmp(p, PacketEventData::cs_sessionStartedEventCode, SZ))
            return true;

         if (!strncmp(p, PacketEventData::cs_sessionEndedEventCode, SZ))
            return true;

         if (!strncmp(p, PacketEventData::cs_sessionEndedEventCode, SZ))
            return true;

         if (!strncmp(p, PacketEventData::cs_retirementEventCode, SZ))
            return true;

         if (!strncmp(p, PacketEventData::cs_chequeredFlagEventCode, SZ))
            return true;

         if (!strncmp(p, PacketEventData::cs_penaltyEventCode, SZ))
            return true;

         if (!strncmp(p, PacketEventData::cs_driveThroughServedEventCode, SZ))
            return true;

         if (!strncmp(p, PacketEventData::cs_stopGoServedEventCode, SZ))
            return true;

         if (!strncmp(p, PacketEventData::cs_retirementEventCode, SZ))
            return true;

         return false;
      }

      static array<Byte>^ StructToBytes(const void* pStruct, size_t size)
      {
         auto arr = gcnew array<Byte>(static_cast<int>(size));
         pin_ptr<Byte> pinned = &arr[0];
         std::memcpy(pinned, pStruct, size);
         return arr;
      }

      // ---- Rate lookup table ------------------------------------------------
      //
      // Hz value per packet type. 0 means always drop.
      // Events (type 3) are handled as a special case before this table
      // is consulted -- they are always forwarded.
      //
      //  [0]  Motion              2 Hz  -- position updates for track map
      //  [1]  Session             1 Hz  -- session info changes slowly
      //  [2]  LapData             2 Hz  -- lap times, positions, pit status
      //  [3]  Event               n/a   -- always pass (handled above)
      //  [4]  Participants        0.5Hz -- rarely changes mid-session
      //  [5]  CarSetup            0 Hz  -- drop (private driver data)
      //  [6]  CarTelemetry        0 Hz  -- drop (private driver data)
      //  [7]  CarStatus           1 Hz  -- tyre compound, ERS, flags
      //  [8]  Classification      1 Hz  -- final results
      //  [9]  Lobby               0.5Hz -- rarely changes
      // [10]  CarDamage           1 Hz  -- wing/tyre damage
      // [11]  SessionHistory      2 Hz  -- per-car lap/sector history
      // [12]  TyreSets            0.5Hz -- tyre set availability
      // [13]  MotionEx            0 Hz  -- drop (extended motion, player only)
      // [14]  TimeTrial           0 Hz  -- drop (not relevant for relay)
      // [15]  LapPositions        2 Hz  -- position data for track map

      static array<float>^ m_packetRateHz =
      {
         5.0f,   //  0: Motion
         0.33f,  //  1: Session
         1.0f,   //  2: LapData
         0.0f,   //  3: Event (special-cased, value unused)
         0.5f,   //  4: Participants
         0.0f,   //  5: CarSetup (drop)
         3.0f,   //  6: CarTelemetry
         0.33f,  //  7: CarStatus
         0.33f,  //  8: Classification
         0.5f,   //  9: Lobby
         0.75f,  // 10: CarDamage
         1.0f,   // 11: SessionHistory
         0.5f,   // 12: TyreSets
         0.0f,   // 13: MotionEx (drop)
         0.0f,   // 14: TimeTrial (drop)
         0.2f,   // 15: LapPositions
      };

      static constexpr int cs_maxPacketTypes = 16;

      F12026_PacketExtractor* m_extractor = nullptr;

      array<array<Byte>^>^   m_lastPacket;
      List<array<Byte>^>^    m_eventHistory;
      array<array<Byte>^>^   m_sessionHistory;
      System::UInt64         m_sessionUID;
      array<Int64>^          m_lastPassTime;  // Stopwatch ticks of last forwarded packet per type
      unsigned               m_carHistoryIdx{0};



   };
}
