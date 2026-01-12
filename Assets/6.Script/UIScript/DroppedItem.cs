using UnityEngine;

public class DroppedItem : MonoBehaviour
{
    [Header("ScriptableObject 데이터")]
    public ItemType itemType;
    public ItemDefinition itemData;
    public int count = 1;
    public int durability = -1;

    [Header("Optional visual")]
    public SpriteRenderer spriteRenderer; // 2D Sprite 용

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

        }
    }
    private void Start()
    {
        if (itemData != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = itemData.icon; // 아이콘 적용
            spriteRenderer.enabled = true;          // 혹시 꺼져 있으면 켜기
        }
    }
    private void Update()
    {
        if (Camera.main != null)
        {
            // 스프라이트가 카메라를 향하도록 회전
            transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                             Camera.main.transform.rotation * Vector3.up);
        }
    }

    // 추가: 자동으로 픽업, 아이템 아이콘 표시 등 구현 가능
    //테스트용 추가
    private void OnTriggerEnter(Collider other)
    {
        // 플레이어와 충돌 시
        if (other.CompareTag("Player"))
        {

            Pickup();
        }

    }

    public void Pickup()
    {
        //if (itemData == null) return;

        var stack = new ItemStack(itemData, count, durability);
        InventoryManager.Instance.AddItem(stack);

        // 아이템 삭제
        Destroy(gameObject);
    }
}