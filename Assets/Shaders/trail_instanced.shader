/* -*- mode:Shader coding:utf-8-with-signature -*-
 */

Shader "Custom/trail_instanced" {
    Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
        _FadeRatio("R", Range(0, 1000)) = 800
    }
	SubShader {
        Tags{ "Queue"="Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "PerformanceChecks" = "False" "RenderPipeline" = "LightweightPipeline"}

		ZWrite Off
		Cull Off
		Blend SrcAlpha OneMinusSrcAlpha // alpha blending
		//Blend SrcAlpha One 				// alpha additive
		
        Pass {
            Name "ForwardLit"
            Tags {"LightMode" = "LightweightForward"}

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase                       // This line tells Unity to compile this pass for forward base.
            #pragma multi_compile_instancing
 			#pragma target 4.5
            #define SMOOTH_TRAIL
 
 			#include "UnityCG.cginc"

			sampler2D _MainTex;
            float _AliveTimeR;
            float _CurrentTime;
            float _FadeRatio;
			int _NodeNum;
            #if SHADER_TARGET >= 45
			struct TrailPoint
			{
				float3 position_;
                float time_;
			};
			StructuredBuffer<TrailPoint> _TrailBuffer;
			#endif

 			struct appdata_custom {
                uint vertexID : SV_VertexID;
				float4 vertex : POSITION;
				float4 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
			};

 			struct v2f
 			{
 				float4 pos:SV_POSITION;
 				fixed4 color:COLOR;
				float2 uv:TEXCOORD0;
 			};
   
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
                int hidx = asint(model_matrix._m31);
				float width = model_matrix._m32;
				int bulk = asint(model_matrix._m33);
				int nidx = v.vertexID/2;
				int tidx = hidx - nidx;
				tidx = tidx < 0 ? tidx+_NodeNum : tidx;
                float normalized_elapsed = (float)nidx/(float)_NodeNum;

// #if defined(UNITY_INSTANCING_ENABLED)
// 				int instanceID = v.instanceID;
// #else
// 				int instanceID = 0;
// #endif
                int instanceID = asint(model_matrix._m03);

                model_matrix._m03 = 0;
                model_matrix._m30 = 0;
                model_matrix._m31 = 0;
                model_matrix._m32 = 0;
                model_matrix._m33 = 1;
                
				int offset_idx = (instanceID + (1023*bulk)) * _NodeNum;
				int prev_tidx = tidx-1;
				prev_tidx = prev_tidx < 0 ? prev_tidx + _NodeNum : prev_tidx;
				int next_tidx = tidx+1;
				next_tidx = next_tidx >= _NodeNum ? next_tidx - _NodeNum : next_tidx;

				float3 node = _TrailBuffer[tidx + offset_idx].position_;
                float spawn_time = _TrailBuffer[tidx + offset_idx].time_;
                float elapsed = _CurrentTime - spawn_time;
				float3 diff = (nidx != _NodeNum-1 ?
							   node - _TrailBuffer[prev_tidx + offset_idx].position_:
							   _TrailBuffer[next_tidx + offset_idx].position_ - node);
				float3 normal = normalize(diff);

				float3 tv = node;
				float3 eye = WorldSpaceViewDir(float4(tv, 1));
				float3 side = normalize(cross(eye, normal));
                float size = width * (1+3*elapsed);
				tv += side * (v.texcoord.x-0.5) * size;
				
#if defined(SMOOTH_TRAIL)
                float3 v0 = node;
                float3 v1 = nidx <= 0 ? node : _TrailBuffer[prev_tidx + offset_idx].position_;
                float3 v2 = nidx >= _NodeNum-1 ? node : _TrailBuffer[next_tidx + offset_idx].position_;
                float4 p0 = UnityWorldToClipPos(v1);
                float4 p1 = UnityWorldToClipPos(v0);
                float4 p2 = UnityWorldToClipPos(v2);
                float wpx = p1.w*p0.x - p0.w*p1.x;
                float wpy = p1.w*p0.y - p0.w*p1.y;
                float wnx = p2.w*p1.x - p1.w*p2.x;
                float wny = p2.w*p1.y - p1.w*p2.y;
                float ww = size/p1.w*_FadeRatio; // the larger, the easier decays.
                float topology_fade = smoothstep(0, ww, wpx*wnx+wpy*wny); // ww=1を仮定できないので除算が呼ばれる
#else
                float topology_fade = 1;
#endif

            	v2f o;
            	o.pos = mul(UNITY_MATRIX_VP, float4(tv.xyz, 1));
                o.color = col;
				o.color.a *= 1 - (elapsed*_AliveTimeR);
                o.color.a *= topology_fade;
		        o.uv = MultiplyUV(UNITY_MATRIX_TEXTURE0, float2(v.texcoord.x, spawn_time*1));
				
            	return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // fixed3 rgb = tex2D(_MainTex, i.uv).rgb;
                // return fixed4(rgb * i.color.rgb, i.color.a);
                fixed3 rgb = tex2D(_MainTex, i.uv).rgb;
                return fixed4(i.color.rgb, rgb.r * i.color.a);
            }

            ENDCG
        }
    }
}

/*
 * End of spark.shader
 */
