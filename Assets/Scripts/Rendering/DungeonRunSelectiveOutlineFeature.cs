using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>Selective visible-surface mask and one-cell inner contours, immediately before accepted C480.</summary>
public sealed class DungeonRunSelectiveOutlineFeature : ScriptableRendererFeature
{
    public Material maskMaterial;
    public Material compositeMaterial;
    private OutlinePass pass;

    public override void Create() => pass = new OutlinePass();

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var data = renderingData.cameraData;
        var camera = data.camera;
        if (!maskMaterial || !compositeMaterial || !maskMaterial.shader.isSupported || !compositeMaterial.shader.isSupported ||
            data.cameraType != CameraType.Game || data.renderType != CameraRenderType.Base || camera.stereoEnabled ||
            camera.gameObject.scene.name != "SceneVictorLab" ||
            !camera.TryGetComponent<DungeonRunPixelRenderSettings>(out var pixel) || !pixel.isActiveAndEnabled ||
            !pixel.PixelEnabled || pixel.VirtualResolution != new Vector2Int(480, 270) ||
            !camera.TryGetComponent<DungeonRunSelectiveOutlineSettings>(out var settings) || !settings.isActiveAndEnabled ||
            settings.candidate == DungeonRunSelectiveOutlineSettings.Candidate.O0)
            return;
        pass.Setup(maskMaterial, compositeMaterial, settings);
        renderer.EnqueuePass(pass);
    }

    private sealed class OutlinePass : ScriptableRenderPass
    {
        private Material maskMaterial, compositeMaterial;
        private DungeonRunSelectiveOutlineSettings settings;

        public OutlinePass()
        {
            renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingPostProcessing - 1);
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.None);
        }

        public void Setup(Material mask, Material composite, DungeonRunSelectiveOutlineSettings source)
        { maskMaterial = mask; compositeMaterial = composite; settings = source; }

        private sealed class Draw
        {
            public Mesh mesh;
            public Matrix4x4 matrix;
            public int submesh;
            public MaterialPropertyBlock properties;
        }

        private sealed class MaskData { public Material material; public List<Draw> draws; }
        private sealed class CompositeData
        {
            public TextureHandle source, mask;
            public Material material;
            public Color color;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer || !resources.activeColorTexture.IsValid() ||
                !resources.activeDepthTexture.IsValid() || !settings) return;

            var camera = frameData.Get<UniversalCameraData>().camera;
            var draws = new List<Draw>();
            var seen = new HashSet<MeshRenderer>();
            foreach (var target in settings.targets ?? System.Array.Empty<DungeonRunSelectiveOutlineSettings.Target>())
            {
                if (target == null) continue;
                var renderer = target.renderer;
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer.gameObject.scene != camera.gameObject.scene || !seen.Add(renderer) ||
                    (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0 ||
                    !renderer.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh) continue;
                // This experiment cannot accidentally expand into player, shield, or world selection.
                int allowedSlot = renderer.name == "CHR_Enemy_01" ? 0 : renderer.name == "CHR_Enemy_02" ? 1 :
                    renderer.name == "CHR_Enemy_03" ? 0 : -1;
                if (allowedSlot < 0 || target.submesh != allowedSlot || target.submesh >= filter.sharedMesh.subMeshCount) continue;
                var properties = new MaterialPropertyBlock();
                properties.SetFloat("_SurfaceId", draws.Count + 1);
                properties.SetVector("_Region", new Vector4(target.maximumLocalX, target.maximumLocalY,
                    target.restrictLocalX ? 1 : 0, target.restrictLocalY ? 1 : 0));
                draws.Add(new Draw { mesh = filter.sharedMesh, matrix = renderer.localToWorldMatrix,
                    submesh = target.submesh, properties = properties });
            }
            if (draws.Count == 0) return;

            var source = resources.activeColorTexture;
            var maskDesc = graph.GetTextureDesc(source);
            maskDesc.name = "DungeonRunSelectiveOutlineMask";
            maskDesc.colorFormat = GraphicsFormat.R16G16_SFloat;
            maskDesc.depthBufferBits = DepthBits.None;
            maskDesc.msaaSamples = MSAASamples.None;
            maskDesc.filterMode = FilterMode.Point;
            maskDesc.clearBuffer = true;
            maskDesc.clearColor = Color.clear;
            var mask = graph.CreateTexture(maskDesc);
            using (var builder = graph.AddRasterRenderPass<MaskData>("Dungeon Run Selected Visible Surfaces", out var data))
            {
                data.material = maskMaterial;
                data.draws = draws;
                builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                builder.SetRenderFunc((MaskData draw, RasterGraphContext context) =>
                {
                    foreach (var item in draw.draws)
                        context.cmd.DrawMesh(item.mesh, item.matrix, draw.material, item.submesh, 0, item.properties);
                });
            }
            var outputDesc = graph.GetTextureDesc(source);
            outputDesc.name = "DungeonRunSelectiveOutlineColor";
            outputDesc.depthBufferBits = DepthBits.None;
            outputDesc.msaaSamples = MSAASamples.None;
            outputDesc.clearBuffer = false;
            outputDesc.filterMode = FilterMode.Point;
            var output = graph.CreateTexture(outputDesc);
            using (var builder = graph.AddRasterRenderPass<CompositeData>("Dungeon Run Virtual Grid Inner Outline", out var data))
            {
                data.source = source; data.mask = mask; data.material = compositeMaterial;
                data.color = settings.OutlineColor.linear;
                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(mask, AccessFlags.Read);
                builder.SetRenderAttachment(output, 0, AccessFlags.Write);
                builder.SetRenderFunc((CompositeData draw, RasterGraphContext context) =>
                {
                    var properties = new MaterialPropertyBlock();
                    properties.SetTexture("_BlitTexture", draw.source);
                    properties.SetTexture("_OutlineMask", draw.mask);
                    properties.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
                    properties.SetVector("_VirtualResolution", new Vector4(480, 270, 1f / 480, 1f / 270));
                    // Already converted from sRGB above; SetVector avoids SetColor's additional conversion.
                    properties.SetVector("_OutlineColor", (Vector4)draw.color);
                    context.cmd.DrawProcedural(Matrix4x4.identity, draw.material, 0, MeshTopology.Triangles, 3, 1, properties);
                });
            }
            resources.cameraColor = output;
        }
    }
}
