using Unity.Mathematics;
using UnityEngine;

namespace UnityMathematicsExtentions
{
    public static class UnityMathematicsExtensions
    {
        public static float3 pos(this Transform transform)
        {
            return math.float3(transform.position);
        }

        public static float4 asFloat4(this Color color)
        {
            return math.float4(color.r, color.g, color.b, color.a);
        }

        public static Vector3 asV3(this float3 p)
        {
            return new Vector3(p.x, p.y, p.z);
        }
        
        public static Vector3 asV3(this int3 p)
        {
            return new Vector3(p.x, p.y, p.z);
        }
    }
}