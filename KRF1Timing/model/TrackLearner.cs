// Copyright 2018-2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Collections.Generic;

namespace adjsw.F12026
{
   /// <summary>
   /// State machine that captures one complete lap of world-coordinate points
   /// and saves them as a TrackMapData JSON.
   ///
   /// State flow:
   ///   Idle ---- [T pressed] ----> WaitingForLap
   ///   WaitingForLap ---- [LapNr changes] ----> Recording   (buffer cleared)
   ///   Recording ---- [LapNr changes] ----> Recording       (lap saved to disk; new lap starts)
   ///   Any active state ---- [SessionUID changes] ----> WaitingForLap (buffers cleared)
   ///   Any active state ---- [T pressed] ----> Idle
   /// </summary>
   public class TrackLearner
   {
      public enum LearnerState { Idle, WaitingForLap, Recording }

      public LearnerState State { get; private set; } = LearnerState.Idle;
      public bool IsActive => State != LearnerState.Idle;

      /// <summary>Status / info messages for the UI info-box.</summary>
      public event Action<string> StatusChanged;

      /// <summary>
      /// Toggle learning mode on/off.
      /// currentTrack and trackName are used when activating (Idle -> WaitingForLap).
      /// Completed laps are saved automatically; pressing T to stop just ends the session.
      /// </summary>
      public void Toggle(Track currentTrack, string trackName)
      {
         if (State == LearnerState.Idle)
         {
            m_currentTrack = currentTrack;
            m_trackName    = trackName;
            m_lastLapNr    = -1;
            m_currentBuffer.Clear();
            State = LearnerState.WaitingForLap;
            StatusChanged?.Invoke("Track Learning ON - waiting for lap start...");
         }
         else
         {
            State = LearnerState.Idle;
            m_currentBuffer.Clear();
            StatusChanged?.Invoke("Track Learning OFF.");
         }
      }

      /// <summary>
      /// Call when the session UID changes.
      /// Resets the state machine to WaitingForLap so the next lap of the new
      /// track is captured cleanly.  Track info is refreshed on the first
      /// subsequent Update() call, guaranteeing the session packets have arrived.
      /// No-op when not active.
      /// </summary>
      public void NotifySessionChanged()
      {
         if (!IsActive)
            return;

         m_currentBuffer.Clear();
         m_lastLapNr = -1;
         State = LearnerState.WaitingForLap;
         StatusChanged?.Invoke("Session changed - restarting track learning...");
      }

      /// <summary>
      /// Call once per UI tick.
      /// currentTrack and trackName are refreshed every tick while WaitingForLap,
      /// ensuring correct track info by the time recording actually starts.
      /// Pass the first present driver (the only car in Time Trial).
      /// </summary>
      public void Update(DriverData driver, Track currentTrack, string trackName)
      {
         if (State == LearnerState.Idle || driver?.TrackPosition3d == null)
            return;

         int lapNr = driver.LapNr;

         switch (State)
         {
            case LearnerState.WaitingForLap:
               // Keep track info current while waiting - session packets may still be arriving
               m_currentTrack = currentTrack;
               m_trackName    = trackName;

               if (m_lastLapNr < 0)
               {
                  m_lastLapNr = lapNr; // anchor
                  return;
               }
               if (lapNr != m_lastLapNr)
               {
                  m_currentBuffer.Clear();
                  m_lastLapNr = lapNr;
                  State = LearnerState.Recording;
                  StatusChanged?.Invoke($"Track Learning - recording lap {lapNr}...");
               }
               break;

            case LearnerState.Recording:
               if (lapNr != m_lastLapNr)
               {
                  // Lap complete - save immediately so the user never has to tab out
                  SaveCurrentBuffer();
                  m_currentBuffer.Clear();
                  m_lastLapNr = lapNr;
                  // this would remove the save information... -> StatusChanged?.Invoke("Recording next lap...");
               }
               else
               {
                  var pos = driver.TrackPosition3d;
                  m_currentBuffer.Add(new TrackPoint { X = pos.x, Z = pos.z });
               }
               break;
         }
      }

      private void SaveCurrentBuffer()
      {
         if (m_currentBuffer.Count == 0)
            return;

         var points = new List<TrackPoint>(m_currentBuffer);

         // Pass 1 - minimum distance: drop points closer than 20 cm (0.2 m)
         points = ThinByMinDistance(points, 0.2f);

         // Pass 2 - directional redundancy: drop points where the heading barely
         //          changes, but never leave a gap larger than 25 m.
         points = ThinByDirection(points, maxSpacingM: 25f, angleThresholdDeg: 5f);

         var data = new TrackMapData
         {
            TrackId         = (int)m_currentTrack,
            TrackName       = m_trackName,
            RotationDegrees = 0.0,
            Points          = points
         };
         TrackMapStore.Save(data);

         StatusChanged?.Invoke(
            $"Track saved: {m_trackName}  " +
            $"({m_currentBuffer.Count} -> {points.Count} points after thinning)");
      }

      /// <summary>
      /// Pass 1: remove any point whose distance from the previously kept point
      /// is less than <paramref name="minDistM"/> metres.
      /// </summary>
      private static List<TrackPoint> ThinByMinDistance(List<TrackPoint> pts, float minDistM)
      {
         if (pts.Count == 0) return pts;

         float minDistSq = minDistM * minDistM;
         var result = new List<TrackPoint> { pts[0] };

         for (int i = 1; i < pts.Count; i++)
         {
            var last = result[result.Count - 1];
            float dx = pts[i].X - last.X;
            float dz = pts[i].Z - last.Z;
            if (dx * dx + dz * dz >= minDistSq)
               result.Add(pts[i]);
         }
         return result;
      }

      /// <summary>
      /// Pass 2: greedy directional thinning.
      /// A candidate point is kept when either
      ///   (a) its distance from the last kept point reaches <paramref name="maxSpacingM"/>, or
      ///   (b) the heading change from the previous segment exceeds
      ///       <paramref name="angleThresholdDeg"/> degrees.
      /// This collapses long straights down to one point every maxSpacingM metres
      /// while preserving every meaningful corner.
      /// </summary>
      private static List<TrackPoint> ThinByDirection(
         List<TrackPoint> pts, float maxSpacingM, float angleThresholdDeg)
      {
         if (pts.Count < 3) return pts;

         float threshRad = angleThresholdDeg * (float)(Math.PI / 180.0);
         var result = new List<TrackPoint> { pts[0] };

         for (int i = 1; i < pts.Count; i++)
         {
            var   last = result[result.Count - 1];
            float dx   = pts[i].X - last.X;
            float dz   = pts[i].Z - last.Z;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);

            // (a) force-keep when max spacing is reached
            if (dist >= maxSpacingM)
            {
               result.Add(pts[i]);
               continue;
            }

            // (b) keep when the heading changes enough
            if (result.Count >= 2)
            {
               var   prev = result[result.Count - 2];
               float pdx  = last.X - prev.X;
               float pdz  = last.Z - prev.Z;
               float pLen = (float)Math.Sqrt(pdx * pdx + pdz * pdz);

               if (pLen > 1e-4f && dist > 1e-4f)
               {
                  float dot   = (pdx * dx + pdz * dz) / (pLen * dist);
                  dot         = Math.Max(-1f, Math.Min(1f, dot));
                  float angle = (float)Math.Acos(dot);

                  if (angle >= threshRad)
                     result.Add(pts[i]);
                  // else: heading barely changed - skip
               }
               else
               {
                  result.Add(pts[i]); // degenerate segment, keep to be safe
               }
            }
            else
            {
               result.Add(pts[i]); // need at least 2 points to establish a heading
            }
         }

         return result;
      }

      private Track            m_currentTrack;
      private string           m_trackName;
      private int              m_lastLapNr     = -1;
      private List<TrackPoint> m_currentBuffer = new List<TrackPoint>();
   }
}
