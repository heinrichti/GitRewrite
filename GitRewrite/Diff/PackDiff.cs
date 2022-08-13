using GitRewrite.Diff;
using GitRewrite.IO;
using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GitRewrite.Diff
{
    internal class PackDiff
    {
        private List<DiffInstruction> _instructions;
        public readonly int TargetLen;
        public readonly long NegativeOffset;

        public PackDiff(MemoryMappedViewAccessor memory, PackObject packObject)
        {
            var (baseOffset, bytesRead) = ReadDeltaOffset(memory, packObject);

            var difInstructionBytes = HashContent.Unpack(memory, packObject, bytesRead);

            (_, bytesRead) = ReadVariableDeltaOffsetLength(difInstructionBytes, 0);
            int targetLength;
            (targetLength, bytesRead) = ReadVariableDeltaOffsetLength(difInstructionBytes, bytesRead);

            _instructions = BuildDeltaInstructions(difInstructionBytes, packObject, bytesRead);
            TargetLen = targetLength;
            NegativeOffset = baseOffset;
        }

        private PackDiff(int targetLen, long negativeOffset, List<DiffInstruction> instructions)
        {
            TargetLen = targetLen;
            NegativeOffset = negativeOffset;
            _instructions = instructions;
        }

        public PackDiff Combine(PackDiff other)
        {
            var instructions = new List<DiffInstruction>();
            foreach (var instruction in _instructions)
            {
                if (instruction is CopyInstruction copyInstruction)
                    instructions.AddRange(GetInstructionsFromCopy(copyInstruction, other));
                else
                    instructions.Add(instruction);
            }

            return new PackDiff(TargetLen, other.NegativeOffset, instructions);
        }

        public byte[] Apply(Memory<byte> bytes)
        {
            var target = new byte[TargetLen];
            var targetOffset = 0;

            foreach (var instruction in _instructions)
            {
                if (instruction is AddInstruction add)
                {
                    var len = add.Length;
                    add.Bytes.Span[add.Start..add.End].CopyTo(target.AsSpan(targetOffset, len));
                    targetOffset += len;
                } 
                else if (instruction is CopyInstruction copy)
                {
                    bytes.Span[copy.Offset..(copy.Offset + copy.Length)].CopyTo(target.AsSpan(targetOffset, copy.Length));
                    targetOffset += copy.Length;
                }
            }

            return target;
        }

        private static IEnumerable<DiffInstruction> GetInstructionsFromCopy(CopyInstruction copyInstruction, PackDiff source)
        {
            var currentSourceOffset = 0;
            var copyInstructionConsumed = 0;

            var endOffset = copyInstruction.Offset + copyInstruction.Length;

            foreach (var sourceInstruction in source._instructions)
            {
                if (copyInstruction.Offset < currentSourceOffset + sourceInstruction.Length
                    && endOffset > currentSourceOffset)
                {
                    var sourceInstructionOffset = copyInstruction.Offset + copyInstructionConsumed - currentSourceOffset;
                    var bytesToTake = sourceInstruction.Length - sourceInstructionOffset <= copyInstruction.Length - copyInstructionConsumed
                        ? sourceInstruction.Length - sourceInstructionOffset
                        : copyInstruction.Length - copyInstructionConsumed;

                    yield return sourceInstruction switch
                    {
                        CopyInstruction copy => new CopyInstruction(copy.Offset + sourceInstructionOffset, bytesToTake),
                        AddInstruction add => new AddInstruction(add.Bytes, add.Start + sourceInstructionOffset, add.Start + sourceInstructionOffset + bytesToTake),
                        _ => throw new NotImplementedException("unknown diff instruction type")
                    };

                    copyInstructionConsumed += bytesToTake;
                }
                else if (endOffset < currentSourceOffset) {
                    break;
                }

                currentSourceOffset += sourceInstruction.Length;
            }
        }

        private static List<DiffInstruction> BuildDeltaInstructions(byte[] diffData, PackObject packObject, int bytesRead)
        {
            var result = new List<DiffInstruction>();

            while (bytesRead < packObject.DataSize)
            {
                var instruction = diffData[bytesRead];

                if ((instruction & 0b10000000) != 0) {
                    var copyInstruction = new CopyInstruction(diffData, ref bytesRead);
                    result.Add(copyInstruction);
                } 
                else
                {
                    var addInstruction = new AddInstruction(diffData, ref bytesRead);
                    result.Add(addInstruction);
                }
            }

            return result;
        }

        private static (long NegativeOffset, int BytesRead) ReadDeltaOffset(MemoryMappedViewAccessor packFile, PackObject packObject)
        {
            var readByte = packFile.ReadByte(packObject.Offset + packObject.HeaderLength);
            var bytesRead = 1;
            var offset = (long)readByte & 127;

            while ((readByte & 128) != 0)
            {
                offset += 1;
                readByte = packFile.ReadByte(packObject.Offset + packObject.HeaderLength + bytesRead++);
                offset <<= 7;
                offset += (long)readByte & 127;
            }

            return (offset, bytesRead);
        }

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
    }
}
