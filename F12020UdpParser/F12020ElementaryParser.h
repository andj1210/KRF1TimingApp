// Copyright 2018-2020 Andreas Jung
// Permission to use, copy, modify, and/or distribute this software for any purpose with or without fee is hereby granted, provided that the above copyright notice and this permission notice appear in all copies.
// THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.

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
