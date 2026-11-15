// Passe couleur de TeamTimo/StylizedFoliage, incluse 4 fois par le shader :
//  - ForwardLit (opaque) : la flore de la zone en fondu est retirée (TT_CameraOcclusionClip) ;
//  - CameraFadeDepth (TT_FADE_PASS + TT_FADE_DEPTH) : profondeur seule de la zone en fondu ;
//  - CameraFade (TT_FADE_PASS) : couleur de la zone en fondu, alpha = TT_CameraFadeAlpha ;
//  - CameraFadeNear (TT_FADE_PASS + TT_FADE_NEAR) : flore près de l'objectif, alpha = TT_CameraFadeNearAlpha.
// Voir CameraOcclusion.hlsl et Assets/Scripts/CameraFoliageOcclusion.cs.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float4 data : TEXCOORD2; // x = occlusion, y = hauteur, z = aléa par amas, w = aléa par arbre
    float fog : TEXCOORD3;
    float2 uv : TEXCOORD4;
    UNITY_VERTEX_OUTPUT_STEREO
};

// Diffus en rampe douce ; s'éteint quand la lumière passe sous l'horizon (soleil la nuit).
float FoliageDiffuse(float3 N, float3 L)
{
    float d = saturate((dot(N, L) + _WrapLight) / (1.0 + _WrapLight));
    d = smoothstep(_RampCenter - _RampSoftness * 0.5, _RampCenter + _RampSoftness * 0.5, d);
    float horizon = saturate(L.y * 5.0 + 0.2);
    return d * horizon;
}

Varyings vert(Attributes v)
{
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    float treeRand;
    float3 posWS = FoliageDisplace(v.positionOS.xyz, v.normalOS, v.color, treeRand);
    o.positionWS = posWS;
    o.positionCS = TransformWorldToHClip(posWS);
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    o.data = float4(v.color.r, v.color.g, v.color.b, treeRand);
    o.fog = ComputeFogFactor(o.positionCS.z);
    o.uv = v.uv;
    return o;
}

half4 frag(Varyings i, bool isFrontFace : SV_IsFrontFace) : SV_Target
{
    #if defined(TT_FADE_NEAR)
        float fadeAlpha = TT_CameraFadeNearAlpha(i.positionWS);
    #elif defined(TT_FADE_PASS)
        float fadeAlpha = TT_CameraFadeAlpha(i.positionWS);
    #else
        TT_CameraOcclusionClip(i.positionWS);
    #endif
    half4 tex = FoliageSampleClip(i.uv);
    #if defined(TT_FADE_DEPTH)
        return 0; // profondeur seule
    #endif
    float occlusion = lerp(1.0, saturate(i.data.x), _OcclusionStrength);
    float h = saturate(i.data.y);

    // Couleur : bas -> haut, creux sombres, variations
    float3 albedo = lerp(_BaseColor.rgb, _TopColor.rgb, pow(max(h, 1e-4), _GradientPower));
    albedo = lerp(_InnerColor.rgb, albedo, occlusion);
    albedo *= 1.0 + (i.data.z - 0.5) * 2.0 * _ClusterVariation + (i.data.w - 0.5) * 2.0 * _TreeVariation;
    albedo *= lerp(1.0, tex.rgb * 1.2, _TextureStrength);
    albedo = saturate(albedo);

    float3 N = normalize(i.normalWS);
    float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
    float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        float4 shadowCoord = float4(screenUV, 0, 1);
    #else
        float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
    #endif
    Light mainLight = GetMainLight(shadowCoord, i.positionWS, half4(1, 1, 1, 1));
    float shadow = lerp(1.0, mainLight.shadowAttenuation, _ReceivedShadow) * mainLight.distanceAttenuation;
    float diffuse = FoliageDiffuse(N, mainLight.direction);
    diffuse = lerp(_ShadowStrength * saturate(mainLight.direction.y * 5.0 + 0.2), 1.0, diffuse);
    float back = pow(saturate(dot(V, -mainLight.direction)), 4.0) * _Translucency * saturate(mainLight.direction.y * 5.0 + 0.2);
    float3 direct = mainLight.color * (diffuse * shadow + back * occlusion);
    float3 ambient = SampleSH(N) * lerp(0.6, 1.0, occlusion);

    #if defined(_SCREEN_SPACE_OCCLUSION)
        AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(screenUV);
        ambient *= ao.indirectAmbientOcclusion;
        direct *= ao.directAmbientOcclusion;
    #endif

    #if defined(_ADDITIONAL_LIGHTS)
        InputData inputData = (InputData)0;
        inputData.positionWS = i.positionWS;
        inputData.normalWS = N;
        inputData.viewDirectionWS = V;
        inputData.normalizedScreenSpaceUV = screenUV;
        uint pixelLightCount = GetAdditionalLightsCount();
        #if USE_CLUSTER_LIGHT_LOOP
        [loop] for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
        {
            Light dl = GetAdditionalLight(dirIndex, i.positionWS, half4(1, 1, 1, 1));
            direct += dl.color * dl.distanceAttenuation * dl.shadowAttenuation * FoliageDiffuse(N, dl.direction);
        }
        #endif
        LIGHT_LOOP_BEGIN(pixelLightCount)
            Light al = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
            direct += al.color * al.distanceAttenuation * al.shadowAttenuation * saturate(dot(N, al.direction) * 0.5 + 0.5);
        LIGHT_LOOP_END
    #endif

    float3 color = albedo * (direct + ambient);

    // Liseré : le contour des amas s'éclaire un peu (lecture de la silhouette)
    float rim = pow(1.0 - saturate(dot(N, V)), _RimPower) * _RimStrength * occlusion;
    color += rim * _RimColor.rgb * (ambient + mainLight.color * shadow * 0.5);

    // Envers (dessous des branches, intérieur des volumes) : plus sombre
    if (!isFrontFace)
        color *= _BackfaceDarkening;

    color = MixFog(color, i.fog);
    #if defined(TT_FADE_PASS)
        return half4(color, fadeAlpha);
    #else
        return half4(color, 1);
    #endif
}
