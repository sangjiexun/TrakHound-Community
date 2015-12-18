﻿using System;

using System.ComponentModel.Composition;
using System.Windows.Media;

namespace TH_PlugIns_Client
{
    [InheritedExport(typeof(AboutPage))]
    public interface AboutPage
    {

        string PageName { get; }

        ImageSource Image { get; }

        object PageContent { get; set; }

    }
}
