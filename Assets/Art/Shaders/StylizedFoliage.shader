// Feuillage stylisé des arbres : sapin en branches-cartes découpées (ArtSource/Trees/build_pine_tree.py)
// et feuillu en amas de boules (ArtSource/Trees/build_round_tree.py, variante non utilisée).
// - Découpe alpha (alpha test, pas de transparence) : _BaseMap = texture d'aiguilles ; RVB = ombrage.
// - Couleur : dégradé bas -> haut (vert de l'herbe -> vert-jaune), creux et dessous plus sombres
//   (occlusion stockée dans la couleur de sommet), variation par amas et par arbre.
// - Lumière en « rampe » douce : chaque amas a une face claire et une face sombre nettes,
//   ce qui fait lire le relief et le contour (au lieu d'un feuillage touffu uniforme).
// - Léger liseré de lumière sur le contour, transluminescence en contre-jour.
// - Vent (100 % GPU) : balancement de tout l'arbre, battement de chaque branche (le bout bouge
//   plus que la base, rythme propre à chaque branche), frémissement des feuilles, rafales qui
//   traversent la forêt.
// - Rétrécissement à distance optionnel (buissons posés en détails du terrain, comme l'herbe).
// - Projette et reçoit les ombres.
// Conventions du mesh : UV0 = texture du feuillage ; couleur de sommet R = occlusion,
// G = hauteur dans le feuillage, B = aléa par branche / amas, A = souplesse au vent (0 = fixe,
// 1 = bout de branche). Les faces sont rendues des deux côtés.
Shader "TeamTimo/StylizedFoliage"
{
    Properties
    {
        [Header(Texture)]
        [NoScaleOffset] _BaseMap ("Texture du feuillage (alpha = découpe)", 2D) = "white" {}
        _Cutoff ("Seuil de découpe", Range(0, 1)) = 0.5
        _TextureStrength ("Force de l'ombrage de la texture", Range(0, 1)) = 1

        [Header(Couleur)]
        _TopColor ("Couleur du haut (soleil)", Color) = (0.58, 0.78, 0.2, 1)
        _BaseColor ("Couleur du bas", Color) = (0.32, 0.56, 0.13, 1)
        _InnerColor ("Couleur des creux", Color) = (0.09, 0.25, 0.1, 1)
        _GradientPower ("Courbe du dégradé bas -> haut", Range(0.2, 4)) = 1.2
        _OcclusionStrength ("Force des creux", Range(0, 1)) = 0.85
        _ClusterVariation ("Variation de teinte par amas", Range(0, 0.5)) = 0.08
        _TreeVariation ("Variation de teinte par arbre", Range(0, 0.5)) = 0.1

        [Header(Lumiere)]
        _RampCenter ("Limite ombre / lumière", Range(0, 1)) = 0.4
        _RampSoftness ("Douceur de la limite", Range(0.01, 1)) = 0.35
        _WrapLight ("Lumière enveloppante", Range(0, 1)) = 0.3
        _ShadowStrength ("Luminosité de la face à l'ombre", Range(0, 1)) = 0.25
        _ReceivedShadow ("Force des ombres reçues", Range(0, 1)) = 0.3
        _Translucency ("Transluminescence en contre-jour", Range(0, 1)) = 0.2
        _RimColor ("Couleur du liseré", Color) = (0.9, 1, 0.6, 1)
        _RimStrength ("Force du liseré (contour)", Range(0, 1)) = 0.18
        _RimPower ("Finesse du liseré", Range(1, 8)) = 4
        _BackfaceDarkening ("Luminosité de l'envers (dessous des branches, intérieur)", Range(0, 1)) = 0.55

        [Header(Vent)]
        _WindDirection ("Direction du vent (X, _, Z)", Vector) = (1, 0, 0.35, 0)
        _WindStrength ("Balancement de tout l'arbre (m)", Range(0, 1)) = 0.12
        _WindSpeed ("Vitesse du vent", Range(0, 5)) = 0.9
        _BranchBend ("Battement des branches (m)", Range(0, 1)) = 0.25
        _BranchSpeed ("Rythme du battement des branches", Range(0, 6)) = 1.6
        _GustScale ("Taille des rafales (m)", Float) = 40
        _GustStrength ("Force des rafales", Range(0, 1)) = 0.5
        _Flutter ("Frémissement des feuilles (m)", Range(0, 0.3)) = 0.03

        [Header(Distance)]
        _FadeStart ("Début du rétrécissement (m, 0 = désactivé)", Float) = 0
        _FadeEnd ("Disparition complète (m, 0 = désactivé)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        Cull Off // cartes visibles des deux côtés ; l'envers est assombri (voir frag)

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "CameraOcclusion.hlsl" // flore en pointillé entre la caméra et le joueur

        CBUFFER_START(UnityPerMaterial)
            float4 _TopColor;
            float4 _BaseColor;
            float4 _InnerColor;
            float _GradientPower;
            float _OcclusionStrength;
            float _ClusterVariation;
            float _TreeVariation;
            float _RampCenter;
            float _RampSoftness;
            float _WrapLight;
            float _ShadowStrength;
            float _ReceivedShadow;
            float _Translucency;
            float4 _RimColor;
            float _RimStrength;
            float _RimPower;
            float4 _WindDirection;
            float _WindStrength;
            float _WindSpeed;
            float _GustScale;
            float _GustStrength;
            float _Flutter;
            float _Cutoff;
            float _TextureStrength;
            float _BackfaceDarkening;
            float _BranchBend;
            float _BranchSpeed;
            float _FadeStart;
            float _FadeEnd;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // Découpe alpha (toutes les passes) ; renvoie la texture pour l'ombrage.
        half4 FoliageSampleClip(float2 uv)
        {
            half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
            clip(tex.a - _Cutoff);
            return tex;
        }

        float FoliageHash(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float FoliageNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = FoliageHash(i);
            float b = FoliageHash(i + float2(1, 0));
            float c = FoliageHash(i + float2(0, 1));
            float d = FoliageHash(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        // Position monde d'un sommet du feuillage après le vent.
        // color : R = occlusion, G = hauteur dans le feuillage, B = aléa par branche, A = souplesse.
        float3 FoliageDisplace(float3 positionOS, float3 normalOS, float4 color, out float treeRand)
        {
            float3 rootWS = TransformObjectToWorld(float3(0, 0, 0));
            float3 posWS = TransformObjectToWorld(positionOS);
            float scale = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
            treeRand = FoliageHash(rootWS.xz * 0.37);

            // Rétrécissement avant la distance d'affichage (buissons en détails du terrain)
            if (_FadeEnd > 0.0)
            {
                float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, distance(rootWS, GetCameraPositionWS()));
                posWS = rootWS + (posWS - rootWS) * fade;
                scale *= fade;
            }

            float h = saturate(color.g);
            float flex = saturate(color.a);
            float2 dir = normalize(_WindDirection.xz + float2(1e-5, 0));
            float t = _Time.y * _WindSpeed;
            float gust = FoliageNoise(rootWS.xz / max(_GustScale, 0.01) - dir * t * 0.35);
            float gustFactor = 1.0 - _GustStrength + 2.0 * _GustStrength * gust;

            // 1) balancement de tout l'arbre, plus fort en haut
            float sway = sin(t + treeRand * 6.2832) * 0.6 + sin(t * 1.9 + treeRand * 12.0) * 0.25 + 0.4;
            float w = 0.3 + 0.7 * h;
            posWS.xz += dir * sway * gustFactor * _WindStrength * w * w * scale;

            // 2) battement des branches : chaque branche à son rythme, le bout bouge le plus
            float phase = color.b * 31.0 + treeRand * 7.0;
            float tb = _Time.y * _BranchSpeed;
            float bob = sin(tb + phase) * 0.7 + sin(tb * 2.3 + phase * 1.7) * 0.3;
            float3 bendDir = normalize(float3(dir.x * 0.7, 0.7, dir.y * 0.7));
            posWS += bendDir * bob * gustFactor * _BranchBend * flex * scale;

            // 3) frémissement des feuilles (rapide, petit, le long de la normale)
            float3 nWS = TransformObjectToWorldNormal(normalOS);
            float flutter = sin(_Time.y * 7.3 + dot(positionOS, float3(3.1, 4.7, 2.3)) + treeRand * 20.0);
            posWS += nWS * flutter * gustFactor * _Flutter * (0.25 + flex) * scale;
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
                TT_CameraOcclusionClip(i.positionWS, i.positionCS);
                half4 tex = FoliageSampleClip(i.uv);
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
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

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
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                float treeRand;
                float3 posWS = FoliageDisplace(v.positionOS.xyz, v.normalOS, v.color, treeRand);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - posWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = positionCS;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                FoliageSampleClip(i.uv);
                return 0;
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
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float treeRand;
                o.positionWS = FoliageDisplace(v.positionOS.xyz, v.normalOS, v.color, treeRand);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                return o;
            }

            half frag(Varyings i) : SV_Target
            {
                TT_CameraOcclusionClip(i.positionWS, i.positionCS);
                FoliageSampleClip(i.uv);
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
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float treeRand;
                o.positionWS = FoliageDisplace(v.positionOS.xyz, v.normalOS, v.color, treeRand);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                TT_CameraOcclusionClip(i.positionWS, i.positionCS);
                FoliageSampleClip(i.uv);
                float3 n = normalize(i.normalWS);
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
