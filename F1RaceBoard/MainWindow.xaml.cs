// Copyright 2018-2020 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using adjsw.F12020;
using DesktopWPFAppLowLevelKeyboardHook;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.RightsManagement;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace F1GameSessionDisplay
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public string ip = "";

        public MainWindow()
        {
            InitializeComponent();

            Title = "F1-Game Session-Display for F1-2020 V0.4";

            m_listenerHdl += KbListener_KeyDown;
            m_kbListener.OnKeyPressed += m_listenerHdl;
            m_kbListener.HookKeyboard();

            m_pollTimer.Tick += PollUpdates_Tick;
            m_pollTimer.Interval = TimeSpan.FromMilliseconds(100);            
            m_pollTimer.IsEnabled = true;

            m_infoBoxTimer.Tick += m_InfoBoxTimer_Tick;

            m_grid.ItemsSource = m_driversList;

            m_parser = new adjsw.F12020.F12020UdpClrMapper();
            m_parser.InsertTestData();
            m_udpClient = new UdpEventClient(20777);
            m_udpClient.ReceiveEvent += OnUdpReceive;
            UpdateGrid();
            UpdateCarStatus();
            ToggleView();

            ShowInfoBox(s_splashText, TimeSpan.FromSeconds(7));

            Closing += MainWindow_Closing;

            //m_CreateTestJsonMappingFile();
            //m_LoadNameMappings();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (m_udpClient != null)
                m_udpClient.Dispose();
        }

        private void ToggleView()
        {
            if (m_grid.Visibility == Visibility.Visible)
            {
                m_grid.Visibility = Visibility.Collapsed;
                m_carStatus.Visibility = Visibility.Visible;
            }

            else
            {
                m_grid.Visibility = Visibility.Visible;
                m_carStatus.Visibility = Visibility.Collapsed;
            }
        }

        private void PollUpdates_Tick(object sender, EventArgs e)
        {
            byte[] newData;
            while (m_packetQue.TryDequeue(out newData))
            {
                m_parser.Proceed(newData);
            }

            m_grid.SessionSource = m_parser.SessionInfo;
            UpdateGrid();
            UpdateCarStatus();

            if (m_parser.SessionInfo.Session == SessionType.Race)
            {
                if (m_parser.Classification != null)
                {
                    if (!m_sessionFinishNotificationShown)
                    {
                        if (!m_autosave)
                            ShowInfoBox("The Race has finished.\r\n Click in the window and hit\r\n---\"s\"---\r\nto save the race report.", TimeSpan.FromSeconds(10));
                        else
                        {
                            SaveReport();
                            //SaveReportJson();
                        }
                        m_sessionFinishNotificationShown = true;
                    }
                }
                else
                {
                    m_sessionFinishNotificationShown = false;
                }
            }

            bool qualySession = false;
            switch (m_parser.SessionInfo.Session)
            {
                case SessionType.P1:
                case SessionType.P2:
                case SessionType.P3:
                case SessionType.ShortPractice:
                case SessionType.Q1:
                case SessionType.Q2:
                case SessionType.Q3:
                case SessionType.ShortQ:
                    qualySession = true;
                    break;

                default:
                case SessionType.OSQ: // this is not meant to show the fastest lap, since only 1 lap show sector deltas...
                    qualySession = false;
                    break;
            }
            m_grid.Quali = qualySession;
        }

        private void OnUdpReceive(object sender, UdpEventClientEventArgs e)
        {
            m_packetQue.Enqueue(e.data);
        }

        private void UpdateGrid()
        {
            if (m_driversList.Count != m_parser.CountDrivers)
            {
                m_driversList.Clear();
                foreach (var driver in m_parser.Drivers)
                {
                    if (driver.Present)
                    {
                        m_driversList.Add(driver);
                    }
                }
                m_grid.ItemsSource = null;
                m_grid.ItemsSource = m_driversList;
            }

            foreach (var driver in m_parser.Drivers)
            {
                if ((driver.Pos) > 0 && (driver.Pos <= 20))
                {
                    if ((driver.Pos - 1) < m_driversList.Count)
                        m_driversList[driver.Pos - 1] = driver;
                }
            }
        }

        private void UpdateCarStatus()
        {
            foreach (var driver in m_parser.Drivers)
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

        public Color ColorFromHSV(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            byte v = (byte) Convert.ToInt32(value);
            byte p = (byte) Convert.ToInt32(value * (1 - saturation));
            byte q = (byte) Convert.ToInt32(value * (1 - f * saturation));
            byte t = (byte) Convert.ToInt32(value * (1 - (1 - f) * saturation));

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
                        ,1 , 1)
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
                SaveReport();

            if (e.Key == Key.L)
                m_grid.LeaderVisible = !m_grid.LeaderVisible;

            if (e.Key == Key.D)
                m_grid.DeltaVisible = !m_grid.DeltaVisible;

            if (e.Key == Key.M)
            {
                m_LoadNameMappings(); // always reload in case text changed

                if (m_nameMappings != null)
                {
                    if (m_nameMappingNextIdx >= m_nameMappings.Length)
                    {
                        m_nameMappingNextIdx = 0;
                        ShowInfoBox("No Drivername Mapping selected.", TimeSpan.FromSeconds(2));
                    }
                        

                    else if (m_nameMappings.Length >= m_nameMappingNextIdx)
                    {
                        m_parser.SetDriverNameMappings(m_nameMappings[m_nameMappingNextIdx]);
                        ShowInfoBox("Drivername Mapping selected: " + m_nameMappings[m_nameMappingNextIdx].LeagueName, TimeSpan.FromSeconds(2));
                        m_nameMappingNextIdx++;
                    }
                    else
                    {
                        m_parser.SetDriverNameMappings(null);
                    }                    
                }
                else
                    m_parser.SetDriverNameMappings(null);

            }
        }

        private void KbListener_KeyDown(object sender, KeyPressedArgs args)
        {
            if (args.KeyPressed == Key.Space)
                ToggleView();
        }

        private string To_H_MM_SS_mmm_String(double inputSeconds)
        {
            string str = "";
            int hour = (int)inputSeconds / 3600;            
            inputSeconds -= hour * 3600.0;
            int minutes = (int)inputSeconds / 60;
            int seconds = (int)inputSeconds % 60;
            int milliseconds = (int) ((inputSeconds % 1) * 1000);

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
            var session = m_parser.SessionInfo;
            var countDrivers = m_parser.CountDrivers;
            var drivers = m_parser.Drivers;
            var events = m_parser.EventList.Events;
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
            if (m_parser.Classification == null)
            {
                sb.Append("No race result available" + nl);
            }
            else
            {
                int maxDriverNameLen = 4; // "Name"
                ClassificationData winner = null;
                foreach (var result in m_parser.Classification)
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
                for (int i = 0; i< addspaces1; ++i)
                    sb.Append(" ");
                sb.Append("Name");
                for (int i = 0; i < addspaces2; ++i)
                    sb.Append(" ");

                sb.Append(" | LAPS | Track Time  |    Delta    | PEN | Total Time  |    Delta    |" + nl);
                sb.Append("--------------------------------------------------------------------------------------" + nl);

                double leaderTimeTrack = 0.0;
                double leaderTimeTotal = 0.0;
                int leaderLaps = 0;

                for (int i = 0; i < m_parser.Classification.Length; ++i)
                {
                    foreach (var result in m_parser.Classification)
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
                        sb.Append(string.Format("| {0,-"  + maxDriverNameLen + "} ", result.Driver.Name)); // todo align column width!
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
                                sb.Append("|    +" + (leaderLaps - result.NumLaps) + "L      ");
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
                                sb.Append("|    +" + (leaderLaps - result.NumLaps) + "L      |");
                        }


                        sb.Append(nl);
                    }
                }                
            }

            // laptimes
            sb.Append(nl + nl + nl + "------------------------------LAPS----------------------------" + nl);
            sb.Append(               "--***Warning*** Laptimes may have rounding issues of +/- 1ms--" + nl);
            sb.Append(               "--------------------------------------------------------------" + nl + nl);

            for (int i = 0; i < countDrivers; ++i)
            {
                var driver = drivers[i];
                sb.Append("Driver: " + driver.Name + nl + sep + nl);
                sb.Append("|LAP | SECTOR1 | SECTOR2 | SECTOR3 | Lap Time | Penalties|" + nl);
                sb.Append(sep + nl);

                for (int j = 0; j < driver.LapNr - 1; ++j)
                {
                    var lap = driver.Laps[j];
                    float sector3 = lap.Lap - (lap.Sector1 + lap.Sector2);
                    int minutes = (int)lap.Lap / 60;
                    float seconds = lap.Lap % 60.0f;
                    int secondsInt = (int)seconds;
                    int milliesInt = (int)((seconds - secondsInt) * 1000);

                    sb.Append(string.Format("| {0,2} | {1,7:0.000} | {2,7:0.000} | {3,7:0.000} | {4}:{5:00}.{6:000} |",
                        j + 1, lap.Sector1, lap.Sector2, sector3, minutes, secondsInt, milliesInt));

                    foreach (var ev in driver.Laps[j].Incidents)
                    {
                        sb.Append(ev.PenaltyType.ToString("g") + ",");
                    }
                    sb.Append(nl);
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

            string filename = DateTime.Now.ToString("ddMMyy_HHmmss") + "_report.txt";
            File.WriteAllText(filename, sb.ToString());
            ShowInfoBox(filename + "\r\nThe race report has been saved.", TimeSpan.FromSeconds(3));
        }


        public class JsonEntry
        {
            public string SessionInfo { get; set; }
            public string Track { get; set; }
            public int Laps { get; set; } // only for race

            public DriverData[] Drivers { get; set; }
            public string[] DriverTag { get; set; }
        }


        private void SaveReportJson()
        {
            // TODO needs a more restricted output (full internal state contains useless information).

            var session = m_parser.SessionInfo;
            var countDrivers = m_parser.CountDrivers;
            var drivers = m_parser.Drivers;
            var events = m_parser.EventList.Events;

            JsonEntry json = new JsonEntry();


            json.SessionInfo = session.Session.ToString("g");
            json.Track = session.EventTrack.ToString("g");
            json.Laps = session.TotalLaps;

            DriverData[] jsonDrivers = new DriverData[countDrivers];
            for (int i = 0; i < countDrivers; ++i)
            {
                jsonDrivers[i] = m_parser.Drivers[i];
            }

            json.Drivers = jsonDrivers;

            string[] jsonDriversTag = new string[countDrivers];
            json.DriverTag = jsonDriversTag;

            string filename = DateTime.Now.ToString("ddMMyy_HHmmss") + "_report.json";

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
            m_infoBox.Visibility = Visibility.Collapsed;
        }

        private void m_CreateTestJsonMappingFile()
        {
            DriverNameMappings[] mappings = new DriverNameMappings[2];
            mappings[0] = new DriverNameMappings();
            mappings[1] = new DriverNameMappings();

            mappings[0].LeagueName = "KRF1 - 1";
            mappings[0].Mappings = new DriverNameMapping[2];
            mappings[0].Mappings[0] = new DriverNameMapping();
            mappings[0].Mappings[1] = new DriverNameMapping();
            mappings[0].Mappings[0].Team = F1Team.RedBull;
            mappings[0].Mappings[0].Name = "Max Damage";
            mappings[0].Mappings[0].DriverNumber = 91;

            mappings[0].Mappings[1].Team = null;
            mappings[0].Mappings[1].Name = "tomy (Veydn)";
            mappings[0].Mappings[1].DriverNumber = 13;

            mappings[1].LeagueName = "KRF1 - 2";
            mappings[1].Mappings = new DriverNameMapping[3];
            mappings[1].Mappings[0] = new DriverNameMapping();
            mappings[1].Mappings[1] = new DriverNameMapping();
            mappings[1].Mappings[2] = new DriverNameMapping();
            mappings[1].Mappings[0].Team = F1Team.RedBull;
            mappings[1].Mappings[0].Name = "Max Damage";
            mappings[1].Mappings[0].DriverNumber = 91;

            mappings[1].Mappings[1].Team = null;
            mappings[1].Mappings[1].Name = "Leopard";
            mappings[1].Mappings[1].DriverNumber = 91;
            mappings[1].Mappings[2].Team = null;
            mappings[1].Mappings[2].Name = "SimonLaui";
            mappings[1].Mappings[2].DriverNumber = 86;

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(mappings, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText("json.txt", json);
        }

        private void m_LoadNameMappings()
        {
            try
            {
                var json = File.ReadAllText("namemappings.json");
                m_nameMappings = Newtonsoft.Json.JsonConvert.DeserializeObject<DriverNameMappings[]>(json) as DriverNameMappings[];
            }
            catch (Exception ex)
            {
                ShowInfoBox("Error loading name mappings file \"namemappings.json\":\r\n" + ex.Message, TimeSpan.FromSeconds(3));
            }
        }

        private LowLevelKeyboardListener m_kbListener = new LowLevelKeyboardListener();
        private EventHandler<KeyPressedArgs> m_listenerHdl; // Needed elsewise error in KeyboardListener / some issue between GC + Native resources
        private UdpEventClient m_udpClient = null;
        private ConcurrentQueue<byte[]> m_packetQue = new ConcurrentQueue<byte[]>();
        private F12020UdpClrMapper m_parser = null;
        private DispatcherTimer m_pollTimer = new DispatcherTimer();
        private DispatcherTimer m_infoBoxTimer = new DispatcherTimer();
        private ObservableCollection<adjsw.F12020.DriverData> m_driversList = new ObservableCollection<adjsw.F12020.DriverData>();
        private bool m_sessionFinishNotificationShown = false;
        private int m_nameMappingNextIdx = 0;
        private DriverNameMappings[] m_nameMappings;
        private bool m_autosave = true;

        private static string s_splashText =
@"
F1-Game Session-Display for F1-2021
Copyright 2018-2021 Andreas Jung

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
