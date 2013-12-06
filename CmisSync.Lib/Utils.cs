﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using System.IO;

using System.Security;
using System.Security.Permissions;

using System.Text.RegularExpressions;
//#if __MonoCS__
//using Mono.Unix.Native;
//#endif

namespace CmisSync.Lib
{
    /// <summary>
    /// Static methods that are useful in the context of synchronization.
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Log.
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Utils));


        /// <summary>
        /// Check whether the current user has write permission to the specified path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool HasWritePermissionOnDir(string path)
        {
            var writeAllow = false;
            var writeDeny = false;
            try
            {
                var accessControlList = Directory.GetAccessControl(path);
                if (accessControlList == null)
                    return false;
                var accessRules = accessControlList.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
                if (accessRules == null)
                    return false;

                foreach (System.Security.AccessControl.FileSystemAccessRule rule in accessRules)
                {
                    if ((System.Security.AccessControl.FileSystemRights.Write & rule.FileSystemRights)
                            != System.Security.AccessControl.FileSystemRights.Write)
                    {
                        continue;
                    }
                    if (rule.AccessControlType == System.Security.AccessControl.AccessControlType.Allow)
                    {
                        writeAllow = true;
                    }
                    else if (rule.AccessControlType == System.Security.AccessControl.AccessControlType.Deny)
                    {
                        writeDeny = true;
                    }
                }
            }
            catch (System.PlatformNotSupportedException)
            {
//#if __MonoCS__
//                writeAllow = (0 == Syscall.access(path, AccessModes.W_OK));
//#endif
            }
            catch(System.UnauthorizedAccessException) {
                var permission = new FileIOPermission(FileIOPermissionAccess.Write, path);
                var permissionSet = new PermissionSet(PermissionState.None);
                permissionSet.AddPermission(permission);
                if (permissionSet.IsSubsetOf(AppDomain.CurrentDomain.PermissionSet))
                {
                    return true;
                }
                else
                    return false;
            }

            return writeAllow && !writeDeny;
        }


        /// <summary>
        /// <para>Creates a log-string from the Exception.</para>
        /// <para>The result includes the stacktrace, innerexception et cetera, separated by <seealso cref="Environment.NewLine"/>.</para>
        /// <para>Code from http://www.extensionmethod.net/csharp/exception/tologstring</para>
        /// </summary>
        /// <param name="ex">The exception to create the string from.</param>
        /// <param name="additionalMessage">Additional message to place at the top of the string, maybe be empty or null.</param>
        /// <returns></returns>
        public static string ToLogString(this Exception ex)
        {
            StringBuilder msg = new StringBuilder();
 
            if (ex != null)
            {
                try
                {
                    string newline = Environment.NewLine;

                    Exception orgEx = ex;
 
                    msg.Append("Exception:");
        
                    msg.Append(newline);
                    while (orgEx != null)
                    {
                        msg.Append(orgEx.Message);
                        msg.Append(newline);
                        orgEx = orgEx.InnerException;
                    }
 
                    if (ex.Data != null)
                    {
                        foreach (object i in ex.Data)
                        {
                            msg.Append("Data :");
                            msg.Append(i.ToString());
                            msg.Append(newline);
                        }
                    }
 
                    if (ex.StackTrace != null)
                    {
                        msg.Append("StackTrace:");
                        msg.Append(newline);
                        msg.Append(ex.StackTrace);
                        msg.Append(newline);
                    }
 
                    if (ex.Source != null)
                    {
                        msg.Append("Source:");
                        msg.Append(newline);
                        msg.Append(ex.Source);
                        msg.Append(newline);
                    }
 
                    if (ex.TargetSite != null)
                    {
                        msg.Append("TargetSite:");
                        msg.Append(newline);
                        msg.Append(ex.TargetSite.ToString());
                        msg.Append(newline);
                    }
 
                    Exception baseException = ex.GetBaseException();
                    if (baseException != null)
                    {
                        msg.Append("BaseException:");
                        msg.Append(newline);
                        msg.Append(ex.GetBaseException());
                    }
                }
                finally
                {
                }
            }
            return msg.ToString();
        }

        /// <summary>
        /// Extensions of files that must be excluded from synchronization.
        /// </summary>
        private static HashSet<String> ignoredExtensions = new HashSet<String>{
            ".autosave", // Various autosaving apps
            ".~lock", // LibreOffice
            ".part", ".crdownload", // Firefox and Chromium temporary download files
            ".un~", ".swp", ".swo", // vi(m)
            ".tmp", // Microsoft Office
            ".sync", // CmisSync download
            ".cmissync" // CmisSync database
        };


        /// <summary>
        /// Check whether the file is worth syncing or not.
        /// Files that are not worth syncing include temp files, locks, etc.
        /// </summary>
        public static Boolean WorthSyncing(string filename)
        {
            if (null == filename)
            {
                return false;
            }

            if(IsInvalidFileName(filename))
                return false;

            // TODO: Consider these ones as well:
            //    ".~lock.*", // LibreOffice
            //    ".*.sw[a-z]", // vi(m)
            //    "*(Autosaved).graffle", // Omnigraffle

            // "*~", // gedit and emacs
            if(filename.EndsWith("~"))
            {
                Logger.Debug("Unworth syncing: " + filename);
                return false;
            }

            // Ignore meta data stores of MacOS
            if (filename == ".DS_Store")
            {
                Logger.Debug("Unworth syncing MacOS meta data file .DS_Store");
                return false;
            }
            filename = filename.ToLower();

            if (ignoredExtensions.Contains(Path.GetExtension(filename))
                || filename[0] == '~' // Microsoft Office temporary files start with ~
                || filename[0] == '.' && filename[1] == '_') // Mac OS X files starting with ._
            {
                Logger.Debug("Unworth syncing: " + filename);
                return false;
            }

            //Logger.Info("SynchronizedFolder | Worth syncing:" + filename);
            return true;
        }

        /// <summary>
        /// Determines whether this instance is valid ISO-8859-1 specified input.
        /// </summary>
        /// <returns>
        /// <c>true</c> if this instance is valid ISO-8859-1 specified input; otherwise, <c>false</c>.
        /// </returns>
        /// <param name='input'>
        /// If set to <c>true</c> input.
        /// </param>
        public static bool IsValidISO88591(string input)
        {
            byte[] bytes = Encoding.GetEncoding(28591).GetBytes(input);
            String result = Encoding.GetEncoding(28591).GetString(bytes);
            return String.Equals(input, result);
        }


        /// <summary>
        /// Check whether a file name is valid or not.
        /// </summary>
        public static bool IsInvalidFileName(string name)
        {
            bool ret = invalidFileNameRegex.IsMatch(name);
            if (ret)
            {
                Logger.Debug(String.Format("The given file name {0} contains invalid patterns", name));
                return ret;
            }
            ret = !IsValidISO88591(name);
            if (ret)
            {
                Logger.Debug(String.Format("The given file name {0} contains invalid characters", name));
            }
            return ret;
        }


        /// <summary>
        /// Regular expression to check whether a file name is valid or not.
        /// </summary>
        private static Regex invalidFileNameRegex = new Regex(
            "[" + Regex.Escape(new string(Path.GetInvalidFileNameChars())+"\"?:/\\|<>*") + "]");


        /// <summary>
        /// Check whether a folder name is valid or not.
        /// </summary>
        public static bool IsInvalidFolderName(string name)
        {
            bool ret = invalidFolderNameRegex.IsMatch(name);
            if (ret)
            {
                Logger.Debug(String.Format("The given directory name {0} contains invalid patterns", name));
                return ret;
            }
            ret = !IsValidISO88591(name);
            if (ret)
            {
                Logger.Debug(String.Format("The given directory name {0} contains invalid characters", name));
            }
            return ret;
        }


        /// <summary>
        /// Regular expression to check whether a filename is valid or not.
        /// </summary>
        private static Regex invalidFolderNameRegex = new Regex(
            "[" + Regex.Escape(new string(Path.GetInvalidPathChars())+"\"?:/\\|<>*") + "]");


        /// <summary>
        /// Find an available name (potentially suffixed) for this file.
        /// For instance:
        /// - if /dir/file does not exist, return the same path
        /// - if /dir/file exists, return /dir/file (1)
        /// - if /dir/file (1) also exists, return /dir/file (2)
        /// - etc
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string SuffixIfExists(String path)
        {
            if (!File.Exists(path))
            {
                return path;
            }
            else
            {
                int index = 1;
                do
                {
                    string ret = path + " (" + index.ToString() + ")";
                    if (!File.Exists(ret))
                    {
                        return ret;
                    }
                    index++;
                }
                while (true);
            }
        }

        /// <summary>
        /// Format a file size nicely.
        /// Example: 1048576 becomes "1 MB"
        /// </summary>
        public static string FormatSize(double byteCount)
        {
            if (byteCount >= 1099511627776)
                return String.Format("{0:##.##} TB", Math.Round(byteCount / 1099511627776, 1));
            else if (byteCount >= 1073741824)
                return String.Format("{0:##.##} GB", Math.Round(byteCount / 1073741824, 1));
            else if (byteCount >= 1048576)
                return String.Format("{0:##.##} MB", Math.Round(byteCount / 1048576, 0));
            else if (byteCount >= 1024)
                return String.Format("{0:##.##} KB", Math.Round(byteCount / 1024, 0));
            else
                return byteCount.ToString() + " bytes";
        }

        /// <summary>
        /// Format a file size nicely.
        /// Example: 1048576 becomes "1 MB"
        /// </summary>
        public static string FormatSize(long byteCount)
        {
            return FormatSize((double) byteCount);
        }

        
        /// <summary>
        /// Whether a file or directory is a symbolic link.
        /// </summary>
        public static bool IsSymlink(string path)
        {
            FileInfo fileinfo = new FileInfo(path);
            if(fileinfo.Exists)
                return IsSymlink(fileinfo);
            DirectoryInfo dirinfo = new DirectoryInfo(path);
            if(dirinfo.Exists)
                return IsSymlink(dirinfo);
            return false;
        }

        /// <summary>
        /// Determines whether this instance is a symlink the specified FileSystemInfo.
        /// </summary>
        /// <returns>
        /// <c>true</c> if this instance is a symlink the specified fsi; otherwise, <c>false</c>.
        /// </returns>
        /// <param name='fsi'>
        /// If set to <c>true</c> fsi.
        /// </param>
        public static bool IsSymlink(FileSystemInfo fsi)
        {
            return ((fsi.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint);
        }
    }
}
