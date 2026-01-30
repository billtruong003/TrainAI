using UnityEngine;

public class PoolMember : MonoBehaviour
{
    private int poolKey;
    private SmartPoolManager myPool;

    public void Init(int key, SmartPoolManager pool)
    {
        poolKey = key;
        myPool = pool;
    }

    public void ReturnToPool()
    {
        if (myPool != null)
            myPool.Return(this);
        else
            Destroy(gameObject);
    }

    public int Key => poolKey;
}