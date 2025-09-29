using UnityEngine;

public static class LanHarnessBootstrap
{
    static bool created;

    public static void InitIfNeeded()
    {
        if (created) return;
        created = true;

        // Si tu HUD LAN ya existe (por escena), solo reactívalo
        var existing = Object.FindObjectOfType<QuickJoinLocalUI>(true);
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            return;
        }

        // Si no existe, créalo por código
        var go = new GameObject("LAN_QuickJoin_UI");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<QuickJoinLocalUI>(); // <--- si tu script se llama distinto, cámbialo aquí
    }
}
