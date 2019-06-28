using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
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
                    offsets.Add(new ObjectHash(offset.Hash), (viewAccessor, offset.Offset));
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

        private static readonly ArrayPool<byte> DiffBytesPool = ArrayPool<byte>.Shared;

        public static (int Type, byte[] Bytes) RestoreDiffedObjectBytes(MemoryMappedViewAccessor memory,
            PackObject packObject)
        {
            var deltaStack = new Stack<Memory<byte>>();
            var bytesToFree = new Stack<byte[]>();

            while (packObject.Type == 6)
            {
                var buffer = DiffBytesPool.Rent(packObject.DataSize);
                var deltaData = buffer.AsMemory(0, packObject.DataSize);
                deltaStack.Push(deltaData);
                bytesToFree.Push(buffer);

                var deltaOffset = ReadDeltaOffset(memory, packObject);
                HashContent.UnpackTo(memory, packObject, deltaData.Span, deltaOffset.BytesRead);
                packObject = ReadPackObject(memory, packObject.Offset - deltaOffset.NegativeOffset);
            }
            
            var type = packObject.Type;

            var restoredObjectBytesFromPool = DiffBytesPool.Rent(packObject.DataSize);
            Span<byte> restoredBytesSpan = restoredObjectBytesFromPool.AsSpan(0, packObject.DataSize);

            HashContent.UnpackTo(memory, packObject, restoredBytesSpan);

            Span<byte> targetBuffer = null;
            while (deltaStack.TryPop(out var deltaData))
            {
                var deltaDataSpan = deltaData.Span;
                var (_, deltaOffset) = ReadVariableDeltaOffsetLength(deltaDataSpan);
                int targetLength;
                (targetLength, deltaOffset) = ReadVariableDeltaOffsetLength(deltaDataSpan, deltaOffset);

                var targetBufferFromPool = DiffBytesPool.Rent(targetLength);
                targetBuffer = targetBufferFromPool.AsSpan(0, targetLength);

                var currentTargetOffset = 0;
                while (deltaOffset < deltaData.Length)
                {
                    ApplyDeltaInstruction(restoredBytesSpan, targetBuffer, deltaDataSpan, ref deltaOffset,
                        ref currentTargetOffset);
                }
                DiffBytesPool.Return(restoredObjectBytesFromPool);
                restoredObjectBytesFromPool = targetBufferFromPool;
                restoredBytesSpan = targetBuffer;

                DiffBytesPool.Return(bytesToFree.Pop());
            }

            var result = targetBuffer.ToArray();

            DiffBytesPool.Return(restoredObjectBytesFromPool);

            return (type, result);
        }

        private static void ApplyDeltaInstruction(in ReadOnlySpan<byte> source, Span<byte> target, in ReadOnlySpan<byte> deltaData,
            ref int currentDeltaOffset, ref int currentTargetBufferOffset)
        {
            var instruction = deltaData[currentDeltaOffset];

            if ((instruction & 0b10000000) != 0)
                HandleCopyInstruction(source, target, deltaData, ref currentDeltaOffset, ref currentTargetBufferOffset);
            else
                HandleInsertInstruction(target, deltaData, ref currentDeltaOffset,
                    ref currentTargetBufferOffset);
        }

        private static void HandleInsertInstruction(in Span<byte> target, in ReadOnlySpan<byte> deltaData,
            ref int currentDeltaOffset, ref int currentTargetOffset)
        {
            var bytesToCopy = deltaData[currentDeltaOffset++];
            deltaData.Slice(currentDeltaOffset, bytesToCopy).CopyTo(target.Slice(currentTargetOffset, bytesToCopy));

            currentDeltaOffset += bytesToCopy;
            currentTargetOffset += bytesToCopy;
        }

        private static void HandleCopyInstruction(in ReadOnlySpan<byte> source, in Span<byte> target, in ReadOnlySpan<byte> deltaData,
            ref int currentDeltaOffset, ref int currentTargetOffset)
        {
            var copyInstruction = deltaData[currentDeltaOffset++];
            var copyFields = new Stack<DeltaCopyFields>(7);

            if ((copyInstruction & 0b01000000) != 0)
                copyFields.Push(DeltaCopyFields.Size3);
            if ((copyInstruction & 0b00100000) != 0)
                copyFields.Push(DeltaCopyFields.Size2);
            if ((copyInstruction & 0b00010000) != 0)
                copyFields.Push(DeltaCopyFields.Size1);
            if ((copyInstruction & 0b00001000) != 0)
                copyFields.Push(DeltaCopyFields.Offset4);
            if ((copyInstruction & 0b00000100) != 0)
                copyFields.Push(DeltaCopyFields.Offset3);
            if ((copyInstruction & 0b00000010) != 0)
                copyFields.Push(DeltaCopyFields.Offset2);
            if ((copyInstruction & 0b00000001) != 0)
                copyFields.Push(DeltaCopyFields.Offset1);

            var offset = 0;
            var length = 0;

            while (copyFields.TryPop(out var field))
            {
                var b = deltaData[currentDeltaOffset++];
                var shift = GetShiftForField(field);

                if (IsOffset(field))
                    offset |= b << shift;
                else
                    length |= b << shift;
            }

            if (length == 0)
                length = 0x10000;

            var sourceSlice = source.Slice(offset, length);
            var targetSlice = target.Slice(currentTargetOffset, length);
            sourceSlice.CopyTo(targetSlice);
            currentTargetOffset += length;
        }

        private static int GetShiftForField(DeltaCopyFields field)
        {
            if (field == DeltaCopyFields.Offset1 || field == DeltaCopyFields.Size1)
                return 0;
            if (field == DeltaCopyFields.Offset2 || field == DeltaCopyFields.Size2)
                return 8;
            if (field == DeltaCopyFields.Offset3 || field == DeltaCopyFields.Size3)
                return 16;
            if (field == DeltaCopyFields.Offset4)
                return 24;

            throw new NotImplementedException();
        }

        private static bool IsOffset(DeltaCopyFields field) =>
            field == DeltaCopyFields.Offset1 || field == DeltaCopyFields.Offset2 || field == DeltaCopyFields.Offset3 ||
            field == DeltaCopyFields.Offset4;

        private static (int targetLength, int deltaOffset) ReadVariableDeltaOffsetLength(in ReadOnlySpan<byte> deltaData,
            int offset = 0)
        {
            var b = deltaData[offset++];
            var length = b & 0b01111111;
            var fsbSet = (b & 0b10000000) != 0;
            var shift = 7;
            while (fsbSet)
            {
                b = deltaData[offset++];
                fsbSet = (b & 0b10000000) != 0;
                length |= (b & 0b01111111) << shift;
                shift += 7;
            }

            return (length, offset);
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

        public static (long NegativeOffset, int BytesRead) ReadDeltaOffset(MemoryMappedViewAccessor packFile, PackObject packObject)
        {
            var readByte = packFile.ReadByte(packObject.Offset + packObject.HeaderLength);
            var bytesRead = 1;
            var offset = (long) readByte & 127;

            while ((readByte & 128) != 0)
            {
                offset += 1;
                readByte = packFile.ReadByte(packObject.Offset + packObject.HeaderLength + bytesRead++);
                offset <<= 7;
                offset += (long) readByte & 127;
            }

            return (offset, bytesRead);
        }

        private enum DeltaCopyFields
        {
            Offset1,
            Offset2,
            Offset3,
            Offset4,
            Size1,
            Size2,
            Size3
        }
    }
}
