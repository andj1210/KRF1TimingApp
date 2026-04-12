// Copyright 2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace adjsw.F12025
{
   /// <summary>
   /// Shows car positions either on an actual track outline (when a track JSON is available)
   /// or on the fallback circle ("circle of doom").
   /// </summary>
   public partial class Trackmap : UserControl
   {
      public Trackmap()
      {
         InitializeComponent();

         DateTime ringNow = DateTime.Now;
         m_arrivalRing = new DateTime[10];
         for (int i = 0; i < 10; i++)
            m_arrivalRing[i] = ringNow - TimeSpan.FromMilliseconds((9 - i) * s_interpolHeadroomMs);
         m_lastNewDataTime = ringNow;

         m_playerMark.Width = 3;
         m_playerMark.Fill = Brushes.Black;
         m_playerMark.Visibility = Visibility.Collapsed;
         m_playerMark.Stroke = Brushes.Black;
         m_playerMark.StrokeThickness = 3;
         m_canv.Children.Add(m_playerMark);

         SizeChanged += (s, e) =>
         {
            m_UpdateBorderAndCircle();
            m_RebuildTrackPolyline();
            m_UpdateEllipseSizes();
         };
      }

      /// <summary>
      /// When true, cars in Garage / Pitlane / Pitting are pushed away from the
      /// nearest track edge by s_pitExaggerationFactor so they are clearly
      /// distinguishable from on-track cars at a glance.
      /// Only has an effect when real track data is loaded.
      /// Set once in MainWindow as a compile-time opt-in.
      /// </summary>
      public bool PitExaggeration { get; set; } = false;

      /// <summary>
      /// Set from MainWindow whenever SessionInfo.EventTrack changes.
      /// Loads the matching JSON from TrackMapStore and switches rendering mode.
      /// </summary>
      public Track ActiveTrack
      {
         set
         {
            if (value == m_activeTrack)
               return;
            m_activeTrack = value;
            m_LoadTrackData();
         }
      }

      /// <summary>When true, car positions are interpolated between relay updates.</summary>
      public bool InterpolationEnabled { get; set; } = false;

      public void Update(DriverData[] dat, DriverData highlight, bool newData)
      {
         if (dat != null)
         {
            m_SetSize(dat.Length);

            if (InterpolationEnabled)
            {
               if (newData)
                  m_OnNewData(dat);

               double t = (DateTime.Now - m_lastNewDataTime).TotalMilliseconds
                          / (m_estimatedIntervalMs + s_interpolHeadroomMs);
               if (t > 1.0) t = 1.0;

               for (int i = 0; i < dat.Length; i++)
                  m_UpdateDriverInterp(dat[i], m_interpStates[i], m_ellipses[i], dat[i] == highlight, t);
            }
            else
            {
               for (int i = 0; i < dat.Length; i++)
                  m_UpdateDriver(dat[i], m_ellipses[i], dat[i] == highlight);
            }
         }
         m_playerMark.Visibility = Visibility.Collapsed;
      }

      // -- dynamic border & circle geometry ----------------------------------

      private void m_UpdateBorderAndCircle()
      {
         double w = ActualWidth > 1 ? ActualWidth : 400;
         double h = ActualHeight > 1 ? ActualHeight : 500;

         // Border matches control size
         m_border.Width = w;
         m_border.Height = h;

         // Circle: use 10% margin on left/right/bottom, and shift the centre
         // downward so there is extra headroom above for pit cars.
         double marginX = w * s_marginFraction;
         double marginY = h * s_marginFraction;

         double usableW = w - 2 * marginX;
         double usableH = h - 2 * marginY;

         // Circle radius limited by the smaller usable dimension
         double radius = Math.Min(usableW, usableH) / 2.0;
         double ringWidth = radius * 0.13;   // proportional ring thickness

         // Centre: horizontally centred, vertically shifted down
         // (top margin is 20% to give pit headroom, bottom margin is 10%)
         double cx = w / 2.0;
         double cy = h * 0.55;

         m_circleOuter.Center = new Point(cx, cy);
         m_circleOuter.RadiusX = radius;
         m_circleOuter.RadiusY = radius;
         m_circleInner.Center = new Point(cx, cy);
         m_circleInner.RadiusX = radius - ringWidth;
         m_circleInner.RadiusY = radius - ringWidth;

         // Cache for circle-fallback driver positioning
         m_circleCenterX = cx;
         m_circleCenterY = cy;
         m_circleRadius = radius - ringWidth / 2.0;   // midpoint of ring

#if DEBUG
         //m_border.Visibility = Visibility.Visible;
         m_border.Visibility = Visibility.Collapsed;
#else
         m_border.Visibility = Visibility.Collapsed;
#endif
      }

      // -- track data loading & polyline -------------------------------
      private void m_LoadTrackData()
      {
         m_RemovePolyline();
         m_transformValid = false;
         m_trackData = null;

         if (m_activeTrack == Track.Unknown)
         {
            m_circleRing.Visibility = Visibility.Visible;
            return;
         }

         var data = TrackMapStore.Load(m_activeTrack);
         if (data?.Points == null || data.Points.Count < 2)
         {
            m_circleRing.Visibility = Visibility.Visible;
            return;
         }

         m_trackData = data;
         m_circleRing.Visibility = Visibility.Collapsed;
         m_RebuildTrackPolyline();
      }

      private void m_RebuildTrackPolyline()
      {
         m_RemovePolyline();
         m_transformValid = false;

         if (m_trackData == null)
            return;

         double canvasW = ActualWidth > 10 ? ActualWidth : 400;
         double canvasH = ActualHeight > 10 ? ActualHeight : 500;

         m_rotRad = m_trackData.RotationDegrees * Math.PI / 180.0;

         // Centroid of the raw point cloud (rotation anchor)
         double sumX = 0, sumY = 0;
         foreach (var p in m_trackData.Points) { sumX += p.X; sumY += p.Z; }
         m_centroidX = sumX / m_trackData.Points.Count;
         m_centroidY = sumY / m_trackData.Points.Count;

         // Bounding box in rotated space
         double minX = double.MaxValue, maxX = double.MinValue;
         double minY = double.MaxValue, maxY = double.MinValue;
         foreach (var p in m_trackData.Points)
         {
            var (rx, ry) = m_Rotate(p.X, p.Z);
            if (rx < minX) minX = rx; if (rx > maxX) maxX = rx;
            if (ry < minY) minY = ry; if (ry > maxY) maxY = ry;
         }

         // 10% margin on each side for off-track cars
         double padX = canvasW * s_marginFraction;
         double padY = canvasH * s_marginFraction;
         double rangeX = Math.Max(maxX - minX, 1);
         double rangeY = Math.Max(maxY - minY, 1);

         m_scale = Math.Min((canvasW - 2 * padX) / rangeX,
                            (canvasH - 2 * padY) / rangeY);

         // Centre the rendered track in the canvas
         double midRX = (minX + maxX) / 2.0;
         double midRY = (minY + maxY) / 2.0;
         m_offsetX = canvasW / 2.0 - midRX * m_scale;
         m_offsetY = canvasH / 2.0 - midRY * m_scale;
         m_transformValid = true;

         // Stroke width proportional to canvas diagonal (reference: 6px at ~640px diagonal)
         double diag = Math.Sqrt(canvasW * canvasW + canvasH * canvasH);
         double strokeW = Math.Max(2, diag / 107.0);

         // Build Polyline
         m_trackPolyline = new Polyline
         {
            Stroke = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
            StrokeThickness = strokeW,
            IsHitTestVisible = false
         };

         foreach (var p in m_trackData.Points)
         {
            var (rx, ry) = m_Rotate(p.X, p.Z);
            m_trackPolyline.Points.Add(new Point(
               rx * m_scale + m_offsetX,
               ry * m_scale + m_offsetY));
         }

         // Close the loop back to first point
         var first = m_trackData.Points[0];
         {
            var (rx, ry) = m_Rotate(first.X, first.Z);
            m_trackPolyline.Points.Add(new Point(
               rx * m_scale + m_offsetX,
               ry * m_scale + m_offsetY));
         }

         Canvas.SetZIndex(m_trackPolyline, 0);
         m_canv.Children.Insert(0, m_trackPolyline);

         // Start/finish line: a short line crossing the track orthogonally at point [0]
         if (m_trackPolyline.Points.Count >= 2)
         {
            var p0 = m_trackPolyline.Points[0];
            var p1 = m_trackPolyline.Points[1];
            double dx = p1.X - p0.X;
            double dy = p1.Y - p0.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len > 0.001)
            {
               // Perpendicular unit vector
               double perpX = -dy / len;
               double perpY = dx / len;
               double halfWidth = strokeW * 2;

               m_startFinishLine = new Line
               {
                  X1 = p0.X - perpX * halfWidth,
                  Y1 = p0.Y - perpY * halfWidth,
                  X2 = p0.X + perpX * halfWidth,
                  Y2 = p0.Y + perpY * halfWidth,
                  Stroke = Brushes.White,
                  StrokeThickness = Math.Max(2, strokeW * 0.5),
                  IsHitTestVisible = false
               };
               Canvas.SetZIndex(m_startFinishLine, 1);
               m_canv.Children.Add(m_startFinishLine);
            }
         }
      }

      private void m_RemovePolyline()
      {
         if (m_trackPolyline != null)
         {
            m_canv.Children.Remove(m_trackPolyline);
            m_trackPolyline = null;
         }
         if (m_startFinishLine != null)
         {
            m_canv.Children.Remove(m_startFinishLine);
            m_startFinishLine = null;
         }
      }

      // -- coordinate helpers --------------------------------------------------

      /// <summary>Rotate a world point around the point-cloud centroid.</summary>
      private (double rx, double ry) m_Rotate(double x, double y)
      {
         double dx = x - m_centroidX;
         double dy = y - m_centroidY;
         return (
            Math.Cos(m_rotRad) * dx - Math.Sin(m_rotRad) * dy + m_centroidX,
            Math.Sin(m_rotRad) * dx + Math.Cos(m_rotRad) * dy + m_centroidY
         );
      }

      /// <summary>Map world coordinates to canvas pixel coordinates.</summary>
      private Point m_WorldToCanvas(float worldX, float worldY)
      {
         var (rx, ry) = m_Rotate(worldX, worldY);
         return new Point(rx * m_scale + m_offsetX, ry * m_scale + m_offsetY);
      }

      // -- per-driver update -----------------------------------------------------

      private void m_UpdateDriver(DriverData d, Ellipse e, bool highlight)
      {
         m_ApplyTeamColor(d, e);

         e.Visibility = d.Present ? Visibility.Visible : Visibility.Collapsed;

         if (m_transformValid && d.TrackPosition3d != null)
         {
            var pt = m_WorldToCanvas(d.TrackPosition3d.x, d.TrackPosition3d.z);

            if (PitExaggeration && m_IsInPit(d))
               pt = m_PitExaggerate(pt);

            Canvas.SetLeft(e, pt.X - e.Width / 2);
            Canvas.SetTop(e, pt.Y - e.Height / 2);
         }
         else
         {
            // -- circle fallback --------------------------------------------------
            double xCenter = m_circleCenterX;
            double yCenter = m_circleCenterY;
            double r = m_circleRadius;

            xCenter -= e.Height / 2;
            yCenter -= e.Width / 2;

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
            }

            double rad = Math.PI * 2 * d.TrackPositionPerc - Math.PI / 2;
            Canvas.SetLeft(e, xCenter + r * Math.Cos(rad));
            Canvas.SetTop(e, yCenter + r * Math.Sin(rad));
         }

         Canvas.SetZIndex(e, highlight ? 26 : 25 - d.Pos);

         // Highlight / player blinking
         if (highlight)
         {
            e.StrokeThickness = DateTime.Now.Millisecond % 1000 < 500 ? 4.0 : 1.0;
         }
         else
         {
            e.StrokeThickness = 1.0;
         }

         if (d.IsPlayer && DateTime.Now.Millisecond % 1000 < 500)
         {
            e.Fill = Brushes.HotPink;
            Canvas.SetZIndex(e, 25);
         }
      }

      private void m_ApplyTeamColor(DriverData d, Ellipse e)
      {
         e.Stroke = Brushes.Black;

         if (PositionColorConverter.s_colors.ContainsKey(d.Team))
            e.Fill = PositionColorConverter.s_colors[d.Team];
      }

      private static bool m_IsInPit(DriverData d)
      {
         return d.Status == DriverStatus.Garage ||
                d.Status == DriverStatus.Pitlane ||
                d.Status == DriverStatus.Pitting;
      }

      /// <summary>
      /// Finds the nearest point on the track polyline to carPt (canvas space),
      /// then returns a new position where the offset from that point is scaled
      /// by s_pitExaggerationFactor, making the pit lane visually wider.
      /// </summary>
      private Point m_PitExaggerate(Point carPt)
      {
         double minDistSq = double.MaxValue;
         Point closestPt = carPt;

         var pts = m_trackPolyline.Points;
         for (int i = 0; i < pts.Count - 1; i++)
         {
            Point p0 = pts[i];
            Point p1 = pts[i + 1];
            double dx = p1.X - p0.X;
            double dy = p1.Y - p0.Y;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-10) continue;

            // Parameter t for the closest point on the segment [0..1]
            double t = ((carPt.X - p0.X) * dx + (carPt.Y - p0.Y) * dy) / lenSq;
            t = Math.Max(0.0, Math.Min(1.0, t));

            Point proj = new Point(p0.X + t * dx, p0.Y + t * dy);
            double distSq = (carPt.X - proj.X) * (carPt.X - proj.X)
                          + (carPt.Y - proj.Y) * (carPt.Y - proj.Y);

            if (distSq < minDistSq)
            {
               minDistSq = distSq;
               closestPt = proj;
            }
         }

         double offX = carPt.X - closestPt.X;
         double offY = carPt.Y - closestPt.Y;
         return new Point(closestPt.X + offX * s_pitExaggerationFactor,
                          closestPt.Y + offY * s_pitExaggerationFactor);
      }

      // -- interpolation -------------------------------------------------------

      private void m_OnNewData(DriverData[] dat)
      {
         // Compute t at transition point before resetting the clock
         double t = (DateTime.Now - m_lastNewDataTime).TotalMilliseconds
                    / (m_estimatedIntervalMs + s_interpolHeadroomMs);
         if (t > 1.0) t = 1.0;

         // Transition each driver: prev = last interpolated pos, curr = new data
         for (int i = 0; i < dat.Length; i++)
            m_TransitionInterpState(dat[i], m_interpStates[i], t);

         // Update ring buffer and recompute estimated interval
         DateTime now = DateTime.Now;
         m_arrivalRing[m_arrivalHead] = now;
         m_arrivalHead = (m_arrivalHead + 1) % 10;
         DateTime oldest = m_arrivalRing[m_arrivalHead];   // head now points to oldest entry
         m_estimatedIntervalMs = Math.Max(50.0, (now - oldest).TotalMilliseconds / 9.0);
         m_lastNewDataTime = now;
      }

      private void m_TransitionInterpState(DriverData d, DriverInterpState s, double t)
      {
         if (!s.Initialized)
         {
            if (d.TrackPosition3d != null)
            {
               s.PrevX = d.TrackPosition3d.x;
               s.PrevZ = d.TrackPosition3d.z;
               s.CurrX = d.TrackPosition3d.x;
               s.CurrZ = d.TrackPosition3d.z;
            }
            s.PrevPerc = d.TrackPositionPerc;
            s.CurrPerc = d.TrackPositionPerc;
            s.Initialized = true;
            return;
         }

         // prev = last interpolated position
         s.PrevPerc = m_LerpPerc(s.PrevPerc, s.CurrPerc, t);
         if (d.TrackPosition3d != null)
         {
            s.PrevX = s.PrevX + (s.CurrX - s.PrevX) * (float)t;
            s.PrevZ = s.PrevZ + (s.CurrZ - s.PrevZ) * (float)t;
            s.CurrX = d.TrackPosition3d.x;
            s.CurrZ = d.TrackPosition3d.z;
         }
         s.CurrPerc = d.TrackPositionPerc;
      }

      private void m_UpdateDriverInterp(DriverData d, DriverInterpState s, Ellipse e, bool highlight, double t)
      {
         m_ApplyTeamColor(d, e);

         e.Visibility = d.Present ? Visibility.Visible : Visibility.Collapsed;

         if (m_transformValid && d.TrackPosition3d != null)
         {
            float ix = s.Initialized
               ? s.PrevX + (s.CurrX - s.PrevX) * (float)t
               : d.TrackPosition3d.x;
            float iz = s.Initialized
               ? s.PrevZ + (s.CurrZ - s.PrevZ) * (float)t
               : d.TrackPosition3d.z;

            var pt = m_WorldToCanvas(ix, iz);

            if (PitExaggeration && m_IsInPit(d))
               pt = m_PitExaggerate(pt);

            Canvas.SetLeft(e, pt.X - e.Width / 2);
            Canvas.SetTop(e, pt.Y - e.Height / 2);
         }
         else
         {
            double xCenter = m_circleCenterX - e.Height / 2;
            double yCenter = m_circleCenterY - e.Width / 2;
            double r = m_circleRadius;

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
            }

            float perc = s.Initialized ? m_LerpPerc(s.PrevPerc, s.CurrPerc, t) : d.TrackPositionPerc;
            double rad = Math.PI * 2 * perc - Math.PI / 2;
            Canvas.SetLeft(e, xCenter + r * Math.Cos(rad));
            Canvas.SetTop(e, yCenter + r * Math.Sin(rad));
         }

         Canvas.SetZIndex(e, highlight ? 26 : 25 - d.Pos);

         if (highlight)
            e.StrokeThickness = DateTime.Now.Millisecond % 1000 < 500 ? 4.0 : 1.0;
         else
            e.StrokeThickness = 1.0;

         if (d.IsPlayer && DateTime.Now.Millisecond % 1000 < 500)
         {
            e.Fill = Brushes.HotPink;
            Canvas.SetZIndex(e, 25);
         }
      }

      /// <summary>
      /// Linearly interpolates between two TrackPositionPerc values, correctly
      /// handling the wrap-around at the start/finish line (0.0 / 1.0 boundary).
      /// </summary>
      private static float m_LerpPerc(float prev, float curr, double t)
      {
         float delta = curr - prev;
         if (delta > 0.5f) delta -= 1.0f;    // car crossed S/F going backward (rare)
         if (delta < -0.5f) delta += 1.0f;   // car crossed S/F going forward
         float result = prev + delta * (float)t;
         if (result < 0.0f) result += 1.0f;
         if (result >= 1.0f) result -= 1.0f;
         return result;
      }

      // -- ellipse pool --------------------------------------------------

      private double m_ComputeEllipseSize()
      {
         double w = ActualWidth > 1 ? ActualWidth : 400;
         double h = ActualHeight > 1 ? ActualHeight : 500;
         double diag = Math.Sqrt(w * w + h * h);
         return Math.Max(12, diag * 0.0125);
      }

      private void m_UpdateEllipseSizes()
      {
         double sz = m_ComputeEllipseSize();
         foreach (var e in m_ellipses)
         {
            e.Width = sz;
            e.Height = sz;
         }
      }

      private void m_SetSize(int size)
      {
         if (m_ellipses.Length == size) return;

         var next = new Ellipse[size];

         int copy = Math.Min(m_ellipses.Length, size);
         for (int i = 0; i < copy; i++)
            next[i] = m_ellipses[i];

         // Add new ellipses
         for (int i = copy; i < size; i++)
         {
            next[i] = m_CreateEllipse();
            m_canv.Children.Add(next[i]);
         }

         // Remove surplus ellipses
         for (int i = size; i < m_ellipses.Length; i++)
            m_canv.Children.Remove(m_ellipses[i]);

         m_ellipses = next;

         var nextStates = new DriverInterpState[size];
         for (int i = 0; i < Math.Min(m_interpStates.Length, size); i++)
            nextStates[i] = m_interpStates[i];
         for (int i = m_interpStates.Length; i < size; i++)
            nextStates[i] = new DriverInterpState();
         m_interpStates = nextStates;
      }

      private Ellipse m_CreateEllipse()
      {
         double sz = m_ComputeEllipseSize();
         return new Ellipse
         {
            Height = sz,
            Width = sz,
            Fill = Brushes.Black,
            Stroke = Brushes.Black,
            StrokeThickness = 1.0
         };
      }

      // -- types --------------------------------------------------

      private class DriverInterpState
      {
         public bool  Initialized = false;
         public float PrevX,  PrevZ,  PrevPerc;
         public float CurrX,  CurrZ,  CurrPerc;
      }

      // -- fields --------------------------------------------------

      private const double s_pitExaggerationFactor = 2.5;
      private const double s_marginFraction        = 0.10;   // 10% margin on each side
      private const double s_interpolHeadroomMs    = 100.0;  // extra buffer to absorb jitter

      private Ellipse[] m_ellipses = new Ellipse[0];
      private Line m_playerMark = new Line();
      private SolidColorBrush m_brushTr = new SolidColorBrush(Color.FromRgb(10, 100, 150));

      // Track rendering
      private Track m_activeTrack = Track.Unknown;
      private TrackMapData m_trackData = null;
      private Polyline m_trackPolyline = null;
      private Line m_startFinishLine = null;

      // Transform (computed once per track load / canvas resize)
      private bool m_transformValid = false;
      private double m_scale;
      private double m_offsetX;
      private double m_offsetY;
      private double m_rotRad;
      private double m_centroidX;
      private double m_centroidY;

      // Circle fallback geometry (recomputed on SizeChanged)
      private double m_circleCenterX = 200;
      private double m_circleCenterY = 300;
      private double m_circleRadius  = 187;

      // Interpolation state
      private DriverInterpState[] m_interpStates     = new DriverInterpState[0];
      private DateTime[]          m_arrivalRing;            // ring buffer: 10 arrival timestamps
      private int                 m_arrivalHead      = 0;
      private double              m_estimatedIntervalMs = 100.0;
      private DateTime            m_lastNewDataTime;
   }
}
