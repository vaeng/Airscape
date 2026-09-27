using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioClip menuBGM;
    [SerializeField] private AudioClip gameplayBGM;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void OnEnable()
    {
        GameManager.OnGameStarted += PlayGameplayBGM;
    }

    private void Start()
    {
        if (GameManager.Instance != null &&
            GameManager.Instance.CurrentState.Value == GameState.Playing)
            PlayGameplayBGM();
        else
            PlayMenuBGM();
    }

    private void OnDisable()
    {
        GameManager.OnGameStarted -= PlayGameplayBGM;
    }

    public void PlayMenuBGM()
    {
        PlayBGM(menuBGM);
    }

    public void PlayGameplayBGM()
    {
        PlayBGM(gameplayBGM);
    }

    private void PlayBGM(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        if (audioSource.clip == clip && audioSource.isPlaying)
            return;

        audioSource.clip = clip;
        audioSource.Play();
    }
}
