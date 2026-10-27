using UnityEngine;

// ลบเอฟเฟกต์เองเมื่อหมดเวลา (lifetime <= 0 = อยู่ตลอด), แสงค่อยๆ จาง และตามตำแหน่ง follow โดยไม่เป็นลูกของมัน
public class DLVFXAutoDestroy : MonoBehaviour
{
    public float lifetime = 2f;
    public bool fadeLights = true;
    [HideInInspector] public Transform follow;

    Light[] lights; float[] baseI; float t;

    void Start()
    {
        lights = GetComponentsInChildren<Light>();
        baseI = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++) baseI[i] = lights[i].intensity;
        if (lifetime > 0) Destroy(gameObject, lifetime);
    }

    void LateUpdate()
    {
        if (follow) transform.position = follow.position;
        if (lifetime <= 0 || !fadeLights) return;
        t += Time.deltaTime;
        float k = Mathf.Clamp01(1f - t / lifetime);
        for (int i = 0; i < lights.Length; i++) if (lights[i]) lights[i].intensity = baseI[i] * k;
    }
}
