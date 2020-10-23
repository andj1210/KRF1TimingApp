// Copyright 2018-2020 Andreas Jung
// Permission to use, copy, modify, and /or distribute this software for any purpose with or without fee is hereby granted, provided that the above copyright noticeand this permission notice appear in all copies.
// THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS.IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.


using namespace System;
using namespace System::Runtime::InteropServices;
using namespace System::Net;
using namespace System::Net::Sockets;

#include "F12020DataDefs.h"
#include "F12020DataDefsClr.h"
#include "F12020ElementaryParser.h"

namespace adjsw::F12020
{
   public ref class F12020Parser
   {
   public:
      F12020Parser(String^ ip, int port)
      {
         m_ep = gcnew IPEndPoint(IPAddress::Parse(ip), port);
         m_socket = gcnew UdpClient(port, AddressFamily::InterNetwork);
         m_parser = new F12020ElementaryParser();
         arr = gcnew array<Byte>(4096);
         len = 0;
         pUnmanaged = Marshal::AllocHGlobal(64*1024);

         Drivers = gcnew array<DriverData^>(22);

         for (int i = 0; i < Drivers->Length; ++i)
            Drivers[i] = gcnew DriverData();
      }

      ~F12020Parser()
      {
         delete m_parser;
         Marshal::FreeHGlobal(pUnmanaged);
      }

      bool Work();

      property int CountDrivers;
      property array<DriverData^>^ Drivers;

   private:
      void m_ClearDrivers();
      void m_UpdateDrivers();
      void m_UpdateTimeDelta(DriverData^ player, int i);
      void m_UpdateTyre(int i);
      void m_UpdateDamage(int i);
      void m_UpdateTelemetry(int i);


      IPEndPoint^ m_ep;
      F12020ElementaryParser* m_parser;
      UdpClient^ m_socket;
      array<Byte>^ arr;
      IntPtr pUnmanaged;
      int len;
   };

   bool F12020Parser::Work()
   {
      try
      {
         if (m_socket->Available == 0)
            return false;

         arr = m_socket->Receive(m_ep);
         len = arr->Length;
      }
      catch (...)
      {
         return false;
      }

      if (len > (64*1024))
         return false;

      Marshal::Copy(arr, 0, pUnmanaged, len);
      auto p = reinterpret_cast<const uint8_t*>(pUnmanaged.ToPointer());

      while (len)
      {
         unsigned processed = m_parser->ProceedPacket(p, len);
         len -= processed;
         p += processed;
         m_UpdateDrivers();
      }
      return true;
   }

   void F12020Parser::m_ClearDrivers()
   {
      m_parser->event.m_eventStringCode[0] = 0; // inhibit another "SSTA" parse
      CountDrivers = 0;

      for each (DriverData^ dat in Drivers)
      {
         dat->LapNr = 0;
         dat->IsPlayer = 0;
         dat->Present = false;
         dat->TimedeltaToPlayer = 0;
         dat->LastTimedeltaToPlayer = 0;
         dat->PenaltySeconds = 0;
         dat->TyreDamage = 0;
         dat->CarDamage = 0;
         for each (LapData^ lap in dat->Laps)
         {
            lap->Sector1 = 0;
            lap->Sector2 = 0;
            lap->Lap = 0;
            lap->LapsAccumulated = 0;
         }
      }
   }


   void F12020Parser::m_UpdateDrivers()
   {
      unsigned i = 0;

      if (!strcmp((const char*)m_parser->event.m_eventStringCode, "SSTA"))
      {
         m_ClearDrivers();
      }

      if (m_parser->participants.m_numActiveCars > CountDrivers)
         CountDrivers = m_parser->participants.m_numActiveCars; // prevent left players to disappear in list


      // Lapdata + Name
      for (int i = 0; i < Drivers->Length; ++i)
      {
         Drivers[i]->SetName(m_parser->participants.m_participants[i].m_name);

         auto& lapNative = m_parser->lap.m_lapData[i];
         auto lapClr = Drivers[i]->Laps;

         Drivers[i]->Pos = lapNative.m_carPosition;

         if (Drivers[i]->LapNr != lapNative.m_currentLapNum) // Update last laptime
         {
            Drivers[i]->LapNr = lapNative.m_currentLapNum;
            if (Drivers[i]->LapNr > 0) // should always be true
            {
               lapClr[Drivers[i]->LapNr - 1]->Sector1 = 0;
               lapClr[Drivers[i]->LapNr - 1]->Sector2 = 0;
               lapClr[Drivers[i]->LapNr - 1]->Lap = 0;
            }
            if (Drivers[i]->LapNr > 1)
            {
               lapClr[Drivers[i]->LapNr - 2]->Lap = lapNative.m_lastLapTime;

               if (Drivers[i]->LapNr == 2)
                  lapClr[0]->LapsAccumulated = lapClr[0]->Lap;
               else
               {
                  lapClr[Drivers[i]->LapNr - 2]->LapsAccumulated = lapClr[Drivers[i]->LapNr - 2]->Lap + lapClr[Drivers[i]->LapNr - 3]->LapsAccumulated;
               }

            }
         }

         else if (Drivers[i]->LapNr > 0) // Update Sector1+2 if available
         {
            auto currentLap = lapClr[Drivers[i]->LapNr - 1];
            if (currentLap->Sector1 == 0)
            {
               if (lapNative.m_sector > 0)
                  currentLap->Sector1 = lapNative.m_sector1TimeInMS / 1000.0f;
            }

            if (currentLap->Sector2 == 0)
            {
               if (lapNative.m_sector > 1)
                  currentLap->Sector2 = lapNative.m_sector2TimeInMS / 1000.0f;
            }
         }
      }

      for (int i = 0; i < Drivers->Length; ++i)
      {
         Drivers[i]->IsPlayer = false;
      }

      for (int i = 0; i < m_parser->participants.m_numActiveCars; ++i)
      {
         Drivers[i]->Present = true;
      }

      for (int i = m_parser->participants.m_numActiveCars; i < Drivers->Length; ++i)
      {
         Drivers[i]->Present = false;
         Drivers[i]->TimedeltaToPlayer = 0; // triggers UI Update
      }

      if (m_parser->lap.m_header.m_playerCarIndex > Drivers->Length)
         return; // comes in visitor modes

      DriverData^ player = Drivers[m_parser->lap.m_header.m_playerCarIndex];
      player->IsPlayer = true;
      player->TimedeltaToPlayer = 0;
      player->PenaltySeconds = m_parser->lap.m_lapData[m_parser->lap.m_header.m_playerCarIndex].m_penalties;
      player->Tyre = F1Tyre(m_parser->status.m_carStatusData[m_parser->lap.m_header.m_playerCarIndex].m_actualTyreCompound);
      player->VisualTyre = F1VisualTyre(m_parser->status.m_carStatusData[m_parser->lap.m_header.m_playerCarIndex].m_visualTyreCompound);
      if (!player->LapNr)
         return;

      // update the delta Time, tyre and car damage
      for (int i = 0; i < Drivers->Length; ++i)
      {
         DriverData^ opponent = Drivers[i];
         if (!opponent->Present)
            continue;
         
         if (!opponent->IsPlayer)
            m_UpdateTimeDelta(player, i);
         
         m_UpdateTelemetry(i);
         m_UpdateTyre(i);
         m_UpdateDamage(i);

         opponent->PenaltySeconds = m_parser->lap.m_lapData[i].m_penalties;
         opponent->Tyre = F1Tyre(m_parser->status.m_carStatusData[i].m_actualTyreCompound);
         opponent->VisualTyre = F1VisualTyre(m_parser->status.m_carStatusData[i].m_visualTyreCompound);

         if (m_parser->participants.m_participants[i].m_teamId < 10)
            opponent->Team = F1Team(m_parser->participants.m_participants[i].m_teamId);

         else
            opponent->Team = F1Team::Classic;
      }
   }

   void F12020Parser::m_UpdateTimeDelta(DriverData^ player, int i)
   {
      DriverData^ opponent = Drivers[i];
      if (opponent->IsPlayer || (!opponent->Present))
         return;

      // player passed Sector 2:
      bool found = false;
      int lapIdx = player->LapNr - 1;
      unsigned lapSector = 2;

      // search the greatest Sector time both cars have
      while (!found)
      {
         if ((opponent->LapNr - 1) < lapIdx)
         {
            --lapIdx;
            lapSector = 2;
            continue;
         }

         switch (lapSector)
         {
         case 0:
            if ((player->Laps[lapIdx]->Sector1) && opponent->Laps[lapIdx]->Sector1)
            {
               found = true;
            }
            break;

         case 1:
            if ((player->Laps[lapIdx]->Sector2) && opponent->Laps[lapIdx]->Sector2)
            {
               found = true;
            }
            break;

         case 2:
            if ((player->Laps[lapIdx]->Lap) && opponent->Laps[lapIdx]->Lap)
            {
               found = true;
            }
            break;
         }

         if (!found)
         {
            if ((lapIdx == 0) && (lapSector == 0))
               break;

            if (lapSector)
               --lapSector;
            else
            {
               --lapIdx;
               lapSector = 2;
            }
            continue;
         }

         float timePlayer = 0;
         float timeOpponent = 0;

         if (found)
         {
            if (lapIdx > 0)
            {
               timePlayer = player->Laps[lapIdx - 1]->LapsAccumulated;
               timeOpponent = opponent->Laps[lapIdx - 1]->LapsAccumulated;
            }

            switch (lapSector)
            {
            case 0:
               timePlayer += player->Laps[lapIdx]->Sector1;
               timeOpponent += opponent->Laps[lapIdx]->Sector1;
               break;

            case 1:
               timePlayer += player->Laps[lapIdx]->Sector1 + player->Laps[lapIdx]->Sector2;
               timeOpponent += opponent->Laps[lapIdx]->Sector1 + opponent->Laps[lapIdx]->Sector2;
               break;

            case 2:
               timePlayer += player->Laps[lapIdx]->Lap;
               timeOpponent += opponent->Laps[lapIdx]->Lap;
               break;
            }
         }
         auto newDelta = timePlayer - timeOpponent;
         newDelta -= m_parser->lap.m_lapData[i].m_penalties;
         if (newDelta != opponent->TimedeltaToPlayer)
         {
            opponent->LastTimedeltaToPlayer = opponent->TimedeltaToPlayer;
            opponent->TimedeltaToPlayer = newDelta;
         }
      }

   }

   void F12020Parser::m_UpdateTyre(int i)
   {
      DriverData^ driver = Drivers[i];
      if (!driver->Present)
         return;

      auto tyres = m_parser->status.m_carStatusData[i].m_tyresDamage;
      float tyreStatus = static_cast<float>(tyres[0] + tyres[1] + tyres[2] + tyres[3]);
      tyreStatus /= 400;

      // map 75% -> 100% ... 0% -> 0%
      if (tyreStatus >= 0.75f)
         tyreStatus = 1;
      else
      {
         tyreStatus *= (1.f / 0.75f);
      }
      driver->TyreDamage = tyreStatus;

      driver->WearDetail->WearFrontLeft = m_parser->status.m_carStatusData[i].m_tyresWear[2];
      driver->WearDetail->WearFrontRight = m_parser->status.m_carStatusData[i].m_tyresWear[3];
      driver->WearDetail->WearRearLeft = m_parser->status.m_carStatusData[i].m_tyresWear[0];
      driver->WearDetail->WearRearRight = m_parser->status.m_carStatusData[i].m_tyresWear[1];
   }

   void F12020Parser::m_UpdateDamage(int i)
   {
      DriverData^ driver = Drivers[i];
      if (!driver->Present)
         return;

      float damage = m_parser->status.m_carStatusData[i].m_frontLeftWingDamage;
      damage += m_parser->status.m_carStatusData[i].m_frontRightWingDamage;
      damage += m_parser->status.m_carStatusData[i].m_rearWingDamage;
      damage /= 300;

      driver->WearDetail->DamageFrontLeft = m_parser->status.m_carStatusData[i].m_frontLeftWingDamage;
      driver->WearDetail->DamageFrontRight = m_parser->status.m_carStatusData[i].m_frontRightWingDamage;

      // map 50% -> 100% ... 0% -> 0%
      if (damage >= 0.5f)
         damage = 1;
      else
      {
         damage *= (1.f / 0.5f);
      }
      driver->CarDamage = damage;
   }

   void F12020Parser::m_UpdateTelemetry(int i)
   {
      DriverData^ driver = Drivers[i];
      if (!driver->Present)
         return;

      driver->WearDetail->TempFrontLeftInner = m_parser->telemetry.m_carTelemetryData[i].m_tyresInnerTemperature[2];
      driver->WearDetail->TempFrontRightInner = m_parser->telemetry.m_carTelemetryData[i].m_tyresInnerTemperature[3];
      driver->WearDetail->TempRearLeftInner = m_parser->telemetry.m_carTelemetryData[i].m_tyresInnerTemperature[0];
      driver->WearDetail->TempRearRightInner = m_parser->telemetry.m_carTelemetryData[i].m_tyresInnerTemperature[1];

      driver->WearDetail->TempFrontLeftOuter = m_parser->telemetry.m_carTelemetryData[i].m_tyresSurfaceTemperature[2];
      driver->WearDetail->TempFrontRightOuter = m_parser->telemetry.m_carTelemetryData[i].m_tyresSurfaceTemperature[3];
      driver->WearDetail->TempRearLeftOuter = m_parser->telemetry.m_carTelemetryData[i].m_tyresSurfaceTemperature[0];
      driver->WearDetail->TempRearRightOuter = m_parser->telemetry.m_carTelemetryData[i].m_tyresSurfaceTemperature[1];

      driver->WearDetail->TempBrakeFrontLeft = m_parser->telemetry.m_carTelemetryData[i].m_brakesTemperature[2];
      driver->WearDetail->TempBrakeFrontRight = m_parser->telemetry.m_carTelemetryData[i].m_brakesTemperature[3];
      driver->WearDetail->TempBrakeRearLeft = m_parser->telemetry.m_carTelemetryData[i].m_brakesTemperature[0];
      driver->WearDetail->TempBrakeRearRight = m_parser->telemetry.m_carTelemetryData[i].m_brakesTemperature[1];

      driver->WearDetail->TempEngine = m_parser->telemetry.m_carTelemetryData[i].m_engineTemperature;
   }

}

