// Copyright 2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
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

namespace adjsw.F12025
{
   /// <summary>
   /// Interaktionslogik für Trackmap.xaml
   /// </summary>
   public partial class Trackmap : UserControl
   {
      public Trackmap()
      {
         InitializeComponent();
         m_playerMark.Width = 3;
         m_playerMark.Fill = Brushes.Black;
         m_playerMark.Visibility = Visibility.Collapsed;
         m_playerMark.Stroke = Brushes.Black;
         m_playerMark.StrokeThickness = 3;
         m_canv.Children.Add(m_playerMark);
      }

      public void Update(DriverData[] dat, DriverData highlight)
      {
         bool havePlayer = false;
         if (dat != null)
         {
            m_SetSize(dat.Length);
            for (int i = 0; i < dat.Length; i++)
            {
               havePlayer |= dat[i].IsPlayer;
               m_UpdateDriver(dat[i], m_ellipses[i], dat[i] == highlight);
            }
         }
         //if (!havePlayer)
            m_playerMark.Visibility = Visibility.Collapsed;
      }


      private void m_UpdateDriver(DriverData d, Ellipse e, bool highlight)
      {
         // according to ui layout
         double xCenter = 400 / 2.0; 
         double yCenter = xCenter + 100;
         double r = 375/2.0;

         xCenter -= e.Height / 2;
         yCenter -= e.Width / 2;

         // draw pitting drivers outside the track
         switch (d.Status)
         {
            case DriverStatus.Garage:
            case DriverStatus.DSQ:
            case DriverStatus.Pitting:
            case DriverStatus.Pitlane:
            case DriverStatus.Retired:
            case DriverStatus.DNF:
               r *= 1.15;
               break;

            case DriverStatus.OutLap:
            case DriverStatus.OnTrack:
            case DriverStatus.Inlap:
               break;
         }

         switch (d.Team)
         {
            case F1Team.Mercedes:
               e.Fill = Brushes.Turquoise;
               break;

            case F1Team.Ferrari:
               e.Fill = Brushes.Red;
               break;

            case F1Team.RedBull:
               e.Fill = Brushes.Blue;
               break;

            case F1Team.Williams:
               e.Fill = Brushes.White;
               break;

            case F1Team.AstonMartin:
               e.Fill = Brushes.Green;
               break;

            case F1Team.Renault:
               e.Fill = Brushes.Yellow;
               break;

            case F1Team.AlphaTauri:
               e.Fill = m_brushTr;
               break;

            case F1Team.Haas:
               e.Fill = Brushes.DarkGray;
               break;

            case F1Team.McLaren:
               e.Fill = Brushes.Orange;
               break;

            case F1Team.Sauber:
               e.Fill = Brushes.DarkRed;
               break;

            case F1Team.Classic:
               e.Fill = Brushes.Gray;
               break;
         }

         e.Visibility = d.Present ? Visibility.Visible : Visibility.Collapsed;

         double rad = Math.PI * 2 * d.TrackPositionPerc; // trackposition in interval 0...2 pi

         rad -= Math.PI / 2;

         double x = xCenter + r * Math.Cos(rad);
         double y = yCenter + r * Math.Sin(rad);
         Canvas.SetZIndex(e, highlight ? 26 : 25 - d.Pos);
         Canvas.SetTop(e, y);
         Canvas.SetLeft(e, x);

         if (highlight)
         {
            int ms = DateTime.Now.Millisecond % 1000;
            if (ms < 500)
            {
               e.StrokeThickness = 4.0;
            }
            else
            {
               e.StrokeThickness = 1.0;
            }
         }
         else
            e.StrokeThickness = 1.0;

         if (d.IsPlayer)
         {
            /* does not work properly 
            m_playerMark.Visibility = Visibility.Visible;
            m_playerMark.X1 = r * Math.Cos(rad);
            m_playerMark.Y1 = r * Math.Sin(rad);
            m_playerMark.X2 = 0;
            m_playerMark.Y2 = 0;
            Canvas.SetTop(m_playerMark, yCenter);
            Canvas.SetLeft(m_playerMark, xCenter);
            ...*/
            int ms = DateTime.Now.Millisecond % 1000;
            if (ms < 500)
            {
               e.Fill = Brushes.HotPink;
            }
            Canvas.SetZIndex(e, 25);
         }
      }

      private void m_SetSize(int size)
      {
         if (m_ellipses.Length < size)
         {
            var ellipsesNew = new Ellipse[size];

            for (int i = 0; i < m_ellipses.Length; i++)
            {
               ellipsesNew[i] = m_ellipses[i];               
            }

            for (int i = m_ellipses.Length; i < size; ++i)
            {
               ellipsesNew[i] = m_CreateEllipse();
               m_canv.Children.Add(ellipsesNew[i]);
            }

            m_ellipses = ellipsesNew;
         }
         else if (m_ellipses.Length > size)
         {
            var ellipsesNew = new Ellipse[size];

            for (int i = 0; i < size; i++)
            {
               ellipsesNew[i] = m_ellipses[i];
            }

            for (int i = size; i < m_ellipses.Length; ++i)
            {
               m_canv.Children.Remove(m_ellipses[i]);
            }

            m_ellipses = ellipsesNew;
         }
      }

      private Ellipse m_CreateEllipse()
      {
         var el = new Ellipse();
         el.Height = 17;
         el.Width = 17;
         el.Fill = Brushes.Black;
         el.Stroke = Brushes.Black;
         el.StrokeThickness = 1.0;
         return el;
      }

      private Ellipse[] m_ellipses = new Ellipse[0];
      private Line m_playerMark = new Line();
      private SolidColorBrush m_brushTr = new SolidColorBrush(Color.FromRgb(10, 100, 150));
   }
}
