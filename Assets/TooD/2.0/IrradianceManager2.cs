using System;
using Unity.Mathematics;
using UnityEngine;
using UnityMathematicsExtentions;
namespace TooD2
{
    [RequireComponent(typeof(Camera))]
    public class IrradianceManager2 : MonoBehaviour
    {
        public int2 probeCounts = new int2(10, 10);
        public int pixelsPerProbe = 32;
        public int pixelsPerUnit = 32;
        [HideInInspector]
        public Transform mainCamera;
        [HideInInspector]
        public new Camera camera;
        public static IrradianceManager2 Instance;
        public RenderTexture wallBuffer;
        public DoubleBuffer irradianceBandBuffer;

        public RenderTexture debug;
        
        static Mesh debugMesh;
        public Material debugMat;

        private void OnValidate()
        {
            camera = GetComponent<Camera>();
            camera.aspect = (float) probeCounts.x / probeCounts.y;
            camera.orthographicSize = (float) probeCounts.y / 2;
        }

        void Start()
        {
            if (Instance != null)
            {
                Debug.LogError($"Duplicate instance of {nameof(IrradianceManager2)}");
                return;
            }
            Instance = this;
            if (math.any(probeCounts % 2 == 1))
                Debug.LogError($"{nameof(probeCounts)} must be even");
            SetPositionRounded(mainCamera.position);
            camera = GetComponent<Camera>();
            if (math.any(camera.backgroundColor.asFloat4()))
            {
                Debug.LogError("Irradiance camera must have a background color of 0,0,0,0");
            }
            
            int2 irradianceBandBufferSize = probeCounts * math.int2(pixelsPerProbe + 2, 1);
            int2 wallBufferSize = probeCounts * pixelsPerUnit;
            
            wallBuffer = new RenderTexture(wallBufferSize.x, wallBufferSize.y, 0, RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Linear);
            wallBuffer.wrapMode = TextureWrapMode.Clamp;
            wallBuffer.Create();
            
            irradianceBandBuffer = new RenderTexture(irradianceBandBufferSize.x, irradianceBandBufferSize.y,
                    0, RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Linear)
                .ToDoubleBuffer();
            irradianceBandBuffer.enableRandomWrite = true;
            irradianceBandBuffer.Create();

            debug = irradianceBandBuffer.Current;
            
            CreateDebugMesh();
        }

        private void CreateDebugMesh()
        {
            debugMesh = new Mesh();
            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 1, 0),
                new Vector3(1, 1, 0)
            };
            debugMesh.vertices = vertices;
            int[] tris = new int[6]
            {
                0, 2, 1,
                2, 3, 1
            };
            debugMesh.triangles = tris;
            Vector2[] uv = new Vector2[4]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            debugMesh.uv = uv;
            
            debugMat.SetVector("probeCounts", new float4(probeCounts.x, probeCounts.y, 0, 0));
            debugMat.SetVector("G_IrradianceBand_Size", new float4(irradianceBandBuffer.Current.width, irradianceBandBuffer.Current.height, 0, 0));
            debugMat.SetInt("pixelsPerProbe", pixelsPerProbe);
        }

        private void OnDestroy()
        {
            wallBuffer.ReleaseIfExists();
            irradianceBandBuffer.ReleaseIfExists();
        }

        int2 SetPositionRounded(float3 newPos)
        {
            int3 old = math.int3(transform.position);
            int3 rounded = math.int3(math.floor(math.double3(newPos.xy, mainCamera.position.z)));
            transform.position = new Vector3(rounded.x, rounded.y, rounded.z);
            return (old - rounded).xy;
        }

        public int2 DoMove()
        {
            if (math.distance(transform.position, mainCamera.position) > 5)
            {
                return SetPositionRounded(mainCamera.position);
            }
            return new int2(0,0);
        }

        //TODO: Probe positions are += 0.5
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying)
            {
                float3 pos = transform.pos() - math.float3(probeCounts, 0) / 2f;
                pos.z = 0;
                float4x4 mat = float4x4.TRS(pos, quaternion.identity, math.float3(probeCounts.xy, 1));
                debugMat.SetPass(0);
                Graphics.DrawMeshNow(debugMesh, mat, 0);
            }
        }
    }
}