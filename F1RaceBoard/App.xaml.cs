// Copyright 2018-2020 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System.Windows;

namespace adjsw.F12023
{
    /// <summary>
    /// Interaktionslogik für "App.xaml"
    /// </summary>
    public partial class App : Application
    {
        public static string PlaybackFile { get; set; }


        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            if (e.Args.Length > 0)
            {
               PlaybackFile = e.Args[0];
            }
        }
    }
}
