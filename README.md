# VeeamDirsync

This application was created for a test assignment given by Veeam.

This application can be run by providing it with two to four arguments:
- Source directory path
- Destination directory path (Where the source will be replicated to)
- (optional) interval in minutes (default is 30 minutes)
- (optional) Log file path

Example usage:
VeeamDirsync D:\Important\Documents E:\Backup\Files 180 E:\Backup\log.txt

Syncing process:
- Creation of missing directories
- Comparison and moving of files if they exists in the replica, but under a different path or name
- Copying of missing files
- Removal of files from the replica without their counterpart in the source
- Removal of directories from the replica without their counterpart in the source

Fully Async
