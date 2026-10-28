using UnityEngine;
using UnityEngine.AI;

/// <summary>Two enemies per wave, stable slot IDs; restarting reconstructs only undefeated slots.</summary>
public sealed class VillageDefenseEncounter : MonoBehaviour
{
    [SerializeField] private PlayerStoryProgress story;
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField,Min(1)] private int enemiesPerWave=2;
    [SerializeField,Min(5)] private float activationDistance=55;
    [SerializeField,Min(0)] private float waveInterval=2.5f;
    private VillageDefenseMember[] living;
    private readonly Collider[] overlaps=new Collider[32];
    private int previousWave=-1;
    private float nextSpawn;
    public PlayerStoryProgress Story=>story;
    public int CurrentWave { get; private set; }
    public int LivingCount { get {int n=0;if(living!=null)foreach(var m in living)if(m!=null&&!m.HealthDead)n++;return n;} }
    private void Awake()=>living=new VillageDefenseMember[story.DefenseQuest.requiredCount];
    private void Update()
    {
        if(story.DefenseStatus!=StoryQuestStatus.Active||Time.timeScale<=0||Time.time<nextSpawn||
            Vector3.Distance(story.transform.position,transform.position)>activationDistance)return;
        int first=0;while(first<living.Length&&story.IsDefenseSlotDefeated(first))first++;
        if(first==living.Length)return;
        int wave=first/enemiesPerWave;
        CurrentWave=wave+1;
        if(wave!=previousWave){previousWave=wave;nextSpawn=Time.time+waveInterval;return;}
        nextSpawn=Time.time+1;
        for(int i=wave*enemiesPerWave;i<Mathf.Min((wave+1)*enemiesPerWave,living.Length);i++)
        {
            if(story.IsDefenseSlotDefeated(i)||living[i]!=null)continue;
            if(!TryPosition(i,out var position))continue; // Retry blocked points without awarding a kill.
            var direction=story.transform.position-position;direction.y=0;
            var enemy=Instantiate(enemyPrefabs[i%enemyPrefabs.Length],position,direction.sqrMagnitude>.01f?Quaternion.LookRotation(direction):Quaternion.identity,transform);
            enemy.name="Village Invader "+(i+1);
            living[i]=enemy.AddComponent<VillageDefenseMember>();living[i].Initialize(this,i);
        }
    }
    private bool TryPosition(int slot,out Vector3 position)
    {
        position=Vector3.zero;if(spawnPoints==null||spawnPoints.Length==0||enemyPrefabs==null||enemyPrefabs.Length==0)return false;
        var path=new NavMeshPath();
        for(int attempt=0;attempt<16;attempt++)
        {
            var anchor=spawnPoints[(slot+attempt)%spawnPoints.Length];if(anchor==null)continue;
            Vector3 trial=anchor.position;
            if(attempt>=spawnPoints.Length){float angle=attempt*2.399f;trial+=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*2;}
            if(!NavMesh.SamplePosition(trial,out var nav,1,NavMesh.AllAreas)||Mathf.Abs(nav.position.y-trial.y)>.6f)continue;
            // Never appear inside the player, houses, trees or existing enemies.
            int count=Physics.OverlapCapsuleNonAlloc(nav.position+Vector3.up*.65f,nav.position+Vector3.up*1.7f,.48f,overlaps,Physics.AllLayers,QueryTriggerInteraction.Ignore);
            bool blocked=count==overlaps.Length;
            for(int j=0;j<count&&!blocked;j++)if(!(overlaps[j] is TerrainCollider))blocked=true;
            if(blocked||!NavMesh.CalculatePath(nav.position,transform.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
            position=nav.position;return true;
        }
        return false;
    }
    public Transform NearestLivingTarget(Vector3 origin)
    {
        Transform result=null;float closest=float.MaxValue;
        if(living!=null)foreach(var member in living)
        {
            if(member==null||member.HealthDead)continue;float distance=(origin-member.transform.position).sqrMagnitude;
            if(distance<closest){closest=distance;result=member.transform;}
        }
        return result;
    }
}
