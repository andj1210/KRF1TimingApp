// Copyright 2018-2020 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using adjsw.F12023;
using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Navigation;

namespace adjsw.F12022
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

         String text = "";
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
               text += "(";
               text += TimeSpan.FromSeconds(dat.RemainingTime).ToString("c");
               text += ")";
               break;
            case SessionType.Q2:
               text += " (";
               text += TimeSpan.FromSeconds(dat.RemainingTime).ToString("c");
               text += ")";
               break;
            case SessionType.Q3:
               text += " (";
               text += TimeSpan.FromSeconds(dat.RemainingTime).ToString("c");
               text += ")";
               break;
            case SessionType.ShortQ:
               text += " (";
               text += TimeSpan.FromSeconds(dat.RemainingTime).ToString("c");
               text += ")";
               break;
            case SessionType.OSQ:
               break;
            case SessionType.Race:
            case SessionType.Race2:
               text += " - Lap ";
               text += dat.CurrentLap;
               text += " / ";
               text += dat.TotalLaps;
               break;
            case SessionType.TimeTrial:
               break;
         }

         return text;
      }

      public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
      {
         throw new NotImplementedException();
      }
   }



   public class PositionColorConverter : IMultiValueConverter
   {
      public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         var dat = values?[1] as DriverData;

         if (null == dat)
            return "?";

         switch (dat.Team)
         {
            case F1Team.Mercedes:
               return Brushes.Turquoise;

            case F1Team.Ferrari:
               return Brushes.Red;

            case F1Team.RedBull:
               return Brushes.Blue;

            case F1Team.Williams:
               return Brushes.White;

            case F1Team.AstonMartin:
               return Brushes.Green;

            case F1Team.Renault:
               return Brushes.Yellow;

            case F1Team.TorroRosso:
               return new SolidColorBrush(Color.FromRgb(10, 100, 150));

            case F1Team.Haas:
               return Brushes.DarkGray;

            case F1Team.McLaren:
               return Brushes.Orange;

            case F1Team.Sauber:
               return Brushes.DarkRed;

            case F1Team.Classic:
               return Brushes.Gray;

         }
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
      public abstract object Convert(object[] values, Type targetType, object parameter, CultureInfo culture);
      public abstract object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture);
   }

   public class DeltaTimeLeaderConverter : QualifyingAwareConverter
   {
      public override object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         var dat = values?[3] as DriverData;

         if (null == dat)
            return "?";

         if (!dat.Present)
            return "    DNF ";

         switch (dat.Status)
         {
            case DriverStatus.DNF:
               return "    DNF ";
            case DriverStatus.DSQ:
               return "    DSQ ";

               /*
           case DriverStatus.Garage:
               return "GARAGE";

           case DriverStatus.OnTrack:
               // show actual delta
               break;
           case DriverStatus.Pitlane:
               return "-PIT-";

           case DriverStatus.Pitting:
               return "-PIT-";
               */
         }

         if (dat.Pos != 1)
         {
            if (dat.TimedeltaToLeader > 0)
               return string.Format(" {0,7:##0.000}", (dat.TimedeltaToLeader + 0.0005));

            else if (dat.TimedeltaToLeader < 0)
            {
               int lapped = (int)(dat.TimedeltaToLeader - 0.5);
               lapped *= -1;
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

         var dat = values?[3] as DriverData;
         if (null == dat)
         {
            setter.SpecialText = "|?";
            return setter;
         }
            

         driver = dat;
         setter.DriverId = driver.Id;
         this.setter = setter;

         if (dat.IsPlayer && !IsQualy)
            setter.SpecialText = 
               "| --- ";

         if (!dat.Present)
            setter.SpecialText = 
               " ***DNF***";

         switch (dat.Status)
         {
            case DriverStatus.DNF:
            case DriverStatus.DSQ:
               setter.SpecialText = " ***DNF***";
               break;
            case DriverStatus.Garage:
               setter.SpecialText = "  GARAGE";
               break;

            case DriverStatus.OnTrack:
               // show actual delta
               break;
            case DriverStatus.Pitlane:
               setter.SpecialText = "  -PIT-";
               break;

            case DriverStatus.Pitting:
               setter.SpecialText = "  -PIT-";
               break;
            case DriverStatus.OutLap:
               setter.SpecialText = "  OUTLAP";
               break;

            case DriverStatus.Inlap:
               setter.SpecialText = "  INLAP";
               break;

            case DriverStatus.Retired:
               setter.SpecialText = " RETIRED";
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
               setter.S1 = StatusView.SetterSectorType.Green;
            else
               setter.S1 = StatusView.SetterSectorType.None;

            if (driver.CurrentLap.Sector2 != 0)
               setter.S2 = StatusView.SetterSectorType.Green;
            else
               setter.S2 = StatusView.SetterSectorType.None;


            if (driver.CurrentLap.Sector3 != 0)
               setter.S3 = StatusView.SetterSectorType.Green;
            else
               setter.S3 = StatusView.SetterSectorType.None;

            if (driver.CurrentLap.Invalid)
            {
               setter.S1 = StatusView.SetterSectorType.Red;
               setter.S2 = StatusView.SetterSectorType.Red;
               setter.S2 = StatusView.SetterSectorType.Red;
            }

            setter.Delta = 0;            
         }

         else
         {
            if (driver.CurrentLap.Sector1 != 0)
               setter.S1 = driver.CurrentLap.Sector1 < driver.FastestLap.Sector1 ? StatusView.SetterSectorType.Green : StatusView.SetterSectorType.Yellow;
            else
               setter.S1 = StatusView.SetterSectorType.None;

            if (driver.CurrentLap.Sector2 != 0)
               setter.S2 = driver.CurrentLap.Sector2 < driver.FastestLap.Sector2 ? StatusView.SetterSectorType.Green : StatusView.SetterSectorType.Yellow;
            else
               setter.S2 = StatusView.SetterSectorType.None;


            if (driver.CurrentLap.Sector3 != 0)
               setter.S3 = driver.CurrentLap.Sector3 < driver.FastestLap.Sector3 ? StatusView.SetterSectorType.Green : StatusView.SetterSectorType.Yellow;
            else
               setter.S3 = StatusView.SetterSectorType.None;

            if (driver.CurrentLap.Invalid)
            {
               setter.S1 = StatusView.SetterSectorType.Red;
               setter.S2 = StatusView.SetterSectorType.Red;
               setter.S2 = StatusView.SetterSectorType.Red;
            }
            else
            {
               
               if (driver.CurrentLap.Sector1Ms == 0)
               {
                  setter.Delta = 0;
               }
               else
               {
                  Int32 delta = (int)driver.CurrentLap.Sector1Ms - (int) driver.FastestLap.Sector1Ms;


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
            }
         }
         return setter;
      }


      public object ConvertRace(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         setter.Quali = false;
         if (driver.IsPlayer)
         {
            setter.Player = true;
            return setter;
         }

         setter.Delta = (System.Int32) driver.TimedeltaToPlayer * 1000;
         return setter;
      }

      private DriverData driver;
      private StatusView.Setter setter;
   }

   public class DeltaTimeConverter : QualifyingAwareConverter
   {
      public override object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         var dat = values?[2] as DriverData;

         if (null == dat)
            return "|?";

         if (dat.IsPlayer)
            return "| --- ";

         if (!dat.Present)
            return "| DNF ";

         switch (dat.Status)
         {
            case DriverStatus.DNF:
            case DriverStatus.DSQ:
               return "| DNF ";
            case DriverStatus.Garage:
               return "GARAGE";

            case DriverStatus.OnTrack:
               // show actual delta
               break;
            case DriverStatus.Pitlane:
               return "|-PIT-";

            case DriverStatus.Pitting:
               return "|-PIT-";
         }

         if (dat.TimedeltaToPlayer > 99.9)
         {
            return "|+99.9";
         }
         else if (dat.TimedeltaToPlayer < -99.9)
         {
            return "|-99.9";
         }
         else
         {
            return "|" + dat.TimedeltaToPlayer.ToString("+00.0;-00.0");
         }
      }

      public override object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
      {
         throw new NotImplementedException();
      }
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

   public class DeltaTimeColorConverter : IMultiValueConverter
   {
      public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         var dat = values?[1] as DriverData;

         if (null == dat)
            return Brushes.Red;

         if (!dat.Present)
            return Brushes.DarkGray;

         if (dat.TimedeltaToPlayer > 0)
         {
            return Brushes.Red;
         }
         else if (dat.TimedeltaToPlayer < 0)
            return Brushes.LightGreen;

         return Brushes.White;
      }

      public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
      {
         throw new Exception("The method or operation is not implemented.");
      }
   }

   public class LastTimeDeltaBgColorConverter : IMultiValueConverter
   {
      public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         var dat = values?[1] as DriverData;

         if (null == dat)
            return Brushes.Transparent;

         float delta = dat.TimedeltaToPlayer - dat.LastTimedeltaToPlayer;

         if (Math.Abs(delta) < 0.05f) // consider 0.050 sec or smaller as equal time
            return Brushes.White;

         else if (delta > 0)
            return Brushes.Red;

         else if (delta < 0)
            return Brushes.LightGreen;

         return Brushes.DarkGray;
      }

      public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
      {
         throw new Exception("The method or operation is not implemented.");
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

   public class TyreDamageColorConverter : IMultiValueConverter
   {
      public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
      {
         var dat = values?[1] as DriverData;

         if (null == dat)
            return Brushes.Gray;

         int r = (int)((dat.TyreDamage * 255) + 0.5f);
         if (r > 255)
            r = 255;

         int g = (int)(((1.0f - dat.TyreDamage) * 255) + 0.5f);
         if (g > 255)
            g = 255;

         if (dat.TyreDamage < 0.5f)
            g = 255;

         else
            r = 255;

         return new SolidColorBrush(Color.FromRgb((byte)r, (byte)g, 0));
      }
      public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
      {
         throw new Exception("The method or operation is not implemented.");
      }
   }
}
