Shader "OldWebMap/V3/Layered Window"
{
    Properties
    {
        _MainTex ("Actual source image", 2D) = "white" {}
        _Reveal ("Window opening", Range(0,1)) = 1
        _Scroll ("Reading scroll", Range(0,1)) = 0
        _Pulse ("Real onset accent", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
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
            sampler2D _MainTex;float _Reveal,_Scroll,_Pulse;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v){UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv=i.uv;
                uv.y=lerp(uv.y,uv.y*.76+_Scroll*.24,step(.0001,_Scroll));
                fixed4 color=tex2D(_MainTex,uv);
                float scan=.99+.01*cos(i.uv.y*720*6.2831853);
                color.rgb*=scan*(1+_Pulse*.12);
                float edge=min(min(i.uv.x,1-i.uv.x),min(i.uv.y,1-i.uv.y));
                color.rgb+=float3(.15,.4,.55)*_Pulse*(1-smoothstep(0,.018,edge));
                color.a*=smoothstep(0,.12,_Reveal);
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
