jumps.umbraco.usync
===================

Syncing tool for umbraco to read and write the database elements to disk.

Objective
---------
The aim of uSync is to make disk based version control of Umbraco possible. 

Background
-----------
Umbraco has a lot of settings stored on disk, and a lot stored in the database. You can version control the
files quite easily but the database changes are a pain. 

uSync attaches itself to the save events inside Umbraco and writes out files containing the settings db settings
for key things when they are saved. 

For example, saving a document type with uSync installed will result in a .xml file been written to the uSync folder
of your Umbraco installation. 

When the web site is restarted, uSync will read from the disk and add any xml files it finds back into the database.

In theory, you could: 

1. Build a new Umbraco install
3. Add the uSync folder and other umbraco files (i.e stylesheets) from another install
2. Add the usync.dll
4. Watch uSync build the umbraco settings from disk. 

Status
======
uSync is becoming more and more stable. 

* On the Umbraco 4.11.x tree, it's quite solid. 
* On Umbraco 6, there have been some changes but uSync appears to work well from 6.0.6/6.1.+


