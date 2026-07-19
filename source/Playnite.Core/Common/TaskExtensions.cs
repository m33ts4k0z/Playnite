using Playnite.SDK;
using System;
using System.Threading.Tasks;

namespace Playnite.Common
{
    public static class TaskExtensions
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public static async void Observe(this Task task, string operation)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            try
            {
                await task.ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                logger.Error(exception, $"Asynchronous operation failed: {operation}");
            }
        }
    }
}
