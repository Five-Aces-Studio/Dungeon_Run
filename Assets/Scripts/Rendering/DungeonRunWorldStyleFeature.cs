using DungeonRun.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Acrylic + Subtle Pixel V3 fullscreen pass: optional screen-space paint filter (half-res anisotropic Kuwahara,
/// depth-aware composite), then pixel accent + palette cohesion + atmosphere/value structure/grade. Also pushes the
/// painted-flame globals (flame clock, glow pools) for acrylic materials and flame shaders.
/// </summary>
public sealed class DungeonRunWorldStyleFeature : ScriptableRendererFeature
{
    public const int MaxPools = 8;

    public Material material;
    private WorldStylePass pass;

    // Always length MaxPools: Unity fixes a global array's size on its first SetGlobalVectorArray.
    private static readonly Vector4[] PoolPositions = new Vector4[MaxPools];
    private static readonly Vector4[] PoolColors = new Vector4[MaxPools];

    public override void Create() => pass = new WorldStylePass();

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var data = renderingData.cameraData;
        var camera = data.camera;

        // Flame clock for every camera/scene: captures freeze the flame shader through this override.
        float? flameOverride = DungeonRunFlameClock.OverrideTime;
        Shader.SetGlobalVector("_DR_FlameTime", flameOverride.HasValue
            ? new Vector4(1f, flameOverride.Value, 0f, 0f)
            : Vector4.zero);

        camera.TryGetComponent<DungeonRunWorldStyleSettings>(out var worldStyle);
        camera.TryGetComponent<DungeonRunPixelRenderSettings>(out var pixel);
        bool legacyPixelEnabled = pixel != null && pixel.isActiveAndEnabled && pixel.PixelEnabled;
        bool settingsActive = worldStyle != null && worldStyle.isActiveAndEnabled;
        var profile = settingsActive ? worldStyle.profile : null;
        bool hasProfile = profile != null;
        bool acrylicLook = hasProfile && profile.sceneLook == DungeonRunWorldStyleProfile.SceneLook.Acrylic;

        bool shouldRender = WorldStyleGate.ShouldRender(camera.gameObject.scene.name,
            data.cameraType == CameraType.Game, data.renderType == CameraRenderType.Base, camera.stereoEnabled,
            camera.orthographic, settingsActive, hasProfile, acrylicLook, legacyPixelEnabled) &&
            material != null && material.shader != null && material.shader.isSupported;

        // Push painterly globals for THIS camera unconditionally: acrylic materials must degrade to neutral
        // whenever the World Style pass is not the active look for this camera (e.g. SceneVictor).
        if (shouldRender)
        {
            var globals = profile.GetGlobals();
            Shader.SetGlobalVector("_DR_Painterly", new Vector4(globals.Painterly.X, globals.Painterly.Y, globals.Painterly.Z, globals.Painterly.W));
            Shader.SetGlobalVector("_DR_Brush", new Vector4(globals.Brush.X, globals.Brush.Y, globals.Brush.Z, globals.Brush.W));
            var cool = profile.coolShadowColor.linear;
            Shader.SetGlobalVector("_DR_CoolColor", new Vector4(cool.r, cool.g, cool.b, 1f));
            Shader.SetGlobalVector("_DR_Light", new Vector4(
                Mathf.Clamp(profile.lightHeadroom, 0f, 2f) * Mathf.Clamp01(profile.painterlyInfluence), 0f, 0f, 0f));
            PushGlowPools(camera, profile);
        }
        else
        {
            Shader.SetGlobalVector("_DR_Painterly", Vector4.zero);
            Shader.SetGlobalVector("_DR_Brush", Vector4.zero);
            Shader.SetGlobalVector("_DR_CoolColor", Vector4.zero);
            Shader.SetGlobalVector("_DR_Light", Vector4.zero);
            Shader.SetGlobalVector("_DR_PoolParams", Vector4.zero); // pool arrays may keep stale values; count 0 ignores them
        }

        if (!shouldRender) return;

        camera.TryGetComponent<DungeonRunPaletteSettings>(out var paletteSettings);
        pass.renderPassEvent = profile.stage == DungeonRunWorldStyleProfile.Stage.BeforePostProcessing
            ? RenderPassEvent.BeforeRenderingPostProcessing
            : RenderPassEvent.AfterRenderingPostProcessing;
        pass.Setup(material, profile, paletteSettings);
        renderer.EnqueuePass(pass);
    }

    // Painted glow pools (decoupled from physical light falloff) from the active flame lights of this camera's scene.
    private static void PushGlowPools(Camera camera, DungeonRunWorldStyleProfile profile)
    {
        var scene = camera.gameObject.scene;
        var flames = DungeonRunFlameLight.Active;
        int count = 0;
        for (int i = 0; i < flames.Count && count < MaxPools; i++)
        {
            var flame = flames[i];
            if (flame == null || !flame.isActiveAndEnabled || flame.gameObject.scene != scene) continue;
            Vector3 position = flame.PoolWorldPosition;
            Color color = flame.poolColor.linear;
            float scale = flame.poolIntensity * flame.CurrentFlicker;
            PoolPositions[count] = new Vector4(position.x, position.y, position.z, Mathf.Max(flame.poolRadius, .01f));
            PoolColors[count] = new Vector4(color.r * scale, color.g * scale, color.b * scale, 0f);
            count++;
        }
        for (int i = count; i < MaxPools; i++)
        {
            PoolPositions[i] = new Vector4(0f, 0f, 0f, 1f);
            PoolColors[i] = Vector4.zero;
        }
        Shader.SetGlobalVectorArray("_DR_PoolPos", PoolPositions);
        Shader.SetGlobalVectorArray("_DR_PoolColor", PoolColors);
        Shader.SetGlobalVector("_DR_PoolParams", new Vector4(count,
            Mathf.Max(profile.poolStrength, 0f) * Mathf.Clamp01(profile.painterlyInfluence),
            Mathf.Clamp01(profile.poolBreakup), Mathf.Max(profile.poolSteps, 0)));
    }

    private sealed class WorldStylePass : ScriptableRenderPass
    {
        // SH_DungeonRun_WorldStyle pass indices.
        private const int WorldStylePassIndex = 0;
        private const int PaintDownsamplePassIndex = 1;
        private const int PaintTensorPassIndex = 2;
        private const int PaintTensorBlurPassIndex = 3;
        private const int PaintKuwaharaPassIndex = 4;
        private const int PaintCompositePassIndex = 5;
        private const int NoTexture = -1;

        private static readonly ProfilingSampler Sampler = new ProfilingSampler("Dungeon Run World Style");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int WorldStyleDepthId = Shader.PropertyToID("_WorldStyleDepth");
        private static readonly int PaintSrcId = Shader.PropertyToID("_WS_PaintSrc");
        private static readonly int TensorSrcId = Shader.PropertyToID("_WS_TensorSrc");
        private static readonly int TensorId = Shader.PropertyToID("_WS_Tensor");
        private static readonly int PaintedId = Shader.PropertyToID("_WS_Painted");
        private static readonly int HalfTexelId = Shader.PropertyToID("_WS_HalfTexel");
        private static readonly int BlurDirId = Shader.PropertyToID("_WS_BlurDir");
        private static readonly int KuwaharaId = Shader.PropertyToID("_WS_Kuwahara");
        private static readonly int PaintId = Shader.PropertyToID("_WS_Paint");

        // One property block per raster pass: render funcs run after the whole graph is recorded, so a shared
        // block would be overwritten by the last recorded pass.
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock downsampleProperties = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock tensorProperties = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock blurHProperties = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock blurVProperties = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock kuwaharaProperties = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock compositeProperties = new MaterialPropertyBlock();
        private Material material;
        private DungeonRunWorldStyleProfile profile;
        private DungeonRunPaletteSettings paletteSettings;

        public WorldStylePass()
        {
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Depth);
            profilingSampler = Sampler;
        }

        public void Setup(Material sourceMaterial, DungeonRunWorldStyleProfile sourceProfile,
            DungeonRunPaletteSettings sourcePaletteSettings)
        {
            material = sourceMaterial;
            profile = sourceProfile;
            paletteSettings = sourcePaletteSettings;
        }

        private sealed class PassData
        {
            public Material material;
            public MaterialPropertyBlock properties;
            public int passIndex;
            public int textureCount;
            public readonly int[] textureIds = new int[3];
            public readonly TextureHandle[] textures = new TextureHandle[3];
        }

        // Records one fullscreen triangle into `destination`; every read texture is declared with UseTexture and
        // bound inside the render func (handles only resolve at execution time).
        private void AddFullscreenPass(RenderGraph graph, string passName, int passIndex, MaterialPropertyBlock block,
            TextureHandle destination, int idA, TextureHandle texA, int idB = NoTexture, TextureHandle texB = default,
            int idC = NoTexture, TextureHandle texC = default)
        {
            using (var builder = graph.AddRasterRenderPass<PassData>(passName, out var data))
            {
                data.material = material;
                data.properties = block;
                data.passIndex = passIndex;
                data.textureCount = 0;
                AddRead(builder, data, idA, texA);
                AddRead(builder, data, idB, texB);
                AddRead(builder, data, idC, texC);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData draw, RasterGraphContext context) =>
                {
                    for (int i = 0; i < draw.textureCount; i++)
                        draw.properties.SetTexture(draw.textureIds[i], draw.textures[i]);
                    context.cmd.DrawProcedural(Matrix4x4.identity, draw.material, draw.passIndex,
                        MeshTopology.Triangles, 3, 1, draw.properties);
                });
            }
        }

        private static void AddRead(IRasterRenderGraphBuilder builder, PassData data, int id, TextureHandle texture)
        {
            if (id == NoTexture) return;
            builder.UseTexture(texture, AccessFlags.Read);
            data.textureIds[data.textureCount] = id;
            data.textures[data.textureCount] = texture;
            data.textureCount++;
        }

        private static MaterialPropertyBlock ResetBlock(MaterialPropertyBlock block)
        {
            block.Clear();
            block.SetVector(BlitScaleBiasId, new Vector4(1, 1, 0, 0));
            return block;
        }

        // Half-res paint chain (downsample -> structure tensor -> separable blur -> anisotropic Kuwahara) and the
        // full-res depth-aware composite. Returns the composite, which replaces the raw source for pass 0.
        private TextureHandle RecordPaintFilter(RenderGraph graph, TextureHandle source, TextureHandle depth,
            TextureDesc sourceDescriptor)
        {
            int fullWidth = sourceDescriptor.width, fullHeight = sourceDescriptor.height;
            int halfWidth = PaintFilterMath.HalfSize(fullWidth), halfHeight = PaintFilterMath.HalfSize(fullHeight);
            var halfTexel = new Vector4(1f / halfWidth, 1f / halfHeight, halfWidth, halfHeight);

            var halfDescriptor = new TextureDesc(halfWidth, halfHeight)
            {
                format = GraphicsFormat.R16G16B16A16_SFloat,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                msaaSamples = MSAASamples.None,
                useMipMap = false,
                clearBuffer = false
            };
            halfDescriptor.name = "DungeonRunPaintSource";
            var paintSource = graph.CreateTexture(halfDescriptor);
            halfDescriptor.name = "DungeonRunPaintTensor";
            var tensor = graph.CreateTexture(halfDescriptor);
            halfDescriptor.name = "DungeonRunPaintTensorBlurH";
            var tensorBlurH = graph.CreateTexture(halfDescriptor);
            halfDescriptor.name = "DungeonRunPaintTensorBlurV";
            var tensorBlurV = graph.CreateTexture(halfDescriptor);
            halfDescriptor.name = "DungeonRunPainted";
            var painted = graph.CreateTexture(halfDescriptor);

            var compositeDescriptor = sourceDescriptor;
            compositeDescriptor.name = "DungeonRunPaintComposite";
            compositeDescriptor.clearBuffer = false;
            compositeDescriptor.depthBufferBits = DepthBits.None;
            compositeDescriptor.msaaSamples = MSAASamples.None;
            compositeDescriptor.filterMode = FilterMode.Point;
            var composite = graph.CreateTexture(compositeDescriptor);

            float radius = PaintFilterMath.KuwaharaRadius(profile.paintRadius, fullHeight, true);

            ResetBlock(downsampleProperties);
            AddFullscreenPass(graph, "Dungeon Run Paint Downsample", PaintDownsamplePassIndex, downsampleProperties,
                paintSource, BlitTextureId, source, WorldStyleDepthId, depth);

            ResetBlock(tensorProperties).SetVector(HalfTexelId, halfTexel);
            AddFullscreenPass(graph, "Dungeon Run Paint Tensor", PaintTensorPassIndex, tensorProperties,
                tensor, PaintSrcId, paintSource);

            ResetBlock(blurHProperties).SetVector(HalfTexelId, halfTexel);
            blurHProperties.SetVector(BlurDirId, new Vector4(1, 0, 0, 0));
            AddFullscreenPass(graph, "Dungeon Run Paint Tensor Blur H", PaintTensorBlurPassIndex, blurHProperties,
                tensorBlurH, TensorSrcId, tensor);

            ResetBlock(blurVProperties).SetVector(HalfTexelId, halfTexel);
            blurVProperties.SetVector(BlurDirId, new Vector4(0, 1, 0, 0));
            AddFullscreenPass(graph, "Dungeon Run Paint Tensor Blur V", PaintTensorBlurPassIndex, blurVProperties,
                tensorBlurV, TensorSrcId, tensorBlurH);

            ResetBlock(kuwaharaProperties).SetVector(HalfTexelId, halfTexel);
            kuwaharaProperties.SetVector(KuwaharaId, new Vector4(radius, Mathf.Max(profile.paintSharpness, 1f),
                Mathf.Max(profile.paintAnisotropy, .01f), 0f));
            AddFullscreenPass(graph, "Dungeon Run Paint Kuwahara", PaintKuwaharaPassIndex, kuwaharaProperties,
                painted, PaintSrcId, paintSource, TensorId, tensorBlurV);

            ResetBlock(compositeProperties).SetVector(HalfTexelId, halfTexel);
            compositeProperties.SetVector(PaintId, new Vector4(Mathf.Clamp01(profile.paintStrength),
                Mathf.Max(profile.paintDepthSigma, .01f), 0f, 0f));
            AddFullscreenPass(graph, "Dungeon Run Paint Composite", PaintCompositePassIndex, compositeProperties,
                composite, BlitTextureId, source, WorldStyleDepthId, depth, PaintedId, painted);

            return composite;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (profile == null || resources.isActiveTargetBackBuffer || !resources.activeColorTexture.IsValid() ||
                !resources.cameraDepthTexture.IsValid()) return;

            var source = resources.activeColorTexture;
            var sourceDescriptor = graph.GetTextureDesc(source);
            var descriptor = sourceDescriptor;
            descriptor.name = "DungeonRunWorldStyleColor";
            descriptor.clearBuffer = false;
            descriptor.depthBufferBits = DepthBits.None;
            descriptor.msaaSamples = MSAASamples.None;
            descriptor.filterMode = FilterMode.Point;
            var destination = graph.CreateTexture(descriptor);

            // Paint filter off (strength ~0): pass 0 reads the raw source exactly as before, with no extra passes.
            var styleSource = profile.paintStrength > 0.001f
                ? RecordPaintFilter(graph, source, resources.cameraDepthTexture, sourceDescriptor)
                : source;

            int cellPx = WorldStyleGrid.CellPixels(profile.pixelScale, descriptor.height);

            properties.Clear();
            properties.SetVector(BlitScaleBiasId, new Vector4(1, 1, 0, 0));
            properties.SetVector("_WS_Grid", new Vector4(cellPx, 1f / cellPx, descriptor.width, descriptor.height));
            properties.SetVector("_WS_Pixel", new Vector4(Mathf.Clamp01(profile.pixelInfluence),
                Mathf.Clamp01(profile.pixelEdgeStrength), Mathf.Clamp01(profile.cellFilter),
                Mathf.Max(profile.edgeDepthThreshold, 0.001f)));
            properties.SetVector("_WS_Color", new Vector4(Mathf.Clamp01(profile.paletteStrength), profile.saturation,
                profile.contrast, profile.shadowLift));
            properties.SetVector("_WS_Quant", new Vector4(Mathf.Clamp01(profile.quantizationStrength),
                profile.colorLevels, Mathf.Clamp01(profile.ditherStrength), Mathf.Clamp01(profile.glowProtection)));
            properties.SetVector("_WS_Atmos", new Vector4(Mathf.Clamp01(profile.distanceDesaturation),
                Mathf.Clamp01(profile.hazeStrength), RenderSettings.fogStartDistance, RenderSettings.fogEndDistance));
            properties.SetVector("_WS_FogColor", (Vector4)RenderSettings.fogColor.linear);
            properties.SetVector("_WS_CoolColor", (Vector4)profile.coolShadowColor.linear);

            // Value structure (painter's vignette) and painted silhouette rim; strength 0 = pass 0 unchanged.
            properties.SetVector("_WS_Focus", new Vector4(Mathf.Clamp01(profile.focusStrength),
                Mathf.Clamp01(profile.focusCentreY), Mathf.Max(profile.focusRadius.x, .01f),
                Mathf.Max(profile.focusRadius.y, .01f)));
            properties.SetVector("_WS_FocusParams", new Vector4(Mathf.Clamp(profile.focusSoftness, .01f, 1f), 0f, 0f, 0f));
            Vector2 rimDirection = profile.rimDirection.normalized;
            float rimStrength = rimDirection.sqrMagnitude > 0f ? Mathf.Clamp01(profile.rimStrength) : 0f;
            properties.SetVector("_WS_Rim", new Vector4(rimStrength, Mathf.Max(profile.rimWidth, .01f),
                Mathf.Clamp(profile.rimDepthThreshold, .02f, 1f), 0f));
            properties.SetVector("_WS_RimDir", new Vector4(rimDirection.x, rimDirection.y, 0f, 0f));
            properties.SetVector("_WS_RimColor", (Vector4)profile.rimColor.linear);
            float rimFullDistance = Mathf.Max(profile.rimFullDistance, 0f);
            properties.SetVector("_WS_RimDistance", new Vector4(rimFullDistance,
                Mathf.Max(profile.rimMaxDistance, rimFullDistance + .01f), 0f, 0f));

            // Palette values from the existing DungeonRunPaletteSettings component; no duplicated palette params.
            var treatment = paletteSettings != null ? paletteSettings.ActiveTreatment : null;
            if (treatment != null)
            {
                properties.SetVector("_Treatment", new Vector4(Mathf.Clamp01(treatment.hueGrouping),
                    Mathf.Clamp01(treatment.chromaRetention), Mathf.Clamp01(treatment.backgroundChromaRetention),
                    Mathf.Clamp(treatment.familySharpness, 1, 20)));
                properties.SetVector("_DepthAccent", new Vector4(paletteSettings.backgroundStart,
                    Mathf.Max(paletteSettings.backgroundStart + .01f, paletteSettings.backgroundEnd),
                    paletteSettings.accentStart, Mathf.Max(paletteSettings.accentStart + .0001f, paletteSettings.accentEnd)));
                properties.SetFloat("_AccentProtection", Mathf.Clamp01(paletteSettings.accentProtection));
                properties.SetVector("_ColdStone", (Vector4)paletteSettings.coldStone.linear);
                properties.SetVector("_Amber", (Vector4)paletteSettings.amber.linear);
                properties.SetVector("_Jade", (Vector4)paletteSettings.jade.linear);
                properties.SetVector("_Terracotta", (Vector4)paletteSettings.terracotta.linear);
                properties.SetVector("_Bone", (Vector4)paletteSettings.bone.linear);
                properties.SetVector("_Cyan", (Vector4)paletteSettings.cyan.linear);
            }
            else
            {
                // No palette component: force paletteStrength 0 in _WS_Color and leave the rest neutral.
                properties.SetVector("_WS_Color", new Vector4(0, profile.saturation, profile.contrast, profile.shadowLift));
                properties.SetVector("_Treatment", Vector4.zero);
                properties.SetVector("_DepthAccent", new Vector4(0, 1, 0, 1));
                properties.SetFloat("_AccentProtection", 0f);
                properties.SetVector("_ColdStone", Vector4.zero);
                properties.SetVector("_Amber", Vector4.zero);
                properties.SetVector("_Jade", Vector4.zero);
                properties.SetVector("_Terracotta", Vector4.zero);
                properties.SetVector("_Bone", Vector4.zero);
                properties.SetVector("_Cyan", Vector4.zero);
            }

            AddFullscreenPass(graph, "Dungeon Run World Style", WorldStylePassIndex, properties, destination,
                BlitTextureId, styleSource, WorldStyleDepthId, resources.cameraDepthTexture);
            resources.cameraColor = destination;
        }
    }
}
