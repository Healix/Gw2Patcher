using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Gw2Patcher
{
    static class Settings
    {
        private const uint HEADER = 1734755152; //PCfg
        private const byte VERSION = 0;

        public enum Panels : byte
        {
            ProcessDat = 0,
            Manifests = 1,
            UpdatesAuto = 2,
            UpdatesManual = 3,
            Server = 4,
            Cleanup = 5
        }

        static Settings()
        {   
            try
            {
                Load();
            }
            catch
            {
                _GW2DatPath = _GW2Path = string.Empty;
                _UpdatesOutputFormat = "$filename $url\r\n";
                _Manifests64 = Environment.Is64BitOperatingSystem;
                _Manifests32 = !_Manifests64;
                _ManifestsEnglish = true;
            }
        }

        private static void Load()
        {
            if (!File.Exists("settings"))
            {
                if (File.Exists("settings.tmp"))
                    File.Move("settings.tmp", "settings");
                else
                    throw new FileNotFoundException();
            }

            using (var r = new BinaryReader(File.Open("settings", FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                if (r.ReadUInt32() != HEADER)
                    return;

                if (r.ReadByte() != VERSION)
                    return;

                _InitialPanel = (Panels)r.ReadByte();
                _GW2Path = r.ReadString();
                _GW2DatPath = r.ReadString();
                _Manifests32 = r.ReadBoolean();
                _Manifests64 = r.ReadBoolean();
                _ManifestsOSX = r.ReadBoolean();
                _ManifestsEnglish = r.ReadBoolean();
                _ManifestsFrench = r.ReadBoolean();
                _ManifestsGerman = r.ReadBoolean();
                _ManifestsChinese = r.ReadBoolean();
                _UpdatesOutputFormat = r.ReadString();
                _UpdatesExportSubstituteHeaders = r.ReadBoolean();
                _UpdatesExportIncludeExisting = r.ReadBoolean();
                _ServerPort = r.ReadInt32();
                _ServerAllowRemoteConnections = r.ReadBoolean();
                _ServerAllowDownloads = r.ReadBoolean();
                _ServerAllowUncompressedDownloads = r.ReadBoolean();
            }
        }

        private static void Save()
        {
            try
            {
                using (var w = new BinaryWriter(File.Open("settings.tmp", FileMode.Create, FileAccess.Write, FileShare.ReadWrite)))
                {
                    w.Write(HEADER);
                    w.Write(VERSION);

                    w.Write((byte)_InitialPanel);
                    w.Write(_GW2Path);
                    w.Write(_GW2DatPath);
                    w.Write(_Manifests32);
                    w.Write(_Manifests64);
                    w.Write(_ManifestsOSX);
                    w.Write(_ManifestsEnglish);
                    w.Write(_ManifestsFrench);
                    w.Write(_ManifestsGerman);
                    w.Write(_ManifestsChinese);
                    w.Write(_UpdatesOutputFormat);
                    w.Write(_UpdatesExportSubstituteHeaders);
                    w.Write(_UpdatesExportIncludeExisting);
                    w.Write(_ServerPort);
                    w.Write(_ServerAllowRemoteConnections);
                    w.Write(_ServerAllowDownloads);
                    w.Write(_ServerAllowUncompressedDownloads);
                }

                if (File.Exists("settings"))
                    File.Delete("settings");
                File.Move("settings.tmp", "settings");
            }
            catch { }
        }

        private static object _lock = new object();
        private static Task task;
        private static DateTime lastChange;

        private static void OnSettingsChanged()
        {
            lastChange = DateTime.UtcNow;

            lock (_lock)
            {
                if (task == null || task.IsCompleted)
                {
                    task = Task.Run(
                        delegate
                        {
                            DateTime startSave;
                            do
                            {
                                Task.Delay(500);
                                startSave = DateTime.UtcNow;
                                Save();
                            }
                            while (startSave < lastChange);
                        });
                }
            }
        }

        private static int _ServerPort;
        public static int ServerPort
        {
            get
            {
                return _ServerPort;
            }
            set
            {
                if (_ServerPort != value)
                {
                    _ServerPort = value;
                    OnSettingsChanged();
                }
            }
        }

        private static bool _ServerAllowRemoteConnections;
        public static bool ServerAllowRemoteConnections
        {
            get
            {
                return _ServerAllowRemoteConnections;
            }
            set
            {
                if (_ServerAllowRemoteConnections != value)
                {
                    _ServerAllowRemoteConnections = value;
                    OnSettingsChanged();
                }
            }
        }

        private static bool _ServerAllowDownloads;
        public static bool ServerAllowDownloads
        {
            get
            {
                return _ServerAllowDownloads;
            }
            set
            {
                if (_ServerAllowDownloads != value)
                {
                    _ServerAllowDownloads = value;
                    OnSettingsChanged();
                }
            }
        }

        private static bool _ServerAllowUncompressedDownloads;
        public static bool ServerAllowUncompressedDownloads
        {
            get
            {
                return _ServerAllowUncompressedDownloads;
            }
            set
            {
                if (_ServerAllowUncompressedDownloads != value)
                {
                    _ServerAllowUncompressedDownloads = value;
                    OnSettingsChanged();
                }
            }
        }

        private static string _GW2Path;
        public static string GW2Path
        {
            get
            {
                return _GW2Path;
            }
            set
            {
                if (_GW2Path != value)
                {
                    _GW2Path = value;
                    OnSettingsChanged();
                }
            }
        }
        
        private static string _GW2DatPath;
        public static string GW2DatPath
        {
            get
            {
                return _GW2DatPath;
            }
            set
            {
                if (_GW2DatPath != value)
                {
                    _GW2DatPath = value;
                    OnSettingsChanged();
                }
            }
        }
        
        private static string _UpdatesOutputFormat;
        public static string UpdatesOutputFormat
        {
            get
            {
                return _UpdatesOutputFormat;
            }
            set
            {
                if (_UpdatesOutputFormat != value)
                {
                    _UpdatesOutputFormat = value;
                    OnSettingsChanged();
                }
            }
        }

        private static bool _UpdatesExportSubstituteHeaders;
        public static bool UpdatesExportSubstituteHeaders
        {
            get
            {
                return _UpdatesExportSubstituteHeaders;
            }
            set
            {
                if (_UpdatesExportSubstituteHeaders != value)
                {
                    _UpdatesExportSubstituteHeaders = value;
                    OnSettingsChanged();
                }
            }
        }

        private static bool _UpdatesExportIncludeExisting;
        public static bool UpdatesExportIncludeExisting
        {
            get
            {
                return _UpdatesExportIncludeExisting;
            }
            set
            {
                if (_UpdatesExportIncludeExisting != value)
                {
                    _UpdatesExportIncludeExisting = value;
                    OnSettingsChanged();
                }
            }
        }
        
        private static Panels _InitialPanel;
        public static Panels InitialPanel
        {
            get
            {
                return _InitialPanel;
            }
            set
            {
                if (_InitialPanel != value)
                {
                    _InitialPanel = value;
                    OnSettingsChanged();
                }
            }
        }
        
        private static bool _ManifestsEnglish;
        public static bool ManifestsEnglish
        {
            get
            {
                return _ManifestsEnglish;
            }
            set
            {
                if (_ManifestsEnglish != value)
                {
                    _ManifestsEnglish = value;
                    OnSettingsChanged();
                }
            }
        }
        
        private static bool _ManifestsFrench;
        public static bool ManifestsFrench
        {
            get
            {
                return _ManifestsFrench;
            }
            set
            {
                if (_ManifestsFrench != value)
                {
                    _ManifestsFrench = value;
                    OnSettingsChanged();
                }
            }
        }
        
        private static bool _ManifestsGerman;
        public static bool ManifestsGerman
        {
            get
            {
                return _ManifestsGerman;
            }
            set
            {
                if (_ManifestsGerman != value)
                {
                    _ManifestsGerman = value;
                    OnSettingsChanged();
                }
            }
        }
        
        private static bool _ManifestsChinese;
        public static bool ManifestsChinese
        {
            get
            {
                return _ManifestsChinese;
            }
            set
            {
                if (_ManifestsChinese != value)
                {
                    _ManifestsChinese = value;
                    OnSettingsChanged();
                }
            }
        }
        
        private static bool _Manifests32;
        public static bool Manifests32
        {
            get
            {
                return _Manifests32;
            }
            set
            {
                if (_Manifests32 != value)
                {
                    _Manifests32 = value;
                    OnSettingsChanged();
                }
            }
        }
        
        private static bool _Manifests64;
        public static bool Manifests64
        {
            get
            {
                return _Manifests64;
            }
            set
            {
                if (_Manifests64 != value)
                {
                    _Manifests64 = value;
                    OnSettingsChanged();
                }
            }
        }
        
        private static bool _ManifestsOSX;
        public static bool ManifestsOSX
        {
            get
            {
                return _ManifestsOSX;
            }
            set
            {
                if (_ManifestsOSX != value)
                {
                    _ManifestsOSX = value;
                    OnSettingsChanged();
                }
            }
        }
    }
}
