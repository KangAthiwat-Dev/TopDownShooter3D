using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum WorldMonsterHabitat { Any, Forest, Ruins }

[Serializable]
public sealed class WorldMonsterSpecies
{
    public string id;
    public GameObject prefab;
    [Min(0)] public int population = 3;
    public WorldMonsterHabitat habitat;
}

[Serializable]
public sealed class WorldMonsterSite
{
    public Transform point;
    public WorldMonsterHabitat habitat;
}

/// <summary>Bounded overworld population; camps respawn only as complete encounters.</summary>
public sealed class WorldMonsterDirector : MonoBehaviour
{
    public Terrain terrain;
    public Transform player;
    public WorldMonsterSafeZone[] safeZones;
    public WorldMonsterSite[] roamingSites;
    public Transform[] campSites;
    public WorldMonsterSpecies[] species;
    public WorldMonsterCamp[] campTemplates;
    public GameObject goblinPrefab;
    public GameObject chiefPrefab;
    [Header("Population and respawn")]
    [Min(0)] public int campPopulation = 3;
    [Min(1)] public float roamingRespawnDelay = 45;
    [Min(1)] public float campRespawnDelay = 150;
    [Min(12)] public float minimumPlayerDistance = 18;
    [Min(24)] public float campPlayerDistance = 32;
    [Tooltip("0 chooses a fresh seed each map load; any other value is reproducible.")]
    public int seed;
    public int ActiveSeed { get; private set; }
    public bool Initialized { get; private set; }
    public int RoamingAlive { get { int n=0; foreach(var s in roaming) if(Living(s.member)) n++; return n; } }
    public int CampCount => camps.Count;
    public int TotalAlive { get { int n=RoamingAlive; foreach(var c in camps) foreach(var m in c.members) if(Living(m)) n++; return n; } }
    public IReadOnlyList<WorldMonsterCamp> ActiveCamps => campViews;

    /// <summary>One signal per actual death. EXP remains owned by EnemyHealth.</summary>
    public event Action<WorldMonsterMember> MonsterDefeated;

    private sealed class RoamingSlot { public int species; public WorldMonsterMember member; public float readyAt; }
    private sealed class CampSlot
    {
        public WorldMonsterCamp layout;
        public readonly List<WorldMonsterMember> members = new List<WorldMonsterMember>();
        public float readyAt = -1;
    }
    private readonly List<RoamingSlot> roaming = new List<RoamingSlot>();
    private readonly List<CampSlot> camps = new List<CampSlot>();
    private readonly List<WorldMonsterCamp> campViews = new List<WorldMonsterCamp>();
    private readonly Collider[] clearance = new Collider[48];
    private System.Random random;
    private Transform roamingRoot, campRoot;
    private Camera view;
    private bool shuttingDown;
    private float nextTick;
    private static bool Living(WorldMonsterMember m) => m != null && !m.Defeated && m.Health != null && m.Health.IsAlive;
    private float Range(float a, float b) => a + (float)random.NextDouble() * (b-a);

    private IEnumerator Start()
    {
        if (terrain == null || player == null || species == null || roamingSites == null || roamingSites.Length == 0 || campSites == null)
        { Debug.LogError("World population is missing its terrain, player or spawn sites.", this); yield break; }
        ActiveSeed = seed == 0 ? Environment.TickCount : seed;
        random = new System.Random(ActiveSeed);
        view = Camera.main;
        roamingRoot = new GameObject("Roaming Monsters (runtime)").transform;
        roamingRoot.SetParent(transform, false);
        campRoot = new GameObject("Goblin Tribes (runtime)").transform;
        campRoot.SetParent(transform, false);
        // Reserve complete camp footprints before placing solitary monsters.
        for (int i=0; i<campPopulation; i++)
        {
            var slot = new CampSlot();
            if (TryCreateCamp(slot, camps.Count, true)) { camps.Add(slot); campViews.Add(slot.layout); }
            else Debug.LogWarning("No safe clearing available for Goblin camp " + (i+1), this);
            yield return null;
        }
        for (int i=0; i<species.Length; i++)
        {
            if (species[i].prefab == null) continue;
            for (int j=0; j<species[i].population; j++)
            {
                var slot = new RoamingSlot {species=i}; roaming.Add(slot);
                TrySpawnRoamer(slot, true);
                yield return null;
            }
        }
        Initialized = true;
    }

    private void Update()
    {
        if (!Initialized || Time.timeScale <= 0 || Time.time < nextTick) return;
        nextTick = Time.time + .75f;
        // A temporarily obstructed clearing must not permanently lose a configured tribe.
        if (camps.Count < campPopulation)
        {
            var missing = new CampSlot();
            if (TryCreateCamp(missing, camps.Count, false)) { camps.Add(missing); campViews.Add(missing.layout); }
        }
        int spawned=0;
        foreach (var slot in roaming)
        {
            if (Living(slot.member) || Time.time < slot.readyAt) continue;
            if (TrySpawnRoamer(slot, false) && ++spawned >= 2) break;
        }
        for (int i=0; i<camps.Count; i++)
        {
            var camp=camps[i];
            bool alive=false; foreach(var member in camp.members) if(Living(member)) {alive=true;break;}
            if (alive) continue;
            if (camp.readyAt < 0) camp.readyAt = Time.time + campRespawnDelay;
            if (Time.time < camp.readyAt || !FarAndHidden(camp.layout.transform.position, camp.layout.radius, false)) continue;
            var replacement=new CampSlot();
            if (!TryCreateCamp(replacement, i, false)) continue;
            Destroy(camp.layout.gameObject);
            camps[i]=replacement; campViews[i]=replacement.layout;
        }
    }

    public bool IsProtected(Vector3 position, float padding=0)
    {
        if (safeZones != null) foreach(var area in safeZones)
            if (area != null && area.Contains(position,padding)) return true;
        return false;
    }

    public bool TryGround(Vector3 point, float radius, out Vector3 grounded)
    {
        grounded=point;
        if (terrain == null) return false;
        Vector3 local=point-terrain.transform.position, size=terrain.terrainData.size;
        if (local.x<radius+2 || local.z<radius+2 || local.x>size.x-radius-2 || local.z>size.z-radius-2) return false;
        point.y=terrain.SampleHeight(point)+terrain.transform.position.y;
        // Rivers/lava basins are below the ordinary traversable ground.
        if(point.y<2.3f || IsProtected(point,radius)) return false;
        if(!NavMesh.SamplePosition(point,out var nav,.7f,NavMesh.AllAreas) ||
           Mathf.Abs(nav.position.y-point.y)>.4f ||
           new Vector2(nav.position.x-point.x,nav.position.z-point.z).sqrMagnitude>.25f) return false;
        grounded=nav.position;
        return Clear(grounded, .5f);
    }

    private bool Clear(Vector3 point, float radius)
    {
        int count=Physics.OverlapCapsuleNonAlloc(point+Vector3.up*(radius+.15f),point+Vector3.up*1.7f,radius,clearance,Physics.AllLayers,QueryTriggerInteraction.Ignore);
        if(count==clearance.Length) return false;
        for(int i=0;i<count;i++) if(clearance[i]!=null && !(clearance[i] is TerrainCollider)) return false;
        return true;
    }

    private bool FarAndHidden(Vector3 point, float radius, bool initial)
    {
        float distance=radius>2 ? campPlayerDistance : minimumPlayerDistance;
        if(player!=null && HorizontalDistance(point,player.position)<distance) return false;
        if(initial || view==null) return true;
        // Entire camp stays outside the camera before either recycling or appearing.
        return !GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(view),
            new Bounds(point+Vector3.up*2,new Vector3(radius*2,6,radius*2)));
    }

    private bool AwayFromPopulation(Vector3 point, float radius)
    {
        foreach(var slot in roaming)
            if(Living(slot.member) && HorizontalDistance(point,slot.member.transform.position)<radius+2) return false;
        foreach(var c in camps)
            if(c.layout!=null && HorizontalDistance(point,c.layout.transform.position)<radius+c.layout.radius+3) return false;
        return true;
    }

    public static float HorizontalDistance(Vector3 a,Vector3 b) => new Vector2(a.x-b.x,a.z-b.z).magnitude;

    private bool TrySpawnRoamer(RoamingSlot slot, bool initial)
    {
        var kind=species[slot.species];
        for(int attempt=0;attempt<100;attempt++)
        {
            var site=roamingSites[random.Next(roamingSites.Length)];
            if(site.point==null || (kind.habitat!=WorldMonsterHabitat.Any && site.habitat!=kind.habitat)) continue;
            var point=site.point.position+new Vector3(Range(-1.5f,1.5f),0,Range(-1.5f,1.5f));
            if(!TryGround(point,1,out point) || !FarAndHidden(point,1,initial) || !AwayFromPopulation(point,1)) continue;
            slot.member=Spawn(kind.prefab,kind.id,-1,point,Range(0,360),roamingRoot);
            return true;
        }
        slot.readyAt=Time.time+3;
        return false;
    }

    private bool TryCreateCamp(CampSlot slot,int id,bool initial)
    {
        if(campTemplates==null || campTemplates.Length==0 || campSites.Length==0 || goblinPrefab==null || chiefPrefab==null) return false;
        for(int attempt=0;attempt<120;attempt++)
        {
            var site=campSites[random.Next(campSites.Length)]; if(site==null) continue;
            var template=campTemplates[random.Next(campTemplates.Length)];float radius=template.radius;
            if(!TryGround(site.position,radius,out var center) || !FarAndHidden(center,radius,initial) || !AwayFromPopulation(center,radius)) continue;
            bool valid=true;
            for(int i=0;i<16;i++)
            {
                float angle=i*Mathf.PI/8;
                Vector3 sample=center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                if(!TryGround(sample,0,out var ground) || Mathf.Abs(ground.y-center.y)>.5f) {valid=false;break;}
            }
            if(!valid) continue;
            float yaw=Range(0,360);var rotation=Quaternion.Euler(0,yaw,0);
            var points=new Vector3[template.memberPoints.Length];
            for(int i=0;i<points.Length;i++)
            {
                var expected=center+rotation*template.memberPoints[i].localPosition;
                if(!TryGround(expected,0,out points[i])) {valid=false;break;}
            }
            if(!valid) continue;
            slot.layout=Instantiate(template,center,rotation,campRoot);slot.layout.name="Goblin Tribe "+(id+1)+" - "+template.name;
            slot.layout.CampId=id;slot.layout.GroundProps(terrain);Physics.SyncTransforms();
            // Check authored paths against the final prop colliders before committing the camp.
            for(int i=0;i<points.Length;i++) if(!Clear(points[i],.45f)) {valid=false;break;}
            if(!valid) {slot.layout.gameObject.SetActive(false);Destroy(slot.layout.gameObject);slot.layout=null;continue;}
            for(int i=0;i<points.Length;i++)
                slot.members.Add(Spawn(i==0?chiefPrefab:goblinPrefab,i==0?"goblin_chief":"goblin_demon",id,points[i],yaw+Range(-75,75),slot.layout.transform));
            return true;
        }
        return false;
    }

    private WorldMonsterMember Spawn(GameObject prefab,string speciesId,int campId,Vector3 position,float yaw,Transform parent)
    {
        var go=Instantiate(prefab,position,Quaternion.Euler(0,yaw,0),parent);
        go.name=speciesId;
        var member=go.AddComponent<WorldMonsterMember>();member.Initialize(this,speciesId,campId);return member;
    }

    internal void MemberDefeated(WorldMonsterMember member)
    {
        foreach(var slot in roaming) if(slot.member==member) {slot.readyAt=Time.time+roamingRespawnDelay;break;}
        MonsterDefeated?.Invoke(member);
    }

    internal void MemberRemoved(WorldMonsterMember member)
    {
        if(shuttingDown) return;
        foreach(var slot in roaming) if(slot.member==member)
        { if(!member.Defeated) slot.readyAt=Time.time+roamingRespawnDelay;slot.member=null;break; }
    }

    private void OnDestroy() => shuttingDown=true;

    private void OnDrawGizmosSelected()
    {
        if(roamingSites!=null) foreach(var site in roamingSites)
        {
            if(site.point==null)continue;
            Gizmos.color=site.habitat==WorldMonsterHabitat.Ruins?new Color(.8f,.4f,.9f):new Color(.4f,.9f,.3f);
            Gizmos.DrawWireSphere(site.point.position+Vector3.up*.3f,.3f);
        }
        Gizmos.color=new Color(1,.6f,.15f);
        if(campSites!=null)foreach(var site in campSites)if(site!=null)Gizmos.DrawWireSphere(site.position+Vector3.up*.3f,.8f);
    }
}
