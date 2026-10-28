using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Owns only its arena's combatants. Leaving resets an unfinished attempt.</summary>
[DefaultExecutionOrder(50)]
public sealed class DemonArenaEncounter : MonoBehaviour
{
    [Serializable] public sealed class Spawn
    {
        public GameObject prefab;
        public Transform anchor;
        [Min(1)] public float health = 250;
        [Min(0)] public int experience = 120;
    }
    [Serializable] public sealed class Wave
    {
        public string title;
        public Spawn[] members;
    }
    [SerializeField] private BossArenaZone arena;
    [SerializeField] private DemonArenaEncounter prerequisite;
    [SerializeField] private GameObject boss;
    [SerializeField] private int bossWave = -1;
    [SerializeField] private Wave[] waves;
    [SerializeField, Min(0)] private float waveDelay = 2.5f;
    [SerializeField, Min(.1f)] private float abandonDelay = 5;
    private sealed class Member { public EnemyHealth health; public bool died, owned; public Action callback; }
    private readonly List<Member> members = new List<Member>();
    private float nextWaveAt, absentSince = -1;
    private bool waiting, started;
    public bool Completed { get; private set; }
    public int WaveIndex { get; private set; } = -1;
    public int LivingCount { get { int n=0; foreach(var m in members) if(!m.died) n++; return n; } }
    public BossArenaZone Arena => arena;
    public IReadOnlyList<Wave> Waves => waves;
    public bool Unlocked => prerequisite == null || prerequisite.Completed;
    public string CurrentTitle => WaveIndex >= 0 && WaveIndex < waves.Length ? waves[WaveIndex].title : "";
    public event Action Changed;

    private void Start() { started = true; if (boss != null) boss.SetActive(false); }
    private void Update()
    {
        if (!started || Completed || Time.timeScale <= 0 || arena == null) return;
        bool present = arena.PlayerInside && Unlocked;
        if (!present)
        {
            if (WaveIndex < 0) return;
            if (absentSince < 0) absentSince = Time.time;
            if (arena.Player == null || !arena.Player.IsAlive || Time.time - absentSince >= abandonDelay) ResetAttempt();
            return;
        }
        absentSince = -1;
        if (WaveIndex < 0) { BeginWave(0); return; }
        // A despawn is not a kill and cannot unlock the next stage.
        foreach (var m in members) if (!m.died && m.health == null) { ResetAttempt(); return; }
        if (!waiting && LivingCount == 0) { waiting = true; nextWaveAt = Time.time + waveDelay; Changed?.Invoke(); }
        if (waiting && Time.time >= nextWaveAt)
        {
            if (WaveIndex + 1 < waves.Length) BeginWave(WaveIndex + 1);
            else { Completed = true; waiting = false; Changed?.Invoke(); }
        }
    }
    private void BeginWave(int index)
    {
        ClearMembers(); WaveIndex = index; waiting = false;
        var wave = waves[index];
        foreach (var spawn in wave.members)
        {
            if (spawn.prefab == null || spawn.anchor == null ||
                !NavMesh.SamplePosition(spawn.anchor.position, out var hit, 1.5f, NavMesh.AllAreas) ||
                Mathf.Abs(hit.position.y-spawn.anchor.position.y)>1.5f || !arena.Contains(hit.position))
            {
                Debug.LogError("Invalid Demon encounter spawn: " + name + " / " + spawn.anchor, this);
                ResetAttempt(); return;
            }
            var actor = Instantiate(spawn.prefab, hit.position, spawn.anchor.rotation, transform);
            actor.name = spawn.anchor.name + "_Combatant";
            var health = actor.GetComponent<EnemyHealth>();
            if (health == null) { Destroy(actor); ResetAttempt(); return; }
            health.ConfigureEncounter(spawn.health, spawn.experience);
            var combat = actor.GetComponent<EncounterCombatant>();
            if (combat != null) combat.ConfigureArena(arena, false);
            var ai = actor.GetComponent<EnemyAI>();
            if (ai != null) ai.JoinEncounter(arena, arena.Player.transform);
            Watch(health, true);
        }
        if (boss != null && index == bossWave) { boss.SetActive(true); Watch(boss.GetComponent<EnemyHealth>(), false); }
        Changed?.Invoke();
    }
    private void Watch(EnemyHealth health, bool owned)
    {
        var member = new Member {health=health, owned=owned};
        member.callback = () => { member.died = true; Changed?.Invoke(); };
        health.Died += member.callback; members.Add(member);
    }
    private void ClearMembers()
    {
        foreach (var m in members)
        {
            if (m.health == null) continue;
            m.health.Died -= m.callback;
            if (m.owned) Destroy(m.health.gameObject);
        }
        members.Clear();
    }
    public void ResetAttempt()
    {
        if (Completed) return;
        ClearMembers(); WaveIndex = -1; waiting = false; absentSince = -1;
        if (boss != null)
        {
            // Initial scene actors have already awakened before Start disables them.
            boss.SetActive(true);
            boss.GetComponent<KingOfDemonAI>()?.ResetEncounter();
            boss.GetComponent<EncounterCombatant>()?.ResetEncounter();
            boss.SetActive(false);
        }
        Changed?.Invoke();
    }
    private void OnDisable() { ClearMembers(); if (boss != null && !Completed) boss.SetActive(false); }
}
