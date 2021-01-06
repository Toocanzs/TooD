using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityMathematicsExtentions;

namespace TooD2
{
    [RequireComponent(typeof(Camera))]
    public class IrradianceManager2 : MonoBehaviour
    {
        public int2 probeCounts = new int2(10, 10);
        public int pixelsPerProbe = 32;
        public int pixelsPerUnit = 32;
        
        public int MaxDirectRayLength = 900;
        [Range(0,1)]
        public float MaxFpsHysterisis = 0.999f;
        [Range(0,1)]
        public float MinFpsHysterisis = 0.979f;

        public float2 DarknessBias = new float2(0, 100);

        [Range(1,5)]
        public float LightSizeMultiplier = 2;
        [HideInInspector] public Transform mainCamera;
        [HideInInspector] public new Camera camera;
        public static IrradianceManager2 Instance;
        public RenderTexture wallBuffer;
        public RenderTexture diffuseRadialBuffer;
        public RenderTextureDescriptor diffuseAveragePerProbeBufferDescriptor;
        public RenderTexture diffuseFullScreenAverageBuffer;
        public RenderTexture phiNoiseBuffer;
        private static readonly int TempPhiTextureId = Shader.PropertyToID("__TEMP__PHI__TEXTURE");

        public int2 BottomLeft => math.int2(transform.pos().xy) - probeCounts / 2;

        public Material phiNoiseMat;

        public RenderTexture debug;

        private Mesh debugMesh;
        public Mesh quadsMesh;
        public Material debugMat;
        public Material quadsOffsetMaterial;
        public Material SmartBlendedBlitMaterial;

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

            wallBuffer = new RenderTexture(wallBufferSize.x, wallBufferSize.y, 0, RenderTextureFormat.DefaultHDR,
                RenderTextureReadWrite.Linear);
            wallBuffer.wrapMode = TextureWrapMode.Clamp;
            wallBuffer.Create();

            diffuseRadialBuffer = new RenderTexture(irradianceBandBufferSize.x, irradianceBandBufferSize.y,
                0, RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Linear);
            diffuseRadialBuffer.enableRandomWrite = true;
            diffuseRadialBuffer.Create();
            
            diffuseAveragePerProbeBufferDescriptor = new RenderTextureDescriptor(probeCounts.x, probeCounts.y, RenderTextureFormat.DefaultHDR, 0);
            diffuseAveragePerProbeBufferDescriptor.sRGB = false;
            diffuseAveragePerProbeBufferDescriptor.enableRandomWrite = true;

            diffuseFullScreenAverageBuffer = new RenderTexture(probeCounts.x * pixelsPerUnit,
                probeCounts.y * pixelsPerUnit, 0,
                RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Linear);
            diffuseFullScreenAverageBuffer.enableRandomWrite = true;
            diffuseFullScreenAverageBuffer.Create();

            phiNoiseBuffer = new RenderTexture(irradianceBandBufferSize.x + pixelsPerProbe + 2,
                irradianceBandBufferSize.y + 1,
                0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            phiNoiseBuffer.Create();
            InitPhiNoise();

            debug = diffuseFullScreenAverageBuffer;
            transform.GetChild(0).GetComponent<MeshRenderer>().material.mainTexture = debug;//TODO: REMVOE

            CreateDebugMesh();
            
            quadsMesh = MeshGenerator.CreateQuadsMesh(probeCounts);
            quadsOffsetMaterial.SetTexture("PhiNoise", phiNoiseBuffer);
            quadsOffsetMaterial.SetVector("probeCounts", new float4(probeCounts.x, probeCounts.y, 0, 0));
            quadsOffsetMaterial.SetInt("pixelsPerProbe", pixelsPerProbe);
        }

        #if DEBUG
        private int what = 0;
        private void Update()
        {
            if (Input.GetButtonDown("Jump"))
            {
                Application.targetFrameRate = Application.targetFrameRate == -1 ? 60 : -1;
            }

            if (Input.GetButtonDown("Fire1"))
            {
                what = what == 0 ? 1 : 0;
                Shader.SetGlobalInt("TEST", what);
            }
        }
        #endif

        private void OnDestroy()
        {
            wallBuffer.ReleaseIfExists();
            diffuseRadialBuffer.ReleaseIfExists();
            phiNoiseBuffer.ReleaseIfExists();
            diffuseFullScreenAverageBuffer.ReleaseIfExists();
        }

        private void InitPhiNoise()
        {
            phiNoiseMat.DisableKeyword("UPDATE");
            UpdatePhiNoise();
            phiNoiseMat.EnableKeyword("UPDATE");
        }

        
        private void UpdatePhiNoise()
        {
            CommandBuffer command = new CommandBuffer();
            UpdatePhiNoise(command);
            Graphics.ExecuteCommandBuffer(command);
        }
        
        public void UpdatePhiNoise(CommandBuffer command)
        {
            command.GenerateTempReadableCopy(TempPhiTextureId, phiNoiseBuffer);
            command.Blit(TempPhiTextureId, phiNoiseBuffer, phiNoiseMat);
            command.ReleaseTemporaryRT(TempPhiTextureId);
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
            debugMat.SetVector("G_IrradianceBand_Size",
                new float4(diffuseRadialBuffer.width, diffuseRadialBuffer.height, 0, 0));
            debugMat.SetInt("pixelsPerProbe", pixelsPerProbe);
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
            return SetPositionRounded(mainCamera.position);
        }
        
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying)
            {
                float3 pos = transform.pos() - math.float3(probeCounts, 0) / 2f;
                pos.z = 0;
                float4x4 matrix = float4x4.TRS(pos, quaternion.identity, math.float3(probeCounts.xy, 1));
                debugMat.SetPass(0);
                Graphics.DrawMeshNow(debugMesh, matrix, 0);
            }
        }
    }
}

public class MeshGenerator
{
    public static Mesh CreateQuadsMesh(int2 size)
    {
        var mesh = new Mesh();
        var vertices = new Vector3[size.x*size.y * 6];
        for (int i = 0, y = 0; y < size.y; y++)
        for (int x = 0; x < size.x; x++, i+=6)
        {
            vertices[i + 0] = (math.float3(0, 0, 0) / math.float3(size, 1)).asV3();
            vertices[i + 1] = (math.float3(0, 1, 0) / math.float3(size, 1)).asV3();
            vertices[i + 2] = (math.float3(1, 1, 0) / math.float3(size, 1)).asV3();
            
            vertices[i + 3] = (math.float3(0, 0, 0) / math.float3(size, 1)).asV3();
            vertices[i + 4] = (math.float3(1, 1, 0) / math.float3(size, 1)).asV3();
            vertices[i + 5] = (math.float3(1, 0, 0) / math.float3(size, 1)).asV3();
        }

        mesh.vertices = vertices;

        var triangles = new int[vertices.Length];
        var bary = new Vector4[vertices.Length];
        for (int ti = 0, vi = 0; ti < triangles.Length; ti += 6, vi += 6)
        {
            triangles[ti + 0] = vi + 0;
            triangles[ti + 1] = vi + 1;
            triangles[ti + 2] = vi + 2;
            
            triangles[ti + 3] = vi + 3;
            triangles[ti + 4] = vi + 4;
            triangles[ti + 5] = vi + 5;
            
            bary[vi + 0] = new Vector4(1, 0, 0, 0);
            bary[vi + 1] = new Vector4(0, 1, 0, 0);
            bary[vi + 2] = new Vector4(0, 0, 1, 0);
            
            bary[vi + 3] = new Vector4(1, 0, 0, 1);
            bary[vi + 4] = new Vector4(0, 1, 0, 1);
            bary[vi + 5] = new Vector4(0, 0, 1, 1);
        }


        mesh.triangles = triangles;

        
        var uvs = new Vector4[vertices.Length];

        for (int i = 0, y = 0; y < size.y; y++)
        for (int x = 0; x < size.x; x++, i+=6)
        {
            uvs[i + 0] = new Vector4(x, y, 0, 0);
            uvs[i + 1] = new Vector4(x, y, 0, 1);
            uvs[i + 2] = new Vector4(x, y, 1, 1);
            
            uvs[i + 3] = new Vector4(x, y, 0, 0);
            uvs[i + 4] = new Vector4(x, y, 1, 1);
            uvs[i + 5] = new Vector4(x, y, 1, 0);
        }
        
        mesh.SetUVs(0, uvs);
        mesh.SetUVs(1, bary);
        return mesh;
    }
}