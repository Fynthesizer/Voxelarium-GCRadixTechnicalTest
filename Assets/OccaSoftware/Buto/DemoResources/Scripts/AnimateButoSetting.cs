using UnityEngine;
using UnityEngine.Rendering;
using OccaSoftware.Buto;

namespace OccaSoftware.Buto.Demo.Runtime
{
    public class AnimateButoSetting : MonoBehaviour
    {
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                Volume[] volumes = FindObjectsOfType<Volume>();
                foreach (Volume volume in volumes)
                {
                    if (volume.profile != null && volume.profile.TryGet(out VolumetricFog volumetricFog))
                    {
                        //volumetricFog.analyticFogEnabled.overrideState = true;
                        //volumetricFog.analyticFogEnabled.value = !volumetricFog.analyticFogEnabled.value;
                        volumetricFog.baseHeight.overrideState = true;
                        volumetricFog.baseHeight.value = volumetricFog.baseHeight.value + 1;
                        Debug.Log(volumetricFog.baseHeight.value);
                    }
                }
            }
        }
    }
}