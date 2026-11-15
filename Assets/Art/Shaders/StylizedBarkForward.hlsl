// Passe couleur de TeamTimo/StylizedBark, incluse 4 fois par le shader :
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
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float4 tangentWS : TEXCOORD2; // w = signe de la bitangente
    float2 uv : TEXCOORD3;
    float fog : TEXCOORD4;
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings vert(Attributes v)
{
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
    VertexNormalInputs nrm = GetVertexNormalInputs(v.normalOS, v.tangentOS);
    o.positionCS = pos.positionCS;
    o.positionWS = pos.positionWS;
    o.normalWS = nrm.normalWS;
    o.tangentWS = float4(nrm.tangentWS, v.tangentOS.w * GetOddNegativeScale());
    o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
    o.fog = ComputeFogFactor(pos.positionCS.z);
    return o;
}

half4 frag(Varyings i) : SV_Target
{
    #if defined(TT_FADE_NEAR)
        float fadeAlpha = TT_CameraFadeNearAlpha(i.positionWS);
    #elif defined(TT_FADE_PASS)
        float fadeAlpha = TT_CameraFadeAlpha(i.positionWS);
    #else
        TT_CameraOcclusionClip(i.positionWS);
    #endif
    #if defined(TT_FADE_DEPTH)
        return 0; // profondeur seule
    #endif

    half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
    float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
    float3 normalWS = normalize(i.normalWS);
    float3 tangentWS = normalize(i.tangentWS.xyz);
    float3 bitangentWS = i.tangentWS.w * cross(normalWS, tangentWS);
    half3x3 tangentToWorld = half3x3(tangentWS, bitangentWS, normalWS);

    InputData inputData = (InputData)0;
    inputData.positionWS = i.positionWS;
    inputData.positionCS = i.positionCS;
    inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        inputData.shadowCoord = float4(inputData.normalizedScreenSpaceUV, 0, 1);
    #else
        inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
    #endif
    inputData.fogCoord = i.fog;
    inputData.bakedGI = SampleSH(inputData.normalWS);
    inputData.shadowMask = half4(1, 1, 1, 1);
    inputData.tangentToWorld = tangentToWorld;

    SurfaceData surface = (SurfaceData)0;
    surface.albedo = tex.rgb * _BaseColor.rgb;
    surface.alpha = 1;
    surface.metallic = 0;
    surface.smoothness = _Smoothness;
    surface.occlusion = 1;
    surface.normalTS = normalTS;

    half4 color = UniversalFragmentPBR(inputData, surface);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    #if defined(TT_FADE_PASS)
        return half4(color.rgb, fadeAlpha);
    #else
        return half4(color.rgb, 1);
    #endif
}
