// Copyright 2018-2020 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace adjsw.F12025
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


      public ConcurrentQueue<byte[]> PacketQue
      {
         get { return m_packetQue; }
      }

      public MainWindow()
      {
         InitializeComponent();

         Title = "KRF1 Timing App for F1-25 V0.9.0";

         m_pollTimer.Tick += PollUpdates_Tick;
         m_pollTimer.Interval = TimeSpan.FromMilliseconds(40);
         m_pollTimer.IsEnabled = true;

         m_infoBoxTimer.Tick += m_InfoBoxTimer_Tick;

         m_board.ItemsSource = m_driversList;

         m_mapper = new adjsw.F12025.F1UdpClrMapper();
         m_mapper.InsertTestData();

         if (!String.IsNullOrEmpty(App.PlaybackFile))
         {
            UdpPlaybackWindow wnd = new UdpPlaybackWindow(
               App.PlaybackFile,
               this);
            m_playbackWindow = wnd;
            wnd.Show();
         }
         else
         {
            m_udpClient = new UdpEventClient(20777);
            m_udpClient.ReceiveEvent += OnUdpReceive;
         }

         UpdateDriverGrid();
         UpdateCarStatus();
         UpdateTrackmap();         

         ShowInfoBox(s_splashText, TimeSpan.FromSeconds(10));

         Loaded += MainWindow_Loaded;
         Closing += MainWindow_Closing;

         m_board.DataGridRightClick += OnGridClick;

         //m_CreateTestJsonMappingFile();
         m_LoadNameMappings(false);
         DriverNameMappings dummy = new DriverNameMappings();
         dummy.LeagueName = "none";
         dummy.Mappings = new DriverNameMapping[0];
         m_emptyMapping = dummy;
         m_nameMappingNextIdx = 0;
         m_runtimeMapping = ReflectionCloner.DeepCopy(m_emptyMapping);

         m_board.DeltaVisible = false;
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
      }

      private void ShowContextMenuMapper(DriverData driver)
      {
         if (m_ctxMenu != null)
         {
            m_ctxMenu.IsOpen = false;
         }
         else
         {
            m_ctxMenu = new ContextMenu();
         }

         // clean old context menu
         // in case we have registered event handlers we should unregister, as it leaks otherwise
         foreach (var item in m_ctxMenu.Items)
         {
            var oldWrap = item as WrapPanel;
            if (oldWrap != null)
            {
               foreach (var innerChild in oldWrap.Children)
               {
                  var btn = innerChild as Button;
                  if (btn != null)
                     btn.Click -= Button_ChangeName_Click;
               }
            }


            MenuItem oldItem = item as MenuItem;
            if (null != oldItem)
            {
               foreach (var itemNested in oldItem.Items)
               {
                  var labelOld = itemNested as Label;

                  if (null != labelOld)
                     labelOld.MouseLeftButtonDown -= OnMappingCtxMenuClick;
               }
            }
         }
         m_ctxMenu.Items.Clear();

         // (re)create context menu items
         string header = driver.Name + " | " + driver.DriverNr + " | " + driver.Team.ToString("g");

         var edit = new TextBox();
         edit.Text = header;
         edit.KeyDown += Edit_ChangeName_KeyDown;
         var button = new Button();
         button.Content = "ok";
         var wrap = new WrapPanel();

         wrap.Children.Add(edit);
         wrap.Children.Add(button);
         button.Click += Button_ChangeName_Click;

         var label = new Label();
         label.Content = header;
         m_ctxMenu.Items.Add(header);
         m_ctxMenu.Items.Add(new Separator());
         m_ctxMenu.Items.Add(wrap);

         foreach (var mappinglist in m_nameMappings)
         {
            MenuItem newItem = new MenuItem();
            newItem.Header = mappinglist.LeagueName;
            foreach (var mapping in mappinglist.Mappings)
            {
               Label nested = new Label();
               nested.Content = mapping;
               nested.MouseLeftButtonDown += OnMappingCtxMenuClick;
               newItem.Items.Add(nested);
            }

            m_ctxMenu.Items.Add(newItem);
         }
         m_ctxMenu.IsOpen = true;
         m_ctxMenuReferencedDriver = driver;
      }

      private void Edit_ChangeName_KeyDown(object sender, KeyEventArgs e)
      {
         if (e.Key == Key.Enter)
         {
            Button_ChangeName_Click(null, null); // same as button event -> update new name
         }
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
         bool verticalLayout = ActualWidth > ActualHeight ? false : true;

         Canvas.SetTop(m_rootCanvas, 0);
         Canvas.SetLeft(m_rootCanvas, 0);
         m_rootCanvas.Height = ActualHeight;
         m_rootCanvas.Width = ActualWidth;

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
               m_board.Visibility = Visibility.Visible;
               m_carStatus.Visibility = Visibility.Visible;
               m_trackmap.Visibility = Visibility.Visible;
               if (verticalLayout)
               {
                  double y1 = ActualHeight * 1 / 2;
                  m_board.MaxHeight = y1;
                  m_board.MaxWidth = ActualWidth;

                  Canvas.SetTop(m_trackmap, y1);
                  Canvas.SetLeft(m_trackmap, 20);

                  Canvas.SetTop(m_carStatus, y1+60);
                  Canvas.SetLeft(m_carStatus, ActualWidth / 2);

               }
               else
               {
                  double x1 = ActualWidth * 3 / 4;
                  m_board.MaxHeight = ActualHeight;
                  m_board.MaxWidth = x1;

                  Canvas.SetTop(m_trackmap, 0);
                  Canvas.SetLeft(m_trackmap, x1);

                  Canvas.SetTop(m_carStatus, ActualHeight / 2);
                  Canvas.SetLeft(m_carStatus, x1+10);

                  if (ActualWidth < 1281)
                  {
                     // for 1280*1024 4:3
                     UpdateScaleCarMap(0.75);
                  }
                  else
                     UpdateScaleCarMap(1.0);

               }

               m_carStatus.Visibility = Visibility.Visible;
               m_board.Visibility = Visibility.Visible;
               break;
            case ViewType.CarMap:
               m_board.Visibility = Visibility.Collapsed;
               m_carStatus.Visibility = Visibility.Visible;
               m_trackmap.Visibility = Visibility.Visible;
               if (verticalLayout)
               {
                  Canvas.SetTop(m_trackmap, 50);
                  Canvas.SetLeft(m_trackmap, 250);

                  Canvas.SetTop(m_carStatus, ActualHeight / 2 + 30);
                  Canvas.SetLeft(m_carStatus, 250);
                  UpdateScaleCarMap(1.25);
               }
               else
               {
                  Canvas.SetTop(m_trackmap, 50);
                  if (ActualWidth > 1280)
                     Canvas.SetLeft(m_trackmap, 250);
                  else
                     Canvas.SetLeft(m_trackmap, 20);

                     Canvas.SetTop(m_carStatus, 150);
                  Canvas.SetLeft(m_carStatus, ActualWidth / 2);
                  UpdateScaleCarMap(1.5);

                  if (ActualWidth > 1920)
                     UpdateScaleCarMap(2);

               }
               break;

            default:
               break;
         }
      }

      private void UpdateScaleCarMap(double scale)
      {
         var transform = m_carStatus.RenderTransform as ScaleTransform;
         if (transform == null)
         {
            transform = new ScaleTransform();
            m_carStatus.RenderTransform = transform;
            m_trackmap.RenderTransform = transform;
         }

         transform.ScaleX = scale;
         transform.ScaleY = scale;
         transform.CenterX = -100 + 100*scale;
      }

      private void M_driverListViewSource_Filter(object sender, FilterEventArgs e)
      {
         DriverData d = e.Item as DriverData;
         if (d == null)
            e.Accepted = false;
         else
            e.Accepted = d.Present;
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
         foreach (var driver in m_mapper.Drivers)
         {
            if (driver.IsPlayer)
            {
               m_carStatus.txt_tyre_fl.Text = "" + driver.WearDetail.WearFrontLeft;
               m_carStatus.txt_tyre_fl.Background = DamageToToColor(driver.WearDetail.WearFrontLeft);

               m_carStatus.txt_tyre_fr.Text = "" + driver.WearDetail.WearFrontRight;
               m_carStatus.txt_tyre_fr.Background = DamageToToColor(driver.WearDetail.WearFrontRight);

               m_carStatus.txt_tyre_rl.Text = "" + driver.WearDetail.WearRearLeft;
               m_carStatus.txt_tyre_rl.Background = DamageToToColor(driver.WearDetail.WearRearLeft);

               m_carStatus.txt_tyre_rr.Text = "" + driver.WearDetail.WearRearRight;
               m_carStatus.txt_tyre_rr.Background = DamageToToColor(driver.WearDetail.WearRearRight);

               m_carStatus.txt_wing_fl.Text = "" + driver.WearDetail.DamageFrontLeft;
               m_carStatus.txt_wing_fl.Background = DamageToToColor(driver.WearDetail.DamageFrontLeft);

               m_carStatus.txt_wing_fr.Text = "" + driver.WearDetail.DamageFrontRight;
               m_carStatus.txt_wing_fr.Background = DamageToToColor(driver.WearDetail.DamageFrontRight);

               m_carStatus.txt_penalty.Text = "" + driver.PenaltySeconds + " s";
               if (driver.PenaltySeconds > 0)
               {
                  m_carStatus.txt_penalty.Foreground = Brushes.Red;
               }
               else
               {
                  m_carStatus.txt_penalty.Foreground = Brushes.Green;
               }

               m_carStatus.txt_temp_fl_inner.Text = "" + driver.WearDetail.TempFrontLeftInner + "°C";
               m_carStatus.txt_temp_fl_inner.Background = TyreToColor(driver.Tyre, driver.WearDetail.TempFrontLeftInner);
               m_carStatus.txt_temp_fl_surface.Text = "" + driver.WearDetail.TempFrontLeftOuter + "°C";
               m_carStatus.txt_temp_fl_surface.Background = TyreToColor(driver.Tyre, driver.WearDetail.TempFrontLeftOuter);
               m_carStatus.txt_temp_fr_inner.Text = "" + driver.WearDetail.TempFrontRightInner + "°C";
               m_carStatus.txt_temp_fr_inner.Background = TyreToColor(driver.Tyre, driver.WearDetail.TempFrontRightInner);
               m_carStatus.txt_temp_fr_surface.Text = "" + driver.WearDetail.TempFrontRightOuter + "°C";
               m_carStatus.txt_temp_fr_surface.Background = TyreToColor(driver.Tyre, driver.WearDetail.TempFrontRightOuter);

               m_carStatus.txt_temp_rl_inner.Text = "" + driver.WearDetail.TempRearLeftInner + "°C";
               m_carStatus.txt_temp_rl_inner.Background = TyreToColor(driver.Tyre, driver.WearDetail.TempRearLeftInner);
               m_carStatus.txt_temp_rl_surface.Text = "" + driver.WearDetail.TempRearLeftOuter + "°C";
               m_carStatus.txt_temp_rl_surface.Background = TyreToColor(driver.Tyre, driver.WearDetail.TempRearLeftOuter);
               m_carStatus.txt_temp_rr_inner.Text = "" + driver.WearDetail.TempRearRightInner + "°C";
               m_carStatus.txt_temp_rr_inner.Background = TyreToColor(driver.Tyre, driver.WearDetail.TempRearRightInner);
               m_carStatus.txt_temp_rr_surface.Text = "" + driver.WearDetail.TempRearRightOuter + "°C";
               m_carStatus.txt_temp_rr_surface.Background = TyreToColor(driver.Tyre, driver.WearDetail.TempRearRightOuter);

               m_carStatus.txt_temp_engine.Text = "" + driver.WearDetail.TempEngine + "°C";
               m_carStatus.txt_temp_engine.Background = EngineToColor(driver.WearDetail.TempEngine);

               break;
            }
         }
      }

      private void UpdateTrackmap()
      {
         m_trackmap.Update(m_mapper.Drivers, m_board.DriverUnderMouse as DriverData);
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

         // map ~62% to full red already
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

      private string To_H_MM_SS_mmm_String(double inputSeconds)
      {
         string str = "";
         int hour = (int)inputSeconds / 3600;
         inputSeconds -= hour * 3600.0;
         int minutes = (int)inputSeconds / 60;
         int seconds = (int)inputSeconds % 60;
         int milliseconds = (int)((inputSeconds % 1) * 1000);

         str += string.Format("{0,1}", hour);
         str += string.Format(":{0,2:00}", minutes);
         str += string.Format(":{0,2:00}", seconds);
         str += string.Format(".{0:000}", milliseconds);
         return str;
      }

      // TODO
      private void SaveReport()
      {
         StringBuilder sb = new StringBuilder();
         var sep = "--------------------------------------------------------------";
         var session = m_mapper.SessionInfo;
         var countDrivers = m_mapper.CountDrivers;
         var drivers = m_mapper.Drivers;
         var events = m_mapper.EventList.Events;
         string nl = "\r\n";

         if (events.Count == 0)
         {
            ShowInfoBox("Event Report not saved - no data!", TimeSpan.FromSeconds(3));
            return;
         }

         // header
         sb.Append("Racereport by " + Title + nl);
         sb.Append(session.EventTrack.ToString("g") + " " + session.Session.ToString("g") + nl + events[0].TimeCode + nl);
         sb.Append(session.TotalLaps + " Laps" + nl);

         // classification
         sb.Append(nl + nl + nl + "--------------------------------------CLASSIFICATION----------------------------------" + nl);
         if (m_mapper.Classification == null)
         {
            sb.Append("No race result available" + nl);
         }
         else
         {
            int maxDriverNameLen = 4; // "Name"
            ClassificationData winner = null;
            foreach (var result in m_mapper.Classification)
            {
               if (result.Driver.Name.Length > maxDriverNameLen)
                  maxDriverNameLen = result.Driver.Name.Length;

               if (result.Position == 1)
                  winner = result;
            }
            // "|POS | Name | LAPS | Track Time  | PEN | Total Time |"
            sb.Append("|POS |");
            sb.Append(" ");
            int addspaces1 = maxDriverNameLen - 4;
            int addspaces2 = addspaces1 / 2 + addspaces1 % 2;
            addspaces1 /= 2;
            for (int i = 0; i < addspaces1; ++i)
               sb.Append(" ");
            sb.Append("Name");
            for (int i = 0; i < addspaces2; ++i)
               sb.Append(" ");

            sb.Append(" | LAPS | Track Time  |    Delta    | PEN | Total Time  |    Delta    |" + nl);
            sb.Append("--------------------------------------------------------------------------------------" + nl);

            double leaderTimeTrack = 0.0;
            double leaderTimeTotal = 0.0;
            int leaderLaps = 0;

            for (int i = 0; i < m_mapper.Classification.Length; ++i)
            {
               foreach (var result in m_mapper.Classification)
               {
                  if (result.Position != (i + 1))
                     continue;

                  if (i == 0)
                  {
                     leaderTimeTrack = result.TotalRaceTime;
                     leaderTimeTotal = leaderTimeTrack + result.PenaltiesTime;
                     leaderLaps = result.NumLaps;
                  }

                  sb.Append(string.Format("| {0,2} ", result.Position));
                  sb.Append(string.Format("| {0,-" + maxDriverNameLen + "} ", result.Driver.Name)); // todo align column width!
                  sb.Append(string.Format("|  {0,2}  ", result.NumLaps));

                  sb.Append("| " + To_H_MM_SS_mmm_String(result.TotalRaceTime) + " ");

                  if (i == 0)
                  {
                     sb.Append("| ----------  ");
                  }
                  else
                  {
                     if (result.NumLaps == leaderLaps)
                        sb.Append("| " + To_H_MM_SS_mmm_String(result.TotalRaceTime - leaderTimeTrack) + " ");
                     else
                        sb.Append("|   +" + (leaderLaps - result.NumLaps).ToString("D2") + "L      ");
                  }


                  if (result.PenaltiesTime > 0)
                     sb.Append("| " + string.Format("{0,2}s ", result.PenaltiesTime));
                  else
                     sb.Append("|     ");

                  sb.Append("| " + To_H_MM_SS_mmm_String(result.TotalRaceTime + result.PenaltiesTime) + " ");

                  if (i == 0)
                  {
                     sb.Append("| ----------  |");
                  }
                  else
                  {
                     if (result.NumLaps == leaderLaps)
                        sb.Append("| " + To_H_MM_SS_mmm_String(result.TotalRaceTime + result.PenaltiesTime - leaderTimeTotal) + " |");
                     else
                        sb.Append("|   +" + (leaderLaps - result.NumLaps).ToString("D2") + "L      |");
                  }


                  sb.Append(nl);
               }
            }
         }

         // laptimes
         sb.Append(nl + nl + nl + "------------------------------LAPS----------------------------" + nl);
         sb.Append("--***Warning*** Laptimes may have rounding issues of +/- 1ms--" + nl);
         sb.Append("--------------------------------------------------------------" + nl + nl);

         for (int i = 0; i < countDrivers; ++i)
         {
            var driver = drivers[i];
            sb.Append("Driver: " + driver.Name + nl + sep + nl);
            sb.Append("|LAP | SECTOR1 | SECTOR2 | SECTOR3 | Lap Time | Penalties|" + nl);
            sb.Append(sep + nl);

            for (int j = 0; j < driver.LapNr; ++j)
            {
               if (j < driver.Laps.Length)
               {
                  var lap = driver.Laps[j];
                  if (j == driver.LapNr)
                     if (lap.Lap == 0)
                        continue;

                  sb.Append(
                     string.Format("| {0,2} | {1,7} | {2,7} | {3,7} | {4} |",
                      j + 1, 
                      lap.To_SS_MMMM(lap.Sector1Ms), 
                      lap.To_SS_MMMM(lap.Sector2Ms), 
                      lap.To_SS_MMMM(lap.Sector3Ms), 
                      lap.To_M_SS_MMMM(lap.LapMs)));

                  foreach (var ev in driver.Laps[j].Incidents)
                  {
                     sb.Append(ev.PenaltyType.ToString("g") + ",");
                  }
                  sb.Append(nl);
               }
            }

            sb.Append(sep + nl + nl + nl);

         }

         // Inicdents
         sb.Append(nl + nl + nl + "---------------------------INCIDENTS--------------------------" + nl);
         sb.Append("LAP | INCIDENT" + nl);

         foreach (var ev in events)
         {
            string driver = "N/A";
            if (ev.CarIndex <= countDrivers)
            {
               driver = drivers[ev.CarIndex].Name;
            }

            string lapStr = string.Format(" {0,2} | ", ev.LapNum);
            if (ev.LapNum == 0)
            {
               lapStr = " -- |";
            }


            switch (ev.Type)
            {
               case EventType.ChequeredFlag:
               case EventType.SessionStarted:
               case EventType.SessionEnded:
                  sb.Append(lapStr + ev.Type.ToString("g") + nl);
                  break;
               case EventType.FastestLap:
               case EventType.Retirement:
               case EventType.RaceWinner:
                  sb.Append(lapStr + driver + ": " + ev.Type.ToString("g") + nl);
                  break;


               case EventType.PenaltyIssued:
                  sb.Append(lapStr + driver + ": " + ev.PenaltyType.ToString("g") + " for " + ev.InfringementType.ToString("g") + nl);
                  break;

               case EventType.DRSenabled:
               case EventType.TeamMateInPits:
               case EventType.SpeedTrapTriggered:
               case EventType.DRSdisabled:
                  // don´t care
                  break;

            }
         }
         sb.Append(sep + nl);

         string filename = DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + "_report.txt";
         File.WriteAllText(filename, sb.ToString());
         ShowInfoBox(filename + "\r\nThe race report has been saved.", TimeSpan.FromSeconds(3));
      }


      private void SaveReportJson()
      {
         if (m_mapper.Classification == null)
            return;

         var json = new ResultExport();
         json.Events = m_mapper.EventList;
         json.EventTrack = m_mapper.SessionInfo.EventTrack;
         json.TotalLaps = m_mapper.SessionInfo.TotalLaps;
         json.Session = m_mapper.SessionInfo.Session;

         // merge drivers into the reduced export model
         json.Drivers = new DriverDataResult[m_mapper.Classification.Length];

         for (int i = 0; i < m_mapper.Classification.Length; ++i)
         {
            json.Drivers[i] = new DriverDataResult();
            DriverDataResult driverResult = json.Drivers[i];
            ClassificationData classification = m_mapper.Classification[i];
            DriverData driverSession = m_mapper.Classification[i].Driver;

            driverResult.DriverTag = driverSession.DriverTag;
            driverResult.DriverNr = driverSession.DriverNr;
            driverResult.Team = driverSession.Team;
            driverResult.Name = driverSession.Name;
            driverResult.PitPenalties = driverSession.PitPenalties;
            driverResult.VisualTyres = driverSession.VisualTyres;

            driverResult.Pos = classification.Position;
            driverResult.PenaltySeconds = classification.PenaltiesTime;
            driverResult.RaceTimeOnTrack = (int)(classification.TotalRaceTime * 1000 + 0.5);

            driverResult.Laps = new LapData[classification.NumLaps];
            for (int j = 0; j < driverResult.Laps.Length; ++j)
            {
               driverResult.Laps[j] = driverSession.Laps[j];
            }

            driverResult.BugtimeRacedirector = 0;
            driverResult.PenaltySecondsRacedirector = 0;
         }

         string filename = DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + "_report.json";
         var jsonText = Newtonsoft.Json.JsonConvert.SerializeObject(json, Newtonsoft.Json.Formatting.Indented);
         File.WriteAllText(filename, jsonText);
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

      private void m_LoadNameMappings(bool showErrorOnFail)
      {
         try
         {
            var json = File.ReadAllText("namemappings.json");
            m_nameMappings = Newtonsoft.Json.JsonConvert.DeserializeObject<DriverNameMappings[]>(json) as DriverNameMappings[];
         }
         catch (Exception ex)
         {
            if (showErrorOnFail)
               ShowInfoBox("Error loading name mappings file \"namemappings.json\":\r\n" + ex.Message, TimeSpan.FromSeconds(3));
            else 
            {
               DriverNameMappings dummy = new DriverNameMappings();
               dummy.LeagueName = "none";
               dummy.Mappings = new DriverNameMapping[0];

               m_nameMappings = new DriverNameMappings[1];
               m_nameMappings[0] = dummy;
            }
         }
      }

      private void PollUpdates_Tick(object sender, EventArgs e)
      {
         bool updated = false;
         byte[] newData;
         while (m_packetQue.TryDequeue(out newData))
         {
            m_mapper.Proceed(newData);
            updated = true;
         }

         if (!updated)
            return;

         m_board.SessionSource = m_mapper.SessionInfo;
         UpdateDriverGrid();
         UpdateCarStatus();
         UpdateTrackmap();
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
                     SaveReport();
                     SaveReportJson();
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
         if (e.Key == Key.F11)
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

         if (e.Key == Key.S)
         {
            SaveReport();
            SaveReportJson();
         }

         if (e.Key == Key.R)
         {
            // enable UDP recording
         }

         if (e.Key == Key.L)
            m_board.LeaderVisible = !m_board.LeaderVisible;

         if (e.Key == Key.D)
            m_board.StatusVisible = !m_board.StatusVisible;

         if (e.Key == Key.Space)
            ToggleView();

         if (e.Key == Key.M)
         {
            m_LoadNameMappings(true); // always reload in case text changed

            if (m_nameMappings != null)
            {
               if (m_nameMappingNextIdx >= m_nameMappings.Length)
               {
                  m_nameMappingNextIdx = 0;
                  m_runtimeMapping = ReflectionCloner.DeepCopy(m_emptyMapping); ;
                  ShowInfoBox("No Drivername Mapping selected.", TimeSpan.FromSeconds(2));
               }

               else if (m_nameMappings.Length >= m_nameMappingNextIdx)
               {
                  m_runtimeMapping = ReflectionCloner.DeepCopy<DriverNameMappings>(m_nameMappings[m_nameMappingNextIdx]);
                  ShowInfoBox("Drivername Mapping selected: " + m_runtimeMapping.LeagueName, TimeSpan.FromSeconds(2));
                  m_nameMappingNextIdx++;
               }
               else
               {
                  m_runtimeMapping = null;
               }
            }
            else
               m_runtimeMapping = null;

            m_mapper.SetDriverNameMappings(m_runtimeMapping);
         }
      }

      private void OnUdpReceive(object sender, UdpEventClientEventArgs e)
      {
         m_packetQue.Enqueue(e.data);
      }

      private void OnMappingCtxMenuClick(object sender, RoutedEventArgs e)
      {
         var itemLb = sender as Label;
         if (itemLb != null)
         {
            var originalMapping = itemLb.Content as DriverNameMapping;
            //var originalMapping = item.Items.CurrentItem as DriverNameMapping;

            if (originalMapping != null)
            {
               DriverNameMapping mappingToDriver = ReflectionCloner.DeepCopy(originalMapping);
               mappingToDriver.DriverNumber = m_ctxMenuReferencedDriver.DriverNr;
               mappingToDriver.Team = m_ctxMenuReferencedDriver.Team;


               bool exchangedMapping = false;
               for (int i = 0; i < m_runtimeMapping.Mappings.Length; ++i)
               {
                  var mapping = m_runtimeMapping.Mappings[i];
                  if (
                      (mapping.DriverNumber == mappingToDriver.DriverNumber) &&
                      (mapping.Team == mappingToDriver.Team)
                      )
                  {
                     m_runtimeMapping.Mappings[i] = mappingToDriver;
                     exchangedMapping = true;
                  }
               }

               if (!exchangedMapping)
               {
                  DriverNameMapping[] newMappingsArray = new DriverNameMapping[m_runtimeMapping.Mappings.Length + 1];

                  Array.Copy(m_runtimeMapping.Mappings, newMappingsArray, m_runtimeMapping.Mappings.Length);
                  newMappingsArray[newMappingsArray.Length - 1] = mappingToDriver;
                  m_runtimeMapping.Mappings = newMappingsArray;
               }

               m_mapper.SetDriverNameMappings(null);
               m_mapper.SetDriverNameMappings(m_runtimeMapping);
            }
         }
      }

      private void Button_ChangeName_Click(object sender, RoutedEventArgs e)
      {
         string newName = "";
         foreach (var child in m_ctxMenu.Items)
         {
            var wrap = child as WrapPanel;
            if (wrap != null)
            {
               foreach (var innerChild in wrap.Children)
               {
                  var tb = innerChild as TextBox;
                  if (tb != null)
                     newName = tb.Text;
               }
            }
         }

         if (string.IsNullOrEmpty(newName))
         {
            m_ctxMenu.IsOpen = false;
            return;
         }

         // find existing mapping and overwrite:
         bool exchangedMapping = false;
         for (int i = 0; i < m_runtimeMapping.Mappings.Length; ++i)
         {
            var mapping = m_runtimeMapping.Mappings[i];
            if (
                (mapping.DriverNumber == m_ctxMenuReferencedDriver.DriverNr) &&
                (mapping.Team == m_ctxMenuReferencedDriver.Team)
                )
            {
               m_runtimeMapping.Mappings[i].Name = newName;
               exchangedMapping = true;
            }
         }

         if (!exchangedMapping)
         {
            DriverNameMapping[] newMappingsArray = new DriverNameMapping[m_runtimeMapping.Mappings.Length + 1];
            Array.Copy(m_runtimeMapping.Mappings, newMappingsArray, m_runtimeMapping.Mappings.Length);
            var addMapping = new DriverNameMapping();
            addMapping.Name = newName;
            addMapping.DriverNumber = m_ctxMenuReferencedDriver.DriverNr;
            addMapping.Team = m_ctxMenuReferencedDriver.Team;
            newMappingsArray[newMappingsArray.Length - 1] = addMapping;
            m_runtimeMapping.Mappings = newMappingsArray;
         }

         m_mapper.SetDriverNameMappings(null);
         m_mapper.SetDriverNameMappings(m_runtimeMapping);
         m_ctxMenu.IsOpen = false;
      }

      private void OnGridClick(object sender, MouseButtonEventArgs e)
      {
         DriverData driver = m_board.DriverUnderMouse as DriverData;
         if (driver != null)
         {
            ShowContextMenuMapper(driver);
         }
      }

      private UdpEventClient m_udpClient = null;
      private UdpPlaybackWindow m_playbackWindow = null;
      private ConcurrentQueue<byte[]> m_packetQue = new ConcurrentQueue<byte[]>();
      private F1UdpClrMapper m_mapper = null;
      private DispatcherTimer m_pollTimer = new DispatcherTimer(DispatcherPriority.Render);
      private DispatcherTimer m_infoBoxTimer = new DispatcherTimer();
      private ObservableCollection<adjsw.F12025.DriverData> m_driversList = new ObservableCollection<adjsw.F12025.DriverData>();
      private CollectionViewSource m_driverListViewSource = new CollectionViewSource();
      private bool m_sessionClassificationHandled = false;
      private int m_nameMappingNextIdx = 0;
      private DriverNameMappings m_emptyMapping;
      private DriverNameMappings[] m_nameMappings;
      private DriverNameMappings m_runtimeMapping = null; // A volatile mapping which is altered during runtime by user intervention
      private bool m_autosave = true;
      private ContextMenu m_ctxMenu = null;
      private DriverData m_ctxMenuReferencedDriver = null;
      private ViewType m_viewType = ViewType.BoardAndCarMap; // on startup will toggle to next view, which will be board only


      private static string s_splashText =
@"
KRF1 Timing App for F1-25
Copyright 2018-2025 Andreas Jung

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
