using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Configuration;

using Umbraco.Core.IO;
using Umbraco.Core.Logging;

using umbraco.BusinessLogic; 

namespace jumps.umbraco.usync
{
    public class uSyncSettings {

        private static string _settingfile = "usyncSettings.config"; 
        private static uSyncSettingsSection _settings ; 

        static uSyncSettings()
        {
            try
            {
                var configFile = IOHelper.MapPath(string.Format("~/config/{0}", _settingfile));
                if (!System.IO.File.Exists(configFile))
                    throw new Exception(string.Format("Cannot find uSync Config file: /config/{0}", _settingfile));

                ExeConfigurationFileMap fileMap = new ExeConfigurationFileMap();
                fileMap.ExeConfigFilename = configFile;

                // load the settings file
                Configuration config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);

                _settings = (uSyncSettingsSection)config.GetSection("usync");
            }
            catch (Exception ex)
            {
                LogHelper.Info<uSyncSettings>("Error loading settings file {0}", () => ex.ToString());
                throw ex;
            }
        }

        public static void Save()
        {
            LogHelper.Info<uSyncSettings>("Saving Settings R:{0} W:{1} A:{2} Wch:{3}", () => _settings.Read, ()=> _settings.Write, ()=> _settings.Attach, ()=> _settings.WatchFolder);

            if (_settings != null)
                _settings.CurrentConfiguration.Save(ConfigurationSaveMode.Full);
        }

        public static bool Versions {
            get { return _settings.Versions; }
        }

        public static bool Write
        {
            get { return _settings.Write  ; }
            set { _settings.Write = value; }
        }

        public static bool Read
        {
            get { return _settings.Read ; }
            set { _settings.Read = value; }
        }

        public static bool Attach
        {
            get { return _settings.Attach ; }
            set { _settings.Attach = value; }
        }

        public static string Folder
        {
            get { return _settings.Folder ; }
        }

        public static string Archive
        {
            get { return _settings.Archive ; }
        }


        public static bool ItemRestore
        {
            get { return _settings.ItemRestore; }
        }

        public static bool FullRestore
        {
            get { return _settings.FullRestore; }
        }

        public static string BackupFolder
        {
            get { return _settings.BackupFolder; }
        }

        public static bool Preserve
        {
            get { return _settings.Preserve; }
        }

        public static bool WatchFolder
        {
            get { return _settings.WatchFolder; }
            set { _settings.WatchFolder = value; }
        }

        public static string[] PreservedPreValueDataTypes
        {
            get { return _settings.PreservedPreValues.GetAll(); }
        }

        public static string[] MatchedPreValueDataTypes
        {
            get { return _settings.MatchPreValues.GetAll(); }
        }

        public static uSyncMappedDataTypes MappedDataTypes
        {
            get { return _settings.MappedDataTypes;  }
        }
        
        public static uSyncElements Elements
        {
            get { return _settings.Elements; }
        }

        public static uSyncReporterSettings Reporter
        {
            get { return _settings.ReporterSettings; }
        }

        public static uSyncDocTypeSettings docTypeSettings
        {
            get { return _settings.DocTypeSettings; }
        }
    }
  
}
