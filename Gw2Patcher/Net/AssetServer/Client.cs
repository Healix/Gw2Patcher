using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.IO;
using System.Net;

namespace Gw2Patcher.Net.AssetServer
{
    class Client
    {
        public event EventHandler<FileNotFoundEventArgs> FileNotFound;
        public event EventHandler<string> RequestRepeated;
        public event EventHandler<HttpStream.HttpRequestHeader> RequestHeaderReceived;
        public event EventHandler<Exception> Error;
        public event EventHandler Closed;

        public class FileNotFoundEventArgs : EventArgs
        {
            public FileNotFoundEventArgs(string location)
            {
                this.Location = location;
            }

            public string Location
            {
                get;
                protected set;
            }

            public bool Retry
            {
                get;
                set;
            }
        }

        public const uint HEADER_HTTP_LOWER = 1886680168; //http
        public const uint HEADER_HTTP_UPPER = 1347703880; //HTTP

        private const int BUFFER_LENGTH = 1024 * 1024 * 5;
        private const int RECEIVE_BUFFER_LENGTH = 1024;
        private const int SEND_BUFFER_LENGTH = 1024 * 256;
        private const int TIMEOUT = 60000;

        private Server server;
        private TcpClient client;
        private string root;

        public Client()
        { }

        public Client(Server server, TcpClient client, string root)
        {
            this.root = root;
            this.client = client;
            this.server = server;

            client.ReceiveTimeout = TIMEOUT;
            client.SendTimeout = TIMEOUT;

            server.ActiveStateChanged += server_ActiveStateChanged;
        }

        void server_ActiveStateChanged(object sender, bool e)
        {
            if (!e)
            {
                client.Close();
            }
        }

        public void Start()
        {
            Task.Factory.StartNew(DoClient, TaskCreationOptions.LongRunning);
        }

        private static void WriteHeader(Stream stream, long contentLength)
        {
            var buffer = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: " + contentLength + "\r\nConnection: keep-alive\r\n\r\n");
            stream.Write(buffer, 0, buffer.Length);
        }

        private static void WriteHeader(Stream stream, int code, string description)
        {
            var buffer = Encoding.ASCII.GetBytes("HTTP/1.1 " + code + " " + description + "\r\nConnection: keep-alive\r\nContent-length: 0\r\n\r\n");
            stream.Write(buffer, 0, buffer.Length);
        }

        private void DoClient()
        {
            string lastRequest = null;
            try
            {
                DateTime lastRequestWriteUtc = DateTime.MinValue;
                int repeated = 0;

                var buffer = new byte[BUFFER_LENGTH];
                int read;

                client.SendBufferSize = SEND_BUFFER_LENGTH;
                client.ReceiveBufferSize = RECEIVE_BUFFER_LENGTH;

                HttpStream stream = new HttpStream(client.GetStream());
                HttpStream.HttpHeader header;

                while (client.Connected)
                {
                    if ((read = stream.ReadHeader(buffer, 0, out header)) == 0)
                        return;

                    try
                    {
                        do
                        {
                            read = stream.Read(buffer, 0, BUFFER_LENGTH);
                        }
                        while (read > 0);

                        var request = (HttpStream.HttpRequestHeader)header;
                        var source = Path.Combine(root, Util.FileName.FromAssetRequest(request.Location));

                        if (RequestHeaderReceived != null)
                            RequestHeaderReceived(this, request);

                        bool retry = false;
                        do
                        {
                            var fi = new FileInfo(source);
                            if (fi.Exists)
                            {
                                if (!retry && lastRequest == request.Location)
                                {
                                    if (lastRequestWriteUtc != fi.LastWriteTimeUtc)
                                    {
                                        lastRequestWriteUtc = fi.LastWriteTimeUtc;
                                        repeated = 0;
                                    }
                                    else if (++repeated == 2)
                                    {
                                        //saved data is likely corrupted
                                        if (RequestRepeated != null)
                                            RequestRepeated(this, request.Location);
                                    }
                                }
                                else
                                {
                                    lastRequest = request.Location;
                                    repeated = 0;
                                    lastRequestWriteUtc = fi.LastWriteTimeUtc;
                                    retry = false;
                                }

                                bool headers = true;
                                using (var r = File.Open(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                                {
                                    do
                                    {
                                        read = r.Read(buffer, 0, BUFFER_LENGTH);
                                        if (read > 0)
                                        {
                                            int offset = 0;

                                            if (headers)
                                            {
                                                headers = false;
                                                var h = BitConverter.ToUInt32(buffer, 0);
                                                if (h == HEADER_HTTP_UPPER || h == HEADER_HTTP_LOWER)
                                                {
                                                    //already has headers

                                                    int n = 0;
                                                    for (var i = 0; i < read; i++)
                                                    {
                                                        switch (buffer[i])
                                                        {
                                                            case (byte)'\r':
                                                                break;
                                                            case (byte)'\n':
                                                                if (++n == 2)
                                                                {
                                                                    stream.Write(buffer, 0, offset = i + 1);
                                                                    i = read;
                                                                }
                                                                break;
                                                            default:
                                                                n = 0;
                                                                break;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    //warning: only full files can have empty headers - checksum required for patches
                                                    WriteHeader(stream, r.Length);
                                                    stream.Write(buffer, 0, read);
                                                }
                                            }

                                            while (offset < read)
                                            {
                                                int c = read - offset;
                                                if (c > SEND_BUFFER_LENGTH)
                                                    c = SEND_BUFFER_LENGTH;
                                                stream.Write(buffer, offset, c);
                                                offset += c;
                                            }
                                        }
                                    }
                                    while (read > 0);
                                }
                            }
                            else
                            {
                                if (retry)
                                {
                                    retry = false;
                                }
                                else
                                {
                                    if (FileNotFound != null)
                                    {
                                        FileNotFoundEventArgs e = new FileNotFoundEventArgs(request.Location);
                                        FileNotFound(this, e);
                                        if (e.Retry)
                                        {
                                            retry = true;
                                        }
                                    }
                                }

                                //sending a bad response will cause GW2 to try different versions: 
                                //patch (baseId/fileId) > full file compressed (0/fileId/compressed) > full file (0/fileId)
                                if (!retry)
                                    WriteHeader(stream, 404, "Not Found");
                            }
                        }
                        while (retry);

                        var keepAlive = request.Headers[HttpRequestHeader.Connection];
                        if (keepAlive == null || !keepAlive.Equals("keep-alive", StringComparison.OrdinalIgnoreCase))
                            return;
                    }
                    finally
                    {
                    }
                }
            }
            catch (Exception e)
            {
                if (Error != null)
                    Error(this, e);
            }
            finally
            {
                server.ActiveStateChanged -= server_ActiveStateChanged;

                if (client != null)
                    client.Close();

                if (Closed != null)
                    Closed(this, EventArgs.Empty);
            }
        }
    }
}
