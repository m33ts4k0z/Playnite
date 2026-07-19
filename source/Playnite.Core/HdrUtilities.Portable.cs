#if !WINDOWS
namespace Playnite
{
    public class HdrUtilities
    {
        public static bool IsHdrSupported()
        {
            return false;
        }

        public static bool IsHdrEnabled()
        {
            return false;
        }

        public static void SetHdrEnabled(bool enable)
        {
        }
    }
}
#endif
