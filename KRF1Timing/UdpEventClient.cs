// Copyright 2018-2020 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace adjsw.F12024
{
    public class UdpEventClientEventArgs
    {
        public UdpEventClientEventArgs(byte[] data)
        {
            this.data = data;
        }

        public byte[] data { get; private set;}
    }

    // receive UDP packets and publish via Event
    public class UdpEventClient : IDisposable
    {
        public UdpEventClient(int port)
        {
            m_ep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);
            m_socket = new UdpClient(port, AddressFamily.InterNetwork);
            m_socket.Client.ReceiveTimeout = 3000; // otherwise the thread will be stuck forever if no new data arrives
            m_udpThread = new Thread(ReceiveThread);
            m_udpThread.Start();
        }

        public delegate void UdpEventClientEventHandler(object sender, UdpEventClientEventArgs e);
        public event UdpEventClientEventHandler ReceiveEvent;

        private void ReceiveThread()
        {
            while (!m_quit)
            {
                try
                {
                    var data = m_socket.Receive(ref m_ep);
                    if (ReceiveEvent != null)
                        ReceiveEvent(this, new UdpEventClientEventArgs(data));
                }
                catch(Exception ex)
                {

                }
            }
        }

        public void Dispose()
        {
            m_quit = true;
            m_udpThread.Join();
        }

        private volatile bool m_quit = false;
        private IPEndPoint m_ep;
        private UdpClient m_socket;
        private Thread m_udpThread;
    }
}
