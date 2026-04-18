#include "TcpServer.h"

bool TcpServer::Listen(uint16_t port)
{
   m_fd = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
   if (m_fd == kInvalidSocket)
   {
      std::fprintf(stderr, "Failed to create socket\n");
      return false;
   }

   int optval = 1;
   setsockopt(m_fd, SOL_SOCKET, SO_REUSEADDR,
      reinterpret_cast<const char*>(&optval), sizeof(optval));

   struct sockaddr_in addr = {};
   addr.sin_family = AF_INET;
   addr.sin_addr.s_addr = INADDR_ANY;
   addr.sin_port = htons(port);

   if (bind(m_fd, reinterpret_cast<struct sockaddr*>(&addr), sizeof(addr)) != 0)
   {
      std::fprintf(stderr, "Failed to bind on port %u\n", port);
      Close();
      return false;
   }

   if (listen(m_fd, 16) != 0) {
      std::fprintf(stderr, "Failed to listen\n");
      Close();
      return false;
   }

   OsSetNonBlocking(m_fd);
   return true;
}

TcpSocket TcpServer::Accept(char* client_ip /*= nullptr*/, size_t ip_buf_len /*= 0*/, uint16_t* client_port /*= nullptr*/)
{
   struct sockaddr_in client_addr;
   socklen_t client_len = sizeof(client_addr);
   socket_t new_fd = accept(m_fd,
      reinterpret_cast<struct sockaddr*>(&client_addr), &client_len);

   if (new_fd == kInvalidSocket)
      return TcpSocket();

   OsSetNonBlocking(new_fd);

   // Disable Nagle for low-latency
   int nodelay = 1;
   setsockopt(new_fd, IPPROTO_TCP, TCP_NODELAY,
      reinterpret_cast<const char*>(&nodelay), sizeof(nodelay));

   if (client_ip && ip_buf_len > 0)
      inet_ntop(AF_INET, &client_addr.sin_addr, client_ip, static_cast<socklen_t>(ip_buf_len));
   if (client_port)
      *client_port = ntohs(client_addr.sin_port);

   return TcpSocket(new_fd);
}
