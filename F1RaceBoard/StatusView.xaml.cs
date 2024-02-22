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
      private static Brush NewTimeBrush = Brushes.LightSteelBlue;

      private static readonly int s_lockTicks = 3 * 5; // = 3*333 ms + 5 = 5 seconds
      private static DispatcherTimer s_dt = new DispatcherTimer();
      private static FreezeStatus[] s_freezeState = new FreezeStatus[22];
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

         // we need id for freezestate after lap housekeeping
         public int DriverId { get; set; }

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

      public class FreezeStatus
      {
         public StatusView View { get; set; } = null;
         public Setter LockedSetter { get; set; } = null;
         
         public int LockTicks { get; set; } = 0;
      }


      static StatusView()
      {
         for (int i = 0; i < s_freezeState.Length; ++i)
         {
            s_freezeState[i] = new FreezeStatus();
         }


         s_dt.Interval = TimeSpan.FromMilliseconds(333);
         s_dt.Tick += S_dt_Tick;
         s_dt.Start();
      }

      public StatusView()
      {
         InitializeComponent();
         m_UpdateView();
      }

      private static void S_dt_Tick(object sender, EventArgs e)
      {
         foreach (var freezeState in s_freezeState)
         {
            if (freezeState.View != null)
            {
               --freezeState.LockTicks;

               if (freezeState.LockTicks <= 0)
               {
                  var view = freezeState.View;
                  freezeState.View = null;
                  view.m_UpdateView();
               }
            }
         }
      }

      private void OnFrezeStop(object sender, EventArgs e)
      {
         s_dt.Stop();
         m_frozen = false;
         m_UpdateView();
      }

      private static void s_StartFreeze(StatusView v, Setter s)
      {
         if (s.DriverId < s_freezeState.Length)
         {
            var st = s_freezeState[s.DriverId];
            st.View = v;
            st.LockedSetter = s;
            st.LockTicks = s_lockTicks;
         }
      }

      private bool m_GetFrozen()
      {
         if (m_setter.DriverId < s_freezeState.Length)
         {
            return s_freezeState[m_setter.DriverId].View != null;
         }
         return false;
      }

      private void m_StartFreeze()
      {
         if (!m_GetFrozen())
         {
            s_StartFreeze(this, m_setter);
         }
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
         var setter = m_setter;
         bool frozen = false;

         if (m_GetFrozen())
         {
            // draw the locked state
            setter = s_freezeState[m_setter.DriverId].LockedSetter;
            frozen = true;
         }

         if (null == setter)
         {
            m_rectS1.Visibility = Visibility.Collapsed;
            m_rectS2.Visibility = Visibility.Collapsed;
            m_rectS3.Visibility = Visibility.Collapsed;

            m_text.Content = "?";
            m_text.Foreground = Brushes.White;
            m_text.Background = Brushes.Transparent;
            return;
         }

         if (!String.IsNullOrEmpty(setter.SpecialText))
         {
            m_rectS1.Visibility = Visibility.Collapsed;
            m_rectS2.Visibility = Visibility.Collapsed;
            m_rectS3.Visibility = Visibility.Collapsed;

            m_text.Content = setter.SpecialText;
            m_text.Foreground = Brushes.Red;
            m_text.Background = Brushes.Transparent;
            return;
         }

         if (setter.Quali)
         {
            m_rectS1.Visibility = Visibility.Visible;
            m_rectS2.Visibility = Visibility.Visible;
            m_rectS3.Visibility = Visibility.Visible;
            m_SetSectorColor(m_rectS1, setter.S1);
            m_SetSectorColor(m_rectS2, setter.S2);
            m_SetSectorColor(m_rectS3, setter.S3);

            if (setter.S3 != SetterSectorType.None)
            {
               // when S3 is available, freeze the state for 3 seconds to show the result.
               if (!frozen)
               {
                  m_StartFreeze();
               }
               m_text.Background = NewTimeBrush;
            }
            else
            {
               m_text.Background = Brushes.Transparent;
            }

            if (setter.Delta == 0)
            {
               m_text.Content = "-----";
               m_text.Foreground = Brushes.White;
            }
            else if (setter.Delta < 0)
            {
               m_text.Foreground = Brushes.Green;
               if (setter.Delta < -9999)
               {
                  m_text.Content = "-9.999";
               }
               else
               {
                  Int32 deltaPos = setter.Delta * -1;
                  string str = "-" + (deltaPos / 1000).ToString("D1") + "." + (deltaPos % 1000).ToString("D3");
                  m_text.Content = str;
               }
            }
            else // setter.Delta > 0
            {
               m_text.Foreground = Brushes.Red;

               if (setter.Delta > 9999)
               {
                  m_text.Content = "+9.999";
               }
               else
               {
                  string str = "+" + (setter.Delta / 1000).ToString("D1") + "." + (setter.Delta % 1000).ToString("D3");
                  m_text.Content = str;
               }
            }
         }
         else
         {
            m_text.Background = Brushes.Transparent;
            // RACE
            if (setter.Player)
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
