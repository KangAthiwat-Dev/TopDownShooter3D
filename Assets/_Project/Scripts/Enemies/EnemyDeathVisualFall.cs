using UnityEngine;

/// <summary>Fallback fall for the Goblin demo rig, whose pack has no death take.</summary>
[RequireComponent(typeof(EnemyHealth))]
public sealed class EnemyDeathVisualFall : MonoBehaviour
{
    [SerializeField] private Transform visual;
    private EnemyHealth health;
    private Quaternion initialRotation;
    private float started=-1;
    private void Awake(){health=GetComponent<EnemyHealth>();if(visual!=null)initialRotation=visual.localRotation;}
    private void OnEnable()=>health.Died+=Fall;
    private void OnDisable(){if(health!=null)health.Died-=Fall;}
    private void Fall()=>started=Time.time;
    private void Update()
    {
        if(started<0||Time.timeScale<=0||visual==null)return;
        float t=Mathf.SmoothStep(0,1,(Time.time-started)/.65f);
        visual.localRotation=Quaternion.Slerp(initialRotation,initialRotation*Quaternion.Euler(0,0,88),t);
    }
}
