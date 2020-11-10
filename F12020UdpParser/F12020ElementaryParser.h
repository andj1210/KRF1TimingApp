// Copyright 2018-2020 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include <stdint.h>
#include <string.h>
#include <fstream>
#include "F12020DataDefs.h"

class PacketForwardReader
{
public:
   PacketForwardReader(const uint8_t* pData, unsigned left) : m_pData(pData), m_left(left) {}

   bool Read(uint8_t* pDst, unsigned cnt)
   {
      if (cnt > m_left)
      {
         m_left = 0;
         return false;
      }

      memcpy(pDst, m_pData, cnt);
      m_pData += cnt;
      m_left -= cnt;
      return true;
   }

   unsigned Left() { return m_left; }

private:
   const uint8_t* m_pData;
   unsigned m_left;
};

struct F12020ElementaryParser
{
   unsigned ProceedPacket(const uint8_t* pData, unsigned len);

   PacketMotionData motion{};
   PacketSessionData session{};
   PacketLapData lap{};
   PacketEventData event {};
   PacketParticipantsData participants{};
   PacketCarSetupData setups{};
   PacketCarTelemetryData telemetry{};
   PacketCarStatusData status{};
};
