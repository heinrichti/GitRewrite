﻿using System;
using System.Collections.Generic;

namespace GitRewrite
{
    class ByteMemoryEqualityComparer : IEqualityComparer<ReadOnlyMemory<byte>>
    {
        public bool Equals(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y)
        {
            if (x.Length != y.Length)
                return false;

            var span1 = x.Span;
            var span2 = y.Span;

            for (int i = x.Length - 1; i >= 0; i--)
            {
                if (span1[i] != span2[i])
                    return false;
            }

            return true;
        }

        public int GetHashCode(ReadOnlyMemory<byte> obj)
        {
            var span = obj.Span;

            unchecked
            {
                var hashCode = 0; 
                for (var index = obj.Length - 1; index >= 0; index--)
                {
                    var b = span[index];
                    hashCode = (hashCode * 31) ^ b;
                }

                return hashCode;
            }
        }
    }
}
