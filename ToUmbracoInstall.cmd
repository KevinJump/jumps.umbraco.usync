@ECHO OFF

REM takes a file from the project and puts it in
REM the umbraco website of your choice
REM
REM separated from post build so you don't need to
REM update your solution file everytime. 

REM
REM Update umbraco path to a site you have locally
REM and don't commit this file back.
REM


SET UmbracoPath=C:\Users\VV\Documents\tfs\trunk\Application\Brokers.Website.Umbraco
ECHO Copying File %1 to "%UmbracoPath%\%2"
XCOPY %1 "%UmbracoPath%\%2" /y /i /q
	


