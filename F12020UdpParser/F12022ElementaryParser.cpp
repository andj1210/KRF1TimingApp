// Copyright 2018-2021 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

#include "F12022ElementaryParser.h"

#include <fstream>
#include <type_traits>

unsigned F12020ElementaryParser::ProceedPacket(const uint8_t* pData, unsigned len, PacketType* pType)
{
   if (len < sizeof(PacketHeader))
      return len;

   PacketHeader hdr;
   PacketType type = PacketType::UnknownOrIllformed;
   if (pType)
      *pType = type;

   memcpy(&hdr, pData, sizeof(PacketHeader));
   if ((hdr.m_packetFormat != 2022) || (hdr.m_packetVersion != 1)) // m_packetversion refers probably to each individual packet type, for now they should all be "1"
      return len;

   switch (hdr.m_packetId)
   {
   case 0:
      if (CopyBytesToStruct(pData, len, &motion))
      {
         type = PacketType::PacketMotionData;
      }
      else
         return len;
      break;

   case 1:
      if (CopyBytesToStruct(pData, len, &session))
      {
         type = PacketType::PacketSessionData;
      }
      else
         return len;
      break;

   case 2:
      if (CopyBytesToStruct(pData, len, &lap))
      {
         type = PacketType::PacketLapData;
      }
      else
         return len;
      break;

   case 3:
      if (CopyBytesToStruct(pData, len, &event))
      {
         // Clear old Data when a new event starts
         if (!strncmp((const char*)event.m_eventStringCode, "SSTA", 4))
         {
            auto eventCpy = this->event;
            *this = F12020ElementaryParser();
            this->event = eventCpy;
         }
         type = PacketType::PacketEventData;
      }
      else
         return len;
      break;

   case 4:
      if (CopyBytesToStruct(pData, len, &participants))
      {
         type = PacketType::PacketParticipantsData;
      }
      else
         return len;
      break;

   case 5:
      if (CopyBytesToStruct(pData, len, &setups))
      {
         type = PacketType::PacketCarSetupData;
      }
      else
         return len;
      break;

   case 6:
      if (CopyBytesToStruct(pData, len, &telemetry))
      {
         type = PacketType::PacketCarTelemetryData;
      }
      else
         return len;
      break;

   case 7:
      if (CopyBytesToStruct(pData, len, &status))
      {
         type = PacketType::PacketCarStatusData;
      }
      else
         return len;
      break;

   case 8:
      if (CopyBytesToStruct(pData, len, &classification))
      {
         type = PacketType::PacketFinalClassificationData;
      }
      else
         return len;
      break;

   case 9:
      if (CopyBytesToStruct(pData, len, &lobby))
      {
         type = PacketType::PacketLobbyInfoData;
      }
      else
         return len;
      break;

   case 10:
      if (CopyBytesToStruct(pData, len, &cardamage))
      {
         type = PacketType::PacketCarDamageData;
      }
      else
         return len;
      break;

   case 11:
      if (CopyBytesToStruct(pData, len, &histoy))
      {
         type = PacketType::PacketSessionHistoryData;
      }
      else
         return len;
      break;
   }

   if (pType)
      *pType = type;

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

   case 25:
      return "Hanoi";

   case 26:
      return "Zandboort";

   case 27:
      return "Imola";

   case 28:
      return "Portimao";

   case 29:
      return "Jeddah";

   case 30:
      return "Miami";
   }
}
