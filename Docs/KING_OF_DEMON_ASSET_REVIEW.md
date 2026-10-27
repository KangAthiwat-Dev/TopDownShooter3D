# ตรวจ KingOfDemon สำหรับทำจอมมาร

**อัปเดต:** ต่อ AI, ลานต่อสู้, ระบบรับดาเมจ/ตาย และ HUD บอสในฉากแล้ว ดู `Docs/KING_OF_DEMON_ENCOUNTER.md` ส่วนรายงานด้านล่างเป็นผลตรวจ asset ต้นฉบับก่อนต่อระบบ Gameplay prefab ใหม่คือ `Assets/_Project/Prefabs/Enemies/KingOfDemon_Boss.prefab`

ใช้เป็นโมเดลและชุดแอนิเมชันของบอสได้ ขณะนี้เป็น prefab สำหรับดูและทดสอบสกิล ยังไม่ได้เชื่อมเป็นศัตรูที่ต่อสู้ครบระบบในเกม

ตำแหน่ง: `Assets/Character/KingOfDemon/KingOfDemon.prefab`

## สิ่งที่ตรวจแล้ว

- Avatar เป็น Humanoid ที่ valid; ทดสอบเล่นคลิปบนโมเดลในฉาก preview แยกแล้วทั้ง 8 ท่า ขยับกระดูกได้ ไม่มีค่าตำแหน่งผิดปกติ
- โมเดลใช้ URP/Lit และ texture อ้างอิงครบ ส่วนวัสดุเอฟเฟกต์ใช้ URP/Particles/Unlit ที่รองรับ
- ไม่มี missing script บนตัวละครและ prefab เอฟเฟกต์ทั้ง 6 ตัว
- Animator มี Speed blend tree: Idle = 0, Walk = 1.5, Run = 3.5 และ Trigger: Attack, Magic, Magic2, Power, Die
- Animation Events ทุกจุดมีเมธอดรับบน BossVFXController และ prefab เอฟเฟกต์ถูกใส่ครบ
- โมเดลมี 1 SkinnedMeshRenderer, 28,641 vertices และ 9,998 triangles เหมาะนำมาลองเป็นบอสหนึ่งตัวก่อน แต่ยังไม่ได้วัดต้นทุนเอฟเฟกต์ขณะเล่นจริง
- Prefab ปรับ scale ประมาณ 2.02; ขนาด bounds ตอน idle ประมาณ 2.75 × 4.50 × 1.98 เมตร ควรใช้ขนาดนี้ตั้งพื้นที่ต่อสู้และ hitbox แล้วตรวจเทียบ player อีกครั้ง
- ไม่มี AudioSource หรือเสียงที่ผูกกับตัวบอส

| ท่า | ความยาว | การวน / เอฟเฟกต์ |
|---|---:|---|
| Idle | 3.183 s | Loop |
| Walk | 1.433 s | Loop |
| Run | 1.833 s | Loop |
| Attack | 2.200 s | Slash ที่ 0.682 และ 1.694 s |
| Magic | 2.700 s | Charge 0.405 s, Fireball 1.323 s |
| Magic2 | 3.317 s | Charge 0.398 s, Shockwave 1.194 s |
| Power | 2.367 s | Aura 0.284 s |
| Death | 3.667 s | DeathBurst 2.493 s |

## สิ่งที่ต้องต่อก่อนสู้จริง

1. **AI บอส:** ตรวจ player, เดินไล่/กลับจุด, หันเข้าหาเป้าหมาย, เลือกสกิลตามระยะ, คูลดาวน์และช่วงหยุดให้หลบ ต้องมีตัวควบคุมการเคลื่อนที่/พื้นชนที่เหมาะกับบอส
2. **เลือดและการถูกโจมตี:** Prefab ยังไม่มี health, collider หรือระบบตายที่เข้ากับเกม ต้องต่อ `IDamageable`, ระบบเลือกเป้าหมาย และป้องกันการสั่งสกิลหลังตาย
3. **ดาเมจแต่ละสกิล:** `VFX_Slash` สร้างเอฟเฟกต์เท่านั้น ยังไม่มี hitbox หรือดาเมจประชิด ส่วนลูกไฟและ shockwave มีโค้ดส่ง `TakeDamage(float)` ซึ่งตรงกับเมธอดของ PlayerHealth แต่ยังไม่ได้ทดสอบการต่อสู้จริง
4. **พื้นที่และการชน:** Shockwave คำนวณระยะบนระนาบ XZ โดยไม่เช็กความสูงหรือกำแพง ส่วนลูกไฟขยับ transform และรอ OnTriggerEnter ควรเช็กการชนระหว่างเฟรมเมื่อทำสกิลจริง
5. **เสียงและการสื่อสารก่อนโจมตี:** ใส่เสียงคำราม/เท้า/สกิลและสัญญาณเตือนให้สัมพันธ์กับ Animation Events

ต้องปิดหรือถอด `BossSkillTester` จาก prefab ที่ใช้สู้จริง: ปัจจุบันมันรับปุ่ม 1–5 และเขียน `Time.timeScale` ทุก Update แม้ไม่ได้เปิด autoCycle จึงชนกับระบบที่ปรับเวลาในเกมได้ ให้แยกไว้เฉพาะตัวทดสอบสกิล

เริ่มต่อเป็นบอสหนึ่งเฟสด้วย Attack, Fireball และ Shockwave ได้ก่อน จากนั้นค่อยเพิ่ม Power เป็นเฟสสองตามเลือดที่เหลือ

## หลักฐานและขอบเขต

- รายงานตรวจ: `Logs/KingOfDemonAssetReview.txt` — 0 inspection failures
- ภาพ preview: `Logs/KingOfDemon_Preview.png`
- เรียกตรวจซ้ำ: `Tools > Assets > King Of Demon > Inspect Model Animations and Effects`
- การตรวจครั้งนี้ใช้ prefab clone ใน preview scene ไม่แก้โมเดล แอนิเมชัน วัสดุ หรือค่าบอสในฉากหลัก และไม่ได้อ้างว่า AI/ดาเมจผ่านการทดสอบแล้ว

## งานทางขึ้นและสะพานที่เสร็จในรอบเดียวกัน

แก้ collider เกาะให้ตรงกับโมเดล เชื่อมรอยต่อบันไดผ่านช่องต่ำข้างสันหิน และใส่ texture พร้อมพื้นชนให้สะพานแล้ว การทดสอบด้วย player จริงผ่านทั้งทางขึ้น การเดินข้ามสองทิศ วิ่ง dash ปกติ และ shadow dash รายละเอียดอยู่ใน `Docs/CASTLE_ROUTE_WALKABILITY.md`
