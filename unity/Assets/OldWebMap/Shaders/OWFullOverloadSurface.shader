Shader "OldWebMap/FullFilm/Overload Surface"
{
    Properties
    {
        _MainTex ("Authored interface atlas", 2D) = "white" {}
        _Rect ("Atlas rectangle", Vector) = (0,0,1,1)
        _Tint ("Surface tint", Color) = (1,1,1,1)
        _Opacity ("Absolute clock envelope", Range(0,1)) = 1
        _Timeline ("Absolute performance seconds", Float) = 0
        _Activity ("Quiet screen activity", Range(0,1)) = 0
        _Desaturate ("Desaturation", Range(0,1)) = 0
        _Glow ("Radial glow instead of interface", Range(0,1)) = 0
        _Shade ("Authored solid shading", Range(0,1)) = 0
        _ZWrite ("Central physical window depth write", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite [_ZWrite] ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile __ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #pragma multi_compile __ UNITY_SINGLE_PASS_STEREO
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize, _Rect, _Tint;
            float _Opacity, _Timeline, _Activity, _Desaturate, _Glow, _Shade, _ZWrite;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float shade:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o; UNITY_INITIALIZE_OUTPUT(v2f,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;
                float3 normal=UnityObjectToWorldNormal(v.normal);
                o.shade=lerp(1,.63+.37*saturate(dot(normal,normalize(float3(-.4,.7,-.65)))),_Shade);
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 inset=_MainTex_TexelSize.xy*.65;
                float2 uv=_Rect.xy+inset+saturate(i.uv)*max(_Rect.zw-2*inset,0.00001);
                fixed4 c=tex2D(_MainTex,uv);
                float gray=dot(c.rgb,float3(.299,.587,.114));
                c.rgb=lerp(c.rgb,gray.xxx,_Desaturate)*_Tint.rgb*i.shade;
                c.rgb*=.986+.014*cos(i.uv.y*720*6.2831853);
                // A dim travelling status highlight, confined to the bottom status strip.
                float sweep=exp(-pow((i.uv.x-frac(_Timeline*.11))*14,2));
                c.rgb+=float3(.12,.20,.19)*sweep*_Activity*step(i.uv.y,.055);
                float radial=pow(saturate(1-length((i.uv-.5)*2)),2.2);
                c.rgb=lerp(c.rgb,_Tint.rgb,_Glow);
                c.a*=lerp(1,radial,_Glow)*_Opacity*_Tint.a;
                if(_ZWrite>.5) clip(c.a-.08);
                else clip(c.a-.001);
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
