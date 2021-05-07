// Copyright 2018-2020 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Windows.Controls;

namespace F1GameSessionDisplay
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

                if (col.Header.ToString() == "Leader")
                {
                    m_leaderDeltaColumn = col;
                }
            }

            if ((m_leaderDeltaColumn == null) || (m_playerDeltaColumn == null))
                throw new Exception("PersonalLeaderboard m_leaderDeltaColumn or m_playerDeltaColumn null");

            LeaderVisible = true;
            DeltaVisible = true;
        }

        public System.Collections.IEnumerable ItemsSource
        {
            get { return m_grid.ItemsSource; }
            set { m_grid.ItemsSource = value; }
        }

        public object SessionSource
        {
            get { return m_textpanel.DataContext; }
            set { m_textpanel.DataContext = value; }
        }
        public bool LeaderVisible
        {
            get { return m_leaderDeltaColumn.Visibility == System.Windows.Visibility.Visible; }
            set {
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

        public bool Quali
        {
            get { return m_isQuali; }
            set 
            {
                if (m_isQuali != value)
                {
                    m_isQuali = value;

                    var converter = this.Resources["DeltaTimeLeaderConverter"] as adjsw.F12020.DeltaTimeLeaderConverter;
                    converter.IsQualy = m_isQuali;
                }            
            }
        }


        private DataGridColumn m_playerDeltaColumn;
        private DataGridColumn m_leaderDeltaColumn;
        private bool m_isQuali = false;

    }
}
