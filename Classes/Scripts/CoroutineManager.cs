using System.Collections;
using UnityEngine;

public class CoroutineManager : MonoBehaviour
{
    private static CoroutineManager instance;

    private static CoroutineManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("CoroutineManager");
                Object.DontDestroyOnLoad(obj);
                instance = obj.AddComponent<CoroutineManager>();
            }
            return instance;
        }
    }

    public static Coroutine RunCoroutine(IEnumerator routine)
    {
        return Instance.StartCoroutine(routine);
    }

    public static void EndCoroutine(Coroutine routine)
    {
        if (routine != null && instance != null)
            instance.StopCoroutine(routine);
    }
}