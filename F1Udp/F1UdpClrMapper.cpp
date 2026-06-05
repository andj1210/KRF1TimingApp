// Copyright 2018-2021 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

#include "F1UdpClrMapper.h"
#include "RelayPacketFilter.h"

//#define FAKE_SECOND_CAR_ENG
//#define FAKE_SECOND_CAR_DRIVER

#define REMAP_26_TO_25_CARS // during the f1 26 period, thread Teams of F1-26 (SP) as the regular teams of F1-25

#ifdef REMAP_26_TO_25_CARS
uint16_t s_TELEMETRY_TEAM_ID_REMAP(uint16_t id)
{
   if (
      (id >= 476)       // MGP 26
      && (id <= 484)    // Haas
      )
      return id - 476; // map MGP26 to MGP and so on

   if (id == 485) // audi
      return 10;  // F1Team::Audi

   if (id == 486) // cadillac
      return 11;  // F1Team::Cadillac

   return id;
}
#endif

namespace adjsw::F12026
{
   void RelayPacketFilter::SetUdpMapper(adjsw::F12026::F1UdpClrMapper^ mapper)
   {
      m_mapper = mapper;
      m_SetExtractor(mapper->GetExtractor());
   }

   F1UdpClrMapper::F1UdpClrMapper()
   {
      m_parser = new F12026_PacketExtractor();
      m_parserSecondary = new F12026_PacketExtractor();
      arr = gcnew array<Byte>(4096);
      len = 0;
      pUnmanaged = Marshal::AllocHGlobal(512 * 1024);
      m_pUnmanagedSecondary = Marshal::AllocHGlobal(512 * 1024);

      SessionInfo = gcnew adjsw::F12026::SessionInfo();
      EventList = gcnew SessionEventList();
      UdpAction = gcnew array<bool>(12);

      Drivers = gcnew array<DriverData^>(cs_maxNumCarsInUDPData);
      m_arrDriverHistoryDirty = gcnew array<volatile bool>(cs_maxNumCarsInUDPData);

      for (int i = 0; i < Drivers->Length; ++i)
         Drivers[i] = gcnew DriverData(SessionInfo);
   }

   F1UdpClrMapper::~F1UdpClrMapper()
   {
      delete m_parser;
      delete m_parserSecondary;
      Marshal::FreeHGlobal(pUnmanaged);
      Marshal::FreeHGlobal(m_pUnmanagedSecondary);
   }

   bool F1UdpClrMapper::Proceed(array<System::Byte>^ input)
   {
      arr = input;
      len = input->Length;

      if (len > (512 * 1024))
         return false;

      Marshal::Copy(arr, 0, pUnmanaged, len);
      auto p = reinterpret_cast<const uint8_t*>(pUnmanaged.ToPointer());

      while (len)
      {
         PacketType tp = PacketType::UnknownOrIllformed;
         unsigned processed = m_parser->ProceedPacket(p, len, &tp);
         len -= processed;
         p += processed;
         ++m_pktCtr;
         m_lastPacketType = tp;

         if ((tp != PacketType::UnknownOrIllformed) && (m_sessionId != m_parser->lastHeader.m_sessionUID))
         {
            if (m_parser->lastHeader.m_sessionUID != 0) // dont clear data when just going back to menu (session id == 0)
            {
               m_Clear();
               m_sessionId = m_parser->lastHeader.m_sessionUID;
               m_sessionConnectTime = m_parser->lastHeader.m_sessionTime;
            }

         }

         if (m_parser->lastHeader.m_sessionUID)
            m_sessionId = m_parser->lastHeader.m_sessionUID;

         switch (tp)
         {
         case PacketType::PacketSessionData:
            m_UpdateSession();
            break;

         case PacketType::PacketLapData:
            if (m_IsQualifyingOrPractice())
               m_UpdateLapQuali();
            else
               m_UpdateLapRace();

            m_UpdateDrivers();
            break;

         case PacketType::PacketEventData:
            m_UpdateEventData();
            break;

         case PacketType::PacketParticipantsData:
            m_UpdateParticipants();
            break;

         case PacketType::PacketCarTelemetryData:
            for (int i = 0; i < Drivers->Length; ++i)
            {
               m_UpdateTelemetry(i);
            }
            break;

         case PacketType::PacketCarTelemetry2Data:
            for (int i = 0; i < Drivers->Length; ++i)
            {
               m_UpdateTelemetry2(i);
            }
            break;

         case PacketType::PacketCarStatusData:
         case PacketType::PacketLobbyInfoData:
            m_UpdateDrivers();
            break;

         case PacketType::PacketFinalClassificationData:
            m_UpdateClassification();
            break;

         case PacketType::PacketCarDamageData:
            for (int i = 0; i < Drivers->Length; ++i)
            {
               if (i == m_secondaryDriverIndex)
                  continue; // damage for this slot is authoritative from ProceedSecondary
               m_UpdateDamage(i);
            }
            break;

         case PacketType::PacketSessionHistoryData:
            if (m_IsQualifyingOrPractice())
               m_UpdateHistoryDataQuali();
            else
               m_UpdateHistoryDataRace();
            break;

         case PacketType::PacketTyreSetsData:
            m_UpdateTyreSetsData();
            break;

         case PacketType::PacketLapPositions:
            m_UpdateCarPositions();
            break;

         case PacketType::PacketMotionData:
            m_UpdateCarPositions3d();
            break;

            // unused / unknown packet types:
         case PacketType::PacketCarSetupData:
         case PacketType::PacketMotionExData:
         case PacketType::PacketTimeTrialData:
         case PacketType::UnknownOrIllformed:
         default:
            break;
         }
      }
      return true;
   }

   bool F1UdpClrMapper::ProceedSecondary(array<System::Byte>^ input)
   {
      if (input == nullptr || input->Length == 0)
         return false;

      int secLen = input->Length;
      if (secLen > (512 * 1024))
         return false;

      Marshal::Copy(input, 0, m_pUnmanagedSecondary, secLen);
      auto p = reinterpret_cast<const uint8_t*>(m_pUnmanagedSecondary.ToPointer());

      while (secLen)
      {
         PacketType tp = PacketType::UnknownOrIllformed;
         unsigned processed = m_parserSecondary->ProceedPacket(p, secLen, &tp);
         secLen -= processed;
         p += processed;

         // m_playerCarIndex in the secondary stream is only valid within that stream's own car ordering.
         // Use it to read identity fields from the secondary participants list.
         uint8_t sec_idx = m_parserSecondary->lastHeader.m_playerCarIndex;
         if (sec_idx >= static_cast<uint8_t>(cs_maxNumCarsInUDPData))
            continue;

         // On a participants packet re-resolve the secondary driver index.
         if (tp == PacketType::PacketParticipantsData)
         {
            uint16_t sec_team    = s_TELEMETRY_TEAM_ID_REMAP(m_parserSecondary->participants.m_participants[sec_idx].m_teamId);
            uint8_t sec_race_nr = m_parserSecondary->participants.m_participants[sec_idx].m_raceNumber;

            // 1st pass -- exact match on team + race number.
            int match_idx = -1;
            for (int i = 0; i < Drivers->Length; ++i)
            {
               if (Drivers[i]->DriverNr == sec_race_nr && static_cast<int>(Drivers[i]->Team) == sec_team)
               {
                  match_idx = i;
                  break;
               }
            }

            // 2nd pass -- team-only fallback: prefer a non-MainDriver slot; if none exists take
            // the second car of the team (i.e. skip the first team member found).
            if (match_idx < 0)
            {
               int first_team_match = -1;
               for (int i = 0; i < Drivers->Length; ++i)
               {
                  if (static_cast<int>(Drivers[i]->Team) != sec_team)
                     continue;

                  if (!Drivers[i]->IsMainDriver)
                  {
                     match_idx = i;
                     break;
                  }

                  if (first_team_match < 0)
                     first_team_match = i; // remember first (MainDriver) slot
               }

               // All team slots are MainDriver -- pick the second one found.
               if (match_idx < 0 && first_team_match >= 0)
               {
                  for (int i = first_team_match + 1; i < Drivers->Length; ++i)
                  {
                     if (static_cast<int>(Drivers[i]->Team) == sec_team)
                     {
                        match_idx = i;
                        break;
                     }
                  }
               }
            }

            if (match_idx < 0)
               continue;

            m_secondaryDriverIndex = match_idx;

            // Clear IsSecondaryDriver on any slot that no longer holds the secondary driver.
            for (int i = 0; i < Drivers->Length; ++i)
               Drivers[i]->IsSecondaryDriver = (i == match_idx);
         }

         // Apply damage using the already-resolved index.
         if (m_secondaryDriverIndex < 0 || m_secondaryDriverIndex >= Drivers->Length)
            continue;

         switch (tp)
         {
         case PacketType::PacketCarDamageData:
         {
            DriverData^ driver = Drivers[m_secondaryDriverIndex];
            if (driver->Present)
            {
               const auto& dmg = m_parserSecondary->cardamage.m_carDamageData[sec_idx];

               // Tyre wear
               driver->WearDetail->WearFrontLeft  = static_cast<int>(dmg.m_tyresWear[2]);
               driver->WearDetail->WearFrontRight = static_cast<int>(dmg.m_tyresWear[3]);
               driver->WearDetail->WearRearLeft   = static_cast<int>(dmg.m_tyresWear[0]);
               driver->WearDetail->WearRearRight  = static_cast<int>(dmg.m_tyresWear[1]);

               // Wing damage
               driver->WearDetail->DamageFrontLeft  = dmg.m_frontLeftWingDamage;
               driver->WearDetail->DamageFrontRight = dmg.m_frontRightWingDamage;
            }
            break;
         }

         case PacketType::PacketCarTelemetryData:
         {
            DriverData^ driver = Drivers[m_secondaryDriverIndex];
            if (driver->Present)
            {
               const auto& tel = m_parserSecondary->telemetry.m_carTelemetryData[sec_idx];

               driver->WearDetail->TempFrontLeftInner = tel.m_tyresInnerTemperature[2];
               driver->WearDetail->TempFrontRightInner = tel.m_tyresInnerTemperature[3];
               driver->WearDetail->TempRearLeftInner = tel.m_tyresInnerTemperature[0];
               driver->WearDetail->TempRearRightInner = tel.m_tyresInnerTemperature[1];

               driver->WearDetail->TempFrontLeftOuter = tel.m_tyresSurfaceTemperature[2];
               driver->WearDetail->TempFrontRightOuter = tel.m_tyresSurfaceTemperature[3];
               driver->WearDetail->TempRearLeftOuter = tel.m_tyresSurfaceTemperature[0];
               driver->WearDetail->TempRearRightOuter = tel.m_tyresSurfaceTemperature[1];

               driver->WearDetail->TempBrakeFrontLeft = tel.m_brakesTemperature[2];
               driver->WearDetail->TempBrakeFrontRight = tel.m_brakesTemperature[3];
               driver->WearDetail->TempBrakeRearLeft = tel.m_brakesTemperature[0];
               driver->WearDetail->TempBrakeRearRight = tel.m_brakesTemperature[1];

               driver->WearDetail->TempEngine = tel.m_engineTemperature;
            }
            break;
         }

         case PacketType::PacketCarTelemetry2Data:
         {
            DriverData^ driver = Drivers[m_secondaryDriverIndex];
            if (driver->Present)
            {
               const auto& tel = m_parserSecondary->telemetry2.m_carTelemetry2Data[sec_idx];

               driver->WearDetail->OvertakeAvailable = tel.m_overtakeAvailable;
            }
            break;
         }

         case PacketType::PacketCarStatusData:
         {
            DriverData^ driver = Drivers[m_secondaryDriverIndex];
            if (driver->Present)
            {
               const auto& tel = m_parserSecondary->status.m_carStatusData[sec_idx];
               driver->WearDetail->ErsMode = CarDetailErsMode(tel.m_ersDeployMode);
               driver->WearDetail->ErsAvailPercent = tel.m_ersStoreEnergy / 4000000.f; // 4 MJ max store = 100%
            }
         }
            break;

         default:
            break;
         }
      } // while (secLen)
      return true;
   }

   void F1UdpClrMapper::InsertTestData()
   {
      m_Clear();

      std::mt19937 engine;

      constexpr unsigned CNT_SIMDATA = 20;
      constexpr unsigned PLAYER_IDX = 0;
      constexpr unsigned LAPS = 4;
      assert(CNT_SIMDATA <= Drivers->Length);

      SessionInfo->Session = SessionType::Race;
      SessionInfo->SessionFinshed = false;
      SessionInfo->TotalLaps = 10;
      SessionInfo->CurrentLap = 5;
      SessionInfo->EventTrack = Track::Austria;

      CountDrivers = CNT_SIMDATA;

      // insert names
      for (unsigned i = 0; i < CNT_SIMDATA; ++i)
      {
         DriverData^ driver = Drivers[i];
         driver->TelemetryName = "Dummy Data " + (i + 1);
         driver->Present = true;
         driver->VisualTyre = F1VisualTyre::Soft;

         if (i == 2)
            driver->VisualTyre = F1VisualTyre::Medium;

         if (i == 3)
            driver->VisualTyre = F1VisualTyre::Hard;

         if (i == 4)
            driver->VisualTyre = F1VisualTyre::Intermediate;

         if (i == 5)
            driver->VisualTyre = F1VisualTyre::Wet;

         if (i == 6)
            driver->VisualTyres->Add(F1VisualTyre::Medium);

         driver->VisualTyres->Add(driver->VisualTyre);
         driver->VisualTyres = driver->VisualTyres;
      }
      Drivers[PLAYER_IDX]->TelemetryName = "Player";
      Drivers[PLAYER_IDX]->IsPlayer = true;

      // insert laptimes
      std::normal_distribution<float> dist(33.f, 2.f);
      for (unsigned i = 0; i < CNT_SIMDATA; ++i)
      {
         DriverData^ driver = Drivers[i];

         for (unsigned j = 0; j < LAPS; ++j)
         {
            driver->Laps[j]->Sector1 = dist(engine);
            driver->Laps[j]->Sector2 = dist(engine);
            driver->Laps[j]->Lap = driver->Laps[j]->Sector1 + driver->Laps[j]->Sector2 + dist(engine);
         }
         driver->LapNr = LAPS;
         driver->Status = DriverStatus::OnTrack;
      }

      // update accumulated laptimes
      for (unsigned j = 0; j < CNT_SIMDATA; ++j)
      {
         DriverData^ driver = Drivers[j];
         double driverTimeAfterLap = 0.f;
         for (unsigned i = 0; i < LAPS; ++i)
         {
            driverTimeAfterLap += Drivers[j]->Laps[i]->Lap;
            Drivers[j]->Laps[i]->LapsAccumulated = driverTimeAfterLap;
         }
      }

      // update delta to player
      {
         double playerTimeAfterLap = Drivers[PLAYER_IDX]->Laps[LAPS - 1]->LapsAccumulated;
         double playerTimeBeforeLastSector =
            playerTimeAfterLap
            - Drivers[PLAYER_IDX]->Laps[LAPS - 1]->Lap
            + Drivers[PLAYER_IDX]->Laps[LAPS - 1]->Sector1
            + Drivers[PLAYER_IDX]->Laps[LAPS - 1]->Sector2;
      }

      // update positions
      array<DriverData^>^ driversSort = gcnew array<DriverData^>(CNT_SIMDATA);
      for (unsigned i = 0; i < CNT_SIMDATA; ++i)
      {
         driversSort[i] = Drivers[i];
      }

      for (unsigned j = 0; j < CNT_SIMDATA; ++j)
      {
         double bestTime = 999999.f;
         unsigned bestIdx = 0;
         for (unsigned i = 0; i < CNT_SIMDATA; ++i)
         {
            if ((driversSort[i] != nullptr) && driversSort[i]->Laps[LAPS - 1]->LapsAccumulated < bestTime)
            {
               bestIdx = i;
               bestTime = driversSort[i]->Laps[LAPS - 1]->LapsAccumulated;
            }
         }
         driversSort[bestIdx]->Pos = j + 1;
         driversSort[bestIdx] = nullptr;
      }

      // update car status
      Drivers[PLAYER_IDX]->WearDetail->WearFrontLeft = 39;
      Drivers[PLAYER_IDX]->WearDetail->WearFrontRight = 12;
      Drivers[PLAYER_IDX]->WearDetail->WearRearLeft = 88;
      Drivers[PLAYER_IDX]->WearDetail->WearRearRight = 19;
      Drivers[PLAYER_IDX]->WearDetail->DamageFrontLeft = 35;
      Drivers[PLAYER_IDX]->WearDetail->TempFrontLeftOuter = 130;
      Drivers[PLAYER_IDX]->WearDetail->TempFrontLeftInner = 95;
      Drivers[PLAYER_IDX]->WearDetail->TempFrontRightOuter = 100;
      Drivers[PLAYER_IDX]->WearDetail->TempFrontRightInner = 77;
   }

   void F1UdpClrMapper::m_Clear()
   {
      m_pktCtr = 0;
      SessionInfo->SessionFinshed = false;
      SessionInfo->CurrentLap = 1;
      SessionInfo->FastestSector1 = 999.0;
      SessionInfo->FastestSector2 = 999.0;
      SessionInfo->FastestSector3 = 999.0;
      EventList->Events->Clear();
      EventList = EventList; // force NPC
      CountDrivers = 0;
      Classification = nullptr;
      m_parser->classification.m_numCars = 0;
      m_udpButtonPreviousMask = 0;

      for (int i = 0; i < Drivers->Length; ++i)
      {
         Drivers[i]->Reset();
         Drivers[i]->Id = i;
      }
   }

   void F1UdpClrMapper::m_UpdateDrivers()
   {
      // Name + Team
      DriverData^ player = nullptr;

      if (m_parser->lap.m_header.m_playerCarIndex < Drivers->Length) // in visitor modes index is 255
         player = Drivers[m_parser->lap.m_header.m_playerCarIndex];

      bool qualyfiyingDelta = false; // (for training or Q1-Q3 use bestlap delta)      
      switch (SessionInfo->Session)
      {
      case SessionType::P1:
      case SessionType::P2:
      case SessionType::P3:
      case SessionType::ShortPractice:
      case SessionType::Q1:
      case SessionType::Q2:
      case SessionType::Q3:
      case SessionType::ShortQ:
         qualyfiyingDelta = true;
         break;
      default: break;
      }

      // find present drivers + set position
      for (int i = 0; i < Drivers->Length; ++i)
      {
         if ((m_parser->lap.m_lapData[i].m_resultStatus > 0) && (m_parser->lap.m_lapData[i].m_resultStatus < 4))
            Drivers[i]->Present = true;

         float pos = m_parser->lap.m_lapData[i].m_lapDistance;
         if (pos < 0.f)
            pos = SessionInfo->TrackLength + pos;

         pos /= SessionInfo->TrackLength;
         if (pos > 1.f)
            pos = 1.f;

         Drivers[i]->TrackPositionPerc = pos;
      }

      // find leader (if available)
      DriverData^ leader = nullptr;
      for (int i = 0; i < Drivers->Length; ++i)
      {
         DriverData^ car = Drivers[i];
         if (car->Pos == 1)
         {
            leader = car;
            leader->TimedeltaToLeader = 0;
            break;
         }
      }

      // m_parser->lap.m_header.m_playerCarIndex defaults to 0 and might change when the first actual packet arrives
      // which means we must check, if we declared first car 0 by accident as player and revert in that case!
      if (m_parser->lap.m_header.m_playerCarIndex != 0)
      {
         Drivers[0]->IsPlayer = false;
         Drivers[0]->IsMainDriver = false;
      }

      if (player)
      {
         if (m_mode == MapperMode::Direct)
         {
            player->IsPlayer = true;
#ifdef FAKE_SECOND_CAR_DRIVER
            if (m_parser->lap.m_header.m_playerCarIndex == 0)
            {
               Drivers[0]->IsSecondaryDriver = true;
            }
            else
            {
               Drivers[1]->IsSecondaryDriver = true;
            }
#endif
         }
         else
         {
            player->IsMainDriver = true;
            player->IsPlayer = false;
#ifdef FAKE_SECOND_CAR_ENG
            if (m_parser->lap.m_header.m_playerCarIndex == 0)
            {
               Drivers[0]->IsSecondaryDriver = true;
            }
            else
            {
               Drivers[1]->IsSecondaryDriver = true;
            }
#endif
         }

         if (!player->LapNr)
            return;
      }

      // update the delta Time, tyre and car damage
      for (int i = 0; i < Drivers->Length; ++i)
      {
         DriverData^ car = Drivers[i];
         if (!car->Present)
            continue;

         Drivers[i]->LocationOnTrack = m_parser->lap.m_lapData[i].m_lapDistance;

         // delta to leader
         if (leader && (car != leader))
         {
            if (qualyfiyingDelta)
               m_UpdateTimeDeltaQualy(leader, i);
            else
            {
               m_UpdateTimeDeltaRace(leader, i);
            }
         }

         car->PenaltySeconds = m_parser->lap.m_lapData[i].m_penalties;

         car->Tyre = F1Tyre(m_parser->status.m_carStatusData[i].m_actualTyreCompound);
         car->VisualTyre = F1VisualTyre(m_parser->status.m_carStatusData[i].m_visualTyreCompound);
         if (!m_IsQualifyingOrPractice() && !car->VisualTyres->Count && (static_cast<int>(car->VisualTyre) != 0) && (m_parser->sessionTime - m_sessionConnectTime) > 2)
         {
            if (car->LapNr > 1)
            {
               if ((car->LapNr - car->TyreAge) > 1)
               {
                  // probably we joint late to a session, add a unknown tyre to the from of the list
                  car->VisualTyres->Add(F1VisualTyre::Unknown);
               }
            }

            // add the first tyre at the start of race
            car->VisualTyres->Add(car->VisualTyre);
            car->NPC("VisualTyres");
         }

         car->TyreAge = m_parser->status.m_carStatusData[i].m_tyresAgeLaps;

         DriverStatus oldDriverStatus = car->Status;

         switch (m_parser->lap.m_lapData[i].m_resultStatus)
         {
            // Result status - 0 = invalid, 1 = inactive, 2 = active
            // 3 = finished, 4 = didnotfinish, 5 = disqualified
            // 6 = not classified, 7 = retired
         case 4:
            car->Status = DriverStatus::DNF;
            break;
         case 5:
            car->Status = DriverStatus::DSQ;
            break;

         case 6:
            // "not classified", what does it actually mean?!
            car->Status = DriverStatus::Garage;
            break;

         case 7:
            if (car->PitPenalties->Count &&
               (car->PitPenalties[car->PitPenalties->Count - 1]->PenaltyType == PenaltyTypes::Retired))
               car->Status = DriverStatus::DNF;

            if (car->Status != DriverStatus::DNF) // dnf is not reported correctly and detected by penalty (terminally damaged), so do not change to "retrired" here if already DNF detected
               car->Status = DriverStatus::Retired;

            break;

         default:
            switch (m_parser->lap.m_lapData[i].m_pitStatus)
            {
            case 1: car->Status = DriverStatus::Pitlane; break;
            case 2: car->Status = DriverStatus::Pitting; break;

            default:
               switch (m_parser->lap.m_lapData[i].m_driverStatus)
               {
                  // Status of driver - 0 = in garage, 1 = flying lap
                  // 2 = in lap, 3 = out lap, 4 = on track
                  //case 0: car->Status = DriverStatus::Garage; break;
               case 0:
                  car->Status = DriverStatus::Garage;
                  break;

               case 3:
                  car->Status = DriverStatus::OutLap;
                  break;

                  // flying lap, in lap, on track are not tracked by the game correctly except for ai, so don´t take inlap too serious               
               case 2:
                  car->Status = DriverStatus::Inlap;
                  break;

               case 1:
               case 4:
                  car->Status = DriverStatus::OnTrack;
                  break;
               default:
                  car->Status = DriverStatus::Garage; // just assume....
                  break;
               }
               break;
            }
            break;
         }

         if (m_IsQualifyingOrPractice())
         {
            // for quali, capture the next tyre, when outlap is started
            // (state should never change from outlap -> something else -> outlap during a run)
            if (oldDriverStatus != DriverStatus::OutLap && (car->Status == DriverStatus::OutLap))
            {
               if ((m_parser->lastHeader.m_sessionTime - car->LastTyreUpdateByHistory) > 3.f)
               {
                  car->VisualTyres->Add(car->VisualTyre);
                  car->VisualTyres = car->VisualTyres; // trigger NotifyPorpertyChanged
               }
            }
         }
         else if (oldDriverStatus == DriverStatus::Pitting && car->Status != oldDriverStatus)
         {
            // deduce the tyres were probably be changed (we don´t get specific notification about that)
            if ((m_parser->lastHeader.m_sessionTime - car->LastTyreUpdateByHistory) > 3.f)
            {
               car->VisualTyres->Add(car->VisualTyre);
               car->VisualTyres = car->VisualTyres; // trigger NotifyPorpertyChanged
            }
         }
      }
   }

   void F1UdpClrMapper::m_UpdateTimeDeltaRace(DriverData^ reference, int i)
   {
      DriverData^ opponent = Drivers[i];
      if (!opponent->Present)
         return;

      // player passed Sector 2:
      bool found = false;
      int lapIdx = reference->LapNr - 1;
      unsigned lapSector = 2;

      if (lapIdx >= reference->Laps->Length)
         lapIdx = reference->Laps->Length - 1; // just prevent crashing by out of bounds access

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
            if ((reference->Laps[lapIdx]->Sector1) && opponent->Laps[lapIdx]->Sector1)
            {
               found = true;
            }
            break;

         case 1:
            if ((reference->Laps[lapIdx]->Sector2) && opponent->Laps[lapIdx]->Sector2)
            {
               found = true;
            }
            break;

         case 2:
            if ((reference->Laps[lapIdx]->Lap) && opponent->Laps[lapIdx]->Lap)
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

         double timePlayer = 0;
         double timeOpponent = 0;

         if (found)
         {
            if (lapIdx > 0)
            {
               timePlayer = reference->Laps[lapIdx - 1]->LapsAccumulated;
               timeOpponent = opponent->Laps[lapIdx - 1]->LapsAccumulated;
            }

            switch (lapSector)
            {
            case 0:
               timePlayer += reference->Laps[lapIdx]->Sector1;
               timeOpponent += opponent->Laps[lapIdx]->Sector1;
               break;

            case 1:
               timePlayer += reference->Laps[lapIdx]->Sector1 + reference->Laps[lapIdx]->Sector2;
               timeOpponent += opponent->Laps[lapIdx]->Sector1 + opponent->Laps[lapIdx]->Sector2;
               break;

            case 2:
               timePlayer += reference->Laps[lapIdx]->Lap;
               timeOpponent += opponent->Laps[lapIdx]->Lap;
               break;
            }
         }

         int lappedCount = reference->LapNr - opponent->LapNr;
         if (opponent->LocationOnTrack > reference->LocationOnTrack)
            --lappedCount;


         if (lappedCount > 0)
         {
            opponent->TimedeltaToLeader = static_cast<float>(-lappedCount); // special meaning for negative numbers: lapped count
         }
         else
         {
            System::UInt32 telemetryDelta = m_parser->lap.m_lapData[i].m_deltaToRaceLeaderMSPart;
            telemetryDelta += 60000 * m_parser->lap.m_lapData[i].m_deltaToRaceLeaderMinutesPart;

            opponent->TimedeltaToLeader = telemetryDelta / 1000.0f;
         }

         System::UInt32 telemetryDelta = m_parser->lap.m_lapData[i].m_deltaToCarInFrontMSPart;
         telemetryDelta += 60000 * m_parser->lap.m_lapData[i].m_deltaToCarInFrontMinutesPart;

         // it is not undocumented, but apparently there can be values > 60000 in the m_deltaToCarInFrontMSPart.
         // this happens when cars are close to each other. Most likely the game wants to communicate to us, that positions
         // changed and there is no new delta yet -> thus if m_deltaToCarInFrontMSPart >= 1minute show as 0:
         opponent->TimedeltaToNext = 
            m_parser->lap.m_lapData[i].m_deltaToCarInFrontMSPart > 59999 ? 
            0 :                        // delta to next car not available
            telemetryDelta / 1000.0f;   // actual delta available
      }
   }

   void F1UdpClrMapper::m_UpdateTimeDeltaQualy(DriverData^ reference, int i)
   {
      DriverData^ opponent = Drivers[i];
      if (!opponent->Present)
         return;

      float newDelta = static_cast<float>(Drivers[i]->FastestLap->Lap - reference->FastestLap->Lap);
      if ((newDelta != opponent->TimedeltaToLeader) && (reference->FastestLap->Lap != 0))
      {
         if (newDelta > 0)
            opponent->TimedeltaToLeader = newDelta;
      }
   }

   void F1UdpClrMapper::m_UpdateSession()
   {
      // Update Session
      SessionInfo->EventTrack =
         (m_parser->session.m_trackId < static_cast<int8_t>(Track::numEntries)) && (m_parser->session.m_trackId >= 0) ?
         Track(m_parser->session.m_trackId) :
         Track::Unknown;


      SessionInfo->Session = SessionType(m_parser->session.m_sessionType);
      SessionInfo->RemainingTime = m_parser->session.m_sessionTimeLeft;
      SessionInfo->TotalLaps = m_parser->session.m_totalLaps;
      SessionInfo->TrackLength = static_cast<float>(m_parser->session.m_trackLength);
      if (SessionInfo->TrackLength)
      {
         SessionInfo->Sector2Start = m_parser->session.m_sector2LapDistanceStart / SessionInfo->TrackLength;
         SessionInfo->Sector3Start = m_parser->session.m_sector3LapDistanceStart / SessionInfo->TrackLength;
      }
      else
      {
         SessionInfo->Sector2Start = 0.3f;
         SessionInfo->Sector3Start = 0.6f;
      }
   }

   void F1UdpClrMapper::m_UpdateLapRace()
   {
      bool isNewFastestLap = false;

      // Lapdata
      for (int i = 0; i < Drivers->Length; ++i)
      {
         auto& lapNative = m_parser->lap.m_lapData[i];
         auto lapsClr = Drivers[i]->Laps;
         DriverData^ driver = Drivers[i];

         driver->Pos = lapNative.m_carPosition;
         unsigned lapNumCurrent = lapNative.m_currentLapNum;

         if (driver->LapNr != lapNumCurrent) // Update last laptime
         {
            // setup new lap, and capture lap time from now finished lap
            driver->LapNr = lapNumCurrent;

            if (driver->LapNr > 0) // should always be true
            {
               if (lapNative.m_lastLapTimeInMS)
               {
                  driver->CurrentLap->Lap = lapNative.m_lastLapTimeInMS / 1000.0;
                  driver->NPC("CurrentLap"); // trigger the final Laptime
               }
               driver->CurrentLap = lapsClr[driver->LapNr - 1];
               driver->CurrentLap->Sector1 = 0;
               driver->CurrentLap->Sector2 = 0;
               driver->CurrentLap->Lap = 0;
               driver->CurrentLap->Invalid = false;
            }

            if (driver->LapNr > 1)
            {
               auto newLapClr = lapsClr[driver->LapNr - 2];
               newLapClr->Lap = lapNative.m_lastLapTimeInMS / 1000.0;

               if (driver->LapNr == 2)
                  lapsClr[0]->LapsAccumulated = lapsClr[0]->Lap;
               else
               {
                  newLapClr->LapsAccumulated = newLapClr->Lap + lapsClr[driver->LapNr - 3]->LapsAccumulated;
               }

               isNewFastestLap = newLapClr->Lap < driver->FastestLap->Lap;
               isNewFastestLap |= (driver->FastestLap->Lap == 0);
               isNewFastestLap &= (!newLapClr->Invalid);
               if (isNewFastestLap)
               {
                  driver->FastestLap->CopyFrom(newLapClr);
                  driver->NPC("FastestLap");
               }
            }
         }

         else if (driver->LapNr > 0) // Update Sector1+2 if available
         {
            auto currentLap = Drivers[i]->CurrentLap;

            bool change = false;
            System::UInt32 s1 = lapNative.m_sector1TimeMSPart + lapNative.m_sector1TimeMinutesPart * 60000;
            System::UInt32 s2 = lapNative.m_sector2TimeMSPart + lapNative.m_sector2TimeMinutesPart * 60000;

            // special rule for last lap!
            // lapNr will not increase post max race laps, thus we need to capture the final lap, by checking if sector times are availabe but became 0 in telemetry:
            if (
               (SessionInfo->TotalLaps == driver->LapNr) &&
               ((driver->CurrentLap->Sector1Ms > 0) && (!s1))
               )
            {
               // TODO: It will probably miss the lapped cars last lap, since they will probably also not increase after chequered flag...
               if (driver->LapNr < (driver->Laps->Length - 1))
               {
                  if (driver->CurrentLap == driver->Laps[driver->LapNr - 1])
                  {
                     driver->CurrentLap->Lap = lapNative.m_lastLapTimeInMS / 1000.0;
                     driver->NPC("CurrentLap");
                     driver->CurrentLap = driver->Laps[driver->LapNr]; // points to the lap behind last lap, and game will not foll sectors
                     driver->NPC("CurrentLap");
                  }
               }
            }


            if (currentLap->Sector1Ms != s1)
            {
               currentLap->Sector1 = s1 / 1000.0;
               change = true;
            }

            if (currentLap->Sector2Ms != s2)
            {
               currentLap->Sector2 = s2 / 1000.0;
               change = true;
            }

            if (change)
            {
               currentLap->Lap = 0;
            }

            if ((lapNative.m_currentLapInvalid > 0) != currentLap->Invalid)
            {
               currentLap->Invalid = lapNative.m_currentLapInvalid;
               change = true;
            }

            if (change)
            {
               driver->NPC("CurrentLap");
            }
         }

         if (lapNumCurrent > static_cast<unsigned>(SessionInfo->CurrentLap))
         {
            SessionInfo->CurrentLap = std::min<int>(lapNumCurrent, SessionInfo->TotalLaps); // clamp to TotalLaps to prevent the post race lap to count behind maximum
         }
      }
   }

   void F1UdpClrMapper::m_UpdateLapQuali()
   {
      // I could not figure out the meaning of "m_currentLapNum" during quali, it seems to stuck to "1" under several conditions
      // -> even consecutive hotlaps will sometimes lead to a constant value of lapNum = "1". Therefore ignore it entrirely. 

      bool isNewFastestLap = false;

      // Lapdata
      for (int i = 0; i < Drivers->Length; ++i)
      {
         auto& lapNative = m_parser->lap.m_lapData[i];
         auto lapsClr = Drivers[i]->Laps;
         DriverData^ driver = Drivers[i];
         driver->Pos = lapNative.m_carPosition;
         driver->LapNr = lapNative.m_currentLapNum; // assign it, but do not try to make any sense of it!


         switch (driver->Status)
         {
         case DriverStatus::Garage:
         case DriverStatus::Inlap:
         case DriverStatus::Pitlane:
         case DriverStatus::Pitting:
         case DriverStatus::Retired:
         case DriverStatus::DNF:
         case DriverStatus::DSQ:
            driver->AllowLapHistoryQuali = true;
            break;
         default:
            driver->AllowLapHistoryQuali = false;
            break;
         }

         if (driver->LocationOnTrack < 0)
         {
            // outlap, find the next "free" lap
            if (driver->CurrentLap->Sector1Ms || driver->CurrentLap->Sector2Ms || driver->CurrentLap->Lap)
            {
               bool found = false;
               for (int i = 0; i < driver->Laps->Length; ++i)
               {
                  LapData^ lap = driver->Laps[i];
                  if (driver->CurrentLap->Sector1Ms || driver->CurrentLap->Sector2Ms || driver->CurrentLap->Lap)
                     continue;

                  driver->CurrentLap = lap;
                  driver->NPC("CurrentLap");
               }
            }
            continue;
         }

         if (driver->Status == DriverStatus::Pitlane || driver->Status == DriverStatus::Pitting || driver->Status == DriverStatus::Inlap)
         {
            // driver enters the pit, it means the current lap was not a finished lap, therefore delete it
            if (driver->CurrentLap->Sector1Ms || driver->CurrentLap->Sector2Ms || driver->CurrentLap->Lap)
            {
               driver->CurrentLap->Sector1 = 0;
               driver->CurrentLap->Sector2 = 0;
               driver->CurrentLap->Lap = 0;
               driver->CurrentLap->Invalid = false;
               driver->NPC("CurrentLap");
            }
         }

         if (driver->CurrentLap->Sector1Ms && driver->CurrentLap->Sector2Ms && (lapNative.m_sector1TimeMSPart == 0))
         {
            // a lap has been finished
            driver->CurrentLap->Lap = lapNative.m_lastLapTimeInMS / 1000.0;
            driver->NPC("CurrentLap"); // trigger the final Laptime
            if (
               (!driver->CurrentLap->Invalid)
               &&
               (!driver->FastestLap->Lap || (driver->CurrentLap->Lap < driver->FastestLap->Lap))
               )
               driver->FastestLap->CopyFrom(driver->CurrentLap);

            // setup the next lap
            for (int i = 0; i < driver->Laps->Length; ++i)
            {
               LapData^ lap = driver->Laps[i];
               if (lap->Sector1Ms || lap->Sector2Ms || lap->Lap)
                  continue;

               driver->CurrentLap = lap;
               if ((!driver->CurrentLap->Invalid) && (driver->CurrentLap->Sector3 < SessionInfo->FastestSector3))
                  SessionInfo->FastestSector3 = driver->CurrentLap->Sector3;

               driver->NPC("CurrentLap");
            }
         }

         else // Update Sector1+2 if available
         {
            auto currentLap = Drivers[i]->CurrentLap;

            bool change = false;
            System::UInt32 s1 = lapNative.m_sector1TimeMSPart + lapNative.m_sector1TimeMinutesPart * 60000;
            System::UInt32 s2 = lapNative.m_sector2TimeMSPart + lapNative.m_sector2TimeMinutesPart * 60000;
            if (currentLap->Sector1Ms != s1)
            {
               currentLap->Sector1 = s1 / 1000.0;
               if ((!currentLap->Invalid) && (currentLap->Sector1 < SessionInfo->FastestSector1))
                  SessionInfo->FastestSector1 = currentLap->Sector1;

               change = true;
            }

            if (currentLap->Sector2Ms != s2)
            {
               currentLap->Sector2 = s2 / 1000.0;
               if ((!currentLap->Invalid) && (currentLap->Sector2 < SessionInfo->FastestSector2))
                  SessionInfo->FastestSector2 = currentLap->Sector2;
               change = true;
            }

            if (change)
            {
               currentLap->Lap = 0;
            }

            if ((lapNative.m_currentLapInvalid > 0) != currentLap->Invalid)
            {
               currentLap->Invalid = lapNative.m_currentLapInvalid;
               change = true;
            }

            if (change)
            {
               driver->NPC("CurrentLap");
            }
         }
      }
   }


   void F1UdpClrMapper::m_UpdateEventData()
   {
      if (m_parser->event.m_eventStringCode[0] != 0)
      {
         //new event
         if (!strncmp((const char*)m_parser->event.m_eventStringCode, "SSTA", 4))
         {
            m_Clear();
            SessionEvent^ e = gcnew SessionEvent();
            e->TimeCode = m_parser->sessionTime;
            e->Type = EventType::SessionStarted;
            e->CarIndex = 0; // N/A
            EventList->Events->Add(e);
         }
         else if (!strncmp((const char*)m_parser->event.m_eventStringCode, "SEND", 4))
         {
            SessionEvent^ e = gcnew SessionEvent();
            e->TimeCode = m_parser->sessionTime;
            e->Type = EventType::SessionEnded;
            e->CarIndex = 0; // N/A
            EventList->Events->Add(e);
            SessionInfo->SessionFinshed = true;
         }
         else if (!strncmp((const char*)m_parser->event.m_eventStringCode, "FTLP", 4))
         {
            SessionEvent^ e = gcnew SessionEvent();
            e->TimeCode = m_parser->sessionTime;
            e->Type = EventType::FastestLap;
            e->CarIndex = m_parser->event.m_eventDetails.FastestLap.vehicleIdx;
            // TODO add parameters
            EventList->Events->Add(e);
         }
         else if (!strncmp((const char*)m_parser->event.m_eventStringCode, "RTMT", 4))
         {
            SessionEvent^ e = gcnew SessionEvent();
            e->TimeCode = m_parser->sessionTime;
            e->Type = EventType::Retirement;
            e->CarIndex = m_parser->event.m_eventDetails.Retirement.vehicleIdx;
            EventList->Events->Add(e);
         }
         else if (!strncmp((const char*)m_parser->event.m_eventStringCode, "DRSE", 4))
         {
            SessionEvent^ e = gcnew SessionEvent();
            e->TimeCode = m_parser->sessionTime;
            e->Type = EventType::DRSenabled;
            e->CarIndex = 0; // N/A
            EventList->Events->Add(e);
         }
         else if (!strncmp((const char*)m_parser->event.m_eventStringCode, "DRSD", 4))
         {
            SessionEvent^ e = gcnew SessionEvent();
            e->TimeCode = m_parser->sessionTime;
            e->Type = EventType::DRSdisabled;
            e->CarIndex = 0; // N/A
            EventList->Events->Add(e);
         }

         else if (!strncmp((const char*)m_parser->event.m_eventStringCode, "TMPT", 4))
         {
            SessionEvent^ e = gcnew SessionEvent();
            e->TimeCode = m_parser->sessionTime;
            e->Type = EventType::TeamMateInPits;
            e->CarIndex = m_parser->event.m_eventDetails.TeamMateInPits.vehicleIdx;
            EventList->Events->Add(e);
         }

         else if (!strncmp((const char*)m_parser->event.m_eventStringCode, "CHQF", 4))
         {
            SessionEvent^ e = gcnew SessionEvent();
            e->TimeCode = m_parser->sessionTime;
            e->Type = EventType::ChequeredFlag;
            e->CarIndex = 0; // N/A
            EventList->Events->Add(e);
         }
         else if (!strncmp((const char*)m_parser->event.m_eventStringCode, "RCWN", 4))
         {
            SessionEvent^ e = gcnew SessionEvent();
            e->TimeCode = m_parser->sessionTime;
            e->Type = EventType::RaceWinner;
            e->CarIndex = m_parser->event.m_eventDetails.RaceWinner.vehicleIdx;
            EventList->Events->Add(e);
         }
         else if (!strncmp((const char*)m_parser->event.m_eventStringCode, "PENA", 4))
         {
            SessionEvent^ e = gcnew SessionEvent();
            e->TimeCode = m_parser->sessionTime;
            e->Type = EventType::PenaltyIssued;
            e->PenaltyType = PenaltyTypes(m_parser->event.m_eventDetails.Penalty.penaltyType);
            e->LapNum = m_parser->event.m_eventDetails.Penalty.lapNum;
            e->CarIndex = m_parser->event.m_eventDetails.Penalty.vehicleIdx;
            e->OtherVehicleIdx = m_parser->event.m_eventDetails.Penalty.otherVehicleIdx;
            e->InfringementType = InfringementTypes(m_parser->event.m_eventDetails.Penalty.infringementType);
            e->TimeGained = m_parser->event.m_eventDetails.Penalty.time;
            e->PlacesGained = m_parser->event.m_eventDetails.Penalty.placesGained;
            e->PenaltyServed = false;
            EventList->Events->Add(e);

            if (e->LapNum < Drivers[e->CarIndex]->Laps->Length)
            {
               int lapIdx = e->LapNum - 1;
               if (lapIdx < 0)
                  lapIdx = 0;

               Drivers[e->CarIndex]->Laps[lapIdx]->Incidents->Add(e);
            }

            switch (e->PenaltyType)
            {
            case PenaltyTypes::DriveThrough:
            case PenaltyTypes::StopGo:
            case PenaltyTypes::Disqualified:
            case PenaltyTypes::Retired:
               Drivers[e->CarIndex]->PitPenalties->Add(e);
               Drivers[e->CarIndex]->NPC("PitPenalties");

               // -> does not work, because driverstatus = retired is reported with delay in lapdata packet:
               //                   if (e->PenaltyType == PenaltyTypes::Retired)
               //                      Drivers[e->CarIndex]->Status = DriverStatus::DNF;
               break;
            }
         }
         else if (!strncmp((const char*)m_parser->event.m_eventStringCode, "DTSV", 4))
         {
            if (m_parser->event.m_eventDetails.DriveThroughPenaltyServed.vehicleIdx < Drivers->Length)
            {
               DriverData^ car = Drivers[m_parser->event.m_eventDetails.DriveThroughPenaltyServed.vehicleIdx];
               for each (SessionEvent ^ penalty in car->PitPenalties)
               {
                  if ((penalty->PenaltyType == PenaltyTypes::DriveThrough) && (!penalty->PenaltyServed))
                  {
                     penalty->PenaltyServed = true;
                     car->NPC("PitPenalties");
                     break;
                  }
               }
            }
         }
         else if (!strncmp((const char*)m_parser->event.m_eventStringCode, "SGSV", 4))
         {
            if (m_parser->event.m_eventDetails.DriveThroughPenaltyServed.vehicleIdx < Drivers->Length)
            {
               DriverData^ car = Drivers[m_parser->event.m_eventDetails.DriveThroughPenaltyServed.vehicleIdx];
               for each (SessionEvent ^ penalty in car->PitPenalties)
               {
                  if ((penalty->PenaltyType != PenaltyTypes::DriveThrough) && (!penalty->PenaltyServed))
                  {
                     if (!penalty->PenaltyServed)
                     {
                        penalty->PenaltyServed = true;
                        car->NPC("PitPenalties");
                        break;
                     }
                  }
               }
            }
         }
         else if (!strncmp((const char*)m_parser->event.m_eventStringCode, "SPTP", 4))
         {
            SessionEvent^ e = gcnew SessionEvent();
            e->TimeCode = m_parser->sessionTime;
            e->Type = EventType::SpeedTrapTriggered;
            e->CarIndex = m_parser->event.m_eventDetails.SpeedTrap.vehicleIdx;
            // TODO add parameters
            EventList->Events->Add(e);
         }
         else if (!strncmp(reinterpret_cast<const char*>(&m_parser->event.m_eventStringCode[0]), "BUTN", 4))
         {
            uint32_t udpButtonMask = (m_parser->event.m_eventDetails.Buttons.buttonStatus >> 20);
            uint32_t udpButtonPressedNew = udpButtonMask & (udpButtonMask ^ m_udpButtonPreviousMask); // set all buttons that transitioned 0 -> 1
            m_udpButtonPreviousMask = udpButtonMask;

            for (unsigned i = 0; i < 12; ++i)
            {
               UdpAction[i] |= (udpButtonPressedNew & 1);
               udpButtonPressedNew >>= 1;
            }
         }
      }
   }

   void F1UdpClrMapper::m_UpdateParticipants()
   {
      // prevent left players to disappear in list
      // which means during a session, the maximum number of players/ai ever present are shown.
      if (m_parser->participants.m_numActiveCars > CountDrivers)
         CountDrivers = m_parser->participants.m_numActiveCars;

      // Lapdata + Name + Team
      for (int i = 0; i < Drivers->Length; ++i)
      {
         uint16_t teamId = s_TELEMETRY_TEAM_ID_REMAP(m_parser->participants.m_participants[i].m_teamId);
         if (teamId < 12)
            Drivers[i]->Team = F1Team(teamId);

         else
            Drivers[i]->Team = F1Team::AnyOther;

         Drivers[i]->DriverNr = m_parser->participants.m_participants[i].m_raceNumber;

         if (String::IsNullOrEmpty(Drivers[i]->TelemetryName) && m_parser->participants.m_participants[i].m_raceNumber)
         {
            m_UpdateDriverName(i);
         }
      }
   }

   void F1UdpClrMapper::m_UpdateDamage(int i)
   {
      DriverData^ driver = Drivers[i];
      if (!driver->Present)
         return;

      if (driver->IsSecondaryDriver)
         return; // data comes from second telemetry channel

      driver->WearDetail->DamageFrontLeft = m_parser->cardamage.m_carDamageData[i].m_frontLeftWingDamage;
      driver->WearDetail->DamageFrontRight = m_parser->cardamage.m_carDamageData[i].m_frontRightWingDamage;

      auto tyres = m_parser->cardamage.m_carDamageData[i].m_tyresWear;
      driver->WearDetail->WearFrontLeft = static_cast<int>(m_parser->cardamage.m_carDamageData[i].m_tyresWear[2]);
      driver->WearDetail->WearFrontRight = static_cast<int>(m_parser->cardamage.m_carDamageData[i].m_tyresWear[3]);
      driver->WearDetail->WearRearLeft = static_cast<int>(m_parser->cardamage.m_carDamageData[i].m_tyresWear[0]);
      driver->WearDetail->WearRearRight = static_cast<int>(m_parser->cardamage.m_carDamageData[i].m_tyresWear[1]);
   }

   void F1UdpClrMapper::m_UpdateTelemetry(int i)
   {
      DriverData^ driver = Drivers[i];
      if (!driver->Present)
         return;

      if (driver->IsSecondaryDriver)
         return; // data comes from second telemetry channel

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

      driver->WearDetail->ErsMode = CarDetailErsMode(m_parser->status.m_carStatusData[i].m_ersDeployMode);
      driver->WearDetail->ErsAvailPercent = m_parser->status.m_carStatusData[i].m_ersStoreEnergy / 4000000.f; // 4 MJ max store = 100%

      driver->WearDetail->TempEngine = m_parser->telemetry.m_carTelemetryData[i].m_engineTemperature;
   }

   void F1UdpClrMapper::m_UpdateDriverName(int i)
   {
      if (0 == m_parser->participants.m_participants[i].m_raceNumber)
      {
         // no valid data from telemetry present, skip
         return;
      }

      Drivers[i]->SetNameFromTelemetry(m_parser->participants.m_participants[i].m_name);

      // For online-only players with no useful telemetry name, generate one from team + number
      // and push it into TelemetryName so Name picks it up automatically.
      if (m_parser->participants.m_participants[i].m_driverId == 255 &&
          !m_parser->participants.m_participants[i].m_showOnlineNames)
      {
         String^ pName = "Car";
         switch (s_TELEMETRY_TEAM_ID_REMAP(m_parser->participants.m_participants[i].m_teamId))
         {
         case 0: pName = "Mercedes"; break;
         case 1: pName = "Ferrari"; break;
         case 2: pName = "Red Bull"; break;
         case 3: pName = "Williams"; break;
         case 4: pName = "Aston Martin"; break;
         case 5: pName = "Alpine"; break;
         case 6: pName = "Racing Bulls"; break;
         case 7: pName = "Haas"; break;
         case 8: pName = "McLaren"; break;
         case 9: pName = "Sauber"; break;
         case 10: pName = "Audio"; break;
         case 11: pName = "Cadillac"; break;
         }
         Drivers[i]->TelemetryName = pName + " (" + Drivers[i]->DriverNr + ")";
      }
   }

   void F1UdpClrMapper::m_UpdateClassification()
   {
      if (Classification != nullptr)
         return;

      if (!m_parser->classification.m_numCars)
         return;

      // classification available, apply:
      Classification = gcnew array<ClassificationData^>(m_parser->classification.m_numCars);

      for (int i = 0; i < Classification->Length; ++i)
      {
         ClassificationData^ pClr = gcnew ClassificationData();
         Classification[i] = pClr;
         FinalClassificationData* pNative = &m_parser->classification.m_classificationData[i];

         pClr->Driver = Drivers[i];
         pClr->BestLapTime = static_cast<double>(pNative->m_bestLapTimeInMS) / 1000.0;
         pClr->TotalRaceTime = pNative->m_totalRaceTime;
         pClr->GridPosition = pNative->m_gridPosition;
         pClr->NumLaps = pNative->m_numLaps;
         pClr->NumPenalties = pNative->m_numPenalties;
         pClr->PenaltiesTime = pNative->m_penaltiesTime;
         pClr->Points = pNative->m_points;
         pClr->Position = pNative->m_position;
      }

      m_parser->classification.m_numCars = 0; // set a marker that classifcation results were captured.
   }

   void F1UdpClrMapper::m_UpdateHistoryDataRace()
   {
      if (m_parser->history.m_carIdx > Drivers->Length)
         return;

      DriverData^ driver = Drivers[m_parser->history.m_carIdx];
      PacketSessionHistoryData& history = m_parser->history;

      // update old laps
      bool wasUpdate = false;
      if (driver->LapNr > 0)
      {
         for (int i = 0; i < driver->LapNr; ++i)
         {
            LapData^ lap = driver->Laps[i];
            if ((lap->Lap < 1.0) || (lap->Sector1 < 1.0) || (lap->Sector2 < 1.0))
            {
               // lap data was not available! So insert:
               wasUpdate = true;

               lap->Sector1 = history.m_lapHistoryData[i].m_sector1TimeMSPart / 1000.0 + history.m_lapHistoryData[i].m_sector1TimeMinutesPart * 60.0;
               lap->Sector2 = history.m_lapHistoryData[i].m_sector2TimeMSPart / 1000.0 + history.m_lapHistoryData[i].m_sector2TimeMinutesPart * 60.0;
               lap->Lap = history.m_lapHistoryData[i].m_lapTimeInMS / 1000.0;
            }
         }
      }

      if (wasUpdate)
      {
         for (int i = 0; i < driver->LapNr; ++i)
         {
            LapData^ lap = driver->Laps[i];
            double accumulated = 0.0;
            if (lap->Lap < 1.0)
            {
               if (lap->Lap > 1.0)
               {
                  accumulated += lap->Lap;
                  lap->LapsAccumulated = accumulated;
               }
            }
         }
      }

      // capture fastest Lap
      if (m_parser->history.m_bestLapTimeLapNum &&
         (m_parser->history.m_bestLapTimeLapNum < sizeof(m_parser->history.m_lapHistoryData) / sizeof(m_parser->history.m_lapHistoryData[0])))
      {
         const auto& lapNative = m_parser->history.m_lapHistoryData[m_parser->history.m_bestLapTimeLapNum - 1];
         DriverData^ driver = Drivers[m_parser->history.m_carIdx];
         uint32_t lapTime = lapNative.m_lapTimeInMS;

         if (!lapTime)
            return;

         driver->FastestLap = driver->Laps[m_parser->history.m_bestLapTimeLapNum - 1];
         driver->NPC("FastestLap");
      }

      // update tyre history
      if (m_parser->history.m_tyreStintsHistoryData[0].m_tyreVisualCompound != 0)
      {
         bool changed = false;
         List<F1VisualTyre>^ newTyreSets = gcnew List<F1VisualTyre>();
         // we have valid data in the history, check if it is in line with our clr data:
         uint8_t cntTyresInPacket = 0;
         for (int i = 0; i < cs_maxTyreStints; ++i)
         {
            uint8 c = m_parser->history.m_tyreStintsHistoryData[i].m_tyreVisualCompound;
            if (c)
            {
               F1VisualTyre t = F1VisualTyre(c);
               newTyreSets->Add(t);
               ++cntTyresInPacket;
               if (driver->VisualTyres->Count < (i + 1))
               {
                  changed = true;
               }
               else if (driver->VisualTyres[i] != t)
               {
                  changed = true;
               }
            }
         }

         if (cntTyresInPacket && (driver->VisualTyres->Count != cntTyresInPacket))
            changed = true;

         if (changed)
         {
            driver->VisualTyres = newTyreSets;
            driver->VisualTyre = driver->VisualTyres[driver->VisualTyres->Count - 1];
            driver->NPC("VisualTyres");
            driver->LastTyreUpdateByHistory = m_parser->history.m_header.m_sessionTime;
            DriverHistoryDirty[m_parser->history.m_carIdx] = true;
         }
      }
   }


   void F1UdpClrMapper::m_UpdateHistoryDataQuali()
   {
      if (m_parser->history.m_carIdx > Drivers->Length)
         return;

      DriverData^ driver = Drivers[m_parser->history.m_carIdx];
      PacketSessionHistoryData& history = m_parser->history;

      if (!driver->AllowLapHistoryQuali)
         return;

      // only care about the fastest lap!       
      // m_numLaps is not always correct, so read all laps, with reasonable data, i.e. all sectors present, and flags set properly
      for (auto& lap : m_parser->history.m_lapHistoryData)
      {
         if ((lap.m_lapValidBitFlags == 0xf) && (lap.m_sector1TimeMSPart && lap.m_sector2TimeMSPart && lap.m_sector3TimeMSPart && lap.m_lapTimeInMS))
         {
            if (!driver->FastestLap->LapMs || (driver->FastestLap->LapMs > lap.m_lapTimeInMS))
            {

               driver->FastestLap->Sector1 = lap.m_sector1TimeMSPart / 1000.0 + lap.m_sector1TimeMinutesPart * 60.0;
               driver->FastestLap->Sector2 = lap.m_sector2TimeMSPart / 1000.0 + lap.m_sector2TimeMinutesPart * 60.0;
               driver->FastestLap->Lap = lap.m_lapTimeInMS / 1000.0;
               driver->NPC("FastestLap");
            }
         }
      }
   }

   bool F1UdpClrMapper::m_IsQualifyingOrPractice()
   {
      switch (SessionInfo->Session)
      {
      case SessionType::P1:
      case SessionType::P2:
      case SessionType::P3:
      case SessionType::ShortPractice:
      case SessionType::Q1:
      case SessionType::Q2:
      case SessionType::Q3:
      case SessionType::ShortQ:
         return true;
         break;
      }
      return false;
   }

   void F1UdpClrMapper::m_UpdateTyreSetsData()
   {
      PacketTyreSetsData& sets = m_parser->tyreSets;

      if (sets.m_carIdx >= Drivers->Length)
         return;

      if (m_parser->participants.m_participants[sets.m_carIdx].m_yourTelemetry == 0)
      {
         // restricted telemetry -> cannot extract tyre history!
         return;
      }
   }

   void F1UdpClrMapper::m_UpdateCarPositions()
   {
      PacketLapPositionsData& pos = m_parser->lapPositions;

   }

   void F1UdpClrMapper::m_UpdateCarPositions3d()
   {
      for (int i = 0; i < cs_maxNumCarsInUDPData; ++i)
      {
         const CarMotionData& dat = m_parser->motion.m_carMotionData[i];
         if (i > Drivers->Length)
            break;

         DriverData^ driver = Drivers[i];
         driver->TrackPosition3d->x = dat.m_worldPositionX;
         driver->TrackPosition3d->y = dat.m_worldPositionY;
         driver->TrackPosition3d->z = dat.m_worldPositionZ;
      }
   }

   void F1UdpClrMapper::m_UpdateTelemetry2(int i)
   {
      DriverData^ driver = Drivers[i];
      if (!driver->Present)
         return;

      if (driver->IsSecondaryDriver)
         return; // data comes from second telemetry channel

      driver->WearDetail->OvertakeAvailable = m_parser->telemetry2.m_carTelemetry2Data[i].m_overtakeAvailable;
   }

}
