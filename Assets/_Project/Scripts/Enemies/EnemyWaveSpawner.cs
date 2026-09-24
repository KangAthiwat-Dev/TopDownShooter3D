using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[Serializable]
public class EnemySpawnEntry
{
    public GameObject enemyPrefab;

    [Min(1)]
    public int count = 1;
}

[Serializable]
public class EnemyWave
{
    public EnemySpawnEntry[] enemies;

    [Min(0f)]
    public float delayAfterWave = 3f;
}

public class EnemyWaveSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform[] spawnPoints;

    [Header("Waves")]
    [SerializeField] private EnemyWave[] waves;
    [SerializeField] private float firstWaveDelay = 2f;
    [SerializeField] private float spawnInterval = 0.5f;

    [Header("Spawn Rules")]
    [SerializeField] private float minimumPlayerDistance = 7f;
    [SerializeField] private float navMeshSampleRadius = 2f;

    public int CurrentWaveNumber => currentWaveIndex + 1;
    public int AliveEnemies => aliveEnemies;

    public event Action<int> WaveStarted;
    public event Action<int> EnemyCountChanged;
    public event Action AllWavesCompleted;

    private int currentWaveIndex = -1;
    private int aliveEnemies;

    private void Awake()
    {
        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }
    }

    private void Start()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("EnemyWaveSpawner has no spawn points.");
            return;
        }

        if (waves == null || waves.Length == 0)
        {
            Debug.LogError("EnemyWaveSpawner has no waves.");
            return;
        }

        StartCoroutine(RunWaves());
    }

    private IEnumerator RunWaves()
    {
        yield return new WaitForSeconds(firstWaveDelay);

        for (int waveIndex = 0;
             waveIndex < waves.Length;
             waveIndex++)
        {
            currentWaveIndex = waveIndex;

            Debug.Log($"Wave {CurrentWaveNumber} started.");
            WaveStarted?.Invoke(CurrentWaveNumber);

            EnemyWave wave = waves[waveIndex];

            foreach (EnemySpawnEntry entry in wave.enemies)
            {
                if (entry.enemyPrefab == null)
                {
                    continue;
                }

                for (int i = 0; i < entry.count; i++)
                {
                    SpawnEnemy(entry.enemyPrefab);

                    yield return new WaitForSeconds(
                        spawnInterval
                    );
                }
            }

            // รอจน Enemy ใน Wave นี้ตายหมด
            yield return new WaitUntil(
                () => aliveEnemies <= 0
            );

            Debug.Log($"Wave {CurrentWaveNumber} cleared.");

            if (waveIndex < waves.Length - 1)
            {
                yield return new WaitForSeconds(
                    wave.delayAfterWave
                );
            }
        }

        Debug.Log("All waves completed.");
        AllWavesCompleted?.Invoke();
    }

    private void SpawnEnemy(GameObject enemyPrefab)
    {
        if (!TryGetSpawnPosition(out Vector3 spawnPosition))
        {
            Debug.LogWarning(
                $"Could not find NavMesh position for " +
                $"{enemyPrefab.name}."
            );

            return;
        }

        GameObject enemy = Instantiate(
            enemyPrefab,
            spawnPosition,
            Quaternion.identity
        );

        EnemyHealth health =
            enemy.GetComponent<EnemyHealth>();

        if (health == null)
        {
            Debug.LogError(
                $"{enemyPrefab.name} has no EnemyHealth."
            );

            Destroy(enemy);
            return;
        }

        RegisterEnemy(health);
    }

    private void RegisterEnemy(EnemyHealth health)
    {
        aliveEnemies++;
        EnemyCountChanged?.Invoke(aliveEnemies);

        Action deathHandler = null;

        deathHandler = () =>
        {
            health.Died -= deathHandler;

            aliveEnemies = Mathf.Max(
                aliveEnemies - 1,
                0
            );

            EnemyCountChanged?.Invoke(aliveEnemies);
        };

        health.Died += deathHandler;
    }

    private bool TryGetSpawnPosition(
        out Vector3 spawnPosition
    )
    {
        const int maximumAttempts = 12;

        for (int attempt = 0;
             attempt < maximumAttempts;
             attempt++)
        {
            Transform spawnPoint = spawnPoints[
                UnityEngine.Random.Range(
                    0,
                    spawnPoints.Length
                )
            ];

            if (spawnPoint == null)
            {
                continue;
            }

            if (player != null)
            {
                float distanceToPlayer = Vector3.Distance(
                    spawnPoint.position,
                    player.position
                );

                if (distanceToPlayer < minimumPlayerDistance)
                {
                    continue;
                }
            }

            if (NavMesh.SamplePosition(
                spawnPoint.position,
                out NavMeshHit hit,
                navMeshSampleRadius,
                NavMesh.AllAreas))
            {
                spawnPosition = hit.position;
                return true;
            }
        }

        spawnPosition = Vector3.zero;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;

        foreach (Transform spawnPoint in spawnPoints)
        {
            if (spawnPoint != null)
            {
                Gizmos.DrawWireSphere(
                    spawnPoint.position,
                    0.5f
                );
            }
        }
    }
}