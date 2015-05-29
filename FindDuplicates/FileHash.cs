﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FindDuplicates
{
    class HashedFile : IEquatable<HashedFile>
    {
        byte[] hash;
        long fileLength;
        int hashCode;
        string path;


        static HashAlgorithm hasher = HashAlgorithm.Create("SHA256");

        public HashedFile(string path)
        {
            // start with a weak hash code for speed.
            fileLength = new FileInfo(path).Length;
            hashCode = (int)fileLength;
            this.path = path;
        }

        public long FileLength { get { return this.fileLength; } }

        static byte[] hashBuffer = null;

        public void SetSha1PrefixHash(int prefixLength)
        {
            if (hashBuffer == null || hashBuffer.Length != prefixLength)
            {
                hashBuffer = new byte[prefixLength];
            }
            hashCode = 0;
            hash = ComputeSha1Hash(this.path);
            foreach (byte b in hash)
            {
                hashCode ^= ~b;
                hashCode <<= 1;
            }
        }

        private byte[] ComputeSha1Hash(string file)
        {
            using (Stream fs = new FileStream(this.path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                int len = fs.Read(hashBuffer, 0, hashBuffer.Length);
                return hasher.ComputeHash(hashBuffer, 0, len);
            }
        }

        public HashedFile(string path, int hashCode)
        {
            this.path = path;
            this.hashCode = hashCode;
        }

        public string Path { get { return this.path; } }

        public override int GetHashCode()
        {
            return hashCode;
        }

        public override bool Equals(object obj)
        {
            HashedFile other = obj as HashedFile;
            if (other == null)
            {
                return false;
            }
            return this.HashEquals(other);
        }

        public bool Equals(HashedFile other)
        {
            return this.HashEquals(other);
        }


        internal bool HashEquals(HashedFile other)
        {
            if (hashCode != other.hashCode)
            {
                return false;
            }

            if (hash == null)
            {
                return true;
            }

            for (int i = 0; i < hash.Length; i++)
            {
                if (hash[i] != other.hash[i])
                {
                    return false;
                }
            }
            return true;
        }

        public bool DeepEquals(HashedFile other)
        {
            using (Stream fs = new FileStream(this.path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                using (Stream fs2 = new FileStream(other.path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return StreamEquals(fs, fs2);
                }
            }
        }

        static byte[] buffer1 = new byte[65536];
        static byte[] buffer2 = new byte[65536];

        static bool StreamEquals(Stream s1, Stream s2)
        {
            while (true)
            {
                int read = s1.Read(buffer1, 0, buffer1.Length);
                int read2 = s2.Read(buffer2, 0, buffer2.Length);
                if (read != read2)
                {
                    return false;
                }
                if (read == 0)
                {
                    break;
                }
                for (int i = 0; i < read; i++)
                {
                    if (buffer1[i] != buffer2[i])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

    }
}
