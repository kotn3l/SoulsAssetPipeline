using SoulsFormatsSAP;
using System.Collections.Generic;
using System.Numerics;

namespace HKX2
{
    public partial class hknpStaticCompoundShapeTree : hkcdStaticTreeDefaultTreeStorage6
    {
        public override uint Signature { get => 897371001; }
        
        
        public override void Read(PackFileDeserializer des, BinaryReaderEx br)
        {
            base.Read(des, br);
        }
        
        public override void Write(PackFileSerializer s, BinaryWriterEx bw)
        {
            base.Write(s, bw);
        }
    }
}
