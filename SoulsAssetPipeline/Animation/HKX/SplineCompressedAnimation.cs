//	This is an adaptation of code from the Havok Format Library
//  https://github.com/PredatorCZ/HavokLib/blob/master/source/hkaSplineDecompressor.cpp
//	Original code Copyright(C) 2016-2019 Lukas Cone
//  Adapted to C# by Meowmaritus and Katalash
//
//	This program is free software : you can redistribute it and / or modify
//	it under the terms of the GNU General Public License as published by
//	the Free Software Foundation, either version 3 of the License, or
//	(at your option) any later version.
//
//	This program is distributed in the hope that it will be useful,
//	but WITHOUT ANY WARRANTY; without even the implied warranty of
//	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
//	GNU General Public License for more details.
//
//	You should have received a copy of the GNU General Public License
//	along with this program.If not, see <https://www.gnu.org/licenses/>.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using SoulsFormats;
using Havoc.Objects;
using static SoulsFormats.DRB;
using HKX2;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;
using DotNext;
using System.Collections;
using System.Threading.Channels;

namespace SoulsAssetPipeline.Animation
{
    public class SplineCompressedAnimation
    {
        [Flags]
        public enum FlagOffset : byte
        {
            StaticX = 0b00000001,
            StaticY = 0b00000010,
            StaticZ = 0b00000100,
            StaticW = 0b00001000,
            SplineX = 0b00010000,
            SplineY = 0b00100000,
            SplineZ = 0b01000000,
            SplineW = 0b10000000
        };

        public enum ScalarQuantizationType
        {
            BITS8 = 0,
            BITS16 = 1,
        };

        public enum RotationQuantizationType
        {
            POLAR32 = 0, //4 bytes long
            THREECOMP40 = 1, //5 bytes long
            THREECOMP48 = 2, //6 bytes long
            THREECOMP24 = 3, //3 bytes long
            STRAIGHT16 = 4, //2 bytes long
            UNCOMPRESSED = 5, //16 bytes long
        }

        public static int GetRotationAlign(RotationQuantizationType qt)
        {
            switch (qt)
            {
                case RotationQuantizationType.POLAR32: return 4;
                case RotationQuantizationType.THREECOMP40: return 1;
                case RotationQuantizationType.THREECOMP48: return 2;
                case RotationQuantizationType.THREECOMP24: return 1;
                case RotationQuantizationType.STRAIGHT16: return 2;
                case RotationQuantizationType.UNCOMPRESSED: return 4;
                default: throw new NotImplementedException();
            }
        }

        public static int GetRotationByteCount(RotationQuantizationType qt)
        {
            switch (qt)
            {
                case RotationQuantizationType.POLAR32: return 4;
                case RotationQuantizationType.THREECOMP40: return 5;
                case RotationQuantizationType.THREECOMP48: return 6;
                case RotationQuantizationType.THREECOMP24: return 3;
                case RotationQuantizationType.STRAIGHT16: return 2;
                case RotationQuantizationType.UNCOMPRESSED: return 16;
                default: throw new NotImplementedException();
            }
        }

        public static int GetScaleSizeMap(ScalarQuantizationType st)
        {
            switch (st)
            {
                case ScalarQuantizationType.BITS8: return 1;
                case ScalarQuantizationType.BITS16: return 2;
                default: throw new NotImplementedException();
            }
        }

        public static float ReadQuantizedFloat(BinaryReaderEx bin, float min, float max, ScalarQuantizationType type)
        {
            float ratio = -1;
            switch (type)
            {
                case ScalarQuantizationType.BITS8: ratio = bin.ReadByte() / 255.0f; break;
                case ScalarQuantizationType.BITS16: ratio = bin.ReadUInt16() / 65535.0f; break;
                default: throw new NotImplementedException();
            }
            return min + ((max - min) * ratio);
        }

        // Because C# can't static cast an int to a float natively
        public static float CastToFloat(uint src)
        {
            var floatbytes = BitConverter.GetBytes(src);
            return BitConverter.ToSingle(floatbytes, 0);
        }

        public static int CastToInt(float src)
        {
            var floatbytes = BitConverter.GetBytes(src);
            return BitConverter.ToInt32(floatbytes, 0);
        }

        public static Quaternion ReadQuatSTRAIGHT16(BinaryReaderEx br)
        {
            byte[] input = br.ReadBytes(2);
            const int res = 7;
            const float scale = 1f / 7f;

            int[] quantized = new int[4];
            quantized[0] = input[0] & 0x0F;
            quantized[1] = input[0] >> 4;
            quantized[2] = input[1] & 0x0F;
            quantized[3] = input[1] >> 4;

            // Set the vector real components
            Quaternion result = new Quaternion(
                (quantized[0] - res) * scale,
                (quantized[1] - res) * scale,
                (quantized[2] - res) * scale,
                (quantized[3] - res) * scale
            );

            // Normalize the quaternion
            result = Quaternion.Normalize(result);

            return result;
        }
        public static Quaternion ReadQuatPOLAR32(BinaryReaderEx br)
        {
            const ulong rMask = (1 << 10) - 1;
            const float rFrac = 1.0f / rMask;
            const float fPI = 3.14159265f;
            const float fPI2 = 0.5f * fPI;
            const float fPI4 = 0.5f * fPI2;
            const float phiFrac = fPI2 / 511.0f;

            uint cVal = br.ReadUInt32();

            float R = CastToFloat((cVal >> 18) & (uint)(rMask & 0xFFFFFFFF)) * rFrac;
            R = 1.0f - (R * R);

            float phiTheta = (float)((cVal & 0x3FFFF));

            float phi = (float)Math.Floor(Math.Sqrt(phiTheta));
            float theta = 0;

            if (phi > 0.0f)
            {
                theta = fPI4 * (phiTheta - (phi * phi)) / phi;
                phi = phiFrac * phi;
            }

            float magnitude = (float)Math.Sqrt(1.0f - R * R);

            Quaternion retVal;
            retVal.X = (float)(Math.Sin(phi) * Math.Cos(theta) * magnitude);
            retVal.Y = (float)(Math.Sin(phi) * Math.Sin(theta) * magnitude);
            retVal.Z = (float)(Math.Cos(phi) * magnitude);
            retVal.W = R;

            if ((cVal & 0x10000000) > 0)
                retVal.X *= -1;

            if ((cVal & 0x20000000) > 0)
                retVal.Y *= -1;

            if ((cVal & 0x40000000) > 0)
                retVal.Z *= -1;

            if ((cVal & 0x80000000) > 0)
                retVal.W *= -1;

            return retVal;
        }
        public static Quaternion ReadQuatTHREECOMP48(BinaryReaderEx br)
        {
            const ulong mask = (1 << 15) - 1;
            const float fractal = 0.000043161f;

            short x = br.ReadInt16();
            short y = br.ReadInt16();
            short z = br.ReadInt16();

            char resultShift = (char)(((y >> 14) & 2) | ((x >> 15) & 1));
            bool rSign = (z >> 15) != 0;

            x &= (short)mask;
            x -= (short)(mask >> 1);
            y &= (short)mask;
            y -= (short)(mask >> 1);
            z &= (short)mask;
            z -= (short)(mask >> 1);

            float[] tempValF = new float[3];
            tempValF[0] = (float)x * fractal;
            tempValF[1] = (float)y * fractal;
            tempValF[2] = (float)z * fractal;

            float[] retval = new float[4];

            for (int i = 0; i < 4; i++)
            {
                if (i < resultShift)
                    retval[i] = tempValF[i];
                else if (i > resultShift)
                    retval[i] = tempValF[i - 1];
            }

            retval[resultShift] = 1.0f - tempValF[0] * tempValF[0] - tempValF[1] * tempValF[1] - tempValF[2] * tempValF[2];

            if (retval[resultShift] <= 0.0f)
                retval[resultShift] = 0.0f;
            else
                retval[resultShift] = (float)Math.Sqrt(retval[resultShift]);

            if (rSign)
                retval[resultShift] *= -1;

            return new Quaternion(retval[0], retval[1], retval[2], retval[3]);
        }
        public static ulong Read40BitValue(BinaryReaderEx br)
        {
            byte[] bytes = br.ReadBytes(5);
            Array.Resize(ref bytes, 8);
            return BitConverter.ToUInt64(bytes, 0);
        }
        public static Quaternion ReadQuatTHREECOMP40(BinaryReaderEx br)
        {
            const ulong mask = (1 << 12) - 1;
            const ulong positiveMask = mask >> 1;
            const float fractal = 0.000345436f;
            // Read only the 5 bytes needed to prevent EndOfStreamException :fatcat:
            ulong cVal = Read40BitValue(br);

            int x = (int)(cVal & mask);
            int y = (int)((cVal >> 12) & mask);
            int z = (int)((cVal >> 24) & mask);

            int resultShift = (int)((cVal >> 36) & 3);

            x -= (int)positiveMask;
            y -= (int)positiveMask;
            z -= (int)positiveMask;

            float[] tempValF = new float[3];
            tempValF[0] = (float)x * fractal;
            tempValF[1] = (float)y * fractal;
            tempValF[2] = (float)z * fractal;

            float[] retval = new float[4];

            for (int i = 0; i < 4; i++)
            {
                if (i < resultShift)
                    retval[i] = tempValF[i];
                else if (i > resultShift)
                    retval[i] = tempValF[i - 1];
            }

            retval[resultShift] = 1.0f - tempValF[0] * tempValF[0] - tempValF[1] * tempValF[1] - tempValF[2] * tempValF[2];

            if (retval[resultShift] <= 0.0f)
                retval[resultShift] = 0.0f;
            else
                retval[resultShift] = (float)Math.Sqrt(retval[resultShift]);

            if (((cVal >> 38) & 1) > 0)
                retval[resultShift] *= -1;

            var finalQuat = new Quaternion(retval[0], retval[1], retval[2], retval[3]);

            return finalQuat;

        }
        public static Quaternion ReadQuantizedQuaternion(BinaryReaderEx br, RotationQuantizationType type)
        {
            switch (type)
            {
                case RotationQuantizationType.POLAR32:
                    return ReadQuatPOLAR32(br);
                case RotationQuantizationType.THREECOMP40:
                    return ReadQuatTHREECOMP40(br);
                case RotationQuantizationType.THREECOMP48:
                    return ReadQuatTHREECOMP48(br);
                case RotationQuantizationType.THREECOMP24:
                    throw new NotImplementedException();
                case RotationQuantizationType.STRAIGHT16:
                    return ReadQuatSTRAIGHT16(br);
                case RotationQuantizationType.UNCOMPRESSED:
                    return new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                default:
                    return Quaternion.Identity;
            }
        }

        // Algorithm A2.1 The NURBS Book 2nd edition, page 68
        public static int FindKnotSpan(int p, float u, int n, byte[] U)
        {
            if (u >= U[n + 1]) return n;
            if (u <= U[0]) return p;

            // Search
            int low = p;
            int high = n + 1;
            int mid = (low + high) / 2;
            while (u < U[mid] || u >= U[mid + 1])
            {
                if (u < U[mid]) high = mid;
                else low = mid;
                mid = (low + high) / 2;
            }
            return mid;
        }

        public static T EvaluatePoint<T>(float u, int degree, byte[] knots, List<T> controlPoints, Func<T, T, float, T> lerp)
        {
            // Clamp u to the nearest integer
            //u = (float)Math.Round(u);

            int n = controlPoints.Count - 1; // Number of control points - 1
            int span = FindKnotSpan(degree, u, n, knots); // Determine the knot span
            double[] basisFunctions = BasisFunctions(span, u, degree, knots);

            // Compute the point on the curve as a weighted sum of the control points
            T result = default;
            for (int i = 0; i <= degree; i++)
            {
                float weight = (float)basisFunctions[i];
                result = Add(result, Multiply(controlPoints[span - degree + i], weight, lerp));
            }

            return result;
        }

        // Helper: Multiply control point by a weight
        private static T Multiply<T>(T controlPoint, float weight, Func<T, T, float, T> lerp)
        {
            return lerp(default, controlPoint, weight); // Equivalent to controlPoint * weight
        }

        // Helper: Add two values of type T
        private static T Add<T>(T a, T b)
        {
            dynamic da = a;
            dynamic db = b;
            return da + db;
        }

        private static double[] BasisFunctions(int span, float u, int degree, byte[] knots)
        {
            double[] N = new double[degree + 1];
            double[] left = new double[degree + 1];
            double[] right = new double[degree + 1];

            N[0] = 1.0;

            for (int j = 1; j <= degree; j++)
            {
                left[j] = u - knots[span + 1 - j];
                right[j] = knots[span + j] - u;

                double saved = 0.0;

                for (int r = 0; r < j; r++)
                {
                    double temp = N[r] / (right[r + 1] + left[j - r]);
                    N[r] = saved + right[r + 1] * temp;
                    saved = left[j - r] * temp;
                }

                N[j] = saved;
            }

            return N;
        }

        private static unsafe Vector4 EvaluateSimple1(int knotSpanIndex, int degree, float frame, byte[] knots, List<Vector4> cPoints)
        {
            hkSingleFloat32[] U = new hkSingleFloat32[6];

            var u = new hkSingleFloat32(frame);

            hkSingleFloat32 left = u - U[0];
            hkSingleFloat32 right = U[1] - u;

            hkSingleFloat32 sdjk = new hkSingleFloat32();
            UnrollfSetDiv.Apply(HkMathAccuracyMode.HK_ACC_FULL, ref sdjk, left, right + left);

            float[] values = new float[4];
            fixed (float* p = values)
            {
                // Store the Vector128<float> into the array
                Sse.Store(p, sdjk.Value);
            }
            Vector4 hihi = new Vector4(values[0], values[1], values[2], values[3]);
            var bminusa = cPoints[1] - cPoints[0];

            return cPoints[0] + (bminusa * hihi);
        }

        private static unsafe Vector4 EvaluateSimple3(int knotSpanIndex, int degree, float frame, byte[] knots, List<Vector4> cPoints)
        {
            hkSingleFloat32[] U = new hkSingleFloat32[6];

            var u = new hkSingleFloat32(frame);

            hkSingleFloat32 left = u - U[0];
            hkSingleFloat32 right = U[1] - u;

            hkSingleFloat32 sdjk = new hkSingleFloat32();
            UnrollfSetDiv.Apply(HkMathAccuracyMode.HK_ACC_FULL, ref sdjk, left, right + left);

            float[] values = new float[4];
            fixed (float* p = values)
            {
                // Store the Vector128<float> into the array
                Sse.Store(p, sdjk.Value);
            }
            Vector4 hihi = new Vector4(values[0], values[1], values[2], values[3]);
            var bminusa = cPoints[1] - cPoints[0];

            return cPoints[0] + (bminusa * hihi);
        }

        public enum HkMathAccuracyMode
        {
            HK_ACC_23_BIT,
            HK_ACC_12_BIT,
            HK_ACC_FULL
        }

        public struct hkSingleFloat32
        {
            public Vector128<float> Value;
            public hkSingleFloat32()
            {
                Value = Vector128<float>.Zero;
            }
            public hkSingleFloat32(Vector128<float> value)
            {
                Value = value;
            }

            public unsafe hkSingleFloat32(float x)
            {
                // Load the scalar value into the lowest element of a Vector128<float>
                Vector128<float> fx = Sse.LoadScalarVector128(&x);

                // Shuffle the vector to broadcast the loaded value to all elements
                Value = Sse.Shuffle(fx, fx, 0);
            }

            public static hkSingleFloat32 operator *(hkSingleFloat32 a, hkSingleFloat32 b)
            {
                return new hkSingleFloat32(Sse.Multiply(a.Value, b.Value));
            }

            public static hkSingleFloat32 operator /(hkSingleFloat32 a, hkSingleFloat32 b)
            {
                return new hkSingleFloat32(Sse.Divide(a.Value, b.Value));
            }

            public static hkSingleFloat32 operator +(hkSingleFloat32 a, hkSingleFloat32 b)
            {
                return new hkSingleFloat32(Sse.Add(a.Value, b.Value));
            }

            public static hkSingleFloat32 operator -(hkSingleFloat32 a, hkSingleFloat32 b)
            {
                return new hkSingleFloat32(Sse.Subtract(a.Value, b.Value));
            }
        }

        public static class HkMath
        {
            public static hkSingleFloat32 QuadReciprocal(hkSingleFloat32 r)
            {
                var two = Vector128.Create(2.0f);
                Vector128<float> rb = Sse.Reciprocal(r.Value);
                Vector128<float> rbr = Sse.Multiply(r.Value, rb);
                Vector128<float> d = Sse.Subtract(two, rbr);
                Vector128<float> result = Sse.Multiply(rb, d);
                return new hkSingleFloat32(result);
            }
        }

        public static class UnrollfSetDiv
        {
            public static void Apply(HkMathAccuracyMode accuracyMode, ref hkSingleFloat32 self, hkSingleFloat32 a, hkSingleFloat32 b)
            {
                if (!Sse.IsSupported)
                    throw new PlatformNotSupportedException("SSE is not supported on this processor.");

                switch (accuracyMode)
                {
                    case HkMathAccuracyMode.HK_ACC_23_BIT:
                        self = a * HkMath.QuadReciprocal(b);
                        break;
                    case HkMathAccuracyMode.HK_ACC_12_BIT:
                        self = new hkSingleFloat32(Sse.Multiply(a.Value, Sse.Reciprocal(b.Value)));
                        break;
                    default: // HK_ACC_FULL
                        self = a / b;
                        break;
                }
            }
        }

        public static unsafe Vector128<float> SetFromFloat(float x)
        {
            // Load the scalar value into the lowest element of a Vector128<float>
            Vector128<float> fx = Sse.LoadScalarVector128(&x);

            // Shuffle the vector to broadcast the loaded value to all elements
            return Sse.Shuffle(fx, fx, 0);
        }
        private static float[] GetSinglePoint(int knotSpanIndex, int degree, float frame, byte[] knots)
        {
            float[] N = { 1, 0, 0, 0, 0 };

            for (int i = 1; i <= degree; i++)
                for (int j = i - 1; j >= 0; j--)
                {

                    float A = (frame - knots[knotSpanIndex - j]) / (knots[knotSpanIndex + i - j] - knots[knotSpanIndex - j]);
                    // without multiplying A, model jitters slightly
                    float tmp = N[j] * A;
                    // without subtracting tmp, model flies away then resets to origin every few frames
                    N[j + 1] += N[j] - tmp;
                    // without setting to tmp, model either is moved from origin or grows very long limbs
                    // depending on the animation
                    N[j] = tmp;
                }

            return N;
        }
        //Basis_ITS1, GetPoint_NR1, TIME-EFFICIENT NURBS CURVE EVALUATION ALGORITHMS, pages 64 & 65
        public static float GetSinglePoint(int knotSpanIndex, int degree, float frame, byte[] knots, List<float> cPoints)
        {
            var N = GetSinglePoint(knotSpanIndex, degree, frame, knots);

            float retVal = 0.0f;



            for (int i = 0; i <= degree; i++)
                retVal += cPoints[knotSpanIndex - i] * N[i];

            return retVal;
        }

        //Basis_ITS1, GetPoint_NR1, TIME-EFFICIENT NURBS CURVE EVALUATION ALGORITHMS, pages 64 & 65
        public static Quaternion GetSinglePoint(int knotSpanIndex, int degree, float frame, byte[] knots, List<Quaternion> cPoints)
        {
            var N = GetSinglePoint(knotSpanIndex, degree, frame, knots);

            Quaternion retVal = new Quaternion(Vector3.Zero, 0.0f);

            if (knotSpanIndex > 0)
            {
                for (int i = 0; i <= degree; i++)
                    retVal += cPoints[knotSpanIndex - i] * N[i];
            }

            return retVal;
        }

        public class SplineChannel<T> : IEnumerable<T>
        {
            public bool IsDynamic { get; private set; }
            public List<T> Values { get; private set; }
            public T? BoundsMin { get; private set; }
            public T? BoundsMax { get; private set; }
            public T this[int i]
            {
                get
                {
                    return Values[i];
                }
            }
            public T[] ToArray() => Values.ToArray();
            public int Count => Values.Count;
            public SplineChannel() : this(true)
            {
            }
            public SplineChannel(T min, T max) :this(true)
            {
                BoundsMin = min;
                BoundsMax = max;
            }
            public SplineChannel(bool dyn)
            {
                IsDynamic = dyn;
                Values = new List<T>();
            }
            public SplineChannel(bool dyn, params T[] values)
            {
                IsDynamic = dyn;
                Values = new List<T>(values);
            }

            public IEnumerator<T> GetEnumerator()
            {
                return Values.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        public class SplineChannelFloat : SplineChannel<float>
        {
            public SplineChannelFloat(float min, float max) : base(min, max)
            {
                if (min > max)
                {
                    throw new InvalidOperationException();
                }
            }
        }

        public abstract class SplineTrack
        {
            public byte[] Knots {  get; private set; }
            protected readonly long debug_StartOfThisSplineTrack;
            public readonly byte Degree;
            protected readonly short numItems;

            protected SplineTrack(BinaryReaderEx br)
            {
                debug_StartOfThisSplineTrack = br.Position;
                

                numItems = br.ReadInt16();
                Degree = br.ReadByte();
                int knotCount = numItems + Degree + 2;
                int m = numItems + Degree + 1;
                Knots = new byte[knotCount];
                for (int i = 0; i < knotCount; i++)
                {
                    Knots[i] = br.ReadByte();
                }
            }
            private SplineTrack()
            {
                    
            }
        }

        public class SplineTrackQuaternion : SplineTrack
        {
            public SplineChannel<Quaternion> Channel { get; private set; }

            internal SplineTrackQuaternion(BinaryReaderEx br, RotationQuantizationType quantizationType) : base(br)
            {
                br.Pad(GetRotationAlign(quantizationType));

                Channel = new SplineChannel<Quaternion>();

                for (int i = 0; i <= numItems; i++)
                {
                    Channel.Values.Add(ReadQuantizedQuaternion(br, quantizationType));

                    //try
                    //{
                        
                    //}
                    //catch (System.IO.EndOfStreamException)
                    //{
                    //    // TEST
                    //    Channel.Values.Add(Quaternion.Identity);
                    //}
                }
            }

            public Quaternion GetValue(float frame)
            {
                int knotspan = FindKnotSpan(Degree, frame, Channel.Values.Count, Knots);

                return Quaternion.Zero + EvaluatePoint(frame, Degree, Knots, Channel.Values, Quaternion.Slerp);

                return GetSinglePoint(knotspan, Degree, frame, Knots, Channel.Values);
            }

            public void Add(SplineTrackQuaternion track)
            {
                if (track.Channel != null)
                {
                    Channel?.Values.AddRange(track.Channel.Values);
                }
            }
        }

        public class SplineTrackVector3 : SplineTrack
        {
            public SplineChannel<float> ChannelX { get; private set; }
            public SplineChannel<float> ChannelY { get; private set; }
            public SplineChannel<float> ChannelZ { get; private set; }
            //public SplineChannel<Vector4> ChannelAll { get; private set; }

            public int Count()
            {
                return Math.Max(ChannelX == null ? 0 : ChannelX.Count, Math.Max(ChannelY == null ? 0 : ChannelY.Count, ChannelZ == null ? 0 : ChannelZ.Count));
            }

            internal SplineTrackVector3(BinaryReaderEx br, HashSet<FlagOffset> channelTypes, ScalarQuantizationType quantizationType) : base(br)
            {
                br.Pad(4);
                //ChannelAll = new SplineChannel<Vector4>();
                if (channelTypes.Contains(FlagOffset.SplineX))
                {
                    ChannelX = new SplineChannelFloat(br.ReadSingle(), br.ReadSingle());
                }
                else if (channelTypes.Contains(FlagOffset.StaticX))
                {
                    ChannelX = new SplineChannel<float>(false, br.ReadSingle());
                }

                if (channelTypes.Contains(FlagOffset.SplineY))
                {
                    ChannelY = new SplineChannelFloat(br.ReadSingle(), br.ReadSingle());
                }
                else if (channelTypes.Contains(FlagOffset.StaticY))
                {
                    ChannelY = new SplineChannel<float>(false, br.ReadSingle());
                }

                if (channelTypes.Contains(FlagOffset.SplineZ))
                {
                    ChannelZ = new SplineChannelFloat(br.ReadSingle(), br.ReadSingle());
                }
                else if (channelTypes.Contains(FlagOffset.StaticZ))
                {
                    ChannelZ = new SplineChannel<float>(false, br.ReadSingle());
                }

                for (int i = 0; i <= numItems; i++)
                {
                    if (channelTypes.Contains(FlagOffset.SplineX))
                    {
                        ChannelX.Values.Add(ReadQuantizedFloat(br, ChannelX.BoundsMin, ChannelX.BoundsMax, quantizationType));
                    }

                    if (channelTypes.Contains(FlagOffset.SplineY))
                    {
                        ChannelY.Values.Add(ReadQuantizedFloat(br, ChannelY.BoundsMin, ChannelY.BoundsMax, quantizationType));
                    }

                    if (channelTypes.Contains(FlagOffset.SplineZ))
                    {
                        ChannelZ.Values.Add(ReadQuantizedFloat(br, ChannelZ.BoundsMin, ChannelZ.BoundsMax, quantizationType));
                    }
                }
            }

            public float? GetValueX(float frame)
            {
                if (ChannelX == null)
                    return null;

                if (ChannelX.Values.Count == 1)
                    return ChannelX.Values[0];


                return EvaluatePoint(frame, Degree, Knots, ChannelX.Values, (a, b, t) => a + (b - a) * t);

                int knotspan = FindKnotSpan(Degree, frame, ChannelX.Values.Count, Knots);
                return GetSinglePoint(knotspan, Degree, frame, Knots, ChannelX.Values);
            }

            public float? GetValueY(float frame)
            {
                if (ChannelY == null)
                    return null;

                if (ChannelY.Values.Count == 1)
                    return ChannelY.Values[0];

                return EvaluatePoint(frame, Degree, Knots, ChannelY.Values, (a, b, t) => a + (b - a) * t);

                int knotspan = FindKnotSpan(Degree, frame, ChannelY.Values.Count, Knots);
                return GetSinglePoint(knotspan, Degree, frame, Knots, ChannelY.Values);
            }

            public float? GetValueZ(float frame)
            {
                if (ChannelZ == null)
                    return null;

                if (ChannelZ.Values.Count == 1)
                    return ChannelZ.Values[0];

                return EvaluatePoint(frame, Degree, Knots, ChannelZ.Values, (a, b, t) => a + (b - a) * t);

                int knotspan = FindKnotSpan(Degree, frame, ChannelZ.Values.Count, Knots);
                return GetSinglePoint(knotspan, Degree, frame, Knots, ChannelZ.Values);
            }

            public void Add(SplineTrackVector3 track)
            {
                if (track.ChannelX != null)
                {
                    ChannelX?.Values.AddRange(track.ChannelX.Values);
                }
                if (track.ChannelY != null)
                {
                    ChannelY?.Values.AddRange(track.ChannelY.Values);
                }
                if (track.ChannelZ != null)
                {
                    ChannelZ?.Values.AddRange(track.ChannelZ.Values);
                }
            }
        }

        public class TransformMask
        {
            public ScalarQuantizationType PositionQuantizationType { get; set; }
            public RotationQuantizationType RotationQuantizationType { get; set; }
            public ScalarQuantizationType ScaleQuantizationType { get; set; }
            public HashSet<FlagOffset> PositionTypes { get; set; }
            public HashSet<FlagOffset> RotationTypes { get; set; }
            public HashSet<FlagOffset> ScaleTypes { get; set; }

            internal TransformMask(BinaryReaderEx br)
            {
                PositionTypes = new HashSet<FlagOffset>();
                RotationTypes = new HashSet<FlagOffset>();
                ScaleTypes = new HashSet<FlagOffset>();

                var byteQuantizationTypes = br.ReadByte();
                var bytePositionTypes = (FlagOffset)br.ReadByte();
                var byteRotationTypes = (FlagOffset)br.ReadByte();
                var byteScaleTypes = (FlagOffset)br.ReadByte();

                PositionQuantizationType = (ScalarQuantizationType)((byteQuantizationTypes >> 0) & 0x03);
                RotationQuantizationType = (RotationQuantizationType)((byteQuantizationTypes >> 2) & 0x0F);
                ScaleQuantizationType = (ScalarQuantizationType)((byteQuantizationTypes >> 6) & 0x03);

                foreach (var flagOffset in (FlagOffset[])System.Enum.GetValues(typeof(FlagOffset)))
                {
                    if ((bytePositionTypes & flagOffset) != 0)
                        PositionTypes.Add(flagOffset);

                    if ((byteRotationTypes & flagOffset) != 0)
                        RotationTypes.Add(flagOffset);

                    if ((byteScaleTypes & flagOffset) != 0)
                        ScaleTypes.Add(flagOffset);
                }
            }

            public override bool Equals(object obj)
            {
                return obj is TransformMask mask &&
                       PositionQuantizationType == mask.PositionQuantizationType &&
                       RotationQuantizationType == mask.RotationQuantizationType &&
                       ScaleQuantizationType == mask.ScaleQuantizationType &&
                       PositionTypes.SequenceEqual(mask.PositionTypes) &&
                       RotationTypes.SequenceEqual(mask.RotationTypes) &&
                       ScaleTypes.SequenceEqual(mask.ScaleTypes);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(PositionQuantizationType, RotationQuantizationType, ScaleQuantizationType, PositionTypes, RotationTypes, ScaleTypes);
            }
        }

        public class TransformTrack
        {
            public readonly TransformMask Mask;

            public readonly bool HasSplinePosition;
            public readonly bool HasSplineRotation;
            public readonly bool HasSplineScale;
            public readonly bool HasStaticRotation;

            public Vector3 StaticPosition;
            public Quaternion StaticRotation;
            public Vector3 StaticScale;
            public SplineTrackVector3 SplinePosition;
            public SplineTrackQuaternion SplineRotation;
            public SplineTrackVector3 SplineScale;

            private readonly BinaryReaderEx _br;

            public TransformTrack(BinaryReaderEx br)
            {
                _br = br;

                StaticPosition = Vector3.Zero;
                StaticRotation = Quaternion.Identity;
                StaticScale = Vector3.One;

                Mask = new TransformMask(br);

                HasSplinePosition = Mask.PositionTypes.Contains(FlagOffset.SplineX)
                                 || Mask.PositionTypes.Contains(FlagOffset.SplineY)
                                 || Mask.PositionTypes.Contains(FlagOffset.SplineZ);

                HasSplineRotation = Mask.RotationTypes.Contains(FlagOffset.SplineX)
                                 || Mask.RotationTypes.Contains(FlagOffset.SplineY)
                                 || Mask.RotationTypes.Contains(FlagOffset.SplineZ)
                                 || Mask.RotationTypes.Contains(FlagOffset.SplineW);

                HasStaticRotation = Mask.RotationTypes.Contains(FlagOffset.StaticX)
                                 || Mask.RotationTypes.Contains(FlagOffset.StaticY)
                                 || Mask.RotationTypes.Contains(FlagOffset.StaticZ)
                                 || Mask.RotationTypes.Contains(FlagOffset.StaticW);

                HasSplineScale = Mask.ScaleTypes.Contains(FlagOffset.SplineX)
                              || Mask.ScaleTypes.Contains(FlagOffset.SplineY)
                              || Mask.ScaleTypes.Contains(FlagOffset.SplineZ);
            }

            public void ReadValues()
            {
                if (HasSplinePosition)
                {
                    SplinePosition = new SplineTrackVector3(_br, Mask.PositionTypes, Mask.PositionQuantizationType);
                }
                else
                {
                    if (Mask.PositionTypes.Contains(FlagOffset.StaticX))
                    {
                        StaticPosition.X = _br.ReadSingle();
                    }

                    if (Mask.PositionTypes.Contains(FlagOffset.StaticY))
                    {
                        StaticPosition.Y = _br.ReadSingle();
                    }

                    if (Mask.PositionTypes.Contains(FlagOffset.StaticZ))
                    {
                        StaticPosition.Z = _br.ReadSingle();
                    }
                }

                _br.Pad(4);


                if (HasSplineRotation)
                {
                    SplineRotation = new SplineTrackQuaternion(_br, Mask.RotationQuantizationType);
                }
                else
                {
                    if (HasStaticRotation)
                    {
                        _br.Pad(SplineCompressedAnimation.GetRotationAlign(Mask.RotationQuantizationType));
                        StaticRotation = SplineCompressedAnimation.ReadQuantizedQuaternion(_br, Mask.RotationQuantizationType); //_br.ReadBytes(GetRotationByteCount(Mask.RotationQuantizationType));
                    }
                }

                _br.Pad(4);

                if (HasSplineScale)
                {
                    SplineScale = new SplineTrackVector3(_br, Mask.ScaleTypes, Mask.ScaleQuantizationType);
                }
                else
                {
                    if (Mask.ScaleTypes.Contains(FlagOffset.StaticX))
                    {
                        StaticScale.X = _br.ReadSingle();
                    }

                    if (Mask.ScaleTypes.Contains(FlagOffset.StaticY))
                    {
                        StaticScale.Y = _br.ReadSingle();
                    }

                    if (Mask.ScaleTypes.Contains(FlagOffset.StaticZ))
                    {
                        StaticScale.Z = _br.ReadSingle();
                    }
                }

                _br.Pad(4);
            }

            public bool AddTrack(TransformTrack track)
            {
                if (!Mask.Equals(track.Mask))
                {
                    return false;
                }

                SplinePosition.Add(track.SplinePosition);
                SplineScale.Add(track.SplineScale);
                SplineRotation.Add(track.SplineRotation);
                return true;
            }
        }


        public int SaturateInt32(float f)
        {
            int output;
            int intValue = SplineCompressedAnimation.CastToInt(f);

            if (intValue > int.MaxValue)
            {
                output = int.MaxValue;
            }
            else if (intValue < int.MinValue)
            {
                output = int.MinValue;
            }
            else
            {
                output = intValue;
            }

            return output;
        }

        public float SetClampedZeroOne(float a)
        {
            float result = a;
            if (float.IsNaN(a))
            {
                result = 1.0f;
            }
            else
            {
                result = Math.Min(1.0f, Math.Max(a, 0.0f));
            }
            return result;
        }
        public static long ComputePackedNurbsOffsets(uint blockOffset, uint maskAndQuantizationSize)
        {
            // Just return the sum of the base and offsets.
            // QuantizedMask would never have the high bit set anyway... or your animation is over 2Gb
            return blockOffset + (maskAndQuantizationSize & ~0x80000000);
        }

    }
}
