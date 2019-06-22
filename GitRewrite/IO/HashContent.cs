using System;
using System.Buffers;
using System.IO;
using System.IO.MemoryMappedFiles;
using GitRewrite.GitObjects;
using ZLibNet;

namespace GitRewrite.IO
{
    public static class HashContent
    {
        private const int BufferLength = 1024 * 1024;

        #region - Methoden oeffentlich -

        public static void WriteFile(string basePath, byte[] bytes, string hash)
        {
            var directoryPath = Path.Combine(basePath, $"objects/{hash.Substring(0, 2)}");
            var filePath = Path.Combine(directoryPath, $"{hash.Substring(2)}");

            if (File.Exists(filePath))
                return;

            Directory.CreateDirectory(directoryPath);

            using (var fileStream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write))
            using (var stream = new ZLibStream(fileStream, CompressionMode.Compress, false))
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        public static void WriteObject(string basePath, GitObjectBase gitObject)
        {
            var bytesWithHeader = GitObjectFactory.GetBytesWithHeader(gitObject.Type, gitObject.SerializeToBytes());
            WriteFile(basePath, bytesWithHeader, gitObject.Hash.ToString());
        }

        private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;

        public static byte[] FromFile(string basePath, string hashCode)
        {
            var filePath = Path.Combine(basePath, $"objects/{hashCode.Substring(0, 2)}/{hashCode.Substring(2)}");

            var targetBuffer = BufferPool.Rent(BufferLength);

            try
            {
                int bytesRead;
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var stream = new ZLibStream(fileStream, CompressionMode.Decompress, false))
                {
                    bytesRead = stream.Read(targetBuffer);
                    // TODO properly handle bigger blobs
                    if (bytesRead == BufferLength && !IsBlob(targetBuffer))
                        throw new Exception("Buffer too small");
                }

                return targetBuffer.AsSpan(0, bytesRead).ToArray();
            }
            finally
            {
                BufferPool.Return(targetBuffer);
            }
        }

        private static bool IsBlob(byte[] buffer) =>
            buffer[0] == 'b' && buffer[1] == 'l' && buffer[2] == 'o' && buffer[3] == 'b' && buffer[4] == ' ';
        
        public static byte[] Unpack(MemoryMappedViewAccessor fileView, PackObject packObject, int additionalOffset = 0)
        {
            var realOffset = packObject.Offset + packObject.HeaderLength + additionalOffset;
            var buffer = new byte[packObject.DataSize];

            var safeHandle = fileView.SafeMemoryMappedViewHandle;
            long size = Math.Min(packObject.DataSize + 512, (long)safeHandle.ByteLength - realOffset);

            using (var unmanagedMemoryStream = new UnmanagedMemoryStream(safeHandle, realOffset, size))
            using (var stream = new ZLibStream(unmanagedMemoryStream, CompressionMode.Decompress, true))
            {
                stream.Read(buffer, 0, packObject.DataSize);
            }

            return buffer;
        }

        #endregion
    }
}