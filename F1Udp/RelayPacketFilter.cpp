// Copyright 2018-2021 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

#include "F1UdpClrMapper.h"
#include "RelayPacketFilter.h"

adjsw::F12026::RelayPacketFilter::RelayPacketFilter()
{
   m_lastPacket = gcnew array<array<Byte>^>(static_cast<uint8_t>(PacketType::numPacketTypes));
   m_eventHistory = gcnew List<array<Byte>^>();
   m_sessionHistory = gcnew array<array<Byte>^>(cs_maxNumCarsInUDPData);
   m_sessionUID = 0;

   // Initialize last-pass timestamps to zero (epoch)
   m_lastPassTime = gcnew array<Int64>(static_cast<uint8_t>(PacketType::numPacketTypes));
}

array<System::Byte>^ adjsw::F12026::RelayPacketFilter::ProcessPacket(PacketType type)
{
   if (m_extractor == nullptr || type == PacketType::UnknownOrIllformed)
      return nullptr;

   uint8_t packetId = m_extractor->lastHeader.m_packetId;
   if (packetId >= static_cast<uint8_t>(PacketType::numPacketTypes))
      return nullptr;

   // Session change: clear all buffers
   uint64_t uid = m_extractor->sessionUID;
   if ((uid != 0) && (uid != m_sessionUID))
   {
      m_ClearBuffers();
      m_sessionUID = uid;
   }

   // Serialize the struct to a managed byte array
   array<Byte>^ serialized = m_SerializePacket(type);
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
      // we cannot always send the current packet, since this would create an aliasing 
      // between our gating rate and udp rate, leading to unequal distribution of sent driver history.
      // 
      // we use two rules: 
      // 1) find the first car with dirty history and send
      // 2) if 1) does not apply send round robin by idx

      // 1)
      if (m_mapper)
      {
         for (int i = 0; i < m_mapper->DriverHistoryDirty->Length; ++i)
         {
            if (m_mapper->DriverHistoryDirty[i])
            {
               m_mapper->DriverHistoryDirty[i] = false;
               return m_sessionHistory[i];
            }
         }
      }

      // 2)
      // return the rolling car idx history instead of the current packet
      ++m_carHistoryIdx;
      if (m_carHistoryIdx >= cs_maxNumCarsInUDPData)
         m_carHistoryIdx = 0;
      else if (m_carHistoryIdx >= m_extractor->participants.m_numActiveCars)
         m_carHistoryIdx = 0;

      return m_sessionHistory[m_carHistoryIdx];
   }

   return serialized;
}

System::Collections::Generic::List<cli::array<System::Byte>^>^ adjsw::F12026::RelayPacketFilter::BuildHistoryBurst()
{
   auto burst = gcnew List<array<Byte>^>();

   m_AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketParticipantsData)]);
   m_AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketSessionData)]);
   m_AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketLobbyInfoData)]);

   for (int i = 0; i < static_cast<int>(cs_maxNumCarsInUDPData); i++)
      m_AddIfNotNull(burst, m_sessionHistory[i]);

   m_AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketTyreSetsData)]);
   m_AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketCarStatusData)]);
   m_AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketCarDamageData)]);
   m_AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketLapData)]);
   m_AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketMotionData)]);
   m_AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketLapPositions)]);
   m_AddIfNotNull(burst, m_lastPacket[static_cast<int>(PacketType::PacketFinalClassificationData)]);

   for each (auto evt in m_eventHistory)
      burst->Add(evt);

   return burst;
}

void adjsw::F12026::RelayPacketFilter::m_ClearBuffers()
{
   m_eventHistory->Clear();

   for (int i = 0; i < static_cast<uint8_t>(PacketType::numPacketTypes); i++)
   {
      m_lastPacket[i] = nullptr;
      m_lastPassTime[i] = 0;
   }

   for (int i = 0; i < static_cast<int>(cs_maxNumCarsInUDPData); i++)
      m_sessionHistory[i] = nullptr;
}

void adjsw::F12026::RelayPacketFilter::m_AddIfNotNull(List<array<Byte>^>^ list, array<Byte>^ pkt)
{
   if (pkt != nullptr)
      list->Add(pkt);
}

cli::array<System::Byte>^ adjsw::F12026::RelayPacketFilter::m_SerializePacket(PacketType type)
{
   switch (type)
   {
   case PacketType::PacketMotionData:
      return m_StructToBytes(&m_extractor->motion, sizeof(PacketMotionData));
   case PacketType::PacketSessionData:
      return m_StructToBytes(&m_extractor->session, sizeof(PacketSessionData));
   case PacketType::PacketLapData:
      return m_StructToBytes(&m_extractor->lap, sizeof(PacketLapData));
   case PacketType::PacketEventData:
      return m_StructToBytes(&m_extractor->event, sizeof(PacketEventData));
   case PacketType::PacketParticipantsData:
      return m_StructToBytes(&m_extractor->participants, sizeof(PacketParticipantsData));
   case PacketType::PacketCarSetupData:
      return m_StructToBytes(&m_extractor->setups, sizeof(PacketCarSetupData));
   case PacketType::PacketCarTelemetryData:
      return m_StructToBytes(&m_extractor->telemetry, sizeof(PacketCarTelemetryData));
   case PacketType::PacketCarStatusData:
      return m_StructToBytes(&m_extractor->status, sizeof(PacketCarStatusData));
   case PacketType::PacketFinalClassificationData:
      return m_StructToBytes(&m_extractor->classification, sizeof(PacketFinalClassificationData));
   case PacketType::PacketLobbyInfoData:
      return m_StructToBytes(&m_extractor->lobby, sizeof(PacketLobbyInfoData));
   case PacketType::PacketCarDamageData:
      return m_StructToBytes(&m_extractor->cardamage, sizeof(PacketCarDamageData));
   case PacketType::PacketSessionHistoryData:
      return m_StructToBytes(&m_extractor->history, sizeof(PacketSessionHistoryData));
   case PacketType::PacketTyreSetsData:
      return m_StructToBytes(&m_extractor->tyreSets, sizeof(PacketTyreSetsData));
   case PacketType::PacketMotionExData:
      return m_StructToBytes(&m_extractor->motionEx, sizeof(PacketMotionExData));
   case PacketType::PacketTimeTrialData:
      return m_StructToBytes(&m_extractor->timeTrial, sizeof(PacketTimeTrialData));
   case PacketType::PacketLapPositions:
      return m_StructToBytes(&m_extractor->lapPositions, sizeof(PacketLapPositionsData));
   case PacketType::PacketCarTelemetry2Data:
      return m_StructToBytes(&m_extractor->telemetry2, sizeof(PacketCarTelemetry2Data));
   default:
      return nullptr;
   }
}

bool adjsw::F12026::RelayPacketFilter::m_IsValidEvent()
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

array<System::Byte>^ adjsw::F12026::RelayPacketFilter::m_StructToBytes(const void* pStruct, size_t size)
{
   auto arr = gcnew array<Byte>(static_cast<int>(size));
   pin_ptr<Byte> pinned = &arr[0];
   std::memcpy(pinned, pStruct, size);
   return arr;
}
