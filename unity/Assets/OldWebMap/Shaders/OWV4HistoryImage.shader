Shader "OldWebMap/V4/History Image"
{
    Properties { _MainTex ("Original image",2D)="white" {} _Exposure ("Exposure",Range(.3,1.3))=1 _Tint ("Tint",Color)=(1,1,1,1) }
    SubShader
    {
        Tags {"Queue"="Geometry+25" "RenderType"="Opaque" "IgnoreProjector"="True"}
        Cull Off ZWrite On ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex;float _Exposure;float4 _Tint;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v){UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            fixed4 frag(v2f i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);fixed4 c=tex2D(_MainTex,i.uv);clip(c.a-.002);return fixed4(c.rgb*_Tint.rgb*_Exposure,1);}
            ENDCG
        }
    }
    Fallback Off
}
