using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Umbraco.Core; 


namespace jumps.umbraco.usync
{
    /// <summary>
    /// uSync Entry point - 
    /// </summary>
    public class uSync : IApplicationEventHandler
    {
        private object _lock = new object(); 
        private bool _synced = false;

        private bool _read, _write, _attach;

        private void DoSync()
        {
            if (!_synced)
            {
                lock (_lock)
                {
                    if (!_synced)
                    {
                        // do the 'stuff' 
                        GetSettings();
                        RunSync(); 

                    }
                }
            }
        }

        private void GetSettings()
        {
            _read = _write = _attach = true; 
        }

        private void RunSync()
        {
            if (_write)
            {
                SyncMediaTypes m = new SyncMediaTypes();
                m.Write();
            }

        }


        public void OnApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            DoSync(); 
        }

        public void OnApplicationInitialized(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            // throw new NotImplementedException();
        }
        public void OnApplicationStarting(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            // throw new NotImplementedException();
        }
    }
}
