// Copyright 2018-2021 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include <stdint.h>
#include <string.h>
#include <fstream>
#include "F1DataDefs.h"

struct F12023_PacketExtractor
{
   unsigned ProceedPacket(const uint8_t* pData, unsigned len, PacketType* pType = nullptr);

   uint64_t sessionUID{ 0 };
   float sessionTime{0};
   PacketHeader lastHeader{};
   PacketMotionData motion{};
   PacketSessionData session{};
   PacketLapData lap{};
   PacketEventData event {};
   PacketParticipantsData participants{};
   PacketCarSetupData setups{};
   PacketCarTelemetryData telemetry{};
   PacketCarStatusData status{};
   PacketFinalClassificationData classification{};
   PacketLobbyInfoData lobby{};
   PacketCarDamageData cardamage{};
   PacketSessionHistoryData history{};
   PacketTyreSetsData tyreSets{};
   PacketMotionExData motionEx{};


   template<typename PKT_TYPE>
   static bool CopyBytesToStruct(const uint8_t* pData, unsigned& len, PKT_TYPE* pPkt);
};


template<typename PKT_TYPE>
bool F12023_PacketExtractor::CopyBytesToStruct(const uint8_t* pData, unsigned& len, PKT_TYPE* pPkt)
{
   if (sizeof(PKT_TYPE) <= len)
   {
      memcpy(pPkt, pData, sizeof(PKT_TYPE));
      len = sizeof(PKT_TYPE);
      return true;
   }

   return false;
}
