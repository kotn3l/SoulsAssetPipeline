using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;

#pragma warning disable CS1591

namespace SoulsAssetPipeline.Animation
{
    public partial class HKX
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

        public enum AnimationBlendHint : uint
        {
            /// Normal
            NORMAL = 0,
            /// Additive (deprecated format)
            ADDITIVE_DEPRECATED = 1,
            /// Additive
            ADDITIVE = 2,
        };

        public enum AnimationType : uint
        {
            /// Should never be used
            HK_UNKNOWN_ANIMATION = 0,
            /// Interleaved
            HK_INTERLEAVED_ANIMATION,
            /// Mirrored
            HK_MIRRORED_ANIMATION,
            /// Spline
            HK_SPLINE_COMPRESSED_ANIMATION,
            /// Quantized
            HK_QUANTIZED_COMPRESSED_ANIMATION,
            /// Predictive
            HK_PREDICTIVE_COMPRESSED_ANIMATION,
            /// Reference Pose
            HK_REFERENCE_POSE_ANIMATION,
        };

        public class Transform : IHKXSerializable
        {
            public HKVector4 Position;
            public HKVector4 Rotation;
            public HKVector4 Scale;

            public Transform()
            {

            }
            public Transform(Vector3 pos, Vector3 rot, Vector3 scale)
            {
                Position = new HKVector4(new Vector4(pos, 0));
                {
                    var cx = MathF.Cos(rot.X / 2);
                    var cy = MathF.Cos(rot.Y / 2);
                    var cz = MathF.Cos(rot.Z / 2);
                    var sx = MathF.Sin(rot.X / 2);
                    var sy = MathF.Sin(rot.Y / 2);
                    var sz = MathF.Sin(rot.Z / 2);
                    var q = new Quaternion();
                    q.X = sx * cy * cz + cx * sy * sz;
                    q.Y = cx * sy * cz + sx * cy * sz;
                    q.Z = cx * cy * sz + sx * sy * cz;
                    q.W = cx * cy * cz + sx * sy * sz;
                    //return q;
                    Matrix4x4 m1 = Matrix4x4.CreateRotationX(rot.X) *
                        Matrix4x4.CreateRotationZ(rot.Z) * Matrix4x4.CreateRotationY(rot.Y);
                    var qs = Quaternion.CreateFromRotationMatrix(m1);
                    //return qs;

                    cx = MathF.Cos(rot.X);
                    cy = MathF.Cos(rot.Y);
                    cz = MathF.Cos(rot.Z);
                    sx = MathF.Sin(rot.X);
                    sy = MathF.Sin(rot.Y);
                    sz = MathF.Sin(rot.Z);
                    Matrix4x4 m = Matrix4x4.Identity;
                    m.M11 = cz * cy;
                    m.M12 = sz;//-sz;
                    m.M13 = -cz * sy;//cz * sy;
                    m.M21 = sx * sy - sz * cx * cy;//sx * sy + cx * sz * cy;
                    m.M22 = cx * cz;
                    m.M23 = cx * sz * sy + cy * sx;//cx * sz * sy - cy * sx;
                    m.M31 = cy * sx * sz + cx * sy;//cy * sx * sz - cx * sy;
                    m.M32 = -cz * sx;//cz * sx;
                    m.M33 = cx * cy - sx * sz * sy;//cx * cy + sx * sz * sy;
                    var qm = Quaternion.CreateFromRotationMatrix(m);
                    Rotation = new HKVector4(qm);
                }
                Scale = new HKVector4(new Vector4(scale, 0));
            }
            public override void Read(HKX hkx, HKXSection section, HKXObject source, BinaryReaderEx br, HKXVariation variation)
            {
                Position = new HKVector4();
                Position.Read(hkx, section, source, br, variation);
                Rotation = new HKVector4();
                Rotation.Read(hkx, section, source, br, variation);
                Scale = new HKVector4();
                Scale.Read(hkx, section, source, br, variation);
            }

            public override void Write(HKX hkx, HKXSection section, BinaryWriterEx bw, uint sectionBaseOffset, HKXVariation variation)
            {
                throw new NotImplementedException();
            }
        }

        public class Bone : IHKXSerializable
        {
            public HKCString Name;
            public int LockTranslation;

            public Bone()
            {

            }
            public Bone(string name)
            {
                Name = new HKCString(name);
                LockTranslation = 0;
            }

            public override void Read(HKX hkx, HKXSection section, HKXObject source, BinaryReaderEx br, HKXVariation variation)
            {
                //AssertPointer(hkx, br);
                //br.ReadUInt64s(1); // blah
                Name = new HKCString(hkx, section, source, br, variation);
                LockTranslation = br.ReadInt32();
                if (variation != HKXVariation.HKXDS1)
                    br.ReadInt32(); // Padding?
            }

            public override void Write(HKX hkx, HKXSection section, BinaryWriterEx bw, uint sectionBaseOffset, HKXVariation variation)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Returns the bone name of this bone
            /// </summary>
            /// <returns>Bone name</returns>
            public override string ToString()
            {
                return Name.GetString();
            }
        }

        public class HKASkeleton : HKXObject
        {
            public HKCString Name;
            public HKArray<HKShort> ParentIndices;
            public HKArray<Bone> Bones;
            public HKArray<Transform> Transforms;
            public HKArray<HKFloat> ReferenceFloats;

            public HKASkeleton()
            {
                ParentIndices = new HKArray<HKShort>();
                Bones = new HKArray<Bone>();
                Transforms = new HKArray<Transform>();
            }

            public HKASkeleton Clone()
            {
                var h = new HKASkeleton();
                h.Name = Name;
                h.ParentIndices = ParentIndices.Clone();
                h.Bones = Bones.Clone();
                h.Transforms = Transforms.Clone();
                h.ReferenceFloats = ReferenceFloats.Clone();
                return h;
            }

            public override void Read(HKX hkx, HKXSection section, BinaryReaderEx br, HKXVariation variation)
            {
                SectionOffset = (uint)br.Position;

                AssertPointer(hkx, br);
                AssertPointer(hkx, br);

                //br.ReadUInt64s(1); // Name
                Name = new HKCString(hkx, section, this, br, variation);
                ParentIndices = new HKArray<HKShort>(hkx, section, this, br, variation);
                Bones = new HKArray<Bone>(hkx, section, this, br, variation);
                Transforms = new HKArray<Transform>(hkx, section, this, br, variation);
                ReferenceFloats = new HKArray<HKFloat>(hkx, section, this, br, variation);
                br.ReadUInt64s(2); // unused array
                br.ReadUInt64s(2); // unused array
                br.ReadUInt64s(2); // unused array
                br.ReadUInt64s(2); // unused array
                br.ReadUInt64s(1); // padding

                DataSize = (uint)br.Position - SectionOffset;
                ResolveDestinations(hkx, section);
            }

            public override void Write(HKX hkx, HKXSection section, BinaryWriterEx bw, uint sectionBaseOffset, HKXVariation variation)
            {
                throw new NotImplementedException();
            }

            public void Add(HKASkeleton b, short index)
            {
                ParentIndices.Add(b.ParentIndices[index]);
                Bones.Add(b.Bones[index]);
                Transforms.Add(b.Transforms[index]);
                if (b.ReferenceFloats.Size > index)
                {
                    ReferenceFloats.Add(b.ReferenceFloats[index]);
                }
            }
        }

        public class HKASplineCompressedAnimation : HKXObject
        {
            public AnimationType AnimationType;
            public float Duration;
            public int TransformTrackCount;
            public int FloatTrackCount;
            public int FrameCount;
            public int BlockCount;
            public int FramesPerBlock;
            public uint MaskAndQuantization;
            public float BlockDuration;
            public float InverseBlockDuration;
            public float FrameDuration;
            public HKArray<HKUInt> BlockOffsets;
            public HKArray<HKUInt> FloatBlockOffsets;
            public HKArray<HKUInt> TransformOffsets;
            public HKArray<HKUInt> FloatOffsets;
            public HKArray<HKByte> Data;
            public int Endian;

            public override void Read(HKX hkx, HKXSection section, BinaryReaderEx br, HKXVariation variation)
            {
                SectionOffset = (uint)br.Position;

                AssertPointer(hkx, br);

                if (variation == HKXVariation.HKXBloodBorne)
                    br.AssertInt32(0);
                else
                    AssertPointer(hkx, br);

                AnimationType = br.ReadEnum32<AnimationType>();
                Duration = br.ReadSingle();
                TransformTrackCount = br.ReadInt32();
                FloatTrackCount = br.ReadInt32();

                if (variation == HKXVariation.HKXBloodBorne)
                    br.Pad(16);

                if (variation == HKXVariation.HKXDS1)
                {
                    br.ReadInt64s(2); // Annotations

                    FrameCount = br.ReadInt32();
                    BlockCount = br.ReadInt32();

                    FramesPerBlock = br.ReadInt32();
                    MaskAndQuantization = br.ReadUInt32(); 
                    BlockDuration = br.ReadSingle();
                    InverseBlockDuration = br.ReadSingle();
                    FrameDuration = br.ReadSingle();
                }
                else
                {
                    br.ReadInt64s(3); // Annotations

                    FrameCount = br.ReadInt32();
                    BlockCount = br.ReadInt32();
                    FramesPerBlock = br.ReadInt32();
                    MaskAndQuantization = br.ReadUInt32();
                    BlockDuration = br.ReadSingle();
                    InverseBlockDuration = br.ReadSingle();
                    FrameDuration = br.ReadSingle();
                    br.ReadUInt32(); // padding?
                }
                
                BlockOffsets = new HKArray<HKUInt>(hkx, section, this, br, variation);
                FloatBlockOffsets = new HKArray<HKUInt>(hkx, section, this, br, variation);
                TransformOffsets = new HKArray<HKUInt>(hkx, section, this, br, variation);
                FloatOffsets = new HKArray<HKUInt>(hkx, section, this, br, variation);
                Data = new HKArray<HKByte>(hkx, section, this, br, variation);
                Endian = br.ReadInt32();

                DataSize = (uint)br.Position - SectionOffset;
                ResolveDestinations(hkx, section);
            }

            public override void Write(HKX hkx, HKXSection section, BinaryWriterEx bw, uint sectionBaseOffset, HKXVariation variation)
            {
                throw new NotImplementedException();
            }

            public byte[] GetData()
            {
                var bytes = new byte[Data.Size];
                for (int i = 0; i < Data.Size; i++)
                {
                    bytes[i] = Data.GetArrayData().Elements[i].data;
                }
                return bytes;
            }
        }

        public class HKAInterleavedUncompressedAnimation : HKXObject
        {
            public AnimationType AnimationType;
            public float Duration;
            public int TransformTrackCount;
            public int FloatTrackCount;
            public HKArray<Transform> Transforms;
            public HKArray<HKFloat> Floats;

            public override void Read(HKX hkx, HKXSection section, BinaryReaderEx br, HKXVariation variation)
            {
                SectionOffset = (uint)br.Position;

                AssertPointer(hkx, br);

                if (variation == HKXVariation.HKXBloodBorne)
                    br.AssertInt32(0);
                else
                    AssertPointer(hkx, br);

                AnimationType = br.ReadEnum32<AnimationType>();
                Duration = br.ReadSingle();
                TransformTrackCount = br.ReadInt32();
                FloatTrackCount = br.ReadInt32();

                if (variation == HKXVariation.HKXBloodBorne)
                    br.Pad(16);

                if (variation == HKXVariation.HKXDS1)
                {
                    br.ReadInt64s(2); // Annotations
                }
                else
                {
                    // Literally guessing here
                    br.ReadInt64s(3); // Annotations
                    //br.ReadUInt32(); // padding?
                }

                Transforms = new HKArray<Transform>(hkx, section, this, br, variation);
                Floats = new HKArray<HKFloat>(hkx, section, this, br, variation);

                DataSize = (uint)br.Position - SectionOffset;
                ResolveDestinations(hkx, section);
            }

            public override void Write(HKX hkx, HKXSection section, BinaryWriterEx bw, uint sectionBaseOffset, HKXVariation variation)
            {
                throw new NotImplementedException();
            }
        }


        public class HKAAnimationBinding : HKXObject
        {
            public HKArray<HKShort> TransformTrackToBoneIndices;
            public HKArray<HKShort> FloatTrackToFloatSlotIndices;
            // Not even sure this is right asdfasdf
            public HKArray<HKShort> PartitionIndices;
            public AnimationBlendHint BlendHint;
            public string OriginalSkeletonName;

            public override void Read(HKX hkx, HKXSection section, BinaryReaderEx br, HKXVariation variation)
            {
                SectionOffset = (uint)br.Position;

                AssertPointer(hkx, br);
                AssertPointer(hkx, br);
                AssertPointer(hkx, br);
                AssertPointer(hkx, br);
                TransformTrackToBoneIndices = new HKArray<HKShort>(hkx, section, this, br, variation);
                
                FloatTrackToFloatSlotIndices = new HKArray<HKShort>(hkx, section, this, br, variation);
                

                if (variation != HKXVariation.HKXDS1)
                {
                    //PartitionIndices = new HKArray<HKShort>(hkx, section, this, br, variation);
                    PartitionIndices = new HKArray<HKShort>(hkx, section, this, br, variation);
                    BlendHint = br.ReadEnum32<AnimationBlendHint>();
                }
                else
                {
                    // I'm so sorry.
                    if (variation == HKXVariation.HKXDS1 && hkx.IsDS1RAnimHotfix)
                    {
                        br.Position += 12;
                        BlendHint = br.ReadEnum32<AnimationBlendHint>();
                    }
                    else
                    {
                        BlendHint = br.ReadEnum32<AnimationBlendHint>();
                    }

                    
                    OriginalSkeletonName = br.ReadShiftJIS();
                    br.Pad(16);
                }

                br.Pad(16);

                DataSize = (uint)br.Position - SectionOffset;
                ResolveDestinations(hkx, section);
            }

            public override void Write(HKX hkx, HKXSection section, BinaryWriterEx bw, uint sectionBaseOffset, HKXVariation variation)
            {
                throw new NotImplementedException();
            }
        }

        public class HKADefaultAnimatedReferenceFrame : HKXObject
        {
            public System.Numerics.Vector4 Up;
            public System.Numerics.Vector4 Forward;
            public float Duration;
            public HKArray<HKVector4> ReferenceFrameSamples;

            public override void Read(HKX hkx, HKXSection section, BinaryReaderEx br, HKXVariation variation)
            {
                SectionOffset = (uint)br.Position;

                AssertPointer(hkx, br);
                AssertPointer(hkx, br);

                if (variation != HKXVariation.HKXBloodBorne)
                {
                    AssertPointer(hkx, br);
                    AssertPointer(hkx, br);
                }

                Up.X = br.ReadSingle();
                Up.Y = br.ReadSingle();
                Up.Z = br.ReadSingle();
                Up.W = br.ReadSingle();

                Forward.X = br.ReadSingle();
                Forward.Y = br.ReadSingle();
                Forward.Z = br.ReadSingle();
                Forward.W = br.ReadSingle();

                Duration = br.ReadSingle();

                if (variation != HKXVariation.HKXDS1)
                    br.AssertInt32(0); // probably padding

                ReferenceFrameSamples = new HKArray<HKVector4>(hkx, section, this, br, variation);

                br.Pad(16); // probably

                DataSize = (uint)br.Position - SectionOffset;
                ResolveDestinations(hkx, section);
            }

            public override void Write(HKX hkx, HKXSection section, BinaryWriterEx bw, uint sectionBaseOffset, HKXVariation variation)
            {
                throw new NotImplementedException();
            }
        }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
