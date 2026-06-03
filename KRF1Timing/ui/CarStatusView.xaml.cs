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

      private readonly ImageSource m_defaultCarImage;
      private F1Team m_loadedTeam = (F1Team)(-1); // sentinel: nothing loaded yet
   }
}
