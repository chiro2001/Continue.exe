// OWHeadSignalNoise —— 头部全屏「信号丢失」层
//
// 用途：网盘峡的失效链接、开源荒原的归档中断、VRChat 城的被摘除瞬间。
//   第一遍（SNOW，alpha 混合）铺一层半透明的灰雪噪点，读数越低越像雪花屏；
//   第二遍（SPARK，加色）叠亮条、丢行、扫描缺口，做出「信号在挣扎」的观感。
//
// 与 OWHeadAtmosphere 的区别：那一支是长期氛围（常态极轻、脉冲短暂），
// 这一支只在该段被唤起，强度由 _Intensity 从 0 推到 1，再落回 0。
//
// 来源说明：雪花噪点、丢行、滚动亮带属于通用 CRT/VHS 语言，
// 思路参考用户自购的 Floppiii《Field of Stars》与 CooGee Works《立体音乐魔法》；
// 本文件为自写实现（哈希、丢行与亮带均为自己写），未复制原资产代码。

Shader "OldWebMap/Head Signal Noise"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 1)) = 0

        [Header(Snow)]
        _SnowTex ("Snow Noise", 2D) = "gray" {}
        _SnowColor ("Snow Color", Color) = (0.75, 0.78, 0.8, 1)
        _SnowScale ("Snow Scale", Float) = 12
        _SnowSpeed ("Snow FPS", Float) = 24
        _SnowThreshold ("Snow Threshold", Range(0, 1)) = 0.55
        _SnowSoftness ("Snow Softness", Range(0.001, 0.5)) = 0.15

        [Header(Dropouts)]
        _DropColor ("Dropout Color", Color) = (1, 1, 1, 1)
        _DropAmount ("Dropout Amount", Range(0, 1)) = 0.35
        _DropSpeed ("Dropout Speed", Float) = 9
        _DropThickness ("Dropout Thickness", Range(0.002, 0.2)) = 0.02

        [Header(Rolling bar)]
        _RollColor ("Roll Bar Color", Color) = (0.85, 0.9, 1, 1)
        _RollAmount ("Roll Bar Amount", Range(0, 1)) = 0.4
        _RollSpeed ("Roll Bar Speed", Float) = 0.25
        _RollSharpness ("Roll Bar Sharpness", Float) = 18

        [Header(Scan gaps)]
        _GapAmount ("Scan Gaps", Range(0, 1)) = 0.3
        _GapCount ("Gap Count", Float) = 140
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

        // ---- 雪花层：alpha 混合的一层灰噪 ----
        Pass
        {
            Name "SNOW"
            Blend SrcAlpha OneMinusSrcAlpha

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

            sampler2D _SnowTex;
            fixed4 _SnowColor;
            float _Intensity, _SnowScale, _SnowSpeed, _SnowThreshold, _SnowSoftness;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 每帧换一组噪声偏移，形成跳动感
                float frame = floor(_Time.y * max(_SnowSpeed, 0.001));
                float2 uv = i.uv * _SnowScale + float2(frame * 0.2718, frame * 0.4713);
                float snow = tex2D(_SnowTex, uv).r;

                // 阈值化：只有高于阈值的高频部分才成为雪花
                float mask = smoothstep(_SnowThreshold, _SnowThreshold + _SnowSoftness, snow);
                float alpha = saturate(mask * _Intensity);

                fixed4 col = fixed4(_SnowColor.rgb * snow, alpha * _SnowColor.a);
                return col;
            }
            ENDCG
        }

        // ---- 加色层：丢行 + 滚动亮带 + 扫描缺口 ----
        Pass
        {
            Name "SPARK"
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

            fixed4 _DropColor, _RollColor;
            float _Intensity, _DropAmount, _DropSpeed, _DropThickness;
            float _RollAmount, _RollSpeed, _RollSharpness;
            float _GapAmount, _GapCount;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float OWMHash11 (float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float3 acc = 0;
                float amount = saturate(_Intensity);

                // 丢行：按行号哈希，偶发几行整体提亮成撕痕
                float lineId = floor(uv.y / max(_DropThickness, 0.001));
                float lineRand = OWMHash11(lineId + floor(_Time.y * _DropSpeed) * 0.137);
                float dropLine = step(1.0 - _DropAmount * amount * 0.35, lineRand);
                acc += _DropColor.rgb * dropLine * 0.8;

                // 滚动亮带：自下而上扫过，像录像带换帧
                float rollPos = frac(_Time.y * max(_RollSpeed, 0.001));
                float roll = exp(-pow((uv.y - rollPos) * _RollSharpness, 2.0));
                acc += _RollColor.rgb * roll * _RollAmount * amount;

                // 扫描缺口：每隔若干行压暗一条，制造不稳定的横向断裂
                float gap = step(1.0 - _GapAmount * amount * 0.4,
                                 OWMHash11(floor(uv.y * _GapCount) + floor(_Time.y * 7.0) * 0.211));
                acc *= lerp(1.0, gap, 0.35);

                return fixed4(acc, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
