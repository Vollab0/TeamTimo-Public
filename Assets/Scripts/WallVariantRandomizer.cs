using UnityEngine;

/// <summary>
/// Remplace au démarrage le mesh d'un mur par une variante de <see cref="WallVariantSet"/>
/// (intact, haut cassé, troué...). Le tirage dépend de la position monde du mur : il est
/// reproductible et identique sur toutes les machines, sans synchronisation réseau.
/// À placer sur l'objet qui porte le MeshFilter / MeshRenderer du mur.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WallVariantRandomizer : MonoBehaviour
{
    [Tooltip("Ensemble de variantes à tirer")]
    [SerializeField] private WallVariantSet _variantSet;

    private void Awake()
    {
        if (_variantSet == null) return;

        // Position arrondie au décimètre : stable malgré les imprécisions des flottants.
        Vector3 p = transform.position;
        uint h = Hash((int)Mathf.Round(p.x * 10f), (int)Mathf.Round(p.y * 10f), (int)Mathf.Round(p.z * 10f), _variantSet.Seed);

        Mesh mesh = _variantSet.Pick((h & 0xFFFFFF) / 16777216f);
        if (mesh != null) GetComponent<MeshFilter>().sharedMesh = mesh;

        Material[] mats = _variantSet.Materials;
        if (mats != null && mats.Length > 0) GetComponent<MeshRenderer>().sharedMaterials = mats;

        // Le mesh est centré sur son pivot (axe vertical = Z local du FBX) : un demi-tour échange ses deux faces.
        if (_variantSet.RandomFlip)
        {
            int quarters = _variantSet.QuarterTurns ? (int)((h >> 24) & 3u) : ((h & 0x1000000) != 0 ? 2 : 0);
            if (quarters != 0) transform.Rotate(0f, 0f, 90f * quarters, Space.Self);
        }
    }

    private static uint Hash(int x, int y, int z, int seed)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B1u;
            h ^= (uint)x * 0x85EBCA6Bu; h = (h << 13) | (h >> 19);
            h ^= (uint)y * 0xC2B2AE35u; h = (h << 13) | (h >> 19);
            h ^= (uint)z * 0x27D4EB2Fu;
            h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
            return h;
        }
    }
}
