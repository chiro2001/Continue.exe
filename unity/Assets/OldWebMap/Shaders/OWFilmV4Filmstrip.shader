Shader "OldWebMap/FilmV4/Archive Filmstrip"
{
    Properties{_MainTex("Anonymized silent footage atlas",2D)="black"{} _Frame("Actual footage frame",Float)=0 _Grid("Columns rows",Vector)=(8,8,0,0) _Opacity("Envelope",Range(0,1))=1}
    SubShader
    {
        Tags{"Queue"="Transparent+23" "RenderType"="Transparent"}
        Cull Off ZWrite On Blend SrcAlpha OneMinusSrcAlpha
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
            sampler2D _MainTex;float _Frame,_Opacity;float4 _Grid;
            struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v){UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            fixed4 frag(v2f i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float row=floor(_Frame/_Grid.x);float2 uv=(float2(fmod(_Frame,_Grid.x),_Grid.y-1-row)+clamp(i.uv,.003,.997))/_Grid.xy;return fixed4(tex2D(_MainTex,uv).rgb,_Opacity);}
            ENDCG
        }
    }
    Fallback Off
}
