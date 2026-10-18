Shader "OldWebMap/V3/Metal Point Matrix"
{
    Properties { _Pulse ("Real onset specular accent", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Cull Back ZWrite On
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
            float _Pulse;
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 vertex:SV_POSITION;float3 normal:TEXCOORD0;float3 world:TEXCOORD1;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v){UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.color=v.color;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normal),view=normalize(_WorldSpaceCameraPos-i.world);
                float3 key=normalize(float3(-.4,.75,-.55)),fill=normalize(float3(.7,.3,.6));
                float diffuse=.16+.65*saturate(dot(n,key))+.18*saturate(dot(n,fill));
                float spec=pow(saturate(dot(n,normalize(key+view))),78)*(1.5+_Pulse);
                float rim=pow(1-saturate(dot(n,view)),3)*.3;
                return fixed4(i.color.rgb*diffuse+float3(.85,.95,1)*spec+float3(.08,.28,.4)*rim,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
