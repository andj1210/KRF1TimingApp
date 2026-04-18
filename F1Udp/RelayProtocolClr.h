// Copyright 2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only
//
// RelayProtocolClr -- C++/CLI managed wrapper for the KRF1 relay protocol.
//
// Exposes all RelayProtocol constants as CLR properties and provides the
// framed I/O helpers that drive RelayClient and RelayUplink on the C# side.

#pragma once
#include "RelayProtocol.h"

using namespace System;
using namespace System::IO;
using namespace System::Net::Sockets;
using namespace System::Runtime::InteropServices;

namespace adjsw::F12025
{
   public ref class RelayProtocolClr sealed
   {
   public:
      // ---- Protocol version ----
      static property System::UInt16 PROTOCOL_VERSION { System::UInt16 get() { return RelayProtocol::PROTOCOL_VERSION; } }

      // ---- Message type constants ----
      static property System::Byte MSG_HELLO { System::Byte get() { return RelayProtocol::MSG_HELLO; } }
      static property System::Byte MSG_AUTH_DRIVER { System::Byte get() { return RelayProtocol::MSG_AUTH_DRIVER; } }
      static property System::Byte MSG_AUTH_ENGINEER { System::Byte get() { return RelayProtocol::MSG_AUTH_ENGINEER; } }
      static property System::Byte MSG_AUTH_ENGINEER_SECONDARY { System::Byte get() { return RelayProtocol::MSG_AUTH_ENGINEER_SECONDARY; } }
      static property System::Byte MSG_F1_PACKET { System::Byte get() { return RelayProtocol::MSG_F1_PACKET; } }
      static property System::Byte MSG_NAME_FIX { System::Byte get() { return RelayProtocol::MSG_NAME_FIX; } }
      static property System::Byte MSG_AUTH_OK { System::Byte get() { return RelayProtocol::MSG_AUTH_OK; } }
      static property System::Byte MSG_AUTH_FAIL { System::Byte get() { return RelayProtocol::MSG_AUTH_FAIL; } }
      static property System::Byte MSG_DRIVER_PASSWORD { System::Byte get() { return RelayProtocol::MSG_DRIVER_PASSWORD; } }
      static property System::Byte MSG_HISTORY_BEGIN { System::Byte get() { return RelayProtocol::MSG_HISTORY_BEGIN; } }
      static property System::Byte MSG_HISTORY_END { System::Byte get() { return RelayProtocol::MSG_HISTORY_END; } }
      static property System::Int32 MAX_PAYLOAD { System::Int32 get() { return RelayProtocol::MAX_PAYLOAD; } }

      // ---- Framed I/O ----

      /// <summary>
      /// Send a framed message: [type:1][len:2 BE][payload].
      /// </summary>
      static void SendMessage(Stream^ stream, System::Byte type,
                              array<System::Byte>^ payload, int offset, int length);

      static void SendMessage(Stream^ stream, System::Byte type,
                              array<System::Byte>^ payload);

      static void SendEmpty(Stream^ stream, System::Byte type);

      /// <summary>
      /// Send the MSG_HELLO version handshake. Must be the very first frame
      /// sent after connecting, before the auth message.
      /// </summary>
      static void SendHello(Stream^ stream);

      /// <summary>
      /// Read exactly <paramref name="count"/> bytes from the stream.
      /// Returns false on disconnect / IO error.
      /// </summary>
      static bool ReadExact(Stream^ stream, array<System::Byte>^ buffer,
                            int offset, int count);

      /// <summary>
      /// Read one complete framed message.
      /// Returns false on disconnect / protocol error.
      /// </summary>
      static bool ReadMessage(Stream^ stream,
                              [Out] System::Byte% type,
                              [Out] array<System::Byte>^% payload);
   };
}
