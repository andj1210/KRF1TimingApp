// Copyright 2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace adjsw.F12025
{
   /// <summary>
   /// Driver-side relay uplink. Connects to the relay server via TCP,
   /// authenticates as a driver, and forwards filtered F1 UDP packets.
   ///
   /// Owns the RelayPacketFilter internally. The main app just calls
   /// FetchPacket() after mapper.Proceed() -- the filter is only ever
   /// touched from the main thread, eliminating data races.
   ///
   /// The background thread handles: connect, auth, receive (NAME_FIX),
   /// and draining the send queue in a paced manner.
   /// </summary>
   public class RelayUplink : IDisposable
   {
      public RelayUplink(RelayConfig config, F1UdpClrMapper mapper)
      {
         m_host        = config.Server;
         m_port        = config.Port;
         m_tlsEnabled  = config.TlsEnabled;
         m_tlsCertFile = config.TlsCertFile;
         m_mapper      = mapper;
         m_filter      = new RelayPacketFilter();
         m_filter.SetUdpMapper(mapper);
      }

      /// <summary>The password assigned by the server after successful connection.</summary>
      public string Password { get; private set; } = "";

      /// <summary>True while connected to the relay server.</summary>
      public bool IsConnected => m_connected;

      /// <summary>Fires when connection state or password changes.</summary>
      public event Action<string> StatusChanged;

      /// <summary>Fires when a NAME_FIX message arrives from the server.</summary>
      public event Action<byte, string> NameFixReceived;

      /// <summary>Fires on unrecoverable errors.</summary>
      public event Action<string> Error;

      /// <summary>
      /// Connect to the relay server on a background thread.
      /// Sends AUTH_DRIVER and waits for password assignment.
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

         while (m_sendQueue.TryDequeue(out _)) { }
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
         m_burstPending = false;
         Password = "";

         // Drain the send queue so stale packets don't linger
         while (m_sendQueue.TryDequeue(out _)) { }

         StatusChanged?.Invoke("");
      }

      /// <summary>
      /// Feed the latest packet from the mapper through the filter and
      /// enqueue it for sending if appropriate.
      /// Called from the main/UI thread after mapper.Proceed().
      ///
      /// The filter is ONLY touched here (main thread), never from the
      /// background thread. When the connection becomes ready and a history
      /// burst is needed, this method detects the pending flag and enqueues
      /// the burst -- keeping everything single-threaded on the filter side.
      /// </summary>
      public void FetchPacket()
      {
         // Check if the background thread signalled that a history burst is needed
         if (m_burstPending)
         {
            m_burstPending = false;
            EnqueueHistoryBurst();
         }

         var filtered = m_filter.ProcessPacket(m_mapper.LastPacketType);
         if (filtered != null && m_connected)
            m_sendQueue.Enqueue(filtered);
      }

      public void Dispose()
      {
         Disconnect();
      }

      // -- background thread ------------------------------------------------

      private void ConnectAndReceiveThread()
      {
         try
         {
            m_client = new TcpClient();
            m_client.NoDelay = true;
            m_client.Connect(m_host, m_port);
            m_networkStream = m_client.GetStream();
            m_stream = m_tlsEnabled ? WrapTls(m_networkStream) : (Stream)m_networkStream;

            //Thread.Sleep(100);
            RelayProtocol.SendHello(m_stream);

            // Send AUTH_DRIVER with empty password (server will assign one)
            byte[] emptyPw = new byte[16];
            RelayProtocol.SendMessage(m_stream, RelayProtocol.MSG_AUTH_DRIVER, emptyPw);

            // Read AUTH_OK
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

            // Read DRIVER_PASSWORD
            if (!RelayProtocol.ReadMessage(m_stream, out byte pwType, out byte[] pwPayload))
            {
               Error?.Invoke("Server closed connection before sending password");
               return;
            }

            if (pwType == RelayProtocol.MSG_DRIVER_PASSWORD && pwPayload != null)
            {
               int len = Array.IndexOf(pwPayload, (byte)0);
               if (len < 0) len = pwPayload.Length;
               Password = Encoding.ASCII.GetString(pwPayload, 0, len);
            }

            m_connected = true;

            // Signal the main thread to enqueue the history burst on next FetchPacket()
            m_burstPending = true;

            StatusChanged?.Invoke("Connected: " + Password);

            // Receive loop: listen for NAME_FIX, send queued packets (paced).
            while (!m_quit)
            {
               if (!m_client.Connected)
               {
                  if (!m_quit)
                     Error?.Invoke("Server disconnected");
                  break;
               }

               // Receive incoming messages (non-blocking check)
               if (m_networkStream.DataAvailable)
               {
                  if (!RelayProtocol.ReadMessage(m_stream, out byte msgType, out byte[] msgPayload))
                  {
                     if (!m_quit)
                        Error?.Invoke("Server disconnected");
                     break;
                  }

                  if (msgType == RelayProtocol.MSG_NAME_FIX && msgPayload != null && msgPayload.Length >= 2)
                  {
                     byte carIndex = msgPayload[0];
                     string name = Encoding.UTF8.GetString(msgPayload, 1, msgPayload.Length - 1);
                     NameFixReceived?.Invoke(carIndex, name);
                  }
                  else if (msgType == RelayProtocol.MSG_AUTH_FAIL)
                  {
                     string reason = msgPayload != null ? Encoding.UTF8.GetString(msgPayload) : "unknown";
                     Error?.Invoke("Server: " + reason);
                     break;
                  }
               }

               // Transmit one queued packet per iteration (paced sending)
               if (m_sendQueue.TryDequeue(out byte[] packet))
               {
                  try
                  {
                     RelayProtocol.SendMessage(m_stream, RelayProtocol.MSG_F1_PACKET, packet);
                     
                  }
                  catch
                  {
                     // Connection lost -- will be detected on next loop iteration
                  }
               }

               // throttle
               Thread.Sleep(20);
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
            m_burstPending = false;
            Password = "";
            try { m_stream?.Close(); } catch { }
            try { m_client?.Close(); } catch { }
            m_stream = null;
            m_networkStream = null;
            m_client = null;
            m_thread = null;
            m_connected = false;
            StatusChanged?.Invoke("");
         }
      }

      // -- TLS helper -------------------------------------------------------

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

      // -- helpers (called from main thread only) ---------------------------

      private void EnqueueHistoryBurst()
      {
         if (!m_filter.HasData)
            return;

         List<byte[]> burst = m_filter.BuildHistoryBurst();
         foreach (var pkt in burst)
            m_sendQueue.Enqueue(pkt);
      }

      // -- fields -----------------------------------------------------------

      private readonly string             m_host;
      private readonly int                m_port;
      private readonly bool               m_tlsEnabled;
      private readonly string             m_tlsCertFile;
      private readonly F1UdpClrMapper     m_mapper;
      private readonly RelayPacketFilter  m_filter;

      private volatile bool m_quit         = false;
      private volatile bool m_connected    = false;
      private volatile bool m_burstPending = false;

      private TcpClient      m_client;
      private NetworkStream   m_networkStream;
      private Stream          m_stream;
      private Thread          m_thread;

      private readonly ConcurrentQueue<byte[]> m_sendQueue = new ConcurrentQueue<byte[]>();
   }
}
