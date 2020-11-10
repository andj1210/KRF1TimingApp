// Copyright 2018-2020 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System.Windows;

namespace F1SessionDisplay
{
    /// <summary>
    /// Interaktionslogik für "App.xaml"
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            if (e.Args.Length > 0)
            {
                // todo add command line argument handling, when necessary 
            }
        }
    }
}
