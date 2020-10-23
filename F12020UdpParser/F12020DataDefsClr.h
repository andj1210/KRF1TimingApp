// Copyright 2018-2020 Andreas Jung
// Permission to use, copy, modify, and/or distribute this software for any purpose with or without fee is hereby granted, provided that the above copyright notice and this permission notice appear in all copies.
// THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.

#pragma once
using namespace System;
#include <string.h>

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

   public ref class LapData
   {
   public:
      property float Sector1;
      property float Sector2;
      property float Lap;
      property float LapsAccumulated;
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
         Name = "?";
         Pos = 0;
         LapNr = 1;
         Laps = gcnew array<LapData^>(100);

         for (int i = 0; i < Laps->Length; ++i)
            Laps[i] = gcnew LapData();

         IsPlayer = false;
         Present = false;
      }

      void SetName(const char(&pName)[48])
      {
         if (strcmp(pName, m_driverNameNative))
         {
            strncpy_s(m_driverNameNative,48, pName, 48);
            unsigned sz = strlen(m_driverNameNative);
            array<Byte>^ arr = gcnew array<Byte>(sz);
            for (unsigned i = 0; i < sz; ++i)
               arr[i] = m_driverNameNative[i];

            Name = System::Text::Encoding::UTF8->GetString(arr);
         }
      }     

      property String^ Name {String^ get() { return m_name; } void set(String^ val) { if (!String::Equals(val, m_name)) { m_name = val; NPC("Name"); } } };
      property bool IsPlayer {bool get() { return m_isPlayer; } void set(bool val) { if (val != m_isPlayer) { m_isPlayer = val; NPC("IsPlayer"); } } };
      property bool Present {bool get() { return m_present; } void set(bool val) { if (val != m_present) { m_present = val; NPC("Present"); } } };
      property F1Team Team {F1Team get() { return m_team; } void set(F1Team val) { if (val != m_team) { m_team = val; NPC("Team"); } } };
      property F1Tyre Tyre {F1Tyre get() { return m_tyre; } void set(F1Tyre val) { if (val != m_tyre) { m_tyre = val; NPC("Tyre"); } } };
      property F1VisualTyre VisualTyre {F1VisualTyre get() { return m_visualTyre; } void set(F1VisualTyre val) { if (val != m_visualTyre) { m_visualTyre = val; NPC("VisualTyre"); } } };
      property float TyreDamage {float get() { return m_tyreDamage; } void set(float val) { if (val != m_tyreDamage) { m_tyreDamage = val; NPC("TyreDamage"); } } };
      property int Pos {int get() { return m_pos; } void set(int val) { if (val != m_pos) { m_pos = val; NPC("Pos"); } } };
      property int LapNr {int get() { return m_lapNr; } void set(int val) { if (val != m_lapNr) { m_lapNr = val; NPC("LapNr"); } } };
      property array<LapData^>^ Laps {array<LapData^>^ get() { return m_laps; } void set(array<LapData^>^ val) { m_laps = val; /*NPC("Laps");*/ }};
      property int PenaltySeconds {int get() { return m_penaltySeconds; } void set(int val) { if (val != m_penaltySeconds) { m_penaltySeconds = val; NPC("PenaltySeconds"); } } };
      property float TimedeltaToPlayer {float get() { return m_timedeltaToPlayer; } void set(float val) { if (val != m_timedeltaToPlayer) { m_timedeltaToPlayer = val; NPC("TimedeltaToPlayer"); } } };
      property float LastTimedeltaToPlayer {float get() { return m_lastTimedeltaToPlayer; } void set(float val) { if (val != m_lastTimedeltaToPlayer) { m_lastTimedeltaToPlayer = val; NPC("LastTimedeltaToPlayer"); } } };
      property float CarDamage {float get() { return m_carDamage; } void set(float val) { if (val != m_carDamage) { m_carDamage = val; NPC("CarDamage"); } } };

      property CarDetail^ WearDetail {CarDetail^ get() { return m_carDetail; } void set(CarDetail^ val) { m_carDetail = val; } };

      virtual event System::ComponentModel::PropertyChangedEventHandler^ PropertyChanged;

   private:
      void NPC(String^ name) { PropertyChanged(this, gcnew System::ComponentModel::PropertyChangedEventArgs(name)); }
      char* m_driverNameNative = nullptr;

      String^ m_name;
      bool m_isPlayer;
      bool m_present;
      F1Team m_team;
      F1Tyre m_tyre;
      F1VisualTyre m_visualTyre;
      float m_tyreDamage;
      int m_pos;
      int m_lapNr;
      int m_penaltySeconds;      
      float m_carDamage;

      array<LapData^>^ m_laps;
      float m_timedeltaToPlayer;
      float m_lastTimedeltaToPlayer;
      CarDetail^ m_carDetail;

   };
}
