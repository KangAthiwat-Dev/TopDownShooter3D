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
    [SerializeField, Min(0f)] private float sprintDrainPerSecond = 18f;
    [SerializeField, Min(0f)] private float minimumEnergyToSprint = 8f;

    private float lastConsumeTime;
    private float bonusEnergy, shadowDrainMultiplier = 1f;

    public float MaxEnergy => maxEnergy + bonusEnergy;
    public float SprintDrainPerSecond => sprintDrainPerSecond;
    public float ShadowDrainPerSecond => shadowDashDrainPerSecond * shadowDrainMultiplier;
    public bool CanStartSprint => CurrentEnergy >= minimumEnergyToSprint;

    public float CurrentEnergy { get; private set; }

    public float NormalizedEnergy =>
        MaxEnergy > 0f
            ? CurrentEnergy / MaxEnergy
            : 0f;

    public bool CanStartShadowDash =>
        CurrentEnergy >= minimumEnergyToStart;

    public event Action<float, float>
        EnergyChanged;

    private void Awake()
    {
        CurrentEnergy = MaxEnergy;
    }

    public void ConfigureUpgrades(float bonus,float formDrainMultiplier)
    {
        float previousMax=MaxEnergy;bonus=Mathf.Max(0,bonus);formDrainMultiplier=Mathf.Clamp(formDrainMultiplier,.1f,1f);
        if(Mathf.Approximately(bonusEnergy,bonus)&&Mathf.Approximately(shadowDrainMultiplier,formDrainMultiplier))return;
        bonusEnergy=bonus;shadowDrainMultiplier=formDrainMultiplier;
        CurrentEnergy=Mathf.Clamp(CurrentEnergy+Mathf.Max(0,MaxEnergy-previousMax),0,MaxEnergy);
        EnergyChanged?.Invoke(CurrentEnergy,MaxEnergy);
    }
    public bool ConsumeSprintEnergy(float deltaTime)=>Consume(sprintDrainPerSecond,deltaTime);

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
    { return Consume(ShadowDrainPerSecond,deltaTime); }

    private bool Consume(float drain,float deltaTime)
    {
        if (CurrentEnergy <= 0f)
        {
            return false;
        }

        lastConsumeTime = Time.time;

        SetEnergy(
            CurrentEnergy -
            drain *
            deltaTime
        );

        return CurrentEnergy > 0f;
    }

    private void SetEnergy(float newEnergy)
    {
        float clampedEnergy = Mathf.Clamp(
            newEnergy,
            0f,
            MaxEnergy
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
            MaxEnergy
        );
    }
}
