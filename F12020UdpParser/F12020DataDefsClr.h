// Copyright 2018-2021 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
using namespace System;
using namespace System::Collections::Generic;
#include <string.h>
#include <list>

namespace adjsw::F12020
{
   public enum class F1Team : int
   {
   Mercedes,
   Ferrari,
   RedBull,
   Williams,
   ForceIndia,
   Renault,
   TorroRosso,
   Haas,   
   McLaren,
   Sauber,
   Classic
   };

   // F1 2020:
   // Modern - 16 = C5, 17 = C4, 18 = C3, 19 = C2, 20 = C1
   // 7 = inter, 8 = wet
   // F1 Classic - 9 = dry, 10 = wet
   // F2 – 11 = super soft, 12 = soft, 13 = medium, 14 = hard
   // 15 = wet

   public enum class F1Tyre : int
   {
      // F1 Modern - 16 = C5, 17 = C4, 18 = C3, 19 = C2, 20 = C1
      // 7 = inter, 8 = wet
      // F1 Classic - 9 = dry, 10 = wet
      // F2 – 11 = super soft, 12 = soft, 13 = medium, 14 = hard
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
      // F1 Classic – same as above
      // F2 – same as above

      Intermediate = 7,
      Wet = 8,

      Soft = 16,
      Medium = 17,
      Hard = 18
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
      Zandvoort
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
      Race,
      Race2,
      TimeTrial
   };

   public enum class DriverStatus
   {
      Garage,
      OnTrack,
      Pitlane,
      Pitting,
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
      RetiredMechanicalFailure,
      RetiredTerminallyDamaged,
      SafetyCarFallingTooFarBack,
      BlackFlagTimer,
      UnservedStopGoPenalty,
      UnservedDriveThroughPenalty,
      EngineComponentChange,
      GearboxChange,
      LeagueGridPenalty,
      RetryPenalty,
      IllegalTimeGain,
      MandatoryPitstop
   };


   public ref class SessionEvent
   {
   public:
      property DateTime TimeCode;
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

      void NPC(String^ name) { PropertyChanged(this, gcnew System::ComponentModel::PropertyChangedEventArgs(name)); }
      virtual event System::ComponentModel::PropertyChangedEventHandler^ PropertyChanged;

   private:
      Track m_track{ Track::Austria };
      SessionType m_session{ SessionType::P1 };
      bool m_sessionFinished{ false };
      int m_remainingSeconds{ 0 };
      int m_totalLaps{ 2 };
      int m_currentLap{ 1 };      
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
      property float Sector1;
      property float Sector2;
      property float Lap;
      property float LapsAccumulated;
      property List<SessionEvent^>^ Incidents;
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

   public ref class DriverData : public System::ComponentModel::INotifyPropertyChanged
   {
   public:
      DriverData()
      {
         m_driverNameNative = new char[48];
         Reset();
         m_carDetail = gcnew CarDetail;
      }
      ~DriverData() { delete m_driverNameNative; }

      void Reset()
      {
         Name = "";
         TelemetryName = "";
         MappedName = "";
         m_driverNameNative[0] = 0;
         Pos = 0;
         LapNr = 1;
         Laps = gcnew array<LapData^>(100); // 100 Laps ought to be enough for anybody
         for (int i = 0; i < Laps->Length; ++i)
         {
            Laps[i] = gcnew LapData();
            Laps[i]->Incidents = gcnew List<SessionEvent^>();
         }
         FastestLap = gcnew LapData();           

         IsPlayer = false;
         Present = false;
         VisualTyres = gcnew List<F1VisualTyre>();
         PitPenalties = gcnew List<SessionEvent^>();
         m_lapTiresFitted = 1;
         m_hasPitted = false;
      }

      void SetNameFromTelemetry(const char(&pName)[48])
      {
         if (strcmp(pName, m_driverNameNative))
         {
            strncpy_s(m_driverNameNative,48, pName, 48);
            unsigned sz = strlen(m_driverNameNative);
            array<Byte>^ arr = gcnew array<Byte>(sz);
            for (unsigned i = 0; i < sz; ++i)
               arr[i] = m_driverNameNative[i];

            TelemetryName = System::Text::Encoding::UTF8->GetString(arr);
         }
      }

      property String^ Name {String^ get() { return m_name; } void set(String^ val) { if (!String::Equals(val, m_name)) { m_name = val; NPC("Name"); } } }; // The name for Display
      property String^ TelemetryName {String^ get() { return m_telemetryName; } void set(String^ val) { if (!String::Equals(val, m_telemetryName)) { m_telemetryName = val; NPC("TelemetryName"); } } }; // The name from telemetry
      property String^ MappedName {String^ get() { return m_mappedName; } void set(String^ val) { if (!String::Equals(val, m_mappedName)) { m_mappedName = val; NPC("MappedName"); } } }; // The name from translation mappings
      property bool IsPlayer {bool get() { return m_isPlayer; } void set(bool val) { if (val != m_isPlayer) { m_isPlayer = val; NPC("IsPlayer"); } } };
      property bool Present {bool get() { return m_present; } void set(bool val) { if (val != m_present) { m_present = val; NPC("Present"); } } };
      property DriverStatus Status {DriverStatus get() { return m_status; } void set(DriverStatus val) { if (val != m_status) { m_status = val; NPC("Status"); } } };
      property F1Team Team {F1Team get() { return m_team; } void set(F1Team val) { if (val != m_team) { m_team = val; NPC("Team"); } } };
      property int DriverNr {int get() { return m_driverNr; } void set(int val) { if (val != m_driverNr) { m_driverNr = val; NPC("DriverNr"); } } };
      property F1Tyre Tyre {F1Tyre get() { return m_tyre; } void set(F1Tyre val) { if (val != m_tyre) { m_tyre = val; NPC("Tyre"); } } };
      property F1VisualTyre VisualTyre {F1VisualTyre get() { return m_visualTyre; } void set(F1VisualTyre val) { if (val != m_visualTyre) { m_visualTyre = val; NPC("VisualTyre"); } } };
      property List<F1VisualTyre>^ VisualTyres {List<F1VisualTyre>^ get() { return m_visualTyres; } void set(List<F1VisualTyre>^ val) { m_visualTyres = val; NPC("VisualTyres"); } };
      property List<SessionEvent^>^ PitPenalties {List<SessionEvent^>^ get() { return m_otherPenalties; } void set(List<SessionEvent^>^ val) { m_otherPenalties = val; NPC("PitPenalties"); } };
      property int TyreAge {int get() { return m_tyreAge; } void set(int val) { if (val != m_tyreAge) { m_tyreAge = val; NPC("TyreAge"); } } };
      property float TyreDamage {float get() { return m_tyreDamage; } void set(float val) { if (val != m_tyreDamage) { m_tyreDamage = val; NPC("TyreDamage"); } } };
      property int Pos {int get() { return m_pos; } void set(int val) { if (val != m_pos) { m_pos = val; NPC("Pos"); } } };
      property int LapNr {int get() { return m_lapNr; } void set(int val) { if (val != m_lapNr) { m_lapNr = val; NPC("LapNr"); } } };
      property array<LapData^>^ Laps {array<LapData^>^ get() { return m_laps; } void set(array<LapData^>^ val) { m_laps = val; /*NPC("Laps");*/ }};
      property LapData^ FastestLap {LapData^ get() { return m_fastestLap; } void set(LapData^ val) { m_fastestLap = val; NPC("FastestLap"); }};
      property int PenaltySeconds {int get() { return m_penaltySeconds; } void set(int val) { if (val != m_penaltySeconds) { m_penaltySeconds = val; NPC("PenaltySeconds"); } } };
      property float TimedeltaToPlayer {float get() { return m_timedeltaToPlayer; } void set(float val) { if (val != m_timedeltaToPlayer) { m_timedeltaToPlayer = val; NPC("TimedeltaToPlayer"); } } };
      property float LastTimedeltaToPlayer {float get() { return m_lastTimedeltaToPlayer; } void set(float val) { if (val != m_lastTimedeltaToPlayer) { m_lastTimedeltaToPlayer = val; NPC("LastTimedeltaToPlayer"); } } };
      property float TimedeltaToLeader {float get() { return m_timedeltaToLeader; } void set(float val) { if (val != m_timedeltaToLeader) { m_timedeltaToLeader = val; NPC("TimedeltaToLeader"); } } };
      property float CarDamage {float get() { return m_carDamage; } void set(float val) { if (val != m_carDamage) { m_carDamage = val; NPC("CarDamage"); } } };

      property CarDetail^ WearDetail {CarDetail^ get() { return m_carDetail; } void set(CarDetail^ val) { m_carDetail = val; } };

      // temporary state only for the UDP mapper
      // It is used to compute the age of tires by some pitlane heuristics. (tyre age should directly be present in telemetry, but actually is dummy value when using reduced telemetry.)
      property bool HasPittedLatch {bool get() { return m_hasPitted; } void set(bool val) { m_hasPitted = val;} };
      property int LapTiresFittedLatch {int get() { return m_lapTiresFitted; } void set(int val) { m_lapTiresFitted = val; } };

      void NPC(String^ name) { PropertyChanged(this, gcnew System::ComponentModel::PropertyChangedEventArgs(name)); }
      virtual event System::ComponentModel::PropertyChangedEventHandler^ PropertyChanged;

   private:
      
      char* m_driverNameNative = nullptr;

      String^ m_name;
      String^ m_telemetryName;
      String^ m_mappedName;
      DriverStatus m_status;
      bool m_isPlayer;
      bool m_present;
      F1Team m_team;
      int m_driverNr{ 0 };
      F1Tyre m_tyre;
      F1VisualTyre m_visualTyre;
      List<F1VisualTyre>^ m_visualTyres;
      List<SessionEvent^>^ m_otherPenalties; // all penalties except time penalties, which can´t be served in the pits
      int m_tyreAge;
      float m_tyreDamage; // TODO Remove, not included for Online Mutiplayer when telemetry = basic
      int m_pos;
      int m_lapNr;
      int m_penaltySeconds;      
      float m_carDamage;
      array<LapData^>^ m_laps;
      LapData^ m_fastestLap;
      float m_timedeltaToPlayer;
      float m_lastTimedeltaToPlayer;
      float m_timedeltaToLeader;
      CarDetail^ m_carDetail;
      int m_lapTiresFitted{ 1 }; // for tyre age, which is not directly available in non complete telemetry.
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
      property float BestLapTime;   // Best lap time of the session in seconds
      property double TotalRaceTime;// Total race time in seconds without penalties
      property int PenaltiesTime;   // Total penalties accumulated in seconds
      property int NumPenalties;    // Number of penalties applied to this driver
   };

   public ref class DriverNameMapping
   {
   public:
      property String^ Name;
      property Nullable<F1Team> Team;
      property int DriverNumber;
      property String^ tag; // some tag which is passed to the result file for arbitrary use (i.e. Id to an external database Id for this driver)

      String^ ToString() override
      {
         return "" + Name + " (" + (Team.HasValue ? Team.Value.ToString("g") + " | " : "") + DriverNumber + ")";
      }
   };

   public ref class DriverNameMappings
   {
   public:
      property String^ LeagueName; // the name of the mapping set
      property array<DriverNameMapping^>^ Mappings; // each driver name mapping
   };

}
