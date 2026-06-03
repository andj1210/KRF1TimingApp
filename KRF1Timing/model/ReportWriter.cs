// Copyright 2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.IO;
using System.Text;

namespace adjsw.F12026
{
   /// <summary>
   /// Static utility for saving race reports (text + JSON) from mapper state.
   /// </summary>
   public static class ReportWriter
   {
      /// <summary>
      /// Save a plain-text race report. Returns the file path on success,
      /// or null if there was no event data.
      /// </summary>
      public static string SaveReport(F1UdpClrMapper mapper, string appTitle)
      {
         var session      = mapper.SessionInfo;
         var countDrivers = mapper.CountDrivers;
         var drivers      = mapper.Drivers;
         var events       = mapper.EventList.Events;
         string nl        = "\r\n";

         if (events.Count == 0)
            return null;

         StringBuilder sb = new StringBuilder();
         var sep = "--------------------------------------------------------------";

         // header
         sb.Append("Racereport by " + appTitle + nl);
         sb.Append(session.EventTrack.ToString("g") + " " + session.Session.ToString("g") + nl + events[0].TimeCode + nl);
         sb.Append(session.TotalLaps + " Laps" + nl);

         // classification
         sb.Append(nl + nl + nl + "--------------------------------------CLASSIFICATION----------------------------------" + nl);
         if (mapper.Classification == null)
         {
            sb.Append("No race result available" + nl);
         }
         else
         {
            int maxDriverNameLen = 4; // "Name"
            ClassificationData winner = null;
            foreach (var result in mapper.Classification)
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

            for (int i = 0; i < mapper.Classification.Length; ++i)
            {
               foreach (var result in mapper.Classification)
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
                  sb.Append(string.Format("| {0,-" + maxDriverNameLen + "} ", result.Driver.Name));
                  sb.Append(string.Format("|  {0,2}  ", result.NumLaps));

                  sb.Append("| " + FormatRaceTime(result.TotalRaceTime) + " ");

                  if (i == 0)
                  {
                     sb.Append("| ----------  ");
                  }
                  else
                  {
                     if (result.NumLaps == leaderLaps)
                        sb.Append("| " + FormatRaceTime(result.TotalRaceTime - leaderTimeTrack) + " ");
                     else
                        sb.Append("|   +" + (leaderLaps - result.NumLaps).ToString("D2") + "L      ");
                  }

                  if (result.PenaltiesTime > 0)
                     sb.Append("| " + string.Format("{0,2}s ", result.PenaltiesTime));
                  else
                     sb.Append("|     ");

                  sb.Append("| " + FormatRaceTime(result.TotalRaceTime + result.PenaltiesTime) + " ");

                  if (i == 0)
                  {
                     sb.Append("| ----------  |");
                  }
                  else
                  {
                     if (result.NumLaps == leaderLaps)
                        sb.Append("| " + FormatRaceTime(result.TotalRaceTime + result.PenaltiesTime - leaderTimeTotal) + " |");
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

         // Incidents
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
                  // don't care
                  break;
            }
         }
         sb.Append(sep + nl);

         string filePath = GetReportPath("_report.txt");
         File.WriteAllText(filePath, sb.ToString());
         return filePath;
      }

      /// <summary>
      /// Save a JSON race report. Returns the file path on success,
      /// or null if no classification is available.
      /// </summary>
      public static string SaveReportJson(F1UdpClrMapper mapper)
      {
         if (mapper.Classification == null)
            return null;

         var json = new ResultExport();
         json.Events     = mapper.EventList;
         json.EventTrack = mapper.SessionInfo.EventTrack;
         json.TotalLaps  = mapper.SessionInfo.TotalLaps;
         json.Session    = mapper.SessionInfo.Session;

         // merge drivers into the reduced export model
         json.Drivers = new DriverDataResult[mapper.Classification.Length];

         for (int i = 0; i < mapper.Classification.Length; ++i)
         {
            json.Drivers[i] = new DriverDataResult();
            DriverDataResult driverResult         = json.Drivers[i];
            ClassificationData classification     = mapper.Classification[i];
            DriverData driverSession              = mapper.Classification[i].Driver;

            driverResult.DriverNr       = driverSession.DriverNr;
            driverResult.Team           = driverSession.Team;
            driverResult.Name           = driverSession.Name;
            driverResult.PitPenalties   = driverSession.PitPenalties;
            driverResult.VisualTyres    = driverSession.VisualTyres;

            driverResult.Pos              = classification.Position;
            driverResult.PenaltySeconds   = classification.PenaltiesTime;
            driverResult.RaceTimeOnTrack  = (int)(classification.TotalRaceTime * 1000 + 0.5);

            driverResult.Laps = new LapData[classification.NumLaps];
            for (int j = 0; j < driverResult.Laps.Length; ++j)
            {
               driverResult.Laps[j] = driverSession.Laps[j];
            }

            driverResult.BugtimeRacedirector       = 0;
            driverResult.PenaltySecondsRacedirector = 0;
         }

         string jsonText = Newtonsoft.Json.JsonConvert.SerializeObject(json, Newtonsoft.Json.Formatting.Indented);
         string filePath = GetReportPath("_report.json");
         File.WriteAllText(filePath, jsonText);
         return filePath;
      }

      // -- helpers ----------------------------------------------------------

      /// <summary>
      /// Format seconds as "H:MM:SS.mmm" (e.g. "1:23:45.678").
      /// </summary>
      public static string FormatRaceTime(double inputSeconds)
      {
         int hour         = (int)inputSeconds / 3600;
         inputSeconds    -= hour * 3600.0;
         int minutes      = (int)inputSeconds / 60;
         int seconds      = (int)inputSeconds % 60;
         int milliseconds = (int)((inputSeconds % 1) * 1000);

         return string.Format("{0,1}:{1,2:00}:{2,2:00}.{3:000}",
            hour, minutes, seconds, milliseconds);
      }

      private static string GetReportPath(string suffix)
      {
         string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reports");
         try { Directory.CreateDirectory(dir); }
         catch { }
         return Path.Combine(dir, DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + suffix);
      }
   }
}
