// Copyright 2018-2026 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace adjsw.F12025
{
   /// <summary>
   /// Context menu for renaming a driver.
   /// Call Show(driver, dynamicMappings, staticMappings) to open.
   /// Subscribe to NameChosen to receive the new name together with the referenced driver.
   /// </summary>
   public partial class DriverNameContextMenu : ContextMenu
   {
      public delegate void NameChosenHandler(DriverData driver, string newName);
      public event NameChosenHandler NameChosen;

      public DriverNameContextMenu()
      {
         InitializeComponent();
      }

      /// <summary>
      /// Rebuild dynamic sections and open the menu for the given driver.
      /// </summary>
      public void Show(
         DriverData driver,
         DriverNameDynamicMappings dynamicMappings,
         DriverNameMappings[] staticMappings)
      {
         m_referencedDriver = driver;

         m_headerText.Text = driver.Name + "  |  #" + driver.DriverNr + "  |  " + driver.Team.ToString("g");

         m_nameEdit.Text = driver.Name;
         m_nameEdit.SelectAll();

         // Remove previously added dynamic items (everything beyond the 3 static items)
         while (Items.Count > s_staticItemCount)
            Items.RemoveAt(s_staticItemCount);

         // History section
         foreach (var kv in dynamicMappings.driverNameList)
         {
            if (kv.Key != driver.DriverNr)
               continue;

            if (kv.Value.Length == 0)
               break;

            Items.Add(MakeSeparator());

            var historyItem = new MenuItem { Header = "History" };
            StyleSubMenu(historyItem);

            foreach (var name in kv.Value)
            {
               var entry = new MenuItem { Header = name };
               StyleLeafItem(entry);
               string captured = name;
               entry.Click += (s, e) => CommitName(captured);
               historyItem.Items.Add(entry);
            }

            Items.Add(historyItem);
            break;
         }

         // Static league mapping sections
         if (staticMappings != null)
         {
            foreach (var mappingList in staticMappings)
            {
               if (mappingList.Mappings == null || mappingList.Mappings.Length == 0)
                  continue;

               Items.Add(MakeSeparator());

               var leagueItem = new MenuItem { Header = mappingList.LeagueName };
               StyleSubMenu(leagueItem);

               foreach (var mapping in mappingList.Mappings)
               {
                  var entry = new MenuItem { Header = mapping.Name };
                  StyleLeafItem(entry);
                  DriverNameMapping captured = mapping;
                  entry.Click += (s, e) => CommitMappingName(captured.Name);
                  leagueItem.Items.Add(entry);
               }

               Items.Add(leagueItem);
            }
         }

         IsOpen = true;

         // Move keyboard focus into the text box so the user can type immediately
         m_nameEdit.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Input,
            new System.Action(() => m_nameEdit.Focus()));
      }

      private void OnNameEdit_KeyDown(object sender, KeyEventArgs e)
      {
         if (e.Key == Key.Enter)
            CommitFromTextBox();
      }

      private void OnOkButton_Click(object sender, RoutedEventArgs e)
      {
         CommitFromTextBox();
      }

      private void CommitFromTextBox()
      {
         string name = m_nameEdit.Text?.Trim();
         if (!string.IsNullOrEmpty(name))
            CommitName(name);
         else
            IsOpen = false;
      }

      private void CommitName(string name)
      {
         IsOpen = false;
         NameChosen?.Invoke(m_referencedDriver, name);
      }

      private void CommitMappingName(string name)
      {
         IsOpen = false;
         NameChosen?.Invoke(m_referencedDriver, name);
      }

      private static void StyleSubMenu(MenuItem item)
      {
         item.Background = s_menuBrush;
         item.Foreground = Brushes.AntiqueWhite;
         item.FontFamily = new FontFamily("Courier New");
         item.FontSize = 14;
         item.FontWeight = FontWeights.Bold;
      }

      private static void StyleLeafItem(MenuItem item)
      {
         item.Background = s_menuBrush;
         item.Foreground = Brushes.White;
         item.FontFamily = new FontFamily("Courier New");
         item.FontSize = 13;
         item.FontWeight = FontWeights.Normal;
      }

      private static Separator MakeSeparator()
      {
         return new Separator { Margin = new Thickness(0) };
      }

      // Number of items defined statically in XAML (header MenuItem + Separator + edit Border)
      private const int s_staticItemCount = 3;

      private static readonly SolidColorBrush s_menuBrush =
         new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x0D));

      private DriverData m_referencedDriver;
   }
}
