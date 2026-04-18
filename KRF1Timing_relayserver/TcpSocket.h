// Copyright 2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only
//
// TcpSocket — wraps a connected TCP socket with framed message I/O.

#pragma once

#include "Platform.h"
#include <cstdint>
#include <cstring>
#include <vector>

static constexpr size_t kMaxPayload = 2048;

// Per-connection read buffer with framed message parsing.
struct ReadBuffer 
{
   uint8_t buf[3 + kMaxPayload]; // header + max payload
   size_t  have = 0;

   // Try to consume one complete message from the buffer.
   // Returns true and fills out type/payload/plen if a full message is available.
   bool try_consume(uint8_t& type, const uint8_t*& payload, uint16_t& plen) 
   {
      if (have < 3)
         return false;
      type = buf[0];
      plen = static_cast<uint16_t>((buf[1] << 8) | buf[2]);
      if (plen > kMaxPayload)
         return false; // protocol error
      size_t total = 3 + plen;
      if (have < total)
         return false;
      payload = buf + 3;
      return true;
   }

   void consume(uint16_t plen) 
   {
      size_t total = 3 + plen;
      if (total < have)
         std::memmove(buf, buf + total, have - total);
      have -= total;
   }

   // Read from socket into buffer. Returns bytes read, 0 on close, -1 on error/would-block.
   int recv_into(socket_t fd) 
   {
      size_t space = sizeof(buf) - have;
      if (space == 0) return -1;
      int n = recv(fd, reinterpret_cast<char*>(buf + have), static_cast<int>(space), 0);
      if (n > 0) have += n;
      return n;
   }
};

// A connected TCP socket with framed message send/receive.
class TcpSocket {
public:
   TcpSocket() = default;
   explicit TcpSocket(socket_t fd) : m_fd(fd) {}

   TcpSocket(TcpSocket&& other) noexcept
      : m_fd(other.m_fd), m_rbuf(other.m_rbuf)
   {
      other.m_fd = kInvalidSocket;
      other.m_rbuf = {};
   }

   TcpSocket& operator=(TcpSocket&& other) noexcept 
   {
      if (this != &other) 
      {
         Close();
         m_fd = other.m_fd;
         m_rbuf = other.m_rbuf;
         other.m_fd = kInvalidSocket;
         other.m_rbuf = {};
      }
      return *this;
   }

   TcpSocket(const TcpSocket&) = delete;
   TcpSocket& operator=(const TcpSocket&) = delete;

   ~TcpSocket() { Close(); }

   bool IsValid() const { return m_fd != kInvalidSocket; }
   socket_t Fd() const { return m_fd; }

   void Close() 
   {
      if (m_fd != kInvalidSocket) 
      {
         OsCloseSocket(m_fd);
         m_fd = kInvalidSocket;
      }
   }

   // Release ownership of the fd (for transferring to another TcpSocket).
   socket_t Release() 
   {
      socket_t fd = m_fd;
      m_fd = kInvalidSocket;
      return fd;
   }

   // --- Framed send ---

   // Send a framed message: [type:1][len:2 big-endian][payload]
   bool SendMsg(uint8_t type, const uint8_t* payload, uint16_t len) 
   {
      uint8_t hdr[3];
      hdr[0] = type;
      hdr[1] = static_cast<uint8_t>(len >> 8);
      hdr[2] = static_cast<uint8_t>(len & 0xFF);
      if (!SendAll(hdr, 3))
         return false;
      if (len > 0 && !SendAll(payload, len))
         return false;
      return true;
   }

   bool SendMsg(uint8_t type, const std::vector<uint8_t>& payload) 
   {
      return SendMsg(type, payload.data(), static_cast<uint16_t>(payload.size()));
   }

   bool SendMsgEmpty(uint8_t type) 
   {
      return SendMsg(type, nullptr, 0);
   }

   // --- Framed receive ---

   ReadBuffer& Rbuf() { return m_rbuf; }

   // Read from socket into buffer. Returns bytes read, 0 on close, -1 on error.
   int RecvInto() { return m_rbuf.recv_into(m_fd); }

   // Try to parse one complete message from the buffer.
   bool TryConsume(uint8_t& type, const uint8_t*& payload, uint16_t& plen) 
   {
      return m_rbuf.try_consume(type, payload, plen);
   }

   void Consume(uint16_t plen) { m_rbuf.consume(plen); }

private:
   bool SendAll(const void* data, size_t len) 
   {
      const uint8_t* p = static_cast<const uint8_t*>(data);
      size_t sent = 0;
      while (sent < len) 
      {
         int n = send(m_fd, reinterpret_cast<const char*>(p + sent), static_cast<int>(len - sent), 0);
         if (n <= 0) 
         {
            if (n < 0 && OsWouldBlock())
               continue;
            return false;
         }
         sent += n;
      }
      return true;
   }

   socket_t   m_fd = kInvalidSocket;
   ReadBuffer m_rbuf = {};
};
