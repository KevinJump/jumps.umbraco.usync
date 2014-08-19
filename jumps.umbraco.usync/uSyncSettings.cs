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
    /// <summary>
    /// uSync Settings - 
    /// 
    /// reads the uSync bit of the Web.Config
    /// 
    /// <uSync>
    ///     <Settings>
    ///         <add 
    ///             read="true"                     - read the uSync directory on startup
    ///             write="false"                   - write the uSync directory on statup
    ///             attach="true"                   - attach the events to save on the fly
    ///             folder="~/uSync/"               - place to put files
    ///             archive="~/uSync.Archive/"      - place to archive files
    ///             versions="true"                 - store versions at every save
    ///             />
    ///     </settings>
    /// </uSync>
    /// 
    /// </summary>
    public class uSyncSettingsSection : ConfigurationSection
    {
        [ConfigurationProperty("read", DefaultValue = "true", IsRequired = false)]
        public Boolean Read
        {
            get
            {
                return (Boolean)this["read"];
            }
            set
            {
                this["read"] = value;
            }
        }

        [ConfigurationProperty("write", DefaultValue = "false", IsRequired = false)]
        public Boolean Write
        {
            get
            {
                return (Boolean)this["write"];
            }
            set
            {
                this["write"] = value;
            }
        }

        [ConfigurationProperty("attach", DefaultValue = "true", IsRequired = false)]
        public Boolean Attach
        {
            get
            {
                return (Boolean)this["attach"];
            }
            set
            {
                this["attach"] = value;
            }
        }

        [ConfigurationProperty("folder", DefaultValue = "~/uSync/", IsRequired = false)]
        public String Folder
        {
            get
            {
                return (String)this["folder"];
            }
            set
            {
                this["folder"] = value;
            }
        }

        [ConfigurationProperty("archive", DefaultValue = "~/uSync.archive/", IsRequired = false)]
        public String Archive
        {
            get
            {
                return (String)this["archive"];
            }
            set
            {
                this["archive"] = value;
            }
        }

        [ConfigurationProperty("versions", DefaultValue = "true", IsRequired = false)]
        public Boolean Versions
        {
            get
            {
                return (Boolean)this["versions"];
            }
            set
            {
                this["versions"] = value;
            }
        }

        [ConfigurationProperty("maxVersions", DefaultValue=0, IsRequired = false)]
        public int MaxVersions {
            get 
            { 
                return (int)this["maxVersions"];
            }
            set 
            {
                this["maxVersions"] = value;
            }
        }

        [ConfigurationProperty("preserve", DefaultValue = "true", IsRequired = false)]
        public Boolean Preserve
        {
            get
            {
                return (Boolean)this["preserve"];
            }
            set
            {
                this["preserve"] = value;
            }
        }

        [ConfigurationProperty("PreservedPreValues")]
        public uSyncPreservedPreValues PreservedPreValues
        {
            get { return (uSyncPreservedPreValues)this["PreservedPreValues"]; }
        }

        [ConfigurationProperty("MatchedPreValues")]
        public uSyncPreservedPreValues MatchPreValues
        {
            get { return (uSyncPreservedPreValues)this["MatchedPreValues"]; }
        }

        [ConfigurationProperty("Elements", IsRequired=false)]
        public uSyncElements Elements
        {
            get { return (uSyncElements)this["Elements"]; }
        }

        [ConfigurationProperty("DocumentTypes", IsRequired = false)]
        public uSyncDocTypeSettings DocTypeSettings
        {
            get { return (uSyncDocTypeSettings)this["DocumentTypes"]; }
        }

        [ConfigurationProperty("DataTypes", IsRequired=false)]
        public uSyncDataTypeSettings DataTypeSettings {
            get { return (uSyncDataTypeSettings)this["DataTypes"]; }
        }

        [ConfigurationProperty("watchFolder", DefaultValue = "false", IsRequired = false)]
        public Boolean WatchFolder
        {
            get
            {
                return (Boolean)this["watchFolder"];
            }
            set
            {
                this["watchFolder"] = value;
            }
        }

        [ConfigurationProperty("dontThrowErrors", DefaultValue = "false", IsRequired = false)]
        public Boolean DontThrowErrors
        {
            get
            {
                return (Boolean)this["dontThrowErrors"];
            }
            set
            {
                this["dontThrowErrors"] = value;
            }
        }

        [ConfigurationProperty("quickUpdate", DefaultValue = "false", IsRequired = false)]
        public Boolean QuickUpdates
        {
            get
            {
                return (Boolean)this["quickUpdate"];
            }
            set
            {
                this["quickUpdate"] = value;
            }
        }

    }

    public class PreservedPreValue : ConfigurationElement 
    {
        [ConfigurationProperty("key", IsRequired=true)]
        public string Key 
        {
            get { return (string)base["key"]; }
        }

        [ConfigurationProperty("value", IsRequired=false, DefaultValue="ting")]
        public string Value 
        {
            get { return (string)base["value"]; }
        }

        internal string key {
            get { return Key ; }
        }
    }

               
    [ConfigurationCollection(typeof(PreservedPreValue), AddItemName = "add", CollectionType = ConfigurationElementCollectionType.BasicMap)]
    public class uSyncPreservedPreValues : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new PreservedPreValue(); 
        }
        
        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((PreservedPreValue)element).key;
        }

        public int IndexOf( PreservedPreValue element)
        {
            return BaseIndexOf(element) ;
        }

        public PreservedPreValue this[int index]
        {
            get { return (PreservedPreValue)BaseGet(index); }
        }

        public string[] GetAll()
        {
            
            return BaseGetAllKeys().Cast<string>().ToArray() ; 
        }

    }

    public class uSyncElements : ConfigurationElement
    {
        [ConfigurationProperty("docTypes", DefaultValue = "true", IsRequired = true)]
        public Boolean DocumentTypes
        {
            get { return (Boolean)this["docTypes"]; }
        }

        [ConfigurationProperty("mediaTypes", DefaultValue = "true", IsRequired = true)]
        public Boolean MediaTypes
        {
            get { return (Boolean)this["mediaTypes"]; }
        }

        [ConfigurationProperty("dataTypes", DefaultValue = "true", IsRequired = true)]
        public Boolean DataTypes
        {
            get { return (Boolean)this["dataTypes"]; }
        }

        [ConfigurationProperty("templates", DefaultValue = "true", IsRequired = true)]
        public Boolean Templates
        {
            get { return (Boolean)this["templates"]; }
        }

        [ConfigurationProperty("stylesheets", DefaultValue = "true", IsRequired = true)]
        public Boolean Stylesheets
        {
            get { return (Boolean)this["stylesheets"]; }
        }

        [ConfigurationProperty("macros", DefaultValue = "true", IsRequired = true)]
        public Boolean Macros
        {
            get { return (Boolean)this["macros"]; }
        }

        [ConfigurationProperty("dictionary", DefaultValue = "false", IsRequired = false)]
        public Boolean Dictionary
        {
            get { return (Boolean)this["dictionary"]; }
        }

    }

    public class uSyncDocTypeSettings : ConfigurationElement
    {
        [ConfigurationProperty("DeletePropertyValues", DefaultValue = "false", IsRequired = true)]
        public Boolean DeletePropertyValues 
        {
            get { return (Boolean)this["DeletePropertyValues"]; }
        }
    }

    public class uSyncDataTypeSettings  : ConfigurationElement
    {
        [ConfigurationProperty("ContentPreValueAliases", DefaultValue = "startNode, startNodeId", IsRequired = false)]
        public String ContentPreValueAliases
        {
            get { return (String)this["ContentPreValueAliases"]; }
        }

        [ConfigurationProperty("StyleSheetAliases", DefaultValue = "editor", IsRequired=false)]
        public String StyleSheetAliases
        {
            get { return (String)this["StyleSheetAliases"]; }
        }

        [ConfigurationProperty("WaitAndSave", DefaultValue = true, IsRequired = false)]
        public Boolean WaitAndSave
        {
            get { return (Boolean)this["WaitAndSave"]; }
        }

    }
/*
        public static List<string> PreservedPreValues
        {
            get
            {
                List<string> datalisttypes = new List<string>();
                datalisttypes.Add("f8d60f68-ec59-4974-b43b-c46eb5677985"); // ApprovedColour
                datalisttypes.Add("b4471851-82b6-4c75-afa4-39fa9c6a75e9"); // Checkbox List
                datalisttypes.Add("a74ea9c9-8e18-4d2a-8cf6-73c6206c5da6"); // dropdown 
                datalisttypes.Add("928639ed-9c73-4028-920c-1e55dbb68783"); // dropdown-multiple
                datalisttypes.Add("a52c7c1c-c330-476e-8605-d63d3b84b6a6"); // radiobox

                return datalisttypes; 
            }
        }

    }
  */   

    public class uSyncSettings {

        private static string _settingfile = "usyncSettings.config"; 
        private static uSyncSettingsSection _settings ;
        private static Configuration config;

        static uSyncSettings()
        {
            try
            {
                ExeConfigurationFileMap fileMap = new ExeConfigurationFileMap();
                fileMap.ExeConfigFilename = IOHelper.MapPath(string.Format("~/config/{0}", _settingfile));

                if (System.IO.File.Exists(fileMap.ExeConfigFilename))
                {
                    // load the settings file
                    config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);

                    if (config != null)
                    {
                        _settings = (uSyncSettingsSection)config.GetSection("usync");
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Info<uSyncSettings>("Error loading settings file {0}", () => ex.ToString());
            }
            finally
            {

                if (_settings == null)
                {
                    LogHelper.Info<uSyncSettings>("WARNING: Working with no config file");
                    _settings = new uSyncSettingsSection(); // default config - won't be savable mind?
                }
            }
        }

        public static void Save()
        {
            if ( _settings != null ) 
                _settings.CurrentConfiguration.Save(ConfigurationSaveMode.Full);
        }

        public static bool Versions {
            get { return _settings.Versions; }
        }

        public static int MaxVersions {
            get { return _settings.MaxVersions; }
        }

        public static bool Write
        {
            get { return _settings.Write  ; }
            set { _settings.Write = value; }
        }

        public static bool Read
        {
            get { return _settings.Read ; }
            set { _settings.Read = value;  }
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

        public static bool Preserve
        {
            get { return _settings.Preserve; }
        }

        public static bool WatchFolder
        {
            get { return _settings.WatchFolder; }
            set { _settings.WatchFolder = value; }
        }

        public static bool DontThrowErrors
        {
            get { return _settings.DontThrowErrors; }
            set { _settings.DontThrowErrors = value; }
        }

        public static bool QuickUpdates
        {
            get { return _settings.QuickUpdates; }
        }

        public static string[] PreservedPreValueDataTypes
        {
            get { return _settings.PreservedPreValues.GetAll(); }
        }

        public static string[] MatchedPreValueDataTypes
        {
            get { return _settings.MatchPreValues.GetAll(); }
        }
        
        public static uSyncElements Elements
        {
            get { return _settings.Elements; }
        }

        public static uSyncDocTypeSettings docTypeSettings
        {
            get { return _settings.DocTypeSettings; }
        }

        public static uSyncDataTypeSettings dataTypeSettings 
        {
            get { return _settings.DataTypeSettings; }
        }
    }
  
}
