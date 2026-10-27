# KingOfDemon: AI และหลอดเลือดบอส

ติดตั้งแล้วใน `Assets/_Project/Scenes/WorldPrototype.unity` บน KingOfDemon ที่ผู้ใช้วางไว้ โดยคงตำแหน่งและการหมุนเดิม

ขนาดล่าสุดปรับให้สูง **3 เท่าของ player**: player ประมาณ 1.845 m, จอมมาร 5.534 m (วัดจากขอบเขตโมเดลที่แสดง ไม่รวมปืน) บันทึกทั้งในฉากและ gameplay prefab แล้ว ส่วนชน จุดเล็ง และ NavMeshAgent ตามขนาดใหม่ โดยคงเลือด ดาเมจ ความเร็ว cooldown และขนาดเอฟเฟกต์/พื้นที่คลื่นกระแทกเดิม รายงานอยู่ใน `Logs/KingOfDemonSize.txt`

## พฤติกรรม

- บอสเลือดเริ่มต้น **1200 HP** รออยู่บนลาน ไม่รับดาเมจและไม่ถูกเลือกเป็นเป้าหมายนอก encounter
- เมื่อ player ที่ยังมีชีวิตเข้าลาน `KingOfDemon_Arena` บอสจะเริ่มไล่ ใช้ NavMeshAgent ความเร็ว 3.5 m/s และส่งความเร็วจริงเข้า Idle/Walk/Run blend tree
- เลือกโจมตีจากระยะและ cooldown: ประชิดประมาณ 4.1 m (เพิ่มตามรัศมีตัวที่ใหญ่ขึ้น), shockwave 6.5 m, ลูกไฟ 17 m มีช่วงเปิดสู้ 0.85 s และพักหลังสกิล 0.65 s ให้หลบได้
- ดาเมจตรงกับ Animation Events เดิม: Slash 18 ต่อจังหวะ (สองจังหวะต่อท่า), Fireball 25, Shockwave 20
- ตัวละครหยุดเดินขณะใช้สกิล ล็อกทิศการฟาดหลังเริ่มท่า และล็อกแนวยิงก่อนปล่อยลูกไฟ
- การตีต้องอยู่ในมุมด้านหน้า ระยะ ความสูง และไม่มีวัตถุทึบขวาง ลูกไฟใช้ sphere sweep กันทะลุกำแพงบาง ส่วน shockwave เช็กช่วงที่วงคลื่นกวาดผ่าน ความสูง และกำแพง
- ออกจากลานแล้วบอสหยุดสู้ ยกเลิกเอฟเฟกต์ที่ยังทำดาเมจ กลับจุดเดิม และฟื้นเลือดเต็ม จากนั้นจึงเริ่มรอบใหม่ได้ หาก player ตายเกมยังหยุดผ่าน GamePauseController ตามเดิม
- บอสตายครั้งเดียว เล่น Death และ DeathBurst ปิด hitbox/การเลือกเป้า และไม่เกิดใหม่เพียงเพราะเข้าออกลาน
- อาวุธประชิดยังฆ่ามอนสเตอร์ทั่วไปทีเดียวตามเดิม แต่ใช้ค่าดาเมจปกติกับบอส ผ่าน `EnemyHealth.AllowInstantKill = false`
- `Targetable.closeRangeOffset` ชดเชยรัศมีตัวบอสที่เพิ่มขึ้น เพื่อให้ player เข้าสู่โหมดประชิดและตีได้โดยไม่ต้องเดินทะลุ collider จอมมาร ตัวทั่วไปใช้ค่า 0 จึงตรวจระยะแบบเดิม

## HUD

`HUDCanvas/.../Boss Encounter HUD` อยู่ด้านบนกลาง ใต้ป้ายชื่อพื้นที่ มีชื่อ **KING OF DEMON** และแถบเลือดสีแดงพร้อมแถบเหลืองตามรอยดาเมจ ไม่มีตัวเลขเลือด

แสดงเมื่อเข้าลานด้วย fade 0.3 s และซ่อนเมื่อออกจากลาน เมื่อบอสตายจะแสดงเลือดศูนย์ประมาณ 2 s แล้วค่อยซ่อน HUD ไม่รับเมาส์และไม่ขวางปุ่ม/ช่อง inventory เดิม

## จุดที่ปรับได้ใน Inspector

| จุด | ค่าที่ปรับ |
|---|---|
| `KingOfDemon > EnemyHealth` | Max Health, Allow Instant Kill |
| `KingOfDemon > KingOfDemonAI` | ชื่อบอส, ความเร็ว, ระยะ, ดาเมจประชิด, cooldown, ช่วงพัก |
| `KingOfDemon > BossVFXController` | prefab เอฟเฟกต์, ความเร็ว/ดาเมจลูกไฟ, รัศมี/ดาเมจคลื่น |
| `KingOfDemon_Arena > BoxCollider` | ขนาด/ตำแหน่งลาน เป็น trigger และตรวจ player โดยตรง จึงรองรับ PlayerShadow ด้วย |
| `Boss Encounter HUD > BossHealthUI` | อ้างอิงบอส, fill/trail/name, ความเร็ว fade |

ลานปัจจุบันอิงแพลตฟอร์มที่สาม ขนาด 25 × 8 × 35 m และหมุนตามแพลตฟอร์ม การตรวจขอบมีระยะเผื่อออก 0.65 m เพื่อไม่ให้หลอดกระพริบขณะยืนที่ขอบ

เมนู `Tools > Enemies > Set DemonLord 2x and KingOfDemon 3x` ใช้เทียบขนาดใหม่กับ player ได้ โดยชดเชย Effect Scale Multiplier ของ KingOfDemon เพื่อไม่ให้การขยายตัวทำให้รัศมีดาเมจและ collider ลูกไฟใหญ่ตามโดยอัตโนมัติ DemonLord ใช้สัดส่วน 2 เท่า (ประมาณ 3.690 m) และมี encounter แยกพร้อม DragonNightmare แล้ว ดู `Docs/DEMON_LORD_DRAGON_ENCOUNTER.md`

## ไฟล์และ prefab

- `Assets/_Project/Scripts/Enemies/KingOfDemonAI.cs`: state, การเคลื่อนที่, การเลือกสกิล, hit ประชิด, กลับจุดและตาย
- `Assets/_Project/Scripts/Enemies/BossArenaZone.cs`: ตรวจพื้นที่ encounter
- `Assets/_Project/Scripts/UI/BossHealthUI.cs`: หลอดเลือดบอสบน HUD
- `Assets/_Project/Prefabs/Enemies/KingOfDemon_Boss.prefab`: gameplay prefab ที่บันทึกจากตัวในฉาก หากวางในฉากใหม่ให้ผูก Arena และ HUD เข้ากับ instance ใหม่ด้วย
- สคริปต์ VFX เดิมเพิ่มการเชื่อมจังหวะโจมตีและยกเลิกสกิล โดยยังใช้แอนิเมชัน วัสดุ และ VFX prefab ต้นฉบับ
- `BossSkillTester` ถูกปิดในตัวใช้งานจริง ไม่เขียนทับ Time.timeScale หรือปุ่มเกม

เมนูติดตั้ง: `Tools > Enemies > King Of Demon > Install Boss Encounter and HUD` ใช้สำหรับติดตั้งครั้งแรก ไม่จำเป็นต้องรันซ้ำหลังปรับค่าบอส เพราะเมนูจะตั้งค่าเริ่มต้นอีกครั้ง สำรองฉากก่อนติดตั้งไว้ใน `Logs/SceneBackups`

## การทดสอบ

เรียกซ้ำได้ที่ `Tools > Enemies > King Of Demon > Run Boss Combat and HUD Test` โดยผลอยู่ใน `Logs/KingOfDemonCombatTest.txt`

ทดสอบผ่านทั้งหมด: นอก/ในพื้นที่, auto-aim และกระสุนจากปืนจริง, HUD ลดตามเลือด, pause inventory, Animation Events ครบสามสกิลและดาเมจถึง PlayerHealth, กลับจุด/ฟื้นเลือด, ลูกไฟความเร็วสูงชนกำแพงบาง, คลื่นไม่ตีข้ามชั้น/กำแพงและทำดาเมจครั้งเดียว, เข้าใหม่, ตายครั้งเดียวพร้อมท่า/เอฟเฟกต์, HUD เลือดศูนย์แล้วซ่อน, ไม่เกิดใหม่หลังตาย และ game-over pause โดยไม่มี runtime error

ตัวทดสอบเพิ่มเลือด player ชั่วคราวเป็น 1000 เพื่ออยู่ดูสกิลครบ โดยไม่เปลี่ยนดาเมจบอส ปิด ordinary spawners เฉพาะตอนทดสอบ และออก Play Mode อัตโนมัติ ค่าทดสอบไม่ถูกบันทึกกลับฉาก

ตอนนี้เป็นบอสหนึ่งเฟส ใช้ Attack, Magic และ Magic2 ส่วน Power/Aura ที่มีอยู่ยังเตรียมไว้สำหรับเพิ่มเฟสในรอบถัดไป ยังไม่ได้เพิ่มเสียงบอสใหม่
