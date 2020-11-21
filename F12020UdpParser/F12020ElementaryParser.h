// Copyright 2018-2020 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

#pragma once
#include <stdint.h>
#include <string.h>
#include <fstream>
#include "F12020DataDefs.h"

struct F12020ElementaryParser
{
   unsigned ProceedPacket(const uint8_t* pData, unsigned len);

   PacketMotionData motion{};
   PacketSessionData session{};
   PacketLapData lap{};
   PacketEventData event {};
   PacketParticipantsData participants{};
   PacketCarSetupData setups{};
   PacketCarTelemetryData telemetry{};
   PacketCarStatusData status{};
   PacketFinalClassificationData classification{};
};
