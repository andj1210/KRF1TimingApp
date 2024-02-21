// Copyright 2018-2021 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using namespace System;
using namespace System::Runtime::InteropServices;
using namespace System::Net;
using namespace System::Net::Sockets;

#include "F12022DataDefs.h"
#include "F12022DataDefsClr.h"
#include "F12022ElementaryParser.h"
#include <cassert>
#include <random>
#include <algorithm>

namespace adjsw::F12022
{
   public ref class F12022UdpClrMapper
   {
   public:
      F12022UdpClrMapper();
      ~F12022UdpClrMapper();

      bool Proceed(array<System::Byte>^ input);

      // insert some data to display, only for debugging!
      void InsertTestData();

      void SetDriverNameMappings(DriverNameMappings^ newMappings);

      property SessionInfo^ SessionInfo;
      property SessionEventList^ EventList;
      property int CountDrivers;
      property array<DriverData^>^ Drivers;
      property array<ClassificationData^>^ Classification; // nullptr if no classification available
      property DriverNameMappings^ NameMappings {DriverNameMappings^ get() { return m_nameMapings; } };

      property bool Udp1Action; // set by parser for each button push, needs to be reset by App
   private:
      void m_Clear();
      
      void m_UpdateDrivers();
      void m_UpdateTimeDeltaRace(DriverData^ reference, int i, bool toPlayer /* if false -> to leader */);
      void m_UpdateTimeDeltaQualy(DriverData^ reference, int i, bool toPlayer /* if false -> to leader */);

      void m_UpdateSession();
      void m_UpdateLap();
      void m_UpdateEventData();
      void m_UpdateParticipants();
      void m_UpdateTyreDamage(int i);
      void m_UpdateDamage(int i);
      void m_UpdateTelemetry(int i);
      void m_UpdateDriverName(int i); // pick the most suited driver name from telemtry + name mappings
      void m_UpdateClassification();
      void m_UpdateHistoryData();
      void m_UpdateTyreSetsData();

      bool m_IsQualifyingOrPractice();

      bool m_udp1Previous{ false };
      DriverNameMappings^ m_nameMapings;
      F1_23_PacketExtractor* m_parser;
      array<Byte>^ arr;
      IntPtr pUnmanaged;
      int len;
      unsigned m_pktCtr;
      uint64_t m_sessionId{};
      float m_sessionConnectTime{ 0 };
   };
}

