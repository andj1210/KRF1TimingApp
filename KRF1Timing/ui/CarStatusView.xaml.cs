// Copyright 2018-2020 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace adjsw.F12026
{
   /// <summary>
   /// Interaktionslogik für CarStatusView.xaml
   /// </summary>
   public partial class CarStatusView : UserControl
   {
      public CarStatusView()
      {
         InitializeComponent();
         m_defaultCarImage = img_car.Source;
      }

      /// <summary>
      /// Switch the car silhouette to a team-specific bird-view image if one is
      /// present in the "cars" sub-folder of the executing assembly, otherwise
      /// restore the built-in placeholder.
      /// File naming convention: cars\car{(int)team}.png
      /// e.g. cars\car0.png = Mercedes, cars\car1.png = Ferrari …
      /// </summary>
      public void SetCarImage(F1Team team)
      {
         if (team == m_loadedTeam)
            return;

         m_loadedTeam = team;

         string asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
         string imgPath = Path.Combine(asmDir, "cars", $"car{(int)team}.png");

         if (File.Exists(imgPath))
         {
            try
            {
               var bmp = new BitmapImage();
               bmp.BeginInit();
               bmp.UriSource = new Uri(imgPath);
               bmp.CacheOption = BitmapCacheOption.OnLoad;
               bmp.EndInit();
               bmp.Freeze();
               img_car.Source = bmp;
               return;
            }
            catch
            {
               // Fall through to default below
            }
         }

         img_car.Source = m_defaultCarImage;
      }

      /// <summary>
      /// Update the ERS row: the small mode box (letter + colours depend on the
      /// deployment mode) and the energy bar (fill = remaining energy 0..1). The
      /// bar is green normally and medium-light blue while overtake is available.
      /// </summary>
      public void SetErs(CarDetailErsMode mode, double availPercent, bool overtakeAvailable)
      {
         string letter;
         Brush boxBg;
         Brush letterFg;

         switch (mode)
         {
         case CarDetailErsMode.Med:
            letter = "M"; boxBg = s_ersMedBrush;   letterFg = Brushes.White; break;
         case CarDetailErsMode.Hot:
            letter = "H"; boxBg = s_ersHotBrush;   letterFg = Brushes.DarkViolet; break;
         case CarDetailErsMode.Boost:
            letter = "B"; boxBg = s_ersBoostBrush; letterFg = Brushes.DarkRed; break;
         case CarDetailErsMode.None:
         default:
            letter = "N"; boxBg = s_ersNoneBrush;  letterFg = s_ersNoneFg;   break;
         }

         ers_letter.Text = letter;
         ers_box.Background = boxBg;
         ers_letter.Foreground = letterFg;

         ers_bar_fill.Fill = overtakeAvailable ? s_ersBarOvertakeBrush : s_ersBarGreenBrush;

         double p = availPercent;
         if (p < 0.0) p = 0.0;
         if (p > 1.0) p = 1.0;
         ers_bar_fill.Width = p * (ers_bar_track.Width - 2.0); // minus the 1px border on each side
      }

      private static Brush _FrozenBrush(byte r, byte g, byte b)
      {
         var br = new SolidColorBrush(Color.FromRgb(r, g, b));
         br.Freeze();
         return br;
      }

      private readonly ImageSource m_defaultCarImage;
      private F1Team m_loadedTeam = (F1Team)(-1); // sentinel: nothing loaded yet

      private static readonly Brush s_ersNoneBrush  = _FrozenBrush(0x44, 0x44, 0x44);
      private static readonly Brush s_ersNoneFg     = _FrozenBrush(0xCC, 0xCC, 0xCC);
      private static readonly Brush s_ersMedBrush   = _FrozenBrush(86, 153, 100);
      private static readonly Brush s_ersHotBrush   = _FrozenBrush(30, 255, 78);
      private static readonly Brush s_ersBoostBrush = _FrozenBrush(30, 255, 78);

      // ERS bar fill: green normally, medium-light blue while overtake is available
      private static readonly Brush s_ersBarGreenBrush    = _FrozenBrush(0x0D, 0xBF, 0x41);
      private static readonly Brush s_ersBarOvertakeBrush = _FrozenBrush(26, 117, 199);
   }
}
