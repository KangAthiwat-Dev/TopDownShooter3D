# ของดรอปจากมอนสเตอร์และระบบดูดของ

ฉาก `Assets/_Project/Scenes/WorldPrototype.unity`

## พฤติกรรม

- มอนสเตอร์ทั่วไปและบอสทั้ง 12 prefab มี `EnemyLootDropper` พร้อมตารางดรอปของตัวเอง
- ทุกตัวมีวัตถุดิบหลักที่ดรอปแน่นอน และบางตัวมีวัตถุดิบเสริมแบบสุ่ม
- เมื่อมอนสเตอร์ตาย ของจะเด้งออกจากตัว ลอยเหนือพื้น และหันไอคอนเข้ากล้อง
- เมื่อผู้เล่นเข้าใกล้ในระยะ **8 เมตร** ของจะเร่งความเร็วพร้อมเส้นแสงเข้าหาผู้เล่นโดยอัตโนมัติ ไม่ต้องกดเก็บหรือหยุดเดิน
- ตรวจสิ่งกีดขวาง layer Environment ก่อนดูด จึงไม่ดูดทะลุกำแพง
- เพิ่มไอเทมเมื่อของลอยถึงตัวจริง แล้วอัปเดต hotbar และหน้ากระเป๋าเดิมทันที
- ใช้ช่องวัตถุดิบ 8 ช่องเดิม แยกจากอาวุธประชิด 2 ช่อง ซ้อนได้ **99 ชิ้นต่อกอง**
- ถ้ากระเป๋าเต็ม ของจะรอบนพื้น เมื่อมีที่ว่างจึงดูดเข้าต่อ
- ถ้าเหลือที่ว่างเพียงบางส่วน จะเก็บเท่าที่ใส่ได้ และเก็บส่วนที่เหลือไว้ในโลก
- ของชนิดเดียวกันที่อยู่ใกล้กันบนพื้นจะรวมกอง ไม่เกิน MaxStack ของไอเทมนั้น
- เปิดกระเป๋า/หยุดเกมแล้ว การเด้ง ลอย ดูด และเพิ่มไอเทมจะหยุดตามเกม
- ของไม่ผูกเป็น child ของศพหรือรัง การลบศพหรือสุ่มรังใหม่จึงไม่ลบของที่ดรอปไว้
- ไม่มีการดรอปตอนลบมอนสเตอร์ที่ยังมีชีวิต หรือ unload ฉาก

## ไอคอนและไอเทม

ใช้รูปโปร่งใสจาก `Assets/Assets_Imported/free-rpg-hunting-game-512x512-icons/PNG/without background/`
ปรับ import เฉพาะ 8 รูปที่ใช้เป็น Sprite พร้อม alpha; ไฟล์ภาพต้นฉบับไม่ถูกแก้ไข

| Item ID | ชื่อในเกม | รูป |
|---|---|---|
| monster_bone | Monster Bone | 13.png |
| monster_fang | Monster Fang | 1.png |
| monster_claw | Monster Claw | 4.png |
| beast_hide | Beast Hide | 25.png |
| raw_meat | Raw Meat | 20.png |
| demon_horn | Demon Horn | 2.png |
| demon_wing | Demon Wing | 7.png |
| dragon_scale | Dragon Scale | 16.png |

ทั้งหมดเป็น Resource สำหรับระบบขาย/คราฟต์ในอนาคต เนื้อยังไม่ใช่อาหารที่กดใช้ได้
สถานะกระเป๋าและของบนพื้นอยู่ในรอบการเล่นปัจจุบัน ตามระบบกระเป๋าเดิม ยังไม่ได้เชื่อมกับเซฟ Hotel ข้ามการเริ่มฉากใหม่

## ตารางดรอปเริ่มต้น

จำนวนเป็นต่อการสังหารหนึ่งตัว รายการที่ไม่มีเปอร์เซ็นต์จะดรอปแน่นอน

| มอนสเตอร์ | ของหลัก | ของเสริม |
|---|---|---|
| Spider | Fang 1–2 | Claw 1 (30%) |
| Bear | Hide 1–2 | Meat 1–3 (65%), Claw 1 (35%) |
| Imp Green | Horn 1 | Wing 1 (20%) |
| Imp Blue | Horn 1–2 | Wing 1 (30%) |
| Imp Red | Horn 2 | Wing 1 (45%) |
| Puglin | Hide 1–2 | Claw 1–2 (50%) |
| Skeleton | Bone 1–3 | — |
| Goblin demon | Horn 1 | Bone 1–2 (35%), Claw 1 (25%) |
| Goblin chief | Horn 2–4 | Bone 2–3 (60%), Claw 1–2 (50%) |
| KingOfDemon | Horn 4–7, Wing 2–4, Bone 3–5 | — |
| DemonLord | Horn 3–5, Wing 1–3, Claw 2–4 | — |
| DragonNightmare | Scale 4–7, Claw 2–3 | Bone 2–4 (70%) |

## ปรับค่าใน Unity

- `World > Monster_Loot > Monster Loot World`: ระยะดูด, ความเร็ว, ความเร่ง, ระยะรวมกอง และ layer กำแพง/พื้น
- `Assets/_Project/Items/MonsterLoot/Tables/`: โอกาสและจำนวนดรอปของแต่ละชนิด (`Chance` ใช้ 0–1)
- `Assets/_Project/Items/MonsterLoot/Items/`: ชื่อ คำอธิบาย รูป และ MaxStack
- `Assets/_Project/Items/MonsterLoot/WorldLootPickup.prefab`: ขนาดภาพ วงแสง และเส้นแสงตอนดูด
- `EnemyLootDropper.Table` บน prefab หรือบอสในฉาก: เลือกตารางสำหรับมอนสเตอร์ใหม่

`EnemyBase` และ `EnemyDummy` ไม่ได้ติดตารางดรอป เพื่อไม่ให้เป้าทดสอบสร้างวัตถุดิบ

## จุดต่อโค้ด

- `EnemyLootDropper` สมัคร `EnemyHealth.Died` ทางเดียว จึงครอบคลุมทั้งมอนสเตอร์ที่เกิดจาก director และบอส
- อย่าสร้าง drop ซ้ำผ่าน `WorldMonsterDirector.MonsterDefeated`; event นั้นยังใช้ต่อ quest/สถิติได้
- `MonsterDropTable` เป็น ScriptableObject สำหรับสุ่มชนิดและจำนวน
- `MonsterLootWorld.SpawnItem(item, amount, position)` สร้างของโลกได้จาก chest/reward ในอนาคต
- `MonsterLootWorld.Collected` ส่งไอเทมและจำนวนที่เก็บสำเร็จ ใช้ต่อเสียงหรือข้อความแจ้งเตือนได้
- `LootPickup` จัดการการเด้ง พักบนพื้น และลอยเข้าตัว โดยไม่มี collider มารบกวนการเดินหรือกระสุน
- `PlayerInventory.SpaceFor(item)` ตรวจที่ว่างรวม ส่วน `TryAdd` ยังคงทำรายการแบบ atomic เหมือนเดิม
- EXP ยังมาจาก `EnemyHealth.TakeDamage(..., player)` ไม่เพิ่มซ้ำในระบบ loot

## การตรวจสอบ

`Tools > Enemies > Monster Loot > Validate Drops and Items`

`Tools > Enemies > Monster Loot > Run Death Magnet and Inventory Test`

ทดสอบผ่าน 37 รายการ: ตารางสุ่ม 12 แบบ แบบละ 100 ครั้ง, ฆ่า Skeleton จริง, ไม่ดรอปซ้ำ,
EXP ครั้งเดียว, ศพถูกลบแล้วของยังอยู่, ดูดและอัปเดต UI, pause, กระเป๋าเต็ม, เก็บบางส่วน,
ของหลายกองเข้าพร้อมกัน, รวมกอง, กำแพงกั้น และตารางบอสเดิม
ตัวทดสอบคืนค่า EXP และ input ของผู้เล่นหลังทดสอบ

ผลอยู่ที่ `Logs/MonsterLootPlaytest.txt`, `Logs/MonsterLootValidation.txt`
ภาพอยู่ที่ `Logs/MonsterLoot_OnGround.png`, `Logs/MonsterLoot_Inventory.png`
