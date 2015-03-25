﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Media;
using System.Windows.Shell;
using System.Reflection;
using WindowState = System.Windows.WindowState;
using GDIScreen = System.Windows.Forms.Screen;
using System.Windows.Interop;

namespace YAPA
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IMainViewModel, INotifyPropertyChanged
    {
        DispatcherTimer dispacherTime = new DispatcherTimer();
        Stopwatch stopWatch = new Stopwatch();
        ItemRepository itemRepository = new ItemRepository();

        private ICommand _showSettings;
        private Storyboard TimerFlush;

        private double _progressValue;
        private string _progressState;
        private int Ticks;
        private int Period;
        private bool IsBreak;
        private bool IsBreakLong;
        private bool IsWork;
        private string BreakLabel;
        private string WorkLabel;
        private SoundPlayer TickSound;
        private SoundPlayer RingSound;

        // For INCP
        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = this;
            _showSettings = new ShowSettings(this);
            Ticks = 0;
            Period = 0;
            IsBreak = false;
            IsBreakLong = false;
            IsWork = true;
            BreakLabel = "break";
            WorkLabel = "work";

            dispacherTime.Tick += new EventHandler(DoTick);
            dispacherTime.Interval = new TimeSpan(0, 0, 0, 0, 250);

            // enable dragging
            this.MouseLeftButtonDown += MainWindow_MouseLeftButtonDown;

            // save window position on close
            this.Closing += MainWindow_Closing;

            // flash timer
            TimerFlush = (Storyboard)TryFindResource("FlashTimer");

            // play sounds
            TickSound = new System.Media.SoundPlayer(AppDomain.CurrentDomain.BaseDirectory + @"\Resources\tick.wav");
            RingSound = new System.Media.SoundPlayer(AppDomain.CurrentDomain.BaseDirectory + @"\Resources\ding.wav");

            Loaded += new RoutedEventHandler(MainWindow_Loaded);

        }

        void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (YAPA.Properties.Settings.Default.IsFirstRun)
            {
                YAPA.Properties.Settings.Default.IsFirstRun = false;
            }

            GDIScreen currentScreen = GDIScreen.FromHandle(new WindowInteropHelper(this).Handle);

            YAPA.Properties.Settings.Default.CurrentScreenHeight = currentScreen.WorkingArea.Height;
            YAPA.Properties.Settings.Default.CurrentScreenWidth = currentScreen.WorkingArea.Width;

            YAPA.Properties.Settings.Default.Save();
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CreateJumpList();

            //if you want to handle to command line args on the first instance you may want it to go here
            //or in the app.xaml.cs
            //ProcessCommandLineArgs(SingleInstance<App>.CommandLineArgs);

            GDIScreen currentScreen = GDIScreen.FromHandle(new WindowInteropHelper(this).Handle);

            bool screenChanged = (currentScreen.WorkingArea.Height != YAPA.Properties.Settings.Default.CurrentScreenHeight ||
                                currentScreen.WorkingArea.Width != YAPA.Properties.Settings.Default.CurrentScreenWidth);

            // default position only for first run or when screen size changes
            // position the clock at top / right, primary screen
            if (YAPA.Properties.Settings.Default.IsFirstRun || screenChanged)
            {
                this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 15.0;
                this.Top = 0;
            }
        }

        private void CreateJumpList()
        {
            JumpList jumpList = new JumpList();
            JumpList.SetJumpList(Application.Current, jumpList);

            JumpTask startTask = new JumpTask();
            startTask.Title = "Start";
            startTask.Description = "Start Pomodoro session";
            startTask.ApplicationPath = Assembly.GetEntryAssembly().Location;
            startTask.Arguments = "/start";
            startTask.IconResourceIndex = 7;
            jumpList.JumpItems.Add(startTask);

            JumpTask pauseTask = new JumpTask();
            pauseTask.Title = "Pause";
            pauseTask.Description = "Pause Pomodoro session";
            pauseTask.ApplicationPath = Assembly.GetEntryAssembly().Location;
            pauseTask.Arguments = "/pause";
            pauseTask.IconResourceIndex = 3;
            jumpList.JumpItems.Add(pauseTask);

            JumpTask stopTask = new JumpTask();
            stopTask.Title = "Restart";
            stopTask.Description = "Restart current Pomodori session";
            stopTask.ApplicationPath = Assembly.GetEntryAssembly().Location;
            stopTask.Arguments = "/restart";
            stopTask.IconResourceIndex = 4;
            jumpList.JumpItems.Add(stopTask);

            JumpTask resetTask = new JumpTask();
            resetTask.Title = "Start from the beginning";
            resetTask.Description = "Start new Pomodoro session";
            resetTask.ApplicationPath = Assembly.GetEntryAssembly().Location;
            resetTask.Arguments = "/reset";
            resetTask.IconResourceIndex = 2;
            jumpList.JumpItems.Add(resetTask);

            JumpTask settingsTask = new JumpTask();
            settingsTask.Title = "Settings";
            settingsTask.Description = "Show YAPA settings";
            settingsTask.ApplicationPath = Assembly.GetEntryAssembly().Location;
            settingsTask.Arguments = "/settings";
            settingsTask.IconResourceIndex = 5;
            jumpList.JumpItems.Add(settingsTask);

            JumpTask homepageTask = new JumpTask();
            homepageTask.Title = "Visit home page";
            homepageTask.Description = "Go to YAPA home page";
            homepageTask.ApplicationPath = Assembly.GetEntryAssembly().Location;
            homepageTask.Arguments = "/homepage";
            homepageTask.IconResourceIndex = 6;
            jumpList.JumpItems.Add(homepageTask);

            jumpList.Apply();
        }

        public ICommand ShowSettings
        {
            get { return _showSettings; }
        }

        public Brush TextBrush
        {
            get { return new BrushConverter().ConvertFromString(YAPA.Properties.Settings.Default.TextBrush) as SolidColorBrush; }
            set
            {
                YAPA.Properties.Settings.Default.TextBrush = value.ToString();
                RaisePropertyChanged("TextBrush");
                RaisePropertyChanged("TextShadowColor");
                RaisePropertyChanged("MouseOverBackgroundColor");
            }
        }

        public Color TextShadowColor
        {
            get
            {
                var shadowColor = Colors.White;

                if (TextBrush.ToString() == Brushes.White.ToString())
                {
                    shadowColor = Colors.Black;
                }
                else
                {
                    shadowColor = Colors.White;
                }

                return shadowColor;
            }
            set
            {

            }
        }

        public Brush MouseOverBackgroundColor
        {
            get
            {
                var mouseOverBackgroundColor = Brushes.White;

                if (TextBrush.ToString() == Brushes.White.ToString())
                {
                    mouseOverBackgroundColor = Brushes.Black;
                }
                else
                {
                    mouseOverBackgroundColor = Brushes.White;
                }

                return mouseOverBackgroundColor;
            }
            set
            {
            }
        }


        public bool SoundEffects
        {
            get { return YAPA.Properties.Settings.Default.SoundNotification; }
            set
            {
                YAPA.Properties.Settings.Default.SoundNotification = value;
                RaisePropertyChanged("UseSoundEfects");
            }
        }

        public double ClockOpacity
        {
            get { return YAPA.Properties.Settings.Default.OpacityLevel; }
            set
            {
                YAPA.Properties.Settings.Default.OpacityLevel = value;
                RaisePropertyChanged("ClockOpacity");
            }
        }

        public double ShadowOpacity
        {
            get { return YAPA.Properties.Settings.Default.ShadowOpacityLevel; }
            set
            {
                YAPA.Properties.Settings.Default.ShadowOpacityLevel = value;
                RaisePropertyChanged("ShadowOpacity");
            }
        }

        public int WorkTime
        {
            get { return YAPA.Properties.Settings.Default.PeriodWork; }
            set
            {
                YAPA.Properties.Settings.Default.PeriodWork = value;
                RaisePropertyChanged("WorkTime");
            }
        }

        public int BreakTime
        {
            get { return YAPA.Properties.Settings.Default.PeriodShortBreak; }
            set
            {
                YAPA.Properties.Settings.Default.PeriodShortBreak = value;
                RaisePropertyChanged("BreakTime");
            }
        }

        public int BreakLongTime
        {
            get { return YAPA.Properties.Settings.Default.PeriodLongBreak; }
            set
            {
                YAPA.Properties.Settings.Default.PeriodLongBreak = value;
                RaisePropertyChanged("BreakLongTime");
            }
        }

        public double ProgressValue
        {
            get { return _progressValue; }
            set
            {
                _progressValue = value;
                RaisePropertyChanged("ProgressValue");
            }
        }

        public bool CountBackwards
        {
            get
            {
                return YAPA.Properties.Settings.Default.CountBackwards;
            }
            set
            {
                YAPA.Properties.Settings.Default.CountBackwards = value;
                this.RaisePropertyChanged("CountBackwards");
            }
        }

        public string ProgressState
        {
            get { return _progressState; }
            set
            {
                _progressState = value;
                RaisePropertyChanged("ProgressState");
            }
        }

        public string CurrentTimeValue
        {
            set
            {
                CurrentTime.Text = value;
                this.Title = String.Format("YAPA - {0}", value);
            }
        }

        void DoTick(object sender, EventArgs e)
        {
            if (stopWatch.IsRunning)
            {
                TimeSpan ts = stopWatch.Elapsed;
                Ticks = (int)ts.TotalSeconds;

                if (IsWork)
                {
                    if (CountBackwards)
                    {
                        ts = TimeSpan.FromMinutes(WorkTime) - ts;
                    }
                    StartTicking(WorkTime, Ticks);
                }
                else if (IsBreak)
                {
                    if (CountBackwards)
                    {
                        ts = TimeSpan.FromMinutes(BreakTime) - ts;
                    }
                    StartTicking(BreakTime, Ticks);
                }
                else if (IsBreakLong)
                {
                    if (CountBackwards)
                    {
                        ts = TimeSpan.FromMinutes(BreakLongTime) - ts;
                    }
                    StartTicking(BreakLongTime, Ticks);
                }

                string currentTime = String.Format("{0:00}:{1:00}", ts.Minutes, ts.Seconds);
                CurrentTimeValue = currentTime;
                CurrentPeriod.Text = Period.ToString();
            }
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (SoundEffects)
                TickSound.Play();
            TimerFlush.Stop(this);
            if (stopWatch.IsRunning)
            {
                Ticks = 0;
                stopWatch.Restart();
            }
            else
            {
                stopWatch.Start();
                dispacherTime.Start();
                if (IsWork)
                    Period++;
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (SoundEffects)
            {
                TickSound.Stop();
                RingSound.Stop();
            }
            if (stopWatch.IsRunning)
            {
                Period--;
                stopWatch.Stop();
                ProgressState = "Paused";
            }
            else
                ResetTicking();
        }

        private void StartTicking(int TotalTime, int Increment)
        {
            int _totalTime = TotalTime * 60;
            ProgressState = "Normal";
            ProgressValue = (double)Increment / _totalTime;
            if (Increment >= _totalTime)
            {
                Ticks = 0;
                if (IsWork)
                {
                    itemRepository.CompletePomodoro();
                }
                StopTicking();
            }
        }

        private void StopTicking()
        {
            if (SoundEffects)
                RingSound.Play();
            TimerFlush.Begin(this, true);
            stopWatch.Reset();
            dispacherTime.Stop();
            ProgressState = "Error";
            if (IsWork)
            {
                CurrentTimeValue = BreakLabel;
                IsWork = false;
                if (Period == 4)
                {
                    IsBreak = false;
                    IsBreakLong = true;
                }
                else
                    IsBreak = true;
            }
            else
            {
                CurrentTimeValue = WorkLabel;
                IsBreak = false;
                IsBreakLong = false;
                IsWork = true;
                if (Period == 4)
                {
                    CurrentPeriod.Text = "";
                    Period = 0;
                }
            }
        }

        private void ResetTicking()
        {
            TimerFlush.Stop(this);
            stopWatch.Reset();
            dispacherTime.Stop();
            CurrentTimeValue = "00:00";
            CurrentPeriod.Text = "";
            Period = 0;
            IsBreak = false;
            IsBreakLong = false;
            IsWork = true;
            ProgressValue = .0;
            ProgressState = "None";
            Ticks = 0;
        }

        private void MainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            DragMove();
        }

        /// <summary>
        /// Used to raise change notifications to other consumers.
        /// </summary>
        private void RaisePropertyChanged(string propName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }

        private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.ShowSettings.Execute(this);
        }

        public bool ProcessCommandLineArgs(IList<string> args)
        {
            if (args == null || args.Count == 0)
                return true;

            if ((args.Count > 1))
            {
                //the first index always contains the location of the exe so we need to check the second index
                if ((args[1].ToLowerInvariant() == "/start"))
                {
                    if (!stopWatch.IsRunning)
                    {
                        if (SoundEffects)
                            TickSound.Play();
                        TimerFlush.Stop(this);
                        stopWatch.Start();
                        dispacherTime.Start();
                        if (IsWork)
                            Period++;
                    }
                }
                else if ((args[1].ToLowerInvariant() == "/pause"))
                {
                    if (SoundEffects)
                    {
                        TickSound.Stop();
                        RingSound.Stop();
                    }
                    if (stopWatch.IsRunning)
                    {
                        Period--;
                        stopWatch.Stop();
                        ProgressState = "Paused";
                    }
                }
                else if ((args[1].ToLowerInvariant() == "/restart"))
                {
                    if (stopWatch.IsRunning)
                    {
                        if (SoundEffects)
                            TickSound.Play();
                        Ticks = 0;
                        stopWatch.Restart();
                    }
                }
                else if ((args[1].ToLowerInvariant() == "/reset"))
                {
                    ResetTicking();
                }
                else if ((args[1].ToLowerInvariant() == "/settings"))
                {
                    this.ShowSettings.Execute(this);
                }
                else if ((args[1].ToLowerInvariant() == "/homepage"))
                {
                    System.Diagnostics.Process.Start("http://lukaszbanasiak.github.io/YAPA/");
                }
            }

            return true;
        }

        private void Exit_OnClick(object sender, RoutedEventArgs e)
        {
            if (stopWatch.IsRunning)
            {
                if (System.Windows.MessageBox.Show("Are you sure you want to exit and cancel pomodoro ?", "Cancel pomodoro", MessageBoxButton.YesNo) == MessageBoxResult.No)
                {
                    return;
                }
            }
            this.Close();
        }

        private void Minimize_OnClick(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MainWindow_OnMouseEnter(object sender, MouseEventArgs e)
        {
            MinExitPanel.Visibility = Visibility.Visible;
        }

        private void MainWindow_OnMouseLeave(object sender, MouseEventArgs e)
        {
            MinExitPanel.Visibility = Visibility.Hidden;
        }
    }
}
