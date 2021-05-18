using System.Collections;
using NavMeshBuilder = UnityEngine.AI.NavMeshBuilder;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class AreaFloorBaker : MonoBehaviour
{
    [SerializeField]
    private NavMeshSurface[] Surfaces;
    [SerializeField]
    private Player Player;
    [SerializeField]
    private float UpdateRate = 0.1f;
    [SerializeField]
    private float MovementThreshold = 3f;
    [SerializeField]
    private Vector3 NavMeshSize = new Vector3(20, 20, 20);
    [SerializeField]
    private bool CacheSources;

    public delegate void NavMeshUpdatedEvent(Bounds Bounds);
    public NavMeshUpdatedEvent OnNavMeshUpdate;

    private Vector3 WorldAnchor;
    private NavMeshData[] NavMeshDatas;
    private Dictionary<int, List<NavMeshBuildSource>> SourcesPerSurface = new Dictionary<int, List<NavMeshBuildSource>>();
    private Dictionary<int, List<NavMeshBuildMarkup>> MarkupsPerSurface= new Dictionary<int , List<NavMeshBuildMarkup>>();
    private Dictionary<int, List<NavMeshModifier>> ModifiersPerSurface = new Dictionary<int, List<NavMeshModifier>>();

    private void Awake()
    {
        NavMeshDatas = new NavMeshData[Surfaces.Length];
        for (int i = 0; i < Surfaces.Length; i++)
        {
            NavMeshDatas[i] = new NavMeshData();
            NavMesh.AddNavMeshData(NavMeshDatas[i]);
            SourcesPerSurface.Add(i, new List<NavMeshBuildSource>());
            MarkupsPerSurface.Add(i, new List<NavMeshBuildMarkup>());
            ModifiersPerSurface.Add(i, new List<NavMeshModifier>());
        }

        BuildNavMesh(false);
        StartCoroutine(CheckPlayerMovement());
    }

    private IEnumerator CheckPlayerMovement()
    {
        WaitForSeconds Wait = new WaitForSeconds(UpdateRate);

        while (true)
        {
            if (Vector3.Distance(WorldAnchor, Player.transform.position) > MovementThreshold)
            {
                BuildNavMesh(true);
                WorldAnchor = Player.transform.position;
            }

            yield return Wait;
        }
    }

    private void BuildNavMesh(bool Async)
    {
        Bounds navMeshBounds = new Bounds(Player.transform.position, NavMeshSize);

        for (int index = 0; index < Surfaces.Length; index++)
        {
            if (MarkupsPerSurface[index].Count == 0)
            {
                if (Surfaces[index].collectObjects == CollectObjects.Children && ModifiersPerSurface[index].Count == 0)
                {
                    ModifiersPerSurface[index] = new List<NavMeshModifier>(GetComponentsInChildren<NavMeshModifier>());
                }
                else if (Surfaces[index].collectObjects != CollectObjects.Children)
                {
                    ModifiersPerSurface[index] = NavMeshModifier.activeModifiers;
                }

                for (int i = 0; i < ModifiersPerSurface[index].Count; i++)
                {
                    if (((Surfaces[index].layerMask & (1 << ModifiersPerSurface[index][i].gameObject.layer)) != 0)
                        && ModifiersPerSurface[index][i].AffectsAgentType(Surfaces[index].agentTypeID))
                    {
                        MarkupsPerSurface[index].Add(new NavMeshBuildMarkup()
                        {
                            root = ModifiersPerSurface[index][i].transform,
                            overrideArea = ModifiersPerSurface[index][i].overrideArea,
                            area = ModifiersPerSurface[index][i].area,
                            ignoreFromBuild = ModifiersPerSurface[index][i].ignoreFromBuild
                        });
                    }
                }
            }

            if (!CacheSources || SourcesPerSurface[index].Count == 0)
            {
                if (Surfaces[index].collectObjects == CollectObjects.Children)
                {
                    NavMeshBuilder.CollectSources(transform, Surfaces[index].layerMask, Surfaces[index].useGeometry, Surfaces[index].defaultArea, MarkupsPerSurface[index], SourcesPerSurface[index]);
                }
                else
                {
                    NavMeshBuilder.CollectSources(navMeshBounds, Surfaces[index].layerMask, Surfaces[index].useGeometry, Surfaces[index].defaultArea, MarkupsPerSurface[index], SourcesPerSurface[index]);
                }
            }

            // Ensure player and enemies are not on the layers considered for NavMesh and we don't need this!
            //Sources.RemoveAll(RemoveNavMeshAgentPredicate);

            if (Async)
            {
                AsyncOperation navMeshUpdateOperation = NavMeshBuilder.UpdateNavMeshDataAsync(NavMeshDatas[index], Surfaces[index].GetBuildSettings(), SourcesPerSurface[index], navMeshBounds);
                navMeshUpdateOperation.completed += HandleNavMeshUpdate;
            }
            else
            {
                NavMeshBuilder.UpdateNavMeshData(NavMeshDatas[index], Surfaces[index].GetBuildSettings(), SourcesPerSurface[index], navMeshBounds);
                OnNavMeshUpdate?.Invoke(navMeshBounds);
            }
        }
    }

    //private bool RemoveNavMeshAgentPredicate(NavMeshBuildSource Source)
    //{
    //    return Source.component != null && Source.component.gameObject.GetComponent<NavMeshAgent>() != null;
    //}

    private void HandleNavMeshUpdate(AsyncOperation Operation)
    {
        OnNavMeshUpdate?.Invoke(new Bounds(WorldAnchor, NavMeshSize));
    }
}