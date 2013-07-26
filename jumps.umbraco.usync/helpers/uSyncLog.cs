using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if UMBRACO6
using Umbraco.Core.Logging ;
#else
using umbraco.BusinessLogic;
#endif

namespace jumps.umbraco.usync.helpers
{
    /// <summary>
    ///  helper logging class. doing it like this to work across versions 
    ///  4/6 log diffrently)
    /// </summary>
    public class uSyncLog
    {
        public static void InfoLog(string message, params object[] args)
        {
#if UMBRACO6
            // debug logging, needs to be turned on in Log
            LogHelper.Info(typeof(uSync), string.Format(message, args));
#else
            Log.Add(LogTypes.System, 0, string.Format(message, args));
#endif
        }


        public static void DebugLog(string message, params object[] args)
        {
            
#if UMBRACO6
            // debug logging, needs to be turned on in Log
            LogHelper.Debug(typeof(uSync), string.Format(message, args));
#else
            Log.Add(LogTypes.Debug, 0, string.Format(message, args));
           
#endif
        }

        public static void ErrorLog(Exception ex, string message, params object[] args )
        {
#if UMBRACO6
            LogHelper.Error(typeof(uSync), string.Format(message, args), ex);
#else
            Log.Add(LogTypes.Error, 0, string.Format(message, args));
#endif
        }
    }
}
