using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Rend transparente (fondu) la flore (arbres, buissons, herbe) qui se trouve entre la caméra et le
/// joueur local, ainsi que celle collée à l'objectif. Envoie chaque frame la position du joueur
/// aux shaders TeamTimo/StylizedFoliage, StylizedGrass et StylizedBark (code commun :
/// Assets/Art/Shaders/CameraOcclusion.hlsl), et ajoute au rendu URP la passe qui dessine cette
/// flore en transparence après les objets transparents. À poser sur la Main Camera.
/// Les ombres de la flore ne changent pas.
/// </summary>
[DisallowMultipleComponent]
public class CameraFoliageOcclusion : MonoBehaviour
{
    [Header("Cible")]
    [Tooltip("Caméra 3e personne dont on suit la cible (Follow = joueur local). Vide = trouvée au démarrage")]
    [SerializeField] private CinemachineFreeLook freeLook;
    [Tooltip("Hauteur du centre du joueur au-dessus de ses pieds (m) ; le joueur fait ~5,2 m")]
    [SerializeField] private float targetHeight = 2.6f;

    [Header("Zone dégagée autour du joueur")]
    [Tooltip("Rayon de la zone transparente autour du joueur, vue depuis la caméra (m)")]
    [SerializeField] private float radius = 4f;
    [Tooltip("Largeur du fondu au bord de la zone (m)")]
    [SerializeField] private float edgeSoftness = 2f;
    [Tooltip("La flore jusqu'à cette distance derrière le centre du joueur est encore transparente (m)")]
    [SerializeField] private float depthBehindTarget = 1.5f;

    [Header("Près de la caméra")]
    [Tooltip("Plus près de la caméra que cette distance, la flore disparaît complètement (m)")]
    [SerializeField] private float nearFadeStart = 2f;
    [Tooltip("Au-delà de cette distance de la caméra, la flore redevient pleine (m)")]
    [SerializeField] private float nearFadeEnd = 5f;

    [Header("Rendu")]
    [Tooltip("Opacité de la flore dans la zone autour du joueur (0 = invisible, 1 = effet coupé)")]
    [Range(0f, 1f)]
    [SerializeField] private float remainingOpacity = 0.25f;

    private static readonly int TargetId = Shader.PropertyToID("_TT_OcclusionTarget");
    private static readonly int ShapeId = Shader.PropertyToID("_TT_OcclusionShape");
    private static readonly int NearId = Shader.PropertyToID("_TT_OcclusionNear");

    private FoliageFadePass _fadePass;
    private bool _active;
    // Données URP de chaque caméra rencontrée (évite un GetComponent à chaque frame).
    private readonly Dictionary<Camera, UniversalAdditionalCameraData> _cameraData = new Dictionary<Camera, UniversalAdditionalCameraData>();

    private void Awake()
    {
        if (freeLook == null) freeLook = FindFirstObjectByType<CinemachineFreeLook>();
        _fadePass = new FoliageFadePass();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        // Coupe l'effet (sinon la scène garderait la flore trouée après le Play Mode).
        _active = false;
        Shader.SetGlobalVector(TargetId, Vector4.zero);
    }

    private void LateUpdate()
    {
        Transform target = freeLook != null ? freeLook.Follow : null;
        _active = target != null;
        if (!_active)
        {
            Shader.SetGlobalVector(TargetId, Vector4.zero);
            return;
        }

        Vector3 center = target.position + Vector3.up * targetHeight;
        Shader.SetGlobalVector(TargetId, new Vector4(center.x, center.y, center.z, 1f));
        Shader.SetGlobalVector(ShapeId, new Vector4(radius, Mathf.Max(edgeSoftness, 0.01f), depthBehindTarget, remainingOpacity));
        Shader.SetGlobalVector(NearId, new Vector4(nearFadeStart, Mathf.Max(nearFadeEnd, nearFadeStart + 0.01f), 0f, 0f));
    }

    /// <summary>
    /// Les passes opaques retirent la flore de la zone : il faut la redessiner en transparence pour
    /// chaque caméra qui affiche le jeu (vue Game, et vue Scene pendant le Play Mode).
    /// </summary>
    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (!_active) return;
        if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;

        if (!_cameraData.TryGetValue(cam, out UniversalAdditionalCameraData data) || data == null)
        {
            data = cam.GetUniversalAdditionalCameraData();
            _cameraData[cam] = data;
        }
        ScriptableRenderer renderer = data != null ? data.scriptableRenderer : null;
        if (renderer != null) renderer.EnqueuePass(_fadePass);
    }

    /// <summary>
    /// Dessine la flore en fondu, après les transparents (le joueur, le décor et la fumée qui sont
    /// derrière sont déjà dessinés) :
    /// 1) flore du cône caméra -> joueur : sa profondeur (passe « TeamTimoFadeDepth » des shaders, pour
    ///    ne garder que la surface la plus proche), puis sa couleur mélangée (« TeamTimoFade ») ;
    /// 2) flore près de l'objectif (« TeamTimoFadeNear ») : de l'arrière vers l'avant, sans profondeur,
    ///    pour qu'elle ne cache jamais la flore du cône derrière elle.
    /// </summary>
    private sealed class FoliageFadePass : ScriptableRenderPass
    {
        private const string PassName = "TeamTimo Foliage Fade";
        private static readonly ShaderTagId DepthTag = new ShaderTagId("TeamTimoFadeDepth");
        private static readonly ShaderTagId ColorTag = new ShaderTagId("TeamTimoFade");
        private static readonly ShaderTagId NearTag = new ShaderTagId("TeamTimoFadeNear");

        private class PassData
        {
            public RendererListHandle depthList;
            public RendererListHandle colorList;
            public RendererListHandle nearList;
        }

        public FoliageFadePass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            profilingSampler = new ProfilingSampler(PassName);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            UniversalRenderingData rendering = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(PassName, out PassData passData, profilingSampler))
            {
                FilteringSettings filtering = new FilteringSettings(RenderQueueRange.opaque);
                DrawingSettings depthDrawing = RenderingUtils.CreateDrawingSettings(DepthTag, rendering, cameraData, lightData, SortingCriteria.CommonOpaque);
                DrawingSettings colorDrawing = RenderingUtils.CreateDrawingSettings(ColorTag, rendering, cameraData, lightData, SortingCriteria.CommonOpaque);
                passData.depthList = renderGraph.CreateRendererList(new RendererListParams(rendering.cullResults, depthDrawing, filtering));
                DrawingSettings nearDrawing = RenderingUtils.CreateDrawingSettings(NearTag, rendering, cameraData, lightData, SortingCriteria.CommonTransparent);
                passData.colorList = renderGraph.CreateRendererList(new RendererListParams(rendering.cullResults, colorDrawing, filtering));
                passData.nearList = renderGraph.CreateRendererList(new RendererListParams(rendering.cullResults, nearDrawing, filtering));

                builder.UseRendererList(passData.depthList);
                builder.UseRendererList(passData.colorList);
                builder.UseRendererList(passData.nearList);
                builder.SetRenderAttachment(resources.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
                builder.UseAllGlobalTextures(true); // ombres, SSAO, lumières
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.depthList);
                    context.cmd.DrawRendererList(data.colorList);
                    context.cmd.DrawRendererList(data.nearList);
                });
            }
        }
    }
}
