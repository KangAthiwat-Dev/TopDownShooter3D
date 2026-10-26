using System;
using UnityEngine;

public class PlayerEnergy : MonoBehaviour
{
    [Header("Energy")]
    [SerializeField, Min(1f)]
    private float maxEnergy = 100f;

    [SerializeField, Min(0f)]
    private float shadowDashDrainPerSecond = 30f;

    [SerializeField, Min(0f)]
    private float regenerationPerSecond = 20f;

    [SerializeField, Min(0f)]
    private float regenerationDelay = 0.75f;

    [SerializeField, Min(0f)]
    private float minimumEnergyToStart = 10f;

    private float lastConsumeTime;

    public float MaxEnergy => maxEnergy;

    public float CurrentEnergy { get; private set; }

    public float NormalizedEnergy =>
        maxEnergy > 0f
            ? CurrentEnergy / maxEnergy
            : 0f;

    public bool CanStartShadowDash =>
        CurrentEnergy >= minimumEnergyToStart;

    public event Action<float, float>
        EnergyChanged;

    private void Awake()
    {
        CurrentEnergy = maxEnergy;
    }

    private void Update()
    {
        if (
            Time.time <
            lastConsumeTime + regenerationDelay
        )
        {
            return;
        }

        SetEnergy(
            CurrentEnergy +
            regenerationPerSecond *
            Time.deltaTime
        );
    }

    public bool ConsumeShadowDashEnergy(
        float deltaTime
    )
    {
        if (CurrentEnergy <= 0f)
        {
            return false;
        }

        lastConsumeTime = Time.time;

        SetEnergy(
            CurrentEnergy -
            shadowDashDrainPerSecond *
            deltaTime
        );

        return CurrentEnergy > 0f;
    }

    private void SetEnergy(float newEnergy)
    {
        float clampedEnergy = Mathf.Clamp(
            newEnergy,
            0f,
            maxEnergy
        );

        if (Mathf.Approximately(
            CurrentEnergy,
            clampedEnergy
        ))
        {
            return;
        }

        CurrentEnergy = clampedEnergy;

        EnergyChanged?.Invoke(
            CurrentEnergy,
            maxEnergy
        );
    }
}