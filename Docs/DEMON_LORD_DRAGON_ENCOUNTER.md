# DemonLord และ DragonNightmare

ติดตั้งใน `Assets/_Project/Scenes/WorldPrototype.unity` แล้ว ใช้ตำแหน่งและขนาด DemonLord ที่ผู้ใช้จัดไว้เดิม และเพิ่มมังกรสีแดงบนแพลตฟอร์มเดียวกัน

| ตัวละคร | เลือด | การโจมตี |
|---|---:|---|
| DemonLord | 900 | ฟาดสองจังหวะ, Blood Bolt, Blood Nova, Curse Circle |
| DragonNightmare | 650 | Bite, Claw Combo สองจังหวะ, Horn Strike |

## พฤติกรรม

- ทั้งสองตัวเดินลาดตระเวนใกล้จุดเกิด แม้ยังไม่มี player ในลาน
- เมื่อ player ที่ยังมีชีวิตเข้าพื้นที่ `DemonLord_Arena` ทั้งคู่เปิดด้วยท่าคำราม/ยั่วยุ จากนั้นเดินไล่และเลือกโจมตีตามระยะกับ cooldown
- การเคลื่อนที่ใช้ NavMeshAgent มี Idle/Walk/Run blend tree และความเร็ววิ่งจริง: DemonLord 3.5 m/s, มังกร 4.8 m/s
- ขณะใช้สกิลจะหยุดเดิน มีช่วงตั้งท่าและพักหลังโจมตีให้หลบได้ ดาเมจฟาด/กัด/ตะปบ/เขาเกิดตาม contact frame ในแอนิเมชัน
- การโจมตีประชิดเช็กระยะ มุมด้านหน้า ความสูง และสิ่งกีดขวาง
- Blood Bolt มีการเลี้ยวตามเล็กน้อยตาม asset เดิม และใช้ sphere sweep ป้องกันทะลุวัตถุบาง ไม่ชนเจ้าของหรือศัตรูฝ่ายเดียวกัน
- Blood Nova เช็กช่วงที่วงคลื่นกวาดผ่าน ไม่ตีข้ามชั้นหรือกำแพง และทำดาเมจหนึ่งครั้งต่อสกิล
- Curse Circle ปักตำแหน่งขณะปล่อย เตือน 1.2 s ก่อนระเบิด จึงวิ่งหรือ dash ออกจากวงได้ ตรวจความสูงและกำแพงก่อนทำดาเมจ
- ออกจากลานแล้วทั้งคู่หยุดสู้ ยกเลิกสกิลที่ยังทำดาเมจ กลับจุดเกิด และฟื้นเลือดเต็ม หากยังมีชีวิต
- เมื่อตายจะปิดการเลือกเป้า/ส่วนชน เล่นท่าตาย และไม่เกิดใหม่เพียงเพราะเข้าออกลาน ตัวที่รอดยังสู้ต่อได้
- อาวุธประชิดใช้ดาเมจตามปกติกับสองตัวนี้ ไม่ฆ่าทีเดียวแบบมอนสเตอร์ทั่วไป
- เปิด inventory แล้ว AI แอนิเมชัน และเวลาของสกิลหยุดตาม GamePauseController เดิม

## HUD

หลอดเลือด **DEMON LORD** และ **NIGHTMARE DRAGON** ปรากฏด้านบนกลางจอเมื่อเข้าลาน แยกเป็นคนละแถว ไม่มีตัวเลขเลือด แถบแดงคือเลือดปัจจุบัน และแถบเหลืองตามหลังการเสียเลือด

เมื่อออกจากลานหลอดจะค่อย ๆ ซ่อน เมื่อตัวใดตายจะแสดงเลือดศูนย์ช่วงสั้น ๆ แล้วซ่อนเฉพาะหลอดของตัวนั้น แถวที่เหลือขยับขึ้นอัตโนมัติ ระบบรองรับหลอด KingOfDemon เดิมด้วย

## ตำแหน่งและการปรับค่า

- `DemonLord_Arena`: จุดอ้างอิงประมาณ (47.94, 9.75, 26.96), BoxCollider ขนาด 24 × 8 × 20 m หมุนตามแพลตฟอร์มที่สอง ปรับขอบเขตได้ใน Inspector
- DemonLord คงตำแหน่ง (41.04, 9.75, 25.77) และขนาด 2 เท่าของ player
- DragonNightmare เริ่มที่ประมาณ (55.14, 9.83, 26.58) อยู่ในลานเดียวกัน มี gameplay root แยกจาก visual เพื่อให้ NavMesh กับส่วนชนไม่เคลื่อนไปตามกระดูกแอนิเมชัน
- ทั้งสองตัวใช้ `EncounterCombatant` ซึ่งอ้างอิง Arena เดียวกัน ปรับความเร็ว ระยะลาดตระเวน ความสูงสายตา และ Skills ใน Inspector ได้
- Skills แต่ละรายการกำหนด animation state, ชนิด, ระยะต่ำสุด/สูงสุด, ดาเมจ, cooldown, recovery และช่วงเวลาที่หันตามเป้าหมายได้
- `EnemyHealth` กำหนดเลือด ส่วน `Targetable` ชดเชยระยะประชิดตามขนาดตัว
- `DemonLordVFX` กำหนดเอฟเฟกต์ ความเร็วลูกเลือด รัศมี Nova/Curse และเวลาก่อน Curse ระเบิด

ดาเมจเริ่มต้น: DemonLord ฟาด 12 ต่อจังหวะ, Bolt 20, Nova 25, Curse 35; มังกรกัด 14, ตะปบ 12 ต่อจังหวะ, เขา 22

## ไฟล์หลัก

- `Assets/_Project/Scripts/Enemies/EncounterCombatant.cs`: AI ร่วม การเคลื่อนที่ สกิล การกลับจุด และการตาย
- `Assets/_Project/Scripts/Enemies/EncounterAnimationEvents.cs`: รับ contact frame ของ Generic dragon และส่งไป gameplay root
- `Assets/_Project/Scripts/UI/BossHealthUI.cs`: อ่านเลือดจาก KingOfDemon หรือ EncounterCombatant และจัดแถวหลอดอัตโนมัติ
- `Assets/_Project/Prefabs/Enemies/DemonLord_Boss.prefab`
- `Assets/_Project/Prefabs/Enemies/DragonNightmare_Guardian.prefab`
- `Assets/_Project/World/DemonLordEncounter/DragonNightmare_Combat.controller`
- `Assets/_Project/World/DemonLordEncounter/DragonClips/`: สำเนาคลิป Generic ที่ตั้ง attack/roar/death ไม่ให้วน และเพิ่ม contact events โดยไม่แก้ FBX ต้นฉบับ
- `Assets/_Project/World/DemonLordEncounter/DragonNightmare_Red_URP.mat`: วัสดุส่วนตัวที่เชื่อม albedo, normal, metallic/smoothness และ AO ของ asset เดิมเข้ากับ URP

โมเดล คลิป FBX texture และ material ต้นฉบับของ FourEvilDragonsPBR ยังคงเดิม DemonLord ใช้ controller และคลิปที่ผู้ใช้เตรียมไว้ พร้อมเชื่อม/ปรับสคริปต์ VFX ให้ทำงานกับ AI การหยุดเกม และการยกเลิกสกิล

หากนำ gameplay prefab ไปวางฉากใหม่ ให้กำหนด `EncounterCombatant.arena` และอ้างอิง actor ใน `BossHealthUI.encounterBoss` ใหม่ด้วย ห้ามเปิด DemonLordTester ในตัวใช้งานจริง เพราะตัวทดสอบนั้นเขียน Time.timeScale และรับปุ่ม 1–7 เอง

## การตรวจสอบ

- ผลติดตั้ง/สำรองฉาก: `Logs/DemonLordEncounterBuild.txt` และ `Logs/SceneBackups/WorldPrototype_BeforeDemonLordEncounter_*.unity`
- ผลทดสอบ: `Logs/DemonLordDragonCombatTest.txt`
- ภาพเกม: `Logs/DemonLordDragon_Combat.png`, `Logs/DemonLordDragon_Defeated.png`
- เรียกทดสอบซ้ำ: `Tools > Enemies > Demon Lord > Run DemonLord and Dragon Combat Test`

ทดสอบผ่านการลาดตระเวนก่อนเข้าเขต การเข้า/ออกลานพร้อมกัน กระสุนปืนจริงทำดาเมจทั้งคู่ สกิล DemonLord ครบสี่แบบ มังกรครบสามท่าและทำดาเมจจริง การหยุด inventory การยกเลิกสกิล กลับบ้าน/ฟื้นเลือด ตายแยกกัน คลิปและเอฟเฟกต์ตาย การซ่อน HUD เฉพาะตัวที่ตาย และไม่เกิดใหม่หลังตาย

ทดสอบเพิ่มเติมว่าลูกเลือดไม่ทะลุกำแพงบาง ทำดาเมจครั้งเดียว Curse รอช่วงเตือนและหยุดเวลากับ inventory ได้ หลบออกจาก Curse ได้ และ Nova ไม่ทำดาเมจข้ามชั้น โดยไม่มี runtime error

ตัวทดสอบเพิ่มเลือด player ชั่วคราวเป็น 2000 เพื่อดูสกิลครบ และปิด ordinary spawners กับ KingOfDemon เฉพาะช่วงทดสอบ ค่าทดสอบไม่ถูกบันทึกกลับฉาก
