using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;

using System.Diagnostics;

namespace jumps.umbraco.usync
{
    /// <summary>
    ///  WebAPI Controller, allows you to fire things to uSync via webservices.
    /// </summary>
    /// 
    [PluginController("Jumoo")]
    public class uSyncController : UmbracoApiController
    {
        [HttpGet]
        public string Hello()
        {
            return "Hello from uSync - how's you day?";
        }

        [HttpGet]
        public string ReadAll()
        {
            LogHelper.Info<uSyncController>("SyncFromDiskCalled");
            Stopwatch sw = Stopwatch.StartNew(); 

            uSync u = new uSync();
            u.ReadAllFromDisk(); 

            sw.Stop() ;
            return string.Format("Read the uSync files from disk in {0} milliseconds", sw.ElapsedMilliseconds);
        }

        [HttpGet]
        public string WriteAll()
        {
            LogHelper.Info<uSyncController>("WriteAll - To Disk");
            Stopwatch sw = Stopwatch.StartNew();

            uSync u = new uSync();
            u.SaveAllToDisk();

            sw.Stop();
            return string.Format("Wrote everything to disk in {0} milliseconds", sw.ElapsedMilliseconds);
        }

        [HttpPost]
        public string ImportDocType(System.Xml.Linq.XElement element)
        {
            return "not implimented .... yet"; 
        }
    }
}
