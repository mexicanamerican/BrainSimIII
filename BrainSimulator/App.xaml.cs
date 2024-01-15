﻿using BrainSimulator;
using System.Windows;

namespace ModuleTester
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            //if (e.Args.Length == 1)
            //    StartupString = e.Args[0];

            MainWindow mainWin = new();
#if !DEBUG
            mainWin.WindowState = WindowState.Minimized;
#endif
            mainWin.Show();
#if !DEBUG
            mainWin.Hide();
#endif        
        }
        private static string startupString = "";

        public static string StartupString { get => startupString; set => startupString = value; }
    }
}
