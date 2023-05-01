using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OccaSoftware.Buto
{
    public enum VolumetricFogMode
	{
        Off,
        On
	}

    [Serializable]
    public sealed class VolumetricFogModeParameter : VolumeParameter<VolumetricFogMode>
    {
        public VolumetricFogModeParameter(VolumetricFogMode value, bool overrideState = false) : base(value, overrideState) { }
    }

    public enum QualityLevel
    {
        Low,
        High,
        Custom
    }

    [Serializable]
    public sealed class QualityLevelParameter : VolumeParameter<QualityLevel>
    {
        public QualityLevelParameter(QualityLevel value, bool overrideState = false) : base(value, overrideState) { }
    }


    [Serializable, VolumeComponentMenuForRenderPipeline("Volumetrics/Buto Volumetric Fog", typeof(UniversalRenderPipeline))]
    public sealed class VolumetricFog : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Set to On to enable Volumetric Fog.")]
        public VolumetricFogModeParameter mode = new VolumetricFogModeParameter(VolumetricFogMode.Off);

        public QualityLevelParameter qualityLevel = new QualityLevelParameter(QualityLevel.Low);
        // Performance and baseline rendering

        [Tooltip("Defines the number of points used to integrate the volumetric fog volume. Higher values are more computationally expensive, but lower values can result in artifacts.")]
        public ClampedIntParameter sampleCount = new ClampedIntParameter(48, 8, 128);

        [Tooltip("When toggled on, the sample positions will be randomly adjusted each frame. This replaces static noise with dynamic noise.")]
        public BoolParameter animateSamplePosition = new BoolParameter(false);

        [Tooltip("When toggled on, fog will realistically attenuate light from the main directional light. Self Shadowing is computationally expensive but looks more realistic.")]
        public BoolParameter selfShadowingEnabled = new BoolParameter(true);

        [Tooltip("Self Shadowing Octaves are extremely performance intensive. Typically, only 1 self shadowing octave is sufficient. However, more self shadowing octaves do give more realistic results. Self shadowing will never use more octaves than the baseline noise rendering (set in Volumetric Noise below).")]
        public ClampedIntParameter maximumSelfShadowingOctaves = new ClampedIntParameter(1, 1, 3);

        [Tooltip("When toggled on, the fog will be shadowed when the main light is below the horizon line. Horizon Shadowing is computationally expensive but looks more realistic.")]
        public BoolParameter horizonShadowingEnabled = new BoolParameter(true);

        [Tooltip("Distance at which the fog renderer will switch from volumetric to analytic fog. This does not affect performance, but can affect fog rendering quality. Smaller values cause the renderer to traverse a smaller distance between fog samples, resulting in higher quality and less artifacts.")]
        public MinFloatParameter maxDistanceVolumetric = new MinFloatParameter(128, 10);

        [Tooltip("Analytic fog replaces volumetric fog after the Volumetric Fog Sampling Distance. Disabling analytic fog can improve performance.")]
        public BoolParameter analyticFogEnabled = new BoolParameter(false);

        [Tooltip("Maximum distance that will be simulated for analytic fog. This does not affect performance, but can affect the visual characteristics of your scene.")]
        public MinFloatParameter maxDistanceAnalytic = new MinFloatParameter(5000, 100);

        [Tooltip("Enables Temporal Anti-Aliasing, which can increase the quality of the fog in scenarios with high density fog.")]
        public BoolParameter temporalAntiAliasingEnabled = new BoolParameter(false);

        [Tooltip("Determines the importance of the most recent frame data when integrating the fog. Lower values mean that more data is used from the historical data cache.")]
        public ClampedFloatParameter temporalAntiAliasingIntegrationRate = new ClampedFloatParameter(0.03f, 0.01f, 0.99f);


        // Fog Parameters
        [Tooltip("Density of fog in the scene.")]
        public MinFloatParameter fogDensity = new MinFloatParameter(5, 0);

        [Tooltip("Defines the direction in which light will scatter. Negative values cause light to scatter backwards. Positive values cause light to scatter forwards.")]
        public ClampedFloatParameter anisotropy = new ClampedFloatParameter(0.2f, -1, 1);

        [Tooltip("A multiplier that affects the intensity of the fog lighting effects in unshadowed areas. Increasing this causes the fog's lighting to become brighter in lit areas. Should only be used for stylized effects. [Default: 1]")]
        public MinFloatParameter lightIntensity = new MinFloatParameter(1, 0);

        [Tooltip("A multiplier that affects the density of the fog in shadowed areas. Increasing this causes the fog to become more dense in shadowed areas. In effect, this causes shadowed areas to become more apparent. However, be wary that it can also cause shadowed areas to become more opaque. Should only be used for stylized effects. [Default: 1]")]
        public MinFloatParameter shadowIntensity = new MinFloatParameter(1, 0);


        // Geometry
        [Tooltip("Defines the \"floor\" of the fog volume. Fog falloff only begins after the fog passes this altitude.")]
        public FloatParameter baseHeight = new FloatParameter(0);

        [Tooltip("Defines the height at which the fog density will have been attenuated to 36% of the set fog density. This is always calculated on top of the Base Height. For example, if your Base Height is 10 and your Attenuation Boundary Size is 15, your fog will be at full density until a Height of y = 10 units, then it will attenuate to 36% of the original density by a height of y = 25 units.")]
        public MinFloatParameter attenuationBoundarySize = new MinFloatParameter(10, 1);


        // Custom Colors
        [Tooltip("Set the lit color value. This will be multiplied with the color ramp, if one is present.")]
        public ColorParameter litColor = new ColorParameter(Color.white, true, false, false);
        [Tooltip("Set the shaded color value. This will be multiplied with the color ramp, if one is present.")]
        public ColorParameter shadowedColor = new ColorParameter(Color.white, true, false, false);
        [Tooltip("Set the emissive color value. This will be multiplied with the color ramp, if one is present.")]
        public ColorParameter emitColor = new ColorParameter(Color.white, true, false, false);

        [Tooltip("A texture that can be used to give the fog an emission value and to manipulate the fog color in lit and shadowed areas. Typically used for stylized effects. Combined multiplicatively with the lit, shadowed, and emission colors.")]
        public Texture2DParameter colorRamp = new Texture2DParameter(null);

        [Tooltip("Controls the amount of influence that the lit, shadowed, and emit colors (as well as the color ramp) exert over the base physical results. A value of 0 means the color values are completely ignored. A value of 1 means that we exclusively use the values provided here.")]
        public ClampedFloatParameter colorInfluence = new ClampedFloatParameter(0, 0, 1);


        [Tooltip("Set the tint of the fog towards the main directional light. Combined multiplicatively with the fog before adding emission. A value of white means the fog is untinted.")]
        public ColorParameter directionalForward = new ColorParameter(Color.white, true, false, false);
        
        [Tooltip("Set the tint of the fog away from the main directional light. Combined multiplicatively with the fog before adding emission. A value of white means the fog is untinted.")]
        public ColorParameter directionalBack = new ColorParameter(Color.white, true, false, false);
        
        [Tooltip("Set the falloff between the forward and back directional lighting terms.")]
        public FloatParameter directionalRatio = new FloatParameter(1f);


        // Noise
        [Obsolete("Replaced by the volume noise parameter. Use that instead.")]
        [Tooltip("A 3D Texture that will be used to define the fog intensity. Repeats over the noise tiling domain. A value of 0 means the fog density is attenuated to 0. A value of 1 means the fog density is not attenuated and matches what is set in the Fog Density parameter.")]
        public Texture3DParameter noiseTexture = new Texture3DParameter(null);

        [Tooltip("Progressively resamples the noise texture over smaller tiling domains to increase the level of detail in the final fog presentation. More octaves are more computationally expensive but increase the level of realism.")]
        public ClampedIntParameter octaves = new ClampedIntParameter(1, 1, 3);

        [Tooltip("Amount by which the texture sampling frequency will increase with each successive octave.")]
        public ClampedFloatParameter lacunarity = new ClampedFloatParameter(2, 1, 8);

        [Tooltip("Amount by which the texture amplitude will decrease with each successive octave.")]
        public ClampedFloatParameter gain = new ClampedFloatParameter(0.3f, 0, 1);

        [Tooltip("The length of each side of the cube that describes the rate at which the noise texture will repeat. In other words, the scale of the noise texture in meters.")]
        public MinFloatParameter noiseTiling = new MinFloatParameter(200, 0);

        [Tooltip("The rate at which the noise will be offset over time. Use this to simulate wind. Measured in meters per second.")]
        public Vector3Parameter noiseWindSpeed = new Vector3Parameter(new Vector3(0, 0, 0));

        [Tooltip("Remaps the noise input texture from the original [0, 1] range to a new range defined by [NoiseIntensityMin, NoiseIntensityMax]. For example, an mapping of [0.2, 0.8] will remap the noise by clipping any values below 0.2 and any values above 0.8, then remapping the remaining 0.2 to 0.8 range back to 0.0 to 1.0 to retain detail.")]
        public FloatRangeParameter noiseMap = new FloatRangeParameter(new Vector2(0, 1), 0, 1);


        public VolumeNoiseParameter volumeNoise = new VolumeNoiseParameter(new VolumeNoise());


        public bool IsActive()
		{
            if (mode.value != VolumetricFogMode.On || fogDensity.value <= 0)
                return false;

            return true;
		}

        public bool IsTileCompatible() => false;
	}

}
