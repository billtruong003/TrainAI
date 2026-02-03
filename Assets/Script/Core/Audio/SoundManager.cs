using UnityEngine;
using System.Collections.Generic;

public sealed class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }
    private readonly Dictionary<int, Stack<SoundPoolMember>> _pools = new Dictionary<int, Stack<SoundPoolMember>>();
    private GameObject _poolRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _poolRoot = new GameObject("AudioPool");
        DontDestroyOnLoad(_poolRoot);
    }

    public void PlaySound(AudioClip clip, Vector3 position, float volume = 1f, float pitchRandomness = 0f)
    {
        if (clip == null) return;
        SoundPoolMember member = GetMember();
        member.transform.position = position;
        float finalPitch = 1f + Random.Range(-pitchRandomness, pitchRandomness);
        member.Play(clip, volume, finalPitch);
    }

    private SoundPoolMember GetMember()
    {
        int key = 0;
        if (!_pools.ContainsKey(key))
        {
            _pools[key] = new Stack<SoundPoolMember>();
        }

        if (_pools[key].Count > 0)
        {
            return _pools[key].Pop();
        }

        GameObject go = new GameObject("SoundEmitter");
        go.transform.SetParent(_poolRoot.transform);
        var source = go.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = 50f;
        source.playOnAwake = false;
        
        var member = go.AddComponent<SoundPoolMember>();
        member.Initialize(this);
        return member;
    }

    public void ReturnToPool(SoundPoolMember member)
    {
        if(member == null) return;
        member.gameObject.SetActive(false);
        if (_pools.ContainsKey(0))
        {
            _pools[0].Push(member);
        }
    }
}