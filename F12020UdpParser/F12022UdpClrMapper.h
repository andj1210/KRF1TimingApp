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
   public ref class F12020UdpClrMapper
   {
   public:
      F12020UdpClrMapper();
      ~F12020UdpClrMapper();

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

   private:
      void m_Clear();

      void m_Update();
      void m_UpdateEvent();
      void m_UpdateDrivers();
      void m_UpdateTimeDeltaRace(DriverData^ reference, int i, bool toPlayer /* if false -> to leader */);
      void m_UpdateTimeDeltaQualy(DriverData^ reference, int i, bool toPlayer /* if false -> to leader */);
      void m_UpdateTyre(int i);
      void m_UpdateDamage(int i);
      void m_UpdateTelemetry(int i);

      void m_UpdateDriverName(int i); // pick the most suited driver name from telemtry + name mappings

      void m_UpdateClassification();

      DriverNameMappings^ m_nameMapings;

      F12020ElementaryParser* m_parser;
      array<Byte>^ arr;
      IntPtr pUnmanaged;
      int len;
      unsigned m_pktCtr;
   };
}

