using UnityEngine;

public enum ConstructionJobPhase { ReturningToWork, Chopping, GoingToSupplies, PickingUp, Carrying, Delivering }

/// <summary>Routine planner only. TownNpcBrain remains the one navigation/facing/animation owner.</summary>
[RequireComponent(typeof(TownNpcBrain)), DisallowMultipleComponent]
public sealed class TownConstructionJob : MonoBehaviour
{
    [SerializeField] private GameObject carriedWood;
    [SerializeField, Min(.1f)] private float carryingSpeed = 1.1f;
    [SerializeField] private Vector2 workSeconds = new Vector2(6f, 10f);
    [SerializeField, Min(.1f)] private float pickupSeconds = 1.25f;
    [SerializeField, Min(.1f)] private float deliverySeconds = 1.25f;
    private TownNpcWorkStation station;
    private TownNpcBrain brain;
    private Transform leftHand, rightHand;
    private bool started;
    private float until, retryAt;
    private Vector3 movementGoal;
    public TownNpcWorkStation Station => station;
    public ConstructionJobPhase Phase { get; private set; }
    public int CompletedDeliveries { get; private set; }
    public bool IsAssigned => station != null;
    public bool HasCargo => carriedWood != null && carriedWood.activeSelf;
    public GameObject CarriedWood => carriedWood;

    private void Awake()
    {
        brain = GetComponent<TownNpcBrain>();
        var animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        { leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand); rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand); }
        SetCargo(false);
    }
    public void Assign(TownNpcWorkStation value) { station = value; Suspend(); }
    public void Suspend() { started = false; retryAt = 0f; SetCargo(false); }

    public void Tick()
    {
        if (!IsAssigned || !enabled || Time.time < retryAt) return;
        if (!started)
        {
            started = true;
            if (Vector3.Distance(transform.position, station.transform.position) < .65f) BeginWork();
            else BeginMove(ConstructionJobPhase.ReturningToWork, station.transform.position, false);
            return;
        }
        if (Phase == ConstructionJobPhase.ReturningToWork || Phase == ConstructionJobPhase.GoingToSupplies || Phase == ConstructionJobPhase.Carrying)
        {
            brain.FaceJobDirection(brain.Agent.desiredVelocity);
            if (!brain.JobMovementFinished) return;
            if (Vector3.Distance(transform.position, movementGoal) > 1.1f) { Retry(); return; }
            if (Phase == ConstructionJobPhase.ReturningToWork) BeginWork();
            else if (Phase == ConstructionJobPhase.GoingToSupplies)
            { Phase = ConstructionJobPhase.PickingUp; until = Time.time + pickupSeconds; brain.SetJobStationary("Interact"); }
            else { Phase = ConstructionJobPhase.Delivering; until = Time.time + deliverySeconds; brain.SetJobStationary("Interact"); }
            return;
        }
        if (Phase == ConstructionJobPhase.Chopping) brain.FaceJobDirection(station.transform.forward);
        if (Time.time < until) return;
        if (Phase == ConstructionJobPhase.Chopping) BeginMove(ConstructionJobPhase.GoingToSupplies, station.Pickup.position, false);
        else if (Phase == ConstructionJobPhase.PickingUp)
        { SetCargo(true); BeginMove(ConstructionJobPhase.Carrying, station.Delivery.position, true); }
        else if (Phase == ConstructionJobPhase.Delivering)
        { SetCargo(false); CompletedDeliveries++; BeginMove(ConstructionJobPhase.ReturningToWork, station.transform.position, false); }
    }
    private void BeginWork()
    {
        Phase = ConstructionJobPhase.Chopping; SetCargo(false);
        until = Time.time + Random.Range(workSeconds.x, workSeconds.y);
        brain.SetJobStationary("TreeChopping");
    }
    private void BeginMove(ConstructionJobPhase phase, Vector3 goal, bool carrying)
    {
        Phase = phase; movementGoal = goal;
        if (!brain.TryJobMove(goal, carrying ? carryingSpeed : 1.65f, carrying)) Retry();
        else movementGoal = brain.Agent.destination;
    }
    private void Retry()
    {
        started = false; SetCargo(false); retryAt = Time.time + 1f;
        brain.SetJobStationary("Locomotion");
    }
    private void SetCargo(bool value) { if (carriedWood != null) carriedWood.SetActive(value); }
    private void LateUpdate()
    {
        if (!HasCargo || leftHand == null || rightHand == null) return;
        carriedWood.transform.SetPositionAndRotation((leftHand.position + rightHand.position) * .5f + transform.forward * .06f - Vector3.up * .07f, transform.rotation);
    }
    private void OnDisable() => Suspend();
}
