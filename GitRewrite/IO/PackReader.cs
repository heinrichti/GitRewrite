using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using GitRewrite.Diff;
using GitRewrite.GitObjects;

namespace GitRewrite.IO
{
    public static class PackReader
    {
        public static Blob GetBlob(ObjectHash objectHash)
        {
            var result = GetGitObject(objectHash);
            if (result == null)
                return null;
            if (result.Type == GitObjectType.Blob)
                return (Blob) result;

            throw new ArgumentException(objectHash + " is not a blob.");
        }

        public static readonly object ReadPackFilesLock = new object();
        public static volatile List<(string IdxFile, string PackFile)> PackFiles;

        public static IEnumerable<(string IdxFile, string PackFile)> GetPackFiles(string repositoryPath)
        {
            if (PackFiles != null)
                return PackFiles;

            lock (ReadPackFilesLock)
            {
                if (PackFiles != null)
                    return PackFiles;

                PackFiles = Directory.GetFiles(Path.Combine(repositoryPath, "objects/pack"), "*.idx",
                    SearchOption.TopDirectoryOnly).Select(idxFile => (idxFile, idxFile.Substring(0, idxFile.Length - 3) + "pack")).ToList();
            }

            return PackFiles;
        }

        public static Commit GetCommit(string repositoryPath, ObjectHash hash)
        {
            var result = GetGitObject(hash);
            if (result == null)
                return null;
            if (result.Type == GitObjectType.Commit)
                return (Commit) result;

            throw new ArgumentException(hash + " is not a commit.");
        }

        public static GitObjectBase GetObject(ObjectHash hash)
            => GetGitObject(hash);

        public static Tree GetTree(ObjectHash hash)
        {
            var result = GetGitObject(hash);
            if (result == null)
                return null;
            if (result.Type == GitObjectType.Tree)
                return (Tree) result;

            throw new ArgumentException(hash + " is not a tree.");
        }

        private static Dictionary<ObjectHash, (MemoryMappedViewAccessor, long)> _packOffsets;

        public static void InitializePackFiles(string vcsPath)
        {
            _packOffsets = BuildPackFileDictionary(GetPackFiles(vcsPath));
        }

        private static Dictionary<ObjectHash, (MemoryMappedViewAccessor, long)>BuildPackFileDictionary(IEnumerable<(string IdxFilePath, string PackFilePath)> packFiles)
        {
            var offsets = new Dictionary<ObjectHash, (MemoryMappedViewAccessor, long)>();

            foreach (var file in packFiles)
            {
                var capacity = new FileInfo(file.PackFilePath).Length;
                var memoryMappedFile = MemoryMappedFile.CreateFromFile(file.PackFilePath, FileMode.Open,
                     null, capacity, MemoryMappedFileAccess.Read);
                var viewAccessor = memoryMappedFile.CreateViewAccessor(0, capacity, MemoryMappedFileAccess.Read);

                foreach (var offset in IdxOffsetReader.GetPackOffsets(file.IdxFilePath))
                {
                    offsets.TryAdd(new ObjectHash(offset.Hash), (viewAccessor, offset.Offset));
                }
            }

            return offsets;
        }

        private static (MemoryMappedViewAccessor ViewAccessor, long Offset) GetOffset(ObjectHash hash)
        {
            if (_packOffsets.TryGetValue(hash, out var result))
                return result;
            return (null, -1);
        }

        public static GitObjectBase GetGitObject(ObjectHash hash)
        {
            var (viewAccessor, offset) = GetOffset(hash);
            if (offset == -1)
                return null;

            var packObject = ReadPackObject(viewAccessor, offset);

            byte[] unpackedBytes;
            var type = packObject.Type;
            if (packObject.Type == 6)
            {
                var unpackedObject = RestoreDiffedObjectBytes(viewAccessor, packObject);

                unpackedBytes = unpackedObject.Bytes;
                type = unpackedObject.Type;
            }
            else if (packObject.Type == 7)
            {
                throw new NotImplementedException();
            }
            else
            {
                unpackedBytes = HashContent.Unpack(viewAccessor, packObject);
            }

            if (type == 1)
                return new Commit(hash, unpackedBytes);

            if (type == 2)
                return new Tree(hash, unpackedBytes);

            if (type == 3)
                return new Blob(hash, unpackedBytes);

            if (type == 4)
                return new Tag(hash, unpackedBytes);

            throw new NotImplementedException();
        }

        public static (int Type, byte[] Bytes) RestoreDiffedObjectBytes(MemoryMappedViewAccessor memory,
            PackObject packObject)
        {
            var packDiff = new PackDiff(memory, packObject);

            packObject = ReadPackObject(memory, packObject.Offset - packDiff.NegativeOffset);

            while (packObject.Type == 6)
            {
                // OFS_DELTA
                var targetDiff = new PackDiff(memory, packObject);
                packDiff = packDiff.Combine(targetDiff);
                packObject = ReadPackObject(memory, packObject.Offset - packDiff.NegativeOffset);
            }

            var content = HashContent.Unpack(memory, packObject, 0);
            return (packObject.Type, packDiff.Apply(content));
        }

        private static PackObject ReadPackObject(MemoryMappedViewAccessor file, long offset)
        {
            var readByte = file.ReadByte(offset);
            var bytesRead = 1;
            const byte typeMask = 0b01110000;
            var fsbSet = (readByte & 0b10000000) != 0;
            var type = (readByte & typeMask) >> 4;
            var dataSize = readByte & 0b00001111;
            var shift = 4;
            while (fsbSet)
            {
                readByte = file.ReadByte(offset + bytesRead++);
                fsbSet = (readByte & 0b10000000) != 0;
                dataSize |= (readByte & 0x7F) << shift;
                shift += 7;
            }

            return new PackObject(type, offset, bytesRead, dataSize);
        }
    }
}
