// Copyright 2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace adjsw.F12026
{
   /// <summary>
   /// Engineer-side relay client. Connects to the relay server via TCP,
   /// authenticates with a driver password, and receives F1 UDP packets
   /// which are enqueued into the shared packet queue for processing
   /// by the existing mapper pipeline.
   /// </summary>
   public class RelayClient : IDisposable
   {
      public RelayClient(RelayConfig config, string password, ConcurrentQueue<byte[]> packetQueue,
                         bool secondary = false)
      {
         m_host        = config.Server;
         m_port        = config.Port;
         m_tlsEnabled  = config.TlsEnabled;
         m_tlsCertFile = config.TlsCertFile;
         m_password    = password;
         m_packetQueue = packetQueue;
         m_secondary   = secondary;
      }

      /// <summary>True while connected to the relay server.</summary>
      public bool IsConnected => m_connected;

      /// <summary>Fires when connection state changes.</summary>
      public event Action<string> StatusChanged;

      /// <summary>Fires on unrecoverable errors.</summary>
      public event Action<string> Error;

      /// <summary>
      /// Connect to the relay server on a background thread.
      /// Sends AUTH_ENGINEER with the driver password and waits for data.
      /// </summary>
      public void Connect()
      {
         if (m_connected)
            return;

         // A previous thread may have exited on its own (error / server gone)
         // but the reference is still set.  Check the actual OS thread state.
         if (m_thread != null)
         {
            if (m_thread.IsAlive)
               return;
            m_thread = null;
         }

         m_quit = false;
         m_thread = new Thread(ConnectAndReceiveThread);
         m_thread.IsBackground = true;
         m_thread.Start();
      }

      /// <summary>
      /// Disconnect from the relay server.
      /// </summary>
      public void Disconnect()
      {
         m_quit = true;
         try { m_client?.Close(); } catch { }

         if (m_thread != null)
         {
            m_thread.Join(2000);
            m_thread = null;
         }

         m_connected = false;
         StatusChanged?.Invoke("");
      }

      /// <summary>
      /// Send a NAME_FIX message to the server (engineer → driver).
      /// </summary>
      public void SendNameFix(byte carIndex, string name)
      {
         if (!m_connected || m_stream == null)
            return;

         try
         {
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            byte[] payload = new byte[1 + nameBytes.Length];
            payload[0] = carIndex;
            Array.Copy(nameBytes, 0, payload, 1, nameBytes.Length);
            RelayProtocol.SendMessage(m_stream, RelayProtocol.MSG_NAME_FIX, payload);
         }
         catch
         {
            // Connection lost — will be detected by the receive thread
         }
      }

      public void Dispose()
      {
         Disconnect();
      }

      // ── background thread ──────────────────────────────────────────────

      private void ConnectAndReceiveThread()
      {
         try
         {
            m_client = new TcpClient();
            m_client.NoDelay = true;
            m_client.Connect(m_host, m_port);
            m_networkStream = m_client.GetStream();
            m_stream = m_tlsEnabled ? WrapTls(m_networkStream) : (Stream)m_networkStream;

            RelayProtocol.SendHello(m_stream);

            // Send AUTH_ENGINEER (or AUTH_ENGINEER_SECONDARY) with password (zero-padded to 16 bytes)
            byte[] pwBytes = new byte[16];
            byte[] ascii = Encoding.ASCII.GetBytes(m_password);
            Array.Copy(ascii, pwBytes, Math.Min(ascii.Length, 16));
            byte authMsg = m_secondary ? RelayProtocol.MSG_AUTH_ENGINEER_SECONDARY
                                       : RelayProtocol.MSG_AUTH_ENGINEER;
            RelayProtocol.SendMessage(m_stream, authMsg, pwBytes);

            // Read AUTH_OK or AUTH_FAIL
            if (!RelayProtocol.ReadMessage(m_stream, out byte type, out byte[] payload))
            {
               Error?.Invoke("Server closed connection during auth");
               return;
            }

            if (type == RelayProtocol.MSG_AUTH_FAIL)
            {
               string reason = payload != null ? Encoding.UTF8.GetString(payload) : "unknown";
               Error?.Invoke("Auth failed: " + reason);
               return;
            }

            if (type != RelayProtocol.MSG_AUTH_OK)
            {
               Error?.Invoke("Unexpected response from server: 0x" + type.ToString("X2"));
               return;
            }

            m_connected = true;
            StatusChanged?.Invoke("Connected as engineer");

            // Receive loop — F1_PACKET messages are enqueued, history markers are handled.
            while (!m_quit)
            {
               if (!m_client.Connected)
               {
                  if (!m_quit)
                     Error?.Invoke("Server disconnected");
                  break;
               }

               if (!m_networkStream.DataAvailable)
               {
                  Thread.Sleep(50);
                  continue;
               }

               if (!RelayProtocol.ReadMessage(m_stream, out byte msgType, out byte[] msgPayload))
               {
                  if (!m_quit)
                     Error?.Invoke("Server disconnected");
                  break;
               }

               if (msgType == RelayProtocol.MSG_F1_PACKET && msgPayload != null)
               {
                  m_packetQueue.Enqueue(msgPayload);
               }
               else if (msgType == RelayProtocol.MSG_HISTORY_BEGIN)
               {
                  StatusChanged?.Invoke("Receiving history...");
               }
               else if (msgType == RelayProtocol.MSG_HISTORY_END)
               {
                  StatusChanged?.Invoke("Connected — live");
               }
               else if (msgType == RelayProtocol.MSG_AUTH_FAIL)
               {
                  string reason = msgPayload != null ? Encoding.UTF8.GetString(msgPayload) : "unknown";
                  Error?.Invoke("Server: " + reason);
                  break;
               }
            }
         }
         catch (SocketException ex)
         {
            Error?.Invoke("Connection failed: " + ex.Message);
         }
         catch (Exception ex)
         {
            if (!m_quit)
               Error?.Invoke("Relay error: " + ex.Message);
         }
         finally
         {
            m_connected = false;
            try { m_stream?.Close(); } catch { }
            try { m_client?.Close(); } catch { }
            m_stream = null;
            m_networkStream = null;
            m_client = null;
            m_thread = null;
            StatusChanged?.Invoke("");
         }
      }

      // ── TLS helper ────────────────────────────────────────────────────

      private Stream WrapTls(NetworkStream networkStream)
      {
         SslStream ssl;
         if (!string.IsNullOrEmpty(m_tlsCertFile))
         {
            var pinnedCert = new X509Certificate2(m_tlsCertFile);
            string thumbprint = pinnedCert.Thumbprint;
            ssl = new SslStream(networkStream, false, (sender, remoteCert, chain, errors) =>
               remoteCert != null &&
               string.Equals(new X509Certificate2(remoteCert).Thumbprint,
                             thumbprint, StringComparison.OrdinalIgnoreCase));
         }
         else
         {
            ssl = new SslStream(networkStream);
         }
         ssl.AuthenticateAsClient(m_host);
         return ssl;
      }

      // ── fields ─────────────────────────────────────────────────────────

      private readonly string m_host;
      private readonly int    m_port;
      private readonly bool   m_tlsEnabled;
      private readonly string m_tlsCertFile;
      private readonly string m_password;
      private readonly bool   m_secondary;
      private readonly ConcurrentQueue<byte[]> m_packetQueue;

      private volatile bool m_quit      = false;
      private volatile bool m_connected = false;

      private TcpClient      m_client;
      private NetworkStream   m_networkStream;
      private Stream          m_stream;
      private Thread          m_thread;
   }
}
