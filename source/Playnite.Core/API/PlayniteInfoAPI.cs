using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playnite.API
{
    public class PlayniteInfoAPI : IPlayniteInfoAPI
    {
        private readonly ApplicationMode mode;

        public System.Version ApplicationVersion { get => CoreRuntime.ApplicationVersion(); }

        public ApplicationMode Mode => mode;

        public bool IsPortable => PlaynitePaths.IsPortable;

        public PlayniteInfoAPI(ApplicationMode mode)
        {
            this.mode = mode;
        }

        public bool InOfflineMode => PlayniteEnvironment.InOfflineMode;

        public bool IsDebugBuild => PlayniteEnvironment.IsDebugBuild;

        public bool ThrowAllErrors => PlayniteEnvironment.ThrowAllErrors;
    }
}
