using System;
using UnityEngine;

/// <summary>
/// Liste des variantes de murs (meshes) tirées au hasard par <see cref="WallVariantRandomizer"/>.
/// Tous les meshes doivent partager le même repère que l'ancien Wall.fbx et les mêmes sous-meshes
/// (0 = lierre, 1 = pierre) pour que <see cref="Materials"/> s'applique à toutes les variantes.
/// </summary>
[CreateAssetMenu(fileName = "WallVariants", menuName = "TeamTimo/Variantes de murs")]
public class WallVariantSet : ScriptableObject
{
    [Serializable]
    public struct Variant
    {
        [Tooltip("Mesh de la variante (sous-mesh 0 = lierre, 1 = pierre)")]
        public Mesh Mesh;
        [Tooltip("Poids relatif du tirage (0 = jamais)")]
        [Min(0f)] public float Weight;
    }

    [Header("Variantes")]
    [SerializeField] private Variant[] _variants = Array.Empty<Variant>();

    [Header("Rendu")]
    [Tooltip("Matériaux appliqués au mur : lierre, pierre, puis Outline (un matériau en trop est rendu sur le dernier sous-mesh, la pierre)")]
    [SerializeField] private Material[] _materials = Array.Empty<Material>();

    [Header("Tirage")]
    [Tooltip("Graine du tirage : la variante dépend uniquement de la position du mur et de cette graine (identique sur toutes les machines)")]
    [SerializeField] private int _seed = 1337;
    [Tooltip("Retourner aléatoirement le mur de 180° pour échanger ses deux faces")]
    [SerializeField] private bool _randomFlip = true;
    [Tooltip("Rotation aléatoire par quarts de tour (0, 90, 180 ou 270°) au lieu du simple demi-tour : pour les piliers à section carrée")]
    [SerializeField] private bool _quarterTurns = false;

    public Material[] Materials => _materials;
    public bool RandomFlip => _randomFlip;
    public bool QuarterTurns => _quarterTurns;
    public int Seed => _seed;

    /// <summary>Choisit une variante à partir d'une valeur aléatoire dans [0,1[.</summary>
    public Mesh Pick(float random01)
    {
        float total = 0f;
        for (int i = 0; i < _variants.Length; i++) total += _variants[i].Weight;
        if (total <= 0f) return null;
        float r = random01 * total;
        for (int i = 0; i < _variants.Length; i++)
        {
            r -= _variants[i].Weight;
            if (r < 0f) return _variants[i].Mesh;
        }
        return _variants[_variants.Length - 1].Mesh;
    }
}
