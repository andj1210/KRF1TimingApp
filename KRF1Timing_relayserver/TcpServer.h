// Copyright 2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only
//
// TcpServer — listening TCP socket that accepts connections as TcpSocket.

#pragma once

#include "TcpSocket.h"
#include <cstdio>

class TcpServer 
{
public:
   TcpServer() = default;
   ~TcpServer() { Close(); }

   TcpServer(const TcpServer&) = delete;
   TcpServer& operator=(const TcpServer&) = delete;

   // Bind and listen on the given port. Returns true on success.
   bool Listen(uint16_t port);

   // Accept a new connection. Returns an invalid TcpSocket if none available.
   // On success, also fills client_ip and client_port if non-null.
   TcpSocket Accept(char* client_ip = nullptr, size_t ip_buf_len = 0, uint16_t* client_port = nullptr);

   bool IsValid() const { return m_fd != kInvalidSocket; }
   socket_t Fd() const { return m_fd; }

   void Close();

private:
   socket_t m_fd = kInvalidSocket;
};

inline void TcpServer::Close()
{
   if (m_fd != kInvalidSocket)
   {
      OsCloseSocket(m_fd);
      m_fd = kInvalidSocket;
   }
}