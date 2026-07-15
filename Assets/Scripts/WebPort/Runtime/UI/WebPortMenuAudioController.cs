using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hackathon.WebPort
{
    [DisallowMultipleComponent]
    public sealed class WebPortMenuAudioController : MonoBehaviour
    {
        private const string MasterVolumeKey = "WebPort.Audio.MasterVolume";
        private const string MusicVolumeKey = "WebPort.Audio.MusicVolume";
        private const string SfxVolumeKey = "WebPort.Audio.SfxVolume";

        [Header("Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Clips")]
        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField] private AudioClip buttonHoverClip;
        [SerializeField] private AudioClip buttonClickClip;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = 0.85f;

        private float _masterVolume = 1f;
        private float _musicVolume = 0.55f;
        private float _sfxVolume = 0.85f;

        public void Configure(AudioClip music, AudioClip hover, AudioClip click)
        {
            backgroundMusic = music;
            buttonHoverClip = hover;
            buttonClickClip = click;
            EnsureSources();
            LoadSavedVolumes();
            PlayMusicIfNeeded();
        }

        public void RegisterButton(Button button)
        {
            if (button == null)
                return;

            WebPortMenuButtonAudioFeedback feedback = button.GetComponent<WebPortMenuButtonAudioFeedback>();
            if (feedback == null)
                feedback = button.gameObject.AddComponent<WebPortMenuButtonAudioFeedback>();

            feedback.Configure(this, button);
        }

        public void SetMasterVolume(float value)
        {
            _masterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterVolumeKey, _masterVolume);
            ApplyVolumes();
        }

        public void SetMusicVolume(float value)
        {
            _musicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, _musicVolume);
            ApplyVolumes();
        }

        public void SetSfxVolume(float value)
        {
            _sfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, _sfxVolume);
            ApplyVolumes();
        }

        public void PlayHover()
        {
            PlaySfx(buttonHoverClip);
        }

        public void PlayClick()
        {
            PlaySfx(buttonClickClip);
        }

        private void Awake()
        {
            EnsureSources();
            LoadSavedVolumes();
            PlayMusicIfNeeded();
        }

        private void EnsureSources()
        {
            if (musicSource == null)
                musicSource = CreateSource("Menu Music Source", true);

            if (sfxSource == null)
                sfxSource = CreateSource("Menu SFX Source", false);

            ConfigureSource(musicSource, true);
            ConfigureSource(sfxSource, false);
            ApplyVolumes();
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            GameObject obj = new(sourceName);
            obj.transform.SetParent(transform, false);
            AudioSource source = obj.AddComponent<AudioSource>();
            ConfigureSource(source, loop);
            return source;
        }

        private void ConfigureSource(AudioSource source, bool loop)
        {
            if (source == null)
                return;

            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.ignoreListenerPause = true;
        }

        private void LoadSavedVolumes()
        {
            _masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, defaultMasterVolume);
            _musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, defaultMusicVolume);
            _sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, defaultSfxVolume);
            ApplyVolumes();
        }

        private void ApplyVolumes()
        {
            if (musicSource != null)
                musicSource.volume = _masterVolume * _musicVolume;

            if (sfxSource != null)
                sfxSource.volume = _masterVolume * _sfxVolume;
        }

        private void PlayMusicIfNeeded()
        {
            if (musicSource == null || backgroundMusic == null)
                return;

            if (musicSource.clip != backgroundMusic)
                musicSource.clip = backgroundMusic;

            if (!musicSource.isPlaying)
                musicSource.Play();
        }

        private void PlaySfx(AudioClip clip)
        {
            if (sfxSource == null || clip == null)
                return;

            sfxSource.PlayOneShot(clip, _masterVolume * _sfxVolume);
        }
    }

    [DisallowMultipleComponent]
    public sealed class WebPortMenuButtonAudioFeedback : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
    {
        private WebPortMenuAudioController _audio;
        private Button _button;

        public void Configure(WebPortMenuAudioController audio, Button button)
        {
            _audio = audio;
            _button = button;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button == null || _button.interactable)
                _audio?.PlayHover();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button == null || _button.interactable)
                _audio?.PlayClick();
        }
    }
}
