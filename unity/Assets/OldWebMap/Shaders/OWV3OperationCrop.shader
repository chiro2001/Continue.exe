Shader "OldWebMap/V3/Operation Crop"
{
    Properties
    {
        _MainTex ("Original source image",2D) = "white" {}
        _Region ("Source UV rectangle xy/wh",Vector) = (0,0,1,1)
        _Tint ("Tint",Color) = (1,1,1,1)
        _Opacity ("Opacity",Range(0,1)) = 1
        _Highlight ("Operation accent",Range(0,1)) = 0
        _Shape ("0 rectangle, 1 click ring, 2 disc",Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source blend",Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend",Float) = 0
        [Toggle] _ZWrite ("Write depth",Float) = 1
        _Cutoff ("Discard invisible pixels",Range(0,.05)) = .001
    }
    SubShader
    {
        Tags {"Queue"="Geometry+20" "RenderType"="Opaque" "IgnoreProjector"="True"}
        Cull Off ZTest LEqual ZWrite [_ZWrite] Blend [_SrcBlend] [_DstBlend]
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
            sampler2D _MainTex;float4 _Region,_Tint;float _Opacity,_Highlight,_Shape,_Cutoff;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v){UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                fixed4 color=tex2D(_MainTex,_Region.xy+i.uv*_Region.zw)*_Tint;
                float edge=min(min(i.uv.x,1-i.uv.x),min(i.uv.y,1-i.uv.y));
                color.rgb+=float3(.12,.38,.62)*_Highlight*(1-smoothstep(0,.025,edge));
                float radius=length(i.uv-.5)*2;
                float ring=smoothstep(.64,.76,radius)*(1-smoothstep(.93,1,radius));
                float disc=1-smoothstep(.86,1,radius);
                float shape=_Shape<.5?1:_Shape<1.5?ring:disc;
                color.a*=_Opacity*shape;
                // Alpha-zero geometry must never leave invisible depth behind.
                clip(color.a-max(_Cutoff,.00001));
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
