#ifndef BUTO_TAA_INCLUDED
#define BUTO_TAA_INCLUDED

#define _DEBUG_MOTION_VECTORS 0

bool IsValidUV(half2 UV)
{
	if (any(UV < 0) || any(UV > 1))
	{
		return false;
	}
	
	return true;
}


half random(half2 seed, half2 dotDir = half2(12.9898, 78.233))
{
	return frac(sin(dot(sin(seed), dotDir)) * 43758.5453);
}

half2 GetTexCoordSize(float scale)
{
	return 1.0 / (_ScreenParams.xy * scale);
}


SamplerState point_clamp_sampler;
SamplerState linear_clamp_sampler;
void TAA_float(Texture2D HistoricData, Texture2D NewFrameData, float2 UV, float BlendFactor, float2 MotionVector, half Depth, out half4 MergedData, out half3 MergedDataRGB, out half MergedDataA)
{
	MergedData = half4(0.0, 0.0, 0.0, 0.0);
	MergedDataRGB = half3(0.0, 0.0, 0.0);
	MergedDataA = 0.0;
	
#ifndef SHADERGRAPH_PREVIEW
	float2 texCoord = GetTexCoordSize(0.5);
	
	float4 newFrame = NewFrameData.SampleLevel(point_clamp_sampler, UV, 0);
	float2 histUV = UV - MotionVector;
	
	bool isValidHistUV = IsValidUV(histUV);
	if (isValidHistUV)
	{
		half4 histSample = HistoricData.SampleLevel(linear_clamp_sampler, histUV, 0);
		
		half4 newSampleUp = NewFrameData.SampleLevel(point_clamp_sampler, UV + half2(0.0, -texCoord.y), 0);
		half4 newSampleDown = NewFrameData.SampleLevel(point_clamp_sampler, UV + half2(0.0, texCoord.y), 0);
		half4 newSampleRight = NewFrameData.SampleLevel(point_clamp_sampler, UV + half2(-texCoord.x, 0.0), 0);
		half4 newSampleLeft = NewFrameData.SampleLevel(point_clamp_sampler, UV + half2(texCoord.x, 0.0), 0);
	
		half4 newSampleUpRight = NewFrameData.SampleLevel(point_clamp_sampler, UV + half2(-texCoord.x, -texCoord.y), 0);;
		half4 newSampleUpLeft = NewFrameData.SampleLevel(point_clamp_sampler, UV + half2(texCoord.x, -texCoord.y), 0);;
		half4 newSampleDownRight = NewFrameData.SampleLevel(point_clamp_sampler, UV + half2(-texCoord.x, texCoord.y), 0);;
		half4 newSampleDownLeft = NewFrameData.SampleLevel(point_clamp_sampler, UV + half2(texCoord.x, texCoord.y), 0);;
	
		half4 minCross = min(min(min(newFrame, newSampleUp), min(newSampleDown, newSampleRight)), newSampleLeft);
		half4 maxCross = max(max(max(newFrame, newSampleUp), max(newSampleDown, newSampleRight)), newSampleLeft);
	
		half4 minBox = min(min(newSampleUpRight, newSampleUpLeft), min(newSampleDownRight, newSampleDownLeft));
		minBox = min(minBox, minCross);
	
		half4 maxBox = max(max(newSampleUpRight, newSampleUpLeft), max(newSampleDownRight, newSampleDownLeft));
		maxBox = max(maxBox, maxCross);
	
		half4 minNew = (minBox + minCross) * 0.5;
		half4 maxNew = (maxBox + maxCross) * 0.5;
	
		half4 clampedHist = clamp(histSample, minNew, maxNew);
		histSample = lerp(clampedHist, histSample, 0.5);
		
		newFrame = lerp(histSample, newFrame, BlendFactor);
	}
	
	
	#if _DEBUG_MOTION_VECTORS
	newFrame = half4(1.0, 1.0, 1.0, 0.0);
	
	if (isValidHistUV)
	{
		newFrame = half4((MotionVector).x, (MotionVector).y, 0, 0) * 10.0;
	}
	#endif
	
	MergedData = newFrame;
	MergedDataRGB = MergedData.rgb;
	MergedDataA = MergedData.a;
	
#endif
}


#endif