using UnityEngine;

// ลบเอฟเฟกต์เองเมื่อหมดเวลา, ทำให้แสงค่อยๆ จาง และ (ถ้ามี follow) ตามตำแหน่งกระดูก/ตัวละครโดยไม่เป็นลูกของมัน
public class VFXAutoDestroy : MonoBehaviour
{
    public float lifetime = 2f;
    public bool fadeLights = true;
    [HideInInspector] public Transform follow;

    Light[] lights; float[] baseIntensity; float t;

    void Start()
    {
        lights = GetComponentsInChildren<Light>();
        baseIntensity = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++) baseIntensity[i] = lights[i].intensity;
        Destroy(gameObject, lifetime);
    }

    void LateUpdate()
    {
        if (follow) transform.position = follow.position;
        t += Time.deltaTime;
        if (fadeLights && lights != null)
        {
            float k = Mathf.Clamp01(1f - t / lifetime);
            for (int i = 0; i < lights.Length; i++) if (lights[i]) lights[i].intensity = baseIntensity[i] * k;
        }
    }
}
