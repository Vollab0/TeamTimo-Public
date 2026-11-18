using UnityEngine;

/// <summary>
/// Zone du Lobby, interdite aux monstres : rectangle (monde, plan XZ) des cellules du Lobby, renseigné par
/// MazeNavMeshBuilder à chaque calcul du NavMesh. Le NavMesh garantit déjà qu'un agent ne s'y rend pas
/// (aire « Lobby » hors du masque des monstres) ; cette classe couvre les déplacements hors NavMesh
/// (charge du taureau) et sert de garde-fou aux apparitions et aux recollages sur le NavMesh.
/// </summary>
public static class LobbyZone
{
    private static Rect s_Rect;

    /// <summary>Vrai une fois le rectangle renseigné (sinon aucune restriction).</summary>
    public static bool IsSet { get; private set; }

    /// <summary>Renseigne le rectangle du Lobby (centre et taille monde, plan XZ).</summary>
    public static void SetBounds(Vector3 center, Vector2 size)
    {
        s_Rect = new Rect(center.x - size.x / 2f, center.z - size.y / 2f, size.x, size.y);
        IsSet = true;
    }

    /// <summary>Vrai si le point (plan XZ) est dans le Lobby agrandi de margin mètres.</summary>
    public static bool Contains(Vector3 position, float margin = 0f)
    {
        if (!IsSet) return false;
        return position.x > s_Rect.xMin - margin && position.x < s_Rect.xMax + margin
            && position.z > s_Rect.yMin - margin && position.z < s_Rect.yMax + margin;
    }

    /// <summary>
    /// Distance (plan XZ) qu'on peut parcourir depuis origin dans la direction dir avant d'entrer dans le Lobby
    /// agrandi de margin mètres. float.MaxValue si le trajet n'y entre jamais, 0 si on y est déjà.
    /// </summary>
    public static float DistanceBeforeEntering(Vector3 origin, Vector3 dir, float margin = 0f)
    {
        if (!IsSet) return float.MaxValue;
        if (Contains(origin, margin)) return 0f;
        float xMin = s_Rect.xMin - margin, xMax = s_Rect.xMax + margin;
        float zMin = s_Rect.yMin - margin, zMax = s_Rect.yMax + margin;
        // Intersection rayon / rectangle (méthode des « dalles »).
        float tEnter = 0f, tExit = float.MaxValue;
        if (!Slab(origin.x, dir.x, xMin, xMax, ref tEnter, ref tExit)) return float.MaxValue;
        if (!Slab(origin.z, dir.z, zMin, zMax, ref tEnter, ref tExit)) return float.MaxValue;
        float planar = new Vector2(dir.x, dir.z).magnitude;
        return planar > 1e-5f ? tEnter * planar : float.MaxValue;
    }

    private static bool Slab(float o, float d, float min, float max, ref float tEnter, ref float tExit)
    {
        if (Mathf.Abs(d) < 1e-6f) return o > min && o < max;
        float t1 = (min - o) / d, t2 = (max - o) / d;
        if (t1 > t2) (t1, t2) = (t2, t1);
        tEnter = Mathf.Max(tEnter, t1);
        tExit = Mathf.Min(tExit, t2);
        return tEnter <= tExit;
    }
}
