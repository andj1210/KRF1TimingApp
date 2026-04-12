// Copyright 2018-2021 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using namespace System;
using namespace System::Runtime::InteropServices;
using namespace System::Net;
using namespace System::Net::Sockets;

#include "F1DataDefs.h"
#include "F1DataDefsClr.h"
#include "F1PacketExtractor.h"
#include <cassert>
#include <random>
#include <algorithm>
#include <memory>

namespace adjsw::F12025
{
   /// <summary>
   /// Controls how the mapper interprets the playerCarIndex in incoming packets.
   /// </summary>
   public enum class MapperMode
   {
      Direct,    // local UDP — IsPlayer is set normally
      Engineer1, // relay engineer, one driver — IsMainDriver is set for that driver
      Engineer2  // relay engineer, two drivers — IsMainDriver + IsSecondaryDriver are set
   };

   public ref class F1UdpClrMapper
   {
   public:
      F1UdpClrMapper();
      ~F1UdpClrMapper();

      bool Proceed(array<System::Byte>^ input);

      /// Feed a packet from the secondary driver's relay stream.
      /// Updates CarDamage and CarStatus for the secondary driver's car index only,
      /// identified by the playerCarIndex field in the packet header.
      bool ProceedSecondary(array<System::Byte>^ input);

      // insert some data to display, only for debugging!
      void InsertTestData();

      property SessionInfo^ SessionInfo;
      property SessionEventList^ EventList;
      property int CountDrivers;
      property array<DriverData^>^ Drivers;
      property array<ClassificationData^>^ Classification; // nullptr if no classification available

      property array<bool>^ UdpAction; // set by parser for each button push, needs to be reset by App, 12 buttons total
      property System::UInt64 SessionUID { System::UInt64 get() { return m_sessionId; } }

      // Expose the native packet extractor for RelayPacketFilter
      F12025_PacketExtractor* GetExtractor() { return m_parser; }

      // Last packet type returned by Proceed()
      property PacketType LastPacketType { PacketType get() { return m_lastPacketType; } }

      /// Connection mode — determines which role fields (IsPlayer / IsMainDriver /
      /// IsSecondaryDriver) are set on DriverData entries.
      property MapperMode Mode { MapperMode get() { return m_mode; } void set(MapperMode value) { m_mode = value; } }      

      /// Car index of the secondary driver (-1 when no secondary connection is active).
      /// When set, primary-stream CarDamage and CarStatus updates are suppressed for
      /// this index and must be supplied via ProceedSecondary() instead.
      property int SecondaryDriverIndex
      {
         int  get()          { return m_secondaryDriverIndex; }
         void set(int value) { m_secondaryDriverIndex = value; }
      }

   private:
      void m_Clear();
      
      void m_UpdateDrivers();
      void m_UpdateTimeDeltaRace(DriverData^ reference, int i, bool toPlayer /* if false -> to leader */);
      void m_UpdateTimeDeltaQualy(DriverData^ reference, int i, bool toPlayer /* if false -> to leader */);

      void m_UpdateSession();
      void m_UpdateLapRace();
      void m_UpdateLapQuali();
      void m_UpdateEventData();
      void m_UpdateParticipants();
      void m_UpdateDamage(int i);
      void m_UpdateTelemetry(int i);
      void m_UpdateDriverName(int i);
      void m_UpdateClassification();
      void m_UpdateHistoryDataRace();
      void m_UpdateHistoryDataQuali();
      void m_UpdateTyreSetsData();
      void m_UpdateCarPositions();
      void m_UpdateCarPositions3d();

      bool m_IsQualifyingOrPractice();

      uint32_t m_udpButtonPreviousMask{ 0 };
      F12025_PacketExtractor* m_parser;
      F12025_PacketExtractor* m_parserSecondary;
      array<Byte>^ arr;
      IntPtr pUnmanaged;
      IntPtr m_pUnmanagedSecondary;
      int len;
      unsigned m_pktCtr;
      uint64_t m_sessionId{};
      PacketType m_lastPacketType = PacketType::UnknownOrIllformed;
      float m_sessionConnectTime{ 0 };
      int        m_secondaryDriverIndex{ -1 };
      MapperMode m_mode{ MapperMode::Direct };
   };
}

