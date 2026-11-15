using Cinemachine;
using UnityEngine;

/// <summary>
/// Met « en pointillé » la flore (arbres, buissons, herbe) qui se trouve entre la caméra et le
/// joueur local, ainsi que celle collée à l'objectif. Envoie chaque frame la position du joueur
/// aux shaders TeamTimo/StylizedFoliage et TeamTimo/StylizedGrass (code commun :
/// Assets/Art/Shaders/CameraOcclusion.hlsl). À poser sur la Main Camera.
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
    [Tooltip("Rayon de la zone en pointillé autour du joueur, vue depuis la caméra (m)")]
    [SerializeField] private float radius = 4f;
    [Tooltip("Largeur du fondu au bord de la zone (m)")]
    [SerializeField] private float edgeSoftness = 2f;
    [Tooltip("La flore jusqu'à cette distance derrière le centre du joueur est encore en pointillé (m)")]
    [SerializeField] private float depthBehindTarget = 1.5f;

    [Header("Près de la caméra")]
    [Tooltip("Plus près de la caméra que cette distance, la flore disparaît complètement (m)")]
    [SerializeField] private float nearFadeStart = 2f;
    [Tooltip("Au-delà de cette distance de la caméra, la flore redevient pleine (m)")]
    [SerializeField] private float nearFadeEnd = 5f;

    [Header("Rendu")]
    [Tooltip("Part de la flore gardée dans la zone autour du joueur (0 = invisible, 1 = effet coupé)")]
    [Range(0f, 1f)]
    [SerializeField] private float remainingOpacity = 0.25f;
    [Tooltip("Taille des points du motif en pointillé (pixels)")]
    [Range(1, 8)]
    [SerializeField] private int dotSize = 2;

    private static readonly int TargetId = Shader.PropertyToID("_TT_OcclusionTarget");
    private static readonly int ShapeId = Shader.PropertyToID("_TT_OcclusionShape");
    private static readonly int NearId = Shader.PropertyToID("_TT_OcclusionNear");

    private void Awake()
    {
        if (freeLook == null) freeLook = FindFirstObjectByType<CinemachineFreeLook>();
    }

    private void LateUpdate()
    {
        Transform target = freeLook != null ? freeLook.Follow : null;
        if (target == null)
        {
            Shader.SetGlobalVector(TargetId, Vector4.zero);
            return;
        }

        Vector3 center = target.position + Vector3.up * targetHeight;
        Shader.SetGlobalVector(TargetId, new Vector4(center.x, center.y, center.z, 1f));
        Shader.SetGlobalVector(ShapeId, new Vector4(radius, Mathf.Max(edgeSoftness, 0.01f), depthBehindTarget, remainingOpacity));
        Shader.SetGlobalVector(NearId, new Vector4(nearFadeStart, Mathf.Max(nearFadeEnd, nearFadeStart + 0.01f), dotSize, 0f));
    }

    private void OnDisable()
    {
        // Coupe l'effet (sinon la scène garderait la flore trouée après le Play Mode).
        Shader.SetGlobalVector(TargetId, Vector4.zero);
    }
}
