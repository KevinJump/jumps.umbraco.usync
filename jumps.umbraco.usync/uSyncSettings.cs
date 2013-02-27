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



    }
     

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
                Log.Add(LogTypes.Error, 0, string.Format("Error loading settings file {0}", ex.ToString()));
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

    }
  
}
