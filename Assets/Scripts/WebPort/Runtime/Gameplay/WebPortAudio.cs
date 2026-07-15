using UnityEngine;

namespace Hackathon.WebPort
{
    // 효과음은 지금 있는 이펙트 시스템(AddEffect)과 동일하게 재생을 트리거한 클라이언트
    // 로컬에서만 들린다 - 네트워크로 동기화하지 않는다. Assets/Resources/WebPort/Audio/
    // 밑에 있는 클립을 첫 재생 시 로드해서 캐싱한다.
    //
    // 2D(spatialBlend=0)로 고정 재생한다 - 카메라가 항상 플레이어에서 고정 오프셋만큼
    // (약 440유닛) 떨어진 3인칭이라, 3D 위치 기반(PlayClipAtPoint) 감쇠를 쓰면 리스너-소스
    // 거리가 Unity 기본 감쇠 범위(1~500유닛) 끝에 걸려서 소리가 항상 작거나 들쭉날쭉했다.
    public static class WebPortAudio
    {
        private const string AudioResourcePath = "WebPort/Audio/";

        private static AudioClip _pushOrGrabUse;
        private static AudioClip _impact;
        private static AudioClip _explosion;
        private static AudioClip _deliverNormal;
        private static AudioClip _deliverHigh;
        private static AudioClip _gravityPickup;
        private static AudioClip _throw;
        private static AudioClip _music;
        private static bool _loaded;
        private static AudioSource _source;
        private static AudioSource _musicSource;

        public static void PlayPushOrGrabUse()
        {
            EnsureLoaded();
            Play(_pushOrGrabUse);
        }

        public static void PlayImpact()
        {
            EnsureLoaded();
            Play(_impact);
        }

        public static void PlayExplosion()
        {
            EnsureLoaded();
            Play(_explosion);
        }

        public static void PlayDelivery(PackageKind kind)
        {
            EnsureLoaded();
            Play(kind == PackageKind.High ? _deliverHigh : _deliverNormal);
        }

        public static void PlayGravityPickup()
        {
            EnsureLoaded();
            Play(_gravityPickup);
        }

        public static void PlayThrow()
        {
            EnsureLoaded();
            Play(_throw);
        }

        // 세션 내내 계속 도는 배경음악 - 앱 시작 시 한 번 켜두면 됨(Awake에서 호출).
        public static void PlayBackgroundMusic()
        {
            EnsureLoaded();
            if (_music == null)
                return;

            if (_musicSource == null)
            {
                GameObject host = new("WebPort Music");
                Object.DontDestroyOnLoad(host);
                _musicSource = host.AddComponent<AudioSource>();
                _musicSource.spatialBlend = 0f;
                _musicSource.loop = true;
                _musicSource.volume = 0.4f;
                _musicSource.clip = _music;
            }

            if (!_musicSource.isPlaying)
                _musicSource.Play();
        }

        public static void StopBackgroundMusic()
        {
            if (_musicSource != null && _musicSource.isPlaying)
                _musicSource.Stop();
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;

            _loaded = true;
            _pushOrGrabUse = Resources.Load<AudioClip>(AudioResourcePath + "Punch-2");
            _impact = Resources.Load<AudioClip>(AudioResourcePath + "Punch-1");
            _explosion = Resources.Load<AudioClip>(AudioResourcePath + "Boom-box");
            _deliverNormal = Resources.Load<AudioClip>(AudioResourcePath + "PointSound");
            _deliverHigh = Resources.Load<AudioClip>(AudioResourcePath + "Gold-boxPointSound");
            _gravityPickup = Resources.Load<AudioClip>(AudioResourcePath + "Gravity-box");
            _throw = Resources.Load<AudioClip>(AudioResourcePath + "SFX- Swoosh16");
            _music = Resources.Load<AudioClip>(AudioResourcePath + "They Bop - Freedom Trail Studio");
        }

        private static void Play(AudioClip clip)
        {
            if (clip == null)
                return;

            // PlayOneShot이라 여러 효과음이 겹쳐도 서로 끊지 않고 같이 재생된다.
            EnsureSource().PlayOneShot(clip);
        }

        private static AudioSource EnsureSource()
        {
            if (_source != null)
                return _source;

            GameObject host = new("WebPort Audio");
            Object.DontDestroyOnLoad(host);
            _source = host.AddComponent<AudioSource>();
            _source.spatialBlend = 0f;
            _source.playOnAwake = false;
            return _source;
        }
    }
}
