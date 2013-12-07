using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

using Umbraco.Core.Logging;
using System.Timers;

namespace jumps.umbraco.usync
{
    // listens for the file changes in the uSync folder
    // if something changes, waits 3secs and if nothing
    // else changes, forces a sync ? 
    public class SyncFileWatcher
    {
        private static FileSystemWatcher watcher;
        private static Timer _notificationTimer; 
        private static int _lockCount = 0 ;

        public static void Init(string path)
        {
            watcher = new FileSystemWatcher();
            watcher.Path = path;
            watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
                | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            watcher.IncludeSubdirectories = true; 

            watcher.Filter = "*.config";

            watcher.Changed += new FileSystemEventHandler(OnChanged);
            watcher.Created += new FileSystemEventHandler(OnChanged);
            watcher.Deleted += new FileSystemEventHandler(OnChanged);
            watcher.Renamed += new RenamedEventHandler(OnRenamed);

            _notificationTimer = new Timer(8128); // wait a perfect amount of time (8 seconds)
            _notificationTimer.Elapsed += _notificationTimer_Elapsed;

        }

        static void _notificationTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Pause();
            LogHelper.Info<SyncFileWatcher>("Notification fired, call sync");
            
            uSync u = new uSync();
            u.ReadAllFromDisk();
            LogHelper.Info<SyncFileWatcher>("Sync Complete");

            Start();
            
        }

        static void OnChanged(object sender, FileSystemEventArgs e)
        {
            LogHelper.Info<SyncFileWatcher>("Change Detected {0} {1}", () => e.ChangeType.ToString(), () => e.FullPath);
            // needs a timer... (we wait n seconds, then fire our do something, then when loads of files are coping, it 
            // will wiat till they are done and start something
            if (_notificationTimer != null)
            {
                _notificationTimer.Stop();
                _notificationTimer.Start();
            }


        }

        static void OnRenamed(object source, RenamedEventArgs e)
        {
            LogHelper.Info<SyncFileWatcher>("Rename Detected {0} {1}", () => e.OldName, () => e.FullPath);
        }


        public static void Start()
        {
            if (watcher != null)
            {
                if (_lockCount > 0)
                {
                    System.Threading.Interlocked.Decrement(ref _lockCount);
                }

                LogHelper.Debug<SyncFileWatcher>("Watcher Lock {0}", ()=> _lockCount); 

                if (_lockCount <= 0)
                {
                    LogHelper.Debug<SyncFileWatcher>("Start");
                    watcher.EnableRaisingEvents = true;
                }
            }
            
            
        }

        public static void Pause()
        {
            if (watcher != null)
            {
                System.Threading.Interlocked.Increment(ref _lockCount);
                LogHelper.Debug<SyncFileWatcher>("Watcher Lock {0}", () => _lockCount);


                if (watcher.EnableRaisingEvents)
                {
                    LogHelper.Debug<SyncFileWatcher>("Pause");
                    watcher.EnableRaisingEvents = false;
                }
            }
        }

    }
}
