using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace F1GameSessionDisplay
{
   /// <summary>
   /// Interaktionslogik für UdpPlaybackWindow.xaml
   /// </summary>
   public partial class UdpPlaybackWindow : Window, INotifyPropertyChanged
   {
      private int m_idx = 0;
      private bool m_play = true;
      private UdpPlaybackData.TimedUpdPacket[] m_data; 
      private DispatcherTimer m_timer;
      private uint m_speed = 1;
      private MainWindow m_wnd;
      UInt64 m_ts = 0;

      

      public bool Play 
      { 
         get { return m_play; } 
         set { m_play = value; NPC(); } 
      }

      public event PropertyChangedEventHandler PropertyChanged;

      public UdpPlaybackWindow(string filename, MainWindow mw)
      {
         InitializeComponent();

         m_wnd = mw;

         var pbd = new UdpPlaybackData(filename);
         m_data = pbd.GetPackets();
         m_timer = new DispatcherTimer(DispatcherPriority.Send);
         m_timer.Interval = TimeSpan.FromMilliseconds(50);
         m_timer.Tick += M_timer_Tick; ;
         m_timer.Start();

         m_pbar.Minimum = 0;
         m_pbar.Maximum = m_data.Length + 1;
         DataContext = this;
      }

      private void M_timer_Tick(object sender, EventArgs e)
      {
         if ((m_idx != m_data.Length) && Play)
            m_ts += (UInt64)m_timer.Interval.TotalMilliseconds * m_speed;

         while (m_idx != m_data.Length)
         {
            if (m_data[m_idx].timestamp < m_ts)
            {
               m_wnd.PacketQue.Enqueue(m_data[m_idx].data);
               ++m_idx;
            }
            else if ((m_data[m_idx].timestamp) > (m_ts + 3000))
            {
               // if for prolonged time no packets, force to send the next packet
               m_ts = m_data[m_idx].timestamp;
            }

            else
               break;
         }

         m_pbar.Value = m_idx;
         m_tbFrame.Text = "" + m_idx;

         int seconds = (int) m_ts / 1000;
         int tenth = (int)m_ts % 1000 / 100;
         int minutes = seconds / 60;
         seconds %=60;

         if (seconds < 10)
            m_lblTime.Content = "" + minutes + ":0" + seconds + "." + tenth;
         else
            m_lblTime.Content = "" + minutes + ":" + seconds + "." + tenth;
      }

      private void Button_Reset_Click(object sender, RoutedEventArgs e)
      {
         m_idx = 0;
         m_ts = 0;
      }
      private void Button_Speedm_Click(object sender, RoutedEventArgs e)
      {
         if (m_speed >= 2)
            m_speed /= 2;

         m_btnPlay.Content = "Play (" + m_speed + "x)";
      }
      private void Button_Speedp_Click(object sender, RoutedEventArgs e)
      {
         if (m_speed < 100)
            m_speed *= 2;

         m_btnPlay.Content = "Play (" + m_speed + "x)";
      }

      private void NPC([CallerMemberName] string propertyName = "")
      {
         PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
      }

   }
}
