using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.IO;

namespace Gw2Patcher.Net
{
    class AssetDownloader : IDisposable
    {
        public delegate void RequestCompleteEventHandler(object sender, int index, string location, long contentBytes);
        public delegate void ErrorEventHandler(object sender, int index, string location, Exception exception);

        public event EventHandler Complete;
        public event ErrorEventHandler Error;
        public event EventHandler<uint> BytesDownloaded;
        public event EventHandler<uint> DownloadRate;
        public event RequestCompleteEventHandler RequestComplete;

        private class SharedWork
        {
            public string host;
            public Queue<string> locations;
            public int length;
            public int index;
            public string path;
            public ManualResetEvent waiter;
            public bool keepalive;
            public bool headersOnly;
            public bool abort;
        }

        private class Worker : IDisposable
        {
            public event ErrorEventHandler Error;
            public event EventHandler Complete;
            public event EventHandler<int> BytesDownloaded;
            public event RequestCompleteEventHandler RequestComplete;
            public event EventHandler RequestBegin;

            public const int BUFFER_LENGTH = 65536;
            private const int TIMEOUT = 5000;

            private TcpClient client, clientSwap;
            private IPPool ipPool;
            private HttpStream stream;
            private IPEndPoint remoteEP;
            private SharedWork work;
            private Thread thread;

            public Worker(IPPool ipPool, SharedWork work)
            {
                this.ipPool = ipPool;
                this.work = work;
            }

            private void WriteHeader(Stream stream, string host, string request)
            {
                byte[] buffer = Encoding.ASCII.GetBytes((work.headersOnly ? "HEAD " : "GET ") + request + " HTTP/1.1\r\nCache-Control: no-cache\r\nPragma: no-cache\r\nHost: " + host + "\r\nConnection: keep-alive\r\n\r\n");
                stream.Write(buffer, 0, buffer.Length);
            }

            public IPEndPoint RemoteEP
            {
                get
                {
                    return remoteEP;
                }
            }

            public IPPool IPPool
            {
                get
                {
                    return ipPool;
                }
            }

            public void Close()
            {
                if (clientSwap != null)
                {
                    clientSwap.Close();
                    clientSwap = null;
                }
                if (stream != null)
                {
                    stream.Dispose();
                    stream = null;
                }
                if (client != null)
                {
                    client.Close();
                    client = null;
                }
            }

            private void DoWork()
            {
                var work = this.work;

                byte[] buffer = new byte[BUFFER_LENGTH];
                bool doSwap = false;
                Task<IPAddress> taskSwap = null;
                int counter = 0;
                var timeout = DateTime.UtcNow.AddMilliseconds(TIMEOUT);

                try
                {
                    while (!work.abort)
                    {
                        string request;
                        int index;
                        bool hasWork;

                        lock (work)
                        {
                            if (work.index < work.length)
                            {
                                do
                                {
                                    request = work.locations.Dequeue();
                                    index = work.index++;
                                    if (hasWork = request != null)
                                        break;
                                }
                                while (work.index < work.length);

                                if (!hasWork)
                                {
                                    if (!work.keepalive)
                                        return;
                                }
                            }
                            else if (!work.keepalive)
                            {
                                return;
                            }
                            else
                            {
                                work.waiter.Reset();

                                hasWork = false;
                                request = null;
                                index = -1;
                            }
                        }

                        if (!hasWork)
                        {
                            work.waiter.WaitOne();
                            continue;
                        }

                        if (DateTime.UtcNow > timeout)
                            Close();

                        long mismatchLength = -1;
                        byte retry = 10;
                        do
                        {
                            #region Remote connection

                            if (doSwap)
                            {
                                if (taskSwap.IsCompleted)
                                {
                                    if (taskSwap.Result != null)
                                    {
                                        ipPool.AddSample(taskSwap.Result, double.MaxValue);
                                    }
                                    else if (clientSwap != null && clientSwap.Connected)
                                    {
                                        if (client != null)
                                            client.Close();
                                        client = clientSwap;
                                        clientSwap = null;
                                        remoteEP = (IPEndPoint)client.Client.RemoteEndPoint;
                                        stream.BaseStream = client.GetStream();
                                    }

                                    doSwap = false;
                                    counter = 0;
                                }
                            }

                            if (client == null || !client.Connected)
                            {
                                client = new TcpClient()
                                {
                                    ReceiveTimeout = TIMEOUT,
                                    SendTimeout = TIMEOUT
                                };

                                try
                                {
                                    remoteEP = new IPEndPoint(ipPool.GetIP(), 80);
                                    for (byte attempt = 10; attempt > 0; attempt--)
                                    {
                                        if (!client.ConnectAsync(remoteEP.Address, remoteEP.Port).Wait(TIMEOUT))
                                        {
                                            client.Close();

                                            if (work.abort || attempt == 1)
                                            {
                                                if (Error != null)
                                                    Error(this, index, request, new TimeoutException("Unable to connect to host"));
                                                return;
                                            }

                                            ipPool.AddSample(remoteEP.Address, double.MaxValue);
                                            Thread.Sleep(100);
                                            continue;
                                        }

                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    ipPool.AddSample(remoteEP.Address, double.MaxValue);

                                    if (Error != null)
                                    {
                                        if (ex.InnerException != null)
                                            Error(this, index, request, ex.InnerException);
                                        else
                                            Error(this, index, request, ex);
                                    }

                                    return;
                                }

                                stream = new HttpStream(client.GetStream());
                            }

                            #endregion

                            #region Swap

                            if (++counter == 10)
                            {
                                var ip = ipPool.GetIP();

                                if (ip == remoteEP.Address)
                                {
                                    counter = 0;
                                }
                                else
                                {
                                    doSwap = true;

                                    taskSwap = Task.Run<IPAddress>(
                                        delegate
                                        {
                                            var clientSwap = this.clientSwap = new TcpClient();
                                            clientSwap.ReceiveTimeout = clientSwap.SendTimeout = TIMEOUT;
                                            try
                                            {
                                                if (!clientSwap.ConnectAsync(ip, 80).Wait(TIMEOUT))
                                                {
                                                    throw new TimeoutException();
                                                }

                                                return null;
                                            }
                                            catch (Exception e)
                                            {
                                                clientSwap.Close();

                                                return ip;
                                            }
                                        });
                                }
                            }

                            #endregion

                            if (RequestBegin != null)
                                RequestBegin(this, EventArgs.Empty);

                            try
                            {
                                WriteHeader(stream, work.host, request);

                                bool delete = true;
                                string path = Path.Combine(work.path, Util.FileName.FromAssetRequest(request) + ".tmp");
                                try
                                {
                                    using (var w = File.Create(path))
                                    {
                                        Net.HttpStream.HttpHeader header;
                                        int read;

                                        var startTime = DateTime.UtcNow;
                                        long total = read = stream.ReadHeader(buffer, 0, out header);

                                        if (read <= 0)
                                            throw new EndOfStreamException();

                                        var response = (Net.HttpStream.HttpResponseHeader)header;

                                        if (work.headersOnly)
                                        {
                                            if (BytesDownloaded != null)
                                                BytesDownloaded(this, read);

                                            w.Write(buffer, 0, read);
                                        }
                                        else
                                        {
                                            while (read > 0)
                                            {
                                                if (BytesDownloaded != null)
                                                    BytesDownloaded(this, read);

                                                w.Write(buffer, 0, read);
                                                read = stream.Read(buffer, 0, BUFFER_LENGTH);

                                                total += read;
                                            }
                                        }

                                        var elapsed = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;

                                        if (response.StatusCode != HttpStatusCode.OK)
                                        {
                                            if (elapsed <= 0)
                                                elapsed = 1;
                                            ipPool.AddSample(remoteEP.Address, total / elapsed * 2);
                                            throw new Exception("Server returned a bad response");
                                        }
                                        else if (elapsed > 0)
                                            ipPool.AddSample(remoteEP.Address, total / elapsed);

                                        if (!work.headersOnly && response.ContentLength > 0 && stream.ContentLengthProcessed != response.ContentLength)
                                        {
                                            ipPool.AddSample(remoteEP.Address, double.MaxValue);
                                            if (mismatchLength == stream.ContentLengthProcessed)
                                                retry = 1;
                                            mismatchLength = stream.ContentLengthProcessed;
                                            throw new Exception("Content length doesn't match header");
                                        }

                                        timeout = DateTime.UtcNow.AddMilliseconds(TIMEOUT);

                                        if (!response.KeepAlive.keepAlive)
                                            client.Close();
                                    }

                                    retry = 1;
                                    string to = path.Substring(0, path.Length - 4);
                                    if (File.Exists(to))
                                        File.Delete(to);
                                    File.Move(path, to);
                                    delete = false;

                                    if (RequestComplete != null)
                                        RequestComplete(this, index, request, stream.ContentLengthProcessed);

                                    break;
                                }
                                finally
                                {
                                    if (delete)
                                    {
                                        try
                                        {
                                            File.Delete(path);
                                        }
                                        catch { }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                client.Close();

                                if (work.abort || --retry == 0)
                                {
                                    if (Error != null)
                                        Error(this, index, request, ex);
                                    return;
                                }

                                Thread.Sleep(1000);
                            }
                        }
                        while (true);
                    }
                }
                finally
                {
                    Close();

                    if (Complete != null)
                        Complete(this, EventArgs.Empty);
                }
            }

            public Thread Start()
            {
                thread = new Thread(new ThreadStart(DoWork));
                thread.IsBackground = true;
                thread.Start();
                return thread;
            }

            public void Abort()
            {
                if (thread != null)
                {
                    thread.Abort();
                }
            }

            public void Dispose()
            {
                try
                {
                    Close();

                    if (thread != null && thread.IsAlive)
                        thread.Abort();
                }
                catch { }
            }
        }

        private bool isActive;
        private Worker[] workers;
        private SharedWork work;
        private byte threads;
        private bool keepalive;

        private long totalBytesDownloaded;

        public AssetDownloader(byte threads, IPPool ipPool, string host, string[] requests, string path)
            : this(threads, ipPool, host, requests, requests.Length, path, false, false)
        {
        }

        public AssetDownloader(byte threads, IPPool ipPool, string host, IEnumerable<string> requests, int length, string path, bool keepalive, bool headersOnly)
        {
            this.threads = threads;
            this.keepalive = keepalive;

            Queue<string> q;
            if (requests != null)
                q = new Queue<string>(requests);
            else
                q = new Queue<string>();

            var work = this.work = new SharedWork()
            {
                host = host,
                locations = q,
                length = length,
                index = 0,
                path = path,
                keepalive = keepalive,
                headersOnly = headersOnly,
                abort = false,
                waiter = keepalive ? new ManualResetEvent(true) : null
            };

            workers = new Worker[threads];
            for (var i = 0; i < threads; i++)
            {
                var worker = workers[i] = new Worker(ipPool, work);
                worker.BytesDownloaded += worker_BytesDownloaded;
                worker.Complete += worker_Complete;
                worker.Error += worker_Error;
                worker.RequestComplete += worker_RequestComplete;
            }
        }

        public AssetDownloader(byte threads, IPPool ipPool, string host, string path)
            : this(threads, ipPool, host, null, 0, path, true, false)
        {
        }

        void worker_RequestComplete(object sender, int index, string location, long contentBytes)
        {
            if (RequestComplete != null)
                RequestComplete(this, index, location, contentBytes);
        }

        void worker_Error(object sender, int index, string request, Exception e)
        {
            if (Error != null)
                Error(this, index, request, e);
        }

        void worker_Complete(object sender, EventArgs e)
        {
            lock (this)
            {
                threads--;
            }
        }

        void worker_BytesDownloaded(object sender, int e)
        {
            lock (this)
            {
                totalBytesDownloaded += e;
            }
        }

        public long TotalBytesDownloaded
        {
            get
            {
                return totalBytesDownloaded;
            }
        }

        public bool IsActive
        {
            get
            {
                return isActive;
            }
        }

        private async void DoMonitor()
        {
            long totalBytes = 0;
            long lastSample0 = 0;
            long lastSample1 = 0;
            bool reset = false;
            DateTime nextSample0 = DateTime.UtcNow;
            DateTime nextSample1 = DateTime.UtcNow;
            DateTime lastRequest = DateTime.MinValue;

            EventHandler requestBegin =
                delegate
                {
                    if (reset)
                    {
                        nextSample0 = nextSample1 = DateTime.UtcNow;
                        nextSample1 = nextSample0;
                        reset = false;
                    }
                };

            foreach (var worker in workers)
                worker.RequestBegin += requestBegin;

            do
            {
                await Task.Delay(1000);

                var l = totalBytesDownloaded;

                if (totalBytes != l)
                {
                    if (BytesDownloaded != null)
                        BytesDownloaded(this, (uint)(l - totalBytes));
                    totalBytes = l;
                }

                var now = DateTime.UtcNow;
                var elapsed = now.Subtract(nextSample0).TotalSeconds;

                if (elapsed > 0)
                {
                    if (l != lastSample0)
                    {
                        double rate = (l - lastSample0) / elapsed;
                        if (DownloadRate != null)
                            DownloadRate(this, (uint)(rate + 0.5));

                        nextSample1 = nextSample0;
                        lastSample1 = lastSample0;
                        nextSample0 = now;
                        lastSample0 = l;
                    }
                    else if (elapsed > 3)
                    {
                        if (!reset)
                        {
                            reset = true;

                            if (DownloadRate != null)
                                DownloadRate(this, 0);
                        }
                    }
                    else
                    {
                        elapsed = now.Subtract(nextSample1).TotalSeconds;
                        if (elapsed > 0)
                        {
                            double rate = (l - lastSample1) / elapsed;
                            if (DownloadRate != null)
                                DownloadRate(this, (uint)(rate + 0.5));
                        }
                    }
                }
            }
            while (threads > 0 || work.keepalive && !work.abort);

            foreach (var worker in workers)
                worker.RequestBegin -= requestBegin;

            isActive = false;

            if (Complete != null)
                Complete(this, EventArgs.Empty);
        }

        public void Start()
        {
            if (isActive)
                return;
            isActive = true;

            threads = (byte)workers.Length;
            work.abort = false;
            work.keepalive = keepalive;
            if (keepalive)
                work.waiter.Set();

            foreach (var worker in workers)
                worker.Start();

            DoMonitor();
        }

        public void Stop()
        {
            if (work.keepalive)
            {
                work.keepalive = false;
                work.waiter.Set();
            }
        }

        public void Abort(bool force)
        {
            work.abort = true;
            if (work.keepalive)
                work.waiter.Set();

            if (force)
            {
                foreach (var worker in workers)
                    worker.Abort();
            }
        }

        public void Add(string request)
        {
            if (!keepalive)
                throw new NotSupportedException("Cannot add more requests when not constructed to");

            lock (work)
            {
                work.locations.Enqueue(request);
                work.length++;
                work.waiter.Set();
            }
        }

        public void Add(IEnumerable<string> requests)
        {
            if (!keepalive)
                throw new NotSupportedException("Cannot add more requests when not constructed to");

            lock (work)
            {
                foreach (var request in requests)
                {
                    work.locations.Enqueue(request);
                    work.length++;
                }
                work.waiter.Set();
            }
        }

        public void Clear()
        {
            if (!keepalive)
                throw new NotSupportedException("Cannot clear when not constructed to");

            lock (work)
            {
                work.locations.Clear();
                work.index = 0;
                work.length = 0;
            }
        }

        public void Dispose()
        {
            work.abort = true;
            foreach (var worker in workers)
                worker.Dispose();
        }
    }
}
