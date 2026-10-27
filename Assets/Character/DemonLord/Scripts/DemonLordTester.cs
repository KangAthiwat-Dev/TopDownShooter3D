using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// สำหรับทดสอบเท่านั้น (ลบออกได้เมื่อมี AI จริง)
// ปุ่ม 1-7 ระหว่าง Play: 1 Attack, 2 Bolt, 3 Nova, 4 Curse, 5 Taunt, 6 Hit, 7 Die
public class DemonLordTester : MonoBehaviour
{
    [Tooltip("ติ๊กเพื่อให้เล่นท่าสกิลวนไปเรื่อยๆ (ไว้ดูเอฟเฟกต์)")] public bool autoCycle = false;
    public float cycleInterval = 4f;
    [Range(0.05f, 1f)] public float timeScale = 1f;

    Animator anim; float timer; int step;
    static readonly string[] Cycle = { "Attack", "Bolt", "Nova", "Curse", "Taunt", "Hit" };
    void Awake() { anim = GetComponent<Animator>(); }

    void Update()
    {
        Time.timeScale = timeScale;
        if (autoCycle && anim)
        {
            timer += Time.deltaTime;
            if (timer >= cycleInterval) { timer = 0; anim.SetTrigger(Cycle[step % Cycle.Length]); step++; }
        }
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current; if (k == null || anim == null) return;
        if (k.digit1Key.wasPressedThisFrame) anim.SetTrigger("Attack");
        if (k.digit2Key.wasPressedThisFrame) anim.SetTrigger("Bolt");
        if (k.digit3Key.wasPressedThisFrame) anim.SetTrigger("Nova");
        if (k.digit4Key.wasPressedThisFrame) anim.SetTrigger("Curse");
        if (k.digit5Key.wasPressedThisFrame) anim.SetTrigger("Taunt");
        if (k.digit6Key.wasPressedThisFrame) anim.SetTrigger("Hit");
        if (k.digit7Key.wasPressedThisFrame) anim.SetTrigger("Die");
#endif
    }
}
