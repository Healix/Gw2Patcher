using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace Gw2Patcher.Net.AssetServer
{
    class Server
    {
        public event EventHandler<bool> ActiveStateChanged;
        public event EventHandler<Client> ClientConnected;
        public event EventHandler<Exception> ClientError;
        public event EventHandler<Client> ClientClosed;
        public event EventHandler<HttpStream.HttpRequestHeader> ClientRequestHeaderReceived;
        public event EventHandler<Client.FileNotFoundEventArgs> FileNotFound;
        public event EventHandler<string> ClientRequestRepeated;

        private TcpListener listener;
        private bool isActive;
        private string root;
        private int lastPort;
        private bool allowRemote;

        public Server(string root)
        {
            this.root = root;
        }

        public int Port
        {
            get
            {
                if (!isActive)
                    return 0;

                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
        }

        public bool AllowRemoteConnections
        {
            get
            {
                return allowRemote;
            }
            set
            {
                if (allowRemote != value)
                {
                    allowRemote = value;

                    if (isActive)
                    {
                        try
                        {
                            listener.Stop();

                            listener = new TcpListener(allowRemote ? IPAddress.Any : IPAddress.Loopback, lastPort);
                            listener.Start(20);

                            Task.Factory.StartNew(DoListener, TaskCreationOptions.LongRunning);
                        }
                        catch (Exception ex)
                        {
                            Stop();
                            throw;
                        }
                    }
                }
            }
        }

        public void Start(int port)
        {
            if (isActive)
                return;
            else
                isActive = true;

            bool retry;
            if (retry = port == 0 && lastPort != 0)
                port = lastPort;

            do
            {
                try
                {
                    listener = new TcpListener(allowRemote ? IPAddress.Any : IPAddress.Loopback, port);
                    listener.Start(20);

                    lastPort = Port;

                    break;
                }
                catch (Exception ex)
                {
                    if (retry)
                    {
                        port = 0;
                        retry = false;
                    }
                    else
                    {
                        isActive = false;
                        throw;
                    }
                }
            }
            while (true);

            Task.Factory.StartNew(DoListener, TaskCreationOptions.LongRunning);

            if (ActiveStateChanged != null)
                ActiveStateChanged(this, true);
        }

        public void Stop()
        {
            if (isActive)
            {
                isActive = false;
                listener.Stop();

                if (ActiveStateChanged != null)
                    ActiveStateChanged(this, false);
            }
        }

        private async void DoListener()
        {
            var listener = this.listener;

            while (isActive)
            {
                TcpClient accept = null;

                try
                {
                    accept = await listener.AcceptTcpClientAsync();

                    var client = new Client(this, accept, this.root);

                    if (ClientConnected != null)
                        ClientConnected(this, client);

                    client.Closed += client_Closed;
                    client.Error += client_Error;
                    client.FileNotFound += client_FileNotFound;
                    client.RequestHeaderReceived += client_RequestHeaderReceived;
                    client.RequestRepeated += client_RequestRepeated;

                    //lock (this)
                    //{
                    //    clients++;
                    //}

                    client.Start();
                }
                catch (Exception e)
                {
                    if (accept != null)
                        accept.Close();

                    var server = listener.Server;
                    if (server == null || !server.IsBound)
                    {
                        if (server != null)
                            server.Dispose();
                        return;
                    }
                }
            }
        }

        void client_RequestRepeated(object sender, string e)
        {
            if (ClientRequestRepeated != null)
                ClientRequestRepeated(sender, e);
        }

        void client_RequestHeaderReceived(object sender, HttpStream.HttpRequestHeader e)
        {
            if (ClientRequestHeaderReceived != null)
                ClientRequestHeaderReceived(sender, e);
        }

        void client_FileNotFound(object sender, Client.FileNotFoundEventArgs e)
        {
            if (FileNotFound != null)
                FileNotFound(sender, e);
        }

        void client_Error(object sender, Exception e)
        {
            if (ClientError != null)
                ClientError(sender, e);
        }

        void client_Closed(object sender, EventArgs e)
        {
            if (ClientClosed != null)
                ClientClosed(this, (Client)sender);
        }
    }
}
