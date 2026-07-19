using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Playnite
{
    /// <summary>
    /// Coordinates one application instance per shell/profile and forwards
    /// activation requests over the existing current-user-only pipe transport.
    /// Named mutexes and named pipes are both supported by .NET on Linux.
    /// </summary>
    public sealed class SingleInstanceCoordinator : IPipeService, IDisposable
    {
        private readonly object handlerLock = new object();
        private readonly Queue<CommandExecutedEventArgs> queuedCommands =
            new Queue<CommandExecutedEventArgs>();
        private readonly string endpoint;
        private readonly Mutex ownershipMutex;
        private readonly PipeServer server;
        private Action<CommandExecutedEventArgs> commandHandler;
        private bool ownsMutex;
        private bool disposed;

        public bool IsPrimary { get { return ownsMutex; } }

        public SingleInstanceCoordinator(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("An instance endpoint is required.", nameof(endpoint));
            }

            this.endpoint = endpoint;
            ownershipMutex = new Mutex(false, GetMutexName(endpoint));
            try
            {
                ownsMutex = ownershipMutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                ownsMutex = true;
            }

            if (ownsMutex)
            {
                server = new PipeServer(endpoint);
                server.StartServer(this);
            }
        }

        public static string CreateEndpoint(string shell, string userDataDirectory)
        {
            if (string.IsNullOrWhiteSpace(shell))
            {
                throw new ArgumentException("A shell name is required.", nameof(shell));
            }

            if (string.IsNullOrWhiteSpace(userDataDirectory))
            {
                throw new ArgumentException("A user-data directory is required.", nameof(userDataDirectory));
            }

            return "playnite-avalonia|" + shell.Trim().ToLowerInvariant() + "|" +
                System.IO.Path.GetFullPath(userDataDirectory);
        }

        public void SendToPrimary(CmdlineCommand command, string arguments)
        {
            ThrowIfDisposed();
            if (ownsMutex)
            {
                throw new InvalidOperationException("The primary instance cannot forward a command to itself.");
            }

            new PipeClient(endpoint).InvokeCommand(command, arguments);
        }

        public void SetCommandHandler(Action<CommandExecutedEventArgs> handler)
        {
            ThrowIfDisposed();
            if (!ownsMutex)
            {
                throw new InvalidOperationException("Only the primary instance can receive commands.");
            }

            List<CommandExecutedEventArgs> pending = null;
            lock (handlerLock)
            {
                commandHandler = handler;
                if (handler != null && queuedCommands.Count > 0)
                {
                    pending = new List<CommandExecutedEventArgs>(queuedCommands);
                    queuedCommands.Clear();
                }
            }

            if (pending != null)
            {
                foreach (var command in pending)
                {
                    handler(command);
                }
            }
        }

        void IPipeService.InvokeCommand(CmdlineCommand command, string args)
        {
            Action<CommandExecutedEventArgs> handler;
            var eventArgs = new CommandExecutedEventArgs(command, args);
            lock (handlerLock)
            {
                handler = commandHandler;
                if (handler == null)
                {
                    queuedCommands.Enqueue(eventArgs);
                    return;
                }
            }

            handler(eventArgs);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            server?.StopServer();
            if (ownsMutex)
            {
                ownershipMutex.ReleaseMutex();
                ownsMutex = false;
            }

            ownershipMutex.Dispose();
        }

        private static string GetMutexName(string endpoint)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(endpoint.Trim().ToLowerInvariant()));
            return "Playnite_" + Convert.ToHexString(hash, 0, 16);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(SingleInstanceCoordinator));
            }
        }
    }
}
