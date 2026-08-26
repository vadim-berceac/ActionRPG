Shader "Fantasy Forest/StandardNoCulling"
{
	Properties
	{
		_Cutoff( "Mask Clip Value", Range(0,1) ) = 0.5
		_MainTex("Main Texture", 2D) = "white" {}
		_Color("Color", Color) = (1,1,1,0)
	}

	SubShader
	{
		Tags{ "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
		Cull Off

		Pass
		{
			Name "ForwardLit"
			Tags{ "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile _ _SHADOWS_SOFT
			#pragma multi_compile_fog

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS   : NORMAL;
				float2 uv         : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
				float2 uv          : TEXCOORD0;
				float3 normalWS    : TEXCOORD1;
				float3 positionWS  : TEXCOORD2;
				float  fogCoord    : TEXCOORD3;
			};

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _Color;
				float  _Cutoff;
			CBUFFER_END

			Varyings vert(Attributes IN)
			{
				Varyings OUT;
				VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
				VertexNormalInputs normalInput = GetVertexNormalInputs(IN.normalOS);

				OUT.positionHCS = vertexInput.positionCS;
				OUT.positionWS  = vertexInput.positionWS;
				OUT.normalWS    = normalInput.normalWS;
				OUT.uv          = IN.uv * _MainTex_ST.xy + _MainTex_ST.zw;
				OUT.fogCoord    = ComputeFogFactor(vertexInput.positionCS.z);
				return OUT;
			}

			half4 frag(Varyings IN) : SV_Target
			{
				half4 tex2DNode3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

				clip( tex2DNode3.a - _Cutoff );

				half3 albedo = ( _Color * tex2DNode3 ).rgb;

				half3 specColor = half3(0,0,0);
				half smoothness = 0;

				float3 normalWS = normalize(IN.normalWS);
				float3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);

				InputData inputData = (InputData)0;
				inputData.positionWS = IN.positionWS;
				inputData.normalWS = normalWS;
				inputData.viewDirectionWS = viewDirWS;
				inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
				inputData.fogCoord = IN.fogCoord;
				inputData.vertexLighting = half3(0,0,0);
				inputData.bakedGI = SampleSH(normalWS);

				SurfaceData surfaceData = (SurfaceData)0;
				surfaceData.albedo = albedo;
				surfaceData.specular = specColor;
				surfaceData.metallic = 0;
				surfaceData.smoothness = smoothness;
				surfaceData.normalTS = half3(0,0,1);
				surfaceData.emission = 0;
				surfaceData.occlusion = 1;
				surfaceData.alpha = 1;

				half4 color = UniversalFragmentPBR(inputData, surfaceData);
				color.rgb = MixFog(color.rgb, IN.fogCoord);
				color.a = 1;
				return color;
			}
			ENDHLSL
		}

		Pass
		{
			Name "ShadowCaster"
			Tags{ "LightMode" = "ShadowCaster" }
			Cull Off

			HLSLPROGRAM
			#pragma vertex ShadowVert
			#pragma fragment ShadowFrag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS   : NORMAL;
				float2 uv         : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _Color;
				float  _Cutoff;
			CBUFFER_END

			float3 _LightDirection;

			Varyings ShadowVert(Attributes IN)
			{
				Varyings OUT;
				float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
				float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

				float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
				#if UNITY_REVERSED_Z
					positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
				#else
					positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
				#endif

				OUT.positionHCS = positionCS;
				OUT.uv = IN.uv * _MainTex_ST.xy + _MainTex_ST.zw;
				return OUT;
			}

			half4 ShadowFrag(Varyings IN) : SV_Target
			{
				half4 tex2DNode3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
				clip( tex2DNode3.a - _Cutoff );
				return 0;
			}
			ENDHLSL
		}
	}
	Fallback "Universal Render Pipeline/Lit"
}