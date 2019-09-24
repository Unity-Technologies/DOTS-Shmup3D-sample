/* -*- mode:Shader coding:utf-8-with-signature -*-
 */

Shader "Custom/debris_instanced" {
	Properties {
	}
	SubShader {
   		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
		ZWrite Off
		Blend SrcAlpha One 				// alpha additive
		ColorMask RGB
		
        Pass {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase                       // This line tells Unity to compile this pass for forward base.
            #pragma multi_compile_instancing
 			#pragma target 3.0
 			
 			#include "UnityCG.cginc"

 			struct appdata_custom {
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
			};

 			struct v2f {
 				float4 pos:SV_POSITION;
 				fixed4 color:COLOR;
 			};
 			
 			float4x4 _PrevInvMatrix;
			float3   _TargetPosition;
			float    _Range;
			float    _RangeR;
			float3   _BaseColor;
   
            v2f vert(appdata_custom v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float4 v0 = v.vertex;
                v0.y -= _Time.w * 20;
				float3 target = _TargetPosition;
				float3 diff = target - v0.xyz;
				float3 trip = floor( (diff*_RangeR + 1) * 0.5 );
				trip *= (_Range * 2);
				v0.xyz += trip;
				// v.vertex.z = 0;

            	float4 tv0 = v0;
            	tv0 = UnityObjectToClipPos(tv0);
				tv0 *= v.texcoord.x;
            	
            	float4 tv1 = float4(v0.x, v0.y+1, v0.z, 1);
				tv1 = float4(UnityObjectToViewPos(tv1), 1);
            	tv1 = mul(_PrevInvMatrix, tv1);
            	tv1 = mul(UNITY_MATRIX_P, tv1);
				tv1 *= v.texcoord.y;
            	
            	v2f o;
            	o.pos = tv0 + tv1;
				fixed alpha = v.color.a;
				half3 col = _BaseColor;
				o.color = fixed4(col, alpha);
            	return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
				return i.color;
            }

            ENDCG
        }
    }
}

/*
 * End of debris.shader
 */
