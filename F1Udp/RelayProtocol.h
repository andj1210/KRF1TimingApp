// Copyright 2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only
//
// RelayProtocol -- native C++ constants for the KRF1 TCP relay protocol.
//
// Wire format: [uint8_t type][uint16_t length big-endian][payload bytes...]
// The very first frame on any new connection must be MSG_HELLO carrying
// PROTOCOL_VERSION as a 2-byte big-endian payload. The server terminates
// the connection if the version does not match.

#pragma once
#include <stdint.h>

class RelayProtocol
{
public:
   /// Increment whenever the wire format changes incompatibly.
   static constexpr uint16_t PROTOCOL_VERSION            = 1;

   static constexpr int      MAX_PAYLOAD                 = 2048;

   // ---- Client -> Server ----
   static constexpr uint8_t  MSG_HELLO                   = 0x00; ///< Version handshake, must be first
   static constexpr uint8_t  MSG_AUTH_DRIVER             = 0x01;
   static constexpr uint8_t  MSG_AUTH_ENGINEER           = 0x02;
   static constexpr uint8_t  MSG_AUTH_ENGINEER_SECONDARY = 0x03;
   static constexpr uint8_t  MSG_F1_PACKET               = 0x10;
   static constexpr uint8_t  MSG_NAME_FIX                = 0x20;

   // ---- Server -> Client ----
   static constexpr uint8_t  MSG_AUTH_OK                 = 0x80;
   static constexpr uint8_t  MSG_AUTH_FAIL               = 0x81;
   static constexpr uint8_t  MSG_DRIVER_PASSWORD         = 0x83;
   static constexpr uint8_t  MSG_HISTORY_BEGIN           = 0x91;
   static constexpr uint8_t  MSG_HISTORY_END             = 0x92;
};
