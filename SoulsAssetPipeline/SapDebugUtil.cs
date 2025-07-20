using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoulsAssetPipeline
{
    public static class SapDebugUtil
    {
        public class SapTestAssert : Exception
        {
            public SapTestAssert(string msg)
                : base(msg)
            {

            }
        }

        public static class Flver2ImportDebug
        {
            /// <summary>
            /// Matches bones by name between skeletons and runs boneCheck on them.
            /// Will assert if a bone does not exist in reimported skeleton or if a bone exists
            /// twice in reimported skeleton, depending on the bool parameters.
            /// </summary>
            public static void AssertReimportedSkeletonMatch(FLVER2 original, FLVER2 reimported, Action<FLVER.Node, FLVER.Node> boneCheck,
                bool assertOnBoneNotExisting = true, bool assertOnBoneExistingTwice = true)
            {
                

            }
        }
    }
}
