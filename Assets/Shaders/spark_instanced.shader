/* -*- mode:Shader coding:utf-8-with-signature -*-
 */

Shader "Custom/spark_instanced" {
	SubShader {
   		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
		ZWrite Off
		// Blend SrcAlpha OneMinusSrcAlpha // alpha blending
		Blend SrcAlpha One 				// alpha additive
		
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
				float4 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
			};

 			struct v2f
 			{
 				float4 pos:SV_POSITION;
 				fixed4 color:COLOR;
 			};
 			
 			float4x4 _PrevInvMatrix;
			float    _CurrentTime;
			float    _PreviousTime;
   
            v2f vert(appdata_custom v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float4x4 model_matrix = unity_ObjectToWorld;
                uint col32 = asuint(model_matrix._m30);
                float4 col = float4(
                    ((col32>>0)&0xff)/255.0,
                    ((col32>>8)&0xff)/255.0,
                    ((col32>>16)&0xff)/255.0,
                    1);
                float start_time = model_matrix._m31;
                model_matrix._m30 = 0;
                model_matrix._m31 = 0;
                model_matrix._m32 = 0;

				float elapsed = (_CurrentTime - start_time);
				float alpha = clamp(1 - elapsed*2, 0, 1); // life time:0.5sec
				float velocity = 8;

				float size = elapsed * velocity;
				size = elapsed < 0.5 ? size : 0;
            	float4 tv0 = v.vertex;
				tv0.xyz *= size;
                tv0 = mul(UNITY_MATRIX_VP, mul(model_matrix, float4(tv0.xyz, 1)));
				tv0 *= v.texcoord.x;
            	
				float prev_elapsed = (_PreviousTime - start_time);
				float prev_size = prev_elapsed * velocity;
				prev_size = elapsed < 0.5 ? prev_size : 0;
            	float4 tv1 = v.vertex;
				tv1.xyz *= prev_size;
                tv1 = mul(UNITY_MATRIX_VP, mul(model_matrix, float4(tv1.xyz, 1)));

				tv1 *= v.texcoord.y;
            	
            	v2f o;
            	o.pos = tv0 + tv1;
                o.color = col;
				o.color.a *= alpha;
				
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
 * End of spark.shader
 */
