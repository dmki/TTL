# TTL
Time To Leave - temporary file cleanup utility
## About
TTL is a command line tool that cleans up old files in folders of your choice. You can read about how it does it here: https://kirsanov.net/post/Handling-Temporary-Files-Best-Practice.aspx
The usual usage scenarios are:
* Clean up temporary directories, while having directories with different retention periods, e.g. you can clean up Downloads directory from files that are 90 days or older, while user Temp directory - for files and empty directories that are over 60 days old. You can even have directories where files will live for a day.
* Clean up game save files - you can delete old files, while keeping X latest files, so that you wouldn't lose all files, no matter how old they are.
* Clean up server logs - depending on free disk space, number of files, age etc

Files can be deleted normally, securely (i.e. you probably couldn't restore them, unless there is a version control) and to recycle bin.

## System Requirements
.NET Framework 4.5 on any Windows that supports it

## Installation
Release section contains link to EXE and 7z files. Use EXE to install TTL on your machine - this will copy additional scripts and create scheduled task (which will be disabled until you enable it). The 7z file contains only main ttl.exe file.
