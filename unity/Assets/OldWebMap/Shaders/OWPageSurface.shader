Shader "OldWebMap/Page Surface"
{
    Properties
    {
        _MainTex ("Authored page / layer", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Exposure ("Surface brightness", Range(0,2)) = 1
        _Cutoff ("Cutout threshold", Range(0,1)) = 0.05
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Cull Off ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile __ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #pragma multi_compile __ UNITY_SINGLE_PASS_STEREO
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Tint; float _Exposure, _Cutoff;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex); o.uv=TRANSFORM_TEX(v.uv,_MainTex); return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                fixed4 col=tex2D(_MainTex,i.uv)*_Tint;
                clip(col.a-_Cutoff); col.rgb*=_Exposure; col.a=1; return col;
            }
            ENDCG
        }
    }
    Fallback "Unlit/Transparent Cutout"
}
