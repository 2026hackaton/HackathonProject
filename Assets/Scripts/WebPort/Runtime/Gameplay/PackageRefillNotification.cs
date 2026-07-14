using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Hackathon.WebPort
{
    public sealed class PackageRefillNotification : MonoBehaviour
    {
        [SerializeField] private RectTransform notificationPanel;
        [SerializeField] private Text messageText;
        [SerializeField] private string defaultMessage = "새로운 택배가 도착합니다!";
        [SerializeField] private Vector2 hiddenPosition = new(0f, 96f);
        [SerializeField] private Vector2 visiblePosition = new(0f, -36f);
        [SerializeField, Min(0.01f)] private float enterSeconds = 0.22f;
        [SerializeField, Min(0f)] private float holdSeconds = 1.25f;
        [SerializeField, Min(0.01f)] private float exitSeconds = 0.22f;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip notificationClip;

        private Coroutine _routine;

        private void Awake()
        {
            if (notificationPanel != null)
            {
                notificationPanel.anchoredPosition = hiddenPosition;
                notificationPanel.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        public void ShowNotification(string message = null)
        {
            if (notificationPanel == null)
            {
                Debug.LogError($"{nameof(PackageRefillNotification)} requires a notification panel.", this);
                return;
            }

            if (_routine != null)
                StopCoroutine(_routine);

            _routine = StartCoroutine(ShowRoutine(string.IsNullOrWhiteSpace(message) ? defaultMessage : message));
        }

        private IEnumerator ShowRoutine(string message)
        {
            if (messageText != null)
                messageText.text = message;

            if (audioSource != null && notificationClip != null)
                audioSource.PlayOneShot(notificationClip);

            notificationPanel.gameObject.SetActive(true);
            yield return MovePanel(hiddenPosition, visiblePosition, enterSeconds);
            if (holdSeconds > 0f)
                yield return new WaitForSeconds(holdSeconds);
            yield return MovePanel(visiblePosition, hiddenPosition, exitSeconds);
            notificationPanel.gameObject.SetActive(false);
            _routine = null;
        }

        private IEnumerator MovePanel(Vector2 from, Vector2 to, float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                t = 1f - Mathf.Pow(1f - t, 3f);
                notificationPanel.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                yield return null;
            }

            notificationPanel.anchoredPosition = to;
        }
    }
}
