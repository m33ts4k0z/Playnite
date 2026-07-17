using Playnite.Common.Web;
using Playnite.Plugins;
using Playnite.SDK;
using Playnite.ViewModels;
using Playnite.Windows;
using System;

namespace Playnite
{
    // Addon license flow, split from AddonManifest: the manifest models live
    // in the UI-free Playnite.Core assembly, while this dialog-driven check
    // stays on the UI side.
    public static class AddonManifestExtensions
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public static bool? CheckAddonLicense(this AddonManifest addon)
        {
            try
            {
                if (addon.UserAgreement != null)
                {
                    var acceptState = ExtensionInstaller.GetAddonLicenseAgreed(addon.AddonId);
                    if (acceptState == null || acceptState < addon.UserAgreement.Updated)
                    {
                        var license = HttpDownloader.DownloadString(addon.UserAgreement.AgreementUrl);
                        var licenseAgree = new LicenseAgreementViewModel(
                            new LicenseAgreementWindowFactory(),
                            license,
                            addon.Name);

                        if (licenseAgree.OpenView() == true)
                        {
                            ExtensionInstaller.AgreeAddonLicense(addon.AddonId);
                            return true;
                        }
                        else
                        {
                            ExtensionInstaller.RemoveAddonLicenseAgreement(addon.AddonId);
                            return false;
                        }
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }
            catch (Exception e) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                logger.Error(e, $"Failed to process addon license.");
                return null;
            }
        }
    }
}
