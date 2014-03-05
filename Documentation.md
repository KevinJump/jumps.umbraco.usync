Usync - 2.2 
===========

usync is database to disk and back syncing tool for umbraco, 

usync takes the bits of umbraco that are stored in the database
and writes them out to disk - it also reads the files from disk
and puts them back into the database. 

It will read/write:
- Document Types 
- Media Types
- Data Types
- Dictionary Items
- Languages 
- Templates
- Stylesheets

* uSync doesn't do content *

Ways to use usync
=================
*Developer Mode* (read=true, attach=true, write=false)
------------------------------------------------------
The default configuration for usync is setup for shared development.
all changes are saved to disk and everytime you start your website up
any changes from disk are put back into the site. 

* Server Mode* (read=true, attach=false, write=false)
-----------------------------------------------------
In Server mode, you would only want the server to read in the changes but not 
write them backout - this might be how you would deploy changes to a live site.

* Deployermode * (read=false, write=true, attach=true)
------------------------------------------------------
Developer mode does this well enough but if you wanted the belt and braces approach
to getting all your changes then setting your site to write everything at startup
ensures this.  


Configuration
-------------
uSync has a number of configuration options, by default it is setup for a developer
environment, writing all files and changes out to disk, and reading changes back in at
start-up. Depending on how you want to use it you can turn these features on and off.

Configuration is controlled by usyncsettings.config which is stored in your umbraco
 /config folder. 
 
Read : [Default: true]
-----------------------
On start-up read the files from the disk, and apply and changes to the umbraco install,
read is triggered every time the web application starts, so when you restart the server
or change one of the config files, usync will read the disk and apply changes

Write: [Default: false]
------------------------
On start-up usync can write all the umbraco settings out to disk. if there is no usync
folder in the site then this happens regardless of this setting - usally you don't need
to set this to true.

Attach: [Default: true]
------------------------
When true usync attaches to the save/delete/create events, and when you save/del/change
anything inside umbraco usync writes those changes to disk. 

Folder [Default: ~/usync/]
-------------------------------
where usync saves its files

Versions: [Default: true]
--------------------------
whenever usync saves a change to the disk, if their is already a file present it will
save that old file in the archive folder with a timestap - it's a poor mans version
history

Archive : [Default: ~/usync.archive/]
-------------------------------------
where to save the versions, this is outside the usync folder as you don't need to
copy this between versions when syncing them.

Preserve: [True]
-----------------
This is a legacy setting to do with how usync tried to keep property values inside
datatypes in sync - in version 7. usync is using the new api - so these values are
not needed.

WatchFolder: [Default: false]
------------------------------
usync watches for changes inside the usync folder, if a usync file is saved in the
folder then usync will take the changes and apply them to the umbraco installation.
this means you don't have to restart the application to apply changes. 

(usync waits 8 seconds from the file change before applying the changes this is to
ensure all file operations are finished before it starts)


Elements section
----------------
use this section of the config to turn elements of usync on or off. 





