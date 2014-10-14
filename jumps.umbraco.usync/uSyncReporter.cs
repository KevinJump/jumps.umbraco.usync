using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using jumps.umbraco.usync.helpers;

using Umbraco.Core.Logging;

namespace jumps.umbraco.usync
{
    /// <summary>
    ///  handles the reporting of usync stuff.
    /// </summary>
    public class uSyncReporter
    {
        public uSyncReporter()
        {
        }

        // will email. - for now just logs.
        public void ReportChanges(List<ChangeItem> changes)
        {
            int changeCount = 0;
            foreach(var change in changes)
            {
                if (change.changeType != ChangeType.NoChange)
                {
                    changeCount++;

                }
                LogHelper.Info<uSyncReporter>("Change: {0} {1} {2} {3}",
                    () => change.changeType, () => change.itemType, () => change.name, () => change.message);

            }

            LogHelper.Info<uSyncReporter>("{0} Items processed {1} changes made", () => changes.Count(), () => changeCount);

        }

    }
}
