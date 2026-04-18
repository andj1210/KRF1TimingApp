// Copyright 2018-2020 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using adjsw.F12025;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Navigation;

namespace adjsw.F12025
{
   public class PositionConverter : IMultiValueConverter
   {
      public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         var dat = values?[1] as DriverData;

         if (null == dat)
            return "?";

         if (dat.Pos < 10)
            return " " + dat.Pos + "|";
         else
            return "" + dat.Pos + "|";
      }

      public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
      {
         throw new NotImplementedException();
      }
   }

   public class RaceEventTextConverter : IMultiValueConverter
   {
      public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         var dat = values?[0] as SessionInfo;

         if (null == dat)
            return "?";

         String text = "--- ";
         text += dat.EventTrack.ToString("g");
         text += " ";
         text += dat.Session.ToString("g");


         switch (dat.Session)
         {
            case SessionType.Unknown:
               break;
            case SessionType.P1:
               text += "(";
               text += TimeSpan.FromSeconds(dat.RemainingTime).ToString("c");
               text += ")";
               break;
            case SessionType.P2:
               text += "(";
               text += TimeSpan.FromSeconds(dat.RemainingTime).ToString("c");
               text += ")";
               break;
            case SessionType.P3:
               text += "(";
               text += TimeSpan.FromSeconds(dat.RemainingTime).ToString("c");
               text += ")";
               break;
            case SessionType.ShortPractice:
               text += "(";
               text += TimeSpan.FromSeconds(dat.RemainingTime).ToString("c");
               text += ")";
               break;
            case SessionType.Q1:
            case SessionType.Q2:
            case SessionType.Q3:
            case SessionType.SprintShootout1:
            case SessionType.SprintShootout2:
            case SessionType.SprintShootout3:
            case SessionType.ShortQ:
            case SessionType.ShortSprintShootout:
               text += " (";
               text += TimeSpan.FromSeconds(dat.RemainingTime).ToString("c");
               text += ")";
               break;
            case SessionType.OSQ:
            case SessionType.OneShotSprintShootout:
               break;
            case SessionType.Race:
            case SessionType.Race2:
            case SessionType.Race3:
               text += " - Lap ";
               text += dat.CurrentLap;
               text += " / ";
               text += dat.TotalLaps;
               break;
            case SessionType.TimeTrial:
               break;
         }

         text += " ---";
         return text;
      }

      public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
      {
         throw new NotImplementedException();
      }
   }

   public class PositionColorConverter : IMultiValueConverter
   {
      public static readonly Dictionary<F1Team, SolidColorBrush> s_colors = new Dictionary<F1Team, SolidColorBrush>      
      {
         { F1Team.Mercedes, new SolidColorBrush(Color.FromRgb(0, 215, 182)) },
         { F1Team.Ferrari, new SolidColorBrush(Color.FromRgb(237, 17, 49)) },
         { F1Team.McLaren, new SolidColorBrush(Color.FromRgb(244, 118, 0)) },
         { F1Team.RedBull, new SolidColorBrush(Color.FromRgb(53, 21, 140))  },
         { F1Team.Williams, new SolidColorBrush(Color.FromRgb(24, 104, 219))  },
         { F1Team.AstonMartin, new SolidColorBrush(Color.FromRgb(36, 107, 53)) },
         { F1Team.Alpine, new SolidColorBrush(Color.FromRgb(0, 161, 232)) },
         { F1Team.RacingBulls, new SolidColorBrush(Color.FromRgb(48, 20, 181)) },
         { F1Team.Haas, new SolidColorBrush(Color.FromRgb(156, 159, 162)) },
         { F1Team.Sauber, new SolidColorBrush(Color.FromRgb(0, 255, 60))  },
         { F1Team.Classic, Brushes.Gray },
      };

      public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         var dat = values?[1] as DriverData;

         if (null == dat)
            return "?";

         if (s_colors.ContainsKey(dat.Team))
            return s_colors[dat.Team];

         return Brushes.Gray;
      }

      public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
      {
         throw new NotImplementedException();
      }
   }

   public abstract class QualifyingAwareConverter : IMultiValueConverter
   {
      public bool IsQualy { get; set; }
      public bool ShowDelta { get; set; }
      public abstract object Convert(object[] values, Type targetType, object parameter, CultureInfo culture);
      public abstract object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture);
   }

   public class LeaderAndDeltaConverter : QualifyingAwareConverter
   {
      public override object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         var dat = values?[4] as DriverData;

         if (null == dat)
            return "?";

         if (!dat.Present)
            return "--------";

         switch (dat.Status)
         {
            case DriverStatus.DNF:
               return "--------";
            case DriverStatus.DSQ:
               return "--------";
         }

         if (dat.Pos != 1)
         {
            float time = ShowDelta ? dat.TimedeltaToNext : dat.TimedeltaToLeader;

            if (time > 0)
               return string.Format(CultureInfo.InvariantCulture, " {0,7:##0.000}", (time + 0.0005));

            else if (time < 0)
            {
               int lapped = (int)(time - 0.5);
               lapped *= -1;
               if (lapped > 9)
                  return "    +" + lapped + "L";
               else
                  return "     +" + lapped + "L";

            }

            else
            {
               return "--------";
            }
         }
         else
         {
            if (IsQualy)
            {
               return dat.FastestLap.To_M_SS_MMMM(dat.FastestLap.Lap);
            }
            else
            {
               return "--------";
            }
         }
      }

      public override object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
      {
         throw new NotImplementedException();
      }
   }

   public class FastestLapConverter : IMultiValueConverter
   {
      public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         var dat = values?[1] as DriverData;

         if ((dat == null) || dat.FastestLap == null || (dat.FastestLap.Lap < 1) || ((parameter as string) == null))
            return "";

         string sector = parameter as string;
         UInt32 value = 0;
         switch (sector)
         {
            case "1":
               value = dat.FastestLap.Sector1Ms;
               break;

            case "2":
               value = dat.FastestLap.Sector2Ms;
               break;

            case "3":
               value = dat.FastestLap.Sector3Ms;
               break;

            default:
               return "";
         }

         return dat.FastestLap.To_SS_MMMM(value);
      }

      public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
      {
         throw new NotImplementedException();
      }
   }

   public class StatusConverter : QualifyingAwareConverter
   {
      public override object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         StatusView.Setter setter = new StatusView.Setter();

         var dat = values?[0] as DriverData;
         if (null == dat)
         {
            setter.SpecialText = "|?";
            return setter;
         }

         driver = dat;
         setter.DriverId = driver.Id;
         setter.Player = dat.IsPlayer || dat.IsMainDriver || dat.IsSecondaryDriver;
         this.setter = setter;

         if (setter.Player && !IsQualy)
            setter.SpecialText = 
               "<---";

         if (!dat.Present)
            setter.SpecialText = 
               "*DNF*";

         switch (dat.Status)
         {
            case DriverStatus.DNF:
            case DriverStatus.DSQ:
               setter.SpecialText = "*DNF*";
               break;
            case DriverStatus.Garage:
               setter.SpecialText = "GARAGE";
               break;

            case DriverStatus.OnTrack:
               // show actual delta
               break;
            case DriverStatus.Pitlane:
               setter.SpecialText = "-PIT-";
               break;

            case DriverStatus.Pitting:
               setter.SpecialText = "-PIT-";
               break;
            case DriverStatus.OutLap:
               if (IsQualy)
                  setter.SpecialText = "OUTLAP";
               break;

            case DriverStatus.Inlap:
               if (IsQualy)
                  setter.SpecialText = "INLAP";
               break;

            case DriverStatus.Retired:
               setter.SpecialText = "RETIRED";
               break;
         }

         if (!string.IsNullOrEmpty(setter.SpecialText))
            return setter;

         if (IsQualy)
         {
            return ConvertQualy(values, targetType, parameter, culture);
         }
         else
         {
            return ConvertRace(values, targetType, parameter, culture);
         }
      }

      public override object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
      {
         throw new NotImplementedException();
      }

      public object ConvertQualy(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         setter.Quali = true;
         if (driver.FastestLap.Lap < 1.0)
         {
            if (driver.CurrentLap.Sector1 != 0)
               setter.S1 = (driver.CurrentLap.Sector1 <= driver.Session.FastestSector1) ? StatusView.SetterSectorType.Purple :  StatusView.SetterSectorType.Green;
            else
               setter.S1 = StatusView.SetterSectorType.None;

            if (driver.CurrentLap.Sector2 != 0)
               setter.S2 = (driver.CurrentLap.Sector2 <= driver.Session.FastestSector2) ? StatusView.SetterSectorType.Purple : StatusView.SetterSectorType.Green;
            else
               setter.S2 = StatusView.SetterSectorType.None;


            if (driver.CurrentLap.Sector3 != 0)
               setter.S3 = (driver.CurrentLap.Sector3 <= driver.Session.FastestSector3) ? StatusView.SetterSectorType.Purple : StatusView.SetterSectorType.Green;
            else
               setter.S3 = StatusView.SetterSectorType.None;

            if (driver.CurrentLap.Invalid)
            {
               setter.LapInvalid = true;
            }

            setter.Delta = 0;            
         }

         else
         {
            if (driver.CurrentLap.Sector1 != 0)
            {
               setter.S1 = driver.CurrentLap.Sector1 < driver.FastestLap.Sector1 ? StatusView.SetterSectorType.Green : StatusView.SetterSectorType.Yellow;
               if (driver.CurrentLap.Sector1 <= driver.Session.FastestSector1)
                  setter.S1 = StatusView.SetterSectorType.Purple;

               if (((int)driver.CurrentLap.Sector1Ms - driver.FastestLap.Sector1Ms) > 1250)
               {
                  setter.S1 = StatusView.SetterSectorType.Red;
               }
            }               
            else
               setter.S1 = StatusView.SetterSectorType.None;

            if (driver.CurrentLap.Sector2 != 0)
            {
               setter.S2 = driver.CurrentLap.Sector2 < driver.FastestLap.Sector2 ? StatusView.SetterSectorType.Green : StatusView.SetterSectorType.Yellow;
               if (driver.CurrentLap.Sector2 <= driver.Session.FastestSector2)
                  setter.S2 = StatusView.SetterSectorType.Purple;

               if (((int)driver.CurrentLap.Sector2Ms - driver.FastestLap.Sector2Ms) > 1250)
               {
                  setter.S2 = StatusView.SetterSectorType.Red;
               }
            }
            else
               setter.S2 = StatusView.SetterSectorType.None;


            if (driver.CurrentLap.Sector3Ms != 0)
            {
               setter.S3 = driver.CurrentLap.Sector3 < driver.FastestLap.Sector3 ? StatusView.SetterSectorType.Green : StatusView.SetterSectorType.Yellow;
               if (driver.CurrentLap.Sector3 <= driver.Session.FastestSector3)
                  setter.S3 = StatusView.SetterSectorType.Purple;

               if (((int)driver.CurrentLap.Sector3Ms - driver.FastestLap.Sector3Ms) > 1250)
               {
                  setter.S3 = StatusView.SetterSectorType.Red;
               }
            }
               
            else
               setter.S3 = StatusView.SetterSectorType.None;

            if (driver.CurrentLap.Sector1Ms == 0)
            {
               setter.Delta = 0;
            }
            else
            {
               Int32 delta = (int)driver.CurrentLap.Sector1Ms - (int)driver.FastestLap.Sector1Ms;


               if (driver.CurrentLap.Sector2Ms > 0)
               {
                  delta += (int)driver.CurrentLap.Sector2Ms - (int)driver.FastestLap.Sector2Ms;
               }

               if (driver.CurrentLap.Sector3Ms > 0)
               {
                  delta += (int)driver.CurrentLap.Sector3Ms - (int)driver.FastestLap.Sector3Ms;
               }
               setter.Delta = delta;
            }

            if (driver.CurrentLap.Invalid)
            {
               setter.LapInvalid = true;
            }
         }

         m_ComputeSectorProgress(setter, driver);
         return setter;
      }

      private void m_ComputeSectorProgress(StatusView.Setter setter, DriverData driver)
      {
         float pos      = driver.TrackPositionPerc;
         float s2Start  = driver.Session.Sector2Start;
         float s3Start  = driver.Session.Sector3Start;

         if (pos <= 0f || s2Start <= 0f || s3Start <= 0f)
         {
            setter.SectorProgress = -1f;
            return;
         }

         if (setter.S1 == StatusView.SetterSectorType.None && setter.S2 == StatusView.SetterSectorType.None && setter.S3 == StatusView.SetterSectorType.None)
         {
            // S1 in progress
            setter.SectorProgress = Math.Min(pos / s2Start, 1f);
         }
         else if (setter.S2 == StatusView.SetterSectorType.None && setter.S3 == StatusView.SetterSectorType.None)
         {
            // S2 in progress
            float sectorLen = s3Start - s2Start;
            setter.SectorProgress = sectorLen > 0f ? Math.Min((pos - s2Start) / sectorLen, 1f) : -1f;
         }
         else if (setter.S3 == StatusView.SetterSectorType.None)
         {
            // S3 in progress
            float sectorLen = 1f - s3Start;
            setter.SectorProgress = sectorLen > 0f ? Math.Min((pos - s3Start) / sectorLen, 1f) : -1f;
         }
         else
         {
            // all sectors complete
            setter.SectorProgress = -1f;
         }
      }

      public object ConvertRace(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         setter.Quali = false;
         setter.Delta = 0;
         return setter;
      }

      private DriverData driver;
      private StatusView.Setter setter;
   }

   public class TyreAgeConverter : IMultiValueConverter
   {
      public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         var dat = values?[1] as DriverData;

         if (null == dat)
            return "?";

         if (dat.TyreAge < 10)
            return " " + dat.TyreAge + "L";
         else
            return "" + dat.TyreAge + "L";
      }

      public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
      {
         throw new NotImplementedException();
      }
   }
   public class PenaltyConverter : IMultiValueConverter
   {
      public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         var dat = values?[1] as DriverData;

         if (null == dat)
            return "?";
         if (dat.PenaltySeconds > 0)
            return "" + dat.PenaltySeconds;

         return "";
      }

      public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
      {
         throw new NotImplementedException();
      }
   }

   public class PitPenaltyConverter : IMultiValueConverter
   {
      public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         var dat = values?[1] as DriverData;

         if (null == dat)
            return "";

         string penaltyStr = "";

         foreach (var penalty in dat.PitPenalties)
         {
            switch (penalty.PenaltyType)
            {
               case PenaltyTypes.DriveThrough:
                  if (!string.IsNullOrEmpty(penaltyStr))
                     penaltyStr += ";";

                  penaltyStr = penaltyStr + (penalty.PenaltyServed ? "(" : "") + "DT" + (penalty.PenaltyServed ? ")" : "");
                  break;
               case PenaltyTypes.StopGo:
                  if (!string.IsNullOrEmpty(penaltyStr))
                     penaltyStr += ";";

                  penaltyStr = penaltyStr + (penalty.PenaltyServed ? "(" : "") + "SG" + (penalty.PenaltyServed ? ")" : "");
                  break;
               case PenaltyTypes.GridPenalty:
                  if (!string.IsNullOrEmpty(penaltyStr))
                     penaltyStr += ";";

                  penaltyStr = penaltyStr + "GRD";
                  break;
               case PenaltyTypes.Disqualified:
                  if (!string.IsNullOrEmpty(penaltyStr))
                     penaltyStr += ";";

                  penaltyStr = penaltyStr + "DSQ";
                  break;
               case PenaltyTypes.Retired:
                  if (!string.IsNullOrEmpty(penaltyStr))
                     penaltyStr += ";";

                  penaltyStr = penaltyStr + "DNF";
                  break;
            }
         }

         return penaltyStr;
      }

      public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
      {
         throw new NotImplementedException();
      }
   }

   public class DeltaTimeBgColorConverter : IMultiValueConverter
   {
      public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         var dat = values?[1] as DriverData;

         if (null == dat)
            return Brushes.Transparent;

         if (dat.IsPlayer)
            return Brushes.DarkViolet;

         return Brushes.Transparent;
      }

      public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
      {
         throw new Exception("The method or operation is not implemented.");
      }
   }

   public class TyreConverter : IMultiValueConverter
   {
      public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         var dat = values?[2] as DriverData;

         if (null == dat)
            return new TyreView(TyreView.OtherTyre);

         WrapPanel wp = new WrapPanel();
         if (dat.VisualTyres.Count >= 1)
         {
            foreach (F1VisualTyre tyre in dat.VisualTyres)
            {
               switch (tyre)
               {
                  case F1VisualTyre.Soft:
                     wp.Children.Add(new TyreView(TyreView.SoftTyre));
                     break;

                  case F1VisualTyre.Medium:
                     wp.Children.Add(new TyreView(TyreView.MediumTyre));
                     break;

                  case F1VisualTyre.Hard:
                     wp.Children.Add(new TyreView(TyreView.HardTyre));
                     break;

                  case F1VisualTyre.Intermediate:
                     wp.Children.Add(new TyreView(TyreView.InterTyre));
                     break;

                  case F1VisualTyre.Wet:
                     wp.Children.Add(new TyreView(TyreView.WetTyre));
                     break;

                  case F1VisualTyre.Unknown:
                  default:
                     wp.Children.Add(new TyreView(TyreView.OtherTyre));
                     break;
               }
            }
         }
         else 
         {
            // tyre list not avaible now, so just display the visual tyre
            switch (dat.VisualTyre)
            {
               case F1VisualTyre.Soft:
                  wp.Children.Add(new TyreView(TyreView.SoftTyre));
                  break;

               case F1VisualTyre.Medium:
                  wp.Children.Add(new TyreView(TyreView.MediumTyre));
                  break;

               case F1VisualTyre.Hard:
                  wp.Children.Add(new TyreView(TyreView.HardTyre));
                  break;

               case F1VisualTyre.Intermediate:
                  wp.Children.Add(new TyreView(TyreView.InterTyre));
                  break;

               case F1VisualTyre.Wet:
                  wp.Children.Add(new TyreView(TyreView.WetTyre));
                  break;

               case F1VisualTyre.Unknown:
               default:
                  wp.Children.Add(new TyreView(TyreView.OtherTyre));
                  break;
            }
         }

         return wp;
      }

      public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
      {
         throw new NotImplementedException();
      }
   }

   public class TyreColorConverter : IMultiValueConverter
   {
      public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         var dat = values?[1] as DriverData;

         if (null == dat)
            return Brushes.Gray;


         switch (dat.VisualTyre)
         {
            case F1VisualTyre.Soft:
               return Brushes.Red;

            case F1VisualTyre.Medium:
               return Brushes.Yellow;

            case F1VisualTyre.Hard:
               return Brushes.Silver;

            case F1VisualTyre.Intermediate:
               return Brushes.DarkGreen;

            case F1VisualTyre.Wet:
               return Brushes.Blue;
         }
         return Brushes.Gray;
      }

      public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
      {
         throw new Exception("The method or operation is not implemented.");
      }
   }

}
