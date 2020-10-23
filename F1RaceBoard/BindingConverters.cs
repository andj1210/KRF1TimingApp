// Copyright 2018-2020 Andreas Jung
// Permission to use, copy, modify, and/or distribute this software for any purpose with or without fee is hereby granted, provided that the above copyright notice and this permission notice appear in all copies.
// THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace adjsw.F12020
{
    public class DeltaTimeConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var dat = values?[1] as DriverData;

            if (null == dat)
                return "?";

            if (dat.IsPlayer)
                return " --- ";

            if (!dat.Present)
                return "EXIT";

            if (dat.TimedeltaToPlayer > 99.9)
            {
                return "+99.9";
            }
            else if (dat.TimedeltaToPlayer < -99.9)
            {
                return "-99.9";
            }
            else
            {
                return dat.TimedeltaToPlayer.ToString("+00.0;-00.0");
            }            
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TyreDamageConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var dat = values?[1] as DriverData;

            if (null == dat)
                return "?";

            float state = 1 - dat.TyreDamage;
            state *= 100;

            if (state > 100)
                state = 100;

            if (state < 0)
                state = 0;

            state += 0.5f;

            return string.Format("{0,3:###}%", (int) state);
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
                return Brushes.Green;

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
                return Brushes.DarkGray;

            if (dat.TimedeltaToPlayer > dat.LastTimedeltaToPlayer)
            {
                return Brushes.Red;
            }
            else if (dat.TimedeltaToPlayer < dat.LastTimedeltaToPlayer)
                return Brushes.Green;

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
                return Brushes.White;

            if (dat.IsPlayer)
                return Brushes.DarkViolet;

            /*
            if (dat.TimedeltaToPlayer > 0)
            {
                return Brushes.Red;
            }
            else if (dat.TimedeltaToPlayer < 0)
                return Brushes.Green;
            */
            return Brushes.Black;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }

    public class NameColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var dat = values?[1] as DriverData;

            if (null == dat)
                return Brushes.Gray;

            if (dat.IsPlayer)
                return Brushes.DarkViolet;


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

                case F1Team.ForceIndia:
                    return Brushes.HotPink;

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

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }

    public class TyreConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var dat = values?[1] as DriverData;

            if (null == dat)
                return "?";

            switch (dat.VisualTyre)
            {
                case F1VisualTyre.Soft:
                    return " S";

                case F1VisualTyre.Medium:
                    return " M";

                case F1VisualTyre.Hard:
                    return " H";

                case F1VisualTyre.Intermediate:
                    return " I";

                case F1VisualTyre.Wet:
                    return " W";
            }
            return "?";
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
