using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Configuration;

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
    ///             read="true" 
    ///             write="false" 
    ///             attach="true" 
    ///             folder="~/uSync/" 
    ///             archive="~/uSync.Archive/" />
    ///     </settings>
    /// </uSync>
    /// 
    /// </summary>
    public class uSyncSettings : ConfigurationSection
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
    }
}
