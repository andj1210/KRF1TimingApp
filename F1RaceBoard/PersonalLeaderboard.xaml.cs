// Copyright 2018-2020 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

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

    }
}
