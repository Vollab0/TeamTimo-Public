// Ciel stylisé (skybox) : dégradé, halo du soleil, soleil, lune, étoiles et nuages peints procéduraux.
// Les couleurs et directions qui changent avec l'heure sont des variables GLOBALES (_TT_*) écrites par
// DayNightCycle : elles ne sont pas dans le bloc Properties pour que le matériau ne soit jamais modifié
// (pas de .mat qui repasse en « modifié » dans git). Les réglages de forme sont, eux, sur le matériau.
Shader "TeamTimo/StylizedSky"
{
    Properties
    {
        [Header(Degrade du ciel)]
        _ZenithExponent ("Hauteur du dégradé horizon → zénith", Range(0.1, 2)) = 0.55
        _HorizonHaze ("Brume à l'horizon", Range(0, 1)) = 0.4
        _HorizonHazeFalloff ("Épaisseur de la brume (plus grand = plus fin)", Range(1, 40)) = 14

        [Header(Soleil)]
        _SunSize ("Taille du disque (rad)", Range(0.005, 0.2)) = 0.05
        _SunIntensity ("Intensité du disque (HDR)", Range(1, 60)) = 14
        _SunHaloIntensity ("Intensité du halo", Range(0, 10)) = 1.6
        _SunHaloSharpness ("Finesse du halo", Range(5, 2000)) = 260
        _SunRays ("Rayons autour du soleil", Range(0, 2)) = 0.45
        _SunRayCount ("Nombre de rayons", Range(4, 40)) = 14

        [Header(Lune)]
        _MoonSize ("Taille de la lune (rad)", Range(0.01, 0.25)) = 0.05
        _MoonIntensity ("Intensité (HDR)", Range(0.5, 10)) = 2.6
        _MoonPhase ("Phase (0 = pleine)", Range(0, 1)) = 0.12
        _MoonHaloIntensity ("Intensité du halo", Range(0, 4)) = 0.9

        [Header(Etoiles)]
        _StarDensity ("Densité", Range(20, 400)) = 160
        _StarSize ("Taille", Range(0.02, 0.4)) = 0.16
        _StarIntensity ("Intensité (HDR)", Range(0, 10)) = 3.5
        _StarTwinkle ("Scintillement", Range(0, 1)) = 0.6

        [Header(Nuages)]
        _CloudScale ("Échelle", Range(0.2, 8)) = 2.0
        _CloudCoverage ("Couverture", Range(0, 1)) = 0.42
        _CloudCoverageVariation ("Variation de couverture (bancs de nuages)", Range(0, 1)) = 0.55
        _CloudSoftness ("Douceur des bords", Range(0.01, 0.5)) = 0.09
        _CloudContrast ("Contraste des formes", Range(1, 5)) = 3.0
        _CloudSpeed ("Vitesse du vent", Range(0, 0.2)) = 0.012
        _CloudCurvature ("Courbure de la couche", Range(0.02, 0.6)) = 0.16
        _CloudLightStep ("Décalage pour l'ombrage", Range(0.01, 0.6)) = 0.16
        _CloudShade ("Ombre sous les nuages", Range(0, 1)) = 0.6
        _CloudLightContrast ("Contraste côté astre", Range(0, 8)) = 2.5
        _CloudWarp ("Volutes (distorsion)", Range(0, 2)) = 0.3
        _CloudBands ("Netteté des aplats (peint)", Range(0, 1)) = 0.55
        _CloudSilverLining ("Liseré lumineux à contre-jour", Range(0, 4)) = 1.6
        _CloudSunClearing ("Ciel dégagé autour du soleil / de la lune", Range(0, 1)) = 0.55
        _CloudSeed ("Graine", Vector) = (13.7, 4.1, 0, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _ZenithExponent, _HorizonHaze, _HorizonHazeFalloff;
                float _SunSize, _SunIntensity, _SunHaloIntensity, _SunHaloSharpness, _SunRays, _SunRayCount;
                float _MoonSize, _MoonIntensity, _MoonPhase, _MoonHaloIntensity;
                float _StarDensity, _StarSize, _StarIntensity, _StarTwinkle;
                float _CloudScale, _CloudContrast, _CloudCoverage, _CloudCoverageVariation, _CloudSoftness, _CloudSpeed, _CloudCurvature;
                float _CloudLightStep, _CloudShade, _CloudLightContrast, _CloudWarp, _CloudBands, _CloudSilverLining, _CloudSunClearing;
                float4 _CloudSeed;
            CBUFFER_END

            // --- Globales écrites par DayNightCycle ---
            float3 _TT_SunDir;
            float3 _TT_MoonDir;
            half3 _TT_SkyZenith;
            half3 _TT_SkyHorizon;
            half3 _TT_SkyGround;
            half3 _TT_SunGlowColor;
            half3 _TT_SunDiscColor;
            half3 _TT_MoonColor;
            half3 _TT_CloudLit;
            half3 _TT_CloudShadow;
            half3 _TT_FogColor;
            float _TT_SunGlow;
            float _TT_SunVisibility;
            float _TT_MoonVisibility;
            float _TT_StarsVisibility;
            float _TT_CloudLightFromMoon; // 0 = nuages éclairés par le soleil, 1 = par la lune

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dirWS : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dirWS = v.positionOS.xyz;
                return o;
            }

            // ---------- Bruit (hash entier : stable même loin de l'origine) ----------
            uint Pcg(uint v)
            {
                uint state = v * 747796405u + 2891336453u;
                uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
                return (word >> 22u) ^ word;
            }
            float Hash21(int2 p) { return Pcg(asuint(p.x) + Pcg(asuint(p.y))) * (1.0 / 4294967295.0); }
            float Hash31(int3 p) { return Pcg(asuint(p.x) + Pcg(asuint(p.y) + Pcg(asuint(p.z)))) * (1.0 / 4294967295.0); }

            float ValueNoise(float2 p)
            {
                int2 i = (int2)floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + int2(1, 0));
                float c = Hash21(i + int2(0, 1));
                float d = Hash21(i + int2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Bruit « gonflé » : les octaves basses font les masses, les hautes les bords en chou-fleur.
            // octaves = 5 pour la forme, 3 pour les échantillons d'éclairage (moins cher).
            float CloudFbm(float2 p, int octaves)
            {
                float sum = 0.0;
                float amp = 0.5;
                float norm = 0.0;
                const float2x2 rot = float2x2(0.8, -0.6, 0.6, 0.8);
                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    if (i < octaves)
                    {
                        float n = ValueNoise(p);
                        // Octaves hautes en « billow » : contour boursouflé façon cumulus.
                        n = (i < 2) ? n : 1.0 - abs(n * 2.0 - 1.0);
                        sum += n * amp;
                        norm += amp;
                    }
                    p = mul(rot, p) * 2.07 + 17.1;
                    amp *= 0.45;
                }
                return sum / norm;
            }

            float CloudDensityAt(float2 uv, float coverage, int octaves)
            {
                // Distorsion de domaine : formes moins régulières.
                float2 warp = float2(ValueNoise(uv * 0.5 + 31.7), ValueNoise(uv * 0.5 + 7.3)) - 0.5;
                float n = CloudFbm(uv + warp * _CloudWarp, octaves);
                // Le fbm reste serré autour de 0,5 : on l'étire pour que « Couverture » soit lisible (0 = rien, 1 = couvert).
                n = saturate((n - 0.5) * _CloudContrast + 0.5);
                return saturate((n - (1.0 - coverage)) / _CloudSoftness);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 dir = normalize(i.dirWS);
                float y = dir.y;
                float up = saturate(y);

                // ---------- Dégradé ----------
                half3 sky = lerp(_TT_SkyHorizon, _TT_SkyZenith, pow(up, _ZenithExponent));
                sky = (y < 0.0) ? lerp(_TT_SkyHorizon, _TT_SkyGround, saturate(-y * 5.0)) : sky;

                // Halo du soleil (large lueur qui colore l'horizon + coeur plus serré).
                float sunDot = dot(dir, _TT_SunDir);
                float sunAbove = smoothstep(-0.25, 0.05, _TT_SunDir.y);
                float horizonMask = exp(-abs(y) * 3.0);
                float broadGlow = pow(saturate(sunDot * 0.5 + 0.5), 6.0) * horizonMask;
                float midGlow = pow(saturate(sunDot), 22.0);
                sky += _TT_SunGlowColor * (broadGlow * 0.9 + midGlow * 0.6) * _TT_SunGlow * sunAbove;

                // Brume claire à l'horizon, raccord avec le brouillard de la scène.
                float haze = exp(-max(y, 0.0) * _HorizonHazeFalloff) * _HorizonHaze;
                sky = lerp(sky, _TT_FogColor, saturate(haze));

                // ---------- Étoiles ----------
                half3 stars = 0;
                if (_TT_StarsVisibility > 0.001)
                {
                    [unroll]
                    for (int layer = 0; layer < 2; layer++)
                    {
                        float density = _StarDensity * (layer == 0 ? 1.0 : 0.45);
                        float3 p = dir * density;
                        int3 cell = (int3)floor(p);
                        float3 f = frac(p);
                        float h = Hash31(cell + layer * 1013);
                        float3 starPos = 0.25 + 0.5 * float3(h, Hash31(cell + 71), Hash31(cell + 137));
                        float d = length(f - starPos);
                        float size = _StarSize * (layer == 0 ? 0.6 : 1.0) * (0.5 + h);
                        float star = smoothstep(size, 0.0, d);
                        float keep = step(layer == 0 ? 0.55 : 0.82, Hash31(cell + 911));
                        float twinkle = 1.0 - _StarTwinkle * 0.5 * (1.0 + sin(_Time.y * (2.0 + h * 5.0) + h * 40.0));
                        stars += star * keep * twinkle * (0.4 + h);
                    }
                    stars *= _StarIntensity * _TT_StarsVisibility * smoothstep(0.02, 0.25, y);
                }
                sky += stars;

                // ---------- Lune ----------
                float moonDot = dot(dir, _TT_MoonDir);
                half3 moon = 0;
                float moonDisc = 0;
                if (_TT_MoonVisibility > 0.001)
                {
                    float3 mUp = abs(_TT_MoonDir.y) > 0.99 ? float3(1, 0, 0) : float3(0, 1, 0);
                    float3 mRight = normalize(cross(mUp, _TT_MoonDir));
                    float3 mTop = cross(_TT_MoonDir, mRight);
                    float2 muv = float2(dot(dir, mRight), dot(dir, mTop)) / _MoonSize;
                    float r2 = dot(muv, muv);
                    moonDisc = smoothstep(1.0, 0.94, sqrt(r2)) * step(0.0, moonDot) * smoothstep(-0.02, 0.02, y);
                    float3 n = float3(muv, sqrt(saturate(1.0 - r2)));
                    // Phase : lumière venant de la gauche, ombre douce à droite.
                    float3 phaseDir = normalize(float3(-_MoonPhase * 4.0, 0.25, 1.0 - _MoonPhase));
                    float lit = smoothstep(-0.05, 0.35, dot(n, phaseDir));
                    float craters = ValueNoise(muv * 2.6 + 3.0) * 0.6 + ValueNoise(muv * 6.0 + 9.0) * 0.4;
                    float shade = lerp(0.72, 1.0, smoothstep(0.35, 0.7, craters));
                    moon = _TT_MoonColor * _MoonIntensity * lerp(0.08, 1.0, lit) * shade * moonDisc;
                    float halo = pow(saturate(moonDot), 900.0) * 1.2 + pow(saturate(moonDot), 60.0) * 0.35 + pow(saturate(moonDot), 8.0) * 0.12;
                    sky += _TT_MoonColor * halo * _MoonHaloIntensity * _TT_MoonVisibility * smoothstep(-0.1, 0.05, _TT_MoonDir.y);
                    sky = lerp(sky, moon, moonDisc * _TT_MoonVisibility);
                }

                // ---------- Soleil ----------
                float sunAngle = acos(clamp(sunDot, -1.0, 1.0));
                float sunDisc = smoothstep(_SunSize * 1.08, _SunSize * 0.92, sunAngle);
                float halo = pow(saturate(sunDot), _SunHaloSharpness) * _SunHaloIntensity
                           + pow(saturate(sunDot), _SunHaloSharpness * 0.12) * _SunHaloIntensity * 0.1;
                // Rayons en étoile, très doux.
                float3 sUp = abs(_TT_SunDir.y) > 0.99 ? float3(1, 0, 0) : float3(0, 1, 0);
                float3 sRight = normalize(cross(sUp, _TT_SunDir));
                float3 sTop = cross(_TT_SunDir, sRight);
                float ang = atan2(dot(dir, sTop), dot(dir, sRight));
                float rays = ValueNoise(float2(ang * _SunRayCount / 6.2831 * 2.0 + _Time.y * 0.05, 0.5));
                rays += ValueNoise(float2(ang * _SunRayCount / 6.2831 * 5.0 - _Time.y * 0.03, 3.5)) * 0.5;
                rays = pow(saturate(rays / 1.5), 3.0) * pow(saturate(sunDot), 30.0) * step(0.0, sunDot);
                half3 sun = _TT_SunDiscColor * (sunDisc * _SunIntensity + halo + rays * _SunRays * 3.0);
                float aboveHorizon = smoothstep(-0.02, 0.02, y);
                sky += sun * _TT_SunVisibility * aboveHorizon;

                // ---------- Nuages ----------
                if (y > -0.02)
                {
                    float2 cuv = dir.xz / (y + _CloudCurvature) * _CloudScale + _CloudSeed.xy;
                    float2 wind = float2(1.0, 0.35) * _Time.y * _CloudSpeed;
                    cuv += wind;

                    // Lumière principale des nuages : soleil le jour, lune la nuit.
                    float3 L = normalize(lerp(_TT_SunDir, _TT_MoonDir, _TT_CloudLightFromMoon));
                    float lightDot = dot(dir, L);

                    // Bancs : la couverture varie à grande échelle et s'ouvre autour de l'astre.
                    float cover = _CloudCoverage + (ValueNoise(cuv * 0.12 + 5.0) - 0.5) * _CloudCoverageVariation;
                    cover -= pow(saturate(lightDot), 10.0) * _CloudSunClearing * 0.9;

                    float density = CloudDensityAt(cuv, cover, 5);
                    if (density > 0.001)
                    {
                        // Dans le plan des nuages, « vers l'horizon » = vers le bas de l'écran.
                        float2 radial = dir.xz / max(length(dir.xz), 1e-3);
                        // Densité au-dessus (vers le zénith) : le haut des nuages est éclairé, la base est à l'ombre.
                        float above1 = CloudDensityAt(cuv - radial * _CloudLightStep, cover, 3);
                        float above2 = CloudDensityAt(cuv - radial * _CloudLightStep * 2.6, cover, 3);
                        float light = 1.0 - (above1 * 0.6 + above2 * 0.4) * _CloudShade;
                        // Côté tourné vers l'astre un peu plus clair.
                        float2 lightDir2 = L.xz / max(length(L.xz), 1e-3);
                        float towardLight = CloudDensityAt(cuv + lightDir2 * _CloudLightStep * 1.5, cover, 3);
                        light = saturate(light + (density - towardLight) * 0.12 * _CloudLightContrast);
                        // Aplats peints : on resserre la transition sans la rendre dure.
                        float banded = smoothstep(0.32, 0.68, light);
                        light = lerp(light, banded, _CloudBands);

                        half3 cloudCol = lerp(_TT_CloudShadow, _TT_CloudLit, light);
                        // Liseré lumineux sur les bords fins quand on regarde vers la lumière.
                        float edge = saturate(1.0 - density) * pow(saturate(lightDot), 5.0);
                        half3 rimColor = lerp(_TT_SunGlowColor, _TT_MoonColor, _TT_CloudLightFromMoon);
                        cloudCol += rimColor * edge * _CloudSilverLining * (0.3 + 0.7 * _TT_SunGlow);
                        // Les nuages lointains se fondent dans la brume de l'horizon.
                        float far = exp(-max(y, 0.0) * 6.0);
                        cloudCol = lerp(cloudCol, _TT_FogColor, far * 0.55);

                        float alpha = density * smoothstep(-0.02, 0.12, y);
                        // L'astre transperce légèrement les nuages fins.
                        sky = lerp(sky, cloudCol, alpha);
                        sky += _TT_SunDiscColor * sunDisc * _SunIntensity * 0.08 * alpha * (1.0 - density) * _TT_SunVisibility;
                    }
                }

                return half4(max(sky, 0.0), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
