jumps.umbraco.usync
===================

Syncing tool for umbraco to read and write the database elements to disk.

Objective
---------
the aim of uSync is to make disk based version control of umbraco possible. 

Baqckground
-----------
Umbraco has a lot of settings stored on disk, and a lot stored in the database. you can version control the
files quite eaisly but the database changes are a pain. 

uSync attaches itself to the save events inside umbraco and writes out files containing the settings db settings
for key things when they are saved. 

for example, saving a document type with uSync installed will result in a .xml file been written to the uSync folder
of your umbraco installation. 

when the web site is restarted uSync will read from the disk and add any xml files it finds back into the database

in theory you could 

1. build a new umbraco install
3. add the uSync folder and other umbraco files (i.e stylesheets) from another install
2. add the usync.dll
4. whach uSync build the umbraco settings from disk. 

Status
======
uSync is becoming more and more stable. 

* on the Umbraco 4.11.x tree it's quite solid. 
* On Umbraco6 there have been some changes but uSync appears to work well from 6.0.4+ 


