using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class StanceVignette : MonoBehaviour
{
    [SerializeField] private float minVal = 0.1f;
    [SerializeField] private float maxVal = 0.35f;
    [SerializeField] private float response = 10f;

    private VolumeProfile _profile;
    private Vignette _vignette;
    public void Initialize(VolumeProfile profile)
    {
        _profile = profile;

        if (!profile.TryGet(out _vignette))
            _vignette = profile.Add<Vignette>();

        _vignette.active = true;
        _vignette.intensity.Override(minVal);
    }

    public void UpdateVignette(float deltaTime, Stance stance)
    {
        var targetIntensity = stance is Stance.Stand ? minVal : maxVal;
        var newIntensity = Mathf.Lerp
        (
            a: _vignette.intensity.value,
            b: targetIntensity,
            t: 1f - Mathf.Exp(-response * deltaTime)
        );
        _vignette.intensity.Override(newIntensity);
    }
}
