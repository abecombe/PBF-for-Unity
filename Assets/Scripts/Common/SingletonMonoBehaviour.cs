using UnityEngine;

public class SingletonMonoBehaviour<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    public static T Instance
    {
        get
        {
            if (_instance is not null) return _instance;
            _instance = (T)FindObjectOfType(typeof(T));
            if (_instance is null) { Debug.LogError(typeof(T) + " is nothing"); }
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (this == Instance) return;
        Destroy(this);
        Debug.LogFormat("Duplicate {0}", typeof(T).Name);
    }

    protected virtual void OnDestroy()
    {
        _instance = null;
    }
}