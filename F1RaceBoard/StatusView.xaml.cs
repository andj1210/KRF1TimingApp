using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.RightsManagement;
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

namespace F1GameSessionDisplay
{
   /// <summary>
   /// Interaktionslogik für StatusView.xaml
   /// </summary>
   public partial class StatusView : UserControl
   {
      private static Brush RedSectorBrush = Brushes.Red;
      private static Brush YellowSectorBrush = Brushes.Yellow;
      private static Brush GreenSectorBrush = Brushes.Green;
      private static Brush PurpleSectorBrush = Brushes.DeepPink;
      private DispatcherTimer m_dt = null;
      private bool m_frozen = false;

      public static readonly DependencyProperty StatusInfoProperty =
         DependencyProperty.Register(
            name: "StatusInfo",
            propertyType: typeof(Setter),
            ownerType: typeof(StatusView),
            typeMetadata: new FrameworkPropertyMetadata(
                defaultValue: new Setter(),
                flags: FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback : new PropertyChangedCallback(OnStatusInfoChangedCallBack))

          );

      public enum SetterSectorType
      {
         None,
         Red,     // super slow, probably cooldown / inlap
         Yellow,
         Green,         
         Purple
      }

      // a value object which is used to determine the appearance of the Statusview:
      // qualifying: display of sector 1-3 + delta
      // race: display delta to player
      public class Setter
      {
         public Setter()
         {
            Quali = true;
            Player = false;
            S1 = SetterSectorType.Green;
            S2 = SetterSectorType.Yellow;
            S3 = SetterSectorType.None;
            Delta = -213;
            SpecialText = "";
         }

         // highest priority, if set, just the text is shown
         public string SpecialText { get; set; }

         public bool Quali { get; set; }
         public bool Player { get; set; }

         // if Quali
         public SetterSectorType S1 { get; set; }
         public SetterSectorType S2 { get; set; }
         public SetterSectorType S3 { get; set; }

         // if qualy: delta [ms] to fastest lap
         // if race: delta [ms] to player
         public Int32 Delta { get; set; }
      }

      public StatusView()
      {
         InitializeComponent();
         m_UpdateView();
      }

      private void OnFrezeStop(object sender, EventArgs e)
      {
         m_dt.Stop();
         m_frozen = false;
         m_UpdateView();
      }

      private void m_StartFreeze()
      {
         if (m_dt == null)
         {
            m_dt = new DispatcherTimer();
            m_dt.Interval = TimeSpan.FromSeconds(5);
            m_dt.Tick += OnFrezeStop;
         }

         m_frozen = true;
         m_dt.Start();
      }

      private static void OnStatusInfoChangedCallBack(
        DependencyObject sender, DependencyPropertyChangedEventArgs e)
      {
         StatusView c = sender as StatusView;
         if (c != null)
         {
            var newVal = e.NewValue as Setter;
            c.StatusInfo = newVal;
         }
      }

      public Setter StatusInfo
      {
         get
         {
            return m_setter;
         }
         set
         {
            if (m_setter != value)
            {
               m_setter = value;
               m_UpdateView();
            }
         }
      }


      private void m_UpdateView()
      {
         if (m_frozen)
         {
            return;
         }

         if (null == m_setter)
         {
            m_rectS1.Visibility = Visibility.Collapsed;
            m_rectS2.Visibility = Visibility.Collapsed;
            m_rectS3.Visibility = Visibility.Collapsed;

            m_text.Content = "?";
            m_text.Foreground = Brushes.White;
            return;
         }

         if (!String.IsNullOrEmpty(m_setter.SpecialText))
         {
            m_rectS1.Visibility = Visibility.Collapsed;
            m_rectS2.Visibility = Visibility.Collapsed;
            m_rectS3.Visibility = Visibility.Collapsed;

            m_text.Content = m_setter.SpecialText;
            m_text.Foreground = Brushes.Red;
            return;
         }

         if (m_setter.Quali)
         {
            m_rectS1.Visibility = Visibility.Visible;
            m_rectS2.Visibility = Visibility.Visible;
            m_rectS3.Visibility = Visibility.Visible;
            m_SetSectorColor(m_rectS1, m_setter.S1);
            m_SetSectorColor(m_rectS2, m_setter.S2);
            m_SetSectorColor(m_rectS3, m_setter.S3);

            if (m_setter.S3 != SetterSectorType.None)
            {
               // when S3 is available, freeze the state for 3 seconds to show the result.               
               //m_StartFreeze();
            }

            if (m_setter.Delta == 0)
            {
               m_text.Content = "-----";
               m_text.Foreground = Brushes.White;
            }
            else if (m_setter.Delta < 0)
            {
               m_text.Foreground = Brushes.Green;
               if (m_setter.Delta < -9999)
               {
                  m_text.Content = "-9.999";
               }
               else
               {
                  Int32 deltaPos = m_setter.Delta * -1;
                  string str = "-" + (deltaPos / 1000).ToString("D1") + "." + (deltaPos % 1000).ToString("D3");
                  m_text.Content = str;
               }
            }
            else // m_setter.Delta > 0
            {
               m_text.Foreground = Brushes.Red;

               if (m_setter.Delta > 9999)
               {
                  m_text.Content = "+9.999";
               }
               else
               {
                  string str = "+" + (m_setter.Delta / 1000).ToString("D1") + "." + (m_setter.Delta % 1000).ToString("D3");
                  m_text.Content = str;
               }
            }
         }
         else
         {
            // RACE
            if (m_setter.Player)
            {
               m_rectS1.Visibility = Visibility.Collapsed;
               m_rectS2.Visibility = Visibility.Collapsed;
               m_rectS3.Visibility = Visibility.Collapsed;

               m_text.Content = "----------";
               m_text.Foreground = Brushes.White;
               m_text.Background = Brushes.Violet;
            }
         }
      }

      private void m_SetSectorColor(Rectangle rect, SetterSectorType sect)
      {
         switch (sect)
         {
            default:
            case SetterSectorType.None:
               rect.Fill = Brushes.Transparent;
               break;
            case SetterSectorType.Red:
               rect.Fill = RedSectorBrush;
               break;
            case SetterSectorType.Yellow:
               rect.Fill = YellowSectorBrush;
               break;
            case SetterSectorType.Green:
               rect.Fill = GreenSectorBrush;
               break;
            case SetterSectorType.Purple:
               rect.Fill = PurpleSectorBrush;
               break;
         }
      }

      private Setter m_setter = new Setter();
   }
}
