using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10000)]
public class EventSystemGuard : MonoBehaviour
{
    private void Awake()
    {
        CleanupDuplicates();
        EnsureOneActive();
    }

    private void Start()
    {
        // На случай, если additive-сцена догрузила ES после нашего Awake
        CleanupDuplicates();
        EnsureOneActive();
    }

public static void CleanupDuplicates()
    {
        var systems = FindObjectsOfType<EventSystem>(true);
        if (systems == null || systems.Length <= 1)
            return;

        EventSystem keep = null;
        foreach (var es in systems)
        {
            if (es == null) continue;
            if (es.gameObject.scene.name == "MainScene")
            {
                keep = es;
                break;
            }
        }

        if (keep == null)
        {
            // Предпочитаем уже активный
            foreach (var es in systems)
            {
                if (es != null && es.isActiveAndEnabled)
                {
                    keep = es;
                    break;
                }
            }
        }

        if (keep == null)
            keep = systems[0];

        foreach (var es in systems)
        {
            if (es == null || es == keep)
                continue;

            // Сразу выключаем, чтобы не плодить warning, затем destroy
            es.enabled = false;
            es.gameObject.SetActive(false);
            Destroy(es.gameObject);
        }
    }

public static void EnsureOneActive()
    {
        var systems = FindObjectsOfType<EventSystem>(true);
        if (systems == null || systems.Length == 0)
            return;

        foreach (var es in systems)
        {
            if (es != null && es.isActiveAndEnabled)
                return;
        }

        // Ни одного активного — активируем первый (или из GameScene)
        EventSystem toEnable = systems[0];
        foreach (var es in systems)
        {
            if (es != null && es.gameObject.scene.name == "GameScene")
            {
                toEnable = es;
                break;
            }
        }

        if (toEnable != null)
        {
            toEnable.gameObject.SetActive(true);
            toEnable.enabled = true;
        }
    }
}
