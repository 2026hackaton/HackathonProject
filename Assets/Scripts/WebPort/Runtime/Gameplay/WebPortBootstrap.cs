using UnityEngine;

namespace Hackathon.WebPort
{
    public static class WebPortBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGameManager()
        {
            if (Object.FindAnyObjectByType<WebPortGameManager>() != null)
                return;

            GameObject root = new("WebVersion Port");
            root.AddComponent<WebPortGameManager>();
        }
    }
}
