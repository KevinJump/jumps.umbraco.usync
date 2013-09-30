using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Umbraco.Core.Logging ;


namespace jumps.umbraco.usync.helpers
{
    /// <summary>
    ///  helper logging class. doing it like this to work across versions 
    ///  4/6 log diffrently)
    /// </summary>
    public class uSyncLog
    {
        [Obsolete("use LogHelper.Info<T> from the core", false)]
        public static void InfoLog(string message, params object[] args)
        {
            LogHelper.Info(typeof(uSync), string.Format(message, args));
        }


        [Obsolete("use LogHelper.Debug<T> from the core", false)]
        public static void DebugLog(string message, params object[] args)
        {
            // debug logging, needs to be turned on in Log
            LogHelper.Debug(typeof(uSync), string.Format(message, args));
        }

        [Obsolete("use LogHelper.Error<T> from the core", false)]
        public static void ErrorLog(Exception ex, string message, params object[] args )
        {
            LogHelper.Error(typeof(uSync), string.Format(message, args), ex);
        }
    }
}
