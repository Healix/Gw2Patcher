using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Threading;
using System.Diagnostics;
using System.Reflection;

namespace Gw2Patcher.UI
{
    public partial class formMain : Form
    {
        private const uint HEADER_ASSETS = 1953710416; //PAst

        private const string ASSET_URL = "http://assetcdn.101.arenanetworks.com";
        private const string URL_FILE_BASE = "/program/101/1/";
        private const string URL_PATH = URL_FILE_BASE + "{0}/{1}";
        private const string URL_PATH_COMPRESSED = URL_PATH + "/compressed";
        private const float AVG_COMPRESSION = 0.627114534f; //average compression ratio of files in Gw2.dat
        private const float AVG_PATCH_COMPRESSION = 0.0724870563f; //0.289863974f; //average difference between patches and full uncompressed files
        private const ushort AVG_HEADER_SIZE = 600;
        public static readonly string PATH_ROOT = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static readonly string PATH_SESSION = Path.Combine(PATH_ROOT, "session");
        public static readonly string PATH_CACHE = Path.Combine(PATH_SESSION, "cache");

        private Gw2Launcher.UI.Controls.SidebarButton selectedButton;
        private Net.AssetServer.Server server;
        private Net.AssetDownloader downloader;
        private long totalBytesDownloaded;
        private HashSet<string> missingFiles;
        private formWarnings requestsWindow;
        private HashSet<string> failedFiles;
        private IPAddress[] assetIPs;

        private struct TaskManifestWork
        {
            public Dat.Manifest manifest;
            public Dat.Manifest.ManifestRecord record;
            public WebClient client;
        }

        private struct LoadAssetsResult
        {
            public AssetEntry[] entries;
            public DateTime date;
            public int buildId;
            public Exception exception;
        }

        private struct AssetEntry
        {
            public int baseId, fileId;
            public uint size;
        }

        public formMain()
        {
            InitializeComponent();

            if (Environment.Is64BitOperatingSystem)
                checkManifests64.Checked = true;
            else
                checkManifests32.Checked = true;

            radioUpdatesAuto.Checked = true;

            buttonProcess.Tag = panelDat;
            buttonManifests.Tag = panelManifests;
            buttonUpdates.Tag = panelUpdates;
            buttonPatch.Tag = panelPatch;
            buttonCleanup.Tag = panelCleanup;

            foreach (Control c in sidebarPanel1.Controls)
            {
                var b = c as Gw2Launcher.UI.Controls.SidebarButton;
                if (b != null)
                {
                    b.SelectedChanged += sidebarButton_SelectedChanged;
                    b.Click += sidebarButton_Click;
                }
            }

            switch (Settings.InitialPanel)
            {
                case Settings.Panels.Cleanup:
                    buttonCleanup.Selected = true;
                    break;
                case Settings.Panels.Manifests:
                    buttonManifests.Selected = true;
                    break;
                case Settings.Panels.Server:
                    buttonPatch.Selected = true;
                    break;
                case Settings.Panels.UpdatesAuto:
                    radioUpdatesAuto.Checked = true;
                    buttonUpdates.Selected = true;
                    break;
                case Settings.Panels.UpdatesManual:
                    radioUpdatesManual.Checked = true;
                    buttonUpdates.Selected = true;
                    break;
                default:
                    buttonProcess.Selected = true;
                    break;
            }

            checkManifests32.Checked = Settings.Manifests32;
            checkManifests64.Checked = Settings.Manifests64;
            checkManifestsChinese.Checked = Settings.ManifestsChinese;
            checkManifestsEnglish.Checked = Settings.ManifestsEnglish;
            checkManifestsFrench.Checked = Settings.ManifestsFrench;
            checkManifestsGerman.Checked = Settings.ManifestsGerman;
            checkManifestsOSX.Checked = Settings.ManifestsOSX;
            checkPatchEnableDownload.Checked = Settings.ServerAllowDownloads;
            checkPatchLocal.Checked = !Settings.ServerAllowRemoteConnections;
            checkUpdatesManualNoHeaders.Checked = Settings.UpdatesExportSubstituteHeaders;
            checkUpdatesManualIncludeExisting.Checked = Settings.UpdatesExportIncludeExisting;
            checkPatchEnableDownloadUncompressed.Checked = Settings.ServerAllowUncompressedDownloads;
        }

        void sidebarButton_SelectedChanged(object sender, EventArgs e)
        {
            var button = (Gw2Launcher.UI.Controls.SidebarButton)sender;
            if (button.Selected)
            {
                if (selectedButton != null)
                {
                    ((Panel)selectedButton.Tag).Visible = false;
                    selectedButton.Selected = false;
                }
                selectedButton = button;
                ((Panel)button.Tag).Visible = true;

                if (button == buttonProcess)
                    Settings.InitialPanel = Settings.Panels.ProcessDat;
                else if (button == buttonManifests)
                    Settings.InitialPanel = Settings.Panels.Manifests;
                else if (button == buttonUpdates)
                    Settings.InitialPanel = radioUpdatesManual.Checked ? Settings.Panels.UpdatesManual : Settings.Panels.UpdatesAuto;
                else if (button == buttonPatch)
                    Settings.InitialPanel = Settings.Panels.Server;
                else if (button == buttonCleanup)
                    Settings.InitialPanel = Settings.Panels.Cleanup;
            }
        }

        private void sidebarButton_Click(object sender, EventArgs e)
        {
            var button = (Gw2Launcher.UI.Controls.SidebarButton)sender;
            button.Selected = true;
        }

        private async void buttonDatProcess_Click(object sender, EventArgs e)
        {
            buttonDatProcess.Enabled = false;
            labelDatReady.Visible = false;

            try
            {
                OpenFileDialog f = new OpenFileDialog();

                f.Filter = "Guild Wars 2|Gw2.dat";
                f.Title = "Open Gw2.dat";

                if (!string.IsNullOrEmpty(Settings.GW2DatPath))
                {
                    f.InitialDirectory = Path.GetDirectoryName(Settings.GW2DatPath);
                    f.FileName = Path.GetFileName(Settings.GW2DatPath);
                }

                if (f.ShowDialog(this) != System.Windows.Forms.DialogResult.OK)
                    return;

                Settings.GW2DatPath = f.FileName;

                labelDatFiles.Text = "---";
                labelDatSize.Text = "---";

                string dat = f.FileName;

                try
                {
                    labelDatSize.Text = FormatBytes(new FileInfo(dat).Length);
                }
                catch { }

                var entries = await Task.Run<Dat.DatFile.MftEntry[]>(
                    delegate
                    {
                        try
                        {
                            return Dat.DatFile.Read(dat);
                        }
                        catch
                        {
                            return null;
                        }
                    });

                if (entries == null)
                {
                    labelDatFiles.Text = "Unable to read Gw2.dat";
                }
                else
                {
                    labelDatFiles.Text = string.Format("{0:#,##0}", entries.Length);

                    var exception = await Task.Run(
                        delegate
                        {
                            try
                            {
                                new DirectoryInfo(PATH_SESSION).Create();

                                var path = Path.Combine(PATH_SESSION, "Gw2.dat.manifest");
                                using (var w = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None)))
                                {
                                    foreach (var entry in entries)
                                    {
                                        if (entry != null)
                                        {
                                            if (entry.baseId <= 0)
                                                continue;

                                            bool b = entry.baseId != entry.fileId;
                                            w.Write(b);
                                            w.Write(entry.baseId);
                                            if (b)
                                                w.Write(entry.fileId);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                return ex;
                            }

                            return null;
                        });

                    if (exception != null)
                    {
                        MessageBox.Show(this, "Failed to write entries:\n\n" + exception.Message, "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        labelDatReady.Visible = true;
                    }
                }
            }
            finally
            {
                buttonDatProcess.Enabled = true;
            }
        }

        private void formMain_Load(object sender, EventArgs e)
        {

        }

        private byte[] DownloadData(WebClient client, string request, bool cache)
        {
            int retries = 10;

            string path = Path.Combine(PATH_CACHE, Util.FileName.FromAssetRequest(request));
            if (cache)
            {
                if (File.Exists(path))
                    return File.ReadAllBytes(path);
            }

            while (true)
            {
                try
                {
                    var by = client.DownloadData(ASSET_URL + request);

                    if (cache)
                    {
                        try
                        {
                            new DirectoryInfo(PATH_CACHE).Create();
                            File.WriteAllBytes(path, by);
                        }
                        catch { }
                    }

                    return by;
                }
                catch (Exception e)
                {
                    if (--retries == 0)
                        throw;

                    Thread.Sleep(1000);
                }
            }
        }

        private string DownloadString(WebClient client, string request, bool cache)
        {
            int retries = 10;

            string path = Path.Combine(PATH_CACHE, Util.FileName.FromAssetRequest(request));
            if (cache)
            {
                if (File.Exists(path))
                    return File.ReadAllText(path);
            }
            while (true)
            {
                try
                {
                    var str = client.DownloadString(ASSET_URL + request);

                    if (cache)
                    {
                        try
                        {
                            new DirectoryInfo(PATH_CACHE).Create();
                            File.WriteAllText(path, str);
                        }
                        catch { }
                    }

                    return str;
                }
                catch (Exception e)
                {
                    if (--retries == 0)
                        throw;

                    Thread.Sleep(1000);
                }
            }
        }

        public string FormatBytes(long bytes)
        {
            if (bytes > 858993459) //0.8 GB
            {
                return string.Format("{0:0.##} GB", bytes / 1073741824d);
            }
            else if (bytes > 838860) //0.8 MB
            {
                return string.Format("{0:0.##} MB", bytes / 1048576d);
            }
            else if (bytes > 819) //0.8 KB
            {
                return string.Format("{0:0.##} KB", bytes / 1024d);
            }
            else
            {
                return bytes + " bytes";
            }
        }

        private async void buttonDownloadManifests_Click(object sender, EventArgs e)
        {
            if (!buttonUpdatesDownload.Enabled || !buttonUpdatesManualImport.Enabled)
            {
                MessageBox.Show(this, "The current manifests are already in use and cannot be modified", "Manifests in use", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            buttonDownloadManifests.Enabled = false;
            progressManifests.Value = 0;
            progressManifests.Visible = true;
            checkManifests64.Enabled = false;
            labelManifestsReady.Visible = false;
            labelManifestsReadyNoUpdates.Visible = false;
            checkManifests32.Enabled = false;
            checkManifests64.Enabled = false;
            checkManifestsChinese.Enabled = false;
            checkManifestsEnglish.Enabled = false;
            checkManifestsFrench.Enabled = false;
            checkManifestsGerman.Enabled = false;
            checkManifestsOSX.Enabled = false;

            try
            {
                if (!(checkManifests64.Checked || checkManifests32.Checked || checkManifestsOSX.Checked))
                {
                    MessageBox.Show(this, "One of the options must be selected to continue\n\n32-bit, 64-bit or OSX", "OS not selected", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                byte languages = 0;
                foreach (var checkbox in new CheckBox[] { checkManifestsEnglish, checkManifestsFrench, checkManifestsGerman, checkManifestsChinese })
                {
                    if (checkbox.Checked)
                        languages++;
                }

                if (languages == 0)
                {
                    if (MessageBox.Show(this, "A language has not been selected\n\nAre you sure?", "Language not selected", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != System.Windows.Forms.DialogResult.Yes)
                        return;
                }

                Settings.Manifests32 = checkManifests32.Checked;
                Settings.Manifests64 = checkManifests64.Checked;
                Settings.ManifestsOSX = checkManifestsOSX.Checked;
                Settings.ManifestsEnglish = checkManifestsEnglish.Checked;
                Settings.ManifestsFrench = checkManifestsFrench.Checked;
                Settings.ManifestsGerman = checkManifestsGerman.Checked;
                Settings.ManifestsChinese = checkManifestsChinese.Checked;

                Dictionary<int, int> baseIds = new Dictionary<int, int>();
                var path = Path.Combine(PATH_SESSION, "Gw2.dat.manifest");

                if (File.Exists(path))
                {
                    await Task.Run(
                        delegate
                        {
                            using (var r = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None)))
                            {
                                while (r.BaseStream.Position < r.BaseStream.Length)
                                {
                                    bool b = r.ReadBoolean();
                                    int baseId = r.ReadInt32();
                                    int fileId;
                                    if (b)
                                        fileId = r.ReadInt32();
                                    else
                                        fileId = baseId;
                                    baseIds[baseId] = fileId;
                                }
                            }
                        });
                }

                if (baseIds.Count == 0)
                {
                    if (MessageBox.Show(this, "No Gw2.dat entries were found.\n\nAre you sure you want to download everything?", "Download everything?", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != System.Windows.Forms.DialogResult.Yes)
                        return;
                }


                WebClient[] clients = new WebClient[10];

                for (var i = 0; i < 10; i++)
                    clients[i] = new WebClient();

                int[] exeIds = new int[3];
                int buildId = 0;
                int manifestId = 0;
                int manifestSize = 0;
                int exeSize = 0;

                var exception = await Task.Run<Exception>(
                    delegate
                    {
                        try
                        {
                            int i = 0;

                            foreach (var checkbox in new CheckBox[] { checkManifests64, checkManifests32, checkManifestsOSX })
                            {
                                if (checkbox.Checked)
                                {
                                    string os;
                                    switch (i)
                                    {
                                        case 0:
                                            os = "64";
                                            break;
                                        case 1:
                                            os = "OSX";
                                            break;
                                        default:
                                            os = "";
                                            break;
                                    }

                                    var _latest = DownloadString(clients[0], "/latest" + os + "/101", false);
                                    var latest = _latest.Split(' ');
                                    
                                    buildId = Int32.Parse(latest[0]);
                                    exeIds[i] = Int32.Parse(latest[1]);
                                    exeSize = Int32.Parse(latest[2]);
                                    manifestId = Int32.Parse(latest[3]);
                                    manifestSize = Int32.Parse(latest[4]);

                                    new DirectoryInfo(PATH_CACHE).Create();
                                    File.WriteAllText(Path.Combine(PATH_CACHE, Util.FileName.FromAssetRequest("/latest" + os + "/101")), _latest);
                                }

                                i++;
                            }
                        }
                        catch (Exception ex)
                        {
                            return ex;
                        }

                        return null;
                    });

                if (exception != null)
                    throw exception;

                using (var w = new BinaryWriter(new BufferedStream(File.Open(Path.Combine(PATH_SESSION, "assets"), FileMode.Create, FileAccess.Write, FileShare.None)), Encoding.ASCII))
                {
                    w.BaseStream.Position = 20;
                    int entries = 0;
                    int baseIdNone = 0;

                    foreach (var exeId in exeIds)
                    {
                        if (exeId != 0)
                        {
                            w.Write(baseIdNone);
                            w.Write(exeId);
                            w.Write((uint)exeSize);
                            entries++;
                        }
                    }

                    w.Write(baseIdNone);
                    w.Write(manifestId);
                    w.Write((uint)manifestSize);
                    entries++;

                    labelManifestsBuild.Text = string.Format("{0:#,##0}", buildId);
                    labelManifestsCount.Text = "---";
                    labelManifestsUpdates.Text = "---";

                    Dat.Manifest manifest = null;

                    exception = await Task.Run<Exception>(
                        delegate
                        {
                            try
                            {
                                manifest = Dat.Manifest.Parse(DownloadData(clients[0], string.Format(URL_PATH, 0, manifestId), true));
                            }
                            catch (Exception ex)
                            {
                                return ex;
                            }

                            return null;
                        });

                    if (exception != null)
                        throw exception;
                    
                    long totalCoreBytes = 0;
                    foreach (var r in manifest.records)
                    {
                        totalCoreBytes += r.size;

                        w.Write(baseIdNone);
                        w.Write(r.fileId);
                        w.Write((uint)r.size);
                        entries++;
                    }

                    labelManifestsCount.Text = string.Format("{0:#,##0} ({1})", manifest.records.Length, FormatBytes(totalCoreBytes));
                    progressManifests.Maximum = manifest.records.Length;

                    DateTime nextUpdatesText = DateTime.MinValue;

                    var setUpdatesText = new Action<int, long, bool>(
                        delegate(int fileCount, long bytes, bool force)
                        {
                            var now = DateTime.UtcNow;
                            if (force || now > nextUpdatesText)
                            {
                                nextUpdatesText = now.AddSeconds(1);
                                labelManifestsUpdates.Text = string.Format("{0:#,##0} (~{1})", fileCount, FormatBytes(bytes));
                            }
                        });

                    long totalUpdatesBytes = 0;
                    totalUpdatesBytes = 0;
                    int files = 0;

                    byte threads = 10;
                    int count = manifest.records.Length;
                    if (count < threads)
                        threads = (byte)count;
                    int i = 0;
                    int k = 0;

                    HashSet<int> handled = new HashSet<int>();
                    CancellationTokenSource cancel = new CancellationTokenSource();

                    using (var pt = new Util.ParallelTasks<TaskManifestWork>(threads, cancel.Token))
                    {
                        Util.ParallelTasks<TaskManifestWork>.TaskResult task;
                        while ((task = await pt.Next()) != null)
                        {
                            WebClient client;

                            if (task.HasException)
                            {
                                cancel.Cancel();
                                throw task.Exception;
                            }
                            else if (task.HasResult)
                            {
                                var result = task.Result;
                                long bytes = 0;
                                int _files = 0;

                                client = result.client;

                                await Task.Run(
                                    delegate
                                    {
                                        foreach (var r in result.manifest.records)
                                        {
                                            if (!handled.Add(r.baseId))
                                                continue;

                                            if (!baseIds.ContainsKey(r.baseId))
                                            {
                                                w.Write(baseIdNone);
                                                w.Write(r.fileId);
                                                w.Write((uint)r.size);
                                                entries++;
                                                bytes += (uint)(r.size * AVG_COMPRESSION + 0.5f);
                                            }
                                            else if (baseIds[r.baseId] < r.fileId)
                                            {
                                                w.Write(baseIds[r.baseId]);
                                                w.Write(r.fileId);
                                                w.Write((uint)r.size);
                                                entries++;
                                                bytes += (uint)(r.size * AVG_PATCH_COMPRESSION + 0.5f);
                                            }
                                            else
                                                continue;

                                            _files++;
                                        }
                                    });

                                if (_files > 0)
                                {
                                    totalUpdatesBytes += bytes;
                                    files += _files;

                                    setUpdatesText(files, totalUpdatesBytes, false);
                                }

                                progressManifests.Value++;
                            }
                            else
                            {
                                client = clients[k++];
                            }

                            if (i < count)
                            {
                                var record = manifest.records[i++];

                                switch (record.baseId)
                                {
                                    case 724786:    //Launcher
                                        break;
                                    case 1283391:   //Launcher64
                                        if (!checkManifests64.Checked)
                                            continue;
                                        break;
                                    case 1475411:   //LauncherOSX
                                        if (!checkManifestsOSX.Checked)
                                            continue;
                                        break;
                                    case 622855:    //ClientContent86
                                        break;
                                    case 1283393:   //ClientContent64
                                        break;
                                    case 296040:    //English
                                        if (!checkManifestsEnglish.Checked)
                                            continue;
                                        break;
                                    case 296042:    //German
                                        if (!checkManifestsGerman.Checked)
                                            continue;
                                        break;
                                    case 296043:    //French
                                        if (!checkManifestsFrench.Checked)
                                            continue;
                                        break;
                                    case 1051220:   //Chinese
                                        if (!checkManifestsChinese.Checked)
                                            continue;
                                        break;
                                }

                                task.QueueWork(new Func<TaskManifestWork>(
                                        delegate
                                        {
                                            return new TaskManifestWork()
                                            {
                                                manifest = Dat.Manifest.Parse(DownloadData(client, string.Format(URL_PATH, 0, record.fileId), true)),
                                                record = record,
                                                client = client
                                            };
                                        }));
                            }
                        }
                    }

                    setUpdatesText(files, totalUpdatesBytes, true);

                    if (totalUpdatesBytes == 0)
                    {
                        labelManifestsUpdates.Text = "0";
                    }

                    w.BaseStream.Position = 0;
                    w.Write(HEADER_ASSETS);
                    w.Write(DateTime.UtcNow.ToBinary());
                    w.Write(buildId);
                    w.Write(entries);

                    labelManifestsReady.Visible = true;
                    labelManifestsReadyNoUpdates.Visible = totalUpdatesBytes == 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to download manifest:\n\n" + ex.Message, "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                buttonDownloadManifests.Enabled = true;
                progressManifests.Visible = false;
                checkManifests32.Enabled = true;
                checkManifests64.Enabled = true;
                checkManifestsChinese.Enabled = true;
                checkManifestsEnglish.Enabled = true;
                checkManifestsFrench.Enabled = true;
                checkManifestsGerman.Enabled = true;
                checkManifestsOSX.Enabled = true;
            }
        }

        private async Task<LoadAssetsResult> LoadAssetRequestsAsync()
        {
            return await Task.Run<LoadAssetsResult>(new Func<LoadAssetsResult>(LoadAssetRequests));
        }

        private LoadAssetsResult LoadAssetRequests()
        {
            LoadAssetsResult result = new LoadAssetsResult();

            try
            {
                using (var r = new BinaryReader(new BufferedStream(File.Open(Path.Combine(PATH_SESSION, "assets"), FileMode.Open, FileAccess.Read, FileShare.None)), Encoding.ASCII))
                {
                    var header = r.ReadUInt32();
                    if (header != HEADER_ASSETS)
                        throw new IOException("Invalid file header");
                    result.date = DateTime.FromBinary(r.ReadInt64());
                    result.buildId = r.ReadInt32();

                    int count = r.ReadInt32();
                    var entries = result.entries = new AssetEntry[count];

                    for (var i = 0; i < count; i++)
                    {
                        entries[i] = new AssetEntry()
                        {
                            baseId = r.ReadInt32(),
                            fileId = r.ReadInt32(),
                            size = r.ReadUInt32()
                        };
                    }
                }
            }
            catch (Exception e)
            {
                result.exception = e;
            }

            return result;
        }

        private async void buttonGenerateUpdateOutput_Click(object sender, EventArgs e)
        {
            buttonGenerateUpdateOutput.Enabled = false;

            try
            {
                var requests = await LoadAssetRequestsAsync();

                if (!VerifyAssets(requests))
                    return;

                IList<AssetEntry> entries = requests.entries;

                if (!checkUpdatesManualIncludeExisting.Checked)
                {
                    List<AssetEntry> _entries = new List<AssetEntry>(requests.entries.Length);
                    await Task.Run(
                        delegate
                        {
                            var existing = new HashSet<string>();
                            try
                            {
                                foreach (var f in Directory.GetFiles(PATH_CACHE))
                                {
                                    existing.Add(Path.GetFileName(f));
                                }
                            }
                            catch { }

                            foreach (var entry in requests.entries)
                            {
                                if (!existing.Contains(Util.FileName.FromAssetRequest(GetAssetRequest(entry))))
                                {
                                    if (entry.baseId != 0 && checkUpdatesManualNoHeaders.Checked && existing.Contains(Util.FileName.FromAssetRequest(GetAssetRequest(0, entry.fileId))))
                                    {
                                        continue;
                                    }

                                    _entries.Add(entry);
                                }
                            }
                        });

                    if (_entries.Count != requests.entries.Length)
                    {
                        entries = _entries;
                        if (_entries.Count == 0)
                        {
                            MessageBox.Show(this, "All files have already been downloaded", "Nothing to export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }
                    }
                }

                if (checkUpdatesManualNoHeaders.Checked)
                {
                    int l = URL_FILE_BASE.Length;
                    long difference = 0;
                    int count = 0;

                    for (int i = 0, j = entries.Count; i < j; i++)
                    {
                        var entry = entries[i];
                        if (entry.baseId != 0)
                        {
                            entry.baseId = 0;
                            entries[i] = entry;
                            difference += (long)(entry.size * AVG_COMPRESSION + 0.5) - (uint)(entry.size * AVG_PATCH_COMPRESSION + 0.5);
                            count++;
                        }
                    }

                    if (count > 0 && MessageBox.Show(this, "When substituting, full files will be downloaded instead of smaller patches. This will add an estimated " + FormatBytes(difference) + ".\n\nHeaders can alternatively be downloaded when verifying the files for roughly " + FormatBytes(count * AVG_HEADER_SIZE) + ".\n\nAre you sure?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != System.Windows.Forms.DialogResult.Yes)
                        return;
                }

                string format;
                using (formOutputFormat f = new formOutputFormat(buttonGenerateUpdateOutput))
                {
                    if (f.ShowDialog(this) != System.Windows.Forms.DialogResult.OK)
                        return;
                    format = f.OutputFormat;
                }

                Settings.UpdatesOutputFormat = format;
                Settings.UpdatesExportIncludeExisting = checkUpdatesManualIncludeExisting.Checked;
                Settings.UpdatesExportSubstituteHeaders = checkUpdatesManualNoHeaders.Checked;

                string path = Path.Combine(PATH_SESSION, "export.txt");

                await Task.Run(
                    delegate
                    {
                        using (var w = new StreamWriter(path))
                        {
                            foreach (var entry in entries)
                            {
                                string r = GetAssetRequest(entry);
                                w.Write(format.Replace("$filename", Util.FileName.FromAssetRequest(r)).Replace("$url", ASSET_URL + r));
                            }
                        }
                    });

                try
                {
                    Process p = new Process();
                    p.StartInfo.UseShellExecute = true;
                    p.StartInfo.FileName = "notepad.exe";
                    p.StartInfo.Arguments = '"' + path + '"';

                    if (!p.Start())
                        throw new Exception();
                }
                catch
                {
                    MessageBox.Show(this, "Output saved as:\n\n" + path, "Output location", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            finally
            {
                buttonGenerateUpdateOutput.Enabled = true;
            }
        }

        private void radioUpdatesAuto_CheckedChanged(object sender, EventArgs e)
        {
            if (radioUpdatesAuto.Checked)
            {
                radioUpdatesManual.Checked = false;
                panelUpdatesAuto.Visible = true;
                panelUpdatesManual.Visible = false;
                if (buttonUpdates.Selected)
                    Settings.InitialPanel = Settings.Panels.UpdatesAuto;
            }
        }

        private void radioUpdatesManual_CheckedChanged(object sender, EventArgs e)
        {
            if (radioUpdatesManual.Checked)
            {
                radioUpdatesAuto.Checked = false;
                panelUpdatesAuto.Visible = false;
                panelUpdatesManual.Visible = true;
                if (buttonUpdates.Selected)
                    Settings.InitialPanel = Settings.Panels.UpdatesManual;
            }
        }

        private bool VerifyAssets(LoadAssetsResult result)
        {
            if (result.exception != null)
            {
                string message;
                if (result.exception is FileNotFoundException)
                    message = "Manifests must be downloaded and processed first";
                else
                    message = result.exception.Message;
                MessageBox.Show(this, "Unable to load the list of assets to download:\n\n" + message, "Unable to load assets", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            var age = (int)(DateTime.UtcNow.Subtract(result.date).TotalDays + 0.5);

            if (age >=  3)
            {
                if (MessageBox.Show(this, "The loaded assets file is over " + age + " days old.\n\nAre you sure?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != System.Windows.Forms.DialogResult.Yes)
                    return false;
            }

            return true;
        }

        private string GetAssetRequest(AssetEntry entry)
        {
            if (entry.baseId != 0)
                return string.Format(URL_PATH, entry.baseId, entry.fileId);
            else
                return string.Format(URL_PATH_COMPRESSED, entry.baseId, entry.fileId);
        }

        private string GetAssetRequest(int baseId, int fileId)
        {
            if (baseId != 0)
                return string.Format(URL_PATH, baseId, fileId);
            else
                return string.Format(URL_PATH_COMPRESSED, baseId, fileId);
        }

        private IPAddress[] GetAssetHostIPs()
        {
            if (assetIPs != null)
                return assetIPs;

            return assetIPs = Dns.GetHostAddresses(new Uri(ASSET_URL).DnsSafeHost);
        }

        private IPAddress[] GetAssetHostIPs(bool showMessageBoxOnFail)
        {
            try
            {
                return GetAssetHostIPs();
            }
            catch (Exception ex)
            {
                if (showMessageBoxOnFail)
                    MessageBox.Show(this, "Unable to resolve IPs for " + new Uri(ASSET_URL).DnsSafeHost + "\n\n" + ex.Message, "DNS failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private async void buttonUpdatesManualImport_Click(object sender, EventArgs e)
        {
            buttonUpdatesManualImport.Enabled = false;
            labelUpdatesImport.Visible = false;
            labelUpdatesManualReady.Visible = false;
            labelUpdatesManualReadyMissing.Visible = false;
            labelUpdatesImportDownload.Visible = false;

            try
            {
                int count = 0;

                var requests = await LoadAssetRequestsAsync();

                if (!VerifyAssets(requests))
                    return;

                labelUpdatesImport.Text = "Processing";
                labelUpdatesImport.Visible = true;

                List<AssetEntry> missingHeaders = new List<AssetEntry>();
                int totalFiles = 0;
                int processedFiles = 0;

                #region Process files

                var t = Task.Run(
                    delegate
                    {
                        var l = requests.entries.Length;

                        HashSet<string> existing = new HashSet<string>();
                        try
                        {
                            var files = Directory.GetFiles(PATH_CACHE);
                            totalFiles = files.Length;
                            foreach (var file in files)
                            {
                                existing.Add(Path.GetFileName(file));
                            }
                        }
                        catch { }

                        if (totalFiles > 0)
                        {
                            for (int j = 0; j < l; j++)
                            {
                                var entry = requests.entries[j];

                                string filename;
                                if (existing.Contains(filename = Util.FileName.FromAssetRequest(GetAssetRequest(entry))))
                                {
                                    if (entry.baseId != 0)
                                    {
                                        //patch to an existing file, headers required
                                        try
                                        {
                                            using (var r = new BinaryReader(File.Open(Path.Combine(PATH_CACHE, filename), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)))
                                            {
                                                if (r.BaseStream.Length > 4)
                                                {
                                                    var h = r.ReadUInt32();
                                                    if (h != Net.AssetServer.Client.HEADER_HTTP_UPPER && h != Net.AssetServer.Client.HEADER_HTTP_LOWER)
                                                        missingHeaders.Add(entry);
                                                }
                                                else
                                                    missingHeaders.Add(entry);
                                            }
                                        }
                                        catch { }
                                    }

                                    count++;
                                }
                                else if (entry.baseId != 0 && existing.Contains(Util.FileName.FromAssetRequest(GetAssetRequest(0, entry.fileId))))
                                {
                                    count++;
                                }

                                processedFiles++;
                            }
                        }
                    });

                #endregion

                int lastProcessed = 0;
                labelUpdatesImportDownload.Visible = true;
                labelUpdatesImportDownload.Text = "...";

                while (!t.IsCompleted)
                {
                    await Task.Delay(1000);

                    if (lastProcessed != processedFiles && totalFiles > 0)
                    {
                        lastProcessed = processedFiles;
                        labelUpdatesImportDownload.Text = lastProcessed + " of " + totalFiles;
                    }
                }

                var missing = requests.entries.Length - count;
                if (missing > 0)
                    labelUpdatesImport.Text = string.Format("{0:#,##0} of {1:#,##0} found, {2:#,##0} missing", count, requests.entries.Length, missing);
                else
                    labelUpdatesImport.Text = string.Format("{0:#,##0} of {1:#,##0} found", count, requests.entries.Length);

                bool hasFiles = count > 0 && missing < count / 4;

                #region Download missing headers

                if (missingHeaders.Count > 0)
                {
                    if (MessageBox.Show(this, "Some files that require headers are missing them.\nEstimated: " + FormatBytes(AVG_HEADER_SIZE * missingHeaders.Count) + "\n\nWould you like to download them now?", "Headers required", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != System.Windows.Forms.DialogResult.Yes)
                        return;

                    #region DNS

                    IPAddress[] ips = GetAssetHostIPs(true);
                    if (ips == null)
                        return;

                    #endregion

                    labelUpdatesImportDownload.Text = "---";

                    string label = labelUpdatesImport.Text;

                    var root = new DirectoryInfo(Path.Combine(PATH_SESSION, "temp"));
                    root.Create();

                    var downloaded = 0;
                    count = missingHeaders.Count;
                    string[] assets = new string[count];
                    for (var i = 0; i < count; i++)
                        assets[i] = GetAssetRequest(missingHeaders[i]);

                    Net.AssetDownloader downloader = new Net.AssetDownloader(2, new Net.IPPool(ips), new Uri(ASSET_URL).DnsSafeHost, assets, assets.Length, root.FullName, false, true);
                    Exception clientException = null;
                    bool isDownloading = true;

                    downloader.Complete +=
                        delegate
                        {
                            isDownloading = false;
                        };

                    downloader.Error +=
                        delegate(object o, int index, string location, Exception ex)
                        {
                            if (clientException == null)
                                clientException = ex;
                            downloader.Abort(false);
                        };

                    downloader.DownloadRate +=
                        delegate(object o, uint bytes)
                        {
                            labelUpdatesImportDownload.Text = FormatBytes(downloader.TotalBytesDownloaded) + " @ " + FormatBytes(bytes) + "/s";
                        };

                    downloader.RequestComplete +=
                        delegate(object o, int index, string location, long contentLength)
                        {
                            lock (assets)
                            {
                                downloaded++;
                            }

                            string name = Util.FileName.FromAssetRequest(location);
                            string source = Path.Combine(PATH_CACHE, name);
                            string header = Path.Combine(root.FullName, name);

                            try
                            {
                                using (var w = File.Open(header, FileMode.Open, FileAccess.Write, FileShare.Read))
                                {
                                    using (var r = File.Open(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                                    {
                                        w.Position = w.Length;
                                        int length = 1024 * 1024 * 5;
                                        if (r.Length < length)
                                            length = (int)r.Length;
                                        byte[] buffer = new byte[length];
                                        int read;

                                        do
                                        {
                                            read = r.Read(buffer, 0, length);
                                            w.Write(buffer, 0, read);
                                        }
                                        while (read > 0);
                                    }
                                }

                                File.Delete(source);
                                File.Move(header, source);
                            }
                            catch (Exception ex)
                            {
                                clientException = ex;
                                downloader.Abort(false);
                            }
                        };

                    downloader.Start();

                    int last = -1;
                    while (isDownloading)
                    {
                        await Task.Delay(1000);

                        if (last != downloaded)
                        {
                            last = downloaded;
                            labelUpdatesImport.Text = "Downloading " + last + " of " + count;
                        }
                    }

                    if (clientException != null)
                    {
                        labelUpdatesImport.Text = "Failed";
                        MessageBox.Show(this, "A file could not be downloaded:\n\n" + clientException.Message, "Download interrupted", MessageBoxButtons.OK, MessageBoxIcon.Error);

                        return;
                    }
                    else
                        labelUpdatesImport.Text = label;
                }

                #endregion

                labelUpdatesImportDownload.Visible = false;

                if (hasFiles)
                {
                    labelUpdatesManualReady.Visible = true;
                    if (missing > 0)
                        labelUpdatesManualReadyMissing.Visible = true;
                }
            }
            finally
            {
                buttonUpdatesManualImport.Enabled = true;
            }
        }

        private async void buttonUpdatesDownload_Click(object sender, EventArgs e)
        {
            if (!buttonManifests.Enabled)
            {
                MessageBox.Show(this, "Updates cannot be downloaded while manifests are being processed", "Manifests in use", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            buttonUpdatesDownload.Enabled = false;
            labelUpdatesAutoReady.Visible = false;
            labelUpdatesDownloadSpeed.Text = "---";
            bool isComplete = false;

            try
            {
                var requests = await LoadAssetRequestsAsync();

                if (!VerifyAssets(requests))
                    return;

                var entries = requests.entries;
                int count = entries.Length;
                var assets = new string[count];
                var sizes = new uint[count];

                long estimatedTotal = 0;
                for (var j = 0; j < count; j++)
                {
                    assets[j] = GetAssetRequest(entries[j]);

                    uint size = entries[j].size;
                    sizes[j] = size = (uint)(size * AVG_COMPRESSION + 0.5f);
                    estimatedTotal += size;
                }

                string totalAssets = string.Format("{0:#,##0}", count);
                string totalSize = "";

                long contentBytesDownloaded = 0;
                long lastBytes = 0;
                int lastCount = 0;
                int filesDownloaded = 0;
                long lastEstimatedTotal = 0;

                progressUpdatesDownload.Maximum = count;
                progressUpdatesDownload.Value = 0;
                progressUpdatesDownload.Visible = true;

                var root = new DirectoryInfo(PATH_CACHE);
                string pathResume = Path.Combine(root.FullName, "resume");
                bool canResume = false;

                #region Existing asset files
                if (!root.Exists)
                {
                    try
                    {
                        root.Create();
                    }
                    catch
                    {
                        MessageBox.Show(this, "Unable to create session folder:\n\n" + root.FullName, "Faile to create folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                else
                {
                    foreach (var f in root.GetFiles("*.tmp"))
                    {
                        try
                        {
                            f.Delete();
                        }
                        catch { }
                    }

                    await Task.Run(new Action(
                        delegate
                        {
                            FileInfo[] files;
                            try
                            {
                                files = root.GetFiles();
                                if (files.Length == 0)
                                    return;
                            }
                            catch
                            {
                                return;
                            }

                            HashSet<string> existing = new HashSet<string>();
                            foreach (var f in files)
                            {
                                existing.Add(f.Name);
                            }

                            Dictionary<string, int> indexes = new Dictionary<string, int>(count);
                            for (int j = 0; j < count; j++)
                                indexes[Util.FileName.FromAssetRequest(assets[j])] = j;

                            try
                            {
                                using (var r = new BinaryReader(File.Open(pathResume, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                                {
                                    using (var w = new BinaryWriter(File.Open(pathResume, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)))
                                    {
                                        var match = DateTime.FromBinary(r.ReadInt64()) == requests.date;

                                        while (r.BaseStream.Position < r.BaseStream.Length)
                                        {
                                            w.BaseStream.Position = r.BaseStream.Position;

                                            string request;
                                            uint contentLength;
                                            try
                                            {
                                                request = GetAssetRequest(r.ReadInt32(),r.ReadInt32());
                                                contentLength = r.ReadUInt32();
                                            }
                                            catch (EndOfStreamException ex)
                                            {
                                                w.BaseStream.SetLength(w.BaseStream.Position);
                                                throw ex;
                                            }

                                            int index;
                                            string name = Util.FileName.FromAssetRequest(request);
                                            if (existing.Contains(name) && indexes.TryGetValue(request, out index))
                                            {
                                                contentBytesDownloaded += contentLength;
                                                estimatedTotal += (contentLength - sizes[index]);
                                                assets[index] = null;
                                                filesDownloaded++;
                                            }
                                        }

                                        canResume = r.BaseStream.Position > 0;
                                    }
                                }
                            }
                            catch { }

                            //handle any remaining assets
                            foreach (var name in existing)
                            {
                                int index;
                                if (indexes.TryGetValue(name, out index))
                                {
                                    if (assets[index] != null)
                                    {
                                        assets[index] = null;
                                        long contentLength;
                                        try
                                        {
                                            contentLength = new FileInfo(Path.Combine(root.FullName, name)).Length;
                                        }
                                        catch
                                        {
                                            contentLength = 0;
                                        }
                                        contentBytesDownloaded += contentLength;
                                        estimatedTotal += (contentLength - sizes[index]);
                                        filesDownloaded++;
                                    }
                                }
                            }
                        }));
                }

                #endregion

                #region DNS

                IPAddress[] ips = GetAssetHostIPs(true);
                if (ips == null)
                    return;

                #endregion

                BinaryWriter session;
                try
                {
                    session = new BinaryWriter(new BufferedStream(File.Open(pathResume, canResume ? FileMode.Open : FileMode.Create, FileAccess.Write, FileShare.None)));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Unable to write session information:\n\n" + ex.Message, "Failed to start session", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Action setStats = new Action(
                    delegate
                    {
                        if (filesDownloaded != lastCount)
                        {
                            lastCount = filesDownloaded;
                            progressUpdatesDownload.Value = lastCount;
                            labelUpdatesDownloadFiles.Text = string.Format("{0:#,##0}", lastCount) + " of " + totalAssets;
                        }

                        if (lastBytes != contentBytesDownloaded)
                        {
                            if (lastEstimatedTotal != estimatedTotal)
                            {
                                lastEstimatedTotal = estimatedTotal;
                                totalSize = "~" + FormatBytes(lastEstimatedTotal);
                            }

                            lastBytes = contentBytesDownloaded;
                            labelUpdatesDownloadSize.Text = FormatBytes(lastBytes) + " of " + totalSize;
                        }
                    });

                Net.AssetDownloader downloader = new Net.AssetDownloader(10, new Net.IPPool(ips), new Uri(ASSET_URL).DnsSafeHost, assets, root.FullName);
                Exception clientException = null;
                bool isDownloading = true;

                downloader.Complete += 
                    delegate
                    {
                        isDownloading = false;
                    };

                downloader.DownloadRate +=
                    delegate(object o, uint rate)
                    {
                        labelUpdatesDownloadSpeed.Text = FormatBytes(rate) + "/s";
                    };

                downloader.Error +=
                    delegate(object o, int index, string location, Exception ex)
                    {
                        if (clientException == null)
                            clientException = ex;
                        downloader.Abort(false);
                    };

                downloader.RequestComplete +=
                    delegate(object o, int index, string location, long contentLength)
                    {
                        lock (assets)
                        {
                            filesDownloaded++;
                            estimatedTotal += (contentLength - sizes[index]);
                            contentBytesDownloaded += contentLength;
                        }

                        try
                        {
                            lock (session)
                            {
                                var entry = entries[index];
                                session.Write(entry.baseId);
                                session.Write(entry.fileId);
                                session.Write((uint)contentLength);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (clientException == null)
                                clientException = ex;
                            downloader.Abort(false);
                        }
                    };

                using (session)
                {
                    if (canResume)
                        session.BaseStream.Position = session.BaseStream.Length;
                    else
                        session.Write(requests.date.ToBinary());

                    downloader.Start();

                    while (isDownloading)
                    {
                        await Task.Delay(500);

                        setStats();
                    }

                    if (clientException != null)
                    {
                        MessageBox.Show(this, "A file could not be downloaded:\n\n" + clientException.Message, "Download interrupted", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    isComplete = true;
                    labelUpdatesDownloadFiles.Text = totalAssets;
                    labelUpdatesDownloadSize.Text = FormatBytes(estimatedTotal);
                    labelUpdatesDownloadSpeed.Text = "Complete";
                    labelUpdatesAutoReady.Visible = true;
                }
            }
            finally
            {
                buttonUpdatesDownload.Enabled = true;
                progressUpdatesDownload.Visible = false;
                if (!isComplete)
                    labelUpdatesDownloadSpeed.Text = "---";
            }
        }

        private void buttonPatchServer_Click(object sender, EventArgs e)
        {
            bool isStarted = false;

            try
            {
                buttonPatchServer.Enabled = false;

                try
                {
                    if (server == null)
                    {
                        failedFiles = new HashSet<string>();

                        server = new Net.AssetServer.Server(PATH_CACHE);
                        server.AllowRemoteConnections = !checkPatchLocal.Checked;
                        server.ActiveStateChanged += server_ActiveStateChanged;
                        server.FileNotFound += server_FileNotFound;
                        server.ClientRequestRepeated += server_ClientRequestRepeated;
                    }

                    if (checkPatchEnableDownload.Checked && downloader == null)
                    {
                        InitializeDownloader();
                    }

                    server.Start(Settings.ServerPort);
                    Settings.ServerPort = server.Port;

                    isStarted = true;

                    DoServer();
                }
                catch (Exception ex)
                {
                    if (Settings.ServerPort != 0)
                    {
                        if (ex is System.Net.Sockets.SocketException)
                        {
                            if (((System.Net.Sockets.SocketException)ex).SocketErrorCode == System.Net.Sockets.SocketError.AddressAlreadyInUse)
                            {
                                if (MessageBox.Show(this, "Port " + Settings.ServerPort + " is already in use\n\nWould you like to try a different one?", "Port already in use", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == System.Windows.Forms.DialogResult.Yes)
                                    Settings.ServerPort = 0;
                                return;
                            }
                        }
                    }

                    MessageBox.Show(this, "Unable to start the server\n\n" + ex.Message, "Unable to start server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                if (!isStarted)
                {
                    buttonPatchServer.Enabled = true;
                }
            }
        }

        void server_ClientRequestRepeated(object sender, string e)
        {
            ShowRequestWarning(e, formWarnings.Warnings.Repeated, null);

            lock (failedFiles)
            {
                if (!failedFiles.Add(e))
                    return;
            }
        }

        private void ShowRequestWarning(string request, formWarnings.Warnings warning, object data)
        {
            if (this.InvokeRequired)
            {
                try
                {
                    this.Invoke(new MethodInvoker(
                        delegate
                        {
                            ShowRequestWarning(request, warning, data);
                        }));
                }
                catch { }
                return;
            }

            try
            {
                if (requestsWindow == null || requestsWindow.IsDisposed)
                {
                    requestsWindow = new formWarnings();
                    requestsWindow.StartPosition = FormStartPosition.Manual;
                    requestsWindow.Location = new Point(this.Location.X + this.Width / 2 - requestsWindow.Width / 2, this.Location.Y + this.Height / 2 - requestsWindow.Height / 2);
                    requestsWindow.Show(this);
                }

                requestsWindow.SetRequestWarning(request, warning, data);
            }
            catch { }
        }

        private bool ParseFilename(string name, out int baseId, out int fileId, out bool isCompressed)
        {
            if (char.IsDigit(name[0]))
            {
                try
                {
                    var i = name.IndexOf('_');
                    baseId = int.Parse(name.Substring(0, i));
                    var j = name.IndexOf('_', ++i);
                    if (j == -1)
                        j = name.Length;
                    fileId = int.Parse(name.Substring(i, j - i));
                    isCompressed = (++j < name.Length && name[j] == 'c');
                    return true;
                }
                catch { }
            }

            baseId = fileId = 0;
            isCompressed = false;
            return false;
        }

        private bool ParseRequest(string request, out int baseId, out int fileId, out bool isCompressed)
        {
            try
            {
                if (request.StartsWith(URL_FILE_BASE))
                {
                    var l = URL_FILE_BASE.Length;
                    var j = request.IndexOf('/', l);
                    if (j == -1)
                        throw new Exception();

                    baseId = int.Parse(request.Substring(l, j - l));
                    l = j + 1;
                    j = request.IndexOf('/', l);

                    if (j == -1)
                    {
                        fileId = int.Parse(request.Substring(l));
                        isCompressed = false;
                        return true;
                    }
                    else
                    {
                        fileId = int.Parse(request.Substring(l, j - l));
                        isCompressed = request[j + 1] == 'c';
                        return true;
                    }
                }
            }
            catch { }

            baseId = fileId = 0;
            isCompressed = false;
            return false;
        }

        private void InitializeDownloader()
        {
            #region DNS

            IPAddress[] ips = GetAssetHostIPs(true);
            if (ips == null)
                return;

            #endregion

            var downloader = new Net.AssetDownloader(2, new Net.IPPool(ips), new Uri(ASSET_URL).DnsSafeHost, PATH_CACHE);
            missingFiles = new HashSet<string>();

            int downloadedFiles = 0;
            int lastFiles = 0;
            uint downloadRate = 0;
            uint lastRate = 0;
            Exception failed = null;

            Action setText =
                delegate
                {
                    if (lastFiles != downloadedFiles || lastRate != downloadRate)
                    {
                        lastFiles = downloadedFiles;
                        lastRate = downloadRate;

                        if (lastRate > 0)
                            labelPatchDownloads.Text = FormatBytes(downloader.TotalBytesDownloaded) + " @ " + FormatBytes(lastRate) + "/s";
                        else
                            labelPatchDownloads.Text = FormatBytes(downloader.TotalBytesDownloaded);
                    }
                };

            EventHandler<uint> downloaderBytesDownloaded =
                delegate(object o, uint bytes)
                {
                    lock (downloader)
                    {
                        totalBytesDownloaded += bytes;
                    }
                };

            EventHandler<uint> downloaderDownloadRate =
                delegate(object o, uint rate)
                {
                    downloadRate = rate;
                    setText();
                };

            Net.AssetDownloader.RequestCompleteEventHandler downloaderRequestComplete =
                delegate(object o, int index, string location, long contentBytes)
                {
                    lock (downloader)
                    {
                        downloadedFiles++;
                    }
                };

            EventHandler downloaderComplete =
                delegate
                {
                    missingFiles.Clear();
                };

            Net.AssetDownloader.ErrorEventHandler downloaderError = null;
            downloaderError=
                delegate(object o, int index, string location, Exception error)
                {
                    lock (downloader)
                    {
                        if (failed != null)
                            return;
                        failed = error;
                    }

                    ShowRequestWarning(location, formWarnings.Warnings.DownloadError, error);
                };

            downloader.BytesDownloaded += downloaderBytesDownloaded;
            downloader.DownloadRate += downloaderDownloadRate;
            downloader.Error += downloaderError;
            downloader.RequestComplete += downloaderRequestComplete;
            downloader.Complete += downloaderComplete;

            this.downloader = downloader;
        }

        void server_FileNotFound(object sender, Net.AssetServer.Client.FileNotFoundEventArgs e)
        {
            bool allowDownloads = checkPatchEnableDownload.Checked;
            bool isCompressed;
            int baseId, fileId;
            if (ParseRequest(e.Location, out baseId, out fileId, out isCompressed))
            {
                if (!isCompressed)
                {
                    if (baseId == 0 && (!allowDownloads || !checkPatchEnableDownloadUncompressed.Checked))
                    {
                        //default uncompressed file - a patch or compressed file was not accepted
                        ShowRequestWarning(e.Location, formWarnings.Warnings.Uncompressed, null);
                        return;
                    }
                    else
                    {
                        if (baseId != 0 && File.Exists(Path.Combine(PATH_CACHE, Util.FileName.FromAssetRequest(GetAssetRequest(0, fileId)))))
                        {
                            return;
                        }
                    }
                }
            }

            if (allowDownloads)
            {
                var downloader = this.downloader;
                if (downloader != null)
                {
                    var client = (Net.AssetServer.Client)sender;

                    ManualResetEvent waiter = new ManualResetEvent(false);
                    EventHandler closed = null;
                    Net.AssetDownloader.RequestCompleteEventHandler requestComplete = null;
                    
                    Action remove =
                        delegate
                        {
                            lock (e)
                            {
                                downloader.Complete -= closed;
                                downloader.RequestComplete -= requestComplete;
                                client.Closed -= closed;
                            }

                            waiter.Set();
                        };

                    closed =
                        delegate
                        {
                            remove();
                        };

                    requestComplete =
                        delegate(object o, int index, string location, long contentLength)
                        {
                            if (location == e.Location)
                            {
                                remove();
                            }
                        };

                    lock (e)
                    {
                        downloader.Complete += closed;
                        downloader.RequestComplete += requestComplete;
                        client.Closed += closed;
                    }

                    bool added = true;
                    lock (downloader)
                    {
                        if (missingFiles.Add(e.Location))
                            downloader.Add(e.Location);
                        else
                            added = false;
                    }

                    if (added)
                    {
                        if (!downloader.IsActive)
                        {
                            try
                            {
                                this.Invoke(new MethodInvoker(
                                    delegate
                                    {
                                        if (!downloader.IsActive)
                                            downloader.Start();
                                    }));
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        if (File.Exists(Path.Combine(PATH_CACHE, Util.FileName.FromAssetRequest(e.Location))))
                        {
                            remove();
                        }
                    }

                    while (!waiter.WaitOne())
                    {
                        Thread.Sleep(1000);
                    }

                    e.Retry = true;
                }
            }
            else
            {
                ShowRequestWarning(e.Location, formWarnings.Warnings.NotFound, null);
            }
        }

        private async void DoServer()
        {
            CancellationTokenSource cancel = new CancellationTokenSource();
            string lastRequest = null;

            EventHandler<bool> stateChanged =
                delegate(object o, bool active)
                {
                    if (!active)
                        cancel.Cancel();
                };

            EventHandler<Net.HttpStream.HttpRequestHeader> headerReceived =
                delegate (object o, Net.HttpStream.HttpRequestHeader header)
                {
                    lastRequest = header.Location;
                };

            server.ActiveStateChanged += stateChanged;
            server.ClientRequestHeaderReceived += headerReceived;

            do
            {
                try
                {
                    await Task.Delay(1000, cancel.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                if (lastRequest != null)
                {
                    labelServerLastRequest.Text = lastRequest;
                    lastRequest = null;
                }
            }
            while (!cancel.IsCancellationRequested);

            server.ActiveStateChanged -= stateChanged;
            server.ClientRequestHeaderReceived -= headerReceived;
        }

        void server_ActiveStateChanged(object sender, bool e)
        {
            if (this.InvokeRequired)
            {
                try
                {
                    this.Invoke(new MethodInvoker(
                        delegate
                        {
                            server_ActiveStateChanged(sender, e);
                        }));
                }
                catch { }
                return;
            }

            if (e)
            {
                var server = (Net.AssetServer.Server)sender;
                labelServerStatus.Text = "Listening on port " + server.Port;

                Settings.ServerAllowDownloads = checkPatchEnableDownload.Checked;
                Settings.ServerAllowRemoteConnections = !checkPatchLocal.Checked;
                Settings.ServerAllowUncompressedDownloads = checkPatchEnableDownloadUncompressed.Checked;
            }
            else
            {
                labelServerStatus.Text = "Inactive";

                if (downloader != null)
                    downloader.Stop();
            }

            buttonPatchServer.Enabled = !e;
            labelPatchServerStop.Visible = e;
        }

        private void labelUpdatesManualOpenFolder_Click(object sender, EventArgs e)
        {
            try
            {
                new DirectoryInfo(PATH_CACHE).Create();
                OpenFolder(PATH_CACHE);
            }
            catch { }
        }

        private void OpenFolder(string path)
        {
            using (Process p = new Process())
            {
                p.StartInfo.UseShellExecute = true;
                p.StartInfo.FileName = "explorer.exe";
                p.StartInfo.Arguments = "\"" + path + '"';

                if (p.Start())
                    return;
            }
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void labelPatchServerStop_Click(object sender, EventArgs e)
        {
            this.server.Stop();
        }

        private void buttonPatchLaunch_Click(object sender, EventArgs e)
        {
            int port = 0;
            if (buttonPatchServer.Enabled)
            {
                buttonPatchServer_Click(sender, e);
                port = server.Port;
            }
            else if (server != null)
                port = server.Port;

            if (port == 0)
                return;

            OpenFileDialog f = new OpenFileDialog();

            f.Filter = "Guild Wars 2|Gw2*.exe|All executables|*.exe";
            if (Environment.Is64BitOperatingSystem)
                f.Title = "Open Gw2-64.exe";
            else
                f.Title = "Open Gw2.exe";

            if (!string.IsNullOrEmpty(Settings.GW2Path))
            {
                f.InitialDirectory = Path.GetDirectoryName(Settings.GW2Path);
                f.FileName = Path.GetFileName(Settings.GW2Path);
            }
            else if (!string.IsNullOrEmpty(Settings.GW2DatPath))
            {
                f.InitialDirectory = Path.GetDirectoryName(Settings.GW2DatPath);
            }

            if (f.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
            {
                Settings.GW2Path = f.FileName;

                try
                {
                    Process.Start(f.FileName, "-log -image -assetsrv 127.0.0.1:" + server.Port);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to launch:\n\n" + ex.Message, "Failed to launch", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void checkPatchLocal_CheckedChanged(object sender, EventArgs e)
        {
            if (server != null)
            {
                try
                {
                    server.AllowRemoteConnections = !checkPatchLocal.Checked;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to update server\n\n" + ex.Message, "Failed to update server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void checkPatchEnableDownload_CheckedChanged(object sender, EventArgs e)
        {
            checkPatchEnableDownloadUncompressed.Enabled = checkPatchEnableDownload.Checked;

            if (server != null)
            {
                if (checkPatchEnableDownload.Checked && downloader == null)
                {
                    InitializeDownloader();
                    if (downloader == null)
                    {
                        checkPatchEnableDownload.Checked = false;
                        return;
                    }
                }

                Settings.ServerAllowDownloads = checkPatchEnableDownload.Checked;
            }
        }

        private void checkUpdatesManualNoHeaders_CheckedChanged(object sender, EventArgs e)
        {
        }

        private void checkPatchEnableDownloadUncompressed_Click(object sender, EventArgs e)
        {
            if (checkPatchEnableDownloadUncompressed.Checked)
            {
                MessageBox.Show(this, "GW2 should only request an uncompressed file when the compressed version isn't supplied. This should only happen when you haven't downloaded all of the required files.\n\nRestart GW2 to try downloading the compressed files instead.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void checkPatchEnableDownloadUncompressed_CheckedChanged(object sender, EventArgs e)
        {
            if (server != null)
            {
                Settings.ServerAllowUncompressedDownloads = checkPatchEnableDownloadUncompressed.Checked;
            }
        }

        private void panelCleanup_VisibleChanged(object sender, EventArgs e)
        {
            if (panelCleanup.Visible)
            {
                ScanCache();
            }
        }

        private async void ScanCache()
        {
            if (panelCleanup.Tag is CancellationTokenSource)
                ((CancellationTokenSource)panelCleanup.Tag).Cancel();

            CancellationTokenSource cancel = new CancellationTokenSource();
            panelCleanup.Tag = cancel;

            EventHandler visibleChanged = null;
            visibleChanged =
                delegate
                {
                    if (!panelCleanup.Visible)
                    {
                        cancel.Cancel();
                        panelCleanup.VisibleChanged -= visibleChanged;
                    }
                };
            panelCleanup.VisibleChanged += visibleChanged;

            int count = 0;
            long size = 0;

            labelCleanupFiles.Text = labelCleanupSize.Text = "---";

            var t = Task.Run(
                delegate
                {
                    FileInfo[] files;
                    try
                    {
                        files = new DirectoryInfo(PATH_SESSION).GetFiles("*", SearchOption.AllDirectories);
                    }
                    catch
                    {
                        return;
                    }

                    foreach (var file in files)
                    {
                        if (cancel.IsCancellationRequested)
                            return;

                        count++;
                        try
                        {
                            size += file.Length;
                        }
                        catch { };
                    }
                });

            do
            {
                try
                {
                    await Task.Delay(1000, cancel.Token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                labelCleanupFiles.Text = string.Format("{0:#,##0}", count);
                labelCleanupSize.Text = FormatBytes(size);
            }
            while (!t.IsCompleted);
        }

        private async void buttonCleanupDelete_Click(object sender, EventArgs e)
        {
            buttonCleanupDelete.Enabled = false;

            try
            {
                if (!Directory.Exists(PATH_SESSION))
                    return;

                var exception = await Task.Run<Exception>(
                    delegate
                    {
                        try
                        {
                            new DirectoryInfo(PATH_SESSION).Delete(true);
                        }
                        catch (Exception ex)
                        {
                            return ex;
                        }

                        return null;
                    });

                if (exception != null)
                {
                    MessageBox.Show(this, "Unable to delete all files\n\n" + exception.Message, "Unable to delete files", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                ScanCache();

                if (panelCleanup.Visible)
                    Settings.InitialPanel = Settings.Panels.ProcessDat;
            }
            finally
            {
                buttonCleanupDelete.Enabled = true;
            }
        }

        private void labelDatReady_Click(object sender, EventArgs e)
        {
            buttonManifests.Selected = true;
        }

        private void labelManifestsReady_Click(object sender, EventArgs e)
        {
            buttonUpdates.Selected = true;
        }

        private void labelUpdatesAutoReady_Click(object sender, EventArgs e)
        {
            buttonPatch.Selected = true;
        }

        private void labelUpdatesManualReady_Click(object sender, EventArgs e)
        {
            buttonPatch.Selected = true;
        }
    }
}
