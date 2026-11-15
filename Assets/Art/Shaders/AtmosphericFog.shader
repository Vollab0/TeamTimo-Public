// Brouillard atmosphérique plein écran (passe « Full Screen Pass » de l'URP, juste avant le post-process) :
//  - brouillard de hauteur exponentiel, plus dense près du sol ;
//  - diffusion vers l'astre (lueur dorée autour du soleil couchant, bleutée autour de la lune) ;
//  - rayons de lumière (« god rays ») en espace écran : on marche vers l'astre et on compte le ciel visible.
// Couleurs, densité et direction de la lumière sont des globales (_TT_*) écrites par DayNightCycle.
Shader "Hidden/TeamTimo/AtmosphericFog"
{
    Properties
    {
        [Header(Brouillard)]
        _FogStart ("Distance sans brouillard (m)", Float) = 18
        _HeightFalloff ("Décroissance avec la hauteur", Range(0.001, 0.5)) = 0.06
        _BaseHeight ("Hauteur de référence (m)", Float) = 0
        _MaxFog ("Opacité max", Range(0, 1)) = 0.92
        _ScatterSharpness ("Finesse de la lueur vers l'astre", Range(1, 64)) = 10

        [Header(Rayons de lumiere)]
        [IntRange] _RaySamples ("Échantillons (coût)", Range(4, 32)) = 14
        _RayLength ("Longueur des rayons (écran)", Range(0.1, 1)) = 0.55
        _RayDecay ("Atténuation le long du rayon", Range(0.8, 1)) = 0.95
        _RaySpread ("Étendue autour de l'astre", Range(1, 64)) = 12
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "AtmosphericFog"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _FogStart, _HeightFalloff, _BaseHeight, _MaxFog, _ScatterSharpness;
            float _RaySamples, _RayLength, _RayDecay, _RaySpread;

            half3 _TT_FogColor;
            float _TT_FogDensity;
            float3 _TT_LightDir;        // direction VERS l'astre dominant (soleil ou lune)
            half3 _TT_ScatterColor;
            float _TT_ScatterIntensity;
            half3 _TT_GodRayColor;
            float _TT_GodRayIntensity;

            bool IsSky(float rawDepth)
            {
            #if UNITY_REVERSED_Z
                return rawDepth <= 1e-6;
            #else
                return rawDepth >= 1.0 - 1e-6;
            #endif
            }

            // Bruit pour décaler le départ de la marche des rayons (évite les bandes).
            float InterleavedGradientNoise(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                // Garde-fou : un NaN venu d'un autre shader serait étalé en carré noir par le bloom.
                if (any(isnan(color)) || any(isinf(color))) color = _TT_FogColor;

                float rawDepth = SampleSceneDepth(uv);
                bool sky = IsSky(rawDepth);
                // Bord d'objet avec MSAA : la profondeur dit « ciel » alors que la couleur résolue contient déjà
                // un peu de l'objet. On prend alors la profondeur du voisin le plus proche (sinon liseré sombre).
                if (sky)
                {
                    // Même chose pour les contours dessinés en transparent (matériau Outline des murs), qui
                    // n'écrivent pas la profondeur : on cherche un objet dans un rayon de 2 pixels.
                    float2 px = _ScreenSize.zw; // taille d'un pixel (la texture de profondeur a la résolution de la caméra)
                    float nearest = UNITY_RAW_FAR_CLIP_VALUE;
                    [unroll]
                    for (int k = 0; k < 8; k++)
                    {
                        float2 o = float2(k < 4 ? 1.0 : 2.0, 0.0);
                        int q = k % 4;
                        o = q == 0 ? o : (q == 1 ? -o : (q == 2 ? o.yx : -o.yx));
                        float n = SampleSceneDepth(uv + o * px);
                    #if UNITY_REVERSED_Z
                        nearest = max(nearest, n);
                    #else
                        nearest = min(nearest, n);
                    #endif
                    }
                    if (!IsSky(nearest)) { rawDepth = nearest; sky = false; }
                }

                float3 positionWS = ComputeWorldSpacePosition(uv, sky ? UNITY_RAW_FAR_CLIP_VALUE : rawDepth, UNITY_MATRIX_I_VP);
                float3 toPixel = positionWS - _WorldSpaceCameraPos;
                float dist = length(toPixel);
                float3 viewDir = toPixel / max(dist, 1e-4);

                float lightDot = saturate(dot(viewDir, _TT_LightDir));

                // ---------- Brouillard de hauteur (pas sur le ciel : la skybox gère sa propre brume) ----------
                if (!sky)
                {
                    float d = max(dist - _FogStart, 0.0);
                    float camH = _WorldSpaceCameraPos.y - _BaseHeight;
                    float dy = viewDir.y * d;
                    // Intégrale analytique de density * exp(-falloff * h) le long du rayon.
                    float k = _HeightFalloff * dy;
                    float lineFactor = abs(k) > 1e-3 ? (1.0 - exp(-k)) / k : 1.0;
                    float opticalDepth = _TT_FogDensity * d * exp(-_HeightFalloff * camH) * lineFactor;
                    float fog = min(1.0 - exp(-max(opticalDepth, 0.0)), _MaxFog);

                    half3 fogColor = _TT_FogColor + _TT_ScatterColor * pow(lightDot, _ScatterSharpness) * _TT_ScatterIntensity;
                    color = lerp(color, fogColor, fog);
                }

                // ---------- Rayons de lumière ----------
                if (_TT_GodRayIntensity > 0.001)
                {
                    float3 camForward = -UNITY_MATRIX_V[2].xyz;
                    float facing = dot(camForward, _TT_LightDir);
                    if (facing > 0.0)
                    {
                        float4 lightCS = mul(UNITY_MATRIX_VP, float4(_WorldSpaceCameraPos + _TT_LightDir * 1000.0, 1.0));
                        float2 lightUV = lightCS.xy / lightCS.w * 0.5 + 0.5;
                    #if UNITY_UV_STARTS_AT_TOP
                        lightUV.y = 1.0 - lightUV.y;
                    #endif
                        float2 delta = (lightUV - uv) * _RayLength / _RaySamples;
                        float jitter = InterleavedGradientNoise(uv * _ScreenParams.xy);
                        float2 samplePos = uv + delta * jitter;
                        float weight = 1.0;
                        float accum = 0.0;
                        float total = 0.0;
                        int count = (int)_RaySamples;
                        for (int s = 0; s < count; s++)
                        {
                            samplePos += delta;
                            float2 suv = saturate(samplePos);
                            accum += (IsSky(SampleSceneDepth(suv)) ? 1.0 : 0.0) * weight;
                            total += weight;
                            weight *= _RayDecay;
                        }
                        float shafts = accum / max(total, 1e-4);
                        // Plus fort autour de l'astre, nul quand il est hors champ derrière la caméra.
                        float around = pow(lightDot, _RaySpread);
                        float onScreen = saturate(facing * 3.0);
                        float rays = shafts * around * onScreen * _TT_GodRayIntensity;
                        // Sur le ciel, on se contente d'un léger voile (le halo est déjà dans la skybox).
                        rays *= sky ? 0.15 : 1.0;
                        color += _TT_GodRayColor * rays;
                    }
                }

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
