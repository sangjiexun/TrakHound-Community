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

using System.Reflection;
using System.IO;

using TH_PlugIns_Client;
using TH_WPF;

namespace TrakHound_Client.Plugins.Pages.Installed
{
    /// <summary>
    /// Interaction logic for Plugins.xaml
    /// </summary>
    public partial class Page : UserControl, TH_Global.Page
    {
        public Page()
        {
            InitializeComponent();

            mw = Application.Current.MainWindow as MainWindow;
        }

        MainWindow mw;

        public string PageName { get { return "Installed"; } }

        public ImageSource Image { get { return new BitmapImage(new Uri("pack://application:,,,/TrakHound-Client;component/Resources/CheckMark_01.png")); } }


        public void AddInstalledItem(PlugInConfiguration config)
        {
            if (mw != null)
            {
                Lazy<PlugIn> lplugin = mw.plugins.Find(x => x.Value.Title.ToUpper() == config.name.ToUpper());
                if (lplugin != null)
                {
                    PlugIn plugin = lplugin.Value;

                    ListContainer lc = new ListContainer();

                    ListItem li = new ListItem();
                    li.PlugIn_Title = config.name;
                    li.PlugIn_Description = config.description;
                    li.config = config;
                    li.PlugIn_Enabled = config.enabled;
                    li.PlugIn_Image = plugin.Image;

                    // Author Info
                    li.Author_Name = plugin.Author;
                    li.Author_Text = plugin.AuthorText;
                    li.Author_Image = Image_Functions.SetImageSize(plugin.AuthorImage, 0, 30);

                    // Version Info
                    Assembly assembly = Assembly.GetAssembly(plugin.GetType());
                    if (assembly != null)
                    {
                        Version version = assembly.GetName().Version;
                        li.PlugIn_Version = "v" + version.Major.ToString() + "." + version.Minor.ToString() + "." + version.Build.ToString();
                    }

                    lc.RootPlugin_GRID.Children.Add(li);

                    if (config.SubCategories != null)
                    {
                        foreach (PlugInConfigurationCategory subcat in config.SubCategories)
                        {
                            SubCategory sc = new SubCategory();
                            sc.Text = subcat.name;

                            foreach (PlugInConfiguration subConfig in subcat.PlugInConfigurations)
                            {
                                Lazy<PlugIn> lcplugin = mw.plugins.Find(x => x.Value.Title.ToUpper() == subConfig.name.ToUpper());
                                if (lcplugin != null)
                                {
                                    try
                                    {
                                        PlugIn cplugin = lcplugin.Value;

                                        ListItem sli = new ListItem();
                                        sli.PlugIn_Title = subConfig.name;
                                        sli.PlugIn_Description = subConfig.description;
                                        sli.config = subConfig;
                                        sli.PlugIn_Enabled = subConfig.enabled;
                                        sli.PlugIn_Image = cplugin.Image;

                                        // Author Info
                                        sli.Author_Name = cplugin.Author;
                                        sli.Author_Text = cplugin.AuthorText;
                                        sli.Author_Image = Image_Functions.SetImageSize(cplugin.AuthorImage, 0, 30);

                                        // Version Info
                                        Assembly sassembly = Assembly.GetAssembly(cplugin.GetType());
                                        if (sassembly != null)
                                        {
                                            Version sversion = sassembly.GetName().Version;
                                            sli.PlugIn_Version = "v" + sversion.Major.ToString() + "." + sversion.Minor.ToString() + "." + sversion.Build.ToString();
                                        }

                                        sc.SubCategorys_STACK.Children.Add(sli);
                                    }
                                    catch (Exception ex)
                                    {
                                        Message_Center.Message_Data mData = new Message_Center.Message_Data();
                                        mData.title = "PlugIn Error";
                                        mData.text = "Error during plugin Configuration Load";
                                        mData.additionalInfo = ex.Message;

                                        mw.messageCenter.AddError(mData);
                                    }
                                }
                            }

                            lc.SubPlugins_STACK.Children.Add(sc);
                        }
                    }

                    Installed_STACK.Children.Add(lc);

                }
            }
        }

        public void ClearInstalledItems()
        {
            Installed_STACK.Children.Clear();
        }

        public void LoadPluginConfigurations()
        {

            // Load Page Plugin Configurations
            if (Properties.Settings.Default.PlugIn_Configurations != null)
            {
                List<PlugInConfiguration> configs = Properties.Settings.Default.PlugIn_Configurations.ToList();

                foreach (ListContainer lc in Installed_STACK.Children.OfType<ListContainer>())
                {
                    foreach (ListItem root_li in lc.RootPlugin_GRID.Children.OfType<ListItem>())
                    {
                        PlugInConfiguration config = configs.Find(x => x.name.ToUpper() == root_li.PlugIn_Title.ToUpper());
                        if (config != null)
                        {
                            root_li.PlugIn_Enabled = config.enabled;
                        }

                        List<ListItem> subitems = lc.SubPlugins_STACK.Children.OfType<ListItem>().ToList();

                        foreach (PlugInConfigurationCategory subCat in config.SubCategories)
                        {
                            foreach (PlugInConfiguration subConfig in subCat.PlugInConfigurations)
                            {
                                ListItem sub_li = subitems.Find(x => x.PlugIn_Title.ToUpper() == subConfig.name.ToUpper());
                                if (sub_li != null)
                                {
                                    sub_li.PlugIn_Enabled = subConfig.enabled;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            dlg.InitialDirectory = Environment.GetEnvironmentVariable("system");
            dlg.Multiselect = true;
            dlg.Title = "Browse for Client Plugins";
            dlg.Filter = "Client Plugin (*.dll)|*.dll";

            dlg.ShowDialog();

            try
            {
                foreach (string filename in dlg.FileNames.ToList())
                {
                    string pluginPath = TH_Global.FileLocations.TrakHound + @"\PlugIns\";

                    string name = System.IO.Path.GetFileNameWithoutExtension(filename);
                    string ext = System.IO.Path.GetExtension(filename);

                    string suffix = "";
                    int suffixNum = 0;

                    string test = pluginPath + name + suffix + ext;

                    while (File.Exists(test))
                    {
                        suffixNum += 1;
                        suffix = "_" + suffixNum.ToString("00");
                        test = pluginPath + name + suffix + ext;
                    }

                    File.Copy(filename, test);
                }

                if (mw != null) mw.PagePlugIns_Find();

                LoadPluginConfigurations();
            }
            catch (Exception ex) 
            {
                Console.WriteLine("Browse_Click() : " + ex.Message);
            }

        }

    }
}
