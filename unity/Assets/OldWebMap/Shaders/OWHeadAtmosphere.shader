// OWHeadAtmosphere —— 头部全屏气氛层（CRT 旧屏 + 呼吸感）
//
// 用法：贴在一块固定在本地玩家视野前方的面片上（配合 OWHeadOverlayRig）。
//   第一遍（BASE，乘法）压暗四角、加色调与胶片颗粒、可选的黑边；
//   第二遍（DETAIL，加色）叠扫描线、脉冲闪光、扫描条与数字噪块。
// 因此它不需要抓屏、不依赖后处理，PC 与 Quest 都能用（两遍共 4 个三角形）。
//
// 参数分为「常态」与「脉冲」两组：常态用很小的值做长期氛围；
// 脉冲由时间轴/音乐事件在短时间内推到 1，用完自动衰减（见 OWHeadOverlayRig）。
//
// 来源说明：思路参考用户自购的 CooGee Works《立体音乐魔法》与 Floppiii《Field of Stars》
// （加色光晕、噪声、遮罩的分层思路）；本文件为自写实现，未复制原资产代码。

Shader "OldWebMap/Head Atmosphere Overlay"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 2)) = 1

        [Header(Base Multiply)]
        _TintColor ("Tint", Color) = (1, 0.94, 0.86, 1)
        _TintStrength ("Tint Strength", Range(0, 1)) = 0.12
        _VignetteStrength ("Vignette Strength", Range(0, 2)) = 0.5
        _VignetteRadius ("Vignette Radius", Range(0, 1.5)) = 0.55
        _VignetteSoftness ("Vignette Softness", Range(0.01, 1.5)) = 0.75
        _Aspect ("Aspect Compensation", Range(0.5, 3)) = 1.6
        _GrainTex ("Grain", 2D) = "gray" {}
        _GrainStrength ("Grain Strength", Range(0, 1)) = 0.06
        _GrainScale ("Grain Scale", Float) = 8
        _GrainSpeed ("Grain FPS", Float) = 12
        _Letterbox ("Letterbox", Range(0, 0.5)) = 0
        _LetterboxSoftness ("Letterbox Softness", Range(0.001, 0.3)) = 0.03

        [Header(Detail Additive)]
        _ScanlineColor ("Scanline Color", Color) = (0.35, 0.75, 1, 1)
        _ScanlineCount ("Scanline Count", Float) = 320
        _ScanlineStrength ("Scanline Strength", Range(0, 2)) = 0.12
        _ScanlineSpeed ("Scanline Speed", Float) = 0.35

        [Header(Pulse)]
        _Pulse ("Pulse (0..1)", Range(0, 1)) = 0
        _PulseColor ("Pulse Color", Color) = (0.85, 0.95, 1, 1)
        _PulseFlash ("Flash", Range(0, 1)) = 0.22
        _PulseScanBar ("Scan Bar", Range(0, 1)) = 0.55
        _PulseBlocks ("Digital Blocks", Range(0, 1)) = 0.35
        _PulseScanSpeed ("Scan Bar Speed", Float) = 0.6

    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Fog { Mode Off }

        // ---- 乘法层：四角压暗 + 色调 + 颗粒 + 黑边 ----
        Pass
        {
            Name "BASE"
            Blend DstColor Zero

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            sampler2D _GrainTex;
            float4 _GrainTex_ST;
            fixed4 _TintColor;
            float _Intensity, _TintStrength, _VignetteStrength, _VignetteRadius;
            float _VignetteSoftness, _Aspect, _GrainStrength, _GrainScale, _GrainSpeed;
            float _Letterbox, _LetterboxSoftness;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // 四角压暗：把 uv 换算到以中心为原点的椭圆距离
                float2 centered = (uv - 0.5) * 2.0;
                centered.x *= _Aspect;
                float dist = length(centered);
                float vignette = 1.0 - saturate((dist - _VignetteRadius) / max(_VignetteSoftness, 0.001));
                vignette = lerp(1.0, vignette, saturate(_VignetteStrength * _Intensity));

                // 色调：偏暖/偏青由 _TintColor 控制
                float3 tint = lerp(float3(1, 1, 1), _TintColor.rgb, saturate(_TintStrength * _Intensity));

                // 胶片颗粒：以离散帧推进，避免每帧噪声抖动过强
                float frame = floor(_Time.y * max(_GrainSpeed, 0.001));
                float2 grainUV = uv * _GrainScale + float2(frame * 0.1731, frame * 0.3177);
                float grain = tex2D(_GrainTex, grainUV).r;
                grain = lerp(1.0, grain, saturate(_GrainStrength * _Intensity));

                // 上下黑边：edge 越靠近上下缘越小；_Letterbox 为 0 时完全不生效
                float edge = min(uv.y, 1.0 - uv.y);
                float letterbox = 1.0 - saturate((_Letterbox - edge) / max(_LetterboxSoftness, 0.001));

                float3 mult = tint * vignette * grain * letterbox;
                return fixed4(mult, 1.0);
            }
            ENDCG
        }

        // ---- 加色层：扫描线 + 脉冲闪光 + 扫描条 + 数字噪块 ----
        Pass
        {
            Name "DETAIL"
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _ScanlineColor, _PulseColor;
            float _Intensity, _ScanlineCount, _ScanlineStrength, _ScanlineSpeed;
            float _Pulse, _PulseFlash, _PulseScanBar, _PulseBlocks, _PulseScanSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 二维哈希：给数字噪块做分组随机
            float OWMHash21 (float2 p)
            {
                float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float3 acc = 0;

                // 扫描线：常态极轻，用于老屏幕质感
                float scan = 0.5 + 0.5 * sin(uv.y * _ScanlineCount * 6.2831853
                                              + _Time.y * _ScanlineSpeed * 6.2831853);
                acc += _ScanlineColor.rgb * (scan * _ScanlineStrength * _Intensity);

                // 脉冲：整屏提亮一下，像信号被顶了一下
                float pulse = saturate(_Pulse);
                acc += _PulseColor.rgb * (pulse * _PulseFlash * _Intensity);

                // 脉冲：一条缓慢上移的亮带（开机/扫描）
                float barPos = frac(_Time.y * max(_PulseScanSpeed, 0.001));
                float bar = exp(-pow((uv.y - barPos) * 26.0, 2.0));
                acc += _PulseColor.rgb * (bar * _PulseScanBar * pulse * _Intensity);

                // 脉冲：少数字块闪一下，对应数据损坏
                float2 blockId = floor(uv * float2(28.0, 16.0));
                float blockRand = OWMHash21(blockId + floor(_Time.y * 12.0) * 0.017);
                float blockOn = step(1.0 - _PulseBlocks * pulse, blockRand);
                acc += _PulseColor.rgb * (blockOn * 0.25 * pulse * _Intensity);

                return fixed4(acc, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
