// OWAdditiveGlow —— 加色中心光晕（流场扰动版）
//
// 用途：节点光点、粒子的中心亮核、扫描线扫过时的高光。
// 做法：主贴图 UV 被一张法线流场推动，再叠加噪波的滚动；
//       可选「中心光晕」用遮罩纹理做阈值，只保留核心的亮部。
//
// 来源说明：思路参考用户已购买的 CooGee Works《立体音乐魔法》里的 CooGee/Add_CenterGlow
// （MainTex × Noise × Flow 扰动 × Mask 阈值中心光晕），代码为本项目自写。
//
// VRChat 注意事项：加色很容易过曝，PC 世界里 _Emission 建议 ≤ 2；
// 与后处理 Bloom 叠加时要再压低，或把 Bloom 阈值提高。

Shader "OldWebMap/Additive Center Glow"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Noise ("Noise", 2D) = "white" {}
        _Flow ("Flow (normal map)", 2D) = "bump" {}
        _Mask ("Mask", 2D) = "white" {}

        _MainScroll ("Main Scroll XY", Vector) = (0.05, 0.02, 0, 0)
        _NoiseScroll ("Noise Scroll XY", Vector) = (0.03, -0.04, 0, 0)
        _FlowPower ("Flow Power", Range(0, 1)) = 0.25

        [Toggle] _UseCenterGlow ("Use Center Glow", Float) = 1
        _CenterThreshold ("Center Threshold", Range(0, 1)) = 0.75
        _CenterSoftness ("Center Softness", Range(0.001, 0.5)) = 0.1

        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Emission ("Emission", Range(0, 8)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
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

        Blend One One
        ColorMask RGB
        Cull [_Cull]
        Lighting Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_particles
            #pragma multi_compile_fog
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
                UNITY_FOG_COORDS(1)
            };

            sampler2D _MainTex; float4 _MainTex_ST;
            sampler2D _Noise; float4 _Noise_ST;
            sampler2D _Flow; float4 _Flow_ST;
            sampler2D _Mask; float4 _Mask_ST;

            float4 _MainScroll, _NoiseScroll;
            float _FlowPower, _CenterThreshold, _CenterSoftness;
            float _UseCenterGlow;
            fixed4 _Color;
            float _Emission;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 mainUV = i.uv * _MainTex_ST.xy + _MainTex_ST.zw + _Time.y * _MainScroll.xy;
                float2 noiseUV = i.uv * _Noise_ST.xy + _Noise_ST.zw + _Time.y * _NoiseScroll.xy;
                float2 flowUV = i.uv * _Flow_ST.xy + _Flow_ST.zw;

                // 流场：用一张法线的 xy 推动主贴图 UV，制造烟/能量的流动感
                float2 flow = (UnpackNormal(tex2D(_Flow, flowUV)).xy) * _FlowPower;
                fixed4 main = tex2D(_MainTex, mainUV + flow);
                fixed4 noise = tex2D(_Noise, noiseUV);

                fixed4 col = main * noise * i.color * _Color;

                // 中心光晕：遮罩里高于阈值的部分保留，其余压暗，形成亮核
                float mask = tex2D(_Mask, i.uv).r;
                float core = smoothstep(_CenterThreshold, _CenterThreshold + _CenterSoftness, mask);
                col.rgb *= lerp(1.0, core, saturate(_UseCenterGlow));

                col.rgb *= _Emission;
                col.a = saturate(col.a);

                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0, 0, 0, 0));
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
