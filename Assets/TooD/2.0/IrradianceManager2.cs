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
        [HideInInspector] public Transform mainCamera;
        [HideInInspector] public new Camera camera;
        public static IrradianceManager2 Instance;
        public RenderTexture wallBuffer;
        public RenderTexture diffuseRadialBuffer;
        public RenderTexture diffuseAveragePerProbeBuffer;
        public DoubleBuffer phiNoiseBuffer;

        public int2 BottomLeft => math.int2(transform.pos().xy) - probeCounts / 2;

        public Material phiNoiseMat;

        public RenderTexture debug;

        private Mesh debugMesh;
        private Mesh gridMesh;
        public Material debugMat;

        public Material gridOffsetMat;

        public int MaxDirectRayLength = 900;

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
            
            diffuseAveragePerProbeBuffer = new RenderTexture(probeCounts.x, probeCounts.y,
                0, RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Linear);
            diffuseAveragePerProbeBuffer.enableRandomWrite = true;
            diffuseAveragePerProbeBuffer.Create();

            phiNoiseBuffer = new RenderTexture(irradianceBandBufferSize.x + pixelsPerProbe + 2, irradianceBandBufferSize.y + 1,
                    0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
                .ToDoubleBuffer();
            phiNoiseBuffer.Create();
            InitPhiNoise();

            debug = diffuseAveragePerProbeBuffer;
            transform.GetChild(0).GetComponent<MeshRenderer>().material.mainTexture = debug;//TODO: REMVOE

            CreateDebugMesh();
            gridMesh = MeshGenerator.CreateGridMesh(probeCounts);
            gridOffsetMat.SetTexture("PhiNoise", phiNoiseBuffer.Current);
            gridOffsetMat.SetVector("probeCounts", new float4(probeCounts.x, probeCounts.y, 0, 0));
            gridOffsetMat.SetInt("pixelsPerProbe", pixelsPerProbe);
        }
        
        private void OnDestroy()
        {
            wallBuffer.ReleaseIfExists();
            diffuseRadialBuffer.ReleaseIfExists();
            diffuseAveragePerProbeBuffer.ReleaseIfExists();
            phiNoiseBuffer.ReleaseIfExists();
        }

        private void InitPhiNoise()
        {
            phiNoiseMat.DisableKeyword("UPDATE");
            UpdatePhiNoise();
            phiNoiseMat.EnableKeyword("UPDATE");
        }

        public void UpdatePhiNoise()
        {
            Graphics.Blit(phiNoiseBuffer.Current, phiNoiseBuffer.Other, phiNoiseMat);
            phiNoiseBuffer.Swap();
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
            if (math.distance(transform.position, mainCamera.position) > 5)
            {
                return SetPositionRounded(mainCamera.position);
            }

            return new int2(0, 0);
        }
        
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying)
            {
                float3 pos = transform.pos() - math.float3(probeCounts, 0) / 2f;
                pos.z = 0;
                float4x4 mat = float4x4.TRS(pos, quaternion.identity, math.float3(probeCounts.xy, 1));
                debugMat.SetPass(0);
                Graphics.DrawMeshNow(debugMesh, mat, 0);
                
                //gridOffsetMat.SetPass(0);
                //Graphics.DrawMeshNow(gridMesh, mat, 0);
            }
        }
    }
}

public class MeshGenerator
{
    public static Mesh CreateGridMesh(int2 size)
    {
        var mesh = new Mesh();
        var vertices = new Vector3[(size.x + 1) * (size.y + 1)];
        for (int i = 0, y = 0; y <= size.y; y++)
        for (int x = 0; x <= size.x; x++, i++)
        {
            vertices[i] = (math.float3(x, y, 0) / math.float3(size, 1)).asV3();
        }

        mesh.vertices = vertices;

        var triangles = new int[size.x * size.y * 6];
        for (int ti = 0, vi = 0, y = 0; y < size.y; y++, vi++)
        for (int x = 0; x < size.x; x++, ti += 6, vi++)
        {
            triangles[ti] = vi;
            triangles[ti + 3] = triangles[ti + 2] = vi + 1;
            triangles[ti + 4] = triangles[ti + 1] = vi + size.x + 1;
            triangles[ti + 5] = vi + size.x + 2;
        }


        mesh.triangles = triangles;

        var uvs = new Vector2[vertices.Length];
        var uv2 = new Vector2[vertices.Length]; //xy pos
        for (int i = 0, y = 0; y <= size.y; y++)
        for (int x = 0; x <= size.x; x++, i++)
        {
            vertices[i] = new Vector3(x, y);
            uvs[i] = new Vector2((float) x / size.x, (float) y / size.y);
            uv2[i] = new Vector2(x, y);
        }

        mesh.uv = uvs;
        mesh.uv2 = uv2;

        return mesh;
    }
}