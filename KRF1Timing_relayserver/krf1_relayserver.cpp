// Copyright 2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only
//
// KRF1 Telemetry Relay Server -- MVP
// Cross-platform (Linux / Windows).
//
// Build:
//   cmake -B build && cmake --build build --config Release
//
// Usage:  krf1_relayserver [port]    (default: 9877)

#include "TcpServer.h"
#include "DriverSession.h"
#include "RelayProtocol.h"

#include <cstdlib>
#include <ctime>
#include <memory>
#include <string>
#include <vector>
#include <algorithm>
#include <random>

static constexpr uint16_t DEFAULT_PORT = 9877;

// ---- Password generation ------------------------------------------------

static const char* s_colors[] = { "red", "blue", "green", "gold", "gray", "cyan", "pink", "lime" };
static constexpr int kNumColors = 8;

static std::mt19937 s_rng(static_cast<unsigned>(std::time(nullptr)));

static std::string generate_password()
{
   int color = std::uniform_int_distribution<int>(0, kNumColors - 1)(s_rng);
   int num   = std::uniform_int_distribution<int>(0, 9999)(s_rng);
   char buf[32];
   std::snprintf(buf, sizeof(buf), "%s%04d", s_colors[color], num);
   return buf;
}

// ---- Pending (unauthenticated) connection -------------------------------

struct PendingConn
{
   TcpSocket socket;
   double    connect_time = 0;
   bool      version_ok   = false;
};

// ---- Server state -------------------------------------------------------

static std::vector<std::unique_ptr<DriverSession>> gSessions;
static std::vector<PendingConn>                    gPending;

// ---- Helpers ------------------------------------------------------------

static DriverSession* find_session_by_password(const std::string& pw)
{
   for (auto& s : gSessions)
   {
      if (s && s->IsAlive() && s->Password() == pw)
         return s.get();
   }
   return nullptr;
}

static bool password_in_use(const std::string& pw)
{
   return find_session_by_password(pw) != nullptr;
}

static std::string generate_unique_password()
{
   for (int attempt = 0; attempt < 1000; attempt++)
   {
      auto pw = generate_password();
      if (!password_in_use(pw))
         return pw;
   }
   return "fallback0000";
}

// ---- Auth handlers ------------------------------------------------------

static void handle_hello(PendingConn& pc, const uint8_t* payload, uint16_t len)
{
   if (len < 2)
   {
      pc.socket.Close();
      return;
   }

   uint16_t client_version = (static_cast<uint16_t>(payload[0]) << 8) | payload[1];
   if (client_version != RelayProtocol::PROTOCOL_VERSION)
   {
      const char* reason = "protocol version mismatch";
      pc.socket.SendMsg(RelayProtocol::MSG_AUTH_FAIL,
         reinterpret_cast<const uint8_t*>(reason),
         static_cast<uint16_t>(std::strlen(reason)));
      std::printf("[main] version mismatch: client=%u server=%u -- disconnecting\n",
         client_version, static_cast<unsigned>(RelayProtocol::PROTOCOL_VERSION));
      pc.socket.Close();
      return;
   }

   pc.version_ok = true;
}

static void handle_auth_driver(PendingConn& pc)
{
   std::string pw = generate_unique_password();

   // Send AUTH_OK
   uint8_t ok_payload[1] = { 1 };
   pc.socket.SendMsg(RelayProtocol::MSG_AUTH_OK, ok_payload, 1);

   // Send password
   uint8_t pw_buf[16] = {};
   std::memcpy(pw_buf, pw.c_str(), std::min(pw.size(), size_t(16)));
   pc.socket.SendMsg(RelayProtocol::MSG_DRIVER_PASSWORD, pw_buf, 16);

   std::printf("[main] driver connected, password=%s\n", pw.c_str());

   // Create session and hand over the socket
   auto session = std::make_unique<DriverSession>();
   session->Start(std::move(pc.socket), pw);
   gSessions.push_back(std::move(session));
}

static void handle_auth_engineer(PendingConn& pc, const uint8_t* payload, uint16_t len, bool secondary)
{
   char pw_buf[17] = {};
   size_t pw_len = std::min(static_cast<size_t>(len), size_t(16));
   std::memcpy(pw_buf, payload, pw_len);
   std::string pw(pw_buf);

   DriverSession* session = find_session_by_password(pw);
   if (!session)
   {
      const char* reason = "no driver with that password";
      pc.socket.SendMsg(RelayProtocol::MSG_AUTH_FAIL,
         reinterpret_cast<const uint8_t*>(reason),
         static_cast<uint16_t>(std::strlen(reason)));
      std::printf("[main] engineer auth failed, password=%s\n", pw.c_str());
      return;
   }

   // Send AUTH_OK before handing to session thread
   uint8_t ok_payload[1] = { 1 };
   pc.socket.SendMsg(RelayProtocol::MSG_AUTH_OK, ok_payload, 1);

   std::printf("[main] engineer authenticated for driver %s (secondary=%d)\n",
      pw.c_str(), secondary ? 1 : 0);

   // Hand the socket to the session thread
   session->AddEngineer(std::move(pc.socket), secondary);
}

// ---- Reap dead sessions -------------------------------------------------

static void reap_dead_sessions()
{
   for (auto it = gSessions.begin(); it != gSessions.end(); )
   {
      if (*it && !(*it)->IsAlive())
      {
         std::printf("[main] reaping session %s\n", (*it)->Password().c_str());
         (*it)->Join();
         it = gSessions.erase(it);
      }
      else
      {
         ++it;
      }
   }
}

// ---- Main loop ----------------------------------------------------------

int main(int argc, char** argv)
{
   WsaInit wsa_init;

   uint16_t port = DEFAULT_PORT;
   if (argc > 1)
   {
      port = static_cast<uint16_t>(std::atoi(argv[1]));
      if (port == 0) port = DEFAULT_PORT;
   }

   TcpServer server;
   if (!server.Listen(port))
      return 1;

   std::printf("KRF1 Relay Server listening on port %u\n", port);

   // Main thread only handles: accept, pending auth, session reaping.
   // All per-driver I/O runs in DriverSession threads.
   std::vector<poll_fd> pfds;

   for (;;)
   {
      const size_t snap_pending = gPending.size();

      // Build poll fd array: [listen_fd, pending_0, pending_1, ...]
      
      pfds.clear();
      pfds.reserve(1 + snap_pending);
      pfds.push_back({ server.Fd(), POLLIN, 0 });

      for (size_t i = 0; i < snap_pending; i++)
      {
         if (gPending[i].socket.IsValid())
            pfds.push_back({ gPending[i].socket.Fd(), POLLIN, 0 });
         else
            pfds.push_back({ kInvalidSocket, 0, 0 });
      }

      int ret = OsPollFd(pfds.data(), static_cast<unsigned>(pfds.size()), 200);
      if (ret < 0)
      {
         if (OsWouldBlock()) continue;
         break;
      }

      double now = OsSeconds();
      size_t idx = 0;

      // ---- Accept new connections ----
      if (pfds[idx].revents & POLLIN)
      {
         char ip_str[INET_ADDRSTRLEN];
         uint16_t client_port;
         TcpSocket new_sock = server.Accept(ip_str, sizeof(ip_str), &client_port);
         if (new_sock.IsValid())
         {
            std::printf("[main] new connection from %s:%u\n", ip_str, client_port);
            PendingConn pc;
            pc.socket       = std::move(new_sock);
            pc.connect_time = now;
            gPending.push_back(std::move(pc));
         }
      }
      idx++;

      // ---- Process pending (unauthenticated) connections ----
      for (size_t pi = 0; pi < snap_pending; pi++, idx++)
      {
         auto& pc = gPending[pi];
         if (!pc.socket.IsValid()) continue;

         // Timeout unauthenticated connections after 10 seconds
         if ((now - pc.connect_time) > 10.0)
         {
            pc.socket.Close();
            continue;
         }

         if (pfds[idx].revents & (POLLERR | POLLHUP | POLLNVAL))
         {
            pc.socket.Close();
            continue;
         }

         if (pfds[idx].revents & POLLIN)
         {
            int n = pc.socket.RecvInto();
            if (n <= 0 && !OsWouldBlock())
            {
               pc.socket.Close();
               continue;
            }

            // Drain all complete messages from the buffer.  HELLO and AUTH
            // may arrive in the same recv() call (especially on reconnect
            // when JIT-compiled code sends them back-to-back).
            uint8_t        msg_type;
            const uint8_t* msg_payload;
            uint16_t       msg_len;
            while (pc.socket.TryConsume(msg_type, msg_payload, msg_len))
            {
               switch (msg_type)
               {
               case RelayProtocol::MSG_HELLO:
                  handle_hello(pc, msg_payload, msg_len);
                  break;
               case RelayProtocol::MSG_AUTH_DRIVER:
                  if (!pc.version_ok) { pc.socket.Close(); break; }
                  handle_auth_driver(pc);
                  break;
               case RelayProtocol::MSG_AUTH_ENGINEER:
                  if (!pc.version_ok) { pc.socket.Close(); break; }
                  handle_auth_engineer(pc, msg_payload, msg_len, false);
                  break;
               case RelayProtocol::MSG_AUTH_ENGINEER_SECONDARY:
                  if (!pc.version_ok) { pc.socket.Close(); break; }
                  handle_auth_engineer(pc, msg_payload, msg_len, true);
                  break;
               default:
                  pc.socket.Close();
                  break;
               }
               // If the connection was promoted (socket moved) or closed,
               // stop processing -- the buffer belongs to DriverSession now.
               if (!pc.socket.IsValid())
                  break;
               pc.socket.Consume(msg_len);
            }
         }
      }

      // ---- Clean up dead pending slots ----
      gPending.erase(
         std::remove_if(gPending.begin(), gPending.end(),
            [](const PendingConn& p) { return !p.socket.IsValid(); }),
         gPending.end());

      // ---- Reap dead sessions ----
      reap_dead_sessions();

      // ---- Periodic status ----
      static int status_counter = 0;
      if (++status_counter >= 25)
      {
         status_counter = 0;
         int alive = 0;
         for (auto& s : gSessions)
            if (s && s->IsAlive()) alive++;
         if (alive > 0 || !gPending.empty())
            std::printf("[status] sessions=%d pending=%zu\n",
               alive, gPending.size());
      }
   }

   // ---- Shutdown: stop all sessions ----
   std::printf("[main] shutting down...\n");
   for (auto& s : gSessions)
   {
      if (s)
      {
         s->RequestStop();
         s->Join();
      }
   }
   gSessions.clear();

   return 0;
}
