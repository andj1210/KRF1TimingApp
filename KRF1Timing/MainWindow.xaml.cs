// Copyright 2018-2020 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace adjsw.F12026
{
   /// <summary>
   /// Interaktionslogik für MainWindow.xaml
   /// </summary>
   public partial class MainWindow : Window
   {
      public string ip = "";

      enum ViewType
      {
         Board,
         BoardAndCarMap,
         CarMap,
         TrackOnly,
         Count
      }

      public class JsonEntry
      {
         public string SessionInfo { get; set; }
         public string Track { get; set; }
         public int Laps { get; set; } // only for race
         public DriverData[] Drivers { get; set; }
         public string[] DriverTag { get; set; }
      }

      public ConcurrentQueue<byte[]> PacketQue { get { return m_packetQue; } }

      public MainWindow()
      {
         InitializeComponent();

#if DEBUG || RELEASEDEV
         m_devExpander.Visibility = Visibility.Visible;
#else
         m_devExpander.Visibility = Visibility.Collapsed;
#endif

         Title = "KRF1 Timing App for F1-25 w. 2026 Season Pack V" + BuildVersion.Value;

         m_pollTimer.Tick += PollUpdates_Tick;
         m_pollTimer.Interval = TimeSpan.FromMilliseconds(40);
         m_pollTimer.IsEnabled = true;

         m_infoBoxTimer.Tick += m_InfoBoxTimer_Tick;

         m_board.ItemsSource = m_driversList;

         m_mapper = new adjsw.F12026.F1UdpClrMapper();
         m_mapper.InsertTestData();

         // Create relay uplink eagerly if config file is present.
         m_relayConfig = RelayConfig.TryLoad();
         if (m_relayConfig != null)
         {
            m_remoteExpander.Visibility = Visibility.Visible;

            m_relayUplink = new RelayUplink(m_relayConfig, m_mapper);

            m_relayUplink.StatusChanged += status =>
               Dispatcher.BeginInvoke(new Action(() =>
               {
                  if (!string.IsNullOrEmpty(status))
                     ShowInfoBox("Relay: " + status, TimeSpan.FromSeconds(5));
                  if (m_relayUplink != null && !string.IsNullOrEmpty(m_relayUplink.Password))
                     m_SetStatusRow("relay", "RELAY:", m_relayUplink.Password, s_relayStatusBrush);
                  else
                     m_SetStatusRow("relay", null, null, null);
               }));

            m_relayUplink.Error += msg =>
               Dispatcher.BeginInvoke(new Action(() =>
               {
                  m_SetStatusRow("relay", null, null, null);
                  ShowInfoBox("Relay error:\r\n" + msg, TimeSpan.FromSeconds(5));
               }));
         }

         if (!String.IsNullOrEmpty(App.PlaybackFile))
         {
            UdpPlaybackWindow wnd = new UdpPlaybackWindow(App.PlaybackFile, this);
            m_playbackWindow = wnd;
            wnd.Show();
         }
         else
         {
            try
            {
               m_udpClient = new UdpEventClient(20777);
               m_udpClient.ReceiveEvent += OnUdpReceive;
            }
            catch (Exception ex)
            {
               m_udpClient = null;
               MessageBox.Show("UDP Error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error );
            }
         }

         m_trackmap.PitExaggeration = true; // compile-time opt-in: exaggerate pit offset on real track map

         UpdateDriverGrid();
         UpdateCarStatus();
         UpdateTrackmap();

         ShowInfoBox(s_splashText, TimeSpan.FromSeconds(10));

         Loaded += MainWindow_Loaded;
         Closing += MainWindow_Closing;

         m_board.DataGridRightClick += OnGridClick;
         m_driverCtxMenu.NameChosen += OnDriverNameChosen;

         m_recorder.StatusChanged  += s => UpdateRecordingStatus();
         m_recorder.RecordingError += msg => ShowInfoBox("Recording error:\r\n" + msg, TimeSpan.FromSeconds(5));

         m_trackLearner.StatusChanged += msg => ShowInfoBox(msg, TimeSpan.FromSeconds(4));

         m_LoadNameMappings();
      }

      private void MainWindow_Loaded(object sender, RoutedEventArgs e)
      {
         UpdateLayout();
      }

      private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
      {
         if (m_udpClient != null)
            m_udpClient.Dispose();

         if (m_playbackWindow != null)
            m_playbackWindow.Close();

         m_recorder.Dispose();
         m_relayUplink?.Dispose();
         m_relayClient?.Dispose();
      }

      private void ToggleView()
      {
         // toggle sequence:
         int viewInt = (int)m_viewType;
         ++viewInt;
         m_viewType = (ViewType)viewInt;
         if (m_viewType == ViewType.Count)
            m_viewType = ViewType.Board;

         UpdateLayout();
      }

      private void UpdateLayout()
      {
         // we try to make it look about right on 4:3 1280x1024, 1080 & 1440 both in horizontal and vertical mode.

         bool verticalLayout = ActualWidth <= ActualHeight;
         bool dualMode = m_twoDriverMode;

         Canvas.SetTop(m_rootCanvas, 0);
         Canvas.SetLeft(m_rootCanvas, 0);
         m_rootCanvas.Height = ActualHeight;
         m_rootCanvas.Width = ActualWidth;

         // Clear any leftover ScaleTransform from the trackmap
         m_trackmap.RenderTransform = Transform.Identity;

         // Default: hide second car; branches below re-show it in dual mode
         m_carStatus2.Visibility = Visibility.Collapsed;

         switch (m_viewType)
         {
            case ViewType.Board:
               m_board.Visibility = Visibility.Visible;
               m_carStatus.Visibility = Visibility.Collapsed;
               m_trackmap.Visibility = Visibility.Collapsed;

               m_board.MaxHeight = ActualHeight;
               m_board.MaxWidth = ActualWidth;

               break;

            case ViewType.BoardAndCarMap:
               m_board.Visibility    = Visibility.Visible;
               m_carStatus.Visibility = Visibility.Visible;
               m_trackmap.Visibility = Visibility.Visible;

               if (verticalLayout)
               {
                  double y1  = ActualHeight / 2.0;
                  double tmH = ActualHeight - y1;

                  m_board.MaxHeight = y1;
                  m_board.MaxWidth  = ActualWidth;

                  // Trackmap: left half of bottom area
                  m_trackmap.Width  = ActualWidth / 2;
                  m_trackmap.Height = tmH;
                  Canvas.SetTop(m_trackmap,  y1);
                  Canvas.SetLeft(m_trackmap, 0);

                  if (dualMode)
                  {
                     double scale    = Math.Min(Math.Min(ActualWidth / 2.05 / (2 * 365.0), tmH / 500.0), 1.0);
                     double car1Left = ActualWidth / 2;
                     double car1Top  = y1 + 30;
                     Canvas.SetTop(m_carStatus,   car1Top);
                     Canvas.SetLeft(m_carStatus,  car1Left);
                     Canvas.SetTop(m_carStatus2,  car1Top);
                     Canvas.SetLeft(m_carStatus2, car1Left + 362 * scale);
                     m_carStatus2.Visibility = Visibility.Visible;
                     UpdateScaleCarMap(scale);
                  }
                  else
                  {
                     Canvas.SetTop(m_carStatus,  y1 + 60);
                     Canvas.SetLeft(m_carStatus, ActualWidth / 2);
                  }
               }
               else
               {
                  double x1 = dualMode ? ActualWidth * 0.75 : ActualWidth * 0.75;

                  m_board.MaxHeight = ActualHeight;
                  m_board.MaxWidth  = x1;

                  // Trackmap: top half of right panel
                  double tmW = ActualWidth - x1;
                  m_trackmap.Width  = tmW;
                  m_trackmap.Height = ActualHeight / 2;
                  Canvas.SetTop(m_trackmap,  0);
                  Canvas.SetLeft(m_trackmap, x1);

                  if (dualMode)
                  {
                     double scale    = Math.Min(
                        Math.Min(tmW / (2.05 * 365.0), ActualHeight / 2.0 / 500.0), 
                        1.0);

                     double leftBleed  = 100.0 * (1.0 - scale) * (1.0 - scale);
                     double visualSpan = (362.0 + 345.0) * scale;   // car2 offset + rightmost content pixel
                     double car1Left   = x1 - 20.0 + (tmW - visualSpan) / 2.0 + leftBleed - 12.0;
                     double car1Top   = ActualHeight / 2;
                     Canvas.SetTop(m_carStatus,   car1Top);
                     Canvas.SetLeft(m_carStatus,  car1Left);
                     Canvas.SetTop(m_carStatus2,  car1Top);
                     Canvas.SetLeft(m_carStatus2, car1Left + 362 * scale);
                     m_carStatus2.Visibility = Visibility.Visible;
                     UpdateScaleCarMap(scale);
                  }
                  else
                  {
                     Canvas.SetTop(m_carStatus,  ActualHeight / 2);
                     Canvas.SetLeft(m_carStatus, x1 + 10);

                     // for 1280×1024 4:3 screens
                     UpdateScaleCarMap(ActualWidth < 1350 ? 0.75 : 1.0);
                  }
               }
               break;

            case ViewType.CarMap:
               m_board.Visibility     = Visibility.Collapsed;
               m_carStatus.Visibility = Visibility.Visible;
               m_trackmap.Visibility  = Visibility.Visible;

               if (verticalLayout)
               {
                  double tmH = ActualHeight / 2;
                  m_trackmap.Width  = ActualWidth;
                  m_trackmap.Height = tmH;
                  Canvas.SetTop(m_trackmap,  0);
                  Canvas.SetLeft(m_trackmap, 0);

                  if (dualMode)
                  {
                     double scale    = Math.Min(Math.Min(ActualWidth / (2 * 365.0), tmH / 500.0), 1.25);
                     double totalW   = 2 * 365 * scale;
                     double car1Left = (ActualWidth - totalW) / 2;
                     double car1Top  = tmH + 30;
                     Canvas.SetTop(m_carStatus,   car1Top);
                     Canvas.SetLeft(m_carStatus,  car1Left);
                     Canvas.SetTop(m_carStatus2,  car1Top);
                     Canvas.SetLeft(m_carStatus2, car1Left + 365 * scale);
                     m_carStatus2.Visibility = Visibility.Visible;
                     UpdateScaleCarMap(scale);
                  }
                  else
                  {
                     Canvas.SetTop(m_carStatus,  tmH + 30);
                     Canvas.SetLeft(m_carStatus, (ActualWidth - 365) / 2);
                     UpdateScaleCarMap(1.25);
                  }
               }
               else
               {
                  double tmW = ActualWidth / 2;
                  m_trackmap.Width  = tmW;
                  m_trackmap.Height = ActualHeight;
                  Canvas.SetTop(m_trackmap,  0);
                  Canvas.SetLeft(m_trackmap, 0);

                  if (dualMode)
                  {
                     double scale     = Math.Min(Math.Min(tmW / (2.05 * 365.0), ActualHeight / 500.0), 1.5);
                     double leftBleed  = 100.0 * (1.0 - scale) * (1.0 - scale);
                     double visualSpan = (365.0 + 345.0) * scale;   // car2 offset + rightmost content pixel
                     double car1Left   = ActualWidth / 2.0 - 20.0 + (tmW - visualSpan) / 2.0 + leftBleed - 12.0;
                     double car1Top   = (ActualHeight - 500 * scale) / 2;
                     Canvas.SetTop(m_carStatus,   car1Top);
                     Canvas.SetLeft(m_carStatus,  car1Left);
                     Canvas.SetTop(m_carStatus2,  car1Top);
                     Canvas.SetLeft(m_carStatus2, car1Left + 365 * scale);
                     m_carStatus2.Visibility = Visibility.Visible;
                     UpdateScaleCarMap(scale);
                  }
                  else
                  {
                     Canvas.SetTop(m_carStatus,  150);
                     Canvas.SetLeft(m_carStatus, ActualWidth / 2);
                     UpdateScaleCarMap(ActualWidth > 1920 ? 2.0 : 1.5);
                  }
               }
               break;

            case ViewType.TrackOnly:
               m_board.Visibility     = Visibility.Collapsed;
               m_carStatus.Visibility = Visibility.Collapsed;
               m_trackmap.Visibility  = Visibility.Visible;

               // Reset the car-map transform so it does not interfere
               UpdateScaleCarMap(1.0);

               // Give the trackmap the full window area (minus small border)
               {
                  const double border = 20;
                  m_trackmap.Width  = Math.Max(100, ActualWidth  - border * 2);
                  m_trackmap.Height = Math.Max(100, ActualHeight - border * 2);
                  Canvas.SetLeft(m_trackmap, border);
                  Canvas.SetTop (m_trackmap, border);
               }
               break;

            default:
               break;
         }
      }

      private void UpdateScaleCarMap(double scale)
      {
         m_ApplyCarStatusScale(m_carStatus, scale);
         m_ApplyCarStatusScale(m_carStatus2, scale);
      }

      private void m_ApplyCarStatusScale(CarStatusView view, double scale)
      {
         var transform = view.RenderTransform as ScaleTransform;
         if (transform == null)
         {
            transform = new ScaleTransform();
            view.RenderTransform = transform;
         }

         transform.ScaleX = scale;
         transform.ScaleY = scale;
         transform.CenterX = -100 + 100 * scale;
      }

      private void UpdateDriverGrid()
      {
         if (m_driversList.Count != m_mapper.CountDrivers)
         {
            m_driversList.Clear();
            for (int i = 0; i < m_mapper.CountDrivers; i++)
            {
               m_driversList.Add(m_mapper.Drivers[i]);
            }

            m_board.ItemsSource = null;
            m_board.ItemsSource = m_driversList;

            if (m_board.TheDataGrid.SelectedItem != null)
            {
               m_board.TheDataGrid.SelectedItem = null; // avoid bluemarking from user for a selected row which cannot get removed afterwards
            }
         }

         foreach (var driver in m_mapper.Drivers)
         {
            if ((driver.Pos > 0) && (driver.Pos <= 22))
            {
               if ((driver.Pos - 1) < m_driversList.Count)
                  m_driversList[driver.Pos - 1] = driver;
            }
         }
      }

      private void UpdateCarStatus()
      {
         bool hasSecondary = false;
         foreach (var driver in m_mapper.Drivers)
         {
            if (driver.IsPlayer || driver.IsMainDriver)
               m_FillCarStatus(m_carStatus, driver);

            else if (driver.IsSecondaryDriver)
            {
               m_FillCarStatus(m_carStatus2, driver);
               hasSecondary = true;
            }
         }
         m_twoDriverMode = hasSecondary;
      }

      private void m_FillCarStatus(CarStatusView view, DriverData driver)
      {
         view.txt_tyre_fl.Text = "" + driver.WearDetail.WearFrontLeft;
         view.txt_tyre_fl.Background = DamageToToColor(driver.WearDetail.WearFrontLeft);

         view.txt_tyre_fr.Text = "" + driver.WearDetail.WearFrontRight;
         view.txt_tyre_fr.Background = DamageToToColor(driver.WearDetail.WearFrontRight);

         view.txt_tyre_rl.Text = "" + driver.WearDetail.WearRearLeft;
         view.txt_tyre_rl.Background = DamageToToColor(driver.WearDetail.WearRearLeft);

         view.txt_tyre_rr.Text = "" + driver.WearDetail.WearRearRight;
         view.txt_tyre_rr.Background = DamageToToColor(driver.WearDetail.WearRearRight);

         view.txt_wing_fl.Text = "" + driver.WearDetail.DamageFrontLeft;
         view.txt_wing_fl.Background = DamageToToColor(driver.WearDetail.DamageFrontLeft);

         view.txt_wing_fr.Text = "" + driver.WearDetail.DamageFrontRight;
         view.txt_wing_fr.Background = DamageToToColor(driver.WearDetail.DamageFrontRight);

         view.txt_driver_name.Text = driver.Name;
         view.tyre_compound.Update(driver.VisualTyre);
         view.SetCarImage(driver.Team);

         view.txt_temp_fl_inner.Text = "" + driver.WearDetail.TempFrontLeftInner + "°C";
         view.txt_temp_fl_inner.Background = TyreToColor(driver.Tyre, driver.WearDetail.TempFrontLeftInner);
         view.txt_temp_fl_surface.Text = "" + driver.WearDetail.TempFrontLeftOuter + "°C";
         view.txt_temp_fl_surface.Background = TyreToColor(driver.Tyre, driver.WearDetail.TempFrontLeftOuter);
         view.txt_temp_fr_inner.Text = "" + driver.WearDetail.TempFrontRightInner + "°C";
         view.txt_temp_fr_inner.Background = TyreToColor(driver.Tyre, driver.WearDetail.TempFrontRightInner);
         view.txt_temp_fr_surface.Text = "" + driver.WearDetail.TempFrontRightOuter + "°C";
         view.txt_temp_fr_surface.Background = TyreToColor(driver.Tyre, driver.WearDetail.TempFrontRightOuter);

         view.txt_temp_rl_inner.Text = "" + driver.WearDetail.TempRearLeftInner + "°C";
         view.txt_temp_rl_inner.Background = TyreToColor(driver.Tyre, driver.WearDetail.TempRearLeftInner);
         view.txt_temp_rl_surface.Text = "" + driver.WearDetail.TempRearLeftOuter + "°C";
         view.txt_temp_rl_surface.Background = TyreToColor(driver.Tyre, driver.WearDetail.TempRearLeftOuter);
         view.txt_temp_rr_inner.Text = "" + driver.WearDetail.TempRearRightInner + "°C";
         view.txt_temp_rr_inner.Background = TyreToColor(driver.Tyre, driver.WearDetail.TempRearRightInner);
         view.txt_temp_rr_surface.Text = "" + driver.WearDetail.TempRearRightOuter + "°C";
         view.txt_temp_rr_surface.Background = TyreToColor(driver.Tyre, driver.WearDetail.TempRearRightOuter);

         view.txt_temp_engine.Text = "" + driver.WearDetail.TempEngine + "°C";
         view.txt_temp_engine.Background = EngineToColor(driver.WearDetail.TempEngine);
      }

      private void UpdateTrackmap(bool motionUpdate = false)
      {         
         m_trackmap.ActiveTrack = m_mapper.SessionInfo.EventTrack; // switch between circle and real layout
         m_trackmap.Update(m_mapper.Drivers, m_board.DriverUnderMouse as DriverData, motionUpdate);
         
         if (m_trackLearner.IsActive && (m_mapper.Drivers != null))
         {
            // Feed the track learner (uses the first present driver - typically the only one in TT)
            foreach (var d in m_mapper.Drivers)
            {
               if (d.Present)
               {
                  m_trackLearner.Update(d,
                     m_mapper.SessionInfo.EventTrack,
                     m_mapper.SessionInfo.EventTrack.ToString("g"));
                  break;
               }
            }
         }
      }

      public Color ColorFromHSV(double hue, double saturation, double value)
      {
         int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
         double f = hue / 60 - Math.Floor(hue / 60);

         value = value * 255;
         byte v = (byte)Convert.ToInt32(value);
         byte p = (byte)Convert.ToInt32(value * (1 - saturation));
         byte q = (byte)Convert.ToInt32(value * (1 - f * saturation));
         byte t = (byte)Convert.ToInt32(value * (1 - (1 - f) * saturation));

         if (hi == 0)
            return Color.FromArgb(255, v, t, p);
         else if (hi == 1)
            return Color.FromArgb(255, q, v, p);
         else if (hi == 2)
            return Color.FromArgb(255, p, v, t);
         else if (hi == 3)
            return Color.FromArgb(255, p, q, v);
         else if (hi == 4)
            return Color.FromArgb(255, t, p, v);
         else
            return Color.FromArgb(255, v, p, q);
      }

      private SolidColorBrush DamageToToColor(int damageInt)
      {
         if (damageInt < 0)
            damageInt = 0;

         if (damageInt > 100)
            damageInt = 100;

         float damage = damageInt / 100.0f;

         // map ~60% to full red already
         damage *= 1.7f;
         if (damage > 1.0f)
            damage = 1.0f;

         damage = 1.0f - damage;
         return new SolidColorBrush(ColorFromHSV(damage * 120, 1, 1)); // 0° ... 120°
      }

      private SolidColorBrush EngineToColor(int temp)
      {
         if (temp < 110)
            return new SolidColorBrush(
                ColorFromHSV(
                    SkalarToHueIterp(80, 110, 240, 120, temp)
                    , 1, 1)
                );

         else if (temp < 120)
            return new SolidColorBrush(ColorFromHSV(120, 1, 1));

         else
            return new SolidColorBrush(
                ColorFromHSV(
                    SkalarToHueIterp(120, 150, 120, 0, temp)
                    , 1, 1)
                );
      }

      private SolidColorBrush TyreToColor(F1Tyre tyre, int temp)
      {
         // ignore tyre for now
         if (temp < 75)
            return new SolidColorBrush(
                ColorFromHSV(
                    SkalarToHueIterp(60, 75, 240, 150, temp)
                    , 1, 1)
                );

         else
            return new SolidColorBrush(
                ColorFromHSV(
                    SkalarToHueIterp(75, 115, 150, 0, temp)
                    , 1, 1)
                    );
      }

      private double SkalarToHueIterp(int min, int max, double hueMin, double hueMax, int actualValue)
      {
         if (actualValue < min)
            actualValue = min;

         if (actualValue > max)
            actualValue = max;

         int interval = max - min;


         if (hueMax > hueMin)
         {
            double hueInterval = hueMax - hueMin;
            return hueMax - hueInterval * (actualValue - min) / (float)interval;
         }
         else
         {
            double hueInterval = hueMin - hueMax;
            return hueMin - hueInterval * (actualValue - min) / (float)interval;
         }
      }

      private void ActionSaveReportImpl()
      {
         string txtPath = ReportWriter.SaveReport(m_mapper, Title);
         if (txtPath != null)
            ShowInfoBox(txtPath + "\r\nThe race report has been saved.", TimeSpan.FromSeconds(3));
         else
            ShowInfoBox("Event Report not saved - no data!", TimeSpan.FromSeconds(3));

         ReportWriter.SaveReportJson(m_mapper);
      }

      private void ShowInfoBox(string text, TimeSpan autoCloseTime)
      {
         m_infoBoxTimer.Stop();
         m_infoTxt.Text = text;
         m_infoBox.Visibility = Visibility.Visible;
         m_infoBoxTimer.Interval = autoCloseTime;
         m_infoBoxTimer.Start();
      }

      private void m_infoBox_MouseDown(object sender, MouseButtonEventArgs e)
      {
         m_infoBoxTimer.Stop();
         m_infoBox.Visibility = Visibility.Collapsed;
      }

      private void m_InfoBoxTimer_Tick(object sender, EventArgs e)
      {
         m_infoBoxTimer.Stop();
         m_imgInfo.Visibility = Visibility.Collapsed;
         m_infoBox.Visibility = Visibility.Collapsed;
      }

      private void m_LoadNameMappings()
      {
         try
         {
            var json = File.ReadAllText("namemappingsdyn.json");
            m_nameMappingsDynamic = Newtonsoft.Json.JsonConvert.DeserializeObject<DriverNameDynamicMappings>(json) as DriverNameDynamicMappings;
         }
         catch
         {
            // non-existence of the dynamic mapping file is not an error
            m_nameMappingsDynamic = new DriverNameDynamicMappings();
         }
      }

      private void PollUpdates_Tick(object sender, EventArgs e)
      {
         bool updated = false;
         bool motionUpdate = false;
         byte[] newData;
         while (m_packetQue.TryDequeue(out newData))
         {
            // 1. Parse - (session UID is updated inside the mapper)
            updated |= m_mapper.Proceed(newData);
            motionUpdate |= (int)m_mapper.LastPacketType == 0;

            // 2. Check for session change AFTER parsing so the new UID is already
            //    committed. If changed, the recorder opens a new file NOW ...
            ulong currentUID = m_mapper.SessionUID;
            if ((currentUID != 0) && (currentUID != m_lastSessionUID))
            {
               m_lastSessionUID = currentUID;
               m_recorder.NotifySessionChanged(currentUID);
               m_trackLearner.NotifySessionChanged();
            }

            // 3. ... so this packet (the one that triggered the change) lands
            //    in the new file, not the old one.
            m_recorder.WritePacket(newData);

            // 4. Relay uplink: feed the filter (queues for sending if connected)
            m_relayUplink?.FetchPacket();
         }

         // Drain secondary engineer queue — feeds only CarDamage/CarStatus into the
         // mapper for the second driver's car index via ProceedSecondary.
         byte[] secData;
         while (m_packetQueSecondary.TryDequeue(out secData))
         {
            updated |= m_mapper.ProceedSecondary(secData);
         }

         UpdateTrackmap(motionUpdate); // trackmap might be in interpolation mode, therefore we might want to re render even if there is no new data...

         if (!updated)
            return;

         m_board.SessionSource = m_mapper.SessionInfo;
         UpdateDriverGrid();
         UpdateCarStatus();
         UpdateLayout();

         if (m_mapper.SessionInfo.Session == SessionType.Race ||
            m_mapper.SessionInfo.Session == SessionType.Race2 ||
            m_mapper.SessionInfo.Session == SessionType.Race3
            )
         {
            if (m_mapper.Classification != null)
            {
               if (!m_sessionClassificationHandled)
               {
                  if (!m_autosave)
                     ShowInfoBox("The Race has finished.\r\n Click in the window and hit\r\n---\"s\"---\r\nto save the race report.", TimeSpan.FromSeconds(10));
                  else
                  {
                     ActionSaveReportImpl();
                  }
                  m_sessionClassificationHandled = true;
               }
            }
            else
            {
               m_sessionClassificationHandled = false;
            }
         }

         bool qualySession = false;
         switch (m_mapper.SessionInfo.Session)
         {
            case SessionType.P1:
            case SessionType.P2:
            case SessionType.P3:
            case SessionType.ShortPractice:
            case SessionType.Q1:
            case SessionType.Q2:
            case SessionType.Q3:
            case SessionType.SprintShootout1:
            case SessionType.SprintShootout2:
            case SessionType.SprintShootout3:
            case SessionType.ShortQ:
            case SessionType.ShortSprintShootout:
               qualySession = true;
               break;

            default:
               qualySession = false;
               break;
         }
         m_board.Quali = qualySession;

         if (m_mapper.UdpAction[0])
         {
            m_mapper.UdpAction[0] = false;

            if (m_udpClient != null)
            {
               // accept button input only in live mode...
               ToggleView();
            }
         }
      }

      private void OnKeyDown(object sender, KeyEventArgs e)
      {
         if (e.Key == Key.F11)    ActionToggleFullscreen();
         if (e.Key == Key.S)      ActionSaveReport();
         if (e.Key == Key.L)      ActionToggleLeader();
         if (e.Key == Key.D)      ActionToggleStatus();
         if (e.Key == Key.Space)  ToggleView();

#if DEBUG || RELEASEDEV
         if (e.Key == Key.R) ActionToggleRecording();
         if (e.Key == Key.T) ActionToggleTrackLearning();
#endif

         if (m_relayConfig != null)
         {
            if (e.Key == Key.U) ActionToggleRelayUplink();
            if (e.Key == Key.I) ActionToggleRelayEngineer();
            if (e.Key == Key.O) ActionToggleSecondaryEngineer();
         }
      }

      private void ActionToggleFullscreen()
      {
         if (WindowStyle == WindowStyle.None)
         {
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Normal;
         }
         else
         {
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
         }
      }

      private void ActionSaveReport() => ActionSaveReportImpl();

      private void ActionToggleRecording()
      {
         m_recorder.Toggle();
         if (m_recorder.IsRecording)
            ShowInfoBox("UDP Recording started.\r\nFiles -> recordings/<sessionId>.pkl", TimeSpan.FromSeconds(3));
         else
            ShowInfoBox("UDP Recording stopped.", TimeSpan.FromSeconds(2));
      }

      private void ActionToggleTrackLearning()
      {
         m_trackLearner.Toggle(
            m_mapper.SessionInfo.EventTrack,
            m_mapper.SessionInfo.EventTrack.ToString("g"));
      }

      /// <summary>
      /// Adds, updates, or removes a named row in the status overlay (bottom-right corner).
      /// Pass a null/empty value to remove the row. The overlay border hides itself
      /// automatically when no rows remain.
      /// </summary>
      private void m_SetStatusRow(string key, string label, string value, Brush valueBrush)
      {
         if (string.IsNullOrEmpty(value))
         {
            if (m_statusRowLookup.TryGetValue(key, out var old))
            {
               m_statusRowsPanel.Children.Remove(old.Row);
               m_statusRowLookup.Remove(key);
            }
         }
         else if (m_statusRowLookup.TryGetValue(key, out var entry))
         {
            entry.ValueBlock.Text       = value;
            entry.ValueBlock.Foreground = valueBrush;
         }
         else
         {
            var labelBlock = new TextBlock
            {
               Text              = label + " ",
               FontFamily        = new FontFamily("Courier New"),
               FontSize          = 14,
               Foreground        = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
               VerticalAlignment = VerticalAlignment.Center
            };
            var valueBlock = new TextBlock
            {
               Text              = value,
               FontFamily        = new FontFamily("Courier New"),
               FontSize          = 14,
               FontWeight        = FontWeights.Bold,
               Foreground        = valueBrush,
               VerticalAlignment = VerticalAlignment.Center
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(labelBlock);
            row.Children.Add(valueBlock);
            m_statusRowsPanel.Children.Add(row);
            m_statusRowLookup[key] = (row, valueBlock);
         }

         m_statusOverlay.Visibility = m_statusRowLookup.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
      }

      private void ActionToggleRelayUplink()
      {
         if (m_relayUplink == null)
         {
            ShowInfoBox("Relay not available.\r\nPlace relay_config.json in app folder.", TimeSpan.FromSeconds(4));
            return;
         }

         if (m_relayUplink.IsConnected)
         {
            var result = System.Windows.MessageBox.Show(
               "Do you want to stop sharing your Telemetry?",
               "Disconnect from " + m_relayConfig.Server,
               MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
               return;

            m_relayUplink.Disconnect();
            m_SetStatusRow("relay", null, null, null);
            ShowInfoBox("Relay sharing stopped.", TimeSpan.FromSeconds(2));
            return;
         }
         else
         {
            if (m_relayClient != null && m_relayClient.IsConnected)
            {
               ShowInfoBox("Cannot share while connected as Race Engineer.", TimeSpan.FromSeconds(4));
               return;
            }

            var result = System.Windows.MessageBox.Show(
               "Do you want to share your Telemetry?",
               "Connect to " + m_relayConfig.Server,
               MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
               return;

            m_relayUplink.Connect();
            ShowInfoBox("Connecting to relay server...", TimeSpan.FromSeconds(3));
         }
      }

      private void ActionToggleRelayEngineer()
      {
         // Disconnect if already connected (also drops the secondary link)
         if ((m_relayClient != null) && m_relayClient.IsConnected)
         {
            var result = System.Windows.MessageBox.Show(
               "Do you want to disconnect from Second Driver?",
               "Disconnect from " + m_relayConfig.Server,
               MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
               return;

            m_relayClient.Disconnect();
            m_relayClient = null;
            m_trackmap.InterpolationEnabled = false;
            m_DisconnectSecondaryEngineer();
            m_mapper.Mode = MapperMode.Direct;
            ShowInfoBox("Engineer relay disconnected.", TimeSpan.FromSeconds(2));
            return;
         }

         if ((m_relayUplink != null) && m_relayUplink.IsConnected)
         {
            ShowInfoBox("Cannot connect as Race Engineer while sharing Telemetry.", TimeSpan.FromSeconds(4));
            return;
         }

         if (m_relayConfig == null)
         {
            m_relayConfig = RelayConfig.TryLoad();
            if (m_relayConfig == null)
            {
               ShowInfoBox("Relay not available.\r\nPlace relay_config.json in app folder.", TimeSpan.FromSeconds(4));
               return;
            }
         }

         // Prompt for password
         string password = Microsoft.VisualBasic.Interaction.InputBox(
            "Enter the driver's relay password:",
            "Engineer Relay Connect",
            "");

         if (string.IsNullOrWhiteSpace(password))
            return;

         m_relayClient = new RelayClient(m_relayConfig, password.Trim(), m_packetQue);

         m_relayClient.StatusChanged += status =>
            Dispatcher.BeginInvoke(new Action(() =>
            {
               if (!string.IsNullOrEmpty(status))
               {
                  ShowInfoBox("Engineer: " + status, TimeSpan.FromSeconds(4));
                  m_SetStatusRow("engineer", "ENGINEER:", status, s_engineerStatusBrush);
               }
               else
               {
                  m_SetStatusRow("engineer", null, null, null);
               }
            }));

         m_relayClient.Error += msg =>
            Dispatcher.BeginInvoke(new Action(() =>
               ShowInfoBox("Engineer error:\r\n" + msg, TimeSpan.FromSeconds(5))));

         m_relayClient.Connect();
         m_mapper.Mode = MapperMode.Engineer1;
         ShowInfoBox("Connecting as engineer...", TimeSpan.FromSeconds(3));
         m_trackmap.InterpolationEnabled = true;
      }

      private void ActionToggleSecondaryEngineer()
      {
         // Disconnect if already connected
         if (m_relayClientSecondary != null && m_relayClientSecondary.IsConnected)
         {
            var result = System.Windows.MessageBox.Show(
               "Do you want to disconnect from Second Driver?",
               "Disconnect from " + m_relayConfig.Server,
               MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
               return;

            m_DisconnectSecondaryEngineer();
            ShowInfoBox("Second driver disconnected.", TimeSpan.FromSeconds(2));
            return;
         }

         // Require a live primary engineer connection first
         if (m_relayClient == null || !m_relayClient.IsConnected)
         {
            ShowInfoBox("Connect as Race Engineer first.", TimeSpan.FromSeconds(3));
            return;
         }

         if (m_relayConfig == null)
         {
            ShowInfoBox("Relay not available.\r\nPlace relay_config.json in app folder.", TimeSpan.FromSeconds(4));
            return;
         }

         string password = Microsoft.VisualBasic.Interaction.InputBox(
            "Enter the second driver's relay password:",
            "Second Driver Connect",
            "");

         if (string.IsNullOrWhiteSpace(password))
            return;

         m_relayClientSecondary = new RelayClient(
            m_relayConfig, password.Trim(),
            m_packetQueSecondary, secondary: true);

         m_relayClientSecondary.StatusChanged += status =>
            Dispatcher.BeginInvoke(new Action(() =>
            {
               if (!string.IsNullOrEmpty(status))
               {
                  ShowInfoBox("Second driver: " + status, TimeSpan.FromSeconds(4));
                  m_SetStatusRow("engineer2", "ENGINEER 2:", status, s_engineer2StatusBrush);
               }
               else
               {
                  m_SetStatusRow("engineer2", null, null, null);
               }
            }));

         m_relayClientSecondary.Error += msg =>
            Dispatcher.BeginInvoke(new Action(() =>
            {
               m_DisconnectSecondaryEngineer();
               ShowInfoBox("Second driver error:\r\n" + msg, TimeSpan.FromSeconds(5));
            }));

         m_relayClientSecondary.Connect();
         m_mapper.Mode = MapperMode.Engineer2;
         ShowInfoBox("Connecting to second driver...", TimeSpan.FromSeconds(3));
      }

      private void m_DisconnectSecondaryEngineer()
      {
         if (m_relayClientSecondary != null)
         {
            m_relayClientSecondary.Disconnect();
            m_relayClientSecondary = null;
         }
         m_mapper.SecondaryDriverIndex = -1;
         foreach (var driver in m_mapper.Drivers)
            driver.IsSecondaryDriver = false;
         m_mapper.Mode = MapperMode.Engineer1;   // primary link still active; caller sets Direct if not
         m_SetStatusRow("engineer2", null, null, null);
      }

      private void ActionToggleLeader()
      {
         m_board.LeaderDeltaMode = !m_board.LeaderDeltaMode;
      }

      private void ActionToggleStatus()
      {
         m_board.StatusVisible = !m_board.StatusVisible;
      }

      private void OnSidebarHotspot_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
      {
         m_sidebar.Visibility = Visibility.Visible;
      }

      private void OnSidebar_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
      {
         m_sidebar.Visibility = Visibility.Collapsed;
      }

      private void OnSidebar_ToggleView(object sender, RoutedEventArgs e)           => ToggleView();
      private void OnSidebar_ToggleLeader(object sender, RoutedEventArgs e)         => ActionToggleLeader();
      private void OnSidebar_ToggleStatus(object sender, RoutedEventArgs e)         => ActionToggleStatus();
      private void OnSidebar_SaveReport(object sender, RoutedEventArgs e)           => ActionSaveReport();
      private void OnSidebar_ToggleFullscreen(object sender, RoutedEventArgs e)     => ActionToggleFullscreen();
      private void OnSidebar_ToggleRecording(object sender, RoutedEventArgs e)      => ActionToggleRecording();
      private void OnSidebar_ToggleTrackLearning(object sender, RoutedEventArgs e)  => ActionToggleTrackLearning();
      private void OnSidebar_ToggleRelayUplink(object sender, RoutedEventArgs e)       => ActionToggleRelayUplink();
      private void OnSidebar_ToggleRelayEngineer(object sender, RoutedEventArgs e)     => ActionToggleRelayEngineer();
      private void OnSidebar_ToggleSecondaryEngineer(object sender, RoutedEventArgs e) => ActionToggleSecondaryEngineer();
      private void OnSidebar_CheckUpdate(object sender, RoutedEventArgs e)             => ActionCheckUpdate();

      private async void ActionCheckUpdate()
      {
         m_sidebar.Visibility = Visibility.Collapsed;

         var updater = new UpdateService();
         try
         {
            UpdateService.ReleaseInfo rel = await updater.CheckAsync();

            if (!updater.IsNewer(rel))
            {
               ShowInfoBox("You are up to date (v" + updater.LocalVersion + ").", TimeSpan.FromSeconds(4));
               return;
            }

            MessageBoxResult answer = MessageBox.Show(
               "A new version is available.\n\n" +
               "Installed: " + updater.LocalVersion + "\n" +
               "Latest:    " + rel.Tag + "\n\n" +
               "Download and restart now?",
               "Update available", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer == MessageBoxResult.Yes)
               await updater.DownloadAndApplyAsync(rel); // extracts, hands off to the batch, shuts down
         }
         catch (Exception ex)
         {
            MessageBox.Show("Update check failed:\n\n" + ex.Message,
               "Update", MessageBoxButton.OK, MessageBoxImage.Warning);
         }
      }

      private void OnUdpReceive(object sender, UdpEventClientEventArgs e)
      {
         m_packetQue.Enqueue(e.data);
      }

      private void UpdateRecordingStatus()
      {
         const string baseTitle = "KRF1 Timing App for F1-25 V0.91.0";
         if (m_recorder.IsRecording)
            Title = baseTitle + "  ● REC";
         else
            Title = baseTitle;
      }

      private void OnGridClick(object sender, MouseButtonEventArgs e)
      {
         DriverData driver = m_board.DriverUnderMouse as DriverData;
         if (driver != null)
         {
            m_driverCtxMenu.Show(driver, m_nameMappingsDynamic);
         }
      }

      private void OnDriverNameChosen(DriverData driver, string newName)
      {
         SetNewNameToReferencedDriver(driver, newName);
         m_nameMappingsDynamic.Add(driver.DriverNr, newName);
         StoreDynamicMappings();
      }

      private void StoreDynamicMappings()
      {
         try
         {
            string filename = "namemappingsdyn.json";
            var jsonText = Newtonsoft.Json.JsonConvert.SerializeObject(m_nameMappingsDynamic, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(filename, jsonText);
         }

         catch(Exception ex)
         {
            ShowInfoBox("Error storing dynamic mappings: \"namemappingsdyn.json\":\r\n" + ex.Message, TimeSpan.FromSeconds(3));
         }
      }

      private void SetNewNameToReferencedDriver(DriverData driver, string newName)
      {
         driver.NameOverride = newName;
      }

      private UdpEventClient m_udpClient = null;
      private UdpPlaybackWindow m_playbackWindow = null;
      private UdpSessionRecorder m_recorder = new UdpSessionRecorder();
      private ulong m_lastSessionUID = 0;


      // Relay
      private bool m_twoDriverMode;
      private RelayConfig  m_relayConfig          = null;
      private RelayUplink  m_relayUplink           = null;
      private RelayClient  m_relayClient           = null;
      private RelayClient  m_relayClientSecondary  = null;
      private ConcurrentQueue<byte[]> m_packetQueSecondary = new ConcurrentQueue<byte[]>();

      // Status overlay
      private Dictionary<string, (StackPanel Row, TextBlock ValueBlock)> m_statusRowLookup
         = new Dictionary<string, (StackPanel, TextBlock)>();
      private static readonly Brush s_relayStatusBrush     = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0x44));
      private static readonly Brush s_engineerStatusBrush  = new SolidColorBrush(Color.FromRgb(0x44, 0xDD, 0xAA));
      private static readonly Brush s_engineer2StatusBrush = new SolidColorBrush(Color.FromRgb(0x44, 0xCC, 0xEE));
      private TrackLearner m_trackLearner = new TrackLearner();
      private ConcurrentQueue<byte[]> m_packetQue = new ConcurrentQueue<byte[]>();
      private F1UdpClrMapper m_mapper = null;
      private DispatcherTimer m_pollTimer = new DispatcherTimer(DispatcherPriority.Render);
      private DispatcherTimer m_infoBoxTimer = new DispatcherTimer();
      private ObservableCollection<adjsw.F12026.DriverData> m_driversList = new ObservableCollection<adjsw.F12026.DriverData>();
      private CollectionViewSource m_driverListViewSource = new CollectionViewSource();
      private bool m_sessionClassificationHandled = false;
      private DriverNameDynamicMappings m_nameMappingsDynamic;
      private bool m_autosave = true;
      private DriverNameContextMenu m_driverCtxMenu = new DriverNameContextMenu();
      private ViewType m_viewType = ViewType.BoardAndCarMap; // on startup will toggle to next view, which will be board only


      private static string s_splashText =
@"
KRF1 Timing App for F1-25 - 2026 Season Pack
Copyright 2018-2026 Andreas Jung

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, version 3.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.

--- For license details refer to the LICENSE.md file in the program folder ---
";
   }
}
