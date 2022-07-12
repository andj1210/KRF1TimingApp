// Copyright 2018-2021 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

#include "F12022ElementaryParser.h"

#include <fstream>
#include <type_traits>


unsigned F12020ElementaryParser::ProceedPacket(const uint8_t* pData, unsigned len)
{
   if (len < sizeof(PacketHeader))
      return len;

   PacketHeader hdr;
   memcpy(&hdr, pData, sizeof(PacketHeader));
   if ((hdr.m_packetFormat != 2022) || (hdr.m_packetVersion != 1)) // m_packetversion refers probably to each individual packet type, for now they should all be "1"
      return len;

   switch (hdr.m_packetId)
   {
   case 0:
      memcpy(&motion, pData, sizeof(motion));
      return sizeof(motion);
      break;

   case 1:
      memcpy(&session, pData, sizeof(session));
      return sizeof(session);
      break;

   case 2:
      memcpy(&lap, pData, sizeof(lap));
      return sizeof(lap);
      break;

   case 3:
      memcpy(&event, pData, sizeof(event));

      // Clear old Data when a new event starts
      if (!strncmp((const char*)event.m_eventStringCode, "SSTA", 4))
      {
         auto eventCpy = this->event;
         *this = F12020ElementaryParser();
         this->event = eventCpy;
      }

      return sizeof(event);
      break;

   case 4:
      memcpy(&participants, pData, sizeof(participants));
      return sizeof(participants);
      break;

   case 5:
      memcpy(&setups, pData, sizeof(setups));
      return sizeof(setups);
      break;

   case 6:
      memcpy(&telemetry, pData, sizeof(telemetry));
      return sizeof(telemetry);
      break;

   case 7:
      memcpy(&status, pData, sizeof(status));
      return sizeof(status);
      break;

   case 8:
      memcpy(&classification, pData, sizeof(classification));
      return sizeof(classification);
      break;

   case 9:
      memcpy(&lobby, pData, sizeof(lobby));
      return sizeof(lobby);
      break;

   case 10:
      memcpy(&cardamage, pData, sizeof(cardamage));
      return sizeof(cardamage);
      break;

   case 11:
      memcpy(&histoy, pData, sizeof(histoy));
      return sizeof(histoy);
      break;
   }
   return len;
}

const char* IdToTrackName(unsigned i)
{
   switch (i)
   {
   default:
      return "unknown";

   case 0:
      return "Melbourne";

   case 1:
      return "Paul Ricard";

   case 2:
      return "Shanghai";
   case 3:
      return "Sakhir";
   case 4:
      return "Catalunya";

   case 5:
      return "Monaco";

   case 6:
      return "Montreal";

   case 7:
      return "Silverstone";

   case 8:
      return "Hockenheim";

   case 9:
      return "Hungaroring";

   case 10:
      return "Spa";

   case 11:
      return "Monza";

   case 12:
      return "Signapore";

   case 13:
      return "Suzuka";

   case 14:
      return "Abu Dhabi";

   case 15:
      return "Texas";
   case 16:
      return "Brazil";

   case 17:
      return "Austria";

   case 18:
      return"Sochi";

   case 19:
      return "Mexico";

   case 20:
      return "Baku";

   case 21:
      return "SakhirShort";

   case 22:
      return "SilverstoneShort";

   case 23:
      return "TexasShort";

   case 24:
      return "SuzukaShort";
   }
}
