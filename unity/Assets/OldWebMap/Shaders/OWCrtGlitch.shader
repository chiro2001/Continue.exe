// OWCrtGlitch —— 屏幕/画布用的 CRT 故障效果
//
// 用途：失效链接的「花屏」、复制粘贴时的数据损坏、旧网页截图的老化表现。
// 用法：贴在全屏 Quad、世界里的「屏幕」面片，或包一层 Blit 当作后期。
// 做法：按方块与扫描线两层哈希，周期性触发位移/灰度/色偏/马赛克，
//       最后叠 CRT 扫描线与色调。
//
// 来源说明：思路参考用户已购买的 Floppiii《Field of Stars》里附带的
// Leviant ScreenSpace Ubershader（MIT License, © 2019 Leviant,
// https://github.com/Leviant/ScreenSpace_Ubershader）中的 glitch 段落：
// 分块/分行哈希 + 周期强度 + 多种故障样式。本文件的哈希与实现为本项目自写。
//
// VRChat 注意事项：
//   - 走 Quest 时降低 _PosterizeSteps 与采样次数（把 _Chromatic 设为 0）。
//   - 作为世界内屏幕使用时把它指向 RenderTexture；不要依赖相机抓屏。

Shader "OldWebMap/CRT Glitch"
{
    Properties
    {
        _MainTex ("Screen Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Emission ("Emission", Range(0, 4)) = 1

        [Header(Glitch Timing)]
        _UPS ("Glitches Per Second", Float) = 12
        _PeriodTime ("Glitch Period (s)", Float) = 6
        _ActiveTime ("Active Portion (0.1 = short spikes)", Range(0.05, 1)) = 0.35

        [Header(Glitch Shape)]
        _BlockSize ("Block Count XY", Float) = 24
        _BlockAmount ("Block Glitch Amount", Range(0, 1)) = 0.25
        _LineAmount ("Line Glitch Amount", Range(0, 1)) = 0.35
        _Shift ("Displace Amount", Range(0, 0.5)) = 0.05
        _Chromatic ("Chromatic Aberration", Range(0, 0.05)) = 0.004
        _Grayscale ("Grayscale Amount", Range(0, 1)) = 0.35
        _PosterizeSteps ("Posterize Steps", Float) = 16

        [Header(CRT)]
        _ScanlineCount ("Scanline Count", Float) = 240
        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0.25
        _ScanlineSpeed ("Scanline Speed", Float) = 0.5
        _Vignette ("Vignette", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Tint;
            float _Emission;
            float _UPS, _PeriodTime, _ActiveTime;
            float _BlockSize, _BlockAmount, _LineAmount, _Shift, _Chromatic;
            float _Grayscale, _PosterizeSteps;
            float _ScanlineCount, _ScanlineStrength, _ScanlineSpeed, _Vignette;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            // 一维哈希：把时间/编号打散成 0..1 的伪随机
            float OWMHash11 (float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            // 二维哈希：给方块编号用
            float OWMHash21 (float2 p)
            {
                float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // 周期性强度：_ActiveTime 越小，故障来得越短促
                float activeExponent = max(0.0001, 1.0 / _ActiveTime - 1.0);
                float period = pow(abs(sin(_Time.y * UNITY_PI / max(_PeriodTime, 0.0001))), activeExponent);

                float time = floor(_Time.y * max(_UPS, 0.0001));
                float seed = OWMHash11(time * 0.017);

                float2 blockIndex = floor(uv * _BlockSize);
                float lineIndex = blockIndex.y;

                float blockNoise = OWMHash21(blockIndex + seed);
                float lineNoise = OWMHash11(lineIndex + seed + 7.77);

                float blockThreshold = _BlockAmount * period;
                float lineThreshold = _LineAmount * period * period;

                float2 offset = float2(0, 0);

                // 方块级位移：整块向一侧撕开
                if (blockNoise < blockThreshold)
                {
                    offset.x += (OWMHash11(seed + blockIndex.x * 1.7 + blockIndex.y * 3.1) - 0.5) * _Shift * 2.0;
                    offset.y += (OWMHash11(seed + blockIndex.y * 5.3) - 0.5) * _Shift * 0.5;
                }

                // 行级位移：模拟扫描线撕裂
                if (lineNoise < lineThreshold)
                {
                    offset.x += (OWMHash11(seed + lineIndex * 9.1) - 0.5) * _Shift * 4.0;
                }

                float2 glitchedUV = saturate(uv + offset);
                fixed4 col = tex2D(_MainTex, glitchedUV);

                // 色散：按故障强度偏移红蓝通道
                float ca = _Chromatic * (0.25 + period);
                col.r = tex2D(_MainTex, saturate(glitchedUV + float2(ca, 0))).r;
                col.b = tex2D(_MainTex, saturate(glitchedUV - float2(ca, 0))).b;

                // 灰度与色偏：让部分方块掉色，模拟信号丢失
                float gray = Luminance(col.rgb);
                float grayAmount = _Grayscale * saturate(blockThreshold * 3.0 + lineThreshold * 2.0);
                col.rgb = lerp(col.rgb, gray.xxx, grayAmount);

                // 色阶压缩：低色阶更接近老屏幕与 GIF 质感
                float steps = max(_PosterizeSteps, 2.0);
                col.rgb = floor(col.rgb * steps + 0.5) / steps;

                // CRT 扫描线
                float scan = 0.5 + 0.5 * sin(uv.y * _ScanlineCount * UNITY_PI * 2.0
                                              + _Time.y * _ScanlineSpeed * 10.0);
                col.rgb *= lerp(1.0, scan, _ScanlineStrength);

                // 暗角
                float2 centered = uv - 0.5;
                float vignette = 1.0 - dot(centered, centered) * 2.0 * _Vignette;
                col.rgb *= saturate(vignette);

                col *= _Tint * i.color;
                col.rgb *= _Emission;
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
