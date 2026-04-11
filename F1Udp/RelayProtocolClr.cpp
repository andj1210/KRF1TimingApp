// Copyright 2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

#include "RelayProtocolClr.h"

using namespace adjsw::F12025;

void RelayProtocolClr::SendMessage(Stream^ stream, System::Byte type, array<System::Byte>^ payload, int offset, int length)
{
   array<System::Byte>^ header = gcnew array<System::Byte>(3);
   header[0] = type;
   header[1] = static_cast<System::Byte>(length >> 8);
   header[2] = static_cast<System::Byte>(length & 0xFF);
   stream->Write(header, 0, 3);
   if (length > 0)
      stream->Write(payload, offset, length);
}

void RelayProtocolClr::SendMessage(Stream^ stream, System::Byte type, array<System::Byte>^ payload)
{
   int len = payload != nullptr ? payload->Length : 0;
   SendMessage(stream, type, payload, 0, len);
}

void RelayProtocolClr::SendEmpty(Stream^ stream, System::Byte type)
{
   SendMessage(stream, type, nullptr, 0, 0);
}

void RelayProtocolClr::SendHello(Stream^ stream)
{
   array<System::Byte>^ payload = gcnew array<System::Byte>(2);
   payload[0] = static_cast<System::Byte>(RelayProtocol::PROTOCOL_VERSION >> 8);
   payload[1] = static_cast<System::Byte>(RelayProtocol::PROTOCOL_VERSION & 0xFF);
   SendMessage(stream, MSG_HELLO, payload);
}

bool RelayProtocolClr::ReadExact(Stream^ stream, array<System::Byte>^ buffer, int offset, int count)
{
   int read = 0;
   while (read < count)
   {
      int n;
      try
      {
         n = stream->Read(buffer, offset + read, count - read);
      }
      catch (IOException^)
      {
         return false;
      }
      if (n <= 0)
         return false;
      read += n;
   }
   return true;
}

bool RelayProtocolClr::ReadMessage(Stream^ stream, [Out] System::Byte% type, [Out] array<System::Byte>^% payload)
{
   type = 0;
   payload = nullptr;

   array<System::Byte>^ header = gcnew array<System::Byte>(3);
   if (!ReadExact(stream, header, 0, 3))
      return false;

   type = header[0];
   int length = (header[1] << 8) | header[2];

   if (length > MAX_PAYLOAD)
      return false;

   if (length == 0)
   {
      payload = System::Array::Empty<System::Byte>();
      return true;
   }

   payload = gcnew array<System::Byte>(length);
   return ReadExact(stream, payload, 0, length);
}
