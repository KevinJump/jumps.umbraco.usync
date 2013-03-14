/*
 * 
 *  We don't do this anymore, we moved to our own config file, so 
 *  we don't need to minuplate xml anymore :o) 
 * 
 * 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection; 

using umbraco.cms.businesslogic.packager.standardPackageActions;
using umbraco.interfaces;
using umbraco.BusinessLogic;

using System.Configuration; 
using System.Web.Configuration; 

namespace jumps.umbraco.usync
{
    class uSyncPackageActions : IPackageAction
    {

        public string Alias()
        {
            return "uSyncActions";
        }

        public bool Execute(string packageName, System.Xml.XmlNode xmlData)
        {
            var config = WebConfigurationManager.OpenWebConfiguration("~");

            string sectionName = "usync";

            if (config.Sections[sectionName] == null)
            {
                string type = "jumps.umbraco.usync.uSyncSettings";
                var assembly = Assembly.Load("jumps.umbraco.usync");

                var section = assembly.CreateInstance(type) as ConfigurationSection ;

                config.Sections.Add(sectionName,section); 
                section.SectionInformation.ForceSave = true ;
                config.Save() ;
            }

            return true; 

        }

        public System.Xml.XmlNode SampleXml()
        {
            var sample = "<Action runat=\"install\" undo=\"false\" alias=\"uSyncActions\" />";

            return helper.parseStringToXmlNode(sample); 
        }

        public bool Undo(string packageName, System.Xml.XmlNode xmlData)
        {
            return false; 
        }
    }
}
*/