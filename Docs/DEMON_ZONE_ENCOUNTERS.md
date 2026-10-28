# ระบบพื้นที่จอมมาร

## พฤติกรรม

- `sample_zone`: Goblin / Skeleton / Goblin Chief รวม 5 ตัว ไม่มีมังกรหรือบอส
- ลาน Demon Lord: Demon Lord + มังกร 2 ตัว + มอนสเตอร์ 4 ตัว ต้องกำจัดครบจึงปลดทางไป Malachar
- ลาน King of Demon มี 3 รอบ: มอนสเตอร์ 8 ตัว → มังกร 2 ตัวพร้อมมอนสเตอร์ 4 ตัว → Malachar ตัวเดียว เว้นระหว่างรอบ 2.5 วินาที
- ไม่มีสัตว์ป่าในชุดศัตรูของรอบต่อสู้ ไม่มีการล็อกตามเลเวล แต่ความถึกและแรงโจมตีของพื้นที่นี้สูงกว่าป่ามาก
- ออกจากพื้นที่เกิน 5 วินาทีหรือตาย จะเริ่มการต่อสู้ที่ยังไม่จบใหม่ รวมถึงคืนชีวิตบอสและลบศัตรูของครั้งก่อน การลบ object โดยไม่เกิดเหตุการณ์ตายจะไม่นับว่าเคลียร์รอบ
- พื้นที่ที่เคลียร์แล้วและประตูที่เปิดแล้วจะคงสถานะจนออกจาก World scene; ยังไม่ได้เพิ่มสถานะเหล่านี้เข้า Hotel save

## ปรับตำแหน่งใน Hierarchy

หา `World > Map > Zone_Demon` ซึ่งแยกเป็น `01_Sample_Area`, `02_DemonLord_Area`, `03_King_Area` และ `00_Shared_Lava_And_Route` แล้ว แต่ละพื้นที่รวมพื้นใน Geometry, ของตกแต่งใน Scenery, arena, encounter และทางกั้นของพื้นที่ไว้ด้วยกัน การจัดกลุ่มรักษาตำแหน่ง/มุม/ขนาดที่ผู้ใช้วางไว้

- `01_Sample_Encounter`, `02_DemonLord_Encounter`, `03_King_Encounter`: ปรับ Waves, prefabs, HP และ EXP ใน Inspector
- ใต้ encounter มี `Wave_N_Spawn_Anchors`: ย้าย `Monster_N` / `Dragon_N` เพื่อเปลี่ยนจุดเกิด ต้องวางบน NavMesh และอยู่ใน arena เดียวกัน
- `03_King_Area > King_Entrance_Walls_And_Door`: รวมกำแพงและประตูจาก MedievalVillageKit ที่ผู้ใช้วางไว้แทนแนวหินเดิม กำแพงใน Permanent_Walls อยู่ถาวรและมี collider/การกั้น NavMesh ตลอด ส่วน `Door_Hinge_MOVE_THIS` เปิดออก 105 องศาหลังเคลียร์ Demon Lord พร้อมผู้พิทักษ์ทุกตัว ประตูไม่เปิดเมื่อฆ่าเฉพาะบอสแล้วมังกรหรือมอนสเตอร์ยังเหลืออยู่
- `01_Sample_Area > Sample_Exit_Stalagmites > Sinking_Stalagmites_MOVE_THESE`: หินงอกที่ผู้ใช้วางไว้ 4 กลุ่ม เริ่มต้นชนได้และกั้นทางออก เมื่อกำจัดมอนสเตอร์ sample ครบ จะจมลง 7 เมตรอย่างนุ่มนวลใน 2 วินาที แล้วซ่อนใต้พื้น การฆ่าเพียงบางตัวไม่เปิดทาง
- `DemonLord_Arena`, `KingOfDemon_Arena`, `Sample_Arena_Monsters_Only`: แก้ขนาด BoxCollider เพื่อกำหนดขอบเขตตรวจผู้เล่น
- `KingOfDemon` และ `DemonLord`: ตัวเดิมและขนาดเดิม ถูกพักไว้จนถึงรอบของตัวเอง จุดวางเริ่มต้นยังแก้ได้ใน Edit mode

ถ้าจะย้ายทั้งโซน ให้ย้าย parent `Zone_Demon` ที่รวมพื้น ลาวา ฉาก และ encounter อยู่ด้วยกัน แล้ว bake navigation ใหม่

## ประตูและหินงอก

`EncounterBarrier` บน root ของทางกั้นมี Prerequisite ระบุ encounter ที่ต้องชนะ, Moving Part ระบุเฉพาะส่วนที่ขยับ, Motion เลือก Swing/Slide, มุมเปิด/ระยะเลื่อน และ Duration ให้ปรับใน Inspector ได้ Collision และ NavMeshObstacle ของส่วนทางผ่านจะถูกปล่อยหลังเคลื่อนพ้นทางเกือบครบ (90%) กำแพงถาวรไม่ถูกปิด collider ตามประตู แอนิเมชันเปิดทางหยุดตามการพักเกม และเปิดค้างจนโหลด World scene ใหม่ตามระบบ encounter เดิม

กล่อง collision ขนาดใหญ่ที่เคยพาดทั้งลานจากแนวหินเดิมถูกนำออกแล้ว ใช้ collider ตามชิ้นกำแพงและบานประตูจริงแทน ทุกชิ้นใช้ชั้น Environment จึงกั้นได้ทั้งร่างปกติและ Shadow ใช้ NavMeshObstacle แบบ carving จึงเปิดเส้นทาง AI ได้โดยไม่ต้อง bake ใหม่ทุกครั้งที่เปิดประตู

ถ้าย้ายแนวประตู ให้ย้าย `King_Entrance_Walls_And_Door` ทั้งกลุ่ม เพื่อให้ Door_Hinge และ Closed_Door_Navigation ไปด้วยกัน หากย้ายเฉพาะบาน ต้องย้าย Closed_Door_Navigation ให้ตรงตำแหน่งปิดใหม่ด้วย ส่วนหินงอกย้ายทั้ง `Sample_Exit_Stalagmites` เพื่อพาการกั้น NavMesh ไปด้วยกัน

## ความยากเริ่มต้นของพื้นที่

| ศัตรู | HP | การโจมตี |
|---|---:|---|
| Skeleton | 220 | 20 |
| Goblin | 280 | 24 |
| Goblin Chief | 420 | 32 |
| Nightmare Dragon | 1,200 | ประมาณ 28–51 ตามท่า |
| Demon Lord | 3,200 | ประมาณ 32–95 ตามท่า |
| King of Demon | 6,000 | ฟาด 65 / ลูกไฟ 60 / คลื่น 70 |

ศัตรูทั่วไปใช้ prefab variants ใต้ `Assets/_Project/Prefabs/Enemies/DemonZone` จึงไม่เปลี่ยนค่าศัตรูป่าหรือภารกิจเดิม ดาบและระบบซื้อดาบยังคงค่าเดิมตามที่ตกลงไว้ รอปรับ damage ของดาบแต่ละเล่มภายหลัง

## การวาร์ปฟาด

King มีวงเตือนตำแหน่งลงพื้น 0.7 วินาทีก่อนวาร์ป เลือกตำแหน่งบน NavMesh ใน arena และตรวจสิ่งกีดขวางอีกครั้งก่อนย้ายตัว หลังวาร์ปจะเหวี่ยงตามตำแหน่งผู้เล่นที่จำไว้ ไม่หมุนไล่กลางท่า จังหวะกระทบมาจาก animation event เดิม ผู้เล่นใช้ Dash ออกนอกระยะ/มุมฟาดได้ ถ้าฟาดพลาดจะพักเพิ่ม 1.6 วินาที วาร์ปมี cooldown 7 วินาที

## หลอดเลือด

`Monster Overhead Health Bars` ใช้ Canvas กลางสำหรับมอนสเตอร์ทั่วไปและมังกร แถบไม่มีตัวเลขหรือข้อความ มีแถบความเสียหายตามหลัง และซ่อนเมื่ออยู่หลังสิ่งกีดขวาง นอกจอ ไกลเกินระยะ หรือตายแล้ว ใช้ HUD บอสด้านบนเฉพาะ Demon Lord และ King เท่านั้น

## ลาวา

`LavaDeathSurface` อยู่บน `Lava` และ `Lava_fall` ตรวจจากสามเหลี่ยมของ mesh น้ำลาวาจริง ไม่เปลี่ยน texture หรือเพิ่มพื้น collision ให้เดินบนลาวา เมื่อตำแหน่งเท้าต่ำถึงผิวลาวาจะตายทันที รวมถึงตอนมีเกราะ ยาป้องกัน หรือ Shadow Dash สะพานที่สูงกว่าผิวลาวายังเดินได้

## ตรวจซ้ำ

เมนู `Tools > Demon Zone > Test Stages Gate Blink Lava and Health Bars` ทดสอบใน Play mode และสำรอง/คืนค่าเซฟผู้เล่นเดิม ผลอยู่ที่ `Logs/DemonZonePlaytest.txt` และภาพอยู่ใต้ `Logs/DemonZoneTests` เมนู Install ใช้กับฉากที่ยังไม่ได้ติดตั้งเท่านั้น ป้องกันกดซ้ำแล้วทับตำแหน่งที่ผู้ใช้ปรับไว้

`Tools > Demon Barriers > Test User Door Sample Stalagmites and Collision` ทดสอบการชนของผู้เล่นจริงก่อน/หลังเปิด การชนของ Shadow การคงกำแพงไว้ เงื่อนไขกำจัดศัตรูครบ การเลื่อนหินงอก การพักเกม และเส้นทาง NavMesh ผลอยู่ `Logs/DemonBarrierPlaytest.txt` ภาพอยู่ `Logs/DemonBarrierTests` โดยสำรองและคืนเซฟผู้เล่นเดิมหลังทดสอบ
