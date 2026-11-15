// Herbe stylisée pour les détails du terrain (meshes instanciés).
// - Le pied des brins reprend la texture d'herbe du terrain (même échelle, en coordonnées monde)
//   pour se fondre parfaitement dans le sol ; les pointes sont plus claires.
// - Éclairage calculé avec une normale verticale, comme le sol : pas de « mur » plus sombre.
// - Vent : balancement + rafales qui traversent le champ + frémissement des pointes (100 % GPU).
// - Rétrécit en douceur avant la distance d'affichage des détails du terrain (pas de « pop »).
// - Pas de passe ShadowCaster : l'herbe reçoit les ombres mais n'en projette pas (performance).
// Conventions du mesh (voir ArtSource/Terrain/build_grass_tufts.py) :
//   UV0.y = hauteur normalisée (0 pied, 1 pointe), UV1.x = aléa par brin,
//   couleur de sommet : RGB = couleur propre (fleurs), A = 1 pour l'utiliser.
Shader "TeamTimo/StylizedGrass"
{
    Properties
    {
        [Header(Couleur)]
        [NoScaleOffset] _GroundTex ("Texture du sol sous l'herbe", 2D) = "white" {}
        _GroundTileSize ("Taille de tuile de cette texture sur le terrain (m)", Float) = 8
        _RootTint ("Teinte du pied", Color) = (1, 1, 1, 1)
        _TipTint ("Multiplicateur des pointes", Color) = (1.12, 1.12, 0.95, 1)
        _TipColor ("Couleur fixe des pointes", Color) = (0.8, 0.86, 0.45, 1)
        _TipColorBlend ("Part de la couleur fixe aux pointes", Range(0, 1)) = 0.1
        _GradientPower ("Courbe du dégradé pied -> pointe", Range(0.2, 4)) = 1.4
        _TintVariation ("Variation de teinte par brin", Range(0, 0.5)) = 0.12
        _PatchVariation ("Variation de teinte par zone", Range(0, 0.5)) = 0.1
        _PatchScale ("Taille des zones de variation (m)", Float) = 35
        _RootDarkening ("Assombrissement au pied", Range(0, 0.5)) = 0.06

        [Header(Lumiere)]
        _WrapLight ("Lumière enveloppante (pointes)", Range(0, 1)) = 0
        _Translucency ("Transluminescence en contre-jour", Range(0, 1)) = 0.12
        _ReceivedShadow ("Force des ombres reçues", Range(0, 1)) = 1

        [Header(Vent)]
        _WindDirection ("Direction du vent (X, _, Z)", Vector) = (1, 0, 0.35, 0)
        _WindStrength ("Force du vent", Range(0, 1.5)) = 0.35
        _WindSpeed ("Vitesse du vent", Range(0, 5)) = 1.2
        _GustScale ("Taille des rafales (m)", Float) = 40
        _GustStrength ("Force des rafales", Range(0, 1)) = 0.6
        _Flutter ("Frémissement des pointes", Range(0, 0.3)) = 0.06

        [Header(Distance)]
        _FadeStart ("Début du rétrécissement (m)", Float) = 55
        _FadeEnd ("Disparition complète (m)", Float) = 75
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "CameraOcclusion.hlsl" // herbe en pointillé entre la caméra et le joueur

        CBUFFER_START(UnityPerMaterial)
            float4 _RootTint;
            float4 _TipTint;
            float4 _TipColor;
            float _TipColorBlend;
            float _GroundTileSize;
            float _GradientPower;
            float _TintVariation;
            float _PatchVariation;
            float _PatchScale;
            float _RootDarkening;
            float _WrapLight;
            float _Translucency;
            float _ReceivedShadow;
            float4 _WindDirection;
            float _WindStrength;
            float _WindSpeed;
            float _GustScale;
            float _GustStrength;
            float _Flutter;
            float _FadeStart;
            float _FadeEnd;
        CBUFFER_END

        TEXTURE2D(_GroundTex);
        SAMPLER(sampler_GroundTex);

        float GrassHash(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float GrassNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = GrassHash(i);
            float b = GrassHash(i + float2(1, 0));
            float c = GrassHash(i + float2(0, 1));
            float d = GrassHash(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        // Position monde d'un sommet après rétrécissement à distance et vent.
        float3 GrassDisplace(float3 positionOS, float height01, out float3 rootWS)
        {
            rootWS = TransformObjectToWorld(float3(0, 0, 0));
            float3 posWS = TransformObjectToWorld(positionOS);
            float worldHeight = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));

            // Rétrécissement avant la distance d'affichage des détails
            float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, distance(rootWS, GetCameraPositionWS()));
            posWS = rootWS + (posWS - rootWS) * fade;
            worldHeight *= fade;

            // Vent
            float rand = GrassHash(rootWS.xz);
            float2 dir = normalize(_WindDirection.xz + float2(1e-5, 0));
            float t = _Time.y * _WindSpeed;
            float gust = GrassNoise(rootWS.xz / max(_GustScale, 0.01) - dir * t * 0.35);
            float wave = sin(t * 1.7 + dot(rootWS.xz, dir) * 0.12 + rand * 1.5) * 0.5 + 0.5;
            float bend = _WindStrength * (0.25 + 0.75 * wave) * (1.0 - _GustStrength + 2.0 * _GustStrength * gust);
            float flutter = sin(t * 6.3 + rand * 30.0 + height01 * 2.0) * _Flutter;
            float2 perp = float2(-dir.y, dir.x);
            float2 offset = (dir * bend + perp * flutter) * height01 * height01 * worldHeight;
            posWS.xz += offset;
            posWS.y -= dot(offset, offset) * 0.5 / max(worldHeight, 0.01); // garde la longueur du brin
            return posWS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 data : TEXCOORD1; // x = hauteur, y = aléa par brin, z = brouillard, w = variation de zone
                float4 color : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Diffus d'une lumière sur l'herbe : comme le sol au pied, un peu enveloppant aux pointes.
            // S'éteint quand la lumière passe sous l'horizon (soleil la nuit), comme sur le sol.
            float GrassDiffuse(float NdotL, float h)
            {
                float wrap = _WrapLight * h;
                float horizon = saturate(NdotL * 5.0 + 0.2);
                return saturate((NdotL + wrap) / (1.0 + wrap)) * horizon;
            }

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float h = saturate(v.uv0.y);
                float3 rootWS;
                float3 posWS = GrassDisplace(v.positionOS.xyz, h, rootWS);
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                float patch = GrassNoise(rootWS.xz / max(_PatchScale, 0.01)) * 2.0 - 1.0;
                o.data = float4(h, v.uv1.x, ComputeFogFactor(o.positionCS.z), patch);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                TT_CameraOcclusionClip(i.positionWS, i.positionCS);
                float h = i.data.x;

                // Couleur : pied = sol du terrain, pointes plus claires
                float3 ground = SAMPLE_TEXTURE2D(_GroundTex, sampler_GroundTex, i.positionWS.xz / max(_GroundTileSize, 0.01)).rgb;
                float3 rootCol = ground * _RootTint.rgb;
                float3 tipCol = lerp(ground * _TipTint.rgb, _TipColor.rgb, _TipColorBlend);
                float3 albedo = lerp(rootCol, tipCol, pow(max(h, 1e-4), _GradientPower));
                float variation = ((i.data.y - 0.5) * 2.0 * _TintVariation + i.data.w * _PatchVariation) * h;
                albedo *= 1.0 + variation;
                albedo *= 1.0 - _RootDarkening * (1.0 - h);
                albedo = lerp(albedo, i.color.rgb, i.color.a); // fleurs
                albedo = saturate(albedo);

                // Éclairage avec une normale verticale (comme le sol)
                float3 N = float3(0, 1, 0);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = float4(screenUV, 0, 1);
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                #endif
                Light mainLight = GetMainLight(shadowCoord, i.positionWS, half4(1, 1, 1, 1));
                float shadow = lerp(1.0, mainLight.shadowAttenuation, _ReceivedShadow) * mainLight.distanceAttenuation;
                float mainNdotL = dot(N, mainLight.direction);
                float diffuse = GrassDiffuse(mainNdotL, h);
                float back = pow(max(dot(V, -mainLight.direction), 1e-4), 4.0) * _Translucency * h * saturate(mainNdotL * 5.0 + 0.2);
                float3 direct = mainLight.color * shadow * (diffuse + back);
                float3 ambient = SampleSH(N);

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
                        direct += dl.color * dl.distanceAttenuation * dl.shadowAttenuation * GrassDiffuse(dot(N, dl.direction), h);
                    }
                    #endif
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light al = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                        direct += al.color * al.distanceAttenuation * al.shadowAttenuation * GrassDiffuse(dot(N, al.direction), h);
                    LIGHT_LOOP_END
                #endif

                float3 color = albedo * (direct + ambient);
                color = MixFog(color, i.data.z);                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0 : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 rootWS;
                o.positionWS = GrassDisplace(v.positionOS.xyz, saturate(v.uv0.y), rootWS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half frag(Varyings i) : SV_Target
            {
                TT_CameraOcclusionClip(i.positionWS, i.positionCS);
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0 : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 rootWS;
                o.positionWS = GrassDisplace(v.positionOS.xyz, saturate(v.uv0.y), rootWS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                TT_CameraOcclusionClip(i.positionWS, i.positionCS);
                float3 n = float3(0, 1, 0);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormal = PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(octNormal * 0.5 + 0.5)), 0.0);
                #else
                    return half4(n, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
