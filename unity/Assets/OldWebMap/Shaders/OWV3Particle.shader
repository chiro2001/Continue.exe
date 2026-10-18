Shader "OldWebMap/V3/Signal Particles"
{
    Properties { [HDR] _Tint ("Tint", Color) = (1,1,1,1) _Shape ("0 phosphor, 1 data square", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent+30" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One Cull Off ZWrite Off ZTest LEqual
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
            float4 _Tint; float _Shape;
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v); v2f o; UNITY_INITIALIZE_OUTPUT(v2f,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color*_Tint; o.uv=v.uv*2-1; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float radial = dot(i.uv,i.uv);
                float glow=exp(-radial*5)*saturate(1-radial);
                float square=(1-smoothstep(.7,1,max(abs(i.uv.x),abs(i.uv.y))));
                float hollow=saturate(square-(1-smoothstep(.42,.64,max(abs(i.uv.x),abs(i.uv.y)))));
                float alpha=lerp(glow,hollow*.8+glow*.24,_Shape)*i.color.a;
                return fixed4(i.color.rgb,alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
