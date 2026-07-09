using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Playnite.SDK;

namespace Playnite
{
    public delegate void CommandExecutedEventHandler(object sender, CommandExecutedEventArgs args);

    public class CommandExecutedEventArgs : EventArgs
    {
        public CmdlineCommand Command
        {
            get; set;
        }

        public string Args
        {
            get; set;
        }

        public CommandExecutedEventArgs()
        {
        }

        public CommandExecutedEventArgs(CmdlineCommand command, string args)
        {
            Command = command;
            Args = args;
        }
    }

    public interface IPipeService
    {
        void InvokeCommand(CmdlineCommand command, string args);
    }

    public class PipeService : IPipeService
    {
        private readonly SynchronizationContext syncContext;
        public event CommandExecutedEventHandler CommandExecuted;

        public PipeService()
        {
            syncContext = SynchronizationContext.Current;
        }

        public void InvokeCommand(CmdlineCommand command, string args)
        {
            // We don't want to block this call because it causes issues if some sync operation that shuts down server is also called.
            // For example, mode switch or instance shutdown calls are stopping server,
            // which results in server close timeout, since server would be still waiting for InvokeCommand to finish.
            Task.Run(async () =>
            {
                await Task.Delay(100);
                syncContext.Post(_ => CommandExecuted?.Invoke(this, new CommandExecutedEventArgs(command, args)), null);
            });
        }
    }

    // Formerly WCF (ServiceHost + NetNamedPipeBinding), which has no server-side
    // support on modern .NET. Same one-way contract over a raw named pipe:
    // one connection per message, single line "<command int>|<base64 args>".
    public class PipeServer
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly string pipeName;
        private CancellationTokenSource cancelSource;
        private Task serverTask;
        private IPipeService service;

        public PipeServer(string endpoint)
        {
            pipeName = GetPipeName(endpoint);
        }

        internal static string GetPipeName(string endpoint)
        {
            // Endpoint is historically a net.pipe:// URI from app config; reduce
            // it to a valid pipe name while keeping it unique per endpoint.
            return "Playnite_" + string.Concat((endpoint ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit));
        }

        public void StartServer(IPipeService service)
        {
            this.service = service;
            cancelSource = new CancellationTokenSource();
            var token = cancelSource.Token;
            serverTask = Task.Run(() => ServerLoop(token));
        }

        private async Task ServerLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (var server = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous))
                    {
                        await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                        using (var reader = new StreamReader(server, Encoding.UTF8))
                        {
                            var line = await reader.ReadLineAsync().ConfigureAwait(false);
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                DispatchMessage(line);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    logger.Error(e, "Pipe server failure while waiting for message.");
                    try
                    {
                        await Task.Delay(200, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private void DispatchMessage(string line)
        {
            try
            {
                var separator = line.IndexOf('|');
                if (separator < 0)
                {
                    logger.Error($"Malformed pipe message: {line}");
                    return;
                }

                var command = (CmdlineCommand)int.Parse(line.Substring(0, separator));
                var encodedArgs = line.Substring(separator + 1);
                var args = encodedArgs.Length == 0
                    ? null
                    : Encoding.UTF8.GetString(Convert.FromBase64String(encodedArgs));
                service.InvokeCommand(command, args);
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to process pipe message: {line}");
            }
        }

        public void StopServer()
        {
            cancelSource?.Cancel();
            try
            {
                serverTask?.Wait(2000);
            }
            catch (Exception)
            {
                // Server task teardown failures are not actionable during shutdown.
            }

            cancelSource?.Dispose();
            cancelSource = null;
            serverTask = null;
            service = null;
        }
    }

    public class PipeClient
    {
        private readonly string pipeName;

        public PipeClient(string endpoint)
        {
            pipeName = PipeServer.GetPipeName(endpoint);
        }

        public void InvokeCommand(CmdlineCommand command, string args)
        {
            using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out))
            {
                client.Connect(3000);
                using (var writer = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true })
                {
                    var payload = args == null
                        ? string.Empty
                        : Convert.ToBase64String(Encoding.UTF8.GetBytes(args));
                    writer.WriteLine($"{(int)command}|{payload}");
                }
            }
        }
    }
}
