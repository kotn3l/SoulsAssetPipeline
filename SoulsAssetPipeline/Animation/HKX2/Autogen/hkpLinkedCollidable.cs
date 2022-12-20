using SoulsFormatsSAP;
using System.Collections.Generic;
using System.Numerics;

namespace HKX2
{
    public partial class hkpLinkedCollidable : hkpCollidable
    {
        public override uint Signature { get => 1426635893; }
        
        
        public override void Read(PackFileDeserializer des, BinaryReaderEx br)
        {
            base.Read(des, br);
            br.ReadUInt64();
            br.ReadUInt64();
        }
        
        public override void Write(PackFileSerializer s, BinaryWriterEx bw)
        {
            base.Write(s, bw);
            bw.WriteUInt64(0);
            bw.WriteUInt64(0);
        }
    }
}
