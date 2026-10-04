using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>One world-only palette pass after accepted C480, before final resolve/native overlay UI.</summary>
public sealed class DungeonRunPaletteFeature : ScriptableRendererFeature
{
    public Material material;
    private PalettePass pass;
    public override void Create() => pass = new PalettePass();

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var data = renderingData.cameraData;
        var camera = data.camera;
        if (!material || !material.shader || !material.shader.isSupported || data.cameraType != CameraType.Game ||
            data.renderType != CameraRenderType.Base || camera.stereoEnabled || camera.orthographic ||
            !DungeonRun.Rendering.WorldStyleGate.IsStyledScene(camera.gameObject.scene.name) ||
            !camera.TryGetComponent<DungeonRunPixelRenderSettings>(out var pixel) || !pixel.isActiveAndEnabled ||
            !pixel.PixelEnabled || pixel.VirtualResolution != new Vector2Int(480, 270) ||
            !camera.TryGetComponent<DungeonRunPaletteSettings>(out var settings) || !settings.isActiveAndEnabled ||
            settings.candidate == DungeonRunPaletteSettings.Candidate.P0 ||
            (camera.TryGetComponent<DungeonRunSelectiveOutlineSettings>(out var outlines) && outlines.isActiveAndEnabled &&
             outlines.candidate != DungeonRunSelectiveOutlineSettings.Candidate.O0)) return;
        pass.Setup(material, settings);
        renderer.EnqueuePass(pass);
    }

    private sealed class PalettePass : ScriptableRenderPass
    {
        private Material material;
        private DungeonRunPaletteSettings settings;
        public PalettePass()
        {
            renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingPostProcessing + 1);
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }
        public void Setup(Material source, DungeonRunPaletteSettings options) { material = source; settings = options; }
        private sealed class PassData
        {
            public TextureHandle source, depth;
            public Material material;
            public MaterialPropertyBlock properties;
        }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (!settings || resources.isActiveTargetBackBuffer || !resources.activeColorTexture.IsValid() ||
                !resources.cameraDepthTexture.IsValid()) return;
            var treatment = settings.ActiveTreatment;
            if (treatment == null) return;
            var source = resources.activeColorTexture;
            var descriptor = graph.GetTextureDesc(source);
            descriptor.name = "DungeonRunPaletteColor";
            descriptor.clearBuffer = false;
            descriptor.depthBufferBits = DepthBits.None;
            descriptor.msaaSamples = MSAASamples.None;
            descriptor.filterMode = FilterMode.Point;
            var destination = graph.CreateTexture(descriptor);
            var properties = new MaterialPropertyBlock();
            properties.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
            properties.SetVector("_VirtualResolution", new Vector4(480, 270, 1f / 480, 1f / 270));
            properties.SetVector("_Treatment", new Vector4(Mathf.Clamp01(treatment.hueGrouping),
                Mathf.Clamp01(treatment.chromaRetention), Mathf.Clamp01(treatment.backgroundChromaRetention),
                Mathf.Clamp(treatment.familySharpness, 1, 20)));
            properties.SetVector("_DepthAccent", new Vector4(settings.backgroundStart,
                Mathf.Max(settings.backgroundStart + .01f, settings.backgroundEnd), settings.accentStart,
                Mathf.Max(settings.accentStart + .0001f, settings.accentEnd)));
            properties.SetFloat("_AccentProtection", Mathf.Clamp01(settings.accentProtection));
            // SetVector transfers our explicitly linearized colors without a second color conversion.
            properties.SetVector("_ColdStone", (Vector4)settings.coldStone.linear);
            properties.SetVector("_Amber", (Vector4)settings.amber.linear);
            properties.SetVector("_Jade", (Vector4)settings.jade.linear);
            properties.SetVector("_Terracotta", (Vector4)settings.terracotta.linear);
            properties.SetVector("_Bone", (Vector4)settings.bone.linear);
            properties.SetVector("_Cyan", (Vector4)settings.cyan.linear);
            using (var builder = graph.AddRasterRenderPass<PassData>("Dungeon Run Luminance-Preserving Palette", out var data))
            {
                data.source = source; data.depth = resources.cameraDepthTexture;
                data.material = material; data.properties = properties;
                builder.UseTexture(data.source, AccessFlags.Read);
                builder.UseTexture(data.depth, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData draw, RasterGraphContext context) =>
                {
                    draw.properties.SetTexture("_BlitTexture", draw.source);
                    draw.properties.SetTexture("_PaletteDepth", draw.depth);
                    context.cmd.DrawProcedural(Matrix4x4.identity, draw.material, 0, MeshTopology.Triangles, 3, 1, draw.properties);
                });
            }
            resources.cameraColor = destination;
        }
    }
}
