# มอนสเตอร์บนแผนที่และรัง Goblin

ฉาก: `Assets/_Project/Scenes/WorldPrototype.unity`

## การใช้งาน

กด Play แล้วระบบจะสุ่มมอนสเตอร์และรังทั้งชุดใหม่ โดยตั้งค่าที่
`World > Map > World_Monster_Population > World Monster Director` ใน Inspector

| มอนสเตอร์ | จำนวนเริ่มต้น | EXP ต่อตัว | พื้นที่ |
|---|---:|---:|---|
| Spider | 5 | 25 | ป่า/พื้นที่ทั่วไป |
| Bear | 3 | 140 | ป่า/พื้นที่ทั่วไป |
| Imp Green | 3 | 40 | พื้นที่ทั่วไป |
| Imp Blue | 3 | 60 | พื้นที่ทั่วไป |
| Imp Red | 2 | 90 | ฝั่งซากปรัก/จอมมาร |
| Puglin | 2 | 160 | พื้นที่ทั่วไป |
| Skeleton | 4 | 75 | ฝั่งซากปรัก/จอมมาร |
| Goblin demon | 12 | 100 | 4 ตัวต่อรัง |
| Goblin chief | 3 | 220 | 1 ตัวต่อรัง |

รวมมอนสเตอร์ทั่วไป 37 ตัว บอส KingOfDemon, DemonLord และ DragonNightmare ใช้ตำแหน่งและระบบเดิม
`EnemyBase` เป็นแม่แบบ และ `EnemyDummy` เป็นเป้าฝึก จึงไม่รวมในการสุ่ม

## พฤติกรรม

- ใช้ AI เดิม: เดินลาดตระเวน มองเห็นด้านหน้า ไล่ล่า จำตำแหน่งที่เห็นล่าสุด ค้นหา และกลับถิ่น
- มีจุดสุ่มเดินกระจายที่ตรวจ NavMesh แล้ว 180 จุด และจุดสำหรับรัง 20 จุด
- เว้นพื้นที่เมือง หมู่บ้าน แท่นอัญเชิญ พื้นที่บอส ขอบแผนที่ และพื้นต่ำในแม่น้ำ
- รังมีเต็นท์หัวหน้า เต็นท์เล็ก 2 หลัง กองไฟ หอสังเกตการณ์ โทเท็ม และกรง จาก VKI/Prop
- พร็อพรังวางแนบพื้น มี BoxCollider และ NavMeshObstacle แบบ carving
- สุ่มแบบรังจาก prefab 3 แบบ พร้อมตำแหน่งและการหมุนของรังทั้งชุด
- มอนสเตอร์เดี่ยวตายแล้วรอ 45 วินาที จึงเติมกลับเมื่อผู้เล่นห่างอย่างน้อย 18 เมตรและอยู่นอกกล้อง
- สมาชิก Goblin ที่ตายจะไม่เกิดเติมทีละตัว รอจนหมดรัง แล้วพัก 150 วินาที
- เมื่อผู้เล่นอยู่ห่างอย่างน้อย 32 เมตร และรังเดิมอยู่นอกกล้อง จึงย้ายไปสุ่มรังใหม่ทั้งชุดในพื้นที่ว่าง
- กำแพงและแม่น้ำไม่ถูกแก้ไข เขตปลอดการสุ่มเป็นเพียงข้อจำกัดตำแหน่งเกิด มอนสเตอร์ยังไล่ตามเป้าหมายได้ตาม AI เดิม
- ปิด component `EnemyWaveSpawner` ในฉากนี้แล้ว เก็บข้อมูล wave เดิมไว้

## ปรับแต่ง

- `Species`: prefab, จำนวน และ habitat ของมอนสเตอร์ที่เดินกระจาย
- `Camp Population`: จำนวนรังเริ่มต้น (3)
- `Camp Templates`: prefab รังใน `Assets/_Project/Enemies/WorldPopulation/Camps/`
- `Roaming Respawn Delay`, `Camp Respawn Delay`: เวลาก่อนเติมมอนสเตอร์/สุ่มรังใหม่
- `Minimum Player Distance`, `Camp Player Distance`: ระยะปลอดภัยก่อนเกิด
- `Seed = 0`: สุ่มใหม่ทุกครั้ง; ใส่เลขอื่นเพื่อทดสอบแบบสุ่มเดิมซ้ำ
- จุดสุ่มอยู่ใต้ `Roaming Sites` และ `Camp Clearing Sites` สามารถขยับหรือลบจุดจากรายการได้
- เขต `Monster Spawn Exclusion` อยู่ใต้เมือง/หมู่บ้าน และเขตอื่นอยู่ใต้จุดเกิด/arena จึงเคลื่อนตาม parent

ถ้าปรับ terrain หรือย้ายจุดสุ่ม ควรตรวจ NavMesh อีกครั้ง ระบบจะงดเกิดในจุดที่เดินไม่ได้หรือมีสิ่งกีดขวาง
จำนวนเริ่มต้นและ prefab ควรแก้ใน Edit Mode ก่อนกด Play

## โมเดลใหม่

**Goblin demon:** ใช้ rig และแอนิเมชัน Generic ของ Charttyp เดิม มี Idle, Run_weapon และ Attack_2
ตอนลาดตระเวนใช้ Run_weapon ที่ลดความเร็วลง ชุด demo ไม่มีท่าตาย จึงใช้การล้มตัวอย่างนุ่มนวลแทน
เสียงพูด/โจมตี/ตายใช้ไฟล์จาก `Assets/_Project/Audio/Mobs/Goblin/` หัวหน้าใช้โมเดลเดียวกัน ขยาย 1.15 เท่า

**Skeleton:** ใช้ `PT_Skeleton_Male_Modular` ซึ่งมี Humanoid avatar ที่ใช้งานได้
สร้าง material URP/Lit สีกระดูกให้สำเนามอนสเตอร์ แก้ปัญหาสีชมพูจาก shader Built-in เดิม
ใช้ Humanoid Idle/Walk/Run/Death ที่มีในโปรเจกต์ และ Punch_Cross สำหรับโจมตีมือเปล่า
ต้นฉบับแพ็กโมเดลและวัสดุไม่ถูกแก้ไข

## ระบบของตก

ต่อระบบดรอปและดูดไอเทมเข้ากระเป๋าแล้ว ดู [MONSTER_DROPS_AND_MAGNET.md](MONSTER_DROPS_AND_MAGNET.md)

- `WorldMonsterMember` เก็บ `SpeciesId`, `CampId`, `Health`, `Defeated`
- `WorldMonsterDirector.MonsterDefeated` รับเหตุการณ์ตายครั้งเดียวพร้อมตัวมอนสเตอร์และตำแหน่ง ใช้ต่อ quest/สถิติได้
- ไม่ส่งเหตุการณ์นี้ตอนลบรังเก่าหรือ despawn
- EXP มาจาก `EnemyHealth.TakeDamage(damage, player)` เดิม จึงไม่ต้องเพิ่ม EXP ซ้ำในระบบ drop
- `EnemyLootDropper` ใช้ `EnemyHealth.Died` ของแต่ละตัวเป็นจุดดรอปเดียว รวมถึงบอส ไม่ต้องสร้างดรอปซ้ำผ่าน director

## การตรวจสอบ

เมนู `Tools > Enemies > World Population > Run Population Camp and Combat Test`
ตรวจจำนวนทุกชนิด, NavMesh, เขตห้ามสุ่ม, การแนบพื้นและ collision ของรัง, ท่าเดิน/วิ่ง/โจมตี,
โจมตีผู้เล่นจริง, ตาย/EXP, เติมมอนสเตอร์, เคลียร์และย้ายรังทั้งชุด พร้อมคืนค่า EXP ของผู้เล่นหลังทดสอบ

ผลตรวจและภาพตัวอย่างเก็บใน `Logs/WorldMonsterPlaytest.txt`, `Logs/WorldMonsterValidation.txt`
และ `Logs/WorldMonster_Camp_Play.png`
