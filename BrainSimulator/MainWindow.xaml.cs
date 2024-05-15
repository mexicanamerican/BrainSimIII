﻿using BrainSimulator.Modules;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using UKS;

namespace BrainSimulator
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //TODO move these to ModuleHandler
        public List<ModuleBase> activeModules = new();
        public List<string> pythonModules = new();

        //the name of the currently-loaded network file
        public static string currentFileName = "";
        public static string pythonPath = "";
        public static ModuleHandler moduleHandler = new();
        public static UKS.UKS theUKS = moduleHandler.theUKS;
        public static MainWindow theWindow = null;

        public MainWindow()
        {
            InitializeComponent();

            SetTitleBar();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            theWindow = this;

            //setup the python support
            pythonPath = (string)Environment.GetEnvironmentVariable("PythonPath", EnvironmentVariableTarget.User);
            if (string.IsNullOrEmpty(pythonPath))
            {
                var result1 = MessageBox.Show("Do you want to use Python Modules?", "Python?", MessageBoxButton.YesNo);
                if (result1 == MessageBoxResult.Yes)
                {
                    string likeliPath = (string)Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    likeliPath += @"\Programs\Python";
                    System.Windows.Forms.OpenFileDialog openFileDialog = new()
                    {
                        Title = "SELECT path to Python .dll (or cancel for no Python support)",
                        InitialDirectory = likeliPath,
                    };

                    // Show the file Dialog.  
                    System.Windows.Forms.DialogResult result = openFileDialog.ShowDialog();
                    // If the user clicked OK in the dialog and  
                    if (result == System.Windows.Forms.DialogResult.OK)
                    {
                        pythonPath = openFileDialog.FileName;
                        Environment.SetEnvironmentVariable("PythonPath", pythonPath, EnvironmentVariableTarget.User);
                    }
                    else
                    {
                        Environment.SetEnvironmentVariable("PythonPath", "", EnvironmentVariableTarget.User);
                    }
                    openFileDialog.Dispose();
                }
                else
                {
                    pythonPath = "no";
                    Environment.SetEnvironmentVariable("PythonPath", pythonPath, EnvironmentVariableTarget.User);
                }
            }
            moduleHandler.PythonPath = pythonPath;
            if (pythonPath != "no")
            {
                moduleHandler.InitPythonEngine();
            }

            //setup the input file
            string fileName = "";
            string savedFile = (string)Properties.Settings.Default["CurrentFile"];
            if (savedFile != "")
                fileName = savedFile;

            try
            {
                if (fileName != "")
                {
                    if (!LoadFile(fileName))
                        CreateEmptyUKS();
                }
                else //force a new file creation on startup if no file name set
                {
                    CreateEmptyUKS();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("UKS Content not loaded");
            }

            //safety check
            if (theUKS.Labeled("BrainSim") == null)
                CreateEmptyUKS();

            LoadModuleTypeMenu();

            InitializeActiveModules();

            LoadMRUMenu();

            //start the module engine
            DispatcherTimer dt = new();
            dt.Interval = TimeSpan.FromSeconds(0.1);
            dt.Tick += Dt_Tick;
            dt.Start();
        }


        public void InitializeActiveModules()
        {
            for (int i = 0; i < activeModules.Count; i++)
            {
                ModuleBase mod = activeModules[i];
                if (mod != null)
                {
                    mod.SetUpAfterLoad();
                }
            }
        }

        public void ShowAllModuleDialogs()
        {
            foreach (ModuleBase mb in activeModules)
            {
                if (mb != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                    {
                        mb.ShowDialog();
                    });
                }
            }
        }

        public void CreateEmptyUKS()
        {
            theUKS.UKSList.Clear();
            theUKS = new UKS.UKS();
            theUKS.AddThing("BrainSim", null);
            theUKS.GetOrAddThing("AvailableModule", "BrainSim");
            theUKS.GetOrAddThing("ActiveModule", "BrainSim");

            InsertMandatoryModules();
            InitializeActiveModules();
        }

        public void InsertMandatoryModules()
        {

            Debug.WriteLine("InsertMandatoryModules entered");
            ActivateModule("UKS");
            ActivateModule("UKSStatement");
        }

        public string ActivateModule(string moduleType)
        {
            Thing t = theUKS.GetOrAddThing(moduleType, "AvailableModule");
            t = theUKS.CreateInstanceOf(theUKS.Labeled(moduleType));
            t.AddParent(theUKS.Labeled("ActiveModule"));

            if (!moduleType.Contains(".py"))
            {
                ModuleBase newModule = CreateNewModule(moduleType);
                newModule.Label = t.Label;
                activeModules.Add(newModule);
            }
            else
            {
                pythonModules.Add(t.Label);
            }

            ReloadActiveModulesSP();
            return t.Label;
        }


        public void CloseAllModuleDialogs()
        {
            lock (activeModules)
            {
                foreach (ModuleBase md in activeModules)
                {
                    if (md != null)
                    {
                        md.CloseDlg();
                    }
                }
            }
        }

        public void CloseAllModules()
        {
            lock (activeModules)
            {
                foreach (ModuleBase mb in activeModules)
                {
                    if (mb != null)
                    {
                        mb.Closing();
                    }
                }
            }
            foreach (string pythonModule in pythonModules)
            {
                moduleHandler.Close(pythonModule);
            }
            pythonModules.Clear();
        }

        private void SetTitleBar()
        {
            Title = "Brain Simulator III " + System.IO.Path.GetFileNameWithoutExtension(currentFileName);
        }

        public static void SuspendEngine()
        {
        }

        public static void ResumeEngine()
        {
        }

        private void Dt_Tick(object? sender, EventArgs e)
        {

            Thing activeModuleParent = theUKS.Labeled("ActiveModule");
            if (activeModuleParent == null) return;
            foreach (Thing module in activeModuleParent.Children)
            {
                ModuleBase mb = activeModules.FindFirst(x => x.Label == module.Label);
                if (mb != null && mb.dlgIsOpen)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate
                    {
                        mb.Fire();
                    });
                }
            }
            foreach (string pythonModule in pythonModules)
            {
                moduleHandler.RunScript(pythonModule);
            }
        }

        private void LoadModuleTypeMenu()
        {
            var moduleTypes = Utils.GetArrayOfModuleTypes();

            foreach (var moduleType in moduleTypes)
            {
                string moduleName = moduleType.Name;
                moduleName = moduleName.Replace("Module", "");
                theUKS.GetOrAddThing(moduleName, "AvailableModule");
            }

            var pythonModules = moduleHandler.GetPythonModules();
            foreach (var moduleType in pythonModules)
            {
                theUKS.GetOrAddThing(moduleType, "AvailableModule");
            }

            ModuleListComboBox.Items.Clear();
            foreach (Thing t in theUKS.Labeled("AvailableModule").Children)
            {
                ModuleListComboBox.Items.Add(new System.Windows.Controls.Label { Content = t.Label, Margin = new Thickness(0), Padding = new Thickness(0) });
            }
        }
    }
}
