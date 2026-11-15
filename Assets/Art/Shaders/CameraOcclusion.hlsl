// Flore « en pointillé » entre la caméra et le joueur (et collée à l'objectif).
// Variables globales envoyées par Assets/Scripts/CameraFoliageOcclusion.cs.
// Utilisé par TeamTimo/StylizedFoliage (arbres, buissons) et TeamTimo/StylizedGrass (herbe),
// dans les passes de couleur et de profondeur ; pas dans la passe d'ombre (les ombres restent pleines).
// Motif : tramage ordonné 4x4 en espace écran (des points réguliers, pas de transparence triée).
#ifndef TEAMTIMO_CAMERA_OCCLUSION_INCLUDED
#define TEAMTIMO_CAMERA_OCCLUSION_INCLUDED

float4 _TT_OcclusionTarget; // xyz = centre du joueur (monde), w = 1 si l'effet est actif
float4 _TT_OcclusionShape;  // x = rayon autour du joueur (m), y = largeur du fondu (m), z = profondeur derrière le joueur (m), w = opacité gardée
float4 _TT_OcclusionNear;   // x = début du fondu près de la caméra (m), y = fin (m), z = taille des points (px)

// Seuil de tramage ordonné (Bayer 4x4) dans ]0, 1[.
float TT_Bayer4(uint2 p)
{
    static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    return (bayer[(p.y & 3u) * 4u + (p.x & 3u)] + 0.5) / 16.0;
}

// Part de flore à retirer en ce point (0 = pleine, 1 = retirée).
float TT_CameraOcclusionAmount(float3 positionWS)
{
    if (_TT_OcclusionTarget.w < 0.5)
        return 0.0;

    float3 camPos = GetCameraPositionWS();
    float3 toTarget = _TT_OcclusionTarget.xyz - camPos;
    float targetDist = max(length(toTarget), 1e-3);
    float3 toPixel = positionWS - camPos;
    float pixelDepth = dot(toPixel, toTarget) / targetDist; // distance le long de l'axe caméra -> joueur

    // Cône caméra -> joueur : on ramène le point dans le plan du joueur et on mesure son écart au joueur.
    float cone = 0.0;
    if (pixelDepth > 0.01)
    {
        float radial = length(toPixel * (targetDist / pixelDepth) - toTarget);
        cone = 1.0 - smoothstep(_TT_OcclusionShape.x, _TT_OcclusionShape.x + _TT_OcclusionShape.y, radial);
        cone *= 1.0 - smoothstep(targetDist, targetDist + _TT_OcclusionShape.z, pixelDepth); // seulement devant le joueur
    }

    // Flore collée à l'objectif (caméra basse dans l'herbe, caméra qui frôle une branche) :
    // retirée complètement tout près, puis de moins en moins jusqu'à la fin du fondu.
    float nearCam = 1.0 - smoothstep(_TT_OcclusionNear.x, _TT_OcclusionNear.y, length(toPixel));

    return max(cone * (1.0 - _TT_OcclusionShape.w), nearCam);
}

// Jette le pixel selon le motif en pointillé. positionCS = SV_POSITION (coordonnées en pixels).
void TT_CameraOcclusionClip(float3 positionWS, float4 positionCS)
{
    float amount = TT_CameraOcclusionAmount(positionWS);
    if (amount <= 0.0)
        return;
    uint2 cell = (uint2)(positionCS.xy / max(_TT_OcclusionNear.z, 1.0));
    clip(TT_Bayer4(cell) - amount);
}

#endif
