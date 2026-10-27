using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// สำหรับทดสอบเท่านั้น: กดปุ่ม 1-5 ระหว่าง Play เพื่อสั่งท่าของบอส (เอาออกได้เมื่อมี AI จริง)
// 1 = Attack, 2 = Magic (ลูกไฟ), 3 = Magic2 (คลื่นกระแทก), 4 = Power, 5 = Die
public class BossSkillTester : MonoBehaviour
{
    [Tooltip("ติ๊กเพื่อให้บอสเล่นท่า Attack -> Magic -> Magic2 -> Power วนไปเรื่อยๆ (ไว้ดูเอฟเฟกต์)")]
    public bool autoCycle = false;
    public float cycleInterval = 4f;
    [Range(0.05f, 1f)] public float timeScale = 1f;   // ลดลงเพื่อดูเอฟเฟกต์แบบสโลว์

    Animator anim; float timer; int step;
    static readonly string[] Cycle = { "Attack", "Magic", "Magic2", "Power" };
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
        if (k.digit2Key.wasPressedThisFrame) anim.SetTrigger("Magic");
        if (k.digit3Key.wasPressedThisFrame) anim.SetTrigger("Magic2");
        if (k.digit4Key.wasPressedThisFrame) anim.SetTrigger("Power");
        if (k.digit5Key.wasPressedThisFrame) anim.SetTrigger("Die");
#endif
    }
}
