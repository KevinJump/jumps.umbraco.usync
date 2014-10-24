using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

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

        [ConfigurationProperty("itemRestore", DefaultValue = false, IsRequired = false)]
        public Boolean ItemRestore
        {
            get
            {
                return (Boolean)this["itemRestore"]; 
            }
        }

        [ConfigurationProperty("fullRestore", DefaultValue = false, IsRequired = false)]
        public Boolean FullRestore
        {
            get
            {
                return (Boolean)this["fullRestore"];
            }
        }

        [ConfigurationProperty("backupFolder", DefaultValue = "~/App_data/TEMP/usync.backups/", IsRequired = true)]
        public String BackupFolder
        {
            get
            {
                return (String)this["backupFolder"];
            }
            set
            {
                this["backupFolder"] = value;
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

        [ConfigurationProperty("MappedDataTypes")]
        public uSyncMappedDataTypes MappedDataTypes
        {
            get { return (uSyncMappedDataTypes)this["MappedDataTypes"]; }
        }

        [ConfigurationProperty("Elements", IsRequired = false)]
        public uSyncElements Elements
        {
            get { return (uSyncElements)this["Elements"]; }
        }

        [ConfigurationProperty("DocumentTypes", IsRequired = false)]
        public uSyncDocTypeSettings DocTypeSettings
        {
            get { return (uSyncDocTypeSettings)this["DocumentTypes"]; }
        }

        [ConfigurationProperty("Reporter", IsRequired = false)]
        public uSyncReporterSettings ReporterSettings 
        {
            get { return (uSyncReporterSettings)this["Reporter"];}
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

    }

    public class PreservedPreValue : ConfigurationElement
    {
        [ConfigurationProperty("key", IsRequired = true)]
        public string Key
        {
            get { return (string)base["key"]; }
        }

        [ConfigurationProperty("value", IsRequired = false, DefaultValue = "ting")]
        public string Value
        {
            get { return (string)base["value"]; }
        }

        internal string key
        {
            get { return Key; }
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

        public int IndexOf(PreservedPreValue element)
        {
            return BaseIndexOf(element);
        }

        public PreservedPreValue this[int index]
        {
            get { return (PreservedPreValue)BaseGet(index); }
        }

        public string[] GetAll()
        {

            return BaseGetAllKeys().Cast<string>().ToArray();
        }

    }

    /* mapping types - DataTypes */
    public class MappedDataTypeSettings : ConfigurationElement
    {
        [ConfigurationProperty("key", IsRequired = true)]
        public string Key
        {
            get { return (string)base["key"]; }
        }

        [ConfigurationProperty("value", IsRequired = true)]
        public string IdObjectType
        {
            get { return (string)base["value"]; }
        }

        [ConfigurationProperty("preValueType", IsRequired = false, DefaultValue = "text")]
        public string PreValueType
        {
            get { return ((string)base["preValueType"]).ToLower(); }
        }

        [ConfigurationProperty("propertyName", IsRequired = false)]
        public string PropName
        {
            get { return (string)base["propertyName"]; }
        }

        [ConfigurationProperty("propertyPosition", IsRequired = false, DefaultValue = 0)]
        public int PropPos
        {
            get { return (int)base["propertyPosition"]; }
        }

        [ConfigurationProperty("propertySplitChar", IsRequired = false, DefaultValue = '\0')]
        public char PropSplit
        {
            get { return (char)base["propertySplitChar"]; }
        }

        [ConfigurationProperty("idRegEx", IsRequired = false, DefaultValue = @"\d{4,9}")]
        public string IdRegEx
        {
            get { return (string)base["idRegEx"]; }
        }

    }

    [ConfigurationCollection(typeof(MappedDataTypeSettings), AddItemName = "add", CollectionType = ConfigurationElementCollectionType.BasicMap)]
    public class uSyncMappedDataTypes : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new MappedDataTypeSettings();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((MappedDataTypeSettings)element).Key;
        }

        public int IndexOf(MappedDataTypeSettings element)
        {
            return BaseIndexOf(element);
        }

        public MappedDataTypeSettings this[int index]
        {
            get { return (MappedDataTypeSettings)BaseGet(index); }
        }

        public MappedDataTypeSettings this[string key]
        {
            get { return (MappedDataTypeSettings)BaseGet(key); }
        }

        public string[] GetAll()
        {

            return BaseGetAllKeys().Cast<string>().ToArray();
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

    public class uSyncReporterSettings : ConfigurationElement
    {
        [ConfigurationProperty("Email", DefaultValue = "reports@jumoo.co.uk", IsRequired = false)]
        public String Email
        {
            get { return (String)this["Email"]; }
        }

        [ConfigurationProperty("ReportChanges", DefaultValue = true, IsRequired = false)]
        public Boolean ReportChanges
        {
            get { return (Boolean)this["ReportChanges"]; }
        }

        [ConfigurationProperty("ReportNoChange", DefaultValue = false, IsRequired =false)]
        public Boolean ReportNoChange
        {
            get { return (Boolean)this["ReportNoChange"]; }
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
}
