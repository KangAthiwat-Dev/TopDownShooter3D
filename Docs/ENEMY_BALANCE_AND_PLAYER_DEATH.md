# สมดุลมอนสเตอร์และท่าตายผู้เล่น

อัปเดตฉาก `Assets/_Project/Scenes/WorldPrototype.unity` ก่อนนำงานเข้า Git

## แนวทางสมดุล

ปืนเริ่มต้นดาเมจ 8 ยิงชุดละ 3 นัด ระยะห่าง 0.2 วินาที และพักชุด 0.8 วินาที
ผู้เล่น Run ปกติที่ 5 เมตร/วินาที จึงควรหนีมอนสเตอร์ทั่วไปได้เมื่อเลือกถอย

พบค่าที่ทำให้ไล่ผู้เล่นเร็วผิดกลุ่ม: Imp Blue มี NavMeshAgent speed 20 และ Puglin speed 8
กำหนด Chase Speed ใน AI และ Agent ให้ตรงกัน พร้อมลด HP ให้สอดคล้องกับปืนเริ่มต้น
แต่ละชนิดยังคงรูปลักษณ์ แอนิเมชัน FOV การจดจำ/ค้นหา การเดินลาดตระเวน EXP และตารางดรอปเดิม

| ชนิด | HP เดิม → ใหม่ | ความเร็วไล่เดิม → ใหม่ | ดาเมจ | พักโจมตี | นัดที่ใช้ฆ่าด้วยปืนเริ่มต้น |
|---|---:|---:|---:|---:|---:|
| Spider | 80 → 32 | 4.6 → 3.6 | 6 | 1.8 วินาที | 4 |
| Imp Green | 120 → 40 | 4.2 → 3.2 | 6 | 1.5 วินาที | 5 |
| Imp Blue | 150 → 56 | 20 → 3.8 | 8 | 1.6 วินาที | 7 |
| Imp Red | 200 → 72 | 3.3 → 3.3 | 10 | 1.75 วินาที | 9 |
| Puglin | 300 → 96 | 8 → 2.8 | 12 | 2 วินาที | 12 |
| Bear | 280 → 120 | 4 → 3.2 | 14 | 2.2 วินาที | 15 |
| Skeleton | 150 → 48 | 3.8 → 2.9 | 8 | 1.7 วินาที | 6 |
| Goblin demon | 170 → 64 | 4 → 3.4 | 8 | 1.8 วินาที | 8 |
| Goblin chief | 320 → 112 | 4 → 3.2 | 12 | 2 วินาที | 14 |

ความเร็วลาดตระเวนอยู่ที่ 1.1–1.4 เมตร/วินาทีตามชนิด
จังหวะโดนของแอนิเมชันยังเป็นค่าเดิม ปรับเฉพาะช่วงพักระหว่างการโจมตี
เวลาฆ่าเมื่อยิงโดนครบต่อเนื่องอยู่ประมาณ 1.2–5.2 วินาที ไม่รวมเวลาเล็งและระยะเดินทางของกระสุน

KingOfDemon, DemonLord และ DragonNightmare ยังคง HP 1200, 900, 650 และระบบสกิล/arena/ขนาดเดิม
อาวุธประชิดยังฆ่ามอนสเตอร์ทั่วไปทีเดียวตามระบบก่อนหน้า

ปรับต่อได้ที่ prefab ใต้ `Assets/_Project/Prefabs/Enemies/`:

- `EnemyHealth.maxHealth`: เลือด
- `EnemyAI.chaseSpeed` และ `NavMeshAgent.speed`: ความเร็วไล่ตาม
- `EnemyAI.patrolSpeed`: ความเร็วลาดตระเวน
- `EnemyAttack.damage`, `attackCooldown`: ความแรงและช่วงพักโจมตี

## ท่าตายผู้เล่น

ใช้ UAL1 `Death01` แบบ Humanoid ที่มีอยู่แล้ว สร้างสำเนาเฉพาะผู้เล่นเป็น
`Assets/_Project/Animations/Player/Player_Death.anim` ความยาว 2.4 วินาที

ลำดับเมื่อ HP หมด:

1. ระบบ Game Over หยุดโลกและปิดกระเป๋า/หน้าอัปเกรดทันที
2. ยกเลิกโจมตี เก็บของ ปืนที่รอยิง และเอฟเฟกต์ Dash/แปลงร่าง
3. ปิดท่าถืออาวุธ แล้วเล่น Death ทั้งตัวครั้งเดียวด้วย Unscaled Time จึงล้มได้แม้ Time.timeScale เป็น 0
4. พักท่าล้มสุดท้ายอีก 0.15 วินาที แล้วแสดงหน้า Game Over และเลือกปุ่ม Restart
5. ค้างท่าศพจนเริ่มเกมใหม่ ไม่มี transition กลับไป Idle หรือ Locomotion

การ Restart โหลดฉากใหม่ คืน Animator เป็นเวลาปกติและคืนการเดิน/โจมตี/เอฟเฟกต์เดิม
เลเวลและอัปเกรดยังคงใช้เซฟเดิม

จุดต่อโค้ด:

- `PlayerDeathPresentation` บน Player เป็นผู้ประสานการตายและคืนรูปลักษณ์จาก Dash
- `PlayerAnimationController.PlayDeath()` เลือกสถานะ `Base Layer.Death` และกันท่าอื่นแทรก
- `PlayerWeaponVisual` ซ่อนอาวุธเมื่อผู้เล่นตาย
- `GameOverUI` ยัง pause ทันที แต่รอท่าตายก่อนแสดงหน้าต่าง โดยใช้เวลาแบบ real time
- ฉากที่ไม่มี PlayerDeathPresentation ยังคงแสดง Game Over ทันทีตามเดิม

## การตรวจสอบ

เมนู `Tools > Combat Balance > Run Balance Death and Restart Test`

ผ่าน 54 รายการ ครอบคลุม prefab ทั้ง 9 ชนิด, จำนวนกระสุนที่ฆ่าจริง, EXP/loot ไม่ซ้ำ,
ความเร็วไล่ของ Blue Imp จากตำแหน่งที่เคลื่อนจริง, การตีของ Skeleton,
ตายขณะเปิดกระเป๋า, การล้มขณะโลกหยุด, ท่าศพใกล้พื้น, UI focus และ Restart ฉากจริง
ตัวทดสอบคืนค่าเลเวลและอัปเกรดเดิมของผู้ใช้หลังจบ

- ผล: `Logs/EnemyBalanceDeathPlaytest.txt`
- ค่าเดิม: `Logs/EnemyBalanceAudit.txt`
- รายละเอียดที่ปรับ: `Logs/EnemyBalanceAndDeathInstallation.txt`
- ภาพ: `Logs/Player_Death_Falling.png`, `Logs/Player_Death_Final.png`
- สำรอง prefab, scene และ controller ก่อนแก้: `Logs/SceneBackups/EnemyBalance_*/`
