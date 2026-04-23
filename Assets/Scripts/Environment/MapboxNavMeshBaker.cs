using System.Collections;
using Mapbox.Unity.Map;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class MapboxNavMeshBaker : MonoBehaviour
{
    private const float NavMeshSearchRadius = 2000.0f;
    private const float BakeDelaySeconds = 3.0f;  // マップ生成完了後、追加で待つ時間

    [SerializeField] private AbstractMap map;
    [SerializeField] private NavMeshSurface navMeshSurface;
    [SerializeField] private GameObject[] navMeshAgents;

    private Vector3[] originalPositions;

    private void Awake()
    {
        if (navMeshAgents != null)
        {
            originalPositions = new Vector3[navMeshAgents.Length];
            for (int i = 0; i < navMeshAgents.Length; i++)
            {
                if (navMeshAgents[i] == null) continue;
                originalPositions[i] = navMeshAgents[i].transform.position;

                NavMeshAgent navAgent = navMeshAgents[i].GetComponent<NavMeshAgent>();
                if (navAgent != null)
                {
                    navAgent.enabled = false;
                }
            }
        }
    }

    private void Start()
    {
        if (map == null)
        {
            Debug.LogWarning($"[{nameof(MapboxNavMeshBaker)}] AbstractMap reference is not assigned.", this);
            return;
        }

        map.OnInitialized += HandleMapInitialized;
    }

    private void OnDestroy()
    {
        if (map != null)
        {
            map.OnInitialized -= HandleMapInitialized;
        }
    }

    private void HandleMapInitialized()
    {
        StartCoroutine(BakeAfterDelay());
    }

    private IEnumerator BakeAfterDelay()
    {
        Debug.Log($"[{nameof(MapboxNavMeshBaker)}] Map initialized. Waiting {BakeDelaySeconds} seconds for all tiles to load...");
        yield return new WaitForSeconds(BakeDelaySeconds);

        if (navMeshSurface == null)
        {
            Debug.LogWarning($"[{nameof(MapboxNavMeshBaker)}] NavMeshSurface reference is not assigned.", this);
            yield break;
        }

        navMeshSurface.BuildNavMesh();
        Debug.Log($"[{nameof(MapboxNavMeshBaker)}] NavMesh baked.");

        // NavMesh の範囲を確認
        var triangulation = NavMesh.CalculateTriangulation();
        Debug.Log($"[{nameof(MapboxNavMeshBaker)}] NavMesh vertex count: {triangulation.vertices.Length}, triangle count: {triangulation.indices.Length / 3}");

        if (navMeshAgents == null) yield break;

        for (int i = 0; i < navMeshAgents.Length; i++)
        {
            GameObject agent = navMeshAgents[i];
            if (agent == null) continue;

            Vector3 originalPos = originalPositions[i];

            if (NavMesh.SamplePosition(originalPos, out NavMeshHit hit, NavMeshSearchRadius, NavMesh.AllAreas))
            {
                agent.transform.position = hit.position;
                Debug.Log($"[{nameof(MapboxNavMeshBaker)}] Placed {agent.name} on NavMesh at {hit.position}.");

                NavMeshAgent navAgent = agent.GetComponent<NavMeshAgent>();
                if (navAgent != null)
                {
                    navAgent.enabled = true;
                    navAgent.Warp(hit.position);
                }
            }
            else
            {
                Debug.LogError(
                    $"[{nameof(MapboxNavMeshBaker)}] NavMesh not found near {agent.name} at {originalPos}. " +
                    "Check map position or increase search radius.",
                    this);
            }
        }
    }
}