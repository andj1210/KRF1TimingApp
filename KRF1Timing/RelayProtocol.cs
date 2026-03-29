// Copyright 2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only
//
// Thin C# forwarder — the implementation lives in RelayProtocolClr (C++/CLI,
// F1Udp project) which pulls constants from the native RelayProtocol.h.
// This class exists only so existing call sites need no changes.

using System.Net.Sockets;

namespace adjsw.F12025
{
   public static class RelayProtocol
   {
      // ---- Protocol version ----
      public static readonly ushort PROTOCOL_VERSION              = RelayProtocolClr.PROTOCOL_VERSION;

      // ---- Message types ----
      public static readonly byte MSG_HELLO                       = RelayProtocolClr.MSG_HELLO;
      public static readonly byte MSG_AUTH_DRIVER                 = RelayProtocolClr.MSG_AUTH_DRIVER;
      public static readonly byte MSG_AUTH_ENGINEER               = RelayProtocolClr.MSG_AUTH_ENGINEER;
      public static readonly byte MSG_AUTH_ENGINEER_SECONDARY     = RelayProtocolClr.MSG_AUTH_ENGINEER_SECONDARY;
      public static readonly byte MSG_F1_PACKET                   = RelayProtocolClr.MSG_F1_PACKET;
      public static readonly byte MSG_NAME_FIX                    = RelayProtocolClr.MSG_NAME_FIX;
      public static readonly byte MSG_AUTH_OK                     = RelayProtocolClr.MSG_AUTH_OK;
      public static readonly byte MSG_AUTH_FAIL                   = RelayProtocolClr.MSG_AUTH_FAIL;
      public static readonly byte MSG_DRIVER_PASSWORD             = RelayProtocolClr.MSG_DRIVER_PASSWORD;
      public static readonly byte MSG_HISTORY_BEGIN               = RelayProtocolClr.MSG_HISTORY_BEGIN;
      public static readonly byte MSG_HISTORY_END                 = RelayProtocolClr.MSG_HISTORY_END;
      public static readonly int  MAX_PAYLOAD                     = RelayProtocolClr.MAX_PAYLOAD;

      // ---- I/O helpers ----

      public static void SendHello(NetworkStream stream)
         => RelayProtocolClr.SendHello(stream);

      public static void SendMessage(NetworkStream stream, byte type, byte[] payload, int offset, int length)
         => RelayProtocolClr.SendMessage(stream, type, payload, offset, length);

      public static void SendMessage(NetworkStream stream, byte type, byte[] payload)
         => RelayProtocolClr.SendMessage(stream, type, payload);

      public static void SendEmpty(NetworkStream stream, byte type)
         => RelayProtocolClr.SendEmpty(stream, type);

      public static bool ReadExact(NetworkStream stream, byte[] buffer, int offset, int count)
         => RelayProtocolClr.ReadExact(stream, buffer, offset, count);

      public static bool ReadMessage(NetworkStream stream, out byte type, out byte[] payload)
         => RelayProtocolClr.ReadMessage(stream, out type, out payload);
   }
}
