using adjsw.F12020;
using DesktopWPFAppLowLevelKeyboardHook;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace F1SessionDisplay
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

            m_listenerHdl += KbListener_KeyDown;
            m_kbListener.OnKeyPressed += m_listenerHdl;
            m_kbListener.HookKeyboard();
            m_timer.Tick += T_Tick;
            m_timer.Interval = TimeSpan.FromMilliseconds(100);
            m_grid.ItemsSource = m_driversList;
            m_timer.IsEnabled = true;
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

        private void T_Tick(object sender, EventArgs e)
        {
            if (null == m_parser)
            {
                if (!String.IsNullOrEmpty(ip))
                {
                    m_parser = new adjsw.F12020.F12020Parser(ip, 20777);
                }
                else
                    m_parser = new adjsw.F12020.F12020Parser("127.0.0.1", 20777);
            }

            while (m_parser.Work()) { }

            m_UpdateGrid();
            m_UpdateCarStatus();
        }

        private void m_UpdateGrid()
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
                //if (driver.Present)
                {
                    if ((driver.Pos) > 0 && (driver.Pos <= 20))
                    {
                        if ((driver.Pos - 1) < m_driversList.Count)
                            m_driversList[driver.Pos - 1] = driver;
                    }
                        
                }
            }
            
        }

        private void m_UpdateCarStatus()
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

            if (e.Key == Key.T)
                ToggleView();
        }

        private void KbListener_KeyDown(object sender, KeyPressedArgs args)
        {
            if (args.KeyPressed == Key.S)
            {
                //m_parser.Save();
            }

            if (args.KeyPressed == Key.Space)
                ToggleView();
        }

        private LowLevelKeyboardListener m_kbListener = new LowLevelKeyboardListener();
        private EventHandler<KeyPressedArgs> m_listenerHdl; // Needed elsewise error in KeyboardListener / some issue between GC + Native resources

        private adjsw.F12020.F12020Parser m_parser = null;

        private DispatcherTimer m_timer = new DispatcherTimer();
        private ObservableCollection<adjsw.F12020.DriverData> m_driversList = new ObservableCollection<adjsw.F12020.DriverData>();
    }
}
