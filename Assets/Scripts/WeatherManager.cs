using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Random = UnityEngine.Random;

public class WeatherManager : MonoBehaviour
{
    [Header("Time")]
    public float time = 9; //Total time
    public float timeOfDay;
    public float timeSpeed = 60f; //1 min per second
    int hour = -1;
    [Range(0f, 90f)] public float sunElevation = 45f;
    public float sunIntensity = 1f;
    public float moonIntensity = 0.4f;

    [Header("Weather")]
    public float cloudiness = 1f;
    public bool raining;
    public float rainValue = 0.0f;
    public float rainTransitionLength = 5f;
    public float rainChance = 0.2f;

    [Header("Wind")]
    public Vector3 windDirection;
    public float windSpeed;
    public float windTurbulence = 0.01f;

    [Header("Fog")]
    [ColorUsage(false, true)] public Color dayFogColor;
    [ColorUsage(false, true)] public Color nightFogColor;
    [ColorUsage(false, true)] public Color duskFogColor;
    public float fogDensity = 0.002f;

    [Header("Sun and Moon")]
    public Color daySunColor;
    public Color duskSunColor;

    ParticleSystem cloudSystem;
    ParticleSystem rainSystem;
    public Transform sky;
    public Transform sun;
    public Transform moon;
    Light sunLight;
    Light moonLight;

    WindZone wind;

    int seed;

    bool indoors = false;

    private bool setup = false;

    public AnimationCurve temperatureOverHeight;

    public void Setup()
    {
        seed = GameManager.gm.worldSettings.seed;
        wind = transform.Find("WindZone").GetComponent<WindZone>();

        hour = Mathf.FloorToInt(time);

        //Generate wind variables
        Vector2 windD = Random.insideUnitCircle.normalized;
        float windS = Random.Range(0f, 1f);
        SetWind(windD, windS);
        //Shader.SetGlobalFloat("_WindStrength", windSpeed * 4);

        //Setup particles
        cloudSystem = GameObject.FindGameObjectWithTag("Clouds").GetComponent<ParticleSystem>();
        rainSystem = GameObject.FindGameObjectWithTag("Rain").GetComponent<ParticleSystem>();
        ParticleSystem.EmissionModule cloudEmission = cloudSystem.emission;
        ParticleSystem.ShapeModule cloudShape = cloudSystem.shape;
        cloudEmission.rateOverTime = cloudiness;
        cloudShape.scale = new Vector3(1000, 100, 1000);
        ParticleSystem.EmissionModule rainEmission = rainSystem.emission;

        SetRain(raining);
        cloudSystem.Simulate(120f);
        cloudSystem.Play();

        RenderSettings.ambientIntensity = 1f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;

        sunLight = sun.GetChild(0).GetComponent<Light>();
        moonLight = moon.GetChild(0).GetComponent<Light>();

        UpdateLights();

        setup = true;
    }

    void SetWind(Vector2 direction, float strength)
    {
        windDirection = new Vector3(direction.x, 0f, direction.y);
        windSpeed = strength;
        Shader.SetGlobalFloat("_WindStrength", windSpeed);
        wind.transform.LookAt(windDirection);
        wind.windMain = strength;
        //FMODUnity.RuntimeManager.StudioSystem.setParameterByName("Wind", strength);
    }

    void SetCloudiness(float value)
    {
        ParticleSystem.EmissionModule cloudEmission = cloudSystem.emission;
        cloudiness = value;
        cloudEmission.rateOverTime = cloudiness;
    }

    void SetRain(bool value)
    {
        raining = value;
        StartCoroutine(RainTransition());
    }


    void SetFog(float value)
    {
        fogDensity = value;
        RenderSettings.fogDensity = fogDensity;
    }

    void UpdateCelestialBodies()
    {
        //Set sun position
        Camera cam = Camera.main;
        sky.transform.position = new Vector3(cam.transform.position.x, 0f, cam.transform.position.z);
        float rot = Map(timeOfDay, 0, 24, -Mathf.PI, Mathf.PI);
        float offset = Map(sunElevation, 0, 90, 1, 0);
        float scale = Map(sunElevation, 0, 90, 0, 1);
        Vector3 sunPos = new Vector3(Mathf.Sin(rot) * scale, Mathf.Cos(rot) * scale, offset);
        Shader.SetGlobalFloat("_SunPosition", sunPos.normalized.y);
        Shader.SetGlobalVector("_SunDirection", sunPos.normalized);

        //To avoid weird lighting, don't allow lights to move below horizon
        sunLight.transform.position = new Vector3(sun.position.x, Mathf.Max(sun.position.y, 1f), sun.position.z);
        moonLight.transform.position = new Vector3(moon.position.x, Mathf.Max(moon.position.y, 1f), moon.position.z);

        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, sunPos);
        sky.rotation = rotation;
    }

    // Update is called once per frame
    void Update()
    {
        if (setup)
        {
            time += (Time.deltaTime * timeSpeed) / 3600;
            timeOfDay = time % 24;

            UpdateCelestialBodies();

            //FMODUnity.RuntimeManager.StudioSystem.setParameterByName("Time", timeOfDay);

            //Update fog and sun colour
            float t;
            Color fc = new Color();
            Color sc = new Color();
            //Night to dawn
            if (timeOfDay < 6f)
            {
                t = Mathf.Clamp(Map(timeOfDay, 5f, 6f, 0f, 1f), 0f, 1f);
                fc = Color.Lerp(nightFogColor, duskFogColor, t);
                sc = duskSunColor;
            }
            //Dawn to day
            else if (timeOfDay < 12f)
            {
                t = Mathf.Clamp(Map(timeOfDay, 6f, 8f, 0f, 1f), 0f, 1f);
                fc = Color.Lerp(duskFogColor, dayFogColor, t);
                sc = Color.Lerp(duskSunColor, daySunColor, t);
            }
            //Day to dusk
            else if (timeOfDay < 18f)
            {
                t = Mathf.Clamp(Map(timeOfDay, 16f, 18f, 0f, 1f), 0f, 1f);
                fc = Color.Lerp(dayFogColor, duskFogColor, t);
                sc = Color.Lerp(daySunColor, duskSunColor, t);
            }
            //Dusk to night
            else if (timeOfDay < 24f)
            {
                t = Mathf.Clamp(Map(timeOfDay, 18f, 19f, 0f, 1f), 0f, 1f);
                fc = Color.Lerp(duskFogColor, nightFogColor, t);
                sc = duskSunColor;
            }

            float h, s, v;
            Color.RGBToHSV(fc, out h, out s, out v);
            fc = Color.HSVToRGB(h, s / (1 + rainValue), v / (1 + rainValue)); //Desaturate fog during rain
            RenderSettings.fogColor = fc;
            sc = Color.Lerp(sc, Color.grey, rainValue);
            sunLight.color = sc;
            Shader.SetGlobalColor("_SunColour", sc);

            ParticleSystem.ShapeModule cloudShape = cloudSystem.shape;
            rainSystem.transform.position = Camera.main.transform.position + new Vector3(0f, 24f, 0f);
            cloudShape.position = new Vector3(Camera.main.transform.position.x, 0, Camera.main.transform.position.z);

            //Update wind direction
            if (windTurbulence > 0f)
            {
                Vector3 w = new Vector3(Mathf.PerlinNoise(time * windTurbulence, seed) - 0.5f, Mathf.PerlinNoise(time * windTurbulence, seed + 10) - 0.5f, Mathf.PerlinNoise(time * windTurbulence, seed + 20) - 0.5f) * 2;
                SetWind(w.normalized, w.magnitude);
            }

            //Function for hourly events
            if (Mathf.FloorToInt(time) != hour)
            {
                OnHour();
            }
        }
    }

    void UpdateLights()
    {
        if(hour <= 6 || hour > 18) //Night time
        {
            moonLight.intensity = moonIntensity;
            sunLight.intensity = 0f;
            sunLight.enabled = false;
            moonLight.enabled = true;
            RenderSettings.ambientIntensity = 0.6f;
        }
        else //Day time
        {
            moonLight.intensity = 0f;
            sunLight.intensity = sunIntensity;
            moonLight.enabled = false;
            sunLight.enabled = true;
            RenderSettings.ambientIntensity = 1f;
        }
    }

    void OnHour()
    {
        hour = Mathf.FloorToInt(time);

        //Sample noise to decide if it should be raining
        float rainSample = Mathf.PerlinNoise(time / 10f, seed);
        if (rainSample < rainChance && !raining) SetRain(true);
        else if (rainSample > rainChance && raining) SetRain(false);
        SetCloudiness(Helpers.Map(rainSample, 0f, 1f, 0f, 2f));

        if (hour == 18f) StartCoroutine(Sunset(0.1f));
        else if (hour == 6f) StartCoroutine(Sunrise(0.1f));
        //Update reflection probe
        //StartCoroutine(BlendProbe());
    }

    public void OnWorldLoad()
    {
        //reflectionProbes[0].GetComponent<ReflectionProbe>().RenderProbe();
        OnHour();
    }

    float Map(float value, float minIn, float maxIn, float minOut, float maxOut)
    {
        return (value - minIn) / (maxIn - minIn) * (maxOut - minOut) + minOut;
    }

    public void SetIndoors(bool value)
    {
        if (indoors != value)
        {
            indoors = value;
            UpdateScene();
        }
    }

    IEnumerator RainTransition()
    {
        float timeElapsed = 0;
        float targetValue;
        float startValue = rainValue;

        ParticleSystem.EmissionModule rainEmission = rainSystem.emission;

        if (raining) targetValue = 1;
        else targetValue = 0;

        //FMODUnity.RuntimeManager.StudioSystem.setParameterByName("Rain", targetValue);

        while (timeElapsed < rainTransitionLength)
        {
            rainValue = Mathf.Lerp(startValue, targetValue, timeElapsed / rainTransitionLength);
            timeElapsed += Time.deltaTime;

            UpdateScene();

            yield return null;
        }
        rainValue = targetValue;

        UpdateScene();
    }

    IEnumerator Sunset(float duration)
    {
        float startTime = time;
        float endTime = time + duration;
        moonLight.enabled = true;
        float progress = 0f;
        while(progress < 1f)
        {
            progress = (time - startTime) / duration;
            sunLight.intensity = Mathf.Lerp(sunIntensity, 0f, progress); //Dim sun
            moonLight.intensity = Mathf.Lerp(0f, moonIntensity, progress); //Brighten moon
            RenderSettings.ambientIntensity = Mathf.Lerp(1f, 0.6f, progress);
            yield return null;
        }
        sunLight.intensity = 0f;
        sunLight.enabled = false;
        moonLight.intensity = moonIntensity;
        //moon.shadows = LightShadows.Soft;
        //sun.shadows = LightShadows.None;
        RenderSettings.ambientIntensity = 0.6f;
        RenderSettings.sun = moonLight;
    }

    IEnumerator Sunrise(float duration)
    {
        float startTime = time;
        float endTime = time + duration;
        sunLight.enabled = true;
        float progress = 0f;
        while (progress < 1f)
        {
            progress = (time - startTime) / duration;
            sunLight.intensity = Mathf.Lerp(0f, sunIntensity, progress); //Dim sun
            moonLight.intensity = Mathf.Lerp(moonIntensity, 0f, progress); //Brighten moon
            RenderSettings.ambientIntensity = Mathf.Lerp(0.6f, 1f, progress);
            yield return null;
        }
        sunLight.intensity = sunIntensity;
        moonLight.enabled = false;
        moonLight.intensity = 0f;
        //moon.shadows = LightShadows.None;
        //sun.shadows = LightShadows.Soft;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.sun = sunLight;
    }

    void UpdateScene()
    {
        ParticleSystem.EmissionModule rainEmission = rainSystem.emission;

        SetFog(Map(rainValue, 0f, 1f, 0.002f, 0.005f));
        //ambienceLayers[1].volume = rainValue;
        sunIntensity = Map(rainValue, 0f, 1f, 1f, 0.5f);
        moonIntensity = Map(rainValue, 0f, 1f, 0.4f, 0.2f);
        Shader.SetGlobalFloat("_Raining", rainValue);
        rainEmission.rateOverTime = Map(rainValue, 0f, 1f, 0, 2500);
        if (indoors) rainEmission.enabled = false;
        else rainEmission.enabled = true;
    }

    public float GetTemperature(Vector3 position)
    {
        float temperature;
        float timeMod; //Variation based on time of day
        float heightMod; //Variation based on altitude
        float rainMod = Helpers.Map(rainValue, 0f, 1f, 0f, -5f); //Make it colder if it's raining
        float windMod = Helpers.Map(windSpeed, 0f, 1f, 0f, -5f); //Variation based on wind strength

        if (position.y > 10) heightMod = temperatureOverHeight.Evaluate(position.y); 
        else heightMod = 15f; //Water temperature

        switch (timeOfDay)
        {
            case < 9f:
                timeMod = Helpers.Map(timeOfDay, 6f, 9f, -5f, 0f, true);
                break;
            case > 18f:
                timeMod = Helpers.Map(timeOfDay, 18f, 21f, 0f, -5f, true);
                break;
            default:
                timeMod = 0f;
                break;
        }

        //Get the radiator with the most effect at the specified position
        HeatRadiator[] radiators = GameObject.FindObjectsOfType<HeatRadiator>();
        float radiatorMod = 0f;
        foreach(HeatRadiator r in radiators)
        {
            float radiatorTemp = r.GetTemperature(position);
            if (radiatorTemp > radiatorMod) radiatorMod = radiatorTemp;
        }

        temperature = heightMod + timeMod + rainMod + radiatorMod + windMod;

        return temperature;
    }
}
