using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoulsAssetPipeline
{
    public static class MemoryHelpers
    {
        public static Memory<byte> ResizeMemory(this Memory<byte> memory, int newSize)
        {
            byte[] newArray = new byte[newSize];
            memory.Span.Slice(0, Math.Min(memory.Length, newSize)).CopyTo(newArray);
            return new Memory<byte>(newArray);
        }
    }
}
