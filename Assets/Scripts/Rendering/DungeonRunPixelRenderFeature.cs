using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>URP RenderGraph-only spatial filter for explicitly opted-in base world cameras.</summary>
public sealed class DungeonRunPixelRenderFeature : ScriptableRendererFeature
{
    // Serialized asset reference also keeps the shader reachable in player builds.
    public Material material;
    private PixelPass pass;

    public override void Create() => pass = new PixelPass();

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var cameraData = renderingData.cameraData;
        if (material == null || material.shader == null || !material.shader.isSupported ||
            cameraData.cameraType != CameraType.Game || cameraData.renderType != CameraRenderType.Base ||
            cameraData.camera.stereoEnabled ||
            !cameraData.camera.TryGetComponent<DungeonRunPixelRenderSettings>(out var settings) ||
            !settings.isActiveAndEnabled || !settings.PixelEnabled)
            return;

        pass.Setup(material);
        renderer.EnqueuePass(pass);
    }

    private sealed class PixelPass : ScriptableRenderPass
    {
        private static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
        private static readonly int Resolution = Shader.PropertyToID("_VirtualResolution");
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private Material material;

        public PixelPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.None);
            profilingSampler = new ProfilingSampler("Dungeon Run Pixel Rendering");
        }

        public void Setup(Material sourceMaterial) => material = sourceMaterial;

        private sealed class PassData
        {
            public TextureHandle source;
            public Material material;
            public Vector4 resolution;
            public MaterialPropertyBlock properties;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var camera = frameData.Get<UniversalCameraData>().camera;
            if (resources.isActiveTargetBackBuffer || !resources.activeColorTexture.IsValid() ||
                !camera.TryGetComponent<DungeonRunPixelRenderSettings>(out var settings) ||
                !settings.isActiveAndEnabled || !settings.PixelEnabled)
                return;

            var source = resources.activeColorTexture;
            var descriptor = renderGraph.GetTextureDesc(source);
            descriptor.name = "DungeonRunPixelColor";
            descriptor.clearBuffer = false;
            descriptor.depthBufferBits = DepthBits.None;
            descriptor.msaaSamples = MSAASamples.None;
            descriptor.filterMode = FilterMode.Point;
            var destination = renderGraph.CreateTexture(descriptor);
            Vector2Int size = settings.VirtualResolution;

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                "Dungeon Run Spatial Pixel Grid", out var data, profilingSampler))
            {
                data.source = source;
                data.material = material;
                data.resolution = new Vector4(size.x, size.y, 1f / size.x, 1f / size.y);
                data.properties = properties;
                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData draw, RasterGraphContext context) =>
                {
                    // Set at execution time: camera-specific values never mutate the shared material.
                    draw.properties.Clear();
                    draw.properties.SetTexture(BlitTexture, draw.source);
                    draw.properties.SetVector(BlitScaleBias, new Vector4(1, 1, 0, 0));
                    draw.properties.SetVector(Resolution, draw.resolution);
                    context.cmd.DrawProcedural(Matrix4x4.identity, draw.material, 0,
                        MeshTopology.Triangles, 3, 1, draw.properties);
                });
            }

            // URP's normal final resolve consumes this handle; no second copy-back is needed.
            resources.cameraColor = destination;
        }
    }
}
