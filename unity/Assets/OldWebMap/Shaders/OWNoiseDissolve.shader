// OWNoiseDissolve —— 噪声溶解 + 边缘菲涅尔 + 深度淡出
//
// 用途：节点/道具的「失效、归档、被摘除」溶解过程；四段式进度由 _Progress 驱动
//       （-1 全显 → 0 半溶 → 1 全消），可用动画/Udon 每帧写参数。
// 做法：世界空间三向投影取噪声，叠加菲涅尔边缘与（可选）相机深度淡出，
//       再用进度波前决定本体透明度，并在溶解线上加一圈亮边。
//
// 来源说明：思路参考用户已购买的 CooGee Works《立体音乐魔法》里的 DissolveSphereField
// （Triplanar 噪声 + 菲涅尔 + 深度淡出 + 出现波前），代码为本项目自写。
//
// VRChat 注意事项：
//   - 深度淡出需要相机深度纹理（_CameraDepthTexture）。VRChat 世界相机默认不一定开启，
//     默认关闭该开关，只用菲涅尔 + 噪声；需要时在本地相机/后期里开启 depthTextureMode。
//   - 走 Quest 时把三向投影噪声换成单张 UV 噪声，省掉三次采样。

Shader "OldWebMap/Noise Dissolve"
{
    Properties
    {
        [Header(Look)]
        _Color ("Tint", Color) = (0.2, 0.9, 0.85, 1)
        _Emission ("Emission", Range(0, 8)) = 3
        _EdgeColor ("Dissolve Edge Color", Color) = (1, 1, 1, 1)
        _EdgePower ("Edge Power", Range(0, 8)) = 2
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.5)) = 0.05

        [Header(Mask)]
        _FresnelExp ("Fresnel Exponent", Range(0.2, 8)) = 4
        [Toggle(_DEPTHFADE_ON)] _DepthFade ("Depth Fade", Float) = 0
        _DepthFadeDistance ("Depth Fade Distance", Float) = 0.25
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _NoiseTiling ("Noise Tiling", Float) = 1
        _NoiseScroll ("Noise Scroll", Float) = 0.25
        _NoisePower ("Noise Power", Range(0, 10)) = 1
        _NoiseAdd ("Noise Add", Range(0, 1)) = 0.25
        _TriplanarFalloff ("Triplanar Falloff", Range(0.1, 8)) = 1

        [Header(Dissolve)]
        _Progress ("Progress (-1 shown .. 1 gone)", Range(-1, 1)) = -1
        _Invert ("Invert Axis", Range(0, 1)) = 0
        _HeightScale ("Object Height (m)", Float) = 1
        _HeightOffset ("Height Pivot Offset (0.5 = centered pivot)", Float) = 0.5

        [Header(Output)]
        [Toggle(_CLIP_ON)] _UseClip ("Use Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "IsEmissive" = "true"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull [_Cull]
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature_local _DEPTHFADE_ON
            #pragma shader_feature_local _CLIP_ON
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float4 projPos : TEXCOORD2;
                float localY : TEXCOORD3;
                UNITY_FOG_COORDS(4)
            };

            sampler2D _NoiseTex; float4 _NoiseTex_ST;
            sampler2D _CameraDepthTexture;

            fixed4 _Color, _EdgeColor;
            float _Emission, _EdgePower, _EdgeSoftness;
            float _FresnelExp, _DepthFadeDistance;
            float _NoiseTiling, _NoiseScroll, _NoisePower, _NoiseAdd, _TriplanarFalloff;
            float _Progress, _Invert, _HeightScale, _HeightOffset;
            float _Cutoff;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.projPos = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.projPos.z);
                o.localY = saturate(v.vertex.y / max(_HeightScale, 0.0001) + _HeightOffset);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            // 世界空间三向投影噪声：按法线权重混合三个平面
            float OWMTriplanarNoise (float3 worldPos, float3 normal)
            {
                float3 blend = pow(abs(normal), _TriplanarFalloff);
                blend /= (blend.x + blend.y + blend.z + 0.0001);
                float3 p = worldPos * _NoiseTiling + _Time.y * _NoiseScroll;
                float n = tex2D(_NoiseTex, p.zy).r * blend.x
                        + tex2D(_NoiseTex, p.xz).r * blend.y
                        + tex2D(_NoiseTex, p.xy).r * blend.z;
                return n;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);

                // 边缘菲涅尔：法线越侧向越亮，让溶解有体积感
                float fresnel = pow(1.0 - saturate(dot(n, viewDir)), _FresnelExp);

                // 可选深度淡出：与场景几何相交处软化，避免穿插硬边
                float depthFade = 1.0;
                #ifdef _DEPTHFADE_ON
                    float2 suv = i.projPos.xy / i.projPos.w;
                    float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, suv));
                    float partZ = i.projPos.z;
                    depthFade = saturate((sceneZ - partZ) / _DepthFadeDistance);
                #endif

                float mask = max(fresnel, depthFade);

                // 噪声驱动的溶解波前
                float noise = OWMTriplanarNoise(i.worldPos, n) * _NoisePower + _NoiseAdd;
                float h = lerp(i.localY, 1.0 - i.localY, step(0.5, _Invert));
                float wave = h + _Progress - noise;
                float body = smoothstep(0.0, _EdgeSoftness, wave);
                float edge = 1.0 - smoothstep(_EdgeSoftness, _EdgeSoftness * 2.0, wave);

                float alpha = saturate(mask * body);
                #ifdef _CLIP_ON
                    clip(alpha - _Cutoff);
                #endif

                float3 rgb = _Color.rgb * _Emission * alpha
                           + _EdgeColor.rgb * _EdgePower * edge * saturate(mask);
                fixed4 col = fixed4(rgb, alpha * _Color.a);

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
