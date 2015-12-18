﻿// Copyright (c) 2015 Feenux LLC, All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Linq;
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

using Microsoft.Shell;

using System.Threading;

using System.Data;
using System.Xml;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

using WinInterop = System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Drawing.Printing;

using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;

using TH_Configuration;
using TH_DeviceManager;
using TH_Database;
using TH_Global;
using TH_PlugIns_Client;
using TH_WPF;
using TH_Updater;
using TH_UserManagement;
using TH_UserManagement.Management;

using TrakHound_Client.Controls;

namespace TrakHound_Client
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, ISingleInstanceApp
    {

        public MainWindow()
        {
            init();
        }

        public void init()
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += currentDomain_UnhandledException;

            Log_Initialize();

            Splash_Initialize();

            devicemanager = new DeviceManager(DeviceManagerType.Client);


            InitializeComponent();
            DataContext = this;

            Splash_UpdateStatus("...Initializing");
            this.SourceInitialized += new EventHandler(win_SourceInitialized);

            Application.Current.MainWindow = this;

            // Initialize Pages
            Pages_Initialize();

            // Set border thickness (maybe make this a static resource in XAML?)
            ResizeBorderThickness = 1;

            LoadDevices_Initialize();

            // Read Users and Login
            Splash_UpdateStatus("...Logging in User");
            ReadUserManagementSettings();
            devicemanager.userDatabaseSettings = UserDatabaseSettings;

            LoginMenu.rememberMeType = RememberMeType.Client;
            LoginMenu.LoadRememberMe();

            Splash_UpdateStatus("...Loading Plugins");
            LoadPlugIns();

            // Wait for the minimum splash time to elapse, then close the splash dialog
            while (SplashWait) { System.Threading.Thread.Sleep(200); }
            Splash_Close();
        }

        void currentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            //MessageBox.Show(e.ExceptionObject.ToString());  
        }

        
        #region "Splash"

        Splash.Screen SPLSH;

        System.Timers.Timer Splash_TIMER;

        void Splash_Initialize()
        {

            SPLSH = new Splash.Screen();
            Splash_Show();

            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();

            Version version = assembly.GetName().Version;

            SPLSH.Version = "Version " + version.Major.ToString() + "." + version.Minor.ToString() + "." + version.Build.ToString() + "." + version.Revision.ToString();

            Splash_TIMER = new System.Timers.Timer();
            Splash_TIMER.Interval = 4000;
            Splash_TIMER.Elapsed += Splash_TIMER_Elapsed;
            Splash_TIMER.Enabled = true;

        }

        void Splash_Show() { this.Dispatcher.Invoke(new Action(Splash_Show_GUI), new object[] { }); }

        void Splash_Show_GUI() { SPLSH.Show(); }

        void Splash_Close() { if (SPLSH != null) SPLSH.Close(); }

        const System.Windows.Threading.DispatcherPriority Priority = System.Windows.Threading.DispatcherPriority.Background;

        void Splash_UpdateStatus(string Status) { this.Dispatcher.Invoke(new Action<string>(Splash_UpdateStatus_GUI), Priority, new object[] { Status }); }

        void Splash_UpdateStatus_GUI(string Status) { SPLSH.Status = Status; }

        void Splash_AddPlugin(PlugIn plugin) { this.Dispatcher.Invoke(new Action<PlugIn>(Splash_AddPlugin_GUI), new object[] { plugin }); }

        void Splash_AddPlugin_GUI(PlugIn plugin) { SPLSH.AddPlugin(plugin); }

        bool SplashWait = true;

        void Splash_TIMER_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Splash_TIMER.Enabled = false;
            SplashWait = false;
        }

        #endregion

        #region "Main Window"

        #region "Window Controls"

        public bool Maximized
        {
            get { return (bool)GetValue(MaximizedProperty); }
            set { SetValue(MaximizedProperty, value); }
        }

        public static readonly DependencyProperty MaximizedProperty =
            DependencyProperty.Register("Maximized", typeof(bool), typeof(MainWindow), new PropertyMetadata(true));
       

        private void Close_BD_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) Application.Current.Shutdown();
        }

        private void Maximize_BD_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) AdjustWindowSize();
        }

        private void Minimize_BD_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.WindowState = WindowState.Minimized;
        }

        #endregion

        private void AdjustWindowSize()
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                Maximized = false;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                Maximized = true;
            }

        }

        #region "Dragging"

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                if (e.ClickCount == 2)
                {
                    AdjustWindowSize();
                }
                else
                {
                    Application.Current.MainWindow.DragMove();
                }
        }

        #endregion

        #region "Resizing"

        public int ResizeBorderThickness
        {
            get { return (int)GetValue(ResizeBorderThicknessProperty); }
            set { SetValue(ResizeBorderThicknessProperty, value); }
        }

        public static readonly DependencyProperty ResizeBorderThicknessProperty =
            DependencyProperty.Register("ResizeBorderThickness", typeof(int), typeof(MainWindow), new PropertyMetadata(2));


        private void Vertical_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.SizeNS;
        }

        private void Vertical_MouseLeave(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.Arrow;
        }

        private bool _isResizing = false;
        private const double CURSOR_OFFSET_SMALL = 3;
        private const double CURSOR_OFFSET_LARGE = 5;

        private void Resize_Begin(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Shapes.Rectangle)
            {
                _isResizing = true;
                ((System.Windows.Shapes.Rectangle)sender).CaptureMouse();
            }
        }

        private void Resize_End(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Shapes.Rectangle)
            {
                _isResizing = false;
                ((System.Windows.Shapes.Rectangle)sender).ReleaseMouseCapture();
            }
        }

        private void Resize(object sender, MouseEventArgs e)
        {

            if (_isResizing && (sender is System.Windows.Shapes.Rectangle))
            {
                double x = e.GetPosition(this).X;
                double y = e.GetPosition(this).Y;

                string mode = ((System.Windows.Shapes.Rectangle)sender).Name.ToLower();
                if (mode.Contains("left"))
                {
                    x -= CURSOR_OFFSET_SMALL;
                    if ((Width - x >= MinWidth) && (Width - x <= MaxWidth))
                    {
                        Width -= x;
                        Left += x;
                    }
                }
                if (mode.Contains("right"))
                {
                    Width = Math.Max(MinWidth, Math.Min(MaxWidth, x + CURSOR_OFFSET_LARGE));
                }
                if (mode.Contains("top"))
                {
                    y -= CURSOR_OFFSET_SMALL;
                    if ((Height - y >= MinHeight) && (Height - y <= MaxHeight))
                    {
                        Height -= y;
                        Top += y;
                    }
                }
                if (mode.Contains("bottom"))
                {
                    Height = Math.Max(MinHeight, Math.Min(MaxHeight, y + CURSOR_OFFSET_SMALL));
                }
            }
        }

        private void Resize_MouseLeave(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.Arrow;
        }

        #region "Top"

        private void Rectangle_TopLeft_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.SizeNWSE;
        }

        private void Rectangle_TopRight_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.SizeNESW;
        }

        private void Rectangle_TopMiddle_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.SizeNS;
        }

        #endregion

        #region "Bottom"

        private void Rectangle_BottomLeft_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.SizeNESW;
        }

        private void Rectangle_BottomRight_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.SizeNWSE;
        }

        private void Rectangle_BottomMiddle_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.SizeNS;
        }

        #endregion

        private void Rectangle_WE_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.SizeWE;
        }

        #endregion

        #region "Maximize and Taskbar Fix"

        void win_SourceInitialized(object sender, EventArgs e)
        {
            System.IntPtr handle = (new WinInterop.WindowInteropHelper(this)).Handle;
            WinInterop.HwndSource.FromHwnd(handle).AddHook(new WinInterop.HwndSourceHook(WindowProc));
        }

        private static System.IntPtr WindowProc(
              System.IntPtr hwnd,
              int msg,
              System.IntPtr wParam,
              System.IntPtr lParam,
              ref bool handled)
        {
            switch (msg)
            {
                case 0x0024:
                    WmGetMinMaxInfo(hwnd, lParam);
                    handled = true;
                    break;
            }

            return (System.IntPtr)0;
        }

        private static void WmGetMinMaxInfo(System.IntPtr hwnd, System.IntPtr lParam)
        {

            MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));

            // Adjust the maximized size and position to fit the work area of the correct monitor
            int MONITOR_DEFAULTTONEAREST = 0x00000002;
            System.IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            if (monitor != System.IntPtr.Zero)
            {

                MONITORINFO monitorInfo = new MONITORINFO();
                GetMonitorInfo(monitor, monitorInfo);
                RECT rcWorkArea = monitorInfo.rcWork;
                RECT rcMonitorArea = monitorInfo.rcMonitor;
                mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
                mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
                mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
                mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }


        /// <summary>
        /// POINT aka POINTAPI
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            /// <summary>
            /// x coordinate of point.
            /// </summary>
            public int x;
            /// <summary>
            /// y coordinate of point.
            /// </summary>
            public int y;

            /// <summary>
            /// Construct a point of coordinates (x,y).
            /// </summary>
            public POINT(int x, int y)
            {
                this.x = x;
                this.y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        };

        /// <summary>
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MONITORINFO
        {
            /// <summary>
            /// </summary>            
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));

            /// <summary>
            /// </summary>            
            public RECT rcMonitor = new RECT();

            /// <summary>
            /// </summary>            
            public RECT rcWork = new RECT();

            /// <summary>
            /// </summary>            
            public int dwFlags = 0;
        }


        /// <summary> Win32 </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        public struct RECT
        {
            /// <summary> Win32 </summary>
            public int left;
            /// <summary> Win32 </summary>
            public int top;
            /// <summary> Win32 </summary>
            public int right;
            /// <summary> Win32 </summary>
            public int bottom;

            /// <summary> Win32 </summary>
            public static readonly RECT Empty = new RECT();

            /// <summary> Win32 </summary>
            public int Width
            {
                get { return Math.Abs(right - left); }  // Abs needed for BIDI OS
            }
            /// <summary> Win32 </summary>
            public int Height
            {
                get { return bottom - top; }
            }

            /// <summary> Win32 </summary>
            public RECT(int left, int top, int right, int bottom)
            {
                this.left = left;
                this.top = top;
                this.right = right;
                this.bottom = bottom;
            }


            /// <summary> Win32 </summary>
            public RECT(RECT rcSrc)
            {
                this.left = rcSrc.left;
                this.top = rcSrc.top;
                this.right = rcSrc.right;
                this.bottom = rcSrc.bottom;
            }

            /// <summary> Win32 </summary>
            public bool IsEmpty
            {
                get
                {
                    // BUGBUG : On Bidi OS (hebrew arabic) left > right
                    return left >= right || top >= bottom;
                }
            }
            /// <summary> Return a user friendly representation of this struct </summary>
            public override string ToString()
            {
                if (this == RECT.Empty) { return "RECT {Empty}"; }
                return "RECT { left : " + left + " / top : " + top + " / right : " + right + " / bottom : " + bottom + " }";
            }

            /// <summary> Determine if 2 RECT are equal (deep compare) </summary>
            public override bool Equals(object obj)
            {
                if (!(obj is Rect)) { return false; }
                return (this == (RECT)obj);
            }

            /// <summary>Return the HashCode for this struct (not garanteed to be unique)</summary>
            public override int GetHashCode()
            {
                return left.GetHashCode() + top.GetHashCode() + right.GetHashCode() + bottom.GetHashCode();
            }


            /// <summary> Determine if 2 RECT are equal (deep compare)</summary>
            public static bool operator ==(RECT rect1, RECT rect2)
            {
                return (rect1.left == rect2.left && rect1.top == rect2.top && rect1.right == rect2.right && rect1.bottom == rect2.bottom);
            }

            /// <summary> Determine if 2 RECT are different(deep compare)</summary>
            public static bool operator !=(RECT rect1, RECT rect2)
            {
                return !(rect1 == rect2);
            }
        }

        [DllImport("user32")]
        internal static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

        /// <summary>
        /// 
        /// </summary>
        [DllImport("User32")]
        internal static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        #endregion

        // Keyboard Keys
        private void Main_Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            // Always get correct key (ex. Alt)
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // Page Tabs
            if (e.Key == Key.Tab && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                ChangePage_Backward();
            }
            else if (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ChangePage_Forward();
            }

            // Toggle MainMenu Bar
            if (key == Key.LeftAlt || key == Key.RightAlt)
            {
                MainMenuBar_Show = !MainMenuBar_Show;
            }

            // Toggle Developer Console with F12
            if (key == Key.F12) developerConsole.Shown = !developerConsole.Shown;
        }

        private void Main_Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            messageCenter.Hide();

            PluginLauncher.Hide();
            MainMenu.Hide();
            LoginMenu.Hide();
        }


        private void Main_Window_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void Main_Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Properties.Settings.Default.PlugIn_Configurations != null)
            {
                List<PlugInConfiguration> configs = Properties.Settings.Default.PlugIn_Configurations.ToList();

                if (configs != null)
                {
                    foreach (PlugInConfiguration config in configs)
                    {
                        if (config.enabled && plugins != null)
                        {
                            foreach (Lazy<PlugIn> lplugin in plugins.ToList())
                            {
                                if (lplugin != null)
                                {
                                    try
                                    {
                                        PlugIn plugin = lplugin.Value;
                                        plugin.Closing();
                                    }
                                    catch (Exception ex) { }
                                }
                            }
                        }
                    }
                }
            }

            Properties.Settings.Default.Save();

        }

        #region "Single Instance"

        public bool SignalExternalCommandLineArgs(IList<string> args)
        {
            // handle command line arguments of second instance
            // …

            return true;
        }

        #endregion

        #endregion

        #region "Pages"

        ObservableCollection<TH_TabHeader_Top> pagetabheaders;
        public ObservableCollection<TH_TabHeader_Top> PageTabHeaders
        {
            get
            {
                if (pagetabheaders == null) pagetabheaders = new ObservableCollection<TH_TabHeader_Top>();
                return pagetabheaders;
            }
            set
            {
                pagetabheaders = value;
            }
        }

        public void AddPageAsTab(object page, string title, ImageSource image)
        {
            // Check to see if Page already exists
            TH_TabItem TI = Pages_TABCONTROL.Items.Cast<TH_TabItem>().ToList().Find(x => x.Title.ToString().ToLower() == title.ToLower());

            if (TI == null)
            {
                TI = new TH_TabItem();
                TI.Content = CreatePage(page); ;
                TI.Title = title;
                TI.Closed += TI_Closed;

                TH_TabHeader_Top header = new TH_TabHeader_Top();
                header.Text = title;
                header.Image = image;
                header.TabParent = TI;
                header.Clicked += header_Clicked;
                header.CloseClicked += header_CloseClicked;
                TI.TH_Header = header;
                
                int zlevel = int.MaxValue;

                // Move all of the existing tabs to the front so that the new tab is behind it (so it can "slide" in behind it)
                for (int x = 0; x <= PageTabHeaders.Count - 1; x++)
                {
                    TH_TabHeader_Top tabHeader = (TH_TabHeader_Top)PageTabHeaders[x];
                    Panel.SetZIndex(tabHeader, zlevel - x);
                }

                PageTabHeaders.Add(header);

                Panel.SetZIndex(header, -1);

                TI.Template = (ControlTemplate)TryFindResource("TabItemControlTemplate");

                Pages_TABCONTROL.Items.Add(TI);
                Pages_TABCONTROL.SelectedItem = TI;
            }
            else
            {
                Pages_TABCONTROL.SelectedItem = TI;
            }
        }

        void TI_Closed(TH_TabItem tab)
        {
            List<TH_TabHeader_Top> headers = new List<TH_TabHeader_Top>();
            headers.AddRange(PageTabHeaders);

            List<TH_TabItem> tabs = Pages_TABCONTROL.Items.OfType<TH_TabItem>().ToList();

            foreach (TH_TabHeader_Top header in headers)
            {
                if (tabs.Find(x => x.Title.ToLower() == header.Text.ToLower()) == null)
                    PageTabHeaders.Remove(header);
            }
        }

        public TH_Page CreatePage(object control)
        {
            TH_Page Result = new TH_Page();

            Result.PageContent = control;

            return Result;
        }

        public void ClosePage(string pageName)
        {
            TH_TabItem ti = Pages_TABCONTROL.Items.Cast<TH_TabItem>().ToList().Find(x => x.Title.ToString().ToLower() == pageName.ToLower());
            if (ti != null)
            {
                ti.Close();

                int index = 0;

                if (Pages_TABCONTROL.SelectedIndex < Pages_TABCONTROL.Items.Count - 1)
                    index = Math.Min(Pages_TABCONTROL.Items.Count, Pages_TABCONTROL.SelectedIndex + 1);
                else
                    index = Math.Max(0, Pages_TABCONTROL.SelectedIndex - 1);

                Pages_TABCONTROL.SelectedItem = Pages_TABCONTROL.Items[index];
            }  
        }

        void header_Clicked(TH_TabHeader_Top header)
        {
            if (header.TabParent != null) Pages_TABCONTROL.SelectedItem = header.TabParent;
        }

        void header_CloseClicked(TH_TabHeader_Top header)
        {
            int index = 0; 

            if (header.IsSelected)
            {
                if (Pages_TABCONTROL.SelectedIndex < Pages_TABCONTROL.Items.Count - 1) 
                    index = Math.Min(Pages_TABCONTROL.Items.Count, Pages_TABCONTROL.SelectedIndex + 1);
                else 
                    index = Math.Max(0, Pages_TABCONTROL.SelectedIndex - 1);

                Pages_TABCONTROL.SelectedItem = Pages_TABCONTROL.Items[index];
            }

            if (header.TabParent != null)
            {
                header.TabParent.Close();
            }
        }

        private void Pages_TABCONTROL_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender.GetType() == typeof(TabControl))
            {
                TabControl tc = (TabControl)sender;

                for (int x = 0; x <= PageTabHeaders.Count - 1; x++)
                {
                    if (x != tc.SelectedIndex)
                    {
                        PageTabHeaders[x].IsSelected = false;
                    }
                    else
                    {
                        PageTabHeaders[x].IsSelected = true;
                    }

                    ZoomLevel = 1;
                }
            }
        }

        void ChangePage_Forward()
        {
            if (Pages_TABCONTROL.Items.Count > 0)
            {
                int index = Pages_TABCONTROL.SelectedIndex;
                int max = Pages_TABCONTROL.Items.Count - 1;

                if (index < max)
                {
                    Pages_TABCONTROL.SelectedItem = Pages_TABCONTROL.Items[index + 1];
                }
                else
                {
                    Pages_TABCONTROL.SelectedItem = Pages_TABCONTROL.Items[0];
                }
            }
        }

        void ChangePage_Backward()
        {
            if (Pages_TABCONTROL.Items.Count > 0)
            {
                int index = Pages_TABCONTROL.SelectedIndex;
                int max = Pages_TABCONTROL.Items.Count - 1;

                if (index > 0)
                {
                    Pages_TABCONTROL.SelectedItem = Pages_TABCONTROL.Items[index - 1];
                }
                else
                {
                    Pages_TABCONTROL.SelectedItem = Pages_TABCONTROL.Items[max];
                }
            }
        }

        #region "Zoom"

        public double ZoomLevel
        {
            get { return (double)GetValue(ZoomLevelProperty); }
            set
            { 
                SetValue(ZoomLevelProperty, value);

                if (Pages_TABCONTROL.SelectedIndex >= 0)
                {
                    TH_TabItem tab = (TH_TabItem)Pages_TABCONTROL.Items[Pages_TABCONTROL.SelectedIndex];

                    TH_Page page = (TH_Page)tab.Content;
                    page.ZoomLevel = value;

                    ZoomLevelDisplay = value.ToString("P0");

                    if (ZoomLevelChanged != null) ZoomLevelChanged(value);
                }

            }
        }

        public static readonly DependencyProperty ZoomLevelProperty =
            DependencyProperty.Register("ZoomLevel", typeof(double), typeof(MainWindow), new PropertyMetadata(1D));


        public string ZoomLevelDisplay
        {
            get { return (string)GetValue(ZoomLevelDisplayProperty); }
            set 
            { 
                SetValue(ZoomLevelDisplayProperty, value);
            }
        }

        public static readonly DependencyProperty ZoomLevelDisplayProperty =
            DependencyProperty.Register("ZoomLevelDisplay", typeof(string), typeof(MainWindow), new PropertyMetadata("100%"));

        public delegate void ZoomLevelChanged_Handler(double zoomlevel);
        public event ZoomLevelChanged_Handler ZoomLevelChanged;

        #endregion

        void Pages_Initialize()
        {
            About_Initialize();
            DeviceManager_Initialize();
            AccountManager_Initialize();
            Options_Initialize();
            Plugins_Initialize();
        }

        #region "About"

        public About.Manager aboutManager;

        void About_Initialize()
        {
            aboutManager = new About.Manager();

            aboutManager.AddPage(new About.Pages.Information.Page());
            aboutManager.AddPage(new About.Pages.License.Page());
        }

        public void About_Open()
        {
            AddPageAsTab(aboutManager, "About", new BitmapImage(new Uri("pack://application:,,,/TrakHound-Client;component/Resources/About_01.png")));
        }

        #endregion

        #region "Device Manager"

        public DeviceManager devicemanager;

        void DeviceManager_Initialize()
        {

        }

        public void DeviceManager_Open()
        {
            AddPageAsTab(devicemanager, "Device Manager", new BitmapImage(new Uri("pack://application:,,,/TrakHound-Client;component/Resources/Root.png")));
        }

        #endregion

        #region "Account Manager"

        public Account_Management.Manager accountManager;

        TH_UserManagement.Create.Page accountpage;

        //CreateAccountPage accountpage;

        //class CreateAccountPage : AboutPage
        //{
        //    public CreateAccountPage()
        //    {
        //        ParentPage = new TH_UserManagement.Create.Page();
        //        PageContent = ParentPage;
        //        ParentPage.UserChanged += ParentPage_UserChanged;
        //    }

        //    void ParentPage_UserChanged(UserConfiguration userConfig)
        //    {
        //        if (UserChanged != null) UserChanged(userConfig);
        //    }

        //    public void LoadUser(UserConfiguration userConfig, Database_Settings userDatabaseSettings)
        //    {
        //        ParentPage.LoadUserConfiguration(userConfig, userDatabaseSettings);
        //    }

        //    public TH_UserManagement.Create.Page ParentPage;

        //    public string PageName { get { return ParentPage.PageName; } }

        //    public ImageSource Image { get { return ParentPage.Image; } }

        //    public object PageContent { get; set; }

        //    public delegate void UserChanged_Handler(UserConfiguration userConfig);
        //    public event UserChanged_Handler UserChanged;
        //}

        void AccountManager_Initialize()
        {
            accountManager = new Account_Management.Manager();

            accountpage = new TH_UserManagement.Create.Page();
            accountpage.UserChanged += accountpage_UserChanged;

            //accountpage = new CreateAccountPage();
            //accountpage.UserChanged += accountpage_UserChanged;
        }

        void accountpage_UserChanged(UserConfiguration userConfig)
        {
            if (LoginMenu != null) LoginMenu.LoadUserConfiguration(userConfig);
        }

        public void AccountManager_Open()
        {
            accountManager.ClearPages();

            accountManager.AddPage(accountpage);
            //accountManager.AddPage(accountpage);
            accountManager.currentUser = currentuser;

            AddPageAsTab(accountManager, "Acount Manager", new BitmapImage(new Uri("pack://application:,,,/TrakHound-Client;component/Resources/blank_profile_01_sm.png")));
        }

        #endregion

        #region "Options"

        Options.Manager optionsManager;

        void Options_Initialize()
        {
            optionsManager = new Options.Manager();

            //optionsManager.AddPage(new Options.Pages.General.Page());
            optionsManager.AddPage(new Options.Pages.Updates.Page());
        }

        public void Options_Open()
        {
            AddPageAsTab(optionsManager, "Options", new BitmapImage(new Uri("pack://application:,,,/TrakHound-Client;component/Resources/options_gear_30px.png")));
        }

        #endregion

        #region "Plugins"

        Plugins.Manager pluginsManager;

        Plugins.Pages.Installed.Page pluginsPage;

        void Plugins_Initialize()
        {
            pluginsManager = new Plugins.Manager();

            pluginsPage = new Plugins.Pages.Installed.Page();
            pluginsManager.AddPage(pluginsPage);
        }

        public void Plugins_Open()
        {
            AddPageAsTab(pluginsManager, "Plugins", new BitmapImage(new Uri("pack://application:,,,/TrakHound-Client;component/Resources/Rocket_02.png")));
        }

        #endregion

        #endregion

        #region "User Login"

        #region "Properties"

        public string CurrentUsername
        {
            get { return (string)GetValue(CurrentUsernameProperty); }
            set { SetValue(CurrentUsernameProperty, value); }
        }

        public static readonly DependencyProperty CurrentUsernameProperty =
            DependencyProperty.Register("CurrentUsername", typeof(string), typeof(MainWindow), new PropertyMetadata(null));


        public ImageSource ProfileImage
        {
            get { return (ImageSource)GetValue(ProfileImageProperty); }
            set { SetValue(ProfileImageProperty, value); }
        }

        public static readonly DependencyProperty ProfileImageProperty =
            DependencyProperty.Register("ProfileImage", typeof(ImageSource), typeof(MainWindow), new PropertyMetadata(null));

        
        public bool LoggedIn
        {
            get { return (bool)GetValue(LoggedInProperty); }
            set { SetValue(LoggedInProperty, value); }
        }

        public static readonly DependencyProperty LoggedInProperty =
            DependencyProperty.Register("LoggedIn", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        #endregion

        public delegate void CurrentUserChanged_Handler(UserConfiguration userConfig);
        public event CurrentUserChanged_Handler CurrentUserChanged;

        private void Login_GRID_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            LoginMenu.Shown = true;
        }

        private void LoginMenu_CurrentUserChanged(UserConfiguration userConfig)
        {
            CurrentUser = userConfig;
        }

        private void LoginMenu_ShownChanged(bool val)
        {
            
        }

        private void LoginMenu_MyAccountClicked()
        {
            AccountManager_Open();
        }

        private void LoginMenu_CreateClicked()
        {
            AccountManager_Open();
        }

        UserConfiguration currentuser;
        public UserConfiguration CurrentUser
        {
            get { return currentuser; }
            set
            {
                currentuser = value;

                if (devicemanager != null) devicemanager.CurrentUser = currentuser;
             
                if (currentuser != null)
                {
                    CurrentUsername = TH_Global.Formatting.UppercaseFirst(currentuser.username);
                    LoggedIn = true;
                }
                else
                {
                    LoggedIn = false;
                    CurrentUsername = null;
                }

                LoadDevices();

                if (accountpage != null) accountpage.LoadUserConfiguration(currentuser, UserDatabaseSettings);

                UpdatePlugInUser(currentuser, UserDatabaseSettings);

                if (CurrentUserChanged != null) CurrentUserChanged(currentuser);
            }
        }

        public Database_Settings UserDatabaseSettings;

        void ReadUserManagementSettings()
        {
            DatabasePluginReader dpr = new DatabasePluginReader();

            string localPath = AppDomain.CurrentDomain.BaseDirectory + "UserConfiguration.Xml";
            string systemPath = TH_Global.FileLocations.TrakHound + @"\" + "UserConfiguration.Xml";

            string configPath;

            // systemPath takes priority (easier for user to navigate to)
            if (File.Exists(systemPath)) configPath = systemPath;
            else configPath = localPath;

            Logger.Log(configPath);

            UserManagementSettings userSettings = UserManagementSettings.ReadConfiguration(configPath);

            if (userSettings != null)
            {
                if (userSettings.Databases.Databases.Count > 0)
                {
                    UserDatabaseSettings = userSettings.Databases;
                    Global.Initialize(UserDatabaseSettings);
                }
            }
        }

        #endregion

        #region "Plugin Launcher"

        private void PluginLauncher_BT_Clicked(Button_01 bt)
        {
            Point point = bt.TransformToAncestor(Main_GRID).Transform(new Point(0, 0));
            PluginLauncher.Margin = new Thickness(0, point.Y + bt.RenderSize.Height, 0, 0);

            PluginLauncher.Shown = true;
        }

        private void PluginLauncher_ShownChanged(bool val)
        {
            PluginLauncher_BT.IsSelected = val;
        }

        void AddAppToList(PlugIn plugin)
        {
            if (plugin.ShowInAppMenu)
            {
                Plugins.Launcher.PluginItem item = new Plugins.Launcher.PluginItem();
                item.plugin = plugin;
                item.Text = plugin.Title;
                item.Image = plugin.Image;
                item.Clicked += item_Clicked;

                if (!PluginLauncher.Plugins.Contains(item)) PluginLauncher.Plugins.Add(item);
            }
        }

        void item_Clicked(Plugins.Launcher.PluginItem item)
        {
            if (item.plugin != null) AddPageAsTab(item.plugin, item.plugin.Title, item.plugin.Image);
            PluginLauncher.Shown = false;
        }

        void RemoveAppFromList(PlugIn plugin)
        {



        }

        #endregion

        #region "Main Menu Button"

        private void MainMenu_BT_Clicked(Button_01 bt)
        {
            Point point = bt.TransformToAncestor(Main_GRID).Transform(new Point(0, 0));
            MainMenu.Margin = new Thickness(0, point.Y + bt.RenderSize.Height, 5, 0);

            MainMenu.Shown = true;
        }

        private void MainMenu_ShownChanged(bool val)
        {
            MainMenu_BT.IsSelected = val;
        }

        #endregion

        #region "Toolbars"

        #region "Main Menu"

        public bool MainMenuBar_Show
        {
            get { return (bool)GetValue(MainMenuBar_ShowProperty); }
            set 
            { 
                SetValue(MainMenuBar_ShowProperty, value); 

                if (value)
                {
                    MainMenuBar_Shown = true;

                    MainMenuBarLoaded_TIMER = new System.Timers.Timer();
                    MainMenuBarLoaded_TIMER.Interval = 200;
                    MainMenuBarLoaded_TIMER.Elapsed += MainMenuBarLoaded_TIMER_Elapsed;
                    MainMenuBarLoaded_TIMER.Enabled = true;
                }
            
            }
        }

        public static readonly DependencyProperty MainMenuBar_ShowProperty =
            DependencyProperty.Register("MainMenuBar_Show", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        public bool MainMenuBar_Shown
        {
            get { return (bool)GetValue(MainMenuBar_ShownProperty); }
            set { SetValue(MainMenuBar_ShownProperty, value); }
        }

        public static readonly DependencyProperty MainMenuBar_ShownProperty =
            DependencyProperty.Register("MainMenuBar_Shown", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));


        System.Timers.Timer MainMenuBarLoaded_TIMER;

        void MainMenuBarLoaded_TIMER_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (sender.GetType() == typeof(System.Timers.Timer))
            {
                System.Timers.Timer timer = (System.Timers.Timer)sender;
                timer.Enabled = false;
            }

            this.Dispatcher.BeginInvoke(new Action(MainMenuBarLoaded_TIMER_Elapsed_GUI));
        }

        void MainMenuBarLoaded_TIMER_Elapsed_GUI()
        {
            MainMenuBar_Shown = false;
        }

        private void MenuItem_About_Click(object sender, RoutedEventArgs e)
        {
            About_Open();
        }

        private void MenuItem_Options_Click(object sender, RoutedEventArgs e) { }

        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MenuItem_RestoreDefaultConfiguration_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Reset();
        }

        #endregion

        object CreateMenuItemIcon(ImageSource img)
        {

            Rectangle rect = new Rectangle();
            rect.Width = 20;
            rect.Height = 20;
            rect.Fill = Brush_Functions.GetSolidBrushFromResource(this, "DWBlue");

            ImageBrush imgBrush = new ImageBrush();
            imgBrush.ImageSource = img;
            imgBrush.Stretch = Stretch.Uniform;

            rect.Resources.Add("IMG", imgBrush);

            Style style = new System.Windows.Style();
            style.TargetType = typeof(Rectangle);

            Setter setter = new Setter();
            setter.Property = Rectangle.OpacityMaskProperty;
            setter.Value = rect.TryFindResource("IMG");

            style.Setters.Add(setter);

            rect.Style = style;

            return rect;

        }

        #endregion

        #region "PlugIns"

        List<PlugInConfiguration> EnabledPlugIns;

        void LoadPlugIns()
        {
            EnabledPlugIns = new List<PlugInConfiguration>();

            Splash_UpdateStatus("...Loading Page Plugins");

            PagePlugIns_Find();

            PlugIns_Load();
        }

        #region "Pages"

        Plugin_Container plugin_container;

        class Plugin_Container
        {
            // Store Plugins
            [ImportMany(typeof(PlugIn))]
            public IEnumerable<Lazy<PlugIn>> plugins { get; set; }
        }

        public List<Lazy<PlugIn>> plugins;

        public void PagePlugIns_Find()
        {

            plugins = new List<Lazy<PlugIn>>();

            string path;

            // Load from System Directory first (easier for user to navigate to 'C:\TrakHound\')
            path = TH_Global.FileLocations.TrakHound + @"\PlugIns\";
            if (Directory.Exists(path)) PagePlugIns_Find_Recursive(path);

            // Load from App root Directory (doesn't overwrite plugins found in System Directory)
            path = AppDomain.CurrentDomain.BaseDirectory + @"PlugIns\";
            if (Directory.Exists(path)) PagePlugIns_Find_Recursive(path);


            // Add Buttons for Plugins on PlugIn Options page
            if (Properties.Settings.Default.PlugIn_Configurations != null && pluginsPage != null)
            {
                pluginsPage.ClearInstalledItems();

                foreach (PlugInConfiguration config in Properties.Settings.Default.PlugIn_Configurations.ToList())
                {
                    pluginsPage.AddInstalledItem(config);
                }
            }

        }

        List<string> DefaultEnablePlugins = new List<string> { "dashboard", "device compare", "table manager", "status data" };

        void PagePlugIns_Find_Recursive(string Path)
        {
            try
            {
                plugin_container = new Plugin_Container();

                var PageCatalog = new DirectoryCatalog(Path);
                var PageContainer = new CompositionContainer(PageCatalog);
                PageContainer.SatisfyImportsOnce(plugin_container);

                if (plugin_container.plugins != null)
                {

                    List<PlugInConfiguration> configs;

                    if (Properties.Settings.Default.PlugIn_Configurations != null)
                    {
                        configs = Properties.Settings.Default.PlugIn_Configurations.ToList();
                    }
                    else
                    {
                        configs = new List<PlugInConfiguration>();
                    }

                    foreach (Lazy<PlugIn> lplugin in plugin_container.plugins.ToList())
                    {
                        try
                        {
                            PlugIn plugin = lplugin.Value;

                            Console.WriteLine(plugin.Title + " Found in '" + Path + "'");

                            PlugInConfiguration config = configs.Find(x => x.name.ToUpper() == plugin.Title.ToUpper());
                            if (config == null)
                            {
                                Console.WriteLine("PlugIn Configuration created for " + plugin.Title);
                                config = new PlugInConfiguration();
                                config.name = plugin.Title;
                                config.description = plugin.Description;

                                // Automatically enable basic Plugins by TrakHound
                                if (DefaultEnablePlugins.Contains(config.name.ToLower()))
                                {
                                    config.enabled = true;
                                    Console.WriteLine("Default TrakHound Plugin Initialized as 'Enabled'");
                                }
                                else config.enabled = false;

                                config.parent = plugin.DefaultParent;
                                config.category = plugin.DefaultParentCategory;

                                config.SubCategories = plugin.SubCategories;

                                configs.Add(config);
                            }
                            else Console.WriteLine("PlugIn Configuration found for " + plugin.Title);

                            if (config.parent == null) config.EnabledChanged += PageConfig_EnabledChanged;

                            plugins.Add(lplugin);

                        }
                        catch (Exception ex)
                        {
                            Message_Center.Message_Data mData = new Message_Center.Message_Data();
                            mData.title = "PlugIn Error";
                            mData.text = "Error during plugin intialization";
                            mData.additionalInfo = ex.Message;

                            messageCenter.AddError(mData);
                        }
                    }


                    // Create a copy of configs since we are modifying it
                    List<PlugInConfiguration> tempConfigs = new List<PlugInConfiguration>();
                    tempConfigs.AddRange(configs);

                    foreach (PlugInConfiguration config in tempConfigs)
                    {
                        if (configs.Contains(config))
                        {
                            if (config.parent != null)
                            {
                                if (config.category != null)
                                {
                                    PlugInConfiguration match1 = configs.Find(x => x.name.ToUpper() == config.parent.ToUpper());
                                    if (match1 != null)
                                    {
                                        PlugInConfigurationCategory match2 = match1.SubCategories.Find(x => x.name.ToUpper() == config.category.ToUpper());
                                        if (match2 != null)
                                        {
                                            configs.Remove(config);
                                            if (match2.PlugInConfigurations.Find(x => x.name.ToUpper() == config.name.ToUpper()) == null)
                                            {
                                                match2.PlugInConfigurations.Add(config);
                                            }

                                        }
                                    }
                                }
                            }
                        }
                    }

                    Properties.Settings.Default.PlugIn_Configurations = configs;
                    Properties.Settings.Default.Save();
                }

                foreach (string directory in Directory.GetDirectories(Path, "*", SearchOption.AllDirectories))
                {
                    PagePlugIns_Find_Recursive(directory);
                }
            }
            catch (Exception ex)
            {
                Message_Center.Message_Data mData = new Message_Center.Message_Data();
                mData.title = "Plugin Load Error";
                mData.text = "Error loading Plugins from " + Path;
                mData.additionalInfo = ex.Message;

                messageCenter.AddError(mData);
            }
        }

        void PageConfig_EnabledChanged(PlugInConfiguration config)
        {
            if (config.enabled) PlugIns_Load(config);
            else PlugIns_Unload(config);

            Properties.Settings.Default.Save();

        }

        public void PlugIns_Load()
        {
            if (Properties.Settings.Default.PlugIn_Configurations != null)
            {
                foreach (PlugInConfiguration config in Properties.Settings.Default.PlugIn_Configurations.ToList())
                {
                    PlugIns_Load(config);
                }
            }
        }

        public void PlugIns_Load(PlugInConfiguration config)
        {
            if (config != null)
            {
                if (!EnabledPlugIns.Contains(config))
                {
                    if (config.enabled)
                    {
                        if (plugins != null)
                        {
                            Lazy<PlugIn> lplugin = plugins.Find(x => x.Value.Title.ToUpper() == config.name.ToUpper());
                            if (lplugin != null)
                            {
                                try
                                {
                                    PlugIn plugin = lplugin.Value;

                                    Splash_UpdateStatus("...Loading Plugin : " + plugin.Title);
                                    Splash_AddPlugin(plugin);

                                    //CP.Devices = Devices;
                                    plugin.DataEvent += Plugin_DataEvent;
                                    plugin.ShowRequested += Plugin_ShowRequested;
                                    plugin.SubCategories = config.SubCategories;

                                    plugin.PlugIns = new List<PlugIn>();

                                    if (plugin.SubCategories != null)
                                    {
                                        foreach (PlugInConfigurationCategory subcategory in plugin.SubCategories)
                                        {
                                            foreach (PlugInConfiguration subConfig in subcategory.PlugInConfigurations)
                                            {
                                                Lazy<PlugIn> clplugin = plugins.Find(x => x.Value.Title.ToUpper() == subConfig.name.ToUpper());
                                                if (clplugin != null)
                                                {
                                                    plugin.PlugIns.Add(clplugin.Value);
                                                }
                                            }
                                        }
                                    }

                                    plugin.Initialize();

                                    AddAppToList(plugin);

                                    if (plugin.OpenOnStartUp)
                                    {
                                        AddPageAsTab(plugin, plugin.Title, plugin.Image);
                                    }

                                    PlugIns_CreateOptionsPage(plugin);

                                    EnabledPlugIns.Add(config);
                                }
                                catch (Exception ex)
                                {
                                    Message_Center.Message_Data mData = new Message_Center.Message_Data();
                                    mData.title = "PlugIn Error";
                                    mData.text = "Error during plugin load";
                                    mData.additionalInfo = ex.Message;

                                    messageCenter.AddError(mData);
                                }
                            }
                        }
                    }
                }
            }
        }

        void Plugin_ShowRequested(PluginShowInfo info)
        {
            PlugIn plugin = null;

            if (info.Page.GetType() == typeof(PlugIn))
            {
                plugin = (PlugIn)info.Page;
            }

            string title = info.PageTitle;
            if (info.PageTitle == null && plugin != null) title = plugin.Title;

            ImageSource image = info.PageImage;
            if (info.PageImage == null && plugin != null) image = plugin.Image;

            object page = info.Page;

            AddPageAsTab(page, title, image);
        }

        void Plugin_DataEvent(DataEvent_Data de_d)
        {
            if (Properties.Settings.Default.PlugIn_Configurations != null)
            {
                List<PlugInConfiguration> configs = Properties.Settings.Default.PlugIn_Configurations.ToList();

                foreach (PlugInConfiguration config in configs)
                {
                    if (config.enabled)
                    {
                        Lazy<PlugIn> lplugin = plugins.ToList().Find(x => x.Value.Title == config.name);
                        if (lplugin != null)
                        {
                            PlugIn plugin = lplugin.Value;
                            plugin.Update_DataEvent(de_d);
                        }
                    }
                }
            }
        }

        public void PlugIns_Unload(PlugInConfiguration config)
        {
            if (config != null)
            {
                if (!config.enabled)
                {
                    // Remove TabItem
                    foreach (TH_TabItem ti in Pages_TABCONTROL.Items.OfType<TH_TabItem>().ToList())
                    {
                        if (ti.Header != null)
                        {
                            if (ti.Header.ToString().ToUpper() == config.name.ToUpper())
                            {
                                if (ti.Content.GetType() == typeof(Grid))
                                {
                                    Grid grid = ti.Content as Grid;
                                    grid.Children.Clear();
                                }
                                Pages_TABCONTROL.Items.Remove(ti);
                            }
                        }
                    }

                    if (optionsManager != null)
                    {
                        foreach (ListButton lb in optionsManager.Pages_STACK.Children.OfType<ListButton>().ToList())
                        {
                            if (lb.Text.ToUpper() == config.name.ToUpper())
                            {
                                optionsManager.Pages_STACK.Children.Remove(lb);
                            }
                        }
                    }

                    if (EnabledPlugIns.Contains(config)) EnabledPlugIns.Remove(config);
                }
            }
        }

        void PlugIns_CreateOptionsPage(PlugIn plugin)
        {

            if (plugin.Options != null) optionsManager.AddPage(plugin.Options);

        }


        void UpdatePlugInDevices(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (plugins != null)
            {
                foreach (Lazy<PlugIn> lplugin in plugins.ToList())
                {
                    PlugIn plugin = lplugin.Value;

                    this.Dispatcher.BeginInvoke(new Action<PlugIn, object, NotifyCollectionChangedEventArgs>(UpdatePluginDevices), Priority, new object[] { plugin, sender, e });
                }
            }       
        }

        void UpdatePluginDevices(PlugIn plugin, object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {



        }


        void UpdatePlugInUser(UserConfiguration userConfig, Database_Settings userDatabaseSettings)
        {
            if (plugins != null)
            {
                foreach (Lazy<PlugIn> lplugin in plugins.ToList())
                {
                    PlugIn plugin = lplugin.Value;

                    this.Dispatcher.BeginInvoke(new Action<PlugIn, UserConfiguration, Database_Settings>(UpdatePlugInUser), Priority, new object[] { plugin, userConfig, userDatabaseSettings });
                }
            }
        }

        void UpdatePlugInUser(PlugIn plugin, UserConfiguration userConfig, Database_Settings userDatabaseSettings)
        {
            plugin.UserDatabaseSettings = userDatabaseSettings;
            plugin.CurrentUser = userConfig;
           
        }

        #endregion

        #endregion

        #region "Devices"

        public ObservableCollection<Configuration> Devices { get; set; }

        #region "Load Devices"

        const System.Windows.Threading.DispatcherPriority priority = System.Windows.Threading.DispatcherPriority.Background;

        Thread loaddevices_THREAD;

        void LoadDevices_Initialize()
        {
            Devices = new ObservableCollection<Configuration>();
            Devices.CollectionChanged += Devices_CollectionChanged;
        }


        void LoadDevices()
        {
            Devices.Clear();

            if (loaddevices_THREAD != null) loaddevices_THREAD.Abort();

            loaddevices_THREAD = new Thread(new ThreadStart(LoadDevices_Worker));
            loaddevices_THREAD.Start();
        }

        void LoadDevices_Worker()
        {
            List<Configuration> configs = new List<Configuration>();

            if (currentuser != null)
            {
                string[] tablenames = Configurations.GetConfigurationsForUser(currentuser, UserDatabaseSettings);

                if (tablenames != null)
                {
                    foreach (string tablename in tablenames)
                    {
                        Configuration config = GetConfiguration(tablename);

                        //this.Dispatcher.BeginInvoke((Action)delegate { LoadDevices_GUI(config); }, priority);

                        this.Dispatcher.BeginInvoke(new Action<Configuration>(LoadDevices_GUI), priority, new object[] { config });
                    }
                }
            }
            // If not logged in Read from File in 'C:\TrakHound\'
            else
            {
                configs = ReadConfigurationFile();

                this.Dispatcher.BeginInvoke(new Action<List<Configuration>>(LoadDevices_GUI), priority, new object[] { configs });
            }

            this.Dispatcher.BeginInvoke(new Action(LoadDevices_Finished), priority, new object[] { });
        }

        void LoadDevices_Worker_GetConfiguration(object o)
        {
            if (o != null)
            {
                string tablename = o.ToString();

                DataTable dt = Configurations.GetConfigurationTable(tablename, UserDatabaseSettings);
                if (dt != null)
                {
                    XmlDocument xml = Converter.TableToXML(dt);
                    Configuration config = Configuration.ReadConfigFile(xml);
                    if (config != null)
                    {
                        config.TableName = tablename;

                        if (config.ClientEnabled)
                        {
                            // Initialize Database Configurations
                            Global.Initialize(config.Databases_Client);

                            this.Dispatcher.BeginInvoke(new Action<Configuration>(LoadDevices_GUI), priority, new object[] { config });
                        }
                    }
                }
            }
        }

        Configuration GetConfiguration(string tablename)
        {
            Configuration result = null;

            DataTable dt = Configurations.GetConfigurationTable(tablename, UserDatabaseSettings);
            if (dt != null)
            {
                XmlDocument xml = Converter.TableToXML(dt);
                Configuration config = Configuration.ReadConfigFile(xml);
                if (config != null)
                {
                    config.TableName = tablename;

                    if (config.ClientEnabled)
                    {
                        // Initialize Database Configurations
                        Global.Initialize(config.Databases_Client);

                        result = config;

                        //this.Dispatcher.BeginInvoke(new Action<Configuration>(LoadDevices_GUI), priority, new object[] { config });
                    }
                }
            }

            return result;
        }

        void LoadDevices_GUI(Configuration config)
        {
            if (config != null) Devices.Add(config);
        }

        void LoadDevices_Finished()
        {
            if (Devices.Count == 0 && currentuser != null)
            {
                if (devicemanager != null) devicemanager.AddDevice();
                DeviceManager_Open();
            }

            DevicesMonitor_Initialize();
        }

        void Devices_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdatePlugInDevices(sender, e);
        }


        List<Configuration> GetConfigurations()
        {
            List<Configuration> result = new List<Configuration>();

            string[] tablenames = Configurations.GetConfigurationsForUser(currentuser, UserDatabaseSettings);

            if (tablenames != null)
            {
                foreach (string tablename in tablenames)
                {
                    DataTable dt = Configurations.GetConfigurationTable(tablename, UserDatabaseSettings);
                    if (dt != null)
                    {
                        XmlDocument xml = Converter.TableToXML(dt);
                        Configuration config = Configuration.ReadConfigFile(xml);
                        if (config != null)
                        {
                            config.TableName = tablename;

                            if (config.ClientEnabled) result.Add(config);
                        }
                    }
                }
            }

            return result;
        }

        void LoadDevices_GUI(List<Configuration> configs)
        {
            Devices.Clear();

            if (configs != null)
            {
                int index = 0;

                DatabasePluginReader dpr = new DatabasePluginReader();

                // Create DevicesList based on Configurations
                foreach (Configuration config in configs)
                {
                    config.Index = index;

                    //if (config.Remote) { StartMonitor(config); }

                    if (config.ClientEnabled)
                    {
                        Devices.Add(config);

                        // Initialize Database Configurations
                        Global.Initialize(config.Databases_Client);
                    }

                    index += 1;
                }
            }

            // If a user is logged in but no Devices are found then open up Device Manager and Add Device page
            if (CurrentUser != null && Devices.Count == 0)
            {
                if (devicemanager != null) devicemanager.AddDevice();

                DeviceManager_Open();
            }

            //UpdatePlugInDevices();

            DevicesMonitor_Initialize();
        }


        #region "Offline Configurations"

        List<Configuration> ReadConfigurationFile()
        {
            List<Configuration> result = new List<Configuration>();

            //UpdateExceptionsThrown = new List<string>();

            string configPath;

            string localPath = AppDomain.CurrentDomain.BaseDirectory + @"\" + "Configuration.Xml";
            string systemPath = TH_Global.FileLocations.TrakHound + @"\" + "Configuration.Xml";

            // systemPath takes priority (easier for user to navigate to)
            if (File.Exists(systemPath)) configPath = systemPath;
            else configPath = localPath;

            if (System.IO.File.Exists(configPath))
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(configPath);

                foreach (XmlNode Node in doc.DocumentElement.ChildNodes)
                {
                    if (Node.NodeType == XmlNodeType.Element)
                    {
                        switch (Node.Name.ToLower())
                        {
                            case "devices":
                                foreach (XmlNode ChildNode in Node.ChildNodes)
                                {
                                    if (ChildNode.NodeType == XmlNodeType.Element)
                                    {
                                        switch (ChildNode.Name.ToLower())
                                        {
                                            case "device":

                                                Configuration config = GetSettingsFromNode(ChildNode);
                                                if (config != null) result.Add(config);

                                                break;
                                        }
                                    }
                                }
                                break;
                        }
                    }
                }
            }

            return result;
        }

        private Configuration GetSettingsFromNode(XmlNode Node)
        {

            Configuration Result = null;

            string configPath = null;

            foreach (XmlNode ChildNode in Node.ChildNodes)
            {
                switch (ChildNode.Name.ToLower())
                {
                    case "configuration_path": configPath = ChildNode.InnerText; break;
                }
            }

            if (configPath != null)
            {
                configPath = GetConfigurationPath(configPath);

                Result = Configuration.ReadConfigFile(configPath);

                if (Result == null)
                {
                    Message_Center.Message_Data mData = new Message_Center.Message_Data();
                    mData.title = "Device Configuration Error";
                    mData.text = "Could not load device configuration from " + configPath;
                    mData.additionalInfo = "Check to make sure the file exists at "
                        + configPath
                        + " and that the format is correct and restart TrakHound Client."
                        + Environment.NewLine
                        + Environment.NewLine
                        + "For more information please contact us at info@TrakHound.org";
                    if (messageCenter != null) messageCenter.AddError(mData);
                }
            }

            return Result;

        }

        static string GetConfigurationPath(string path)
        {
            // If not full path, try System Dir ('C:\TrakHound\') and then local App Dir
            if (!System.IO.Path.IsPathRooted(path))
            {
                // Remove initial Backslash if contained in "configuration_path"
                if (path[0] == '\\' && path.Length > 1) path.Substring(1);

                string original = path;

                // Check System Path
                path = TH_Global.FileLocations.TrakHound + "\\Configuration Files\\" + original;
                if (File.Exists(path)) return path;

                // Check local app Path
                path = AppDomain.CurrentDomain.BaseDirectory + "Configuration Files\\" + original;
                if (File.Exists(path)) return path;

                // if no files exist return null
                return null;
            }
            else return path;
        }

        #endregion

        #endregion

        #region "Devices Monitor"

        System.Timers.Timer devicesMonitor_TIMER;

        void DevicesMonitor_Initialize()
        {
            //if (devicesMonitor_TIMER != null) devicesMonitor_TIMER.Enabled = false;

            //devicesMonitor_TIMER = new System.Timers.Timer();
            //devicesMonitor_TIMER.Interval = 5000;
            //devicesMonitor_TIMER.Elapsed += devicesMonitor_TIMER_Elapsed;
            //devicesMonitor_TIMER.Enabled = true;
        }

        void devicesMonitor_TIMER_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(DevicesMonitor_Worker), Devices.ToList());
        }

        Thread devicesMonitor_THREAD;

        void DevicesMonitor_Start()
        {
            if (devicesMonitor_THREAD != null) devicesMonitor_THREAD.Abort();

            devicesMonitor_THREAD = new Thread(new ParameterizedThreadStart(DevicesMonitor_Worker));
            devicesMonitor_THREAD.Start(Devices.ToList());
        }

        void DevicesMonitor_Worker(object o)
        {
            bool changed = false;

            if (o != null)
            {
                List<Configuration> devs = (List<Configuration>)o;

                if (currentuser != null)
                {
                    List<Configuration> userConfigs = GetConfigurations();
                    if (userConfigs != null)
                    {
                        foreach (Configuration userConfig in userConfigs)
                        {
                            if (userConfig != null)
                            {
                                Configuration match = devs.Find(x => x.UniqueId == userConfig.UniqueId);
                                if (match != null)
                                {
                                    bool update = userConfig.ClientUpdateId == match.ClientUpdateId;
                                    if (!update)
                                    {
                                        // Configuration has been updated / changed
                                        changed = true;
                                        break;
                                    }
                                }
                                else if (userConfig.ClientEnabled)
                                {
                                    // Configuration has been added or removed
                                    changed = true;
                                    break;
                                }
                            }
                        }
                    }
                    else if (devs.Count > 0) changed = true;
                }
            }

            this.Dispatcher.BeginInvoke(new Action<bool>(DevicesMonitor_Finished), priority, new object[] { changed });
        }

        void DevicesMonitor_Finished(bool changed)
        {
            if (changed)
            {
                if (devicesMonitor_TIMER != null) devicesMonitor_TIMER.Enabled = false;

                LoadDevices();
            }
        }

        #endregion

        #endregion

        #region "Message Center"

        public int NotificationsCount
        {
            get { return (int)GetValue(NotificationsCountProperty); }
            set { SetValue(NotificationsCountProperty, value); }
        }

        public static readonly DependencyProperty NotificationsCountProperty =
            DependencyProperty.Register("NotificationsCount", typeof(int), typeof(MainWindow), new PropertyMetadata(0));

        private void MessageCenter_ToolBarItem_Clicked()
        {
            messageCenter.Shown = !messageCenter.Shown;
        }

        #endregion

        #region "Developer Console"

        public bool DevConsole_Shown
        {
            get { return (bool)GetValue(DevConsole_ShownProperty); }
            set { SetValue(DevConsole_ShownProperty, value); }
        }

        public static readonly DependencyProperty DevConsole_ShownProperty =
            DependencyProperty.Register("DevConsole_Shown", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));


        private void DeveloperConsole_ToolBarItem_Clicked()
        {
            developerConsole.Shown = !developerConsole.Shown;
        }

        private void developerConsole_ShownChanged(bool shown)
        {
            DevConsole_Shown = shown;
        }

        void Log_Initialize()
        {
            LogWriter logWriter = new LogWriter();
            logWriter.Updated += Log_Updated;
            Console.SetOut(logWriter);
        }

        void Log_Updated(string newline)
        {
            this.Dispatcher.BeginInvoke(new Action<string>(Log_Updated_GUI), Priority, new object[] { newline });
        }

        void Log_Updated_GUI(string newline)
        {
            developerConsole.AddLine(newline);
        }

        #endregion

    }

    //class NavigationItem : Button
    //{

    //    public object Data { get; set; }

    //    protected override void OnPreviewMouseDown(System.Windows.Input.MouseButtonEventArgs e)
    //    {
    //        if (Clicked != null) Clicked(Data);
    //    }

    //    public delegate void Clicked_Handler(object data);

    //    public event Clicked_Handler Clicked;

    //}

}
