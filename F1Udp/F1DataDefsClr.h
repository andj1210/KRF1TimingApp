// Copyright 2018-2021 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
using namespace System;
using namespace System::Collections::Generic;
#include <string.h>
#include <list>

namespace adjsw::F12026
{
   public enum class F1Team : int
   {
   // ---- Current teams (ids 0-9) -- direct cast from m_teamId ----
   Mercedes,      // 0
   Ferrari,       // 1
   RedBull,       // 2
   Williams,      // 3
   AstonMartin,   // 4
   Alpine,        // 5
   RacingBulls,   // 6  (RB)
   Haas,          // 7
   McLaren,       // 8
   Sauber,        // 9
   Audi,          // 10 - this does not exist in UDP telemetry, we use it as a placeholder, so all teams are mapped to 0...11
   Cadillac,       // 11 - this does not exist in UDP telemetry, we use it as a placeholder, so all teams are mapped to 0...11

   AnyOther = 12,  // - this does not exist in UDP telemetry, synthetic catch-all kept for legacy mapping

   // ---- Appendix teams (2026 Season Pack) ----
   // Appended naive for now -- year '24/'25/'26 variants alias the same real cars.
   F1Generic = 41,
   F1CustomTeam = 104,
   Konnersport = 129,
   APXGP24 = 142,
   APXGP25 = 154,
   Konnersport24 = 155,
   ArtGP24 = 158,
   Campos24 = 159,
   RodinMotorsport24 = 160,
   AIXRacing24 = 161,
   DAMS24 = 162,
   Hitech24 = 163,
   MPMotorsport24 = 164,
   Prema24 = 165,
   Trident24 = 166,
   VanAmersfoortRacing24 = 167,
   Invicta24 = 168,
   Mercedes24 = 185,
   Ferrari24 = 186,
   RedBullRacing24 = 187,
   Williams24 = 188,
   AstonMartin24 = 189,
   Alpine24 = 190,
   RB24 = 191,
   Haas24 = 192,
   McLaren24 = 193,
   Sauber24 = 194,
   ArtGP25 = 465,
   Campos25 = 466,
   RodinMotorsport25 = 467,
   AIXRacing25 = 468,
   DAMS25 = 469,
   Hitech25 = 470,
   MPMotorsport25 = 471,
   Prema25 = 472,
   Trident25 = 473,
   VanAmersfoortRacing25 = 474,
   Invicta25 = 475,
   Mercedes26 = 476,
   Ferrari26 = 477,
   RedBullRacing26 = 478,
   Williams26 = 479,
   AstonMartin26 = 480,
   Alpine26 = 481,
   RB26 = 482,
   Haas26 = 483,
   McLaren26 = 484,
   Audi26 = 485,
   Cadillac26 = 486
   };

   // F1 2020:
   // Modern - 16 = C5, 17 = C4, 18 = C3, 19 = C2, 20 = C1
   // 7 = inter, 8 = wet
   // F1 Classic - 9 = dry, 10 = wet
   // F2 � 11 = super soft, 12 = soft, 13 = medium, 14 = hard
   // 15 = wet

   public enum class F1Tyre : int
   {
      // F1 Modern - 16 = C5, 17 = C4, 18 = C3, 19 = C2, 20 = C1
      // 7 = inter, 8 = wet
      // F1 Classic - 9 = dry, 10 = wet
      // F2 � 11 = super soft, 12 = soft, 13 = medium, 14 = hard
      // 15 = wet

      Intermediate = 7,
      Wet = 8,

      C5 = 16,
      C4 = 17,
      C3 = 18,
      C2 = 19,
      C1 = 20,

      ClassicDry = 9,
      ClassicWet = 10,

      F2SuperSoft = 11,
      F2Soft = 12,
      F2Medium = 13,
      F2Hard = 14,
      F2Wet = 15,

      Other,
   };

   public enum class F1VisualTyre : int
   {
      // F1 visual (can be different from actual compound)
      // 16 = soft, 17 = medium, 18 = hard, 7 = inter, 8 = wet
      // F1 Classic � same as above
      // F2 � same as above

      Intermediate = 7,
      Wet = 8,

      Soft = 16,
      Medium = 17,
      Hard = 18,

      Unknown = 254
   };

   public enum class Track
   {
      Unknown = -1,
      Melbourne = 0,
      PaulRicard,
      Shanghai,
      Sakhir,
      Catalunya,
      Monaco,
      Montreal,
      Silverstone,
      Hockenheim,
      Hungaroring,
      Spa,
      Monza,
      Singapore,
      Suzuka,
      AbuDhabi,
      Texas,
      Brazil,
      Austria,
      Sochi,
      Mexico,
      Baku,
      SakhirShort,
      SilverstoneShort,
      TexasShort,
      SuzukaShort,
      Hanoi,
      Zandvoort,
      Imola,
      Portimao,
      Jeddah,
      Miami,
      LasVegas,
      Losail,             // 32

      // make ids 33...38 valid dummy ids
      Dummy33,
      Dummy34,
      Dummy35,
      Dummy36,
      Dummy37,
      Dummy38,

      // ---- 2026 Season Pack additions ----
      SilverstoneReverse = 39,
      AustriaReverse = 40,
      ZandvoortReverse = 41,
      Madrid = 42,
      numEntries          // 43
   };

   public enum class SessionType
   {
      Unknown = 0,
      P1,
      P2,
      P3,
      ShortPractice,
      Q1,
      Q2,
      Q3,
      ShortQ,
      OSQ,
      SprintShootout1,
      SprintShootout2,
      SprintShootout3,
      ShortSprintShootout,
      OneShotSprintShootout,
      Race,
      Race2,
      Race3,
      TimeTrial
   };

   public enum class DriverStatus
   {
      Garage,
      OutLap,
      OnTrack,
      Inlap,
      Pitlane,
      Pitting,
      Retired,
      DNF,
      DSQ
   };


   public enum class EventType
   {
      SessionStarted,
      SessionEnded,
      FastestLap,
      Retirement,
      DRSenabled,
      DRSdisabled,
      TeamMateInPits,
      ChequeredFlag,
      RaceWinner,
      PenaltyIssued,
      SpeedTrapTriggered
   };

   public enum class PenaltyTypes
   {
      DriveThrough = 0,
      StopGo,
      GridPenalty,
      PenaltyReminder,
      TimePenalty,
      Warning,
      Disqualified,
      RemovedFromFormationLap,
      ParkedTooLongTimer,
      TyreRegulations,
      ThisLapInvalidated,
      ThisAndNextLapInvalidated,
      ThisLapInvalidatedWithoutReason,
      ThisAndNextLapInvalidatedWithoutReason,
      ThisAndPreviousLapInvalidated,
      ThisAndPreviousLapInvalidatedWithoutReason,
      Retired,
      BlackFlagTimer
   };

   public enum class InfringementTypes
   {
      BlockingBySlowDriving = 0,
      BlockinByWrongWayDriving,
      ReversingOffTheStartLine,
      BigCollision,
      SmallCollision,
      CollisionFailedToHandBackPositionSingle,
      CollisionFailedToHandBackPositionMultiple,
      CornerCuttingGainedTime,
      CornerCuttingOvertakeSingle,
      CornerCuttingOvertakeMultiple,
      CrossedPitExitLane,
      IgnoringBlueFlags,
      IgnoringYellowFlags,
      IgnoringDriveThrough,
      TooManyDriveThroughs,
      DriveThroughReminderServeWithinNLaps,
      DriveThroughReminderServeThisLap,
      PitLaneSpeeding,
      ParkedForTooLong,
      IgnoringTyreRegulations,
      TooManyPenalties,
      MultipleWarnings,
      ApproachingDisqualification,
      TyreRegulationsSelectSingle,
      TyreRegulationsSelectMultiple,
      LapInvalidatedCornerCutting,
      LapInvalidatedRunningWide,
      CornerCuttingRanWideGainedTimeMinor,
      CornerCuttingRanWideGainedTimeSignificant,
      CornerCuttingRanWideGainedTimeExtreme,
      LapInvalidatedWallRiding,
      LapInvalidatedFlashbackUsed,
      LapInvalidatedResetToTrack,
      BlockingThePitlane,
      JumpStart,
      SafetyCarToCarCollision,
      SafetyCarIllegalOvertake,
      SafetyCarExceedingAllowedPace,
      VirtualSafetyCarExceedingAllowedPace,
      FormationLapBelowAllowedSpeed,
      FormationLapParking,
      RetiredMechanicalFailure,
      RetiredTerminallyDamaged,
      SafetyCarFallingTooFarBack,
      BlackFlagTimer,
      UnservedStopGoPenalty,
      UnservedDriveThroughPenalty,
      EngineComponentChange,
      GearboxChange,
      ParcFermeChange,
      LeagueGridPenalty,
      RetryPenalty,
      IllegalTimeGain,
      MandatoryPitstop,
      AttributeAssigned // (???)
   };


   public ref class SessionEvent
   {
   public:
      property double TimeCode; // in seconds since session start
      property EventType Type;
      property int CarIndex;

      // penalty info
      property PenaltyTypes PenaltyType;
      property InfringementTypes InfringementType;
      property int OtherVehicleIdx;
      property int TimeGained; // Time gained, or time spent doing action in seconds
      property int LapNum;
      property int PlacesGained;
      property bool PenaltyServed; // not present in actual telemetry, deduced from race telemetry
   };


   public ref class SessionInfo : public System::ComponentModel::INotifyPropertyChanged
   {
   public:      
      property Track EventTrack { Track get() { return m_track; } void set(Track val) { if (val != m_track) { m_track = val; NPC("EventTrack"); } } };

      property SessionType Session { SessionType get() { return m_session; } void set(SessionType val) { if (val != m_session) { m_session = val; NPC("Session"); } } };
      property bool SessionFinshed { bool get() { return m_sessionFinished; } void set(bool val) { if (val != m_sessionFinished) { m_sessionFinished = val; NPC("SessionFinshed"); } } };

      // for training / qualifying
      property int RemainingTime { int get() { return m_remainingSeconds; } void set(int val) { if (val != m_remainingSeconds) { m_remainingSeconds = val; NPC("RemainingTime"); } } };

      // for race
      property int TotalLaps { int get() { return m_totalLaps; } void set(int val) { if (val != m_totalLaps) { m_totalLaps = val; NPC("TotalLaps"); } } };
      property int CurrentLap { int get() { return m_currentLap; } void set(int val) { if (val != m_currentLap) { m_currentLap = val; NPC("CurrentLap"); } } };

      property double FastestSector1 { double get() { return m_fastestSector1; } void set(double val) { if (val != m_fastestSector1) { m_fastestSector1 = val; NPC("FastestSector1"); } } };
      property double FastestSector2 { double get() { return m_fastestSector2; } void set(double val) { if (val != m_fastestSector2) { m_fastestSector2 = val; NPC("FastestSector2"); } } };
      property double FastestSector3 { double get() { return m_fastestSector3; } void set(double val) { if (val != m_fastestSector3) { m_fastestSector3 = val; NPC("FastestSector3"); } } };


      property float TrackLength {float get() { return m_trackLength; } void set(float val) { if (val != m_trackLength) { m_trackLength = val; NPC("TrackLength"); } }}
      property float Sector2Start {float get() { return m_sector2Start; } void set(float val) { if (val != m_sector2Start) { m_sector2Start = val; NPC("Sector2Start"); } }}
      property float Sector3Start {float get() { return m_sector3Start; } void set(float val) { if (val != m_sector3Start) { m_sector3Start = val; NPC("Sector3Start"); } }}

      void NPC(String^ name) { PropertyChanged(this, gcnew System::ComponentModel::PropertyChangedEventArgs(name)); }
      virtual event System::ComponentModel::PropertyChangedEventHandler^ PropertyChanged;

   private:
      Track m_track{ Track::Austria };
      SessionType m_session{ SessionType::P1 };
      bool m_sessionFinished{ false };
      int m_remainingSeconds{ 0 };
      int m_totalLaps{ 2 };
      int m_currentLap{ 1 };
      float m_trackLength{ 3500.f };
      float m_sector2Start{ 0.3f };
      float m_sector3Start{ 0.6f };
      double m_fastestSector1{ 999.0 };
      double m_fastestSector2{ 999.0 };
      double m_fastestSector3{ 999.0 };
   };



   public ref class SessionEventList : public System::ComponentModel::INotifyPropertyChanged
   {
   public:
      SessionEventList()
      {
         m_events = gcnew List<SessionEvent^>();
      }

      property List<SessionEvent^>^ Events {  List<SessionEvent^>^ get() { return m_events; } void set(List<SessionEvent^>^ val) { m_events = val; NPC("Events"); } };

      void NPC(String^ name) { PropertyChanged(this, gcnew System::ComponentModel::PropertyChangedEventArgs(name)); }
      virtual event System::ComponentModel::PropertyChangedEventHandler^ PropertyChanged;

   private:
      List<SessionEvent^>^ m_events;
   };

   public ref class LapData
   {
   public:
      property System::UInt32 Sector1Ms {System::UInt32 get() { return m_DoubleSecToIntMsec(Sector1); } }
      property System::UInt32 Sector2Ms {System::UInt32 get() { return m_DoubleSecToIntMsec(Sector2); } }
      property System::UInt32 Sector3Ms {System::UInt32 get() { return m_DoubleSecToIntMsec(Sector3); } }
      property System::UInt32 LapMs {System::UInt32 get() { return m_DoubleSecToIntMsec(Lap); } }

      property double Sector1;
      property double Sector2;
      property double Sector3 {double get() { return (Lap != 0.0) ? Lap - (Sector1 + Sector2) : 0.0; } };

      property double Lap;
      property double LapsAccumulated;
      property List<SessionEvent^>^ Incidents;

      property bool Invalid;

      void CopyFrom(LapData^ lap)
      {
         Sector1 = lap->Sector1;
         Sector2 = lap->Sector2;
         Lap = lap->Lap;
         LapsAccumulated = lap->LapsAccumulated;
         Invalid = lap->Invalid;
         Incidents = lap->Incidents;
      }

      String^ To_M_SS_MMMM(UInt32 totalMilliSeconds)
      {
         UInt32 mins = totalMilliSeconds / 60000;
         UInt32 seconds = (totalMilliSeconds % 60000) / 1000;
         UInt32 milliSeconds = totalMilliSeconds % 1000;

         return "" + mins + ":" + seconds.ToString("D2") + "." + milliSeconds.ToString("D3");
      }

      String^ To_M_SS_MMMM(double totalSeconds)
      {
         return To_M_SS_MMMM(m_DoubleSecToIntMsec(totalSeconds));
      }

      String^ To_SS_MMMM(UInt32 totalMilliSeconds)
      {
         UInt32 seconds = totalMilliSeconds / 1000;
         UInt32 milliSeconds = totalMilliSeconds % 1000;            

         if (seconds < 10)
            return "" + seconds.ToString("D2") + "." + milliSeconds.ToString("D3");
         else
            return "" + seconds + "." + milliSeconds.ToString("D3");
      }

      String^ To_SS_MMMM(double totalSeconds)
      {
         return To_SS_MMMM(m_DoubleSecToIntMsec(totalSeconds));
      }

   private:
      // get double seconds to int milliseconds with correct rounding
      System::UInt32 m_DoubleSecToIntMsec(double d) { d *= 1000.0; d += 0.5; return (System::UInt32)d; }
   };

   public ref class CarDetail
   {
   public:
      property int DamageFrontLeft {int get() { return m_dmgFrontLeft; } void set(int val) { m_dmgFrontLeft = val; } };
      property int DamageFrontRight {int get() { return m_dmgFrontRight; } void set(int val) { m_dmgFrontRight = val; } };

      property int WearFrontLeft {int get() { return m_wearFrontLeft; } void set(int val) { m_wearFrontLeft = val; } };      
      property int WearFrontRight {int get() { return m_wearFrontRight; } void set(int val) { m_wearFrontRight = val; } };
      property int WearRearLeft {int get() { return m_wearRearLeft; } void set(int val) { m_wearRearLeft = val; } };
      property int WearRearRight {int get() { return m_wearRearRight; } void set(int val) { m_wearRearRight = val; } };

      property int TempFrontLeftInner {int get() { return m_tempFrontLeftInner; } void set(int val) { m_tempFrontLeftInner = val; } };
      property int TempFrontLeftOuter {int get() { return m_tempFrontLeftOuter; } void set(int val) { m_tempFrontLeftOuter = val; } };
      property int TempFrontRightInner {int get() { return m_tempFrontRightInner; } void set(int val) { m_tempFrontRightInner = val; } };
      property int TempFrontRightOuter {int get() { return m_tempFrontRightOuter; } void set(int val) { m_tempFrontRightOuter = val; } };

      property int TempRearLeftInner {int get() { return m_tempRearLeftInner; } void set(int val) { m_tempRearLeftInner = val; } };
      property int TempRearLeftOuter {int get() { return m_tempRearLeftOuter; } void set(int val) { m_tempRearLeftOuter = val; } };
      property int TempRearRightInner {int get() { return m_tempRearRightInner; } void set(int val) { m_tempRearRightInner = val; } };
      property int TempRearRightOuter {int get() { return m_tempRearRightOuter; } void set(int val) { m_tempRearRightOuter = val; } };

      property int TempEngine {int get() { return m_tempEngine; } void set(int val) { m_tempEngine = val; } };

      property int TempBrakeFrontLeft {int get() { return m_tempBrakeFrontLeft; } void set(int val) { m_tempBrakeFrontLeft = val; } };
      property int TempBrakeFrontRight {int get() { return m_tempBrakeFrontRight; } void set(int val) { m_tempBrakeFrontRight = val; } };

      property int TempBrakeRearLeft{int get() { return m_tempBrakeRearLeft; } void set(int val) { m_tempBrakeRearLeft = val; } };
      property int TempBrakeRearRight {int get() { return m_tempBrakeRearRight; } void set(int val) { m_tempBrakeRearRight = val; } };

   private:
      int m_dmgFrontLeft{ 0 };
      int m_dmgFrontRight{ 0 };
      int m_wearFrontLeft{ 0 };
      int m_wearFrontRight{ 0 };
      int m_wearRearLeft{ 0 };
      int m_wearRearRight{ 0 };     

      int m_tempFrontLeftInner{ 0 };
      int m_tempFrontLeftOuter{ 0 };
      int m_tempFrontRightInner{ 0 };
      int m_tempFrontRightOuter{ 0 };

      int m_tempRearLeftInner{ 0 };
      int m_tempRearLeftOuter{ 0 };
      int m_tempRearRightInner{ 0 };
      int m_tempRearRightOuter{ 0 };

      int m_tempEngine{0};
      int m_tempBrakeFrontLeft{ 0 };
      int m_tempBrakeFrontRight{ 0 };
      int m_tempBrakeRearLeft{ 0 };
      int m_tempBrakeRearRight{ 0 };
   };

   public ref class DriverPos3d
   {
   public:
      float x{ 0 };
      float y{ 0 };
      float z{ 0 };
   };

   public ref class DriverData : public System::ComponentModel::INotifyPropertyChanged
   {
   public:
      DriverData(SessionInfo^ inf)
      {
         m_driverNameNative = new char[48];
         Reset();
         m_carDetail = gcnew CarDetail;
         m_sessionInfo = inf;
      }
      ~DriverData() { delete m_driverNameNative; }

      void Reset()
      {
         TelemetryName = "";
         m_driverNameNative[0] = 0;
         Pos = 0;
         LapNr = 1;
         Status = DriverStatus::Garage;
         Laps = gcnew array<LapData^>(100); // 100 Laps ought to be enough for anybody
         for (int i = 0; i < Laps->Length; ++i)
         {
            Laps[i] = gcnew LapData();
            Laps[i]->Incidents = gcnew List<SessionEvent^>();
         }
         FastestLap = gcnew LapData();      
         CurrentLap = Laps[0];
         IsPlayer          = false;
         IsMainDriver      = false;
         IsSecondaryDriver = false;
         Present = false;
         VisualTyres = gcnew List<F1VisualTyre>();
         PitPenalties = gcnew List<SessionEvent^>();
         m_hasPitted = false;

         TimedeltaToLeader = 0;
         TimedeltaToNext = 0;
         Id = 0;
         AllowLapHistoryQuali = true;
         m_trackPos3d = gcnew DriverPos3d();
      }

      void SetNameFromTelemetry(const char(&pName)[32])
      {
         if (strcmp(pName, m_driverNameNative))
         {
            strncpy_s(m_driverNameNative, 32, pName, 32);
            unsigned sz = strlen(m_driverNameNative);
            array<Byte>^ arr = gcnew array<Byte>(sz);
            for (unsigned i = 0; i < sz; ++i)
               arr[i] = m_driverNameNative[i];

            TelemetryName = System::Text::Encoding::UTF8->GetString(arr);
         }
      }

      property int Id;
      property SessionInfo^ Session {SessionInfo^ get() { return m_sessionInfo; }}
      property String^ Name { String^ get() { return String::IsNullOrEmpty(m_nameOverride) ? m_telemetryName : m_nameOverride; } }; // computed: NameOverride if set, else TelemetryName
      property String^ NameOverride {String^ get() { return m_nameOverride; } void set(String^ val) { if (!String::Equals(val, m_nameOverride)) { m_nameOverride = val; NPC("NameOverride"); NPC("Name"); } } };
      property String^ TelemetryName {String^ get() { return m_telemetryName; } void set(String^ val) { if (!String::Equals(val, m_telemetryName)) { m_telemetryName = val; NPC("TelemetryName"); NPC("Name"); } } }; // the name from telemetry
      property bool IsPlayer          {bool get() { return m_isPlayer;          } void set(bool val) { if (val != m_isPlayer)          { m_isPlayer          = val; NPC("IsPlayer");          } } };
      property bool IsMainDriver      {bool get() { return m_isMainDriver;      } void set(bool val) { if (val != m_isMainDriver)      { m_isMainDriver      = val; NPC("IsMainDriver");      } } };
      property bool IsSecondaryDriver {bool get() { return m_isSecondaryDriver; } void set(bool val) { if (val != m_isSecondaryDriver) { m_isSecondaryDriver = val; NPC("IsSecondaryDriver"); } } };
      property bool Present {bool get() { return m_present; } void set(bool val) { if (val != m_present) { m_present = val; NPC("Present"); } } };
      property DriverStatus Status {DriverStatus get() { return m_status; } void set(DriverStatus val) { if (val != m_status) { m_status = val; NPC("Status"); } } };
      property CarDetail^ WearDetail {CarDetail^ get() { return m_carDetail; } void set(CarDetail^ val) { m_carDetail = val; } };
      property F1Team Team {F1Team get() { return m_team; } void set(F1Team val) { if (val != m_team) { m_team = val; NPC("Team"); } } };
      property int DriverNr {int get() { return m_driverNr; } void set(int val) { if (val != m_driverNr) { m_driverNr = val; NPC("DriverNr"); } } };
      property F1Tyre Tyre {F1Tyre get() { return m_tyre; } void set(F1Tyre val) { if (val != m_tyre) { m_tyre = val; NPC("Tyre"); } } };
      property F1VisualTyre VisualTyre {F1VisualTyre get() { return m_visualTyre; } void set(F1VisualTyre val) { if (val != m_visualTyre) { m_visualTyre = val; NPC("VisualTyre"); } } };
      property List<F1VisualTyre>^ VisualTyres {List<F1VisualTyre>^ get() { return m_visualTyres; } void set(List<F1VisualTyre>^ val) { m_visualTyres = val; NPC("VisualTyres"); } };
      property List<SessionEvent^>^ PitPenalties {List<SessionEvent^>^ get() { return m_otherPenalties; } void set(List<SessionEvent^>^ val) { m_otherPenalties = val; NPC("PitPenalties"); } }; // penalties, that will be served by pitstop
      property int TyreAge {int get() { return m_tyreAge; } void set(int val) { if (val != m_tyreAge) { m_tyreAge = val; NPC("TyreAge"); } } };
      property int Pos {int get() { return m_pos; } void set(int val) { if (val != m_pos) { m_pos = val; NPC("Pos"); } } };
      property int LapNr {int get() { return m_lapNr; } void set(int val) { if (val != m_lapNr) { m_lapNr = val; NPC("LapNr"); } } };
      property array<LapData^>^ Laps {array<LapData^>^ get() { return m_laps; } void set(array<LapData^>^ val) { m_laps = val; /*NPC("Laps");*/ }};
      property LapData^ FastestLap {LapData^ get() { return m_fastestLap; } void set(LapData^ val) { m_fastestLap = val; NPC("FastestLap"); }};
      property LapData^ CurrentLap {LapData^ get() { return m_currentLap; } void set(LapData^ val) { m_currentLap = val; NPC("CurrentLap"); }};
      property int PenaltySeconds {int get() { return m_penaltySeconds; } void set(int val) { if (val != m_penaltySeconds) { m_penaltySeconds = val; NPC("PenaltySeconds"); } } };
      property float TimedeltaToLeader {float get() { return m_timedeltaToLeader; } void set(float val) { if (val != m_timedeltaToLeader) { m_timedeltaToLeader = val; NPC("TimedeltaToLeader"); } } };
      property float TimedeltaToNext{ float get() { return m_timedeltaToNext; } void set(float val) { if (val != m_timedeltaToNext) { m_timedeltaToNext = val; NPC("TimedeltaToNext"); } } };
      property float TrackPositionPerc{ float get() { return m_trackPosPerc; } void set(float val) { if ((val != m_trackPosPerc) && (val > 0.f)) { m_trackPosPerc = val; NPC("TrackPositionPerc"); } } }
      property DriverPos3d^ TrackPosition3d{ DriverPos3d^ get() { return m_trackPos3d; } void set(DriverPos3d^ v) { m_trackPos3d = v; } };
      property float LocationOnTrack;

      // quali
      property bool AllowLapHistoryQuali;
      property float LastTyreUpdateByHistory; // signal to make tyre update by heuristic or take from history.

      void NPC(String^ name) { PropertyChanged(this, gcnew System::ComponentModel::PropertyChangedEventArgs(name)); }
      virtual event System::ComponentModel::PropertyChangedEventHandler^ PropertyChanged;

   private:
      
      char* m_driverNameNative = nullptr;

      String^ m_nameOverride;
      String^ m_telemetryName;
      DriverStatus m_status;
      bool m_isPlayer;
      bool m_isMainDriver;
      bool m_isSecondaryDriver;
      bool m_present;
      F1Team m_team;
      int m_driverNr{ 0 };
      F1Tyre m_tyre;
      F1VisualTyre m_visualTyre;
      List<F1VisualTyre>^ m_visualTyres;
      List<SessionEvent^>^ m_otherPenalties; // all penalties except time penalties, which can�t be served in the pits
      int m_tyreAge;
      float m_tyreDamage; // TODO Remove, not included for Online Mutiplayer when telemetry = basic
      int m_pos;
      int m_lapNr;
      int m_penaltySeconds;      
      float m_carDamage;
      array<LapData^>^ m_laps;
      LapData^ m_fastestLap;
      LapData^ m_currentLap;
      float m_timedeltaToPlayer;
      float m_lastTimedeltaToPlayer;
      float m_timedeltaToLeader;
      float m_timedeltaToNext;
      CarDetail^ m_carDetail;
      float m_trackPosPerc{ 0.f };
      DriverPos3d^ m_trackPos3d;
      SessionInfo^ m_sessionInfo;

      int m_hasPitted{ 0 };      // for tyre age, which is not directly available in non complete telemetry.
   };

   public ref class ClassificationData
   {
   public:
      property DriverData^ Driver;
      property int Position;        // Finishing position
      property int NumLaps;         // Number of laps completed
      property int GridPosition;    // Grid position of the car
      property int Points;          // Number of points scored
      property double BestLapTime;   // Best lap time of the session in seconds
      property double TotalRaceTime;// Total race time in seconds without penalties
      property int PenaltiesTime;   // Total penalties accumulated in seconds
      property int NumPenalties;    // Number of penalties applied to this driver
   };

   public ref class DriverNameDynamicMappings
   {
   public:
      DriverNameDynamicMappings() { driverNameList = gcnew Dictionary<int, array<String^>^>(); }
      void Add(int driverNumber, String^ name) { 
         array<String^>^ mappings = nullptr;
         if (driverNameList->TryGetValue(driverNumber, mappings))
         {
            for (unsigned i = 0; i < mappings->Length; ++i)
            {
               if (String::Equals(mappings[i], name))
                  return; // already existent!
            }

            if (mappings->Length < 5)
            {
               array<String^>^ mappingsNew = gcnew array<String^>(mappings->Length + 1);
               mappingsNew[0] = name;
               for (unsigned i = 0; i < mappings->Length; ++i)
               {
                  mappingsNew[i + 1] = mappings[i];
               }
               driverNameList->Remove(driverNumber);
               driverNameList->Add(driverNumber, mappingsNew);
            }
            else
            {
               // cap to 5 entries of history
               array<String^>^ mappingsNew = gcnew array<String^>(5);
               mappingsNew[0] = name;
               mappingsNew[1] = mappings[0];
               mappingsNew[2] = mappings[1];
               mappingsNew[3] = mappings[2];
               mappingsNew[4] = mappings[3];

               driverNameList->Remove(driverNumber);
               driverNameList->Add(driverNumber, mappingsNew);
            }
         }
         else
         {
            mappings = gcnew array<String^>(1);
            mappings[0] = name;
            driverNameList->Add(driverNumber, mappings);
         }
      }
      property Dictionary<int, array<String^>^>^ driverNameList;
   };


   // reduced data model for result export
   public ref class DriverDataResult : public System::ComponentModel::INotifyPropertyChanged
   {
   public:
      DriverDataResult()
      {}

      property String^ Name {String^ get() { return m_name; } void set(String^ val) { if (!String::Equals(val, m_name)) { m_name = val; NPC("Name"); } } }; // The name for Display      
      property String^ DriverTag {String^ get() { return m_tag; } void set(String^ val) { if (!String::Equals(val, m_tag)) { m_tag = val; NPC("DriverTag"); } } }; // arbitrary tag passed from name mapping to results for external use
      property DriverStatus Status {DriverStatus get() { return m_status; } void set(DriverStatus val) { if (val != m_status) { m_status = val; NPC("Status"); } } };
      property F1Team Team {F1Team get() { return m_team; } void set(F1Team val) { if (val != m_team) { m_team = val; NPC("Team"); } } };
      property int DriverNr {int get() { return m_driverNr; } void set(int val) { if (val != m_driverNr) { m_driverNr = val; NPC("DriverNr"); } } };      

      // --------- result

      property int Pos {int get() { return m_pos; } void set(int val) { if (val != m_pos) { m_pos = val; NPC("Pos"); } } };
      property int RaceTimeOnTrack;   // Total race time in milliseconds without penalties      
      property int PenaltySeconds {int get() { return m_penaltySeconds; } void set(int val) { if (val != m_penaltySeconds) { m_penaltySeconds = val; NPC("PenaltySeconds"); } } };

      property int TotalRaceTime {int get() { return RaceTimeOnTrack + m_penaltySeconds * 1000; }}
      
      // for post-processing: Additional Penalty addition or subtraction from human race director
      property int PenaltySecondsRacedirector {int get() { return m_penaltySecondsRacedirector; } void set(int val) {
         if (val != m_penaltySecondsRacedirector) {
            m_penaltySecondsRacedirector = val; NPC("PenaltySecondsRacedirector"); NPC("RaceTimeOnTrackFinal"); NPC("TotalRaceTimeFinal");
         }
      } }
      
      // for post-processing: When in game result do not match with the actual result, this time can be used to correct the order of cars
      // in ms, since sub seconds adjustments might be neccessary
      property int BugtimeRacedirector {int get() { return m_bugtimeRacedirector; } void set(int val) {
         if (val != m_bugtimeRacedirector) {
            m_bugtimeRacedirector = val; NPC("BugtimeRacedirector"); NPC("RaceTimeOnTrackFinal"); NPC("TotalRaceTimeFinal");
         }
      }}

      property int RaceTimeOnTrackFinal {int get() {return RaceTimeOnTrack + BugtimeRacedirector;}}
      property int TotalRaceTimeFinal {int get() { return RaceTimeOnTrackFinal + (PenaltySeconds + PenaltySecondsRacedirector) * 1000 ; }}


      property int GridPosition;    // Grid position of the car

      // --------- tire strategy
      property List<F1VisualTyre>^ VisualTyres {List<F1VisualTyre>^ get() { return m_visualTyres; } void set(List<F1VisualTyre>^ val) { m_visualTyres = val; NPC("VisualTyres"); } };
      
      // --------- penalties ( DT & Stop+Go) during the race
      property List<SessionEvent^>^ PitPenalties {List<SessionEvent^>^ get() { return m_otherPenalties; } void set(List<SessionEvent^>^ val) { m_otherPenalties = val; NPC("PitPenalties"); } };
      
      // --------- Laptimes during the race
      property array<LapData^>^ Laps {array<LapData^>^ get() { return m_laps; } void set(array<LapData^>^ val) { m_laps = val; NPC("Laps"); }};

      virtual event System::ComponentModel::PropertyChangedEventHandler^ PropertyChanged;

   private:
      void NPC(String^ name) { PropertyChanged(this, gcnew System::ComponentModel::PropertyChangedEventArgs(name)); }

      String^ m_name;
      String^ m_tag;
      DriverStatus m_status;
      F1Team m_team;
      int m_driverNr{ 0 };
      List<F1VisualTyre>^ m_visualTyres;
      List<SessionEvent^>^ m_otherPenalties; // all penalties except time penalties, which can�t be served in the pits
      int m_pos;
      int m_lapNr;
      int m_penaltySeconds;
      int m_penaltySecondsRacedirector;
      int m_bugtimeRacedirector;
      array<LapData^>^ m_laps;
   };

   public ref class ResultExport
   {
   public:
      property Track EventTrack;
      property SessionType Session;
      property SessionEventList^ Events;
      property int TotalLaps; // for race only
      property array<DriverDataResult^>^ Drivers;
   };

}
