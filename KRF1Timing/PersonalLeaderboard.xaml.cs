// Copyright 2018-2020 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace adjsw.F12025
{
   /// <summary>
   /// Personal Leaderboard displays the leaderboard with the focus on the player, which means
   /// for instance gaps are presented relative to the players car etc...
   /// </summary>
   public partial class PersonalLeaderboard : UserControl
   {
      public PersonalLeaderboard()
      {
         InitializeComponent();

         var cols = m_grid.Columns;
         foreach (var col in cols)
         {
            if (col.Header.ToString() == "Delta")
            {
               m_playerDeltaColumn = col;
            }

            if (col.Header.ToString() == "Status")
            {
               m_statusColumn = col;
            }

            if (col.Header.ToString() == "Leader")
            {
               m_leaderDeltaColumn = col;
            }

            if (col.Header.ToString() == "S1")
            {
               m_fastestLapS1Column = col;
            }

            if (col.Header.ToString() == "S2")
            {
               m_fastestLapS2Column = col;
            }

            if (col.Header.ToString() == "S3")
            {
               m_fastestLapS3Column = col;
            }
         }

         if ((m_leaderDeltaColumn == null) || (m_playerDeltaColumn == null))
            throw new Exception("PersonalLeaderboard m_leaderDeltaColumn or m_playerDeltaColumn null");

         LeaderVisible = true;
         DeltaVisible = true;

         // trigger correct cell size
         Quali = true;
         Quali = false; 
      }

      public System.Collections.IEnumerable ItemsSource
      {
         get { return m_grid.ItemsSource; }
         set { m_grid.ItemsSource = value; }
      }

      public DataGrid TheDataGrid
      {
         get { return m_grid; }
      }

      public object SessionSource
      {
         get { return m_textpanel.DataContext; }
         set { m_textpanel.DataContext = value; }
      }
      public bool LeaderVisible
      {
         get { return m_leaderDeltaColumn.Visibility == System.Windows.Visibility.Visible; }
         set
         {
            if (value)
            {
               m_leaderDeltaColumn.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
               m_leaderDeltaColumn.Visibility = System.Windows.Visibility.Collapsed;
            }
         }
      }

      public bool DeltaVisible
      {
         get { return m_playerDeltaColumn.Visibility == System.Windows.Visibility.Visible; }
         set
         {
            if (value)
            {
               m_playerDeltaColumn.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
               m_playerDeltaColumn.Visibility = System.Windows.Visibility.Collapsed;
            }
         }
      }

      public bool StatusVisible
      {
         get { return m_statusColumn.Visibility == System.Windows.Visibility.Visible; }
         set
         {
            if (value)
            {
               m_statusColumn.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
               m_statusColumn.Visibility = System.Windows.Visibility.Collapsed;
            }
         }
      }

      public bool Quali
      {
         get { return m_isQuali; }
         set
         {
            if ((m_isQuali != value) 
               || (m_isQuali == true) // for changing S1-S3 enabled or not
               )
            {
               m_isQuali = value;

               var converter = this.Resources["DeltaTimeLeaderConverter"] as adjsw.F12025.QualifyingAwareConverter;
               converter.IsQualy = m_isQuali;
               converter = this.Resources["DeltaTimeConverter"] as adjsw.F12025.QualifyingAwareConverter;
               converter.IsQualy = m_isQuali;
               converter = this.Resources["StatusConverter"] as adjsw.F12025.QualifyingAwareConverter;
               converter.IsQualy = m_isQuali;

               if (m_isQuali)
               {
                  if (ActualWidth > 1080)
                  {
                     m_fastestLapS1Column.Visibility = System.Windows.Visibility.Visible;
                     m_fastestLapS2Column.Visibility = System.Windows.Visibility.Visible;
                     m_fastestLapS3Column.Visibility = System.Windows.Visibility.Visible;
                  }
                  else
                  {
                     m_fastestLapS1Column.Visibility = System.Windows.Visibility.Collapsed;
                     m_fastestLapS2Column.Visibility = System.Windows.Visibility.Collapsed;
                     m_fastestLapS3Column.Visibility = System.Windows.Visibility.Collapsed;
                  }
                  m_statusColumn.Width = 205;
               }
               else
               {
                  m_fastestLapS1Column.Visibility = System.Windows.Visibility.Collapsed;
                  m_fastestLapS2Column.Visibility = System.Windows.Visibility.Collapsed;
                  m_fastestLapS3Column.Visibility = System.Windows.Visibility.Collapsed;
                  m_statusColumn.Width = 130;
               }

            }
         }
      }


      public object DriverUnderMouse
      {
         get
         {
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(m_grid, Mouse.GetPosition(m_grid));
            DataGridRow dataGridRow = hitTestResult.VisualHit.GetParentOfType<DataGridRow>();
            int index = -1;

            if (dataGridRow != null)
               index = dataGridRow.GetIndex();

            if ((index >= 0) && (index < m_grid.Items.Count))
            {
               return m_grid.Items[index];
            }
            return null;
         }
      }


      public event MouseButtonEventHandler DataGridRightClick
      {
         add { m_grid.MouseRightButtonDown += value; }
         remove { m_grid.MouseRightButtonDown -= value; }
      }

      private DataGridColumn m_playerDeltaColumn;
      private DataGridColumn m_leaderDeltaColumn;
      private DataGridColumn m_statusColumn;
      private DataGridColumn m_fastestLapS1Column;
      private DataGridColumn m_fastestLapS2Column;
      private DataGridColumn m_fastestLapS3Column;
      private bool m_isQuali = false;
   }

   static class Extension
   {
      // https://stackoverflow.com/questions/25502150/wpf-datagrid-get-row-number-which-mouse-cursor-is-on
      public static T GetParentOfType<T>(this DependencyObject element) where T : DependencyObject
      {
         Type type = typeof(T);
         if (element == null) return null;
         DependencyObject parent = VisualTreeHelper.GetParent(element);
         if (parent == null && ((FrameworkElement)element).Parent is DependencyObject) parent = ((FrameworkElement)element).Parent;
         if (parent == null) return null;
         else if (parent.GetType() == type || parent.GetType().IsSubclassOf(type)) return parent as T;
         return GetParentOfType<T>(parent);
      }

   }
}
