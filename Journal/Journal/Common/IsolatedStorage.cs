﻿// Copyright (c) 2010 Microsoft Corporation.  All rights reserved.
//
//
// Use of this source code is subject to the terms of the Microsoft
// license agreement under which you licensed this source code.
// If you did not accept the terms of the license agreement,
// you are not authorized to use this source code.
// For the terms of the license, please see the license agreement
// signed by you and Microsoft.
// THE SOURCE CODE IS PROVIDED "AS IS", WITH NO WARRANTIES OR INDEMNITIES.
//
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Microsoft.Utilities
{

    /// <summary>
    /// Isolated storage file helper class
    /// </summary>
    /// <typeparam name="T">Data type to serialize/deserialize</typeparam>
    public class IsolatedStorage<T>
    {
        public IsolatedStorage()
        {
        }

        /// <summary>
        /// Loads data from a file asynchronously.
        /// </summary>
        /// <param name="fileName">Name of the file to read.</param>
        /// <returns>Deserialized data object</returns>
        public static async Task<T> LoadFromFileAsync(string fileName)
        {
            T loadedFile = default(T);

            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;

                string fullPath = System.IO.Path.Combine(localFolder.Path, fileName);

                StorageFile storageFile = null;
                try
                {
                    storageFile = await StorageFile.GetFileFromPathAsync(fullPath);
                }
                catch
                {
                    // doesn't exist?
                }
                return await LoadFromFileAsync(storageFile);
            }
            catch
            {
                // silently rebuild data file if it got corrupted.
            }
            return loadedFile;
        }
        
        /// <summary>
        /// Loads data from a file asynchronously.
        /// </summary>
        /// <param name="storageFile">The file to load.</param>
        /// <returns>Deserialized data object</returns>
        public static async Task<T> LoadFromFileAsync(StorageFile storageFile)
        {
            T loadedFile = default(T);

            try
            {
                if (storageFile != null)
                {
                    using (Stream myFileStream = await storageFile.OpenStreamForReadAsync())
                    {
                        // Call the Deserialize method and cast to the object type.
                        return LoadFromStream(myFileStream);
                    }
                }
            }
            catch
            {
                // silently rebuild data file if it got corrupted.
            }
            return loadedFile;
        }

        public static T LoadFromStream(Stream s)
        {
            // Call the Deserialize method and cast to the object type.
            XmlSerializer mySerializer = new XmlSerializer(typeof(T));
            return (T)mySerializer.Deserialize(s);
        }

        /// <summary>
        /// Saves data to a file.
        /// </summary>
        /// <param name="fileName">Name of the file to write to</param>
        /// <param name="data">The data to save</param>
        public static async Task SaveToFileAsync(string fileName, T data)
        {
            var localFolder = ApplicationData.Current.LocalFolder;

            StorageFolder storageFolder = null;

            string fname = System.IO.Path.GetFileName(fileName);
            string subdir = System.IO.Path.GetDirectoryName(fileName);
            if (string.IsNullOrEmpty(subdir))
            {
                storageFolder = localFolder;
            }
            else
            {
                try
                {
                    storageFolder = await localFolder.GetFolderAsync(subdir);
                }
                catch
                {
                }

                if (storageFolder == null)
                {
                    // create it.
                    storageFolder = await localFolder.CreateFolderAsync(subdir, CreationCollisionOption.FailIfExists);
                }
            }
            
            try
            {
                StorageFile storageFile = await storageFolder.CreateFileAsync(fname, CreationCollisionOption.ReplaceExisting);
                await SaveToFileAsync(storageFile, data);
            }
            catch
            {
                // ???
            }
        }

        public static async Task SaveToFileAsync(StorageFile storageFile, T data)
        {

            try
            {
                using (IRandomAccessStream fileStream = await storageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    XmlSerializer mySerializer = new XmlSerializer(typeof(T));
                    mySerializer.Serialize(fileStream.AsStreamForWrite(), data);
                }
            }
            catch
            {
                // ???
            }
        }

    }
}
