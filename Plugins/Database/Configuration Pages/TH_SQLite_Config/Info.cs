﻿// Copyright (c) 2016 Feenux LLC, All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Data;

using TH_Global.Functions;
using TH_Plugins.Database;

namespace TH_SQLite_Config
{
    public class Info : IConfigurationInfo
    {

        public string Type { get { return "SQLite"; } }

        public Type ConfigurationPageType { get { return typeof(Page); } }

        public object CreateConfigurationButton(DataTable dt)
        {
            var result = new Button();

            if (dt != null)
            {
                if (dt.Rows.Count > 0)
                {
                    result.DatabasePath = DataTable_Functions.GetTableValue(dt, "address", "database_path", "value");
                }
            }

            return result;
        }

    }
}
