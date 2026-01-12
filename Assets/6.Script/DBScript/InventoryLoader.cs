using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class InventoryItemData
{
    public int player_id;
    public string item_name;
    public int count=0;
    public int durability=0;
    public int slot_index=0;
    public int use_count;
}

[System.Serializable]
public class InventoryMoveData
{
    public int player_id;
    public int fromSlot;
    public int toSlot;
    public string item_name;
    public int count;
    public int remainingCount;
}

[System.Serializable]
public class InventoryItemDataList
{
    public List<InventoryItemData> items;
}


public class InventoryLoader : MonoBehaviour
{

    public int playerId = 1; // 임시 플레이어 ID
    public InventoryManager inventoryManager;
    public InventoryUI inventoryUI;

    //private void Start()
    //{
    //    LoadInventory();
    //}
    private IEnumerator Start()
    {
        // HotbarUI.Instance와 hotbarButtons 생성 완료까지 기다림
        yield return new WaitUntil(() =>
            HotbarUI.Instance != null &&
            HotbarUI.Instance.inventoryData != null &&
            HotbarUI.Instance.inventoryData.SlotCount >= 36);

        LoadInventory();
    }
    public void LoadInventory()
    {
        inventoryUI.ClearInventoryUI();
        StartCoroutine(LoadInventoryCoroutine());
    }

    private IEnumerator LoadInventoryCoroutine()
    {
        string url = $"https://minehub.co.kr/inventory/{playerId}";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"❌ 인벤토리 로드 실패: {request.error}");
                yield break;
            }

            string json = request.downloadHandler.text;
            Debug.Log($"✅ 인벤토리 로드 성공: {json}");

            // 서버에서 가져온 JSON을 파싱
            List<InventoryItemData> items = JsonHelper.FromJson<InventoryItemData>(json).ToList();

            ApplyServerInventory(items);
        }
    }
    private void ApplyServerInventory(List<InventoryItemData> items)
    {
        var invData = inventoryManager.inventoryData;

        // 1️⃣ 기존 슬롯 초기화
        invData.Init(invData.SlotCount);

        // 2️⃣ 서버 데이터 적용
        foreach (var item in items)
        {
            var def = ItemDatabase.GetItemByName(item.item_name);
            if (def == null) continue;

            // DB slot_index 사용
            int index = item.slot_index;
            if (index < 0 || index >= invData.SlotCount)
                index = FindEmptySlot(invData); // 안전하게 빈 슬롯 찾기

            ItemStack stack = new ItemStack(def, item.count, item.durability, index);
            invData.SetSlot(index, stack);
        }

        // 3️⃣ UI 갱신
        inventoryManager.inventoryUI.Refresh();
        inventoryManager.hotbarUI.Refresh();
    }


    private int FindEmptySlot(InventoryData invData)
    {
        for (int i = 0; i < invData.SlotCount; i++)
            if (invData.GetSlot(i).IsEmpty)
                return i;
        return -1;
    }
}


