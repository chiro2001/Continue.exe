// OWAnimeParticle —— 三层贴图粒子着色器
//
// 用途：音符粒子、星芒、节点发光碎屑。
// 做法：每层贴图独立滚动 + 旋转，用一张法线贴图给 UV 加扰流；
//       三层相加后再乘遮罩贴图与粒子顶点色，最后统一发光强度。
//
// 来源说明：思路参考用户已购买的 VRChat 特效资产（CooGee Works《立体音乐魔法》的
// Add_CenterGlow、Floppiii《Field of Stars》的 AnimeParticlesV1.1），代码为本项目自写，
// 未复制原资产内容。原资产只作参考，不随本仓库分发。
//
// VRChat 注意事项：
//   - PC 世界可用；走 Quest 时把 _Emission 压低、关闭第三层、贴图压到 512 以内。
//   - 默认合成方式是加色（SrcAlpha One）；要改成普通透明把 _DstBlend 设为 10。

Shader "OldWebMap/Anime Particle (3 Layer)"
{
    Properties
    {
        [Header(Layer 1)]
        _Tex1 ("Texture", 2D) = "white" {}
        _Tex1Strength ("Strength", Range(0, 4)) = 1
        _Tex1UseColor ("Use Color (0 = grayscale)", Range(0, 1)) = 1
        _Tex1Scroll ("Scroll XY", Vector) = (0.1, 0, 0, 0)
        _Tex1Rotate ("Rotation Speed", Range(-2, 2)) = 0
        _Dist1 ("Distortion (normal map)", 2D) = "bump" {}
        _Dist1Strength ("Distortion Strength", Range(0, 1)) = 0
        _Dist1Scroll ("Distortion Scroll XY", Vector) = (0.1, 0, 0, 0)

        [Header(Layer 2)]
        _Tex2 ("Texture", 2D) = "white" {}
        _Tex2Strength ("Strength", Range(0, 4)) = 0.5
        _Tex2UseColor ("Use Color (0 = grayscale)", Range(0, 1)) = 1
        _Tex2Scroll ("Scroll XY", Vector) = (-0.07, 0.03, 0, 0)
        _Tex2Rotate ("Rotation Speed", Range(-2, 2)) = 0
        _Dist2 ("Distortion (normal map)", 2D) = "bump" {}
        _Dist2Strength ("Distortion Strength", Range(0, 1)) = 0
        _Dist2Scroll ("Distortion Scroll XY", Vector) = (0.1, 0, 0, 0)

        [Header(Layer 3)]
        _Tex3 ("Texture", 2D) = "white" {}
        _Tex3Strength ("Strength", Range(0, 4)) = 0.25
        _Tex3UseColor ("Use Color (0 = grayscale)", Range(0, 1)) = 1
        _Tex3Scroll ("Scroll XY", Vector) = (0.04, -0.05, 0, 0)
        _Tex3Rotate ("Rotation Speed", Range(-2, 2)) = 0
        _Dist3 ("Distortion (normal map)", 2D) = "bump" {}
        _Dist3Strength ("Distortion Strength", Range(0, 1)) = 0
        _Dist3Scroll ("Distortion Scroll XY", Vector) = (0.1, 0, 0, 0)

        [Header(Mask)]
        _Mask ("Mask", 2D) = "white" {}
        _MaskStrength ("Mask Strength", Range(0, 2)) = 1
        _MaskScroll ("Mask Scroll XY", Vector) = (0, 0, 0, 0)
        _MaskRotate ("Mask Rotation Speed", Range(-2, 2)) = 0
        _MaskDist ("Mask Distortion", 2D) = "white" {}
        _MaskDistStrength ("Mask Distortion Strength", Range(0, 0.5)) = 0
        _MaskDistScroll ("Mask Distortion Scroll XY", Vector) = (0.1, 0, 0, 0)

        [Header(Output)]
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Emission ("Emission", Range(0, 8)) = 1.5
        _AlphaStrength ("Alpha Strength", Range(0, 2)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (1 = additive)", Float) = 1
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

        Blend [_SrcBlend] [_DstBlend]
        Cull [_Cull]
        Lighting Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
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

            sampler2D _Tex1; float4 _Tex1_ST;
            sampler2D _Tex2; float4 _Tex2_ST;
            sampler2D _Tex3; float4 _Tex3_ST;
            sampler2D _Dist1; float4 _Dist1_ST;
            sampler2D _Dist2; float4 _Dist2_ST;
            sampler2D _Dist3; float4 _Dist3_ST;
            sampler2D _Mask; float4 _Mask_ST;
            sampler2D _MaskDist; float4 _MaskDist_ST;

            float _Tex1Strength, _Tex2Strength, _Tex3Strength;
            float _Tex1UseColor, _Tex2UseColor, _Tex3UseColor;
            float4 _Tex1Scroll, _Tex2Scroll, _Tex3Scroll;
            float _Tex1Rotate, _Tex2Rotate, _Tex3Rotate;
            float _Dist1Strength, _Dist2Strength, _Dist3Strength;
            float4 _Dist1Scroll, _Dist2Scroll, _Dist3Scroll;
            float _MaskStrength, _MaskRotate, _MaskDistStrength;
            float4 _MaskScroll, _MaskDistScroll;
            fixed4 _Color;
            float _Emission, _AlphaStrength;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float2 OWMRotate (float2 uv, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return mul(uv - 0.5, float2x2(c, -s, s, c)) + 0.5;
            }

            // 单层采样：旋转 + 滚动 UV，用法线贴图的 xy 扰流，再决定保留彩色或用亮度
            #define OWM_LAYER(IDX)                                                       \
                float2 uv##IDX = OWMRotate(i.uv * _Tex##IDX##_ST.xy + _Tex##IDX##_ST.zw, \
                                           _Time.y * _Tex##IDX##Rotate);                \
                uv##IDX += _Time.y * _Tex##IDX##Scroll.xy;                               \
                float2 duv##IDX = i.uv * _Dist##IDX##_ST.xy + _Dist##IDX##_ST.zw         \
                                  + _Time.y * _Dist##IDX##Scroll.xy;                     \
                float3 dn##IDX = UnpackNormal(tex2D(_Dist##IDX, duv##IDX));              \
                fixed4 c##IDX = tex2D(_Tex##IDX, uv##IDX + dn##IDX.xy * _Dist##IDX##Strength) * i.color; \
                c##IDX.rgb = lerp(Luminance(c##IDX.rgb).xxx, c##IDX.rgb, _Tex##IDX##UseColor); \
                col += c##IDX * _Tex##IDX##Strength;

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = fixed4(0, 0, 0, 0);

                OWM_LAYER(1)
                OWM_LAYER(2)
                OWM_LAYER(3)

                // 遮罩：可滚动、可旋转，并可用第二张噪波进一步扰动边缘
                float2 muv = OWMRotate(i.uv * _Mask_ST.xy + _Mask_ST.zw, _Time.y * _MaskRotate);
                muv += _Time.y * _MaskScroll.xy;
                float2 mduv = i.uv * _MaskDist_ST.xy + _MaskDist_ST.zw + _Time.y * _MaskDistScroll.xy;
                float maskNoise = tex2D(_MaskDist, mduv).r;
                float mask = tex2D(_Mask, muv + (maskNoise - 0.5) * _MaskDistStrength).r * _MaskStrength;

                col *= mask;
                col.rgb *= _Color.rgb * _Emission;
                col.a = saturate(col.a * _Color.a * _AlphaStrength * mask);

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
