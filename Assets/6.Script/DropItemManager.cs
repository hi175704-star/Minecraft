using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class DropItemManager : MonoBehaviour
{
    public static DropItemManager Instance;

    [System.Serializable]
    public struct DropPrefabEntry
    {
        public ItemType itemType;
        public GameObject prefab; // Inspector에서 드래그
    }

    [Header("드롭 아이템 프리팹")]
    public DropPrefabEntry[] dropPrefabs;

    private Dictionary<ItemType, string> prefabNames = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 프리팹 이름 자동 등록
        foreach (var entry in dropPrefabs)
        {
            if (entry.prefab == null)
            {
                Debug.LogWarning($"[DropItemManager] 프리팹이 비어있습니다 ({entry.itemType})");
                continue;
            }

            string name = entry.prefab.name;
            prefabNames[entry.itemType] = name;

            // Resources에 있는지 체크
            if (Resources.Load(name) == null)
            {
                Debug.LogWarning($"[DropItemManager] 프리팹 '{name}' 이 Resources 폴더에 없습니다! Photon instantiate 실패함.");
            }
        }
    }

    /// <summary>
    /// 아이템 드롭 생성 (네트워크 공유)
    /// </summary>
    public GameObject SpawnDropItem(ItemType type, Vector3 worldPos)
    {
        if (!prefabNames.TryGetValue(type, out string prefabName))
        {
            Debug.LogWarning($"[DropItemManager] 등록된 프리팹 이름 없음 (ItemType: {type})");
            return null;
        }

        if (Resources.Load(prefabName) == null)
        {
            Debug.LogError($"[DropItemManager] Resources에서 프리팹 '{prefabName}' 를 찾을 수 없음.");
            return null;
        }

        // 🔥 Photon Instantiate로 네트워크 전체에 드롭 아이템 생성
        GameObject obj = PhotonNetwork.Instantiate(prefabName, worldPos, Quaternion.identity);

        return obj;
    }
}
