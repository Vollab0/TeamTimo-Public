// Flore en fondu (transparence) entre la caméra et le joueur, et collée à l'objectif.
// Variables globales envoyées par Assets/Scripts/CameraFoliageOcclusion.cs.
// Réservé aux ARBRES : TeamTimo/StylizedBark (troncs) et TeamTimo/StylizedFoliage (aiguilles). Les buissons
// partagent StylizedFoliage mais coupent l'effet par matériau (_CameraOcclusion = 0) ; l'herbe ne l'a pas.
// Ombres non concernées (elles restent pleines).
// Un shader peut définir TT_OCCLUSION_TOGGLE (propriété du matériau, 0 = effet coupé) avant d'inclure ce fichier.
//
// Deux effets :
//  - le CÔNE caméra -> joueur : la flore devant le joueur devient transparente ;
//  - PRÈS DE L'OBJECTIF : la flore à moins de quelques mètres de la caméra s'efface.
//
// Rendu en trois temps :
//  1) passes opaques (ForwardLit, DepthOnly, DepthNormals) : TT_CameraOcclusionClip retire toute la
//     flore concernée ; le joueur et le décor derrière se dessinent normalement ;
//  2) après les transparents, CameraFoliageOcclusion dessine la flore du CÔNE avec
//     « TeamTimoFadeDepth » (profondeur seule) puis « TeamTimoFade » (couleur mélangée) : seule la
//     surface la plus proche est dessinée, sinon les branches superposées s'additionneraient et un
//     sapin redeviendrait presque opaque. Toutes ces surfaces ont la même opacité (elle ne dépend
//     que du rayon caméra -> pixel), donc cacher celles de derrière ne crée aucune cassure ;
//  3) puis « TeamTimoFadeNear » dessine la flore PRÈS DE L'OBJECTIF, triée de l'arrière vers
//     l'avant, sans écrire la profondeur : elle ne cache jamais ce qui est derrière. Sinon une
//     branche contre l'objectif cachait d'un coup toute la flore du cône derrière elle, et le bord
//     du cercle devenait une ligne nette.
#ifndef TEAMTIMO_CAMERA_OCCLUSION_INCLUDED
#define TEAMTIMO_CAMERA_OCCLUSION_INCLUDED

float4 _TT_OcclusionTarget; // xyz = centre du joueur (monde), w = 1 si l'effet est actif
float4 _TT_OcclusionShape;  // x = rayon autour du joueur (m), y = largeur du fondu (m), z = profondeur derrière le joueur (m), w = opacité gardée
float4 _TT_OcclusionNear;   // x = début du fondu près de la caméra (m), y = fin (m)

// En dessous de cette part retirée, le pixel reste dans les passes opaques.
#define TT_FADE_EPSILON 0.002

// Part retirée par le cône (cone) et par la proximité de l'objectif (nearCam), 0 = pleine, 1 = retirée.
void TT_CameraOcclusionTerms(float3 positionWS, out float cone, out float nearCam)
{
    cone = 0.0;
    nearCam = 0.0;
    if (_TT_OcclusionTarget.w < 0.5)
        return;
#if defined(TT_OCCLUSION_TOGGLE)
    if (TT_OCCLUSION_TOGGLE < 0.5)
        return; // effet coupé sur ce matériau : opaque normal, rien dans les passes TeamTimoFade*
#endif

    float3 camPos = GetCameraPositionWS();
    float3 toTarget = _TT_OcclusionTarget.xyz - camPos;
    float targetDist = max(length(toTarget), 1e-3);
    float3 toPixel = positionWS - camPos;
    float pixelDepth = dot(toPixel, toTarget) / targetDist; // distance le long de l'axe caméra -> joueur

    // Cône caméra -> joueur : on ramène le point dans le plan du joueur et on mesure son écart au joueur.
    if (pixelDepth > 0.01)
    {
        float radial = length(toPixel * (targetDist / pixelDepth) - toTarget);
        cone = 1.0 - smoothstep(_TT_OcclusionShape.x, _TT_OcclusionShape.x + _TT_OcclusionShape.y, radial);
        cone *= 1.0 - smoothstep(targetDist, targetDist + _TT_OcclusionShape.z, pixelDepth); // seulement devant le joueur
        cone *= 1.0 - _TT_OcclusionShape.w;
    }

    // Flore collée à l'objectif (caméra basse dans l'herbe, caméra qui frôle une branche) :
    // retirée complètement tout près, puis de moins en moins jusqu'à la fin du fondu.
    nearCam = 1.0 - smoothstep(_TT_OcclusionNear.x, _TT_OcclusionNear.y, length(toPixel));
}

// Passes opaques : retire le pixel s'il est concerné par l'un des deux effets (il est redessiné par les passes TeamTimoFade*).
void TT_CameraOcclusionClip(float3 positionWS)
{
    float cone, nearCam;
    TT_CameraOcclusionTerms(positionWS, cone, nearCam);
    clip(TT_FADE_EPSILON - max(cone, nearCam));
}

// Passes TeamTimoFadeDepth / TeamTimoFade : flore du cône, hors de la zone près de l'objectif.
// Renvoie l'opacité du pixel.
float TT_CameraFadeAlpha(float3 positionWS)
{
    float cone, nearCam;
    TT_CameraOcclusionTerms(positionWS, cone, nearCam);
    clip(TT_FADE_EPSILON - nearCam); // près de l'objectif : passe TeamTimoFadeNear
    clip(cone - TT_FADE_EPSILON);    // hors du cône : déjà dessiné par la passe opaque
    clip(0.995 - cone);              // retiré complètement : rien à dessiner, pas de profondeur
    return 1.0 - cone;
}

// Passe TeamTimoFadeNear : flore près de l'objectif (dans le cône ou non). Renvoie l'opacité du pixel.
float TT_CameraFadeNearAlpha(float3 positionWS)
{
    float cone, nearCam;
    TT_CameraOcclusionTerms(positionWS, cone, nearCam);
    clip(nearCam - TT_FADE_EPSILON);
    float amount = max(cone, nearCam);
    clip(0.995 - amount);
    return 1.0 - amount;
}

#endif
