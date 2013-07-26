using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Configuration;

using Umbraco.Core.IO;
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

        static uSyncSettings()
        {
            try
            {
                ExeConfigurationFileMap fileMap = new ExeConfigurationFileMap();
                fileMap.ExeConfigFilename = IOHelper.MapPath(string.Format("~/config/{0}", _settingfile));

                // load the settings file
                Configuration config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
                
                _settings = (uSyncSettingsSection)config.GetSection("usync");
            }
            catch (Exception ex)
            {
                helpers.uSyncLog.ErrorLog(ex, "Error loading settings file {0}", ex.ToString());
            }
        }

        public static bool Versions {
            get { return _settings.Versions; }
        }

        public static bool Write
        {
            get { return _settings.Write  ; }
        }

        public static bool Read
        {
            get { return _settings.Read ; }
        }

        public static bool Attach
        {
            get { return _settings.Attach ; }
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
         


    }
  
}
