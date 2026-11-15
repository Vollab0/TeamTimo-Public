using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

/// <summary>
/// Extension Cinemachine : empêche la caméra de descendre sous le sol des terrains (relief),
/// sans tenir compte des arbres (contrairement à un raycast sur le TerrainCollider, qui
/// touche aussi les troncs). À poser sur la CinemachineFreeLook, à côté du CinemachineCollider
/// qui gère les murs.
/// </summary>
[AddComponentMenu("Cinemachine/Camera Ground Clearance (TeamTimo)")]
[SaveDuringPlay]
[ExecuteAlways]
[DisallowMultipleComponent]
public class CameraGroundClearance : CinemachineExtension
{
    [Tooltip("Hauteur minimale de la caméra au-dessus du sol du terrain (m)")]
    [SerializeField] private float minHeightAboveGround = 0.8f;

    // Réutilisée à chaque frame : aucune allocation.
    private static readonly List<Terrain> s_Terrains = new List<Terrain>(64);

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body) return;

        Vector3 position = state.CorrectedPosition;
        if (!TryGetGroundHeight(position, out float groundHeight)) return;

        float minY = groundHeight + minHeightAboveGround;
        if (position.y < minY)
            state.PositionCorrection += Vector3.up * (minY - position.y);
    }

    /// <summary>Hauteur du sol du terrain actif sous ce point (false si aucun terrain dessous).</summary>
    private static bool TryGetGroundHeight(Vector3 worldPosition, out float height)
    {
        Terrain.GetActiveTerrains(s_Terrains);
        for (int i = 0; i < s_Terrains.Count; i++)
        {
            Terrain terrain = s_Terrains[i];
            if (terrain == null || terrain.terrainData == null) continue;

            Vector3 origin = terrain.GetPosition();
            Vector3 size = terrain.terrainData.size;
            if (worldPosition.x < origin.x || worldPosition.x > origin.x + size.x) continue;
            if (worldPosition.z < origin.z || worldPosition.z > origin.z + size.z) continue;

            height = origin.y + terrain.SampleHeight(worldPosition);
            return true;
        }

        height = 0f;
        return false;
    }
}
