#ifndef OSFOG_INCLUDE
#define OSFOG_INCLUDE

#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
#pragma multi_compile _ _SHADOWS_SOFT
#pragma multi_compile _ _BUTO_SELF_ATTENUATION_ENABLED
#pragma multi_compile _ _BUTO_HORIZON_SHADOWING_ENABLED
#pragma multi_compile _ _BUTO_ANALYTIC_FOG_ENABLED

half3 rayDirection;
half3 mainLightDirection;
half3 mainLightColor;


#define MAX_LIGHT_COUNT 8
int _LightCountButo = 0;
half3 _LightPosButo[MAX_LIGHT_COUNT];
half _LightIntensityButo[MAX_LIGHT_COUNT];
half3 _LightColorButo[MAX_LIGHT_COUNT];
half3 _LightDirectionButo[MAX_LIGHT_COUNT];
half2 _LightAngleButo[MAX_LIGHT_COUNT];

#define MAX_VOLUME_COUNT 8
int _VolumeCountButo = 0;
int _VolumeShape[MAX_VOLUME_COUNT]; // 0 => Sphere, 1 => Box
half3 _VolumePosition[MAX_VOLUME_COUNT]; // xyz world pos
half3 _VolumeSize[MAX_VOLUME_COUNT]; // Sphere => x = radius, Box => xyz = size, 
half _VolumeIntensityButo[MAX_VOLUME_COUNT]; // Intensity scalar
int _VolumeBlendMode[MAX_VOLUME_COUNT]; // BlendMode per Volume
half _VolumeBlendDistance[MAX_VOLUME_COUNT]; // Blend Distance per Volume

int _MaximumSelfShadowingOctaves = 1;
half3 _WorldColor = half3(0, 0, 0);

// SimpleColor[0] -> Lit, SimpleColor[1] -> Shadowed, SimpleColor[2] -> Emit
half3 _SimpleColor[3];

half3 _DirectionalLightingForward;
half3 _DirectionalLightingBack;
half _DirectionalLightingRatio;

// Returns % between start and stop
half InverseLerp(half start, half stop, half value)
{
	return (value - start) / (stop - start);
}

half Remap(half inStart, half inStop, half outStart, half outStop, half v)
{
	half t = InverseLerp(inStart, inStop, v); 
	return lerp(outStart, outStop, saturate(t));
}

half CalculateHorizonFalloff(half3 RayPosition, half3 LightDirection)
{
	half h = max(RayPosition.y, 0);
	half r = 6371000.0; // Earth Radius (m)
	half a = r + h;
	half b = r / a;
	half c = acos(b);
	LightDirection = normalize(LightDirection);
	half angle = LightDirection.y * 1.57079632679;
	half d = angle - c;
	
	return smoothstep(0.0, radians(5.0), d);
}

half GetLightFalloff(half3 RayPosition, half3 LightPosition, half2 LightAngle, half3 LightForward)
{
	half3 dirToRay = normalize(LightPosition - RayPosition);
	half dotDir = dot(dirToRay, LightForward);
	half spotAttenuation = saturate((dotDir * LightAngle.x) + LightAngle.y);
	spotAttenuation *= spotAttenuation;
	
	half d = distance(RayPosition, LightPosition);
	half distanceAttenuation = 1.0 / ((1.0 + (d * d)));
	return distanceAttenuation * spotAttenuation;
}

half3 GetDirectionalLightOverrides(half3 ForwardColor, half3 BackColor, half Ratio)
{
	half3 color = half3(0, 0, 0);
	
	
	half cosAngle = dot(normalize(mainLightDirection), normalize(rayDirection)); // [-1, 1]
	cosAngle = saturate((cosAngle + 1.0) * 0.5); // [0, 1]
	cosAngle = pow(cosAngle, Ratio);
	color = lerp(BackColor, ForwardColor, cosAngle);
	
	
	half lightApexFade = abs(dot(normalize(mainLightDirection), float3(0, 1, 0))); // [0,1]
	color = lerp(color, half3(1.0, 1.0, 1.0), lightApexFade);
	
	
	return color;
}

half3 GetAdditionalLightData(half3 RayPosition)
{
	half3 finalColor = half3(0,0,0);
	
	for (int lightIndex = 0; lightIndex < _LightCountButo; lightIndex++)
	{
		finalColor += _LightColorButo[lightIndex] * GetLightFalloff(RayPosition, _LightPosButo[lightIndex], _LightAngleButo[lightIndex], _LightDirectionButo[lightIndex]) * _LightIntensityButo[lightIndex];
	}
	return finalColor;
}


SAMPLER_CMP(sampler_ButoLinearClampCompare); // Unity renamed sampler_MainLightShadowmapTexture to sampler_LinearClampCompare in 2022.2.11. Now, we declare our own sampler.
// See https://github.com/Unity-Technologies/Graphics/blob/d84f56b68aea37785813fddfcbcf63384e9640ff/Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl

half GetRealtimeShadows(float4 shadowCoord)
{
	ShadowSamplingData shadowSamplingData = GetMainLightShadowSamplingData();
    half4 shadowParams = GetMainLightShadowParams();
    return SampleShadowmap(TEXTURE2D_ARGS(_MainLightShadowmapTexture, sampler_ButoLinearClampCompare), shadowCoord, shadowSamplingData, shadowParams, false);
}

half GetShadowAttenuation(half3 RayPos)
{
	#ifndef SHADERGRAPH_PREVIEW
	half4 shadowCoord = TransformWorldToShadowCoord(RayPos);
	return saturate(lerp(GetRealtimeShadows(shadowCoord), 1.0, GetMainLightShadowFade(RayPos)));
	#endif
}

float rand2dTo1d(float2 vec, float2 dotDir = float2(12.9898, 78.233))
{
	float random = dot(sin(vec.xy), dotDir);
	random = frac(sin(random) * 143758.5453);
	return random;
}

float2 rand2dTo2d(float2 vec, float2 seed = 4605)
{
	return float2(
		rand2dTo1d(vec + seed, float2(12.989, 78.233)),
		rand2dTo1d(vec + seed, float2(39.346, 11.135))
	);
}

half GetRandomHalf(half2 Seed)
{
	return saturate(frac(sin(dot(Seed + half2(642.86, 808.10), half2(12.9898, 78.233))) * 43758.5453));
}

half HenyeyGreenstein(half eccentricity)
{
	half cosAngle = dot(normalize(mainLightDirection), normalize(rayDirection));
	half e2 = eccentricity * eccentricity;
	return ((1.0 - e2) / pow(abs(1.0 + e2 - 2.0 * eccentricity * cosAngle), 1.5)) / 4.0 * 3.1416;
}

half GetFogDensityByHeight(half AttenuationBoundarySize, half BaseHeight, half3 RayPosition)
{
	half height = max(RayPosition.y - BaseHeight, 0.0);
	return exp(-height * 1.0 / max(AttenuationBoundarySize, 0.001));
}

half GetFogDensityByNoise(Texture3D NoiseTexture, SamplerState Sampler, half3 RayPosition, half InvNoiseScale, half3 WindVelocity, half NoiseMin, half NoiseMax, int Octaves, int mip)
{
	half3 uvw = RayPosition * InvNoiseScale;
	half3 wind = WindVelocity * InvNoiseScale * _Time.y;
	
	half4 value = 0;
	half c = 0;
	half amp = 1.0;
	
	for (int i = 1; i <= Octaves; i++)
	{
		value += amp * SAMPLE_TEXTURE3D_LOD(NoiseTexture, Sampler, uvw + wind, mip);
		c += amp;
		uvw *= _Lacunarity;
		amp *= _Gain;
	}
	value /= c;
	
	half v = value.r * 0.53 + value.g * 0.27 + value.b * 0.13 + value.a * 0.07;
	v = Remap(NoiseMin, NoiseMax, 0.0, 1.0, v);

	return v;
}

struct Box
{
	float3 position;
	float3 size;
};

struct Sphere
{
	float3 position;
	float radius;
};


half SdBox(float3 rayPosition, Box box)
{
	half3 v = abs(rayPosition - box.position) - box.size * 0.5;
	return length(max(v, 0)) + min(max(v.x, max(v.y,v.z)), 0);
}

half SdSphere(float3 rayPosition, Sphere sphere)
{
	return max(length(rayPosition - sphere.position) - sphere.radius, 0);
}


half GetFogFalloff(float3 RayPosition)
{

	// BlendMode 0 -> Multiplicative
	// BlendMode 1 -> Exclusive
	
	// Shape 0 -> Sphere
	// Shape 1 -> Box
	
	half a = 1.0;
	half x = 0.0;
	int xo = 0;
	
	
	for (int i = 0; i < _VolumeCountButo; i++)
	{
		float d = 0;
		
		if(_VolumeShape[i] == 0)
		{
			Sphere sphere;
			sphere.position = _VolumePosition[i].xyz;
			sphere.radius = _VolumeSize[i].x;
			d = SdSphere(RayPosition, sphere);
		}
		
		if(_VolumeShape[i] == 1)
		{
			Box box;
			box.position = _VolumePosition[i].xyz;
			box.size = _VolumeSize[i].xyz;
			d = SdBox(RayPosition, box);
		}
		
		
		half2 remapVals = half2(_VolumeIntensityButo[i], 1);
		
		if (_VolumeBlendMode[i] == 1)
		{
			xo = 1;
			remapVals = half2(1, 0);
		}
		
		half v = Remap(0, _VolumeBlendDistance[i], remapVals.x, remapVals.y, d);
		
		
		if (_VolumeBlendMode[i] == 0)
		{
			a *= v;
		}
		
		else if(_VolumeBlendMode[i] == 1)
		{
			x += v;
		}
	}
	
	if(xo == 1)
		a *= saturate(x);
	
	return a;
}

half CalculateExponentialHeightFog(half extinction, half fogExp, half rayOriginY, half rayLength)
{
	return saturate((extinction / fogExp) * exp(-rayOriginY * fogExp) * (1.0 - exp(-rayLength * rayDirection.y * fogExp)) / rayDirection.y);
}


half2 GetTexCoordSize(float scale)
{
	return 1.0 / (_ScreenParams.xy * scale);
}



void SampleVolumetricFog_half(half2 UV, half3 RayOrigin, 
half MaxDistanceNonVolumetric, half MaxDistanceVolumetric, half Anisotropy,
half BaseHeight, half AttenuationBoundarySize, half FogDensity, half LightIntensity,
half ShadowIntensity, Texture2D ColorRamp, half ColorInfluence, Texture3D NoiseTexture,
SamplerState NoiseSampler, half3 NoiseWindVelocity, half NoiseScale, half NoiseMin,
half NoiseMax, bool animateSamplePosition, int SampleCount, SamplerState PointSampler, 
int Octaves, half UnjitteredDepth,
out half3 Color, out half Alpha)
{
	// Out Value Setup
	Color = half3(0.0, 0.0, 0.0);
	Alpha = 1.0;
	
	#ifndef SHADERGRAPH_PREVIEW
	
	// Initializing and Checking Params
	AttenuationBoundarySize = max(AttenuationBoundarySize, 0.001);
	LightIntensity = max(LightIntensity, 0.0);
	ShadowIntensity = max(ShadowIntensity, 0.0);
	FogDensity = max(FogDensity, 0.001);
	
	// Light Setup
	Light mainLight = GetMainLight();
	mainLightColor = mainLight.color;
	mainLightDirection = normalize(mainLight.direction);
	
	half2 jitteredUV = UV;
	// Ray Setup
	if (animateSamplePosition)
	{
		half2 texCoord = GetTexCoordSize(0.5);
		half2 jitter = texCoord * (rand2dTo2d(UV + _Time.x) - 0.5);
		jitteredUV += jitter;
	}
	
	half depth01 = SampleSceneDepth(UV);
	half depth = LinearEyeDepth(depth01, _ZBufferParams);
	
	half3 viewVector = mul(unity_CameraInvProjection, half4(jitteredUV * 2 - 1, 0.0, -1)).xyz;
	viewVector = mul(unity_CameraToWorld, half4(viewVector, 0.0)).xyz;
	half viewLength = length(viewVector);
	half realDepth = depth * viewLength;
	rayDirection = viewVector / viewLength;
	
	half targetRayDistance = MaxDistanceVolumetric;
	
	// Prevents early culling if the far clip plane is quite short
	if(depth01 < 1.0)
		targetRayDistance = min(targetRayDistance, realDepth);
	
	// Lighting
	half extinction = FogDensity * FogDensity * 0.001;
	half hg = HenyeyGreenstein(Anisotropy);
	
	static half points[3] =
	{
		0.165,
		0.495,
		0.825
	};
	
	
	// Ray March
	half2 seed = UV;
	if(animateSamplePosition)
		seed += _Time.xx;
	
	half random = rand2dTo1d(seed);
	
	half invStepCount = 1.0 / half(SampleCount);
	half invNoiseScale = 1.0 / NoiseScale;
	half invMaxRayDistance = 1.0 / MaxDistanceVolumetric;
	
	half3 rayPosition = RayOrigin;
	half rayDepth_previous, rayDepth_current;
	rayDepth_previous = 0;
	rayDepth_current = 0;
	half3 directionalColor = GetDirectionalLightOverrides(_DirectionalLightingForward, _DirectionalLightingBack, _DirectionalLightingRatio);
	
	#define SELF_SHADOWING_STEP_COUNT 4
	float selfShadowSteps[SELF_SHADOWING_STEP_COUNT] = 
	{
		0.125 * AttenuationBoundarySize,
		0.25 * AttenuationBoundarySize,
		0.5 * AttenuationBoundarySize,
		1.0 * AttenuationBoundarySize
	};
	half stepSize = targetRayDistance * invStepCount;
	rayDepth_current += random * stepSize;
	
	for (int i = 1; i <= SampleCount; i++)
	{
		// Positioning
		half ratio = half(i) * invStepCount;
		
		bool lastRun = false;
		if(rayDepth_current > targetRayDistance)
		{
			rayDepth_current = targetRayDistance;
			lastRun = true;
		}
		
		rayPosition = RayOrigin + rayDirection * rayDepth_current;
		half stepLength = rayDepth_current - rayDepth_previous;
		rayDepth_previous = rayDepth_current;
		
		// Transmittance
		half sampleDensity = GetFogDensityByHeight(AttenuationBoundarySize, BaseHeight, rayPosition) * GetFogDensityByNoise(NoiseTexture, NoiseSampler, rayPosition, invNoiseScale, NoiseWindVelocity, NoiseMin, NoiseMax, Octaves, 0) * GetFogFalloff(rayPosition);
		half e = 1.0;
		#if !_BUTO_ANALYTIC_FOG_ENABLED
		e = (1.0 - ratio) * (1.0 - ratio);
		#endif
		
		half sampleExtinction = extinction * sampleDensity * e;
		
		if (sampleExtinction > 0.0001)
		{
			// Evaluate Color
			half3 colorSamples[3];
			half3 shadedColor = _WorldColor;
			half3 litColor = _WorldColor + mainLightColor;
			half3 emitColor = 0;
			
			if (ColorInfluence > 0.001)
			{
				for (int i = 0; i <= 2; i++)
				{
					colorSamples[i] = SAMPLE_TEXTURE2D_LOD(ColorRamp, PointSampler, half2(saturate(rayDepth_current * invMaxRayDistance), points[i]), 0).rgb;
				}
			
				shadedColor = lerp(shadedColor, colorSamples[0] * _SimpleColor[1], ColorInfluence);
				litColor = lerp(litColor, colorSamples[1] * _SimpleColor[0], ColorInfluence);
				emitColor = colorSamples[2] * _SimpleColor[2] * ColorInfluence;
			}
			litColor *= LightIntensity;
			
			half shadowAttenuation = GetShadowAttenuation(rayPosition);
			half finalShading = shadowAttenuation;
			
			#if _BUTO_SELF_ATTENUATION_ENABLED
				half shadowSelfAttenuation = 1.0;
				
				for (int f = 1; f <= SELF_SHADOWING_STEP_COUNT; f++)
				{
					half selfAttenStepLength = selfShadowSteps[f - 1];
					half3 currRayPos = rayPosition + mainLightDirection * selfAttenStepLength;
					half density = GetFogDensityByHeight(AttenuationBoundarySize, BaseHeight, currRayPos) * GetFogDensityByNoise(NoiseTexture, NoiseSampler, currRayPos, invNoiseScale, NoiseWindVelocity, NoiseMin, NoiseMax, _MaximumSelfShadowingOctaves, 1) * GetFogFalloff(currRayPos);
					shadowSelfAttenuation *= exp(-density * extinction * selfAttenStepLength);
				}
				finalShading *= shadowSelfAttenuation;
			#endif
			
			#if _BUTO_HORIZON_SHADOWING_ENABLED
				finalShading *= CalculateHorizonFalloff(rayPosition, mainLightDirection);
			#endif
			
			
			half3 fogColor = lerp(shadedColor, litColor * hg, finalShading) * directionalColor + emitColor;
			
			// Solve Transmittance
			half shadowIntensity = lerp(ShadowIntensity, 1.0, shadowAttenuation);
			half transmittance = exp(-sampleExtinction * stepLength * shadowIntensity);
			half3 additionalLightColor = GetAdditionalLightData(rayPosition);
			
			// Solve Color
			half3 inScatteringData = sampleExtinction * (fogColor + additionalLightColor);
			half3 integratedScattering = (inScatteringData - (inScatteringData * transmittance)) / sampleExtinction;	
			Color += Alpha * integratedScattering;
			
			// Apply Transmittance
			Alpha *= transmittance;
			
		}
		
		if(lastRun)
			break;
			
		rayDepth_current += stepSize;
	}
	
	#if _BUTO_ANALYTIC_FOG_ENABLED
		if (MaxDistanceVolumetric < realDepth)
		{
			half s = smoothstep(-MaxDistanceVolumetric * 0.2, 0, realDepth - MaxDistanceVolumetric);
			half sampleDist = MaxDistanceNonVolumetric;
			if (depth01 < 1.0)
			{
				sampleDist = min(sampleDist, realDepth);
			}
		
			half rayLength = sampleDist - rayDepth_current;
			half rayHeight = max(rayPosition.y - BaseHeight, 0.0);
	
			//half rayLength = sampleDist;
			//half rayHeight = max(RayOrigin.y - BaseHeight, 0.0);
	
			// Multiplied by 0.5 to account for the noise texture (average value = 0.5) and by the NoiseMin range^2 to account for the remaining % coverage
			//half e = extinction * 0.25 * ((1.0 - NoiseMin) * (1.0 - NoiseMin)) * lerp(1.0, 1.0-Alpha,0.8);
	
	
			half e = extinction * 0.5;
			half fogAmount = 1.0 - CalculateExponentialHeightFog(e, 1.0 / max(AttenuationBoundarySize, 0.001), rayHeight, rayLength) * s;
		
			// Apply Transmittance
			half alpha_previous = Alpha;
			Alpha *= fogAmount;
	
			// Evaluate Color
			half3 colorSamples[3];
			for (int f = 0; f <= 2; f++)
			{
				colorSamples[f] = SAMPLE_TEXTURE2D_LOD(ColorRamp, PointSampler, half2(1.0, points[f]), 0).rgb;
			}
		
			half shadowAttenuation = 1.0;
	
			#if _BUTO_HORIZON_SHADOWING_ENABLED
				shadowAttenuation *= CalculateHorizonFalloff(rayPosition, mainLightDirection);
			#endif
		
			half3 shadedColor = lerp(_WorldColor, colorSamples[0] * _SimpleColor[1], ColorInfluence);
			half3 litColor = lerp(_WorldColor + mainLightColor, colorSamples[1] * _SimpleColor[0], ColorInfluence) * LightIntensity;
			half3 emitColor = colorSamples[2] * _SimpleColor[2] * ColorInfluence;
		
			half3 fogColor = lerp(shadedColor, litColor * hg, shadowAttenuation) * directionalColor + emitColor;
		
			Color += fogColor * (alpha_previous - Alpha) * s;
		}	
#endif
	#endif
}

#endif