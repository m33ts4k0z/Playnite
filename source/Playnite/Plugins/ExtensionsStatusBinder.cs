using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Playnite.Plugins
{
    /// <summary>
    /// WPF binding adapter for the core extension host. It deliberately stays
    /// in the UI assembly because it reads the active application singleton.
    /// </summary>
    public class ExtensionsStatusBinder
    {
        public class Status : ObservableObject
        {
            private bool isInstalled;
            public bool IsInstalled { get => isInstalled; set => SetValue(ref isInstalled, value); }
        }

        public Status this[string pluginId]
        {
            get
            {
                var plugin = PlayniteApplication.Current.Extensions?.Plugins
                    .FirstOrDefault(a => a.Value.Description.Id == pluginId).Value;
                return new Status { IsInstalled = plugin != null };
            }

            set => throw new NotSupportedException();
        }
    }
}
