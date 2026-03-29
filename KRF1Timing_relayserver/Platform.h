// Copyright 2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only
//
// Platform abstraction for cross-platform socket code (Linux / Windows).

#pragma once

#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <ws2tcpip.h>
using socklen_t = int;
using socket_t  = SOCKET;
using poll_fd   = WSAPOLLFD;
static constexpr socket_t kInvalidSocket = INVALID_SOCKET;
inline int  OsPollFd(poll_fd* fds, unsigned n, int ms) { return WSAPoll(fds, n, ms); }
inline void OsCloseSocket(socket_t s) { closesocket(s); }
inline int  OsGetErrno() { return WSAGetLastError(); }
inline bool OsWouldBlock() { int e = OsGetErrno(); return e == WSAEWOULDBLOCK || e == WSAEINPROGRESS; }
inline void OsSetNonBlocking(socket_t s) 
{
   u_long mode = 1;
   ioctlsocket(s, FIONBIO, &mode);
}
struct WsaInit 
{
   WsaInit()  { WSADATA d; WSAStartup(MAKEWORD(2, 2), &d); }
   ~WsaInit() { WSACleanup(); }
};

#else
#include <sys/socket.h>
#include <sys/types.h>
#include <netinet/in.h>
#include <netinet/tcp.h>
#include <arpa/inet.h>
#include <poll.h>
#include <unistd.h>
#include <fcntl.h>
#include <cerrno>
using socket_t = int;
using poll_fd  = struct pollfd;
static constexpr socket_t kInvalidSocket = -1;
inline int  OsPollFd(poll_fd* fds, unsigned n, int ms) { return poll(fds, n, ms); }
inline void OsCloseSocket(socket_t s) { close(s); }
inline int  OsGetErrno() { return errno; }
inline bool OsWouldBlock() { return errno == EAGAIN || errno == EWOULDBLOCK; }
inline void OsSetNonBlocking(socket_t s) 
{
   int flags = fcntl(s, F_GETFL, 0);
   fcntl(s, F_SETFL, flags | O_NONBLOCK);
}
struct WsaInit {}; // no-op on Linux
#endif

#include <chrono>

// Monotonic-ish wall clock, good enough for timeouts
inline double OsSeconds() 
{
   static auto tp = std::chrono::steady_clock::now();
   auto now = std::chrono::steady_clock::now();
   auto nowMs = std::chrono::duration_cast<std::chrono::milliseconds>(tp-now).count();

   return nowMs / 1000.0;
}
