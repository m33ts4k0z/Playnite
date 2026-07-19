using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace Playnite.Tests.Common
{
    [TestFixture]
    public class PipeServerTests
    {
        private sealed class RecordingPipeService : IPipeService
        {
            public TaskCompletionSource<CommandExecutedEventArgs> Invocation { get; } =
                new TaskCompletionSource<CommandExecutedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

            public void InvokeCommand(CmdlineCommand command, string args)
            {
                Invocation.TrySetResult(new CommandExecutedEventArgs(command, args));
            }
        }

        [Test]
        public async Task RoundTripsCommandAndUnicodeArgumentsForCurrentUser()
        {
            var endpoint = "net.pipe://localhost/PlayniteTests/" + Guid.NewGuid().ToString("N");
            var service = new RecordingPipeService();
            var server = new PipeServer(endpoint);
            server.StartServer(service);

            try
            {
                new PipeClient(endpoint).InvokeCommand(CmdlineCommand.UriRequest, "playnite://test/åäö/🎮");
                var completed = await Task.WhenAny(service.Invocation.Task, Task.Delay(TimeSpan.FromSeconds(5)));

                Assert.AreSame(service.Invocation.Task, completed, "The pipe server did not receive the command.");
                var invocation = await service.Invocation.Task;
                Assert.AreEqual(CmdlineCommand.UriRequest, invocation.Command);
                Assert.AreEqual("playnite://test/åäö/🎮", invocation.Args);
            }
            finally
            {
                server.StopServer();
            }
        }

        [Test]
        public void PipeNamesAreStableAndDoNotCollapseDistinctEndpoints()
        {
            var first = PipeServer.GetPipeName("net.pipe://localhost/Playnite/a-b");
            var sameNormalized = PipeServer.GetPipeName(" NET.PIPE://LOCALHOST/PLAYNITE/A-B ");
            var distinct = PipeServer.GetPipeName("net.pipe://localhost/Playnite/ab");

            Assert.AreEqual(first, sameNormalized);
            Assert.AreNotEqual(first, distinct);
            Assert.AreEqual("Playnite_".Length + 64, first.Length);
        }
    }
}
