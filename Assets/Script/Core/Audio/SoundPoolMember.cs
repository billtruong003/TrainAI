using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class SoundPoolMember : MonoBehaviour
{
    private AudioSource _source;
    private SoundManager _manager;

    public void Initialize(SoundManager manager)
    {
        _manager = manager;
        _source = GetComponent<AudioSource>();
    }

    public void Play(AudioClip clip, float volume, float pitch)
    {
        gameObject.SetActive(true);
        _source.clip = clip;
        _source.volume = volume;
        _source.pitch = pitch;
        _source.Play();
        StartCoroutine(ReturnRoutine());
    }

    private IEnumerator ReturnRoutine()
    {
        float delay = 0f;
        if (_source.clip != null)
        {
            delay = _source.clip.length / Mathf.Max(0.1f, Mathf.Abs(_source.pitch));
        }
        yield return new WaitForSeconds(delay + 0.1f);
        if (_manager != null)
        {
            _manager.ReturnToPool(this);
        }
    }
}